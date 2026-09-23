using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AIBotBridge;

internal static class LanBindingSelfTest
{
    internal static void Run()
    {
        var wired = new LanAddressCandidate(IPAddress.Parse("192.168.20.77"), IPAddress.Parse("255.255.255.0"));
        var wireless = new LanAddressCandidate(IPAddress.Parse("192.168.255.191"), IPAddress.Parse("255.255.254.0"));
        var addresses = new[] { wired, wireless };
        if (!Equals(LanPairingFactory.SelectAddress(addresses, "192.168.254.14"), wireless.Address))
            throw new InvalidOperationException("Device subnet did not select the /23 wireless adapter.");
        if (!Equals(LanPairingFactory.SelectAddress(addresses, "192.168.20.14"), wired.Address))
            throw new InvalidOperationException("Device subnet did not select the wired adapter.");
        if (!Equals(LanPairingFactory.SelectAddress(addresses, null), wired.Address))
            throw new InvalidOperationException("Adapter selection without a device address changed.");
        Console.WriteLine("LAN_BINDING_SELF_TEST_OK");
    }

    internal static async Task RunRebindingAsync()
    {
        var first = IPAddress.Loopback;
        var second = IPAddress.Parse("127.0.0.2");
        var selected = first;
        var serial = new SerialPublisher(null);
        var reserved = new TcpListener(first, 0);
        reserved.Start();
        var port = ((IPEndPoint)reserved.LocalEndpoint).Port;
        reserved.Stop();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var run = LanBindingManager.RunAsync(port, serial,
            () => throw new InvalidOperationException("Unauthenticated request reached the snapshot."),
            () => [], stop.Token,
            _ => Volatile.Read(ref selected),
            address => new LanPairing(address, port, "synthetic-test-token"));
        try
        {
            await WaitForBindingAsync(serial, first, stop.Token);
            await ExpectUnauthorizedAsync(first, port, stop.Token);
            Volatile.Write(ref selected, second);
            await WaitForBindingAsync(serial, second, stop.Token);
            await ExpectUnauthorizedAsync(second, port, stop.Token);
            await WaitForClosedAsync(first, port, stop.Token);
            Console.WriteLine("LAN_REBINDING_SELF_TEST_OK");
        }
        finally
        {
            stop.Cancel();
            await run;
        }
    }

    private static async Task WaitForBindingAsync(SerialPublisher serial, IPAddress expected, CancellationToken token)
    {
        while (!Equals(serial.CurrentPairing?.Address, expected))
            await Task.Delay(50, token);
    }

    private static async Task ExpectUnauthorizedAsync(IPAddress address, int port, CancellationToken token)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(address, port, token);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /status HTTP/1.1\r\nHost: localhost\r\n\r\n"), token);
        var line = await reader.ReadLineAsync(token);
        if (line != "HTTP/1.1 401 Unauthorized")
            throw new InvalidOperationException($"Listener at {address} returned {line}.");
    }

    private static async Task WaitForClosedAsync(IPAddress address, int port, CancellationToken token)
    {
        while (true)
        {
            using var client = new TcpClient();
            try { await client.ConnectAsync(address, port, token); }
            catch (SocketException) { return; }
            await Task.Delay(50, token);
        }
    }
}
