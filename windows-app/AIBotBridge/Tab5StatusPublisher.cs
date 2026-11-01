namespace AIBotBridge;

// Runs as a DeviceServiceManager worker, never on the WinForms dispatcher.
// A slow publication has at most one coalesced tick, not a queue of stale frames.
// The manager cancels and awaits this worker before disposing the paired service.
internal static class Tab5StatusPublisher
{
    internal static async Task RunAsync(Action publish,CancellationToken token,TimeSpan? interval=null)
    {
        using var timer=new PeriodicTimer(interval??TimeSpan.FromSeconds(2));
        do {
            token.ThrowIfCancellationRequested();
            publish();
        } while(await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }
}
