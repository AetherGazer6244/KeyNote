namespace KeyNote.Core.Quota.Providers;

/// <summary>
/// 功 能： 把上游 API 返回的原始错误文本映射成人话。只做关键词模式匹配，
///         匹配不到返回原文（过长截断），避免为覆盖所有错误维护一张巨表。
///         典型案例：智谱返回「当前用户不存在coding plan」，用户看到只会更困惑。
/// 2026/11/21 xliu
/// </summary>
public static class ApiErrorText
{
    /// <summary>单条错误文本的最大展示长度。</summary>
    private const int MaxLength = 80;

    public static string Humanize(string? raw)
    {
        var s = raw?.Trim() ?? "";
        if (s.Length == 0) return "未知错误";

        var hit = Match(s);
        var text = hit ?? s;
        return text.Length > MaxLength ? text[..MaxLength] + "…" : text;
    }

    private static string? Match(string s)
    {
        // 忽略大小写的英文关键词统一走小写比较
        var lower = s.ToLowerInvariant();

        if (lower.Contains("coding plan"))
            return "该账号未开通 Coding Plan 套餐，当前 API Key 不适用于此接口";

        if (lower.Contains("invalid_api_key") || lower.Contains("invalid api key") ||
            lower.Contains("无效的密钥") || lower.Contains("无效的 api") || lower.Contains("key 无效") ||
            lower.Contains("apikey不正确") || lower.Contains("authentication"))
            return "API Key 无效或已失效，请重新生成";

        if (lower.Contains("insufficient") || lower.Contains("余额不足") ||
            lower.Contains("quota exceeded") || lower.Contains("额度不足") || lower.Contains("欠费"))
            return "账户余额或额度不足";

        if (lower.Contains("rate limit") || lower.Contains("too many requests") ||
            lower.Contains("限速") || lower.Contains("请求过于频繁") || lower.Contains("限流"))
            return "请求过于频繁，已被限流，稍后自动重试";

        if (lower.Contains("timeout") || lower.Contains("timed out") || lower.Contains("超时"))
            return "上游请求超时";

        if (lower.Contains("unauthorized") || lower.Contains("forbidden"))
            return "鉴权失败：API Key 无权限访问该接口";

        if (lower.Contains("model") && (lower.Contains("not exist") || lower.Contains("not found") || lower.Contains("不存在")))
            return "模型名不存在或无权访问";

        return null;
    }
}
