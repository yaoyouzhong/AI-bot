using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5RemoteOtaSelfTest
{
    internal static async Task RunAsync()
    {
        if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
        var store=new Tab5PairingStore();var pair=store.Pair("001122aabbcc",@"USB\REMOTE-OTA-TEST");var key=Convert.FromBase64String(pair.Key);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-remote-ota-test"),()=>[],new Tab5CodexJournal());
        using var service=new Tab5Service(store,tasks);
        var snapshot=new StatusSnapshot(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null));
        snapshot=snapshot with {RuntimeHealth=new("remote-health-fixture",1,DateTimeOffset.UtcNow,
            new[]{"cpu","memory","queue","disk","vpn","pac","codex","bridge"}.Select(k=>new RuntimeHealthCheck(k,k,"ok","正常","","",DateTimeOffset.UtcNow)).ToArray(),[],
            new SystemPressure(0,0,1,0,8000,8,DateTimeOffset.UtcNow){CommitPercent=50,CommittedMemoryMb=8000,CommitLimitMb=16000})};
        service.Publish(snapshot);
        var image=new Tab5OtaPackage(new byte[1024],new string('a',64),"0.2.191-ui",new string('b',32));service.OfferOta(image);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server=new LanStatusServer(new(IPAddress.Loopback,port,"unused"),tab5:service,legacyEnabled:()=>false);
        var serving=server.RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());await ready.Task;
        using var http=new HttpClient();
        try {
            void Fresh(){string n=Guid.NewGuid().ToString("N");if(service.Respond(pair.DeviceId,n,Tab5Protocol.Proof(key,$"GET|{pair.DeviceId}|{n}")) is null)throw new Exception("Device authentication failed");}
            Fresh();service.ObserveFirmware(pair.DeviceId,"0.2.188-ui");
            if(service.CanStartOta||Tab5Service.SupportsRemoteOta("190")||Tab5Service.SupportsRemoteOta("0.2.190"))throw new Exception("Unsupported peer enabled");
            try{await service.StartOtaAsync(CancellationToken.None);throw new Exception("Legacy peer upgraded");}catch(InvalidOperationException){}
            service.ObserveFirmware(pair.DeviceId,"0.2.190-ui");if(!service.CanStartOta)throw new Exception("Capable fresh peer disabled");
            JsonElement Command(){using var frame=JsonDocument.Parse(service.CurrentFrame!);return frame.RootElement.Clone();}
            async Task<JsonElement> Ack(JsonElement frame,string stage,string error="",string? requestOverride=null,string? shaOverride=null,bool corrupt=false) {
                var c=frame.GetProperty("data").GetProperty("connectionHealth").GetProperty("otaCommand");string n=Guid.NewGuid().ToString("N");
                var raw=JsonSerializer.SerializeToUtf8Bytes(new{kind="ota-control",session=frame.GetProperty("session").GetString(),issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    requestId=requestOverride??c.GetProperty("requestId").GetString(),offerId=c.GetProperty("offerId").GetString(),sha256=shaOverride??c.GetProperty("sha256").GetString(),stage,error},JsonDefaults.Options);
                var packet=Tab5Protocol.Encrypt(key,n,raw);string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{n}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
                using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/rpc"){Content=new ByteArrayContent(packet)};
                request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",n);request.Headers.Add("X-AIBot-Proof",corrupt?new string('0',64):proof);
                using var response=await http.SendAsync(request);var bytes=await response.Content.ReadAsByteArrayAsync();
                if(corrupt){if(response.StatusCode!=HttpStatusCode.Unauthorized)throw new Exception("Unauthenticated upgrade accepted");return default;}
                using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,n,bytes));return decoded.RootElement.Clone();
            }
            using var pipeStop=new CancellationTokenSource();string pipeName="AIBot.RemoteOta.Test."+Guid.NewGuid().ToString("N");
            var pipeServer=Tab5OtaControl.RunAsync(()=>service,pipeStop.Token,pipeName);
            try {
                var info=await Tab5OtaControl.CallAsync("info",name:pipeName);
                if(!info.GetProperty("ok").GetBoolean()||info.GetProperty("body").GetProperty("sha256").GetString()!=image.Sha256)throw new Exception("IPC offer metadata differs");
                var invalid=await Tab5OtaControl.CallAsync("start",pair.DeviceId,new string('e',64),pipeName);
                var invalidVerify=await Tab5OtaControl.CallAsync("verify",pair.DeviceId,new string('e',64),pipeName);
                if(invalidVerify.GetProperty("ok").GetBoolean())throw new Exception("Local boot verification accepted the wrong image");
                if(invalid.GetProperty("ok").GetBoolean()||service.RemoteOtaPending)throw new Exception("IPC wrong image started");
            }catch{pipeStop.Cancel();await pipeServer;throw;}
            var localStart=Tab5OtaControl.CallAsync("start",pair.DeviceId,image.Sha256,pipeName);
            long until=Environment.TickCount64+2000;
            while(!service.RemoteOtaPending||Command().GetProperty("data").GetProperty("connectionHealth").GetProperty("otaCommand").ValueKind!=JsonValueKind.Object){if(localStart.IsCompleted||Environment.TickCount64>=until)throw new Exception("IPC start did not publish");await Task.Delay(5);}
            Task pending=localStart;var frame=Command();
            var health=frame.GetProperty("data").GetProperty("runtimeHealth");
            if(health.GetProperty("checks").GetArrayLength()!=8||health.GetProperty("pressure").GetProperty("commitPercent").GetDouble()!=50)
                throw new Exception("Remote upgrade publication lost runtime monitoring");
            if(!service.Busy||!service.RemoteOtaPending)throw new Exception("Request not protected");
            try{service.CancelOta();throw new Exception("Pending image withdrawn");}catch(InvalidOperationException){}
            try{await service.StartOtaAsync(CancellationToken.None);throw new Exception("Double start accepted");}catch(InvalidOperationException){}
            await Ack(frame,"ready",corrupt:true);
            if((await Ack(frame,"ready",requestOverride:new string('c',32))).GetProperty("status").GetInt32()!=409||
                (await Ack(frame,"ready",shaOverride:new string('d',64))).GetProperty("status").GetInt32()!=409)throw new Exception("Wrong request/image committed");
            if(pending.IsCompleted)throw new Exception("Mismatched report completed start");
            var commit=await Ack(frame,"ready");if(!commit.GetProperty("body").GetProperty("commit").GetBoolean()||pending.IsCompleted)throw new Exception("Preparation confused with installation");
            await Ack(frame,"started");await pending;if(!(await localStart).GetProperty("ok").GetBoolean())throw new Exception("IPC start failed");pipeStop.Cancel();await pipeServer;service.ObserveFirmware(pair.DeviceId,image.Version);
            if(service.RemoteOtaPending||service.CanStartOta)throw new Exception("Installed version restarted");
            if((await Ack(frame,"ready")).GetProperty("status").GetInt32()!=409)throw new Exception("Old request committed again");
            service.ObserveFirmware(pair.DeviceId,"0.2.190-ui");Fresh();
            pending=service.StartOtaAsync(CancellationToken.None);frame=Command();await Ack(frame,"rejected","draft_save_failed");
            try{await pending;throw new Exception("Draft failure counted as started");}catch(IOException ex) when(ex.Message.Contains("草稿保存失败")){}
            using var cancel=new CancellationTokenSource();pending=service.StartOtaAsync(cancel.Token);frame=Command();cancel.Cancel();
            try{await pending;throw new Exception("Cancellation lost");}catch(OperationCanceledException){}
            if(service.RemoteOtaPending||service.Busy||(await Ack(frame,"ready")).GetProperty("status").GetInt32()!=409)throw new Exception("Cancelled intent remained armed");
            string failedNonce=Guid.NewGuid().ToString("N");
            if((await service.RpcAsync(pair.DeviceId,failedNonce,new string('0',64),new byte[28],CancellationToken.None,transport:"USB")).Status!=401||
                service.CanStartOta||!service.OtaStartHint.Contains("固件尚未传输"))throw new Exception("Unauthenticated USB left firmware installation enabled");
            frame=Command();string verifiedNonce=Guid.NewGuid().ToString("N");
            var harmless=JsonSerializer.SerializeToUtf8Bytes(new{kind="host-binding-probe",session=frame.GetProperty("session").GetString(),issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
            var verifiedPacket=Tab5Protocol.Encrypt(key,verifiedNonce,harmless);
            string verifiedProof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{verifiedNonce}|{Convert.ToHexString(SHA256.HashData(verifiedPacket)).ToLowerInvariant()}");
            await service.RpcAsync(pair.DeviceId,verifiedNonce,verifiedProof,verifiedPacket,CancellationToken.None,transport:"USB");
            if(!service.CanStartOta)throw new Exception("Authenticated USB could not recover after a pairing failure");
            Console.WriteLine("TAB5_REMOTE_OTA_OK actual encrypted HTTP/RPC; legacy gate; immediate publication; paired commit then started; wrong hash/request/auth, duplicate/cancel/draft failure rejected; USB authentication failure blocks start with no-transfer reason and recovers after valid authentication; no physical flash claim");
        }finally{stop.Cancel();await serving;}
    }
}
