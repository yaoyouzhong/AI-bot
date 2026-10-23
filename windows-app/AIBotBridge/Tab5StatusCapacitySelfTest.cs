using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5StatusCapacitySelfTest
{
    internal static async Task RunAsync(string? vectors)
    {
        void Check(bool value,string name){if(!value)throw new InvalidOperationException("TAB5 status capacity: "+name);}
        byte[] Expand(byte[] wire) {
            using var doc=JsonDocument.Parse(wire);
            if(doc.RootElement.GetProperty("type").GetString()!="tab5_packed")return wire;
            using var input=new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("payload").GetString()!));
            using var zip=new ZLibStream(input,CompressionMode.Decompress);using var result=new MemoryStream();zip.CopyTo(result);return result.ToArray();
        }
        var directory=Path.Combine(Path.GetTempPath(),"tab5-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var store=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=store.Pair("001122334455",@"USB\CAPACITY_TEST");
            byte[] key=Convert.FromBase64String(pair.Key);string nonce=Guid.NewGuid().ToString("N");
            JsonElement Info(string firmware,string? proof=null)=>JsonSerializer.SerializeToElement(new{firmware,firmwareProof=proof??Tab5Protocol.Proof(key,"FIRMWARE|"+nonce+"|"+firmware)});
            foreach(string? unknown in new string?[]{null,"","bad","0.2.197-ui","0.2.198","999.1.0-unknown"})
                Check(Tab5StatusCapacity.ForFirmware(unknown)==32768,"unknown/old firmware remains 32 KiB");
            Check(Tab5StatusCapacity.FromSignedFirmware(Info("0.2.198-ui"),pair,nonce)==65536,"signed BLE firmware enables 64 KiB");
            Check(Tab5StatusCapacity.FromSignedFirmware(Info("0.2.198-ui",new string('0',64)),pair,nonce)==32768,"forged BLE capacity rejected");
            Check(Tab5StatusCapacity.FromSignedFirmware(Info("0.2.198-ui"),pair,new string('a',32))==32768,"old-challenge capacity rejected");
            var hello=JsonSerializer.SerializeToElement(new{version=1,type="tab5_hello",deviceId=pair.DeviceId,firmware="0.2.198-ui"});
            Check(Tab5StatusCapacity.FromUsbHello(hello,pair.DeviceId)==65536&&Tab5StatusCapacity.FromUsbHello(hello,"wrong")==32768,"USB hello identity bound");
            using(var malformed=JsonDocument.Parse("{\"version\":\"1\",\"type\":7}"))
                Check(Tab5StatusCapacity.FromUsbHello(malformed.RootElement,pair.DeviceId)==32768,"malformed USB hello safely stays legacy");
            using var tasks=new Tab5CodexTasks((_,_,_)=>Task.FromResult(default(JsonElement)),()=>[]);
            using var service=new Tab5Service(store,tasks);
            var now=DateTimeOffset.UtcNow;
            var health=new RuntimeHealthSnapshot("capacity",1,now,[new("vpn","网络","error","测试","", "检查连接",now)],[]);
            var normal=new StatusSnapshot(1,"12:00:00",now.ToUnixTimeSeconds(),28800,now,new("idle",0),new("idle",0),RuntimeHealth:health);
            service.Publish(normal);
            var large=normal with {RuntimeHealth=health with {Checks=[health.Checks[0] with {Detail=new string('x',35000)}]}};
            service.Publish(large);Check(service.CurrentFrame!.Length+28<=32768,"old receiver keeps 32 KiB publication");
            service.ObserveFirmware("wrong","0.2.198-ui");service.Publish(large);
            Check(service.CurrentFrame!.Length+28<=32768,"another device cannot enable expanded state");
            service.ObserveFirmware(pair.DeviceId,"0.2.198-ui");
            byte[] full=service.CurrentFrame!;
            Check(full.Length>32740&&full.Length+28<=49152,"new firmware keeps complete state within 48 KiB target");
            using(var state=JsonDocument.Parse(full))Check(state.RootElement.GetProperty("data").GetProperty("runtimeHealth").GetProperty("checks")[0].GetProperty("detail").GetString()!.Length==35000,"large detail preserved");
            for(int channel=0;channel<3;channel++) {
                service.ResetTelemetryChannel(channel);
                byte[] wire=service.TelemetryFrame(channel,true,maximum:65536)!;
                Check(Expand(wire).SequenceEqual(full),"expanded decompression matches USB/Wi-Fi/BLE full state");
                service.ResetTelemetryChannel(channel);
                byte[] old=Expand(service.TelemetryFrame(channel,true,maximum:32768)!);
                Check(old.Length+28<=32768,"old receiver never gets oversized expanded JSON");
            }
            byte[] Wifi(string firmware,bool signed) {
                service.ResetTelemetryChannel(1);
                return service.Respond(pair.DeviceId,nonce,Tab5Protocol.Proof(key,"GET|"+pair.DeviceId+"|"+nonce),firmware:firmware,
                    firmwareProof:signed?Tab5Protocol.Proof(key,"FIRMWARE|"+nonce+"|"+firmware):new string('0',64))!;
            }
            Check(Expand(Tab5Protocol.Decrypt(key,nonce,Wifi("0.2.198-ui",true),65536)).Length>32740,"authenticated Wi-Fi expansion");
            Check(Expand(Tab5Protocol.Decrypt(key,nonce,Wifi("0.2.198-ui",false))).Length<=32740,"forged Wi-Fi version gets legacy view");
            Check(Expand(Tab5Protocol.Decrypt(key,nonce,Wifi("0.2.197-ui",true))).Length<=32740,"downgrade returns legacy state without restart");
            service.ObserveFirmware(pair.DeviceId,"0.2.198-ui");
            var excessive=large with {RuntimeHealth=health with {Checks=[health.Checks[0] with {Detail=new string('x',100000)}]}};
            service.Publish(excessive);Check(service.CurrentFrame!.Length+28<=65536&&service.Summary.Contains("degraded=True"),"64 KiB overflow self-recovers");
            service.Publish(large);Check(service.CurrentFrame!.Length>32740&&service.Summary.Contains("degraded=False"),"64 KiB full state resumes automatically");
            byte[] Exact(int length,long sequence) {
                string head=$"{{\"version\":1,\"type\":\"tab5_status\",\"deviceId\":\"{pair.DeviceId}\",\"session\":\"status64\",\"sequence\":{sequence},\"data\":{{\"codex\":{{}},\"claude\":{{}},\"padding\":\"";
                const string tail="\"}}";return Encoding.UTF8.GetBytes(head+new string('x',length-Encoding.UTF8.GetByteCount(head+tail))+tail);
            }
            byte[] boundary=Exact(65508,1000),packet=Tab5Protocol.Encrypt(key,nonce,boundary,65536);
            Check(packet.Length==65536&&Tab5Protocol.Decrypt(key,nonce,packet,65536).SequenceEqual(boundary),"exact 64 KiB authenticated packet boundary");
            bool rejected=false;try{Tab5Protocol.Encrypt(key,nonce,Exact(65509,1001),65536);}catch(ArgumentException){rejected=true;}
            Check(rejected,"one byte above 64 KiB rejected");
            rejected=false;try{Tab5Protocol.Encrypt(key,nonce,boundary);}catch(ArgumentException){rejected=true;}
            Check(rejected,"legacy encryption bound unchanged");
            foreach(int chunk in new[]{20,244,488}) {
                using var assembled=new MemoryStream();bool ack=false;
                await Tab5BleTransfer.SendAsync(Tab5Protocol.WithLength(packet),chunk,false,(part,ct)=>{assembled.Write(part.Span);return Task.CompletedTask;},ct=>{ack=true;return Task.FromResult(true);},CancellationToken.None,maximum:65536);
                byte[] received=assembled.ToArray();Check(ack&&BinaryPrimitives.ReadUInt32LittleEndian(received)==65536&&received.AsSpan(4).SequenceEqual(packet),"64 KiB BLE uint32 reassembly at negotiated MTUs");
            }
            if(vectors is not null) {
                Directory.CreateDirectory(vectors);File.WriteAllBytes(Path.Combine(vectors,"status64-full.json"),boundary);
                File.WriteAllBytes(Path.Combine(vectors,"status64-packed.json"),Tab5Service.PackTelemetry(boundary,CompressionLevel.Optimal));
                File.WriteAllBytes(Path.Combine(vectors,"status64-over.json"),Exact(65509,1001));
                File.WriteAllBytes(Path.Combine(vectors,"status48-full.json"),Exact(49124,999));
            }
            Console.WriteLine("TAB5_STATUS_64K_OK signed Wi-Fi/BLE firmware, USB identity, 48KiB target, legacy decompression view, downgrade, self recovery, exact 65536/65537 encrypted bounds, chunks20/244/488");
        }finally{Directory.Delete(directory,true);}
    }
}
