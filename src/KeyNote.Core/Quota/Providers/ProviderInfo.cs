namespace KeyNote.Core.Quota.Providers;

/// <summary>
/// provider 静态资料(官网等)。Core 只放纯数据,UI 拿 URL 去开浏览器。
/// </summary>
public static class ProviderInfo
{
    /// <summary>provider 官网;自定义中转/兼容站没有固定官网,返回 null。</summary>
    public static string? OfficialUrl(string? kind) => kind switch
    {
        "deepseek" => "https://platform.deepseek.com",
        "zhipu" => "https://open.bigmodel.cn",
        "minimax" => "https://platform.minimaxi.com",
        "opencode_go" => "https://opencode.ai",
        "siliconflow" => "https://cloud.siliconflow.cn",
        "openrouter" => "https://openrouter.ai",
        "yuanxuai" => "https://token.yuanxuai.xyz",
        _ => null,
    };
}
