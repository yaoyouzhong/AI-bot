using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    internal static async Task RuntimeDiagnosticsSelfTestAsync() {
        int checks=0;void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
        Check(!SupportsRuntimeDiagnostics("0.2.249-ui")&&SupportsRuntimeDiagnostics("0.2.250-ui"),"capability boundary");
        string folder=Path.Combine(Path.GetTempPath(),"tab5-runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            foreach(string transport in new[]{"Wi-Fi","BLE"}) {
                var store=new Tab5PairingStore(Path.Combine(folder,transport));var pair=store.Pair("001122aabbcc","USB\\FIXTURE");
                using var service=new Tab5Service(store);service.UsbDevices=()=>[];
                service.Publish(new(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null)));
                service.ObserveFirmware(pair.DeviceId,"0.2.250-ui");
                if(transport=="Wi-Fi")service._wifiRequestAt=Environment.TickCount64;else service._bleAckAt=Environment.TickCount64;
                JsonElement Snapshot()=>JsonSerializer.SerializeToElement(new {
                    diagnosticVersion=1,sampleAgeMs=0,deviceId=pair.DeviceId,hostId=Tab5HostBinding.Id(pair),firmware="0.2.250-ui",uptimeMs=123456,
                    uiAgeMs=12,flushCount=300,flushErrors=0,lcdUnderruns=0,displayDiag="1,2,3;ortho,1,0;asset,2,2,1259136,1,3,0,1,4",cameraDiag="9,0,1234",photoDiag="7,0,12",photoUploadDiag="Wi-Fi,123,45,200"});
                JsonElement Report(string requestId,JsonElement? value=null,string? session=null)=>JsonSerializer.SerializeToElement(new {
                    kind="runtime-diagnostics",requestId,session=session??service._session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),snapshot=value??Snapshot()});
                async Task<int> Rpc(JsonElement root,bool proofValid=true,string? channel=null,string? id=null) {
                    string nonce=Guid.NewGuid().ToString("N");byte[] key=Convert.FromBase64String(pair.Key),packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(root));
                    string digest=Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant();
                    var result=await service.RpcAsync(id??pair.DeviceId,nonce,proofValid?Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{digest}"):new string('0',64),packet,CancellationToken.None,transport:channel??transport);
                    if(result.Packet is null)return result.Status;
                    using var reply=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,result.Packet));return reply.RootElement.GetProperty("status").GetInt32();
                }
                Check(await Rpc(Report(new string('a',32)))==409,"unsolicited sample");
                var read=service.RefreshDiagnosticSummaryAsync(CancellationToken.None);var p=service._runtimeProbe!;
                Check(p is not null&&p.Transport==transport,"selected authenticated channel");
                var second=service.RefreshDiagnosticSummaryAsync(CancellationToken.None);
                Check(ReferenceEquals(p,service._runtimeProbe),"concurrent reads coalesce");
                Check(await Rpc(Report(p!.Id),false)==401,"forged proof");
                Check(await Rpc(Report(p.Id),id:"ffeeddccbbaa")==401,"wrong paired device");
                Check(await Rpc(Report(p.Id),channel:transport=="BLE"?"Wi-Fi":"BLE")==409,"wrong channel");
                Check(await Rpc(Report(p.Id,session:new string('a',32)))==400,"wrong session");
                Check(await Rpc(Report(new string('f',32)))==409,"wrong request");
                foreach(string property in new[]{"hostId","deviceId","uptimeMs","displayDiag"}) {
                    var bad=JsonNode.Parse(Snapshot().GetRawText())!.AsObject();bad[property]=property=="displayDiag"?"secret\ntext":"wrong";
                    Check(await Rpc(Report(p.Id,JsonSerializer.SerializeToElement(bad)))==400,"malformed snapshot "+property);
                }
                Check(await Rpc(Report(p.Id))==200,"authenticated snapshot");
                Check(await Rpc(Report(p.Id))==409,"consumed request");
                string summary=await read;await second;
                Check(summary.Contains("诊断采样："+transport)&&summary.Contains("运行毫秒：123456")&&summary.Contains("实时"),"fresh common format");
                Check(summary.Contains(";asset,2,2,1259136,1,3,0,1,4"),"optional asset-copy diagnostics retained");
                Check(service._runtimeProbe is null&&service._ota is null,"independent of OTA");
                Check((await service.RefreshDiagnosticSummaryAsync(CancellationToken.None)).Contains("123456")&&service._runtimeProbe is null,"fresh read cached");
                service._runtimeSample=service._runtimeSample! with{Tick=Environment.TickCount64-11000};
                Check(service.DeviceHealthDiagnostic.Contains("已过期"),"stale explicit");
                if(transport=="Wi-Fi")service._wifiRequestAt=Environment.TickCount64;else service._bleAckAt=Environment.TickCount64;
                read=service.RefreshDiagnosticSummaryAsync(CancellationToken.None);p=service._runtimeProbe!;
                service._runtimeProbe=p with{Expires=DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds()};
                Check(await Rpc(Report(p.Id))==409,"expired challenge");service._runtimeProbe=p;
                await Task.Delay(2);service.RecordRuntimeHealth(Snapshot(),"USB");
                Check(await Rpc(Report(p.Id))==200,"reply during USB recovery");await read;
                Check(service.DeviceHealthDiagnostic.Contains("诊断采样：USB"),"newer USB sample wins");
                service._runtimeSample=service._runtimeSample! with{Tick=Environment.TickCount64-11000};
                read=service.RefreshDiagnosticSummaryAsync(CancellationToken.None);p=service._runtimeProbe!;
                p.Completion.TrySetException(new TimeoutException());summary=await read;
                Check(summary.Contains("已过期")&&summary.Contains("超时")&&summary.Contains("123456"),"failure retains stale evidence");
                service._wifiRequestAt=service._bleAckAt=Environment.TickCount64;
                read=service.RefreshDiagnosticSummaryAsync(CancellationToken.None);p=service._runtimeProbe!;
                Check(p.Transport=="Wi-Fi","both radios initially prefer Wi-Fi");
                p.Completion.TrySetException(new TimeoutException());
                for(int i=0;i<100&&ReferenceEquals(p,service._runtimeProbe);i++)await Task.Delay(10);
                var fallback=service._runtimeProbe!;
                Check(fallback is not null&&fallback.Transport=="BLE"&&fallback.Id!=p.Id&&fallback.HostId==p.HostId,
                    "BLE-only selection retries a fresh challenge on the same host");
                Check(await Rpc(Report(p.Id),channel:"Wi-Fi")==409,"late first-channel reply rejected");
                Check(await Rpc(Report(fallback!.Id),channel:"BLE")==200,"fallback channel accepted");
                Check((await read).Contains("诊断采样：BLE"),"fallback is labelled with actual transport");
            }
            var reserve=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);reserve.Start();int port=((System.Net.IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
            using var stop=new CancellationTokenSource();int requested=0;
            var server=new LocalStatusServer(port,tab5LiveInfo:async token=>{requested++;await Task.Delay(10,token);return "live-wireless-fixture";});
            var running=server.RunAsync(SessionActivityReader.Capture,stop.Token);
            using var http=new HttpClient(new HttpClientHandler{UseProxy=false});
            try {
                string url=$"http://127.0.0.1:{port}/diagnostics/tab5";
                using var response=JsonDocument.Parse(await http.GetStringAsync(url));
                Check(response.RootElement.GetProperty("summary").GetString()=="live-wireless-fixture"&&requested==1,"existing endpoint awaits live refresh");
                using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Add("Origin","https://example.org");
                using var blocked=await http.SendAsync(request);
                Check(blocked.StatusCode==System.Net.HttpStatusCode.NotFound&&requested==1,"browser cannot trigger wireless diagnostics");
            }finally{stop.Cancel();await running;}
            Console.WriteLine($"RUNTIME_DIAGNOSTICS_OK checks={checks}; Wi-Fi/BLE encrypted RPC, identity/session/channel/request/expiry/auth rejection, coalescing, USB recovery, stale retention, loopback live HTTP and browser isolation");
        }finally {
            string resolved=Path.GetFullPath(folder),parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!resolved.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("tab5-runtime-",StringComparison.Ordinal))throw new IOException("Invalid test cleanup path");
            Directory.Delete(resolved,true);
        }
    }
}
