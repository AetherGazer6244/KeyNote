using System.Collections.Concurrent;
using KeyConfig = KeyNote.Core.Settings.AppSettings;

namespace KeyNote.Core.Devices.Settings;

/// 设备域设置:VigilWatt AppSettings 的键空间适配层。
/// 标量与插件开关落在 config.json 扁平键(device_ 前缀),设备状态字典落在 device_state.json。
/// 进程内单例:DeviceManager / 插件经 Load() 拿到的始终是同一实例,黑/忽略名单的增删直接改本实例再 Save()。
public sealed class AppSettings
{
    private static readonly object Gate = new();
    private static AppSettings? _instance;

    /// 插件开关默认表(会写帧的协议一律默认关闭,安全约束不因整合放开)。
    private static readonly Dictionary<string, bool> PluginDefaults = new()
    {
        ["Logitech"] = false,
        ["Mchose"] = false,
        ["Rapoo"] = true,
        ["Betop"] = true,
        ["Ajazz"] = false,
        ["Thunderobot"] = false,
        ["Inphic"] = true,
        ["Darkrim"] = false,
        ["GameSir"] = false,
        ["Hecate"] = true,
        ["HecateWireless"] = false,
        ["Delux"] = false,
        ["Aula"] = false,
        ["Akg"] = false,
        ["DualSense"] = true,
        ["Ganss"] = false,
        ["Cherry"] = false,
        ["Pulsar"] = false,
        ["Eweadn"] = false,
        ["Razer"] = false,
        ["SonyInzone"] = false,
        ["Atk"] = false,
        ["HyperX"] = true,
    };

    // ──── 标量(config.json 扁平键) ────
    public int LowBatteryThreshold { get; set; } = 20;
    public bool RemainingTimeEstimateEnabled { get; set; } = true;
    public int SampleIntervalMinutes { get; set; } = 5;
    public int HistoryRetentionDays { get; set; } = 90;
    public bool MonitorSystemBattery { get; set; }
    public bool WidgetEnabled { get; set; }
    public double WidgetX { get; set; } = 60;
    public double WidgetY { get; set; } = 60;
    public int WidgetMaxDevices { get; set; } = 3;
    public string WidgetDisplayMode { get; set; } = "Normal";
    public bool TaskbarBatteryEnabled { get; set; }
    public string TaskbarBatteryMode { get; set; } = "All";
    public int TaskbarBatteryMaxDevices { get; set; } = 3;
    public int TaskbarBatteryOffsetX { get; set; }
    public int TaskbarBatteryOffsetDpi { get; set; }
    public string TaskbarBatteryScreen { get; set; } = "";
    public string TaskbarBatteryScreenLabel { get; set; } = "";
    public string LogLevel { get; set; } = "Info";
    public int LogRetentionDays { get; set; } = 14;

    // ──── 插件开关(config.json device_plugin_<Name>) ────
    public Dictionary<string, bool> PluginEnabled { get; private set; } = new(PluginDefaults);

    // ──── 设备状态字典(device_state.json) ────
    public Dictionary<string, DeviceCustomization> DeviceCustomizations { get; private set; } = new();
    public ConcurrentDictionary<string, BlockedDevice> BlockedDevices { get; } = new();
    public ConcurrentDictionary<string, long> DismissedDevices { get; } = new();
    public ConcurrentDictionary<string, IgnoredDevice> IgnoredDevices { get; } = new();

    private DeviceState _state = new();

    /// 最近一次 Save 失败原因(空 = 没失败过;UI 据此提示)。
    public static string LastSaveError { get; private set; } = "";

    // ──── 加载/保存 ────
    public static AppSettings Load()
    {
        lock (Gate)
        {
            if (_instance is { } s) return s;
            s = new AppSettings();
            s.SyncFromConfig(KeyConfig.Load());
            s.LoadState();
            _instance = s;
            return s;
        }
    }

    /// 设置页改动 config.json 后调用:重读标量与插件开关到本实例(设备状态字典保持内存态不被覆盖)。
    public void SyncFromConfig(KeyConfig cfg)
    {
        LowBatteryThreshold = cfg.GetInt("device_low_battery_threshold", 20);
        RemainingTimeEstimateEnabled = cfg.GetBool("device_remaining_time_enabled", true);
        SampleIntervalMinutes = Math.Max(1, cfg.GetInt("device_sample_interval_min", 5));
        HistoryRetentionDays = Math.Max(0, cfg.GetInt("device_history_retention_days", 90));
        MonitorSystemBattery = cfg.GetBool("device_monitor_system_battery", false);
        WidgetEnabled = cfg.GetBool("device_widget_enabled", false);
        WidgetX = cfg.GetDouble("device_widget_x", 60);
        WidgetY = cfg.GetDouble("device_widget_y", 60);
        WidgetMaxDevices = Math.Clamp(cfg.GetInt("device_widget_max", 3), 1, 5);
        WidgetDisplayMode = cfg.GetString("device_widget_mode", "Normal");
        TaskbarBatteryEnabled = cfg.GetBool("device_taskbar_pill_enabled", false);
        TaskbarBatteryMode = cfg.GetString("device_taskbar_pill_mode", "All");
        TaskbarBatteryMaxDevices = Math.Clamp(cfg.GetInt("device_taskbar_pill_max", 3), 1, 5);
        TaskbarBatteryOffsetX = cfg.GetInt("device_taskbar_pill_offset_x", 0);
        TaskbarBatteryOffsetDpi = cfg.GetInt("device_taskbar_pill_offset_dpi", 0);
        TaskbarBatteryScreen = cfg.GetString("device_taskbar_pill_screen", "");
        TaskbarBatteryScreenLabel = cfg.GetString("device_taskbar_pill_screen_label", "");
        LogLevel = cfg.GetString("device_log_level", "Info");
        LogRetentionDays = cfg.GetInt("device_log_retention_days", 14);

        var plugins = new Dictionary<string, bool>(PluginDefaults);
        foreach (var name in plugins.Keys.ToList())
            plugins[name] = cfg.GetBool($"device_plugin_{name}", PluginDefaults[name]);
        PluginEnabled = plugins;
    }

    /// 把内存态标量与插件开关写回 config.json;设备状态字典写 device_state.json。返回是否全部成功。
    public bool Save()
    {
        LastSaveError = "";
        try
        {
            var cfg = KeyConfig.Load();
            cfg.Set("device_low_battery_threshold", LowBatteryThreshold);
            cfg.Set("device_remaining_time_enabled", RemainingTimeEstimateEnabled);
            cfg.Set("device_sample_interval_min", SampleIntervalMinutes);
            cfg.Set("device_history_retention_days", HistoryRetentionDays);
            cfg.Set("device_monitor_system_battery", MonitorSystemBattery);
            cfg.Set("device_widget_enabled", WidgetEnabled);
            cfg.Set("device_widget_x", WidgetX);
            cfg.Set("device_widget_y", WidgetY);
            cfg.Set("device_widget_max", WidgetMaxDevices);
            cfg.Set("device_widget_mode", WidgetDisplayMode);
            cfg.Set("device_taskbar_pill_enabled", TaskbarBatteryEnabled);
            cfg.Set("device_taskbar_pill_mode", TaskbarBatteryMode);
            cfg.Set("device_taskbar_pill_max", TaskbarBatteryMaxDevices);
            cfg.Set("device_taskbar_pill_offset_x", TaskbarBatteryOffsetX);
            cfg.Set("device_taskbar_pill_offset_dpi", TaskbarBatteryOffsetDpi);
            cfg.Set("device_taskbar_pill_screen", TaskbarBatteryScreen);
            cfg.Set("device_taskbar_pill_screen_label", TaskbarBatteryScreenLabel);
            cfg.Set("device_log_level", LogLevel);
            cfg.Set("device_log_retention_days", LogRetentionDays);
            foreach (var (name, on) in PluginEnabled) cfg.Set($"device_plugin_{name}", on);
            cfg.Save();
        }
        catch (Exception ex)
        {
            LastSaveError = $"config: {ex.Message}";
            return false;
        }

        try
        {
            SaveState();
            return true;
        }
        catch (Exception ex)
        {
            LastSaveError = $"state: {ex.Message}";
            return false;
        }
    }

    // ──── device_state.json ────
    private sealed class DeviceState
    {
        public Dictionary<string, DeviceCustomization> Customizations { get; set; } = new();
        public Dictionary<string, BlockedDevice> Blocked { get; set; } = new();
        public Dictionary<string, long> Dismissed { get; set; } = new();
        public Dictionary<string, IgnoredDevice> Ignored { get; set; } = new();
    }

    private static string StatePath =>
        System.IO.Path.Combine(KeyNote.Core.Utils.AppPaths.DataDir(), "device_state.json");

    private void LoadState()
    {
        try
        {
            if (!System.IO.File.Exists(StatePath)) return;
            var state = System.Text.Json.JsonSerializer.Deserialize<DeviceState>(
                System.IO.File.ReadAllText(StatePath));
            if (state is null) return;
            DeviceCustomizations = state.Customizations;
            foreach (var (id, v) in state.Blocked) BlockedDevices[id] = v;
            foreach (var (id, v) in state.Dismissed) DismissedDevices[id] = v;
            foreach (var (id, v) in state.Ignored) IgnoredDevices[id] = v;
        }
        catch (Exception ex)
        {
            // 状态文件损坏不致命:改名留存,按空名单启动
            KeyNote.Core.Utils.CrashLog.Write("device.state.parse", ex);
            try
            {
                var bak = StatePath + ".corrupt-" + DateTimeOffset.Now.ToUnixTimeMilliseconds();
                System.IO.File.Move(StatePath, bak);
            }
            catch { /* 留存失败忽略 */ }
        }
    }

    private void SaveState()
    {
        var state = new DeviceState
        {
            Customizations = DeviceCustomizations,
            Blocked = BlockedDevices.ToDictionary(kv => kv.Key, kv => kv.Value),
            Dismissed = DismissedDevices.ToDictionary(kv => kv.Key, kv => kv.Value),
            Ignored = IgnoredDevices.ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        var dir = System.IO.Path.GetDirectoryName(StatePath)!;
        System.IO.Directory.CreateDirectory(dir);
        var tmp = StatePath + ".tmp";
        using (var fs = new System.IO.FileStream(tmp, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
        using (var w = new System.IO.StreamWriter(fs))
        {
            w.Write(System.Text.Json.JsonSerializer.Serialize(state,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            w.Flush();
            fs.Flush(true);
        }
        System.IO.File.Move(tmp, StatePath, true);
    }
}
