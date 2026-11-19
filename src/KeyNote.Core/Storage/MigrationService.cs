using KeyNote.Core.Settings;
using KeyNote.Core.Storage;
using KeyNote.Core.Utils;
using Microsoft.Data.Sqlite;

namespace KeyNote.Core.Migration;

/// <summary>
/// 旧应用数据首启自动导入(三合一整合):
/// - VigilWatt:%LOCALAPPDATA%\VigilWatt\history.db 的 samples 表 → keynote.db v7
///   device_battery_samples(经 ImportDeviceSamples 批量事务,旧目录只读不动);
/// - AgentEye:~/.agenteye/config.json → 数据目录 quota.json,~/.agenteye/cache →
///   quota-cache/(DPAPI 按 Windows 用户解密,跨应用可读,整文件原样搬运);
/// 幂等标记落在 config.json(migration_*_imported),只有源存在且目标缺失才搬运。
/// </summary>
public static class MigrationService
{
    public static void Run(AppSettings settings)
    {
        ImportVigilWatt(settings);
        ImportAgentEye(settings);
    }

    // ──── VigilWatt 电量历史 ────
    private static void ImportVigilWatt(AppSettings settings)
    {
        if (settings.GetBool("migration_vigilwatt_imported")) return;
        try
        {
            var dbPath = ResolveVigilDb();
            if (dbPath is null)
            {
                MarkDone(settings, "migration_vigilwatt_imported");
                return;
            }

            var rows = new List<(string DeviceId, string Name, int Kind, int Percent, bool Charging, long TsMs)>();
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();
            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                // 老库可能没有 kind 列(vigil 迁移补列逻辑同款语义):缺列按 Unknown(0)
                var hasKind = false;
                using (var info = conn.CreateCommand())
                {
                    info.CommandText = "PRAGMA table_info(samples)";
                    using var r = info.ExecuteReader();
                    while (r.Read()) hasKind |= r.GetString(1) == "kind";
                }
                cmd.CommandText = hasKind
                    ? "SELECT device_id, name, kind, percent, charging, ts FROM samples ORDER BY ts"
                    : "SELECT device_id, name, 0, percent, charging, ts FROM samples ORDER BY ts";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0)) continue;
                    rows.Add((
                        reader.GetString(0),
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.GetInt32(2),
                        reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        !reader.IsDBNull(4) && reader.GetInt64(4) != 0,
                        reader.IsDBNull(5) ? 0 : reader.GetInt64(5)));
                }
            }

            var imported = KeyNoteDb.ImportDeviceSamples(rows);
            settings.Set("migration_vigilwatt_rows", imported);
            MarkDone(settings, "migration_vigilwatt_imported");
        }
        catch (Exception ex)
        {
            // 不打标记:下次启动重试(只有源库损坏这类持续失败会反复走 catch,代价可忽略)
            CrashLog.Write("migration.vigilwatt", ex);
        }
    }

    private static string? ResolveVigilDb()
    {
        var custom = Environment.GetEnvironmentVariable("VIGILWATT_DATA_DIR");
        var root = !string.IsNullOrWhiteSpace(custom) ? custom.Trim() : null;
        if (root is null)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local)) return null;
            root = Path.Combine(local, "VigilWatt");
        }
        var db = Path.Combine(root, "history.db");
        return File.Exists(db) ? db : null;
    }

    // ──── AgentEye 额度配置与缓存 ────
    private static void ImportAgentEye(AppSettings settings)
    {
        if (settings.GetBool("migration_agenteye_imported")) return;
        try
        {
            var profile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (string.IsNullOrEmpty(profile))
                profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var srcRoot = Path.Combine(profile, ".agenteye");

            var srcCfg = Path.Combine(srcRoot, "config.json");
            var dstCfg = Path.Combine(AppPaths.DataDir(), "quota.json");
            if (File.Exists(srcCfg) && !File.Exists(dstCfg))
                File.Copy(srcCfg, dstCfg);

            var srcCache = Path.Combine(srcRoot, "cache");
            var dstCache = Path.Combine(AppPaths.DataDir(), "quota-cache");
            if (Directory.Exists(srcCache) && !Directory.Exists(dstCache))
            {
                Directory.CreateDirectory(dstCache);
                foreach (var f in Directory.GetFiles(srcCache, "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(dstCache, Path.GetRelativePath(srcCache, f));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(f, target);
                }
            }

            MarkDone(settings, "migration_agenteye_imported");
        }
        catch (Exception ex)
        {
            CrashLog.Write("migration.agenteye", ex);
        }
    }

    private static void MarkDone(AppSettings settings, string marker)
    {
        settings.Set(marker, true);
        settings.Save();
    }
}
