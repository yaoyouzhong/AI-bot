using System.IO.Ports;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class SerialPublisher : IUsbFallbackDevice
{
    private const string Prefix = "@AIBOT ";
    private readonly object _portSync = new();
    private volatile string? _portName;
    private volatile string? _deviceHost;
    private volatile string? _usbFirmwareVersion;
    private volatile string? _lanFirmwareVersion;
    internal string? UsbFirmwareVersion => _usbFirmwareVersion;
    internal string? LanFirmwareVersion => _lanFirmwareVersion;
    internal void ObserveLanFirmware(string? value) => _lanFirmwareVersion = EspFirmwareVersion.Read(value);
    private volatile string? _configuredLanHost;
    private volatile int _configuredLanPort;
    private SerialPort? _activePort;
    private LanPairing? _pairing;
    private string? _preferredPort;
    internal string? ExpectedUsbIdentity {get;set;}
    internal string ConnectionStatus {get;private set;}="等待 USB 设备";
    internal string? LastConnectionError {get;private set;}
    internal void ReloadPort()=>_preferredPort=NormalizePort(BridgeSettings.Load().Get("serial_port"));
    private long _pauseUntil;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private volatile bool _flashPaused;
    private volatile bool _usbDataEnabled=true;
    private long _lastStatusWrittenAt;
    internal long LastDataWrittenAt=>Interlocked.Read(ref _lastStatusWrittenAt);
    internal bool UsbDataEnabled {
        get=>_usbDataEnabled;
        set {lock(_portSync){_usbDataEnabled=value;if(!value)Interlocked.Exchange(ref _lastStatusWrittenAt,0);}}
    }
    internal bool UsbDataActive=>UsbDataEnabled&&PortName is not null&&Interlocked.Read(ref _lastStatusWrittenAt)>0&&Environment.TickCount64-Interlocked.Read(ref _lastStatusWrittenAt)<8000;
    internal bool FlashBusy=>_flashPaused;
    internal Func<string?>? ReservedPort { get; set; }

    // The gate covers probing as well as an established connection. A ready
    // acknowledgement means the serial handle has actually been disposed.
    internal async Task PauseForFlashAsync(CancellationToken token)
    {
        _flashPaused = true;
        try { await _connectionGate.WaitAsync(token); }
        catch { _flashPaused = false; throw; }
    }

    internal void ResumeAfterFlash()
    {
        _flashPaused = false;
        _connectionGate.Release();
    }
    private Func<StatusSnapshot>? _captureStatus;
    private readonly DisplayCommandQueue _displayCommands;

    internal bool TransmissionPaused { get { lock (_portSync) return IsPaused; } }
    private bool IsPaused => _flashPaused || Environment.TickCount64 < _pauseUntil;

    public void PauseTransmission()
    {
        lock (_portSync)
        {
            if (_activePort?.IsOpen != true) throw new IOException("USB 未连接。");
            _pauseUntil = Environment.TickCount64 + 30_000;
        }
    }

    public void ResumeTransmission() { lock (_portSync) _pauseUntil = 0; }

    public UsbDeviceInfo ReadDeviceInfo() =>
        UsbDeviceProtocol.ReadInfo(RequestDevice("device_info_request", "device_info"));

    internal void ResetDeviceWiFi()
    {
        var reply = RequestDevice("reset_wifi", "reset_wifi_ack", confirm: true);
        if (!reply.GetProperty("ok").GetBoolean()) throw new IOException("设备拒绝重置。");
    }

    private JsonElement RequestDevice(string type, string replyType, bool confirm = false)
    {
        lock (_portSync)
        {
            if (_flashPaused || _activePort?.IsOpen != true || (IsPaused && type != "device_info_request"))
                throw new IOException("USB 未连接或正在回退测试。");
            uint requestId;
            do { requestId = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)); } while (requestId == 0);
            _activePort.WriteLine(Prefix + JsonSerializer.Serialize(new
                { version = 1, type, request_id = requestId, confirm }));
            var deadline = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < deadline)
            {
                var reply = UsbDeviceProtocol.ParseReply(_activePort.ReadLine().Trim(), replyType, requestId);
                if (reply is not null) return reply.Value;
            }
            throw new TimeoutException("设备未确认请求；请检查固件版本。重置请求不会自动重试。");
        }
    }

    internal SerialPublisher(LanPairing? pairing, string? preferredPort = null)
    {
        _pairing = pairing;
        _preferredPort = NormalizePort(preferredPort);
        _displayCommands = new DisplayCommandQueue(_portSync, mode =>
        {
            if (TrySend(new { version = 1, type = "display", mode })) DisplayModeWritten?.Invoke(mode);
        });
    }

    internal string? PortName => _portName;
    internal string? DeviceHost => _deviceHost;
    internal string? ConfiguredLanHost => _configuredLanHost;
    internal int ConfiguredLanPort => _configuredLanPort;
    internal LanPairing? CurrentPairing => Volatile.Read(ref _pairing);
    internal void SetPairing(LanPairing? pairing) {
        Volatile.Write(ref _pairing, pairing);
        if(pairing is null)_lanFirmwareVersion=null;
    }

    internal async Task RunMetricsAsync(Func<SystemMetricsSnapshot?> capture, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        DateTimeOffset? sentAt = null;
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            lock (_portSync)
            {
                // A resource transfer may hold the port for seconds. Sample after
                // acquiring it so this frame cannot replace newer heartbeat data.
                if (_activePort?.IsOpen != true || IsPaused || !UsbDataEnabled) continue;
                var metrics = capture();
                if (metrics is null || metrics.UpdatedAt == sentAt) continue;
                if (TrySend(new { version = 1, type = "metrics", data = metrics })) sentAt = metrics.UpdatedAt;
            }
        }
    }

    internal bool SendDisplayMode(string mode)
    {
        if (!DisplayModes.IsValid(mode) || _portName is null || IsPaused) return false;
        _displayCommands.Enqueue(mode);
        return true; // Accepted for delivery; policy heartbeats also carry the selection.
    }

    internal bool SendBrightness(int level) => TrySend(new
    {
        version = 1,
        type = "brightness",
        level = Math.Clamp(level, 0, 100)
    });

    internal string? LastResourceFailure { get; private set; }
    internal event Action<string>? DisplayModeWritten;

    internal bool SendResource(BinaryResourceKind kind, byte[] data)
    {
        var transferId = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
        var chunks = BinaryResourceProtocol.CreateChunks(kind, data, transferId);
        lock (_portSync)
        {
            LastResourceFailure = null;
            if (_activePort?.IsOpen != true || IsPaused || !UsbDataEnabled) { LastResourceFailure = "port closed, paused or Wi-Fi selected"; return false; }
            var nextHeartbeat = Environment.TickCount64;
            foreach (var chunk in chunks)
            {
                if (_flashPaused) return false;
                _displayCommands.Drain();
                // APET and Chinese resources can span several seconds. Preserve
                // status freshness between acknowledged binary chunks.
                if (_captureStatus is not null && Environment.TickCount64 >= nextHeartbeat)
                {
                    _activePort.WriteLine(Prefix + DeviceStatusFrame.Create(_captureStatus()).ToJsonString(JsonDefaults.Options));
                    Interlocked.Exchange(ref _lastStatusWrittenAt,Environment.TickCount64);
                    nextHeartbeat = Environment.TickCount64 + 2000;
                }
                var acknowledged = false;
                for (var attempt = 0; attempt < 3 && !acknowledged; attempt++)
                {
                    if (_flashPaused) return false;
                    _displayCommands.Drain();
                    _activePort.Write(chunk.WireBytes, 0, chunk.WireBytes.Length);
                    acknowledged = WaitForResourceAck(_activePort, chunk.TransferId, chunk.Sequence);
                    _displayCommands.Drain();
                }
                if (!acknowledged) { LastResourceFailure = $"kind={kind} sequence={chunk.Sequence} bytes={data.Length}: no positive ACK after 3 attempts"; return false; }
            }
            return true;
        }
    }

    internal void NotifyHostGoingAway()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            TrySend(new { version = 1, type = "host_going_away" });
            if (attempt < 2) Thread.Sleep(25);
        }
    }

    internal async Task RunAsync(Func<StatusSnapshot> snapshot,
        Func<IReadOnlyList<ResourcePayload>> resources, CancellationToken cancellationToken)
    {
        _captureStatus = snapshot;
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<FlashUsbDevice> usbDevices;
            try{usbDevices=FlashDeviceDiscovery.Read();}
            catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException){ConnectionStatus="USB 检测失败："+ex.GetType().Name;await Task.Delay(3000,cancellationToken);continue;}
            var tabPorts=usbDevices.Where(d=>d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase)).Select(d=>d.Port).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates=BoundPorts(ExpectedUsbIdentity,usbDevices,CandidatePorts);
            if(candidates.Count==0)ConnectionStatus="未找到已绑定的 USB 设备";
            foreach (var candidate in candidates)
            {
                if(ExpectedUsbIdentity is {} expected&&!usbDevices.Any(d=>d.Port.Equals(candidate,StringComparison.OrdinalIgnoreCase)&&d.Identity==expected))continue;
                if (tabPorts.Contains(candidate)||string.Equals(candidate, ReservedPort?.Invoke(), StringComparison.OrdinalIgnoreCase)) continue;
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_flashPaused) break;
                await _connectionGate.WaitAsync(cancellationToken);
                using var port = CreatePort(candidate);
                try
                {
                    if (_flashPaused) continue;
                    if (tabPorts.Contains(candidate)||string.Equals(candidate, ReservedPort?.Invoke(), StringComparison.OrdinalIgnoreCase)) continue;
                    ConnectionStatus="正在连接 "+candidate;port.Open();
                    await Task.Delay(1200, cancellationToken);
                    if (_flashPaused) continue;
                    port.DiscardInBuffer();
                    port.WriteLine(Prefix + "{\"version\":1,\"type\":\"ping\"}");

                    if (!WaitForPong(port, out var deviceHost, out var firmware))
                        {ConnectionStatus=candidate+" 未回复心跳";continue;}

                    _portName = candidate;
                    ConnectionStatus="已连接 · "+candidate;
                    _deviceHost = deviceHost;
                    _usbFirmwareVersion = firmware;
                    lock (_portSync) _activePort = port;
                    var sentRevisions = new Dictionary<BinaryResourceKind, int>();
                    LanPairing? sentPairing = null;
                    var nextDeviceProbeAt = DateTime.UtcNow.AddSeconds(deviceHost is null ? 5 : 30);
                    while (!cancellationToken.IsCancellationRequested && !_flashPaused && port.IsOpen)
                    {
                        if (TransmissionPaused) {
                            await Task.Delay(100, cancellationToken);
                            continue;
                        }
                        if (DateTime.UtcNow >= nextDeviceProbeAt)
                        {
                            RefreshDeviceHost(port);
                            nextDeviceProbeAt = DateTime.UtcNow.AddSeconds(_deviceHost is null ? 5 : 30);
                        }
                        var pairing = Volatile.Read(ref _pairing);
                        if (pairing is null) { sentPairing = null; _configuredLanHost = null; _configuredLanPort = 0; }
                        else if (pairing != sentPairing && TrySend(new
                        {
                            version = 1,
                            type = "lan_config",
                            data = new { host = pairing.Address.ToString(), port = pairing.Port, token = pairing.Token }
                        })) { sentPairing = pairing; _configuredLanHost = pairing.Address.ToString(); _configuredLanPort = pairing.Port; }
                        // Pairing and diagnostics remain available over USB in Wi-Fi mode,
                        // but no status, metrics or resources may refresh the USB data path.
                        foreach (var resource in UsbDataEnabled?resources():Array.Empty<ResourcePayload>())
                        {
                            if (sentRevisions.TryGetValue(resource.Kind, out var revision) &&
                                revision == resource.Revision) continue;
                            if (SendResource(resource.Kind, resource.Data))
                                sentRevisions[resource.Kind] = resource.Revision;
                        }
                        lock (_portSync)
                        {
                            // Capture after acquiring the port: an older heartbeat
                            // must not undo a mode selected while it was waiting.
                            Write(port, DeviceStatusFrame.Create(snapshot()));LastConnectionError=null;
                            _displayCommands.Drain();
                            if (port.IsOpen && port.BytesToRead > 0) port.ReadExisting();
                        }
                        await Task.Delay(2000, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
                {
                    // A port may disappear, be busy, or belong to a different device.
                    LastConnectionError=ex.GetType().Name+" · "+ex.Message;
                    ConnectionStatus=candidate+" · "+(ex is UnauthorizedAccessException?"端口被占用":ex is TimeoutException?"设备回复超时":ex is IOException?"连接中断":"数据发送失败");
                }
                finally
                {
                    lock (_portSync)
                        if (ReferenceEquals(_activePort, port)) _activePort = null;
                    _portName = null;
                    _deviceHost = null;
                    _usbFirmwareVersion = null;
                    _configuredLanHost = null;
                    _configuredLanPort = 0;
                    try { port.Dispose(); } finally { _connectionGate.Release(); }
                }
            }

            await Task.Delay(3000, cancellationToken);
        }
    }

    private bool TrySend(object frame)
    {
        lock (_portSync)
        {
            if (_activePort?.IsOpen != true || IsPaused) return false;
            try
            {
                _activePort.WriteLine(Prefix + JsonSerializer.Serialize(frame, JsonDefaults.Options));
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
                return false;
            }
        }
    }

    private void Write(SerialPort port, object frame)
    {
        lock (_portSync)
            if (port.IsOpen && !IsPaused && UsbDataEnabled) {
                port.WriteLine(Prefix + JsonSerializer.Serialize(frame, JsonDefaults.Options));
                Interlocked.Exchange(ref _lastStatusWrittenAt,Environment.TickCount64);
            }
    }

    private static SerialPort CreatePort(string name) => new(name, 460800)
    {
        NewLine = "\n",
        ReadTimeout = 2400,
        WriteTimeout = 2000,
        DtrEnable = false,
        RtsEnable = false
    };

    private static bool WaitForPong(SerialPort port, out string? deviceHost, out string? firmware)
    {
        deviceHost = null;
        firmware = null;
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var line = port.ReadLine().Trim();
                if (TryParsePong(line, out deviceHost, out firmware))
                    return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
        return false;
    }

    private void RefreshDeviceHost(SerialPort port)
    {
        lock (_portSync)
        {
            if (!ReferenceEquals(_activePort, port) || !port.IsOpen || IsPaused) return;
            try
            {
                port.WriteLine(Prefix + "{\"version\":1,\"type\":\"ping\"}");
                if (WaitForPong(port, out var deviceHost, out var firmware)) {
                    _deviceHost = deviceHost; _usbFirmwareVersion = firmware;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
            }
        }
    }

    internal static bool TryParsePong(string line, out string? deviceHost)
        => TryParsePong(line, out deviceHost, out _);

    internal static bool TryParsePong(string line, out string? deviceHost, out string? firmware)
    {
        deviceHost = null;
        firmware = null;
        if (!line.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            using var document = JsonDocument.Parse(line[Prefix.Length..]);
            var root = document.RootElement;
            if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1 ||
                !root.TryGetProperty("type", out var type) || type.GetString() != "pong" ||
                !root.TryGetProperty("device", out var device) || device.GetString() != "esp8266")
                return false;
            if (root.TryGetProperty("ip", out var ip) && ip.ValueKind == JsonValueKind.String)
            {
                var candidate = ip.GetString();
                if (candidate is not null && IsPrivateIPv4(candidate)) deviceHost = candidate;
            }
            firmware = EspFirmwareVersion.Read(root.TryGetProperty("firmware", out var field)
                ? field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "" : null);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static bool IsPrivateIPv4(string value)
    {
        var parts = value.Split('.', StringSplitOptions.None);
        if (parts.Length != 4) return false;
        Span<byte> octets = stackalloc byte[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!byte.TryParse(parts[index], out octets[index]) ||
                parts[index] != octets[index].ToString()) return false;
        }
        return octets[0] == 10 ||
               octets[0] == 192 && octets[1] == 168 ||
               octets[0] == 172 && octets[1] is >= 16 and <= 31;
    }

    private static bool WaitForResourceAck(SerialPort port, uint transferId, ushort sequence)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var line = port.ReadLine().Trim();
                if (!line.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                using var document = JsonDocument.Parse(line[Prefix.Length..]);
                var root = document.RootElement;
                if (root.GetProperty("type").GetString() == "resource_ack" &&
                    root.GetProperty("transferId").GetUInt32() == transferId &&
                    root.GetProperty("sequence").GetUInt16() == sequence)
                    return root.GetProperty("ok").GetBoolean();
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (JsonException)
            {
            }
        }
        return false;
    }

    internal static IReadOnlyList<string> BoundPorts(string? identity,IReadOnlyList<FlashUsbDevice> devices,Func<IReadOnlyList<string>> unboundPorts)=>
        identity is null?unboundPorts():devices.Where(d=>d.Identity==identity).Select(d=>d.Port).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private IReadOnlyList<string> CandidatePorts()
    {
        var configured = NormalizePort(Environment.GetEnvironmentVariable("AIBOT_PORT")) ?? _preferredPort;
        if (configured is not null)
            return [configured];

        return SerialPort.GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizePort(string? value)
    {
        var port = value?.Trim().ToUpperInvariant();
        return port is { Length: > 3 } && port.StartsWith("COM", StringComparison.Ordinal) &&
               port[3..].All(char.IsDigit) ? port : null;
    }
}
