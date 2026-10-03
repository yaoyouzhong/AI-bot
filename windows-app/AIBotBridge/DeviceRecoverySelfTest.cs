using System.Collections.Concurrent;
using System.IO.Compression;

namespace AIBotBridge;
internal static class DeviceRecoverySelfTest
{
    internal static async Task RunAsync() {
        AppPaths.BeginPublicSelfTest();
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
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
            await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(ReferenceEquals(original,manager.Tab5)&&starts["esp-usb"]==2&&starts["tab5-usb"]==1&&starts["lan"]==1,"Reconnect restarted unrelated services");
            original!.OtaTransferActive(true);
            try{await manager.ReconnectAsync(registry,tab.Id);throw new Exception("Busy reconnect accepted");}catch(InvalidOperationException){}finally{original.OtaTransferActive(false);}
            await manager.ReconnectAsync(registry,tab.Id);await Task.Delay(80);
            Check(!ReferenceEquals(original,manager.Tab5)&&starts["tab5-usb"]==2&&starts["lan"]==1,"TAB5 was not independently recreated");
            fail=true;await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(manager.View(esp).Status=="连接异常","Worker failure not visible");
            fail=false;await manager.ReconnectAsync(registry,esp.Id);await Task.Delay(80);
            Check(manager.View(esp).Status!="连接异常","Faulted worker could not be recovered");
            Check(pair.Current.Key==new Tab5PairingStore().Current!.Key,"Reconnect altered credentials");
        }finally{await manager.StopAsync();}
        Console.WriteLine("DEVICE_RECOVERY_OK timestamps, counters, private export, isolated restart, busy protection, fault recovery");
    }
}
