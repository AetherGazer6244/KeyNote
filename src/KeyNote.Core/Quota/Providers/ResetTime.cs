using System.Globalization;
using System.Text.RegularExpressions;
using KeyNote.Core.Quota;

namespace KeyNote.Core.Quota.Providers;

/// <summary>
/// 重置时间的归一化与倒计时文案。
///
/// 各家 provider 的重置字段语义不统一:
/// - 智谱 nextResetTime:绝对 Unix 毫秒
/// - OpenCode Go resetsAt:ISO 时间串
/// - MiniMax remains_time / reset_time:相对毫秒 或 绝对秒/毫秒(量级判别)
///
/// 统一归一成**绝对 Unix 毫秒**存进 ProviderResult.ResetAtMs,
/// UI 只认这一种,倒计时文案由 FormatLeft 现算(轮询刷新即更新)。
/// </summary>
public static class ResetTime
{
    /// <summary>数值型重置字段 → 绝对 Unix 毫秒。量级判别:
    /// ≥1e11 绝对毫秒;[1e9,1e11) 绝对秒;其余相对毫秒(从现在起算)。</summary>
    public static long? FromNumeric(double v)
    {
        if (v <= 0) return null;
        if (v >= 1e11) return (long)v;
        if (v >= 1e9) return (long)(v * 1000.0);
        return NowMs() + (long)v;
    }

    /// <summary>ISO 时间串 → 绝对 Unix 毫秒。</summary>
    public static long? FromIso(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var text = s.Trim();
        if (text.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            text = text[..^1] + "+00:00";
        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
            return null;
        return dt.ToUnixTimeMilliseconds();
    }

    /// <summary>当前 Unix 毫秒。</summary>
    public static long NowMs() => (long)(Http.Now() * 1000.0);

    /// <summary>距重置还有多久(本地化短文案);已过期/无值返回 null。</summary>
    public static string? FormatLeft(long resetAtMs)
    {
        var ms = resetAtMs - NowMs();
        if (ms <= 0) return null;
        // 四舍五入到秒:截断会把 2天3时 打成 2天2时59分(几毫秒的误差就掉一小时)
        var sec = (long)Math.Round(ms / 1000.0);
        var d = sec / 86400;
        var rest = sec % 86400;
        var h = rest / 3600;
        var m = rest % 3600 / 60;
        if (d > 0) return L.F("fmt.leftDays", d, h);
        if (h > 0) return L.F("fmt.leftHours", h, m);
        return L.F("fmt.leftMinutes", m);
    }

    /// <summary>绝对时间的本地显示(yyyy-MM-dd HH:mm)。</summary>
    public static string FormatAbsolute(long resetAtMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(resetAtMs)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// 把 detail 末尾的"重置"后缀剥掉(UI 上重置时间已单独用 chip 展示)。
    /// 只剥尾部整段(" · 重置 2d03h" / " · resets 2h30m"),
    /// 段内括号形式("(重置2d03h)")保留——那是各额度窗口自己的重置时间,
    /// chip 只覆盖主窗口,剥了就丢信息。
    /// </summary>
    public static string StripResetNote(string? detail)
    {
        if (string.IsNullOrEmpty(detail)) return detail ?? "";
        var marker = L.T("fmt.resetWord");
        if (string.IsNullOrEmpty(marker)) return detail;
        var s = Regex.Replace(
            detail,
            @"\s*[·|]\s*" + Regex.Escape(marker) + @"[^\[\|·]*$",
            "");
        return s.Trim();
    }
}
