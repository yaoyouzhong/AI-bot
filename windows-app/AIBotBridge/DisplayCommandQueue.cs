namespace AIBotBridge;

// The UI never waits for the serial transport. Its owner can drain the latest
// selection between acknowledged chunks without releasing the ACK reader lock.
internal sealed class DisplayCommandQueue(object transportSync, Action<string> send)
{
    private string? _pending;
    private int _worker;

    internal void Enqueue(string mode)
    {
        Interlocked.Exchange(ref _pending, mode);
        Schedule();
    }

    private void Schedule()
    {
        if (Interlocked.CompareExchange(ref _worker, 1, 0) != 0) return;
        _ = Task.Run(() =>
        {
            try { lock (transportSync) Drain(); }
            finally
            {
                Volatile.Write(ref _worker, 0);
                if (Volatile.Read(ref _pending) is not null) Schedule();
            }
        });
    }

    // Caller must own transportSync, including the chunk/ACK writer.
    internal void Drain()
    {
        if (!Monitor.IsEntered(transportSync)) throw new SynchronizationLockException();
        if (Interlocked.Exchange(ref _pending, null) is { } mode) send(mode);
    }
}
