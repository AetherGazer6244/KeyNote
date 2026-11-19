using KeyNote.Core.Devices.Models;
using KeyNote.Core.Utils;
using Microsoft.Data.Sqlite;

namespace KeyNote.Core.Storage;

/// 设备电量样本持久化(v7 增量,三合一整合):队列写入 + 历史查询 + 保留期清理 + 批量导入。
/// 表 device_battery_samples 与按键侧共用 keynote.db / writer 线程 / 备份链路,不另建库。
public static partial class KeyNoteDb
{
    private static readonly object DeviceWriteGate = new();
    private static int _deviceRetentionDays = 90;

    /// 运行时更新设备样本保留天数(设置页改键后立即生效)。
    public static void SetDeviceRetentionDays(int days) =>
        Interlocked.Exchange(ref _deviceRetentionDays, Math.Max(0, days));

    // ──── 写入(经 writer 队列,Kind='d') ────
    /// 入队一条电量采样;无电量的设备跳过。
    public static void InsertDeviceSample(Devices.Models.DeviceStatus device)
    {
        if (device.BatteryPercent is not { } percent) return;
        var q = _queue;
        if (q is null || _shutdown) return;
        try
        {
            q.TryAdd(new WriteItem(
                device.LastUpdated.ToUnixTimeMilliseconds() / 1000.0, 'd', "", "",
                new DeviceRow(device.Id, device.DisplayName, (int)device.Kind, percent,
                    DeviceStatusFormatter.IsChargingOrFull(device.Charging) ? 1 : 0)));
        }
        catch
        {
            // 队列满时丢弃(与按键侧一致)
        }
    }

    /// 批量导入(旧库迁移用):直接走写连接事务,不经队列。返回导入条数。
    public static int ImportDeviceSamples(
        IEnumerable<(string DeviceId, string Name, int Kind, int Percent, bool Charging, long TsMs)> rows)
    {
        lock (DeviceWriteGate)
        {
            if (_writerConn is null) return 0;
            using var tx = _writerConn.BeginTransaction();
            try
            {
                var total = 0;
                var chunk = new List<object?[]>();
                foreach (var r in rows)
                {
                    chunk.Add(new object?[] { r.DeviceId, r.Name, r.Kind, r.Percent, r.Charging ? 1 : 0, r.TsMs });
                    total++;
                    if (chunk.Count >= 500)
                    {
                        ExecDeviceRows(tx, chunk);
                        chunk.Clear();
                    }
                }
                if (chunk.Count > 0) ExecDeviceRows(tx, chunk);
                tx.Commit();
                return total;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* 回滚失败忽略 */ }
                CrashLog.Write("db.device.import", ex);
                return 0;
            }
        }
    }

    private static void ExecDeviceRows(SqliteTransaction tx, List<object?[]> chunk)
    {
        var head = "INSERT INTO device_battery_samples(device_id, name, kind, percent, charging, ts) VALUES ";
        var sb = new System.Text.StringBuilder();
        var bind = new List<(string, object?)>();
        for (var r = 0; r < chunk.Count; r++)
        {
            if (r > 0) sb.Append(',');
            sb.Append('(');
            for (var i = 0; i < 6; i++)
            {
                if (i > 0) sb.Append(',');
                var name = $"$d{r}_{i}";
                sb.Append(name);
                bind.Add((name, chunk[r][i]));
            }
            sb.Append(')');
        }
        using var cmd = _writerConn!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = head + sb;
        foreach (var (n, v) in bind) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ──── 查询 ────
    private static DateTimeOffset LocalTime(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime();

    /// ORDER BY 用:0 = 可显示名,1 = 占位(空 / 等于 device_id / 含 Bluetooth#)。全是占位时仍取最新一条。
    private const string PreferredNameSql =
        "CASE WHEN name = '' OR name = device_id OR INSTR(name, 'Bluetooth#') > 0 THEN 1 ELSE 0 END";

    /// 取单台设备时间范围内的采样,画曲线用。
    public static List<HistorySample> QueryDeviceSamples(string deviceId, DateTimeOffset from, DateTimeOffset to)
    {
        var list = new List<HistorySample>();
        if (!Ready) return list;
        using var cmd = ReadCmd("SELECT id, device_id, name, kind, percent, charging, ts FROM device_battery_samples " +
                                "WHERE device_id = $id AND ts BETWEEN $from AND $to ORDER BY ts");
        if (cmd is null) return list;
        cmd.Parameters.AddWithValue("$id", deviceId);
        cmd.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new HistorySample(
                r.GetInt64(0), r.GetString(1), r.GetString(2), (DeviceKind)r.GetInt32(3),
                r.GetInt32(4), r.GetInt32(5) == 1, LocalTime(r.GetInt64(6))));
        }
        return list;
    }

    /// 每台设备的聚合(条数/名称/最早/最新),最近有数据的在前;名称取最新非占位名。
    public static List<DeviceHistoryStat> GetDeviceHistoryStats()
    {
        var list = new List<DeviceHistoryStat>();
        if (!Ready) return list;
        using var cmd = ReadCmd($"""
            SELECT device_id, name, kind, cnt, first_ts, last_ts FROM (
                SELECT device_id, name, kind,
                       COUNT(*) OVER (PARTITION BY device_id) AS cnt,
                       MIN(ts) OVER (PARTITION BY device_id) AS first_ts,
                       MAX(ts) OVER (PARTITION BY device_id) AS last_ts,
                       ROW_NUMBER() OVER (
                           PARTITION BY device_id
                           ORDER BY {PreferredNameSql} ASC, ts DESC, id DESC
                       ) AS rn
                FROM device_battery_samples
            ) WHERE rn = 1
            ORDER BY last_ts DESC
            """);
        if (cmd is null) return list;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DeviceHistoryStat(
                r.GetString(0), r.GetString(1), (DeviceKind)r.GetInt32(2), r.GetInt32(3),
                LocalTime(r.GetInt64(4)), LocalTime(r.GetInt64(5))));
        }
        return list;
    }

    /// 删除指定设备的全部历史,返回删除条数。
    public static int DeleteDeviceHistory(IReadOnlyCollection<string> deviceIds)
    {
        if (deviceIds.Count == 0 || !Ready) return 0;
        var placeholders = deviceIds.Select((_, i) => $"$id{i}").ToArray();
        using var cmd = ReadCmd($"DELETE FROM device_battery_samples WHERE device_id IN ({string.Join(",", placeholders)})");
        if (cmd is null) return 0;
        var i = 0;
        foreach (var id in deviceIds) cmd.Parameters.AddWithValue($"$id{i++}", id);
        return cmd.ExecuteNonQuery();
    }

    /// 按保留天数清理设备样本,返回删除条数。
    public static int CleanupDeviceSamplesOlderThan(DateTimeOffset cutoff)
    {
        lock (DeviceWriteGate)
        {
            if (_writerConn is null) return 0;
            try
            {
                using var cmd = _writerConn.CreateCommand();
                cmd.CommandText = "DELETE FROM device_battery_samples WHERE ts < $cutoff";
                cmd.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeMilliseconds());
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                CrashLog.Write("db.device.cleanup", ex);
                return 0;
            }
        }
    }

    /// 设备样本总条数(健康检查/托盘用)。
    public static long DeviceSampleCount()
    {
        if (!Ready) return 0;
        return (long)(Scalar("SELECT COUNT(*) FROM device_battery_samples") ?? 0L);
    }
}
