using System.Collections.Concurrent;
using System.IO.Compression;

namespace AIBotBridge;
internal static class DeviceRecoverySelfTest
{
    internal static async Task RunAsync() {
        if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        await StatusPublisherAsync();
        var history=new DeviceConnectionHistory();var now=DateTimeOffset.UtcNow;
        var a=history.Observe("secret-id",true,true,1000,"private",now,1000);
        Check(a.Recoveries==0&&a.LastCommunication==now,"Initial communication recorded incorrectly");
        history.Observe("secret-id",true,false,1000,"有效通信超时",now.AddSeconds(16),17000);
        var b=history.Observe("secret-id",true,true,18000,"",now.AddSeconds(17),18000);
        Check(b.Recoveries==1&&b.LastDisconnected==now.AddSeconds(16),"Reconnect transition lost");
        var path=Path.Combine(Path.GetTempPath(),"aibot-diagnostics-"+Guid.NewGuid().ToString("N")+".zip");
        try {
            DeviceConnectionHistory.Export(path,[(HardwareKind.Tab5,b,true,true)]);
            using var zip=ZipFile.OpenRead(path);using var reader=new StreamReader(zip.Entries.Single().Open());
            Check(!reader.ReadToEnd().Contains("secret-id"),"Export leaked registry identity");
        }finally{File.Delete(path);}
        using var runtime=new BridgeRuntime(startRefresh:false);
        var pair=new Tab5PairingStore();pair.Pair("001122334455",@"USB\TEST");
        var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"private name",null);
        var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"private tablet",pair.Current!.DeviceId);
        var registry=new DeviceRegistry(1,true,false,false,[esp,tab]);
        var starts=new ConcurrentDictionary<string,int>();var stops=new ConcurrentDictionary<string,int>();bool fail=false;
        async Task Worker(string name,CancellationToken token) {
            starts.AddOrUpdate(name,1,(_,n)=>n+1);
            try{if(fail&&name=="esp-usb")throw new IOException("simulated fault");await Task.Delay(Timeout.Infinite,token);}
            finally{stops.AddOrUpdate(name,1,(_,n)=>n+1);}
        }
        var manager=new DeviceServiceManager(runtime,0,CancellationToken.None,Worker);
        try {
            await manager.ApplyAsync(registry);await Task.Delay(80);var original=manager.Tab5;
            Check(starts["tab5-status"]==1,"TAB5 status worker was not started");
            await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(ReferenceEquals(original,manager.Tab5)&&starts["esp-usb"]==2&&starts["tab5-usb"]==1&&starts["lan"]==1,"Reconnect restarted unrelated services");
            original!.OtaTransferActive(true);
            try{await manager.ReconnectAsync(registry,tab.Id);throw new Exception("Busy reconnect accepted");}catch(InvalidOperationException){}finally{original.OtaTransferActive(false);}
            await manager.ReconnectAsync(registry,tab.Id);await Task.Delay(80);
            Check(!ReferenceEquals(original,manager.Tab5)&&starts["tab5-usb"]==2&&starts["lan"]==1,"TAB5 was not independently recreated");
            Check(starts["tab5-status"]==2&&stops["tab5-status"]==1,"Status worker did not follow paired service lifetime");
            fail=true;await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(manager.View(esp).Status=="连接异常","Worker failure not visible");
            Check(manager.FaultSources==RuntimeFaultSource.Esp8266,"ESP failure attributed to unrelated device");
            var fault=RuntimeHealthGuard.Check(3,now,sources:manager.FaultSources);
            Check(fault.Summary=="小屏通信服务异常"&&fault.Advice.Contains("重新连接"),"Fault source/advice lost");
            Check(ConnectionAdvice.For(true,manager.View(esp)).Contains("重新连接"),"Service failure guidance missing");
            fail=false;await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(manager.View(esp).Status!="连接异常"&&manager.FaultSources==RuntimeFaultSource.None,"Faulted worker could not be recovered");
            Check(ConnectionAdvice.For(true,new(false,"离线","","",Wifi:"已联网 · 桥接未通")).Contains("同一局域网"),"Wi-Fi guidance lost");
            Check(ConnectionAdvice.For(true,new(false,"离线","","",Wifi:"等待设备回报")).Contains("等待接入"),"Offline treated as service fault");
            Check(ConnectionAdvice.For(false,new(false,"离线","","" )).Contains("已停用"),"Disabled device guidance incorrect");
            Check(RuntimeHealthGuard.Check(3,now,sources:RuntimeFaultSource.Tab5).Summary=="TAB5 通信服务异常","TAB5 fault source lost");
            var multiple=RuntimeHealthGuard.Check(3,now,sources:RuntimeFaultSource.Esp8266|RuntimeFaultSource.Lan);
            Check(multiple.Detail.Contains("小屏通信")&&multiple.Detail.Contains("局域网"),"Multiple fault sources lost");
            Check(RuntimeHealthGuard.Check(0,now,sources:RuntimeFaultSource.Esp8266).State=="ok","Stale source revived recovered alarm");
            Check(pair.Current.Key==new Tab5PairingStore().Current!.Key,"Reconnect altered credentials");
            fail=true;await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            await manager.StopAsync();
            Check(manager.ActiveServices.Length==0&&manager.Error?.Contains(nameof(IOException))==true,"Faulted worker interrupted bridge shutdown or lost its error");
            Check(starts["tab5-status"]==stops["tab5-status"],"Status worker survived shutdown");
        }finally{await manager.StopAsync();}
        Console.WriteLine("DEVICE_RECOVERY_OK timestamps, counters, private export, isolated restart, busy protection, fault recovery");
    }

    private static async Task StatusPublisherAsync() {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        using var stop=new CancellationTokenSource();
        using var release=new ManualResetEventSlim();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls=0,active=0,maximum=0,latest=1,observed=0;
        var run=Task.Run(()=>Tab5StatusPublisher.RunAsync(()=>{
            int concurrent=Interlocked.Increment(ref active);maximum=Math.Max(maximum,concurrent);
            int call=Interlocked.Increment(ref calls);
            try {
                if(call==1){entered.SetResult();if(!release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();}
                observed=Volatile.Read(ref latest);
                if(call==2)stop.Cancel();
            }finally{Interlocked.Decrement(ref active);}
        },stop.Token,TimeSpan.FromMilliseconds(10)));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(150); // Many timer periods expire while one frame is blocked.
            Check(calls==1&&!run.IsCompleted,"Slow status publication overlapped or detached from worker lifetime");
            Volatile.Write(ref latest,2);release.Set();
            try{await run.WaitAsync(TimeSpan.FromSeconds(5));throw new Exception("Cancelled publisher kept running");}
            catch(OperationCanceledException) when(stop.IsCancellationRequested){}
            Check(calls==2&&maximum==1&&active==0&&observed==2,"Status backlog, stale capture or overlapping publication");
        }finally{release.Set();stop.Cancel();try{await run;}catch(OperationCanceledException){}}

        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try{await Tab5StatusPublisher.RunAsync(()=>throw new Exception("Published after cancellation"),canceled.Token);}
        catch(OperationCanceledException) when(canceled.IsCancellationRequested){}
        try{await Tab5StatusPublisher.RunAsync(()=>throw new IOException("synthetic status failure"),CancellationToken.None);throw new Exception("Status error swallowed");}
        catch(IOException ex) when(ex.Message=="synthetic status failure"){}

        // Normal exit must await an in-flight publication before disposing its service.
        using var draining=new CancellationTokenSource();using var finish=new ManualResetEventSlim();
        var busy=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int completed=0;
        var drain=Task.Run(()=>Tab5StatusPublisher.RunAsync(()=>{busy.SetResult();if(!finish.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();completed++;},draining.Token));
        try {
            await busy.Task.WaitAsync(TimeSpan.FromSeconds(5));draining.Cancel();
            Check(!drain.IsCompleted,"Cancellation detached an active publication");finish.Set();
            try{await drain.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException) when(draining.IsCancellationRequested){}
            Check(completed==1,"Exit lost an in-flight status publication");
        }finally{finish.Set();draining.Cancel();try{await drain;}catch(OperationCanceledException){}}
        Console.WriteLine("TAB5_STATUS_WORKER_OK sequential, coalesced ticks, fresh capture, cancellation drain and visible faults; synthetic only");
    }
}
