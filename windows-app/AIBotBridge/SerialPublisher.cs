using System.IO.Ports;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class SerialPublisher
{
    private const string Prefix = "@AIBOT ";
    private volatile string? _portName;

    internal string? PortName => _portName;

    internal async Task RunAsync(Func<StatusSnapshot> snapshot, CancellationToken cancellationToken)
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
                    while (!cancellationToken.IsCancellationRequested && port.IsOpen)
                    {
                        var frame = new
                        {
                            version = 1,
                            type = "status",
                            data = snapshot()
                        };
                        port.WriteLine(Prefix + JsonSerializer.Serialize(frame, JsonDefaults.Options));
                        if (port.BytesToRead > 0)
                            port.ReadExisting();
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
                    _portName = null;
                }
            }

            await Task.Delay(3000, cancellationToken);
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
