using KeyNote.Core.Devices.Models;

namespace KeyNote.Core.Storage;

/// 单条设备电量历史采样(画曲线用);v7 device_battery_samples 表的行模型。
public sealed record HistorySample(
    long Id, string DeviceId, string Name, DeviceKind Kind, int Percent, bool Charging, DateTimeOffset Timestamp);

/// 单台设备的历史聚合(条数/名称/最早/最新);离线占位补全与设备历史列表用。
public sealed record DeviceHistoryStat(
    string DeviceId, string Name, DeviceKind Kind, int Count, DateTimeOffset First, DateTimeOffset Last);
