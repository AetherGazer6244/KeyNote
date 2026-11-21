namespace KeyNote.Core.Utils;

/// 崩溃日志:永远落盘 keynote.crash.log,与用户日志开关解耦(对齐 key_note.py 防线)。
public static class CrashLog
{
    private static readonly object Gate = new();

    /// <summary>crash.log 里最后一条记录的标记名（如 "app.start" / "app.exit"），用于判断上次是否干净退出。</summary>
    public static string? LastMarker()
    {
        try
        {
            if (!File.Exists(AppPaths.CrashLogPath)) return null;
            string? last = null;
            foreach (var line in File.ReadLines(AppPaths.CrashLogPath))
                if (line.StartsWith('[')) last = line;
            if (last is null) return null;
            var end = last.IndexOf(']');
            return end > 1 ? last[1..end] : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string prefix, Exception? ex = null, string? message = null)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(AppPaths.CrashLogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                using var f = new StreamWriter(AppPaths.CrashLogPath, append: true);
                f.WriteLine($"[{prefix}] {ts}");
                if (!string.IsNullOrEmpty(message)) f.WriteLine(message);
                if (ex is not null) f.WriteLine(ex.ToString());
                f.WriteLine();
            }
        }
        catch
        {
            // 日志失败静默
        }
    }
}
