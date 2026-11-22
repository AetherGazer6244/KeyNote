using System.Text.Json;
using KeyNote.Core.Quota;

namespace KeyNote.Core.Quota.Providers;

/// <summary>元序钱包额度:Bearer Key 查询 /v1/usage,并尽力读取 OpenAI 兼容模型列表。</summary>
public static class YuanxuProvider
{
    public const string DefaultBaseUrl = "https://token.yuanxuai.xyz/v1";

    private static string V1Base(string? value)
    {
        var bas = string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.Trim().TrimEnd('/');
        return bas.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? bas : bas + "/v1";
    }

    public static async Task<FetchPayload> FetchAsync(
        GenericOpenAi.LegacyEntry e, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(e.ApiKey))
            return FetchPayload.Err(L.T("pr.noKey"), unconfigured: true);

        var bas = V1Base(e.BaseUrl);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {e.ApiKey.Trim()}",
            ["Accept"] = "application/json",
        };
        var (ok, status, body, why) = await Http.GetJsonAsync(
            bas + "/usage", headers, Http.DefaultTimeoutSec, ct);
        if (!ok) return FetchPayload.Err(Http.NetErrText(why));
        if (status is 401 or 403) return FetchPayload.Err(L.F("pr.keyInvalid", status));
        if (status != 200) return FetchPayload.Err($"HTTP {status}");
        if (!Http.IsObj(body)) return FetchPayload.Err(L.T("pr.notJson"));

        var parsed = ParseUsage(body);
        if (parsed.Error is not null) return parsed;
        parsed.QuotaEndpoint = "/usage";

        var models = await FetchModelsAsync(bas, headers, ct);
        if (models is { Count: > 0 })
        {
            parsed.Models = models;
            parsed.ModelsCount = models.Count;
        }
        return parsed;
    }

    /// <summary>解析 usage 响应的纯逻辑入口,便于无需发请求验证不同响应形态。</summary>
    public static FetchPayload ParseUsage(JsonElement body)
    {
        if (!Http.IsObj(body)) return FetchPayload.Err(L.T("pr.structUnknown"));
        if (Http.Obj(body, "isValid") is { ValueKind: JsonValueKind.False })
            return FetchPayload.Err(L.T("yx.keyInvalid"));

        var remaining = Http.Num(body, "balance") ?? Http.Num(body, "remaining");
        if (remaining is null) return FetchPayload.Err(L.T("pr.structUnknown"));

        var rawUnit = Http.Str(body, "unit") ?? "USD";
        var unit = rawUnit.Equals("USD", StringComparison.OrdinalIgnoreCase) || rawUnit == "$"
            ? "$"
            : rawUnit.Equals("CNY", StringComparison.OrdinalIgnoreCase) || rawUnit == "¥"
                ? "¥"
                : rawUnit + " ";

        var usage = Http.Obj(body, "usage");
        var today = usage is { ValueKind: JsonValueKind.Object }
            ? Http.Obj(usage.Value, "today") : null;
        var total = usage is { ValueKind: JsonValueKind.Object }
            ? Http.Obj(usage.Value, "total") : null;
        var todayCost = today is { ValueKind: JsonValueKind.Object }
            ? Http.Num(today.Value, "actual_cost") : null;
        var totalCost = total is { ValueKind: JsonValueKind.Object }
            ? Http.Num(total.Value, "actual_cost") : null;
        var todayRequests = today is { ValueKind: JsonValueKind.Object }
            ? Http.Num(today.Value, "requests") : null;
        var totalRequests = total is { ValueKind: JsonValueKind.Object }
            ? Http.Num(total.Value, "requests") : null;

        var detail = new List<string>();
        var planName = Http.Str(body, "planName");
        if (!string.IsNullOrWhiteSpace(planName)) detail.Add(planName);
        if (todayCost is { } tc)
        {
            var todayLine = L.F("yx.today", unit, Http.F2(tc));
            if (todayRequests is { } tr) todayLine += L.F("yx.requests", (long)tr);
            detail.Add(todayLine);
        }
        if (totalCost is { } totalUsed)
        {
            var totalLine = L.F("yx.total", unit, Http.F2(totalUsed));
            if (totalRequests is { } requests) totalLine += L.F("yx.requests", (long)requests);
            detail.Add(totalLine);
        }

        return new FetchPayload
        {
            Remaining = Math.Max(0, remaining.Value),
            Used = totalCost,
            UsedToday = todayCost,
            Total = null,
            Unit = unit,
            Pct = null,
            Detail = string.Join(" · ", detail),
            IsEstimate = false,
        };
    }

    private static async Task<List<string>?> FetchModelsAsync(
        string bas, Dictionary<string, string> headers, CancellationToken ct)
    {
        var (ok, status, body, _) = await Http.GetJsonAsync(
            bas + "/models", headers, Http.DefaultTimeoutSec, ct);
        if (!ok || status != 200 || !Http.IsObj(body)) return null;
        var raw = Http.Obj(body, "data");
        if (raw is not { ValueKind: JsonValueKind.Array }) return null;

        var models = new List<string>();
        foreach (var item in raw.Value.EnumerateArray())
        {
            if (!Http.IsObj(item)) continue;
            var id = Http.Str(item, "id") ?? Http.Str(item, "name") ?? Http.Str(item, "model");
            if (!string.IsNullOrWhiteSpace(id)) models.Add(id);
        }
        return models;
    }
}
