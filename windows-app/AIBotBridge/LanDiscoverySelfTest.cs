using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal static class LanDiscoverySelfTest
{
    internal static void Run()
    {
        const string token = "synthetic-pairing-token-for-discovery-test";
        const string nonce = "0123456789abcdef";
        var request = "AIBOT_DISCOVER_V1|" + nonce;
        var proof = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(token), Encoding.ASCII.GetBytes(request))).ToLowerInvariant();
        if (!LanDiscoveryProtocol.TryReadRequest(
            Encoding.ASCII.GetBytes(request + "|" + proof), token, out var parsed) || parsed != nonce)
            throw new InvalidOperationException("Paired discovery request was rejected.");
        if (LanDiscoveryProtocol.TryReadRequest(
            Encoding.ASCII.GetBytes(request + "|" + proof), "wrong-token", out _))
            throw new InvalidOperationException("Unpaired discovery request was accepted.");
        var pairing = new LanPairing(IPAddress.Parse("192.168.255.191"), 18765, token);
        var response = Encoding.ASCII.GetString(LanDiscoveryProtocol.CreateResponse(nonce, pairing));
        var fields = response.Split('|');
        if (fields.Length != 5 || fields[0] != "AIBOT_BRIDGE_V1" || fields[1] != nonce ||
            fields[2] != "192.168.255.191" || fields[3] != "18765")
            throw new InvalidOperationException("Discovery response fields changed.");
        var payload = string.Join('|', fields.Take(4));
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(token), Encoding.ASCII.GetBytes(payload));
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(fields[4])))
            throw new InvalidOperationException("Discovery response signature changed.");
        Console.WriteLine("LAN_DISCOVERY_SELF_TEST_OK");
    }

    internal static async Task RunAddressChangeAsync()
    {
        const string token = "synthetic-pairing-token-for-discovery-test";
        var firstHost = IPAddress.Parse("127.0.0.1");
        var secondHost = IPAddress.Parse("127.0.0.2");
        var firstDevice = IPAddress.Parse("127.0.0.3");
        var secondDevice = IPAddress.Parse("127.0.0.4");
        var httpPort = ReserveTcpPort();
        var discoveryPort = ReserveUdpPort();
        IPAddress Select(string? host) => host == secondDevice.ToString() ? secondHost : firstHost;
        var discovery = new LanDiscoveryServer(discoveryPort, host => Select(host));
        var serial = new SerialPublisher(null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var binding = LanBindingManager.RunAsync(httpPort, serial,
            () => throw new InvalidOperationException("Synthetic LAN status was requested."),
            () => [], stop.Token, host => Select(host),
            address => new LanPairing(address, httpPort, token), discovery);
        var receiver = discovery.RunAsync(stop.Token);
        try
        {
            await WaitForAddressAsync(discovery, firstHost, stop.Token);
            await SendAndCheckAsync(firstDevice, firstHost, discoveryPort, httpPort, token, stop.Token);
            await SendAndCheckAsync(secondDevice, secondHost, discoveryPort, httpPort, token, stop.Token);
            if (!Equals(serial.CurrentPairing?.Address, secondHost))
                throw new InvalidOperationException("Discovery did not update the LAN binding.");
            Console.WriteLine("LAN_DISCOVERY_ADDRESS_CHANGE_OK");
        }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(binding, receiver); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    private static async Task SendAndCheckAsync(IPAddress device, IPAddress expectedHost,
        int discoveryPort, int httpPort, string token, CancellationToken cancellationToken)
    {
        using var sender = new UdpClient(new IPEndPoint(device, 0));
        var nonce = device.Equals(IPAddress.Parse("127.0.0.3"))
            ? "0123456789abcdef" : "fedcba9876543210";
        await sender.SendAsync(LanDiscoveryProtocol.CreateRequest(nonce, token),
            new IPEndPoint(IPAddress.Loopback, discoveryPort), cancellationToken);
        var reply = await sender.ReceiveAsync(cancellationToken);
        var fields = Encoding.ASCII.GetString(reply.Buffer).Split('|');
        if (fields.Length != 5 || fields[0] != "AIBOT_BRIDGE_V1" || fields[1] != nonce ||
            fields[2] != expectedHost.ToString() || fields[3] != httpPort.ToString() ||
            !reply.RemoteEndPoint.Address.Equals(expectedHost))
            throw new InvalidOperationException("Discovery replied from the wrong host address.");
        var payload = string.Join('|', fields.Take(4));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.ASCII.GetBytes(payload));
        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(fields[4])))
            throw new InvalidOperationException("Discovery reply signature is invalid.");
    }

    private static async Task WaitForAddressAsync(LanDiscoveryServer server, IPAddress expected, CancellationToken token)
    {
        while (!Equals(server.Binding?.Address, expected)) await Task.Delay(50, token);
    }

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        return port;
    }

    private static int ReserveUdpPort()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
    }
}
