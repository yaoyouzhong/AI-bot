namespace AIBotBridge;

internal static class FlashUsbLeaseSelfTest
{
    internal static async Task RunAsync()
    {
        var name = "AIBotBridge.FlashUsb.test." + Guid.NewGuid().ToString("N");
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumes = 0; var restores = 0; var paused = false;
        var server = FlashUsbLease.ServeAsync(async token =>
        {
            pauseStarted.TrySetResult();
            await closed.Task.WaitAsync(token);
            paused = true;
        }, () => { paused = false; Interlocked.Increment(ref resumes); },
        _ => { Interlocked.Increment(ref restores); return Task.CompletedTask; }, shutdown.Token, name);
        try
        {
            var acquire = FlashUsbLease.AcquireAsync(shutdown.Token, name);
            await pauseStarted.Task.WaitAsync(shutdown.Token);
            if (acquire.IsCompleted) throw new Exception("USB lease acknowledged before the port closed");
            closed.SetResult();
            using (var lease = await acquire)
            {
                if (!paused) throw new Exception("USB was not paused");
                await lease.ReleaseAsync(true);
                if (paused || resumes != 1 || restores != 1) throw new Exception("Normal USB/cycle recovery failed");
            }
            // Simulate process death: no release command, only disconnect.
            (await FlashUsbLease.AcquireAsync(shutdown.Token, name)).Dispose();
            while (Volatile.Read(ref resumes) < 2) await Task.Delay(10, shutdown.Token);
            if (paused || restores != 1) throw new Exception("Disconnect did not resume or changed cycle settings");
            using (var lease = await FlashUsbLease.AcquireAsync(shutdown.Token, name))
            {
                await lease.ReleaseAsync(false);
                if (resumes != 3 || restores != 1) throw new Exception("Backup modified cycle settings");
            }
            closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pauseStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            var abandoned = FlashUsbLease.AcquireAsync(cancel.Token, name);
            await pauseStarted.Task.WaitAsync(shutdown.Token);
            cancel.Cancel();
            try { await abandoned; throw new Exception("Cancelled handoff was accepted"); }
            catch (OperationCanceledException) { }
            closed.SetResult();
            while (Volatile.Read(ref resumes) < 4) await Task.Delay(10, shutdown.Token);
            if (paused || restores != 1) throw new Exception("Cancelled handoff left USB paused");
        }
        finally
        {
            shutdown.Cancel();
            await server;
        }
        Console.WriteLine("FLASH_USB_LEASE_OK wait-for-close/normal/backup/disconnect/cancel-handoff/no-network-listener");
    }
}
