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
            bytes[12]=9;File.WriteAllBytes(path,bytes);
            try{Tab5OtaPackage.Load(path);throw new Exception("Wrong chip accepted");}catch(IOException){}
            bytes=TestFirmwareImage.Create("0.2.53-test");bytes[500]^=1;File.WriteAllBytes(path,bytes);
            try{Tab5OtaPackage.Load(path);throw new Exception("Corrupt image accepted");}catch(IOException){}
            Console.WriteLine("UPGRADE_OK chip identity, image checksum, both OTA slots, hash mismatch, pending boot rejection");
        }finally{File.Delete(path);if(File.Exists(path+".notes.json"))File.Delete(path+".notes.json");}
    }
}
