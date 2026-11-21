namespace KeyNote.Core.Utils;

using KeyNote.Core.Settings;

/// <summary>
/// 运行日志门面:关键事件流(启动/钩子/额度/更新等)统一落盘到 Devices.Log 的
/// 按天日志(logs/app-yyyy-MM-dd.log,与设备域共用一个时间线,便于一个文件看全)。
/// 写入/跨天/保留清理/失败兜底全部复用 vigil 移植的成熟实现,本类只做两件事:
/// ① app_log_enabled 开关(经 <see cref="Init"/> 注入 AppSettings,每条实时读取,设置页保存立即生效);
/// ② 带 tag 的轻量入口。CrashLog 的异常也经此进入同一时间线。
/// </summary>
public static class AppLog
{
    private static AppSettings? _settings;

    /// <summary>注入设置源(仅 App 启动时一次);null = 未初始化,视为关闭。</summary>
    public static void Init(AppSettings settings) => _settings = settings;

    public static bool Enabled => _settings?.GetBool("app_log_enabled", true) ?? false;

    public static void Info(string tag, string message)
    {
        if (Enabled) Devices.Log.Info($"[{tag}] {message}");
    }

    public static void Warn(string tag, string message)
    {
        if (Enabled) Devices.Log.Warn($"[{tag}] {message}");
    }

    public static void Error(string tag, string message, Exception? ex = null)
    {
        if (!Enabled) return;
        var line = $"[{tag}] {message}";
        if (ex is not null) Devices.Log.Error(line, ex);
        else Devices.Log.Error(line);
    }
}
