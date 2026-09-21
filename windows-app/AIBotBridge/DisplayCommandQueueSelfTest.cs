using System.Collections.Concurrent;
using System.Diagnostics;

namespace AIBotBridge;

internal static class DisplayCommandQueueSelfTest
{
    internal static async Task RunAsync()
    {
        var gate = new object();
        var sent = new ConcurrentQueue<string>();
        var queue = new DisplayCommandQueue(gate, sent.Enqueue);
        using var occupied = new ManualResetEventSlim();
        using var drain = new ManualResetEventSlim();
        using var drained = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var transfer = Task.Run(() =>
        {
            lock (gate)
            {
                occupied.Set();
                if (!drain.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                queue.Drain(); // A chunk completes, while the transfer still owns the port.
                drained.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            }
        });
        try
        {
            Require(occupied.Wait(TimeSpan.FromSeconds(5)), "Transfer acquired transport");
            var clock = Stopwatch.StartNew();
            queue.Enqueue("weather"); queue.Enqueue("codex"); queue.Enqueue("stocks");
            Require(clock.ElapsedMilliseconds < 250, "UI must not wait for transfer");
            Require(sent.IsEmpty, "Do not interleave writes inside a chunk/ACK exchange");
            drain.Set();
            Require(drained.Wait(TimeSpan.FromSeconds(5)), "Transfer services pending display command");
            Require(sent.SequenceEqual(["stocks"]) && !transfer.IsCompleted, "Latest selection sent before transfer finishes");
            release.Set(); await transfer;
            queue.Enqueue("system");
            var deadline = Stopwatch.StartNew();
            while (sent.Count < 2 && deadline.ElapsedMilliseconds < 5000) await Task.Delay(10);
            Require(sent.SequenceEqual(["stocks", "system"]), "No replay of superseded selection; worker delivers when idle");
            Console.WriteLine("DISPLAY_QUEUE_SELF_TEST_OK nonblocking/latest-wins/chunk-priority/no-replay/idle");
        }
        finally { drain.Set(); release.Set(); await transfer; }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
