namespace AIBotBridge;

internal static class SerialDisconnectSelfTest
{
    internal static async Task RunAsync() {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var recovered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sample=new SystemMetricsSnapshot(10,20,30,40,DateTimeOffset.UtcNow);
        int attempts=0,delivered=0;
        var publisher=new SerialPublisher(null,writeOverride:line=> {
            if(!line.StartsWith("@AIBOT ",StringComparison.Ordinal)||!line.Contains("\"metrics\""))throw new Exception("Unexpected transport frame");
            int attempt=Interlocked.Increment(ref attempts);
            switch(attempt) {
                case 1:throw new UnauthorizedAccessException("Disconnected serial device fixture");
                case 2:throw new IOException("Lost USB fixture");
                case 3:throw new System.ComponentModel.Win32Exception(5);
                case 4:throw new TimeoutException("Write timeout fixture");
                case 5:throw new InvalidOperationException("Closed port fixture");
            }
            if(Interlocked.Increment(ref delivered)==1)sample=sample with{UpdatedAt=sample.UpdatedAt.AddSeconds(1)};
            else recovered.TrySetResult();
        });
        publisher.UsbDataEnabled=false;
        var worker=publisher.RunMetricsAsync(()=>sample,stop.Token);
        try {
            await Task.Delay(350,stop.Token);
            if(attempts!=0||worker.IsCompleted)throw new Exception("Disabled USB must wait without sending or faulting");
            publisher.UsbDataEnabled=true;
            if(await Task.WhenAny(worker,recovered.Task)!=recovered.Task) {
                await worker;
                throw new Exception("Metrics worker stopped before USB recovery");
            }
            if(attempts!=7||delivered!=2||worker.IsFaulted)throw new Exception("Metrics did not retry and resume after serial disconnection");
        } finally {
            stop.Cancel();
            try{await worker;}catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        }
        Console.WriteLine("SERIAL_DISCONNECT_OK disabled USB, access denied, disconnect, Win32, timeout, closed port, retry and resumed metrics");
    }
}
