using System.Text.Json;

namespace AIBotBridge;
internal static class UpgradeSelfTest
{
    internal static void Run() {
        string path=Path.Combine(Path.GetTempPath(),"aibot-image-"+Guid.NewGuid().ToString("N")+".bin");
        try {
            byte[] bytes=TestFirmwareImage.Create("0.2.53-test");File.WriteAllBytes(path,bytes);var image=Tab5OtaPackage.Load(path);
            foreach(string notes in new[]{"第一行\n第二行",@"第一行\n第二行",@"第一行\r\n第二行"}) {
                File.WriteAllText(path+".notes.json",JsonSerializer.Serialize(new{version=image.Version,sha256=image.Sha256,notes}));
                if(Tab5OtaPackage.Load(path).Notes!="第一行\n第二行")throw new Exception("Release notes line break lost");
            }
            File.Delete(path+".notes.json");
            void CheckDiagnostic(string partition,int address,int state,string sha,bool valid) {
                var root=JsonSerializer.SerializeToElement(new{type="tab5_ota_diagnostic",firmware=image.Version,partition,address,state,stateError=0,elfSha256=sha});
                try{Tab5Service.VerifyUpgradeDiagnostic(root,image);if(!valid)throw new Exception("Invalid boot accepted");}catch(IOException) when(!valid){}
            }
            CheckDiagnostic("ota_0",0x20000,2,image.ElfSha256,true);CheckDiagnostic("ota_1",0x700000,2,image.ElfSha256,true);
            CheckDiagnostic("ota_1",0x700000,1,image.ElfSha256,false);CheckDiagnostic("ota_0",0x20000,2,new string('0',64),false);
            var trace=JsonSerializer.SerializeToElement(new{trace=new{previousStage=8,previousOffset=123,previousTotal=123,previousError=0,previousStackFree=4096,previousOtaMs=new[]{1000,800,700},abortDetail="private fixture text"}});
            string timing=Tab5Service.FormatUpgradeTiming(trace);
            if(!timing.Contains("previousOtaMs=[1000,800,700]")||timing.Contains("private fixture"))throw new Exception("OTA timing missing or non-numeric trace leaked");
            using(var badTiming=JsonDocument.Parse("{\"trace\":{\"previousStage\":\"bad\",\"previousOtaMs\":[1,-1,2]}}"))
                if(Tab5Service.FormatUpgradeTiming(badTiming.RootElement)!="timing=unavailable")throw new Exception("Invalid OTA timing accepted");
            var phaseTrace=JsonSerializer.SerializeToElement(new{trace=new{previousOtaPhases=new{transport=1,prepareMs=400,writeMs=600,freeWaitMs=50,readyWaitMs=200,verifyMs=30,installMs=1500,privateText="must not leak"}}});
            timing=Tab5Service.FormatUpgradeTiming(phaseTrace);
            if(!timing.Contains("previousOtaPhases={transport=1,prepareMs=400,writeMs=600,freeWaitMs=50,readyWaitMs=200,verifyMs=30,installMs=1500}")||timing.Contains("leak"))throw new Exception("OTA phases lost or unexpected data leaked");
            foreach(string invalid in new[]{"{\"transport\":9}","{\"transport\":1,\"prepareMs\":-1}","{\"transport\":\"wifi\"}","null"}) {
                using var invalidTrace=JsonDocument.Parse("{\"trace\":{\"previousOtaPhases\":"+invalid+"}}");
                if(Tab5Service.FormatUpgradeTiming(invalidTrace.RootElement)!="timing=unavailable")throw new Exception("Invalid OTA phases accepted");
            }
            var flash=JsonSerializer.SerializeToElement(new {trace=new {},flashBenchmark=new {state=2,sampleBytes=131072,partitionAddress=0x700000,offset=0x6c0000,jedecId=0x164020,pageBytes=256,error=0,restored=1,modified=1,
                rows=new[]{8192,16384,49152,65536}.Select(chunk=>new{chunkBytes=chunk,completed=3,skipped=0,eraseUs=new[]{100,101,99},writeUs=new[]{200,201,199},privateText="must not leak"})}});
            string formatted=Tab5Service.FormatUpgradeTiming(flash);
            if(!formatted.Contains("flashBenchmark={state=2")||!formatted.Contains("chunkBytes=49152")||!formatted.Contains("writeUs=[200,201,199]")||formatted.Contains("leak"))throw new Exception("Flash timings lost or unexpected data leaked");
            using(var scanned=JsonDocument.Parse(flash.GetRawText().Replace("\"state\":2","\"state\":3,\"phase\":1,\"scanned\":4")))
                if(!Tab5Service.FormatFlashBenchmark(scanned.RootElement).Contains("phase=1,scanned=4"))throw new Exception("Flash scan diagnostics lost");
            foreach(string invalid in new[]{"null","{}","{\"state\":99}",flash.GetProperty("flashBenchmark").GetRawText().Replace("\"writeUs\":[200,201,199]","\"writeUs\":[200,-1,199]")}) {
                using var invalidFlash=JsonDocument.Parse("{\"flashBenchmark\":"+invalid+"}");
                if(Tab5Service.FormatFlashBenchmark(invalidFlash.RootElement).Length!=0)throw new Exception("Invalid flash result accepted");
            }
            void CheckWire(string type,int padding,bool accept) {
                byte[] wire=System.Text.Encoding.UTF8.GetBytes(Tab5Protocol.Prefix+JsonSerializer.Serialize(new{type,trace=new{},padding=new string('x',padding)})+"\n");int at=0;
                try {
                    using var reply=Tab5UsbText.ReadBlocking((buffer,_)=>{int count=Math.Min(buffer.Length,wire.Length-at);Array.Copy(wire,at,buffer,0,count);at+=count;return count;},type,Environment.TickCount64+1000,CancellationToken.None);
                    if(!accept)throw new Exception("Overlong diagnostic accepted");
                }catch(IOException) when(!accept){}
            }
            CheckWire("tab5_crash_diagnostic",5400,true);CheckWire("tab5_crash_diagnostic",6200,false);CheckWire("tab5_ack",5400,false);
            bytes[12]=9;File.WriteAllBytes(path,bytes);
            try{Tab5OtaPackage.Load(path);throw new Exception("Wrong chip accepted");}catch(IOException){}
            bytes=TestFirmwareImage.Create("0.2.53-test");bytes[500]^=1;File.WriteAllBytes(path,bytes);
            try{Tab5OtaPackage.Load(path);throw new Exception("Corrupt image accepted");}catch(IOException){}
            Console.WriteLine("UPGRADE_OK chip identity, image checksum, both OTA slots, hash mismatch, pending boot rejection; numeric flash result and bounded diagnostic framing");
        }finally{File.Delete(path);if(File.Exists(path+".notes.json"))File.Delete(path+".notes.json");}
    }
}
