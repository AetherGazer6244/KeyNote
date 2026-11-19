using KeyNote.App.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace KeyNote.App.Services;

/// 额度域画刷解析(vigil-watt 同款适配思路):AgentEye 原版走 App.xaml 的 Ae*Brush
/// 主题字典;KeyNote 暗色锁定、色板唯一真源在 UiColors,这里改为静态映射,
/// Brush/BrushOrNull 签名保持不变,移植的页面与小组件零改动。
public static class QuotaRes
{
    /// Ae*Brush 键 → UiColors 语义色(KeyNote 暗色令牌)。
    private static readonly Dictionary<string, Windows.UI.Color> Map = new()
    {
        ["AeOkBrush"] = UiColors.QuotaOk,
        ["AeWarnBrush"] = UiColors.QuotaWarn,
        ["AeCriticalBrush"] = UiColors.QuotaCritical,
        ["AeErrorBrush"] = UiColors.Danger,
        ["AePausedBrush"] = UiColors.QuotaPaused,
        ["AeTextPrimaryBrush"] = UiColors.FgPrimary,
        ["AeTextSecondaryBrush"] = UiColors.FgSecondary,
        ["AeSubtleFillBrush"] = UiColors.BgSecondary,
        ["AeCardBrush"] = UiColors.BgSecondary,
        ["AeCardStrokeBrush"] = UiColors.Border,
        // Acrylic 小组件玻璃面:卡片底压淡,让 acrylic 直透
        ["AeGlassTintBrush"] = Windows.UI.Color.FromArgb(0x66, UiColors.BgSecondary.R, UiColors.BgSecondary.G, UiColors.BgSecondary.B),
    };

    private static Brush Cached(Windows.UI.Color c) => new SolidColorBrush(c);

    public static Brush Brush(FrameworkElement el, string key, Brush? fallback = null) =>
        Map.TryGetValue(key, out var c) ? Cached(c) : fallback ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);

    /// 拿不到返回 null(用于"没有描边"这类合法缺省)。
    public static Brush? BrushOrNull(FrameworkElement el, string key) =>
        Map.TryGetValue(key, out var c) ? Cached(c) : null;
}
