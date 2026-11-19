using KeyNote.Core.Devices;
using KeyNote.Core.Storage;
using DS = KeyNote.Core.Devices.Settings.AppSettings;

namespace KeyNote.App.Services;

/// 设备域组合根(vigil-watt 移植适配):页面 / 贴片 / 桌面小组件统一经 App.Services 取服务。
/// ShowMainWindow / ExitApplication 由 App.OnLaunched 注入回调,域内代码不反向依赖主窗与托盘。
public sealed class AppServices : IDisposable
{
    public DS Settings { get; }
    public DeviceManager Manager { get; }
    public DeviceHistoryFacade History { get; }

    /// 桌面悬浮小组件 / 任务栏贴片(App 拉起后挂进来,开关由设置驱动)。
    public Widgets.WidgetWindow? Widget { get; set; }
    public TaskbarPillManager? TaskbarBattery { get; set; }

    /// 低电提醒文案出口(App 注入 → 托盘气泡;域内线程触发,封装送须由实现方保证)。
    public Action<string>? NotifyLowBattery { get; set; }

    /// 唤起主窗 / 退出应用(贴片双击、贴片与小组件右键菜单共用)。
    public Action? ShowMainWindow { get; set; }
    public Action? ExitApplication { get; set; }

    public AppServices()
    {
        Settings = DS.Load();
        Manager = new DeviceManager(Settings);
        History = new DeviceHistoryFacade();
    }

    /// 把 config.json 的 device_log_level / device_log_retention_days 落到设备域日志器(启动与保存后调用)。
    public void ApplyLogSettings()
    {
        if (Enum.TryParse<LogLevel>(Settings.LogLevel, true, out var level)) Log.MinLevel = level;
        Log.RetentionDays = Settings.LogRetentionDays;
    }

    public void Dispose()
    {
        // 幂等 + 逐个兜底:任一项失败不能连累其余(照搬 vigil AppServices 约定)
        if (_disposed) return;
        _disposed = true;
        try { Manager.Dispose(); } catch (Exception ex) { Log.Warn($"DeviceManager 释放失败: {ex.Message}"); }
    }

    private bool _disposed;
}

/// HistoryStore 门面:DevicesPage 的三处调用(GetDeviceSummary/DeleteDevices/Compact)映射到 KeyNoteDb,
/// 设备样本与按键统计共用 keynote.db,不另建 history.db。
public sealed class DeviceHistoryFacade
{
    public DeviceHistorySummary GetDeviceSummary(string deviceId) =>
        KeyNoteDb.GetDeviceSampleSummary(deviceId);

    public int DeleteDevices(IReadOnlyCollection<string> deviceIds) =>
        KeyNoteDb.DeleteDeviceHistory(deviceIds);

    public void Compact()
    {
        if (!KeyNoteDb.Vacuum()) Log.Warn("历史压缩(VACUUM)失败");
    }
}
