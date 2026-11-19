using KeyNote.App.Theme;
using KeyNote.Core.Devices.Models;
using Windows.UI;

namespace KeyNote.App.Services;

/// 设备域统一状态色板(vigil-watt 移植适配层):色值唯一真源在 Theme/UiColors.cs,
/// 这里只做设备语义 → UiColors 令牌的映射与少数派生色,供设备页/小组件/贴片/图表共用。
public static class UiPalette
{
    public static readonly Color Charging = UiColors.Accent;
    public static readonly Color Low = UiColors.Danger;
    public static readonly Color Mid = UiColors.Warning;
    public static readonly Color Good = UiColors.Success;
    public static readonly Color Offline = UiColors.Offline;

    // ──── 来源徽标色:蓝牙=蓝 / 2.4G=紫 / 本机=青,与电量状态色(红琥珀绿蓝灰)刻意错开 ────
    public static readonly Color SourceBluetooth = UiColors.Accent;
    public static readonly Color Source24G = UiColors.Source24G;
    public static readonly Color SourceSystem = UiColors.SourceSystem;

    /// 来源 → 徽标主色(文字色;胶囊底色用 Tint 压淡)。
    public static Color SourceTag(DeviceSource source) => source switch
    {
        DeviceSource.Ble or DeviceSource.BluetoothClassic => SourceBluetooth,
        DeviceSource.System => SourceSystem,
        _ => Source24G,
    };

    /// 同色半透明底(徽标胶囊背景),默认约 16% 不透明度。
    public static Color Tint(Color color, byte alpha = 0x28) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    /// 多系列图表循环取色(首位为主系列色)。
    public static readonly Color[] SeriesColors =
    [
        Charging,
        Good,
        Mid,
        Color.FromArgb(255, 0xAF, 0x52, 0xDE),
        Color.FromArgb(255, 0x5E, 0xCE, 0xF1),
        Color.FromArgb(255, 0xFF, 0x9F, 0x0A),
    ];

    /// 电量状态 → 状态色。离线(IsOnline=false)由调用方直接用 Offline。
    public static Color BatteryLevel(int? percent, bool charging)
    {
        if (charging) return Charging;
        return percent switch
        {
            null => Offline,
            <= 20 => Low,
            <= 50 => Mid,
            _ => Good,
        };
    }

    public static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(Color color) => new(color);

    /// 托盘图标 / 任务栏贴片走 GDI+,需要 System.Drawing 的颜色类型。
    public static System.Drawing.Color ToDrawing(Color color) =>
        System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}
