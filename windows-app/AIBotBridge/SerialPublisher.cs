using System.IO.Ports;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class SerialPublisher
{
    private const string Prefix = "@AIBOT ";
    private readonly object _portSync = new();
    private volatile string? _portName;
    private SerialPort? _activePort;
    private readonly LanPairing? _pairing;

    internal SerialPublisher(LanPairing? pairing)
    {
        _pairing = pairing;
    }

    internal string? PortName => _portName;

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

                    if (!WaitForPong(port))
                        continue;

                    _portName = candidate;
                    lock (_portSync) _activePort = port;
                    var sentRevisions = new Dictionary<BinaryResourceKind, int>();
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

    private static bool WaitForPong(SerialPort port)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var line = port.ReadLine().Trim();
                if (line.StartsWith(Prefix, StringComparison.Ordinal) &&
                    line.Contains("\"type\":\"pong\"", StringComparison.Ordinal))
                    return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
        return false;
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

    private static IReadOnlyList<string> CandidatePorts()
    {
        var configured = Environment.GetEnvironmentVariable("AIBOT_PORT");
        if (!string.IsNullOrWhiteSpace(configured))
            return [configured.Trim()];

        return SerialPort.GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
