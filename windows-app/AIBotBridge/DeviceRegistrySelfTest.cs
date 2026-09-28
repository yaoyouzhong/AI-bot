using System.Text.Json;
namespace AIBotBridge;
internal static class DeviceRegistrySelfTest
{
    internal static void Run() {
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        string folder=Path.Combine(Path.GetTempPath(),"aibot-devices-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            string path=Path.Combine(folder,"devices.json");var store=new DeviceRegistryStore(path);
            store.Migrate(null,false,false);Check(store.Snapshot.Devices.Length==0,"Empty profile fabricated hardware");
            store.Migrate("001122334455",true,true);Check(store.Snapshot.Devices.Length==0,"Migration repeated");
            var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"书桌","001122334455");store.Add(tab);
            Check(new DeviceRegistryStore(path).Snapshot.Devices.Single().Id==tab.Id,"Registry did not persist");
            try{store.Add(tab with {Id=Guid.NewGuid().ToString("N")});throw new Exception("Duplicate model allowed");}catch(InvalidOperationException){}
            store.Update(tab with {Enabled=false});Check(!DeviceCapabilities.Allows(store.Snapshot.Devices[0],"voice"),"Disabled device action accepted");
            store.Remove(tab.Id);new DeviceRegistryStore(path).Migrate(tab.HardwareId,true,true);
            Check(new DeviceRegistryStore(path).Snapshot.Devices.Length==0,"Removed device resurrected");
            var migrated=new DeviceRegistryStore(Path.Combine(folder,"migrated.json"));migrated.Migrate(tab.HardwareId,true,true);
            Check(migrated.Snapshot.LegacyDecisionPending&&migrated.Snapshot.Devices.Length==1,"Legacy evidence fabricated ESP device");
            var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"原小屏",null);migrated.Add(esp);
            Check(migrated.Snapshot.Devices.Length==2&&!migrated.Snapshot.LegacyDecisionPending,"Legacy confirmation failed");
            Check(!DeviceCapabilities.Allows(esp,"voice")&&!DeviceCapabilities.Allows(tab,"mirror"),"Cross-model actions exposed");
            try{DeviceCapabilities.Require(migrated.Snapshot,esp.Id,"reset",false);throw new Exception("Offline reset allowed");}catch(InvalidOperationException){}
            string valid=File.ReadAllText(path);File.WriteAllText(path,"{broken");
            try{_=new DeviceRegistryStore(path);throw new Exception("Corrupt registry accepted");}catch(InvalidDataException){}
            Check(File.ReadAllText(path)=="{broken","Corrupt data overwritten");
            File.WriteAllText(path,JsonSerializer.Serialize(new DeviceRegistry(99,true,false,false,[])));
            try{_=new DeviceRegistryStore(path);throw new Exception("Future schema accepted");}catch(InvalidDataException){}
            Check(File.Exists(path+".bak"),"Previous registry backup missing");
            Console.WriteLine("DEVICE_REGISTRY_OK empty/migration/identity/duplicate/disabled/removed/corrupt/future/offline/bak");
        }finally{Directory.Delete(folder,true);}
    }
}
