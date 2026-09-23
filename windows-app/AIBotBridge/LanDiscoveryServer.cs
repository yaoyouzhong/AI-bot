using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal sealed class LanDiscoveryServer
{
    internal const int Port = 18766;
    private readonly int _port;
    private readonly Func<string, IPAddress?> _findAddress;
    private volatile string? _deviceHost;
    private LanPairing? _binding;

    internal LanDiscoveryServer(int port = Port, Func<string, IPAddress?>? findAddress = null)
    {
        _port = port;
        _findAddress = findAddress ?? (host => LanPairingFactory.FindPrivateAddress(host, requireSubnet: true));
    }

    internal string? DeviceHost => _deviceHost;
    internal LanPairing? Binding => Volatile.Read(ref _binding);
    internal void SetBinding(LanPairing? binding) => Volatile.Write(ref _binding, binding);

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await ServeAsync(cancellationToken); }
            catch (SocketException ex) when (!cancellationToken.IsCancellationRequested)
            {
                QuotaRequestDiagnostics.RecordLocalServerFailure(ex.SocketErrorCode.ToString());
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try { packet = await receiver.ReceiveAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            if (packet.Buffer.Length > 160 || packet.RemoteEndPoint.Address.AddressFamily != AddressFamily.InterNetwork)
                continue;
            var pairing = Binding;
            if (pairing is null || !LanDiscoveryProtocol.TryReadRequest(
                packet.Buffer, pairing.Token, out var nonce)) continue;

            var device = packet.RemoteEndPoint.Address;
            var expected = _findAddress(device.ToString());
            if (expected is null) continue;
            _deviceHost = device.ToString();
            for (var attempt = 0; attempt < 40 && !cancellationToken.IsCancellationRequested; attempt++)
            {
                pairing = Binding;
                if (pairing is not null && pairing.Address.Equals(expected)) break;
                await Task.Delay(100, cancellationToken);
            }
            pairing = Binding;
            if (pairing is null || !pairing.Address.Equals(expected)) continue;
            var response = LanDiscoveryProtocol.CreateResponse(nonce, pairing);
            using var sender = new UdpClient(new IPEndPoint(pairing.Address, 0));
            await sender.SendAsync(response, packet.RemoteEndPoint, cancellationToken);
        }
    }
}

internal static class LanDiscoveryProtocol
{
    private const string Request = "AIBOT_DISCOVER_V1";
    private const string Response = "AIBOT_BRIDGE_V1";

    internal static byte[] CreateRequest(string nonce, string token)
    {
        var payload = Request + "|" + nonce;
        var proof = HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.ASCII.GetBytes(payload));
        return Encoding.ASCII.GetBytes(payload + "|" + Convert.ToHexString(proof).ToLowerInvariant());
    }

    internal static bool TryReadRequest(byte[] bytes, string token, out string nonce)
    {
        nonce = string.Empty;
        var parts = Encoding.ASCII.GetString(bytes).Split('|');
        if (parts.Length != 3 || parts[0] != Request || !IsHex(parts[1], 16) ||
            !IsHex(parts[2], 64)) return false;
        var payload = Request + "|" + parts[1];
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.ASCII.GetBytes(payload));
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(parts[2]))) return false;
        nonce = parts[1];
        return true;
    }

    internal static byte[] CreateResponse(string nonce, LanPairing pairing)
    {
        var payload = $"{Response}|{nonce}|{pairing.Address}|{pairing.Port}";
        var proof = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pairing.Token), Encoding.ASCII.GetBytes(payload));
        return Encoding.ASCII.GetBytes(payload + "|" + Convert.ToHexString(proof).ToLowerInvariant());
    }

    private static bool IsHex(string value, int length) => value.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
