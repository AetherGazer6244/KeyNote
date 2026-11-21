using System.Collections.Concurrent;
using KeyNote.Core.Devices.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace KeyNote.Core.Devices.Ble;

/// <summary>
/// 功 能： BLE 电量监视器：DeviceWatcher 监听系统缓存电量 + 主动 GATT 直读 0x180F/0x2A19 兜底（详见 docs/project/code-flow.md §3）。
/// 2026/10/06 15:53:36 xliu
/// </summary>
public sealed class BleBatteryWatcher : IDisposable
{
    private DeviceWatcher? _watcher;
    private bool _started;
    private readonly ConcurrentDictionary<string, DeviceStatus> _devices = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastGattAttempt = new();

    /// <summary>已经问过 AEP Category 的设备（每台一次，结果覆盖名字猜测）。</summary>
    private readonly ConcurrentDictionary<string, byte> _kindQueried = new();
    private const string CategoryKey = "System.Devices.Aep.Category";
    private const string AepBatteryKey = "System.Devices.AepBattery_Level";
    private readonly SemaphoreSlim _gattGate = new(1, 1);

    /// <summary>每台设备 AEP 缓存电量的查询节流。</summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAepProbe = new();

    public event Action<DeviceStatus>? DeviceUpdated;

    public void Start()
    {
        if (_started) return;
        _started = true;

        // ⚠️ 电量属性（AepBattery_Level）不是合法的 watcher 属性键——实测四个写法全抛
        // "Property key syntax error"（2026-11-21），系统根本没把它开放给 DeviceWatcher。
        // watcher 只订 Name + IsConnected；电量走 AEP 嵌套属性查询 + GATT 直读兜底（见下）。
        var selector = BluetoothLEDevice.GetDeviceSelector();
        DeviceWatcher? watcher;
        try
        {
            watcher = DeviceInformation.CreateWatcher(
                selector, new[] { WatcherProps.Name, WatcherProps.Connected }, DeviceInformationKind.AssociationEndpoint);
        }
        catch (Exception ex)
        {
            Log.Error($"BLE DeviceWatcher 创建失败，BLE 监控不可用: {ex.Message}");
            return;
        }

        watcher.Added += (w, info) => HandleDevice(info.Id, info.Properties, info.Name);
        watcher.Updated += (w, update) => HandleDevice(update.Id, update.Properties, null);
        watcher.Removed += (w, update) => HandleRemoved(update.Id);
        watcher.Start();
        _watcher = watcher;
        Log.Info("BLE DeviceWatcher 已启动");
    }

    private void HandleDevice(string id, IReadOnlyDictionary<string, object> props, string? name)
    {
        // 永久移除的设备：不再跟踪、不再 GATT 直读（否则每次 Updated 都会白读一轮）
        if (DeviceBlocklist.IsBlocked($"ble:{id}"))
        {
            if (_devices.TryRemove(id, out var blocked))
                Log.Info($"BLE 设备已永久移除，停止跟踪: {blocked.DisplayName}");
            return;
        }

        // Updated 只带变化的属性：IsConnected 缺失时必须沿用现状，不能默认 false
        // （真机 2026-10-08：戴尔 WK717 已连接却被后续 Updated 误判离线，与经典蓝牙同坑）
        bool? connected = WatcherProps.ReadConnected(props);

        if (connected == false)
        {
            if (_devices.TryGetValue(id, out var tracked) && tracked.IsOnline)
            {
                tracked.IsOnline = false;
                tracked.LastUpdated = DateTimeOffset.Now;
                Log.Info($"BLE 设备离线: {tracked.DisplayName}");
                DeviceUpdated?.Invoke(tracked);
            }
            return;
        }

        // 未跟踪过、且未明确在线 → 邻居设备，忽略
        bool known = _devices.ContainsKey(id);
        if (!known && connected != true)
            return;

        var status = _devices.GetOrAdd(id, _ => new DeviceStatus
        {
            Id = $"ble:{id}",
            Name = name ?? "",
            Source = DeviceSource.Ble,
            Protocol = "BLE GATT",
        });
        ApplyName(status, name, props);
        // 先按名字给个即时结果，别让首帧停在全空白图标上
        if (status.Kind == DeviceKind.Unknown)
            status.Kind = DeviceKindGuesser.Guess(status.Name);

        // 系统类别更权威（设备自己声明的 Input.Keyboard / Communication.Phone），查到就覆盖名字的猜测：
        // 名字可能含误导词，也可能一个品类词都没有（真机 "MCHOSE G87 5.0"）。每台只查一次。
        if (_kindQueried.TryAdd(id, 0))
            _ = ResolveKindFromCategoryAsync(id, status);

        // 明确在线，或读到电量，或本来就在列表里且本次未说断开 → 保持/标为在线
        bool wasOffline = !status.IsOnline;
        status.IsOnline = true;
        status.LastUpdated = DateTimeOffset.Now;

        if (wasOffline || !known)
            DeviceUpdated?.Invoke(status);

        // watcher 属性集里没有电量（见 Start 注释）：先查 AEP 缓存电量（快、零连接、节流 5 分钟），
        // 拿不到再 GATT 直读一次（限流 2 分钟）
        _ = ResolveBatteryFromAepAsync(id, status);
        if (!_lastGattAttempt.TryGetValue(id, out var last) ||
            DateTimeOffset.Now - last > TimeSpan.FromMinutes(2))
        {
            _lastGattAttempt[id] = DateTimeOffset.Now;
            _ = ReadBatteryViaGattAsync(id, status);
        }
    }

    /// <summary>读 AEP 嵌套属性 <c>System.Devices.AepBattery_Level</c>（系统缓存的电量，零连接开销）。
    /// 该键只是 DeviceInformation 的嵌套属性，不是合法的 watcher 属性键（塞进 CreateWatcher 直接抛
    /// Property key syntax error），只能按单设备嵌套属性查询——与 <see cref="ResolveKindFromCategoryAsync"/>
    /// 同一套模式。查询结果由节流字典压频（5 分钟/台），失败静默，交给 GATT 兜底。</summary>
    private async Task ResolveBatteryFromAepAsync(string aepId, DeviceStatus status)
    {
        if (_lastAepProbe.TryGetValue(aepId, out var last) &&
            DateTimeOffset.Now - last < TimeSpan.FromMinutes(5))
            return;
        _lastAepProbe[aepId] = DateTimeOffset.Now;
        try
        {
            var info = await DeviceInformation.CreateFromIdAsync(
                aepId, new[] { AepBatteryKey }, DeviceInformationKind.AssociationEndpoint);
            if (!info.Properties.TryGetValue(AepBatteryKey, out var raw)) return;
            int percent = Convert.ToInt32(raw);
            if (percent is not (> 0 and <= 100)) return;
            // 设备已被移除或句柄换代：丢弃结果，否则会复活幽灵条目
            if (!_devices.TryGetValue(aepId, out var current) || !ReferenceEquals(current, status)) return;

            bool changed = status.BatteryPercent != percent;
            status.BatteryPercent = percent;
            status.LastUpdated = DateTimeOffset.Now;
            if (changed)
            {
                Log.Info($"BLE 电量(AEP 缓存): {status.DisplayName} → {percent}%");
                DeviceUpdated?.Invoke(status);
            }
        }
        catch
        {
            // 设备不支持该属性 / 已移除：静默
        }
    }

    private void HandleRemoved(string id)
    {
        _kindQueried.TryRemove(id, out _);
        if (_devices.TryRemove(id, out var tracked) && tracked.IsOnline)
        {
            tracked.IsOnline = false;
            tracked.LastUpdated = DateTimeOffset.Now;
            Log.Info($"BLE 设备移除: {tracked.DisplayName}");
            DeviceUpdated?.Invoke(tracked);
        }
    }

    private static void ApplyName(
        DeviceStatus status, string? infoName, IReadOnlyDictionary<string, object> props)
    {
        if (!string.IsNullOrEmpty(infoName))
        {
            status.Name = infoName!;
            return;
        }
        if (props.TryGetValue(WatcherProps.Name, out var nameObj))
        {
            var n = nameObj?.ToString();
            if (!string.IsNullOrEmpty(n)) status.Name = n!;
        }
    }

    /// <summary>读 AEP 的 <c>System.Devices.Aep.Category</c>（真机给 "Input.Keyboard" / "Communication.Phone"）并覆盖名字猜的类型。
    /// 每台只查一次；设备已被移除则丢弃结果，否则会复活一条幽灵条目。</summary>
    private async Task ResolveKindFromCategoryAsync(string aepId, DeviceStatus status)
    {
        try
        {
            var info = await DeviceInformation.CreateFromIdAsync(
                aepId, new[] { CategoryKey }, DeviceInformationKind.AssociationEndpoint);

            if (!info.Properties.TryGetValue(CategoryKey, out var raw)) return;
            var categories = raw switch
            {
                string[] arr => arr,
                string s => new[] { s },
                _ => Array.Empty<string>(),
            };

            var kind = ClassicDeviceKindResolver.FromCategories(categories);
            if (kind == DeviceKind.Unknown) return;
            if (!_devices.TryGetValue(aepId, out var current) || !ReferenceEquals(current, status)) return;

            status.Kind = kind;
            status.LastUpdated = DateTimeOffset.Now;
            Log.Info($"BLE 设备类型(AEP Category): {status.DisplayName} → {kind}");
            DeviceUpdated?.Invoke(status);
        }
        catch (Exception ex)
        {
            Log.Warn($"BLE 设备类别读取失败 {aepId}: {ex.Message}");
        }
    }

    private async Task ReadBatteryViaGattAsync(string aepId, DeviceStatus status)
    {
        await _gattGate.WaitAsync();
        try
        {
            var ble = await OpenDeviceAsync(aepId);
            if (ble == null) return;
            using (ble)
            {
                // FromIdAsync 成功且能读 GATT = 设备实际可达，纠正假离线
                if (ble.ConnectionStatus != BluetoothConnectionStatus.Connected)
                    return;

                // DeviceWatcher 的 Added 常带空 Name（系统还没查到属性），UI 只能显示 Id 或「BLE 设备」；
                // 打开设备后 OS 已有缓存名，补上并通知一次。来源 PR #14（dotisland）。
                if (string.IsNullOrEmpty(status.Name) && !string.IsNullOrEmpty(ble.Name))
                {
                    status.Name = ble.Name;
                    status.LastUpdated = DateTimeOffset.Now;
                    Log.Info($"BLE 设备名已补全（GATT 缓存）: {status.Name}");
                    DeviceUpdated?.Invoke(status);
                }

                var svcResult = await ble
                    .GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(8));
                if (svcResult.Status != GattCommunicationStatus.Success || svcResult.Services.Count == 0) return;

                var chrResult = await svcResult.Services[0]
                    .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(8));
                if (chrResult.Status != GattCommunicationStatus.Success || chrResult.Characteristics.Count == 0) return;

                var read = await chrResult.Characteristics[0]
                    .ReadValueAsync(BluetoothCacheMode.Uncached)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(8));
                if (read.Status != GattCommunicationStatus.Success) return;

                var reader = DataReader.FromBuffer(read.Value);
                int percent = reader.ReadByte();
                if (percent is > 0 and <= 100)
                {
                    bool wasOffline = !status.IsOnline;
                    status.IsOnline = true;
                    status.BatteryPercent = percent;
                    status.LastUpdated = DateTimeOffset.Now;
                    if (status.Kind == DeviceKind.Unknown)
                        status.Kind = DeviceKindGuesser.Guess(status.Name);
                    DeviceUpdated?.Invoke(status);
                    Log.Info(wasOffline
                        ? $"BLE GATT 直读(恢复在线): {status.DisplayName} {percent}%"
                        : $"BLE GATT 直读: {status.DisplayName} {percent}%");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"BLE GATT 读取失败 {aepId}: {ex.Message}");
        }
        finally
        {
            _gattGate.Release();
        }
    }

    /// <summary>周期轮询已跟踪设备（含曾被误标离线的，便于自愈）。</summary>
    public async Task PollConnectedAsync()
    {
        foreach (var (id, status) in _devices.ToArray())
        {
            if (!_lastGattAttempt.TryGetValue(id, out var last) ||
                DateTimeOffset.Now - last > TimeSpan.FromMinutes(2))
            {
                _lastGattAttempt[id] = DateTimeOffset.Now;
                await ReadBatteryViaGattAsync(id, status);
            }
        }
    }

    private static async Task<BluetoothLEDevice?> OpenDeviceAsync(string aepId)
    {
        try
        {
            var task = BluetoothLEDevice.FromIdAsync(aepId).AsTask();
            if (await Task.WhenAny(task, Task.Delay(8000)) != task) return null;
            return task.Result;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<DeviceStatus> Snapshot() => _devices.Values.ToList();

    public void Dispose()
    {
        _watcher?.Stop();
        _gattGate.Dispose();
    }
}
