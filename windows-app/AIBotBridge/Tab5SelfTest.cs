using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5SelfTest
{
    private sealed class RemovedUsbPort(Exception? error=null) : IDisposable {
        internal int Disposals;
        public void Dispose(){Disposals++;if(error is not null)throw error;}
    }
    internal static async Task RunAsync()
    {
        Tab5DiscoveryProtocol.SelfTest();
        Tab5Calendar.SelfTest();
        WeatherForecastSelfTest.Run();
        await Tab5OtaFlowSelfTest.RunAsync();
        QuotaHistorySelfTest.Run();
        await QuotaHistorySelfTest.CollectionAsync();
        await Tab5CodexSelfTest.RunAsync();
        await Tab5ImageSelfTest.RunAsync();
        void Check(bool pass,string name) {if(!pass)throw new InvalidOperationException("TAB5 test failed: "+name);}
        var removed=new RemovedUsbPort(new IOException("device removed"));
        Check(!Tab5Service.DisposeUsbPort(removed)&&removed.Disposals==1,"unplug cleanup does not kill reconnect worker");
        var reconnected=new RemovedUsbPort();
        Check(Tab5Service.DisposeUsbPort(reconnected)&&reconnected.Disposals==1,"replacement port can close normally");
        bool unexpectedPropagated=false;
        try{Tab5Service.DisposeUsbPort(new RemovedUsbPort(new InvalidOperationException("unexpected")));}
        catch(InvalidOperationException){unexpectedPropagated=true;}
        Check(unexpectedPropagated,"unrelated cleanup faults are not hidden");
        using(var idle=JsonDocument.Parse("{\"status\":{\"type\":\"notLoaded\"},\"turns\":[{\"status\":\"completed\",\"completedAt\":1}]}"))
            Check(Tab5CodexTasks.CanContinue(idle.RootElement),"completed Codex task can continue");
        using(var active=JsonDocument.Parse("{\"status\":{\"type\":\"notLoaded\"},\"turns\":[{\"status\":\"interrupted\",\"completedAt\":null}]}"))
            Check(!Tab5CodexTasks.CanContinue(active.RootElement),"incomplete desktop task is not eligible for continuation");
        using(var active=JsonDocument.Parse("{\"status\":{\"type\":\"active\"},\"turns\":[{\"status\":\"completed\",\"completedAt\":1}]}"))
            Check(!Tab5CodexTasks.CanContinue(active.RootElement),"active app-server task cannot be resumed concurrently");
        var directory=Path.Combine(Path.GetTempPath(),"aibot-tab5-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var path=Path.Combine(directory,"pairing.dat");var store=new Tab5PairingStore(path);
            var pair=store.Pair("001122334455",@"USB\VID_303A&PID_1001\TEST");
            Check(new Tab5PairingStore(path).Current==pair,"DPAPI round trip");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(pair.Key),"pairing never stored in clear");
            var key=Convert.FromBase64String(pair.Key);var nonce=new string('1',32);var clear=Encoding.UTF8.GetBytes("中文状态 UTF-8");
            var encrypted=Tab5Protocol.Encrypt(key,nonce,clear);
            Check(Tab5Protocol.Decrypt(key,nonce,encrypted).SequenceEqual(clear),"encrypted UTF-8 round trip");
            encrypted[^1]^=1;bool rejected=false;try{Tab5Protocol.Decrypt(key,nonce,encrypted);}catch(CryptographicException){rejected=true;}
            Check(rejected,"tampered packet rejected");
            encrypted=Tab5Protocol.Encrypt(key,nonce,clear);rejected=false;
            try{Tab5Protocol.Decrypt(key,new string('2',32),encrypted);}catch(CryptographicException){rejected=true;}
            Check(rejected,"wrong connection challenge rejected");
            Check(!Tab5Protocol.Verify(key,"hello",new string('0',64)),"wrong authentication proof rejected");
            var snapshot=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("working",0),new("offline",null));
            var now = new DateTimeOffset(2026,9,23,12,0,0,TimeSpan.FromHours(8));
            var codex = new ProviderQuotaSnapshot("codex","pro",null,null,92,now.AddDays(3),3,
                [now.AddDays(10).ToUnixTimeSeconds(),now.AddDays(11).ToUnixTimeSeconds(),now.AddDays(20).ToUnixTimeSeconds()],now,false);
            var mapped = Tab5Details.Quotas(snapshot with {Quotas=new(null,codex)})[1];
            Check(mapped.Plan=="PRO" && mapped.Primary is null && mapped.Weekly==92 && mapped.PlanPercent is null,
                "Codex only exposes real limits and normalized membership");
            Check(mapped.CreditExpiries.Length==3 && mapped.CreditExpiries[0]=="2026-10-03 12:00", "every credit expiry preserved in display timezone");
            var oldPro=codex with {PrimaryPercent=42,PrimaryResetsAt=now.AddHours(2)};
            Check(Tab5Details.Quotas(snapshot with {Quotas=new(null,oldPro)})[1].Primary is null,
                "PRO cached five-hour limit suppressed");
            var plus=Tab5Details.Quotas(snapshot with {Quotas=new(null,oldPro with {Plan=" plus "})})[1];
            Check(plus.Primary==42 && plus.PrimaryReset!="--","PLUS five-hour limit retained");
            var payg = new DomesticProviderQuotaSnapshot("zhipu","API PAYG",null,null,null,null,96.72,3.27,"CNY",now,false);
            var apiMapped = Tab5Details.Quotas(snapshot with {DomesticQuotas=new(null,null,null,payg with {Provider="deepseek",Plan=null,UsedCost=null},payg,null,null,null)});
            Check(apiMapped[5].BillingMode=="payg" && apiMapped[5].Cost is null && apiMapped[6].Balance==96.72 && apiMapped[6].Cost==3.27,
                "PAYG balance and cumulative spend preserved; unavailable spend stays unknown");
            Check(Tab5Details.BillingMode("zhipu",payg with {Plan="Coding Plan",PrimaryPercent=0})=="subscription",
                "Coding Plan quotas take precedence over ancillary monetary fields");
            var coding = payg with {Provider="alibaba",Plan="Coding Plan",PrimaryPercent=25,Balance=null,PlanExpiresAt=now.AddDays(30)};
            var codingDetails=Tab5Details.Quotas(snapshot with {DomesticQuotas=new(coding,null,null,null,null,null,null,null)})[2];
            Check(codingDetails.BillingMode=="subscription" && codingDetails.PlanExpiry!="--" &&
                  Tab5Details.Quotas(snapshot with {DomesticQuotas=new(coding with {PlanExpiresAt=null},null,null,null,null,null,null,null)})[2].PlanExpiry=="--",
                "Coding Plan expiry requires an explicit source date");
            Check(Tab5Details.Quotas(snapshot with {DomesticQuotas=new(null,null,null,null,payg with {Balance=0,UsedCost=0},null,null,null)})[6].Cost==0,
                "Zero monetary use is not missing data");
            var history=Tab5QuotaTrend.Build([
                new(now.AddMinutes(-4),"PRO",80,now.AddDays(3),null,null,"test"),
                new(now.AddMinutes(-2),"PRO",82,now.AddDays(3),null,null,"test"),
                new(now,"PRO",85,now.AddDays(3),null,null,"test")],now);
            Check(history.Days.Length==7 && history.Days[0].Used is null && history.Days[^1].Used==5 && history.Days[^1].Partial,
                "daily observed growth, missing dates and partial current day");
            var pixels=new byte[120*120*8*2];Random.Shared.NextBytes(pixels);
            var asset=Tab5Assets.Create(0,120,120,[20,30,40,50,60,70,80,90],pixels);
            var unpacked=new List<byte>();
            for(int at=0;at<asset.Packed.Length;at+=4) {
                int count=asset.Packed[at]|asset.Packed[at+1]<<8;
                for(int n=0;n<count;n++){unpacked.Add(asset.Packed[at+2]);unpacked.Add(asset.Packed[at+3]);}
            }
            Check(unpacked.SequenceEqual(pixels),"maximum animation RLE round trip");
            Check(asset.Packed.Length<=524288,"device resource capacity");
            var resource=new {slot=0,asset.Id,asset.Width,asset.Height,asset.Frames,asset.Delays,total=asset.Packed.Length,offset=0,bytes=Convert.ToBase64String(asset.Packed,0,1024)};
            var quota=new DomesticProviderQuotaSnapshot("test","测试套餐",52,DateTimeOffset.UtcNow,70,DateTimeOffset.UtcNow,12,3,"CNY",DateTimeOffset.UtcNow,true){PlanPercent=40,PlanResetsAt=DateTimeOffset.UtcNow};
            var full=snapshot with {
                Weather=new("测试城市","多云",20,25,15,80,1,null,null,"qweather",now,false) {
                    Hourly=Enumerable.Range(0,24).Select(i=>new WeatherHour(now.AddHours(i).ToString("O"),"雷阵雨伴有大风",-10+i,80)).ToArray(),
                    Daily=Enumerable.Range(0,7).Select(i=>new WeatherDay(now.AddDays(i).ToString("yyyy-MM-dd"),"雷阵雨伴有大风",-10,30,null,12.5)).ToArray()},
                Quotas=new(null,codex),
                Stocks=new(Enumerable.Range(0,20).Select(i=>new StockQuote("sh600000","600000","测试股票名称", "12345.678","+12.34%",1)).ToArray(),DateTimeOffset.UtcNow,false),
                DomesticQuotas=new(quota,quota,quota,quota,quota,quota,quota,quota),
                SystemMetrics=new(30,40,100,200,DateTimeOffset.UtcNow){SampleSequence=16,SampleSession="test",Samples=Enumerable.Range(0,16).Select(_=>new NetworkSample(9999999,8888888)).ToArray()}
            };
            var fullFrame=Tab5Protocol.Snapshot(full,pair.DeviceId,"test",1,resource,[asset.Id,null,null]);
            Check(fullFrame.Length+28<Tab5Protocol.MaximumFrame,"expanded status plus artwork fits all transports");
            var calendarFrame=System.Text.Json.Nodes.JsonNode.Parse(fullFrame)!;
            calendarFrame["data"]!["calendar"]!["detail"]!["birthdays"]=JsonSerializer.SerializeToNode(
                Enumerable.Range(0,32).Select(i=>new {name=new string('生',16),month=i%12+1,day=1,lunar=true,leap=false,remind=30}),JsonDefaults.Options);
            Check(Encoding.UTF8.GetByteCount(calendarFrame.ToJsonString(JsonDefaults.Options))+28<Tab5Protocol.MaximumFrame,"32 birthdays plus expanded status fits all transports");
            using(var doc=JsonDocument.Parse(fullFrame)) {
                var data=doc.RootElement.GetProperty("data");
                Check(data.GetProperty("pcInput").GetProperty("session").GetString()=="test", "PC input session reaches all transports");
                Check(data.GetProperty("pcInput").GetProperty("sequence").GetInt64()==1, "PC input ordering reaches the wire");
                Check(SystemIdleTime.ExpandInputTick(0x100000010L,0xfffffff0)==0xfffffff0L, "last input survives 32 bit tick wrap");
                Check(SystemIdleTime.ExpandInputTick(1200,1000)==SystemIdleTime.ExpandInputTick(1500,1000), "idle heartbeats do not change last input");
                Check(data.GetProperty("quotaDetails").GetArrayLength()==10,"all ten quota providers mapped");
                Check(data.GetProperty("stocks").GetProperty("quotes").GetArrayLength()==20,"all twenty stocks retained");
                Check(data.GetProperty("quotaDetails")[2].GetProperty("balance").GetDouble()==12,"domestic balance mapped");
                Check(data.GetProperty("quotaDetails")[0].GetProperty("primary").ValueKind==JsonValueKind.Null,"missing provider not converted to zero");
                Check(data.GetProperty("quotaDetails")[1].GetProperty("creditExpiries").GetArrayLength()==3,"all credit dates reach the wire");
                Check(data.GetProperty("quotaTrend").GetProperty("days").GetArrayLength()==7,"daily chart reaches the wire");
            }
            Console.WriteLine("TAB5_EXPANDED_FRAME_BYTES="+fullFrame.Length);
            var service=new Tab5Service(store);service.Publish(snapshot);
            using(var doc=JsonDocument.Parse(service.CurrentFrame!)) {
                var data=doc.RootElement.GetProperty("data");
                Check(data.GetProperty("quotas").ValueKind==JsonValueKind.Null,"unknown quota remains null");
                Check(!data.TryGetProperty("displayPolicy",out _),"old device policy excluded");
            }
            string receivedIds;
            using(var doc=JsonDocument.Parse(service.CurrentFrame!))receivedIds=string.Join(",",doc.RootElement.GetProperty("data").GetProperty("assetIds").EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.String?v.GetString():"-"));
            string requestProof=Tab5Protocol.Proof(key,"GET|"+pair.DeviceId+"|"+nonce);
            service.Respond(pair.DeviceId,nonce,requestProof,receivedIds,new string('0',64));service.Publish(snapshot);
            using(var doc=JsonDocument.Parse(service.CurrentFrame!))Check(doc.RootElement.GetProperty("data").GetProperty("resource").ValueKind==JsonValueKind.Object,"forged cache receipt cannot suppress transfer");
            service.Respond(pair.DeviceId,nonce,requestProof,receivedIds,Tab5Protocol.Proof(key,"ASSETS|"+nonce+"|"+receivedIds));service.Publish(snapshot);
            using(var doc=JsonDocument.Parse(service.CurrentFrame!))Check(doc.RootElement.GetProperty("data").GetProperty("resource").ValueKind==JsonValueKind.Null,"cached resources omitted from normal status");
            service.Respond(pair.DeviceId,nonce,requestProof,"-,-,-",Tab5Protocol.Proof(key,"ASSETS|"+nonce+"|-,-,-"));service.Publish(snapshot);
            using(var doc=JsonDocument.Parse(service.CurrentFrame!))Check(doc.RootElement.GetProperty("data").GetProperty("resource").ValueKind==JsonValueKind.Object,"missing cache resumes resource transfer");
            using(var doc=JsonDocument.Parse(service.CurrentFrame!))Check(doc.RootElement.GetProperty("data").GetProperty("codexTasks").GetProperty("tasks").ValueKind==JsonValueKind.Array,"Codex task catalog reaches device");
            Check(service.Respond(pair.DeviceId,nonce,new string('0',64)) is null,"unauthorized request rejected");
            var action=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new {session="wrong-session",taskId=Guid.NewGuid(),message="test"}));
            var actionHash=Convert.ToHexString(SHA256.HashData(action)).ToLowerInvariant();
            var actionProof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{nonce}|{actionHash}");
            Check((await service.SubmitCodexAsync(pair.DeviceId,nonce,new string('0',64),action,CancellationToken.None)).Status==401,"Codex action rejects forged proof");
            Check((await service.SubmitCodexAsync(pair.DeviceId,nonce,actionProof,action,CancellationToken.None)).Status==400,"Codex action rejects wrong session");
            action[^1]^=1;
            Check((await service.SubmitCodexAsync(pair.DeviceId,nonce,actionProof,action,CancellationToken.None)).Status==401,"Codex action rejects tampered ciphertext");
            using(var state=JsonDocument.Parse(service.CurrentFrame!)) {
                var actionNonce=new string('3',32);
                var validAction=Tab5Protocol.Encrypt(key,actionNonce,JsonSerializer.SerializeToUtf8Bytes(new {
                    session=state.RootElement.GetProperty("session").GetString(),taskId=Guid.NewGuid().ToString(),message="test",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                var validHash=Convert.ToHexString(SHA256.HashData(validAction)).ToLowerInvariant();
                var validProof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{actionNonce}|{validHash}");
                Check((await service.SubmitCodexAsync(pair.DeviceId,actionNonce,validProof,validAction,CancellationToken.None)).Status==404,"authenticated action never targets an unknown task");
                Check((await service.SubmitCodexAsync(pair.DeviceId,actionNonce,validProof,validAction,CancellationToken.None)).Status==409,"authenticated action replay is rejected");
                Check((await service.SubmitCodexAsync(pair.DeviceId,actionNonce,validProof,validAction,CancellationToken.None,readOnly:true)).Status==400,"turn packet cannot be replayed as a history read");
                using var readerService=new Tab5Service(store);readerService.Publish(snapshot);
                using var readerState=JsonDocument.Parse(readerService.CurrentFrame!);
                var readNonce=new string('6',32);
                var readPacket=Tab5Protocol.Encrypt(key,readNonce,JsonSerializer.SerializeToUtf8Bytes(new {
                    op="read",session=readerState.RootElement.GetProperty("session").GetString(),taskId=Guid.NewGuid().ToString(),message="",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                var readHash=Convert.ToHexString(SHA256.HashData(readPacket)).ToLowerInvariant();
                var readProof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{readNonce}|{readHash}");
                Check((await readerService.SubmitCodexAsync(pair.DeviceId,readNonce,readProof,readPacket,CancellationToken.None)).Status==400,"read packet cannot submit a turn");
                Check((await readerService.SubmitCodexAsync(pair.DeviceId,readNonce,new string('0',64),readPacket,CancellationToken.None,true)).Status==401,"read requires paired proof");
                Check((await readerService.SubmitCodexAsync(pair.DeviceId,readNonce,readProof,readPacket,CancellationToken.None,true)).Status==404,"authenticated read rejects unknown tasks");
                Check((await readerService.SubmitCodexAsync(pair.DeviceId,readNonce,readProof,readPacket,CancellationToken.None,true)).Status==409,"read replay is rejected");
                var sendNonce=Guid.NewGuid().ToString("N");
                var sendPacket=Tab5Protocol.Encrypt(key,sendNonce,JsonSerializer.SerializeToUtf8Bytes(new {
                    session=readerState.RootElement.GetProperty("session").GetString(),taskId=Guid.NewGuid().ToString(),message="synthetic",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                var sendHash=Convert.ToHexString(SHA256.HashData(sendPacket)).ToLowerInvariant();
                var sendProof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{sendNonce}|{sendHash}");
                Check((await readerService.SubmitCodexAsync(pair.DeviceId,sendNonce,sendProof,sendPacket,CancellationToken.None)).Status==404,"recent read must not rate-limit an explicit send; unknown task still rejected");
                using var draftService=new Tab5Service(store);draftService.Publish(snapshot);
                using var draftState=JsonDocument.Parse(draftService.CurrentFrame!);
                async Task<int> NewOperation(object operation,bool read=false) {
                    string n=Guid.NewGuid().ToString("N");
                    byte[] b=Tab5Protocol.Encrypt(key,n,JsonSerializer.SerializeToUtf8Bytes(new{
                        op=operation,session=draftState.RootElement.GetProperty("session").GetString(),taskId=Guid.NewGuid().ToString(),
                        requestId=Guid.NewGuid().ToString(),message="synthetic",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                    string hash=Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant(),proof=Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{n}|{hash}");
                    return (await draftService.SubmitCodexAsync(pair.DeviceId,n,proof,b,CancellationToken.None,read)).Status;
                }
                Check(await NewOperation(3)==400,"malformed operation must not default to send");
                Check(await NewOperation("draft-load")==400,"draft load requires read route");
                Check(await NewOperation("draft-save",true)==400,"draft save cannot use read route");
                Check(await NewOperation("draft-save")==404,"draft cannot target unknown task");
                Check(await NewOperation("send")==404,"autosave cannot rate-limit a user's send");
            }
            var otaBytes=new byte[1024];otaBytes[0]=0xE9;
            BitConverter.GetBytes(0xABCD5432u).CopyTo(otaBytes,32);
            Encoding.ASCII.GetBytes("0.2.7-test").CopyTo(otaBytes,48);
            Encoding.ASCII.GetBytes("aibot_tab5").CopyTo(otaBytes,80);
            var otaPath=Path.Combine(directory,"synthetic-tab5.bin");File.WriteAllBytes(otaPath,otaBytes);
            var largePath=Path.Combine(directory,"synthetic-large-tab5.bin");
            var largeBytes=new byte[6_500_000];otaBytes.CopyTo(largeBytes,0);File.WriteAllBytes(largePath,largeBytes);
            Check(Tab5OtaPackage.Load(largePath).Image.Length==largeBytes.Length,"OTA accepts current 6.5 MB application");
            using(var tooLarge=new FileStream(largePath,FileMode.Create))tooLarge.SetLength(Tab5OtaPackage.MaximumSize+1L);
            bool oversizeRejected=false;try{Tab5OtaPackage.Load(largePath);}catch(ArgumentException){oversizeRejected=true;}
            Check(oversizeRejected,"OTA rejects images exceeding the new slot");
            var otaSha=Convert.ToHexString(SHA256.HashData(otaBytes)).ToLowerInvariant();
            string releaseNotes="修复升级崩溃\n新增更新说明";
            File.WriteAllText(otaPath+".notes.json",JsonSerializer.Serialize(new {version="wrong",sha256=otaSha,notes=releaseNotes}));
            bool mismatchRejected=false;try{Tab5OtaPackage.Load(otaPath);}catch(ArgumentException){mismatchRejected=true;}
            Check(mismatchRejected,"OTA refuses notes for another version");
            File.WriteAllText(otaPath+".notes.json",JsonSerializer.Serialize(new {version="0.2.7-test",sha256=new string('0',64),notes=releaseNotes}));
            mismatchRejected=false;try{Tab5OtaPackage.Load(otaPath);}catch(ArgumentException){mismatchRejected=true;}
            Check(mismatchRejected,"OTA refuses notes for another image");
            File.WriteAllText(otaPath+".notes.json",JsonSerializer.Serialize(new {version="0.2.7-test",sha256=otaSha,notes=new string('中',342)}));
            bool notesTooLong=false;try{Tab5OtaPackage.Load(otaPath);}catch(ArgumentException){notesTooLong=true;}
            Check(notesTooLong,"OTA limits UTF8 notes to device buffer");
            File.WriteAllText(otaPath+".notes.json",JsonSerializer.Serialize(new {version="0.2.7-test",sha256=otaSha,notes=releaseNotes}));
            service.OfferOta(otaPath);service.Publish(snapshot);
            Check(service.OtaSummary.Contains("等待设备"),"offered image is not confirmed installed");
            service.Respond(pair.DeviceId,nonce,requestProof,firmware:"0.2.7-test",firmwareProof:new string('0',64));
            Check(service.OtaSummary.Contains("等待设备"),"forged firmware receipt ignored");
            service.Respond(pair.DeviceId,nonce,requestProof,firmware:"0.2.7-test",firmwareProof:Tab5Protocol.Proof(key,"FIRMWARE|"+nonce+"|0.2.7-test"));
            Check(service.OtaSummary.Contains("已运行此版本"),"authenticated Wi-Fi receipt removes new-version wording");
            string firstOffer;
            using(var state=JsonDocument.Parse(service.CurrentFrame!)) {
                Check(state.RootElement.GetProperty("data").GetProperty("ota").GetProperty("sha256").GetString()==otaSha,"OTA offer reaches encrypted device status");
                firstOffer=state.RootElement.GetProperty("data").GetProperty("ota").GetProperty("offerId").GetString()!;
                Check(firstOffer.Length==32,"OTA offer has one manual attempt ID");
                Check(state.RootElement.GetProperty("data").GetProperty("ota").GetProperty("notes").GetString()==releaseNotes,"verified update notes reach device status");
            }
            Check(service.OtaImage(pair.DeviceId,nonce,new string('0',64),otaSha) is null,"OTA image rejects forged proof");
            Check(service.OtaImage(pair.DeviceId,nonce,Tab5Protocol.Proof(key,$"GET|/tab5/v1/ota/{otaSha}|{pair.DeviceId}|{nonce}"),otaSha)!.SequenceEqual(otaBytes),"paired device receives exact offered image");
            using var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
            using var stop=new CancellationTokenSource();
            var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var run=new LanStatusServer(new(IPAddress.Loopback,port,"synthetic-old-device-token"),tab5:service).RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());
            try {
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(5)};
                using var request=new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}/tab5/v1/status");
                request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",nonce);request.Headers.Add("X-AIBot-Proof",Tab5Protocol.Proof(key,"GET|"+pair.DeviceId+"|"+nonce));
                request.Headers.Add("X-Tab5-Firmware","0.2.6-ui");
                request.Headers.Add("X-Tab5-Firmware-Proof",Tab5Protocol.Proof(key,"FIRMWARE|"+nonce+"|0.2.6-ui"));
                using var response=await client.SendAsync(request);Check(response.StatusCode==HttpStatusCode.OK,"authenticated HTTP endpoint");
                Check(service.OtaSummary.Contains("可供设备升级"),"HTTP firmware headers reach upgrade status");
                using var data=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,await response.Content.ReadAsByteArrayAsync()));
                Check(data.RootElement.GetProperty("data").GetProperty("codex").GetProperty("state").GetString()=="working","actual TCP payload");
                using var unauth=await client.GetAsync($"http://127.0.0.1:{port}/tab5/v1/status");Check(unauth.StatusCode==HttpStatusCode.Unauthorized,"HTTP no-auth rejection");
                using var actionRequest=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/codex/turn") {Content=new ByteArrayContent(action)};
                actionRequest.Headers.Add("X-AIBot-Device",pair.DeviceId);actionRequest.Headers.Add("X-AIBot-Nonce",nonce);actionRequest.Headers.Add("X-AIBot-Proof",actionProof);
                using var actionResponse=await client.SendAsync(actionRequest);
                Check(actionResponse.StatusCode==HttpStatusCode.Unauthorized,"Codex action HTTP rejects tampered packet");
                using var otaRequest=new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}/tab5/v1/ota/{otaSha}");
                otaRequest.Headers.Add("X-AIBot-Device",pair.DeviceId);otaRequest.Headers.Add("X-AIBot-Nonce",nonce);
                otaRequest.Headers.Add("X-AIBot-Proof",Tab5Protocol.Proof(key,$"GET|/tab5/v1/ota/{otaSha}|{pair.DeviceId}|{nonce}"));
                using var otaResponse=await client.SendAsync(otaRequest);
                Check(otaResponse.StatusCode==HttpStatusCode.OK&&(await otaResponse.Content.ReadAsByteArrayAsync()).SequenceEqual(otaBytes),"authenticated OTA binary HTTP transfer");
                using var otaRejected=await client.GetAsync($"http://127.0.0.1:{port}/tab5/v1/ota/{otaSha}");
                Check(otaRejected.StatusCode==HttpStatusCode.Unauthorized,"anonymous OTA download rejected");
                service.OfferOta(otaPath);service.Publish(snapshot);
                using(var state=JsonDocument.Parse(service.CurrentFrame!)) {
                    var next=state.RootElement.GetProperty("data").GetProperty("ota");
                    Check(next.GetProperty("sha256").GetString()==otaSha&&next.GetProperty("offerId").GetString()!=firstOffer,
                        "reselecting same image creates a new OTA attempt");
                }
                service.CancelOta();service.Publish(snapshot);
                using(var state=JsonDocument.Parse(service.CurrentFrame!))Check(state.RootElement.GetProperty("data").GetProperty("ota").ValueKind==JsonValueKind.Null,"cancelled OTA offer disappears");
                client.DefaultRequestHeaders.Add("X-AIBot-Token","synthetic-old-device-token");
                using var old=await client.GetAsync($"http://127.0.0.1:{port}/status");Check(old.StatusCode==HttpStatusCode.OK,"legacy endpoint preserved");
            } finally {stop.Cancel();await run;}
            // Public fixed key is a cross-language test vector, never a device credential.
            var vectorKey=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();
            var vector=Tab5Protocol.Encrypt(vectorKey,new string('a',32),Encoding.UTF8.GetBytes("TAB5 interop 中文"));
            Console.WriteLine("TAB5_INTEROP_VECTOR="+Convert.ToBase64String(vector));
            Console.WriteLine("TAB5_SELF_TEST_OK crypto, pairing persistence, authenticated HTTP, null data, old-device compatibility");
        } finally {Directory.Delete(directory,true);}
    }
}
