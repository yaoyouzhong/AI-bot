namespace AIBotBridge;

// Runs as a DeviceServiceManager worker, never on the WinForms dispatcher.
// A slow publication has at most one coalesced tick, not a queue of stale frames.
// The manager cancels and awaits this worker before disposing the paired service.
internal static class Tab5StatusPublisher
{
    internal static async Task RunAsync(Action publish,CancellationToken token,TimeSpan? interval=null,Action? refresh=null)
    {
        var period=interval??TimeSpan.FromSeconds(2);
        using var timer=new PeriodicTimer(refresh is null?period:TimeSpan.FromMilliseconds(500));
        long next=0;
        do {
            token.ThrowIfCancellationRequested();
            long now=Environment.TickCount64;
            if(now>=next) { next=now+(long)period.TotalMilliseconds;publish(); }
            else refresh?.Invoke();
        } while(await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }
}
