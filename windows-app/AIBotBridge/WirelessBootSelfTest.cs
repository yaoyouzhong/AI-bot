using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    internal static async Task WirelessBootSelfTestAsync() {
        if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
        int checks=0;
        void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        Check(!SupportsWirelessBoot("0.2.225-ui")&&SupportsWirelessBoot("0.2.226-ui"),"Wireless capability version boundary");
        string folder=Path.Combine(Path.GetTempPath(),"tab5-wireless-boot-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            string path=Path.Combine(folder,"test.bin");File.WriteAllBytes(path,TestFirmwareImage.Create("0.2.226-ui"));var image=Tab5OtaPackage.Load(path);
            foreach(string transport in new[]{"Wi-Fi","BLE"}) {
                var store=new Tab5PairingStore(Path.Combine(folder,transport));var pair=store.Pair("001122aabbcc","USB\\FIXTURE");
                using var service=new Tab5Service(store);service.UsbDevices=()=>[];
                service.Publish(new(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null)));
                service.ObserveFirmware(pair.DeviceId,image.Version);service.OfferOta(image);
                if(transport=="Wi-Fi")service._wifiRequestAt=Environment.TickCount64;else service._bleAckAt=Environment.TickCount64;
                JsonElement Report(string id,long uptime,string hash,int state=2)=>JsonSerializer.SerializeToElement(new {
                    kind="boot-verify",requestId=id,session=service._session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    hello=new{type="tab5_hello",version=1,deviceId=pair.DeviceId,firmware=image.Version,uptimeMs=uptime,uiAgeMs=50,flushCount=20,flushErrors=0},
                    diagnostic=new{type="tab5_ota_diagnostic",firmware=image.Version,elfSha256=hash,partition="ota_1",address=0x700000,state,stateError=0}});
                async Task<(int Status,JsonElement Body)> Rpc(JsonElement root,bool validProof=true) {
                    string nonce=Guid.NewGuid().ToString("N");byte[] key=Convert.FromBase64String(pair.Key),packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(root));
                    string digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packet)).ToLowerInvariant();
                    var result=await service.RpcAsync(pair.DeviceId,nonce,validProof?Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{digest}"):new string('0',64),packet,CancellationToken.None,transport:transport);
                    if(result.Packet is null)return(result.Status,default);
                    using var reply=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,result.Packet));return(reply.RootElement.GetProperty("status").GetInt32(),reply.RootElement.GetProperty("body").Clone());
                }
                Check((await Rpc(Report(new string('a',32),100,image.ElfSha256))).Status==409,"Unsolicited report rejected");
                using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                Task verify=service.VerifyWirelessBootAsync(image,pair.DeviceId,cancel.Token);string previousId="";
                for(int i=0;i<3;i++) {
                    BootProbe? probe;
                    while((probe=service._bootProbe) is null||probe.Id==previousId)await Task.Delay(10,cancel.Token);
                    previousId=probe.Id;
                    Check(service.Busy,"Verification owns lifecycle gate");
                    Check(probe.Transport==transport,"Transport stays fixed");
                    Check(service.ReceiveBootProbe(Report(probe.Id,100+i,image.ElfSha256),"ffeeddccbbaa",transport).Status==409,"Wrong paired device rejected");
                    Check(service.ReceiveBootProbe(Report(probe.Id,100+i,image.ElfSha256),pair.DeviceId,transport=="BLE"?"Wi-Fi":"BLE").Status==409,"Cross-transport report rejected");
                    Check((await Rpc(Report(new string('f',32),100+i,image.ElfSha256))).Status==409,"Wrong challenge rejected");
                    Check((await Rpc(Report(probe.Id,100+i,image.ElfSha256),false)).Status!=200,"Forged authentication rejected");
                    Check((await Rpc(Report(probe.Id,100+i,image.ElfSha256))).Status==200,"Authenticated live sample accepted");
                    Check((await Rpc(Report(probe.Id,100+i,image.ElfSha256))).Status==409,"Consumed request cannot be replayed with fresh envelope");
                }
                await verify;Check(service._verifiedBoot?.ElfSha256==image.ElfSha256,"Three live samples establish exact boot");
                Check(!service.Busy&&service._bootProbe is null,"Gate and command cleared on completion");
                foreach(var invalid in new[]{Report("a",100,new string('0',64)),Report("a",100,image.ElfSha256,1),Report("a",99,image.ElfSha256)}) {
                    bool rejected=false;try{ValidateWirelessBootReport(invalid,image,pair.DeviceId,99);}catch(IOException){rejected=true;}Check(rejected,"Wrong hash, pending state or nonadvancing uptime rejected");
                }
                using var stop=new CancellationTokenSource();var pending=service.VerifyWirelessBootAsync(image,pair.DeviceId,stop.Token);stop.Cancel();
                try{await pending;throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
                Check(service._verifiedBoot is null&&!service.Busy&&service._bootProbe is null,"Cancellation clears verification, gate and request");
            }
            Console.WriteLine($"WIRELESS_BOOT_OK checks={checks}; authenticated Wi-Fi/BLE 3-sample lifecycle, identity/transport/challenge/auth/replay rejection, hash/state/uptime checks and cancellation");
        }finally{Directory.Delete(folder,true);}
    }
}
