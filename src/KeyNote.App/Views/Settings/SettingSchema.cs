using KeyNote.App.Services;

namespace KeyNote.App.Views;

/// 设置项声明表:字段种类 + 元数据(扩字段只加一行)。
public sealed partial class SettingsPage
{
    private enum FieldKind { Switch, Slider, Combo, Number, Path, Action, Info, Hotkey }

    private sealed record FieldSpec(
        FieldKind Kind,
        string Key,
        string Title,
        string Desc,
        double Lo = 0,
        double Hi = 0,
        double Step = 0,
        string[]? Options = null,
        int[]? Values = null,
        string? ActionKey = null,
        bool Advanced = false,
        string? Value = null);

    private static readonly (string Name, FieldSpec[] Fields)[] Groups =
    {
        ("启动与常规", new[]
        {
            new FieldSpec(FieldKind.Switch, "auto_start", "开机自启动", "系统登录时自动启动 KeyNote,持续后台记录按键。"),
            new FieldSpec(FieldKind.Switch, "silent_start", "静默启动", "完全静默启动,无窗口、无托盘通知(适合后台常驻)。"),
            new FieldSpec(FieldKind.Switch, "start_minimized", "启动时最小化", "开机后直接驻留托盘,不弹出主窗口。"),
            new FieldSpec(FieldKind.Switch, "close_to_tray", "关闭窗口到托盘", "点关闭按钮时最小化到托盘而不是退出程序。"),
            new FieldSpec(FieldKind.Switch, "hide_tray_icon", "隐藏托盘图标", "隐藏后重启仍保持隐藏;打开主窗口,回到本页关闭此项即可恢复显示。", Advanced: true),
        }),
        ("界面", new[]
        {
            new FieldSpec(FieldKind.Switch, "remember_window_pos", "记住窗口位置", "记住主窗口位置,下次启动恢复。"),
        }),
        ("设备电量", new[]
        {
            new FieldSpec(FieldKind.Switch, "device_monitor_system_battery", "本机电池监控", "把电脑自带电池也纳入电量列表,无无线设备时也有数据可看。"),
            new FieldSpec(FieldKind.Slider, "device_low_battery_threshold", "低电提醒阈值", "设备电量低于该值时经托盘气泡提醒。", Lo: 5, Hi: 50, Step: 1),
            new FieldSpec(FieldKind.Switch, "device_remaining_time_enabled", "续航估算", "按近期耗电速度估算无线设备剩余可用时间。"),
            new FieldSpec(FieldKind.Slider, "device_sample_interval_min", "电量采样间隔", "每隔多少分钟记一条电量历史(设备页曲线的疏密)。", Lo: 1, Hi: 30, Step: 1),
            new FieldSpec(FieldKind.Combo, "device_history_retention_days", "电量历史保留", "设备电量原始样本的保留时长,到期自动清理。", Options: ["30 天", "90 天", "180 天", "365 天", "730 天"], Values: [30, 90, 180, 365, 730]),
            new FieldSpec(FieldKind.Switch, "device_widget_enabled", "桌面悬浮小组件", "在桌面显示在线设备的电量角标,可拖动、右键菜单管理。"),
            new FieldSpec(FieldKind.Switch, "device_taskbar_pill_enabled", "任务栏电量贴片", "在任务栏常驻低电设备电量贴片,双击唤起主窗口。"),
        }),
        ("设备插件", DevicePluginFields()),
        ("AI 额度", new[]
        {
            new FieldSpec(FieldKind.Switch, "quota_alert_enabled", "额度告警", "provider 剩余额度跨过阈值时弹系统通知(同等间隔 60 分钟冷却,一轮合并一条)。"),
            new FieldSpec(FieldKind.Number, "quota_warn_pct", "关注阈值", "额度用量百分比达到该值时按 warn 提醒。", Lo: 0, Hi: 100, Step: 1),
            new FieldSpec(FieldKind.Number, "quota_critical_yuan", "余额告警线", "余额类 provider 低于该金额(元)时按 critical 提醒。", Lo: 0, Hi: 500, Step: 1),
            new FieldSpec(FieldKind.Number, "quota_refresh_interval", "刷新间隔(秒)", "轮询 provider 配额接口的周期,15~3600 秒。", Lo: 15, Hi: 3600, Step: 5),
            new FieldSpec(FieldKind.Switch, "quota_adaptive_poll", "自适应轮询", "有 critical 行时间隔减半、持续失败指数退避、全正常放缓 ×1.5。"),
            new FieldSpec(FieldKind.Switch, "quota_widget_enabled", "额度悬浮小组件", "在桌面常驻额度行悬浮条(Acrylic 玻璃面,可拖动)。"),
            new FieldSpec(FieldKind.Switch, "quota_hotkey_enabled", "全局快捷键", "启用后按组合键随时唤起/收起额度小组件。"),
            new FieldSpec(FieldKind.Hotkey, "quota_hotkey", "快捷键组合", "点输入框后直接按下组合键录入;也可手打如 Ctrl+Shift+A,空 = 不设组合。"),
        }),
        ("数据", new[]
        {
            new FieldSpec(FieldKind.Action, "backup_now", "立即备份", "对当前数据库做一致性快照。", ActionKey: "backup", Advanced: true),
            new FieldSpec(FieldKind.Action, "export_csv", "导出 CSV", "导出最近 30 天原始按键记录到数据目录。", ActionKey: "csv", Advanced: true),
            new FieldSpec(FieldKind.Combo, "db_retention_days", "保留 raw 数据", "原始按键记录的保留时长,到期自动清理(聚合表永久保留)。",
                Options: ["永久", "7 天", "180 天", "365 天", "730 天"], Advanced: true),
            new FieldSpec(FieldKind.Path, "backup_dir", "备份目录", "备份 .db 文件的目标目录,空 = 使用数据目录下的 backups/。", Advanced: true),
            new FieldSpec(FieldKind.Action, "vacuum_now", "立即 VACUUM", "重建数据库回收空间。", ActionKey: "vacuum", Advanced: true),
        }),
        ("关于", new[]
        {
            new FieldSpec(FieldKind.Info, "app_version", "当前版本", "程序与安装包版本信息。", Value: $"v{UpdateService.CurrentVersionText}"),
            new FieldSpec(FieldKind.Action, "check_update", "检查更新", "查询 GitHub Releases 是否有新版本;发现新版后可下载安装。", ActionKey: "update"),
        }),
    };

    private static readonly int[] RetentionValues = [0, 7, 180, 365, 730];

    // ──── 设备插件开关组(三合一整合) ────
    /// 插件开关从 DeviceSettings 默认表生成,默认值全盘继承 vigil(会写帧的协议一律默认关)。
    private static FieldSpec[] DevicePluginFields()
    {
        var s = Core.Devices.Settings.AppSettings.Load();
        return s.PluginEnabled
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new FieldSpec(FieldKind.Switch, $"device_plugin_{kv.Key}",
                PluginTitle(kv.Key), PluginDesc(kv.Key)))
            .ToArray();
    }

    private static string PluginTitle(string name) => name switch
    {
        "Logitech" => "罗技 (Logitech)",
        "Mchose" => "迈从 (Mchose)",
        "Rapoo" => "雷柏 (Rapoo)",
        "Betop" => "北通 (Betop)",
        "Ajazz" => "黑爵 (Ajazz)",
        "Thunderobot" => "雷神 (Thunderobot)",
        "Inphic" => "英菲克 (Inphic)",
        "Darkrim" => "Darkrim",
        "GameSir" => "盖世小鸡 (GameSir)",
        "Hecate" => "漫步者 HECATE 有线 (Hecate)",
        "HecateWireless" => "漫步者无线接收器 (HecateWireless)",
        "Delux" => "多彩 (Delux)",
        "Aula" => "狼蛛 (Aula)",
        "Akg" => "AKG",
        "DualSense" => "PS5 手柄 (DualSense)",
        "Ganss" => "GANSS",
        "Cherry" => "CHERRY",
        "Pulsar" => "Pulsar",
        "Eweadn" => "前行者 (Eweadn)",
        "Razer" => "雷蛇 (Razer)",
        "SonyInzone" => "索尼 INZONE (SonyInzone)",
        "Atk" => "ATK",
        "HyperX" => "HyperX",
        _ => name,
    };

    /// 开关说明与 vigil 默认表注释一一对应:默认关的插件查询电量都要写帧。
    private static string PluginDesc(string name) => name switch
    {
        "Logitech" => "罗技 HID++ 非官方协议,会写帧,安全起见默认关。",
        "Mchose" => "迈从 dongle 协议未公开,探测会写帧,默认关。",
        "Rapoo" => "只读取不写帧,默认开。",
        "Betop" => "官方同步只做状态查询,默认开。",
        "Ajazz" => "需写帧查询电量,默认关。",
        "Thunderobot" => "需写帧查询电量,默认关。",
        "Inphic" => "report 0x03 只读不写帧,默认开。",
        "Darkrim" => "官方同源协议需写帧查询,默认关。",
        "GameSir" => "需写帧握手才能通,默认关。",
        "Hecate" => "状态帧只读不写帧,默认开。",
        "HecateWireless" => "查询需写帧,默认关。",
        "Delux" => "与英菲克同 VID/PID,协议未验证,默认关。",
        "Aula" => "轮询需写帧,默认关。",
        "Akg" => "轮询需写帧,默认关。",
        "DualSense" => "HID 只读不动手,默认开。",
        "Ganss" => "轮询需写帧,默认关。",
        "Cherry" => "开通握手需写帧,默认关。",
        "Pulsar" => "查询需写帧,默认关。",
        "Eweadn" => "协议需写查询帧,默认关。",
        "Razer" => "feature 查询需写帧,默认关。",
        "SonyInzone" => "控制传输查询需写帧,默认关。",
        "Atk" => "查询需写帧,默认关。",
        "HyperX" => "只读不写帧,默认开。",
        _ => "开关该品牌的电量探测。",
    };
}
