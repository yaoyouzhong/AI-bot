using System.IO.Ports;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class SerialPublisher
{
    private const string Prefix = "@AIBOT ";
    private readonly object _portSync = new();
    private volatile string? _portName;
    private volatile string? _deviceHost;
    private SerialPort? _activePort;
    private readonly LanPairing? _pairing;
    private readonly string? _preferredPort;

    internal SerialPublisher(LanPairing? pairing, string? preferredPort = null)
    {
        _pairing = pairing;
        _preferredPort = NormalizePort(preferredPort);
    }

    internal string? PortName => _portName;
    internal string? DeviceHost => _deviceHost;

    internal bool SendDisplayMode(string mode) => TrySend(new
    {
        version = 1,
        type = "display",
        mode
    });

    internal bool SendBrightness(int level) => TrySend(new
    {
        version = 1,
        type = "brightness",
        level = Math.Clamp(level, 0, 100)
    });

    internal bool SendResource(BinaryResourceKind kind, byte[] data)
    {
        var transferId = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
        var chunks = BinaryResourceProtocol.CreateChunks(kind, data, transferId);
        lock (_portSync)
        {
            if (_activePort?.IsOpen != true) return false;
            foreach (var chunk in chunks)
            {
                var acknowledged = false;
                for (var attempt = 0; attempt < 3 && !acknowledged; attempt++)
                {
                    _activePort.Write(chunk.WireBytes, 0, chunk.WireBytes.Length);
                    acknowledged = WaitForResourceAck(_activePort, chunk.TransferId, chunk.Sequence);
                }
                if (!acknowledged) return false;
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
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var candidate in CandidatePorts())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                using var port = CreatePort(candidate);
                try
                {
                    port.Open();
                    await Task.Delay(1200, cancellationToken);
                    port.DiscardInBuffer();
                    port.WriteLine(Prefix + "{\"version\":1,\"type\":\"ping\"}");

                    if (!WaitForPong(port, out var deviceHost))
                        continue;

                    _portName = candidate;
                    _deviceHost = deviceHost;
                    lock (_portSync) _activePort = port;
                    var sentRevisions = new Dictionary<BinaryResourceKind, int>();
                    var nextDeviceProbeAt = DateTime.UtcNow.AddSeconds(deviceHost is null ? 5 : 30);
                    if (_pairing is not null)
                    {
                        var pairingFrame = new
                        {
                            version = 1,
                            type = "lan_config",
                            data = new
                            {
                                host = _pairing.Address.ToString(),
                                port = _pairing.Port,
                                token = _pairing.Token
                            }
                        };
                        Write(port, pairingFrame);
                    }
                    while (!cancellationToken.IsCancellationRequested && port.IsOpen)
                    {
                        if (DateTime.UtcNow >= nextDeviceProbeAt)
                        {
                            RefreshDeviceHost(port);
                            nextDeviceProbeAt = DateTime.UtcNow.AddSeconds(_deviceHost is null ? 5 : 30);
                        }
                        foreach (var resource in resources())
                        {
                            if (sentRevisions.TryGetValue(resource.Kind, out var revision) &&
                                revision == resource.Revision) continue;
                            if (SendResource(resource.Kind, resource.Data))
                                sentRevisions[resource.Kind] = resource.Revision;
                        }
                        var frame = new
                        {
                            version = 1,
                            type = "status",
                            data = snapshot()
                        };
                        Write(port, frame);
                        lock (_portSync)
                            if (port.IsOpen && port.BytesToRead > 0) port.ReadExisting();
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
                }
                finally
                {
                    lock (_portSync)
                        if (ReferenceEquals(_activePort, port)) _activePort = null;
                    _portName = null;
                    _deviceHost = null;
                }
            }

            await Task.Delay(3000, cancellationToken);
        }
    }

    private bool TrySend(object frame)
    {
        lock (_portSync)
        {
            if (_activePort?.IsOpen != true) return false;
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
            if (port.IsOpen)
                port.WriteLine(Prefix + JsonSerializer.Serialize(frame, JsonDefaults.Options));
    }

    private static SerialPort CreatePort(string name) => new(name, 460800)
    {
        NewLine = "\n",
        ReadTimeout = 2400,
        WriteTimeout = 2000,
        DtrEnable = false,
        RtsEnable = false
    };

    private static bool WaitForPong(SerialPort port, out string? deviceHost)
    {
        deviceHost = null;
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var line = port.ReadLine().Trim();
                if (TryParsePong(line, out deviceHost))
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
            if (!ReferenceEquals(_activePort, port) || !port.IsOpen) return;
            try
            {
                port.WriteLine(Prefix + "{\"version\":1,\"type\":\"ping\"}");
                if (WaitForPong(port, out var deviceHost)) _deviceHost = deviceHost;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
            }
        }
    }

    internal static bool TryParsePong(string line, out string? deviceHost)
    {
        deviceHost = null;
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
