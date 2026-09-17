using System.Net;
using System.Net.Sockets;

namespace AIBotBridge;

internal static class ListenerStartupSelfTest
{
    internal static async Task RunAsync()
    {
        var owner = new TcpListener(IPAddress.Loopback, 0);
        owner.ExclusiveAddressUse = true;
        owner.Start();
        var port = ((IPEndPoint)owner.LocalEndpoint).Port;
        var listener = new TcpListener(IPAddress.Loopback, port);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var pending = ListenerStartup.StartAsync(listener, stop.Token);
            if (pending.IsCompleted) throw new Exception("Occupied port did not defer startup.");
            owner.Stop();
            await pending;
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, stop.Token);
            using var accepted = await listener.AcceptTcpClientAsync(stop.Token);
        }
        finally { owner.Stop(); listener.Stop(); }
        var blocked = new TcpListener(IPAddress.Loopback, 0); blocked.Start();
        var retry = new TcpListener(IPAddress.Loopback, ((IPEndPoint)blocked.LocalEndpoint).Port);
        using var cancel = new CancellationTokenSource();
        try {
            var pending = ListenerStartup.StartAsync(retry, cancel.Token);
            cancel.Cancel();
            try { await pending; throw new Exception("Cancelled startup did not stop."); }
            catch (OperationCanceledException) { }
        }
        finally { blocked.Stop(); retry.Stop(); }
        Console.WriteLine("LISTENER_STARTUP_OK contention recovery and cancellation");
    }
}
