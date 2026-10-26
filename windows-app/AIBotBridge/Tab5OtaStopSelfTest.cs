using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal static class OtaStopSelfTest
    {
        internal static void StagePreview(Tab5Service service,string state)
        {
            if(!AppPaths.IsPublicSelfTest)throw new InvalidOperationException("Offline fixture only");
            lock(service._lifecycle) {
                var delivery=service._otaDelivery!;service.BeginOtaDelivery(delivery);delivery.Committed=delivery.DataStarted=true;
                delivery.FinalReleased=state=="finalizing";
                Interlocked.Exchange(ref service._rpcOtaUntil,Environment.TickCount64+90000);
                service._otaTransferDiagnostic=$"BLE；已提供：{(delivery.FinalReleased?65536:16384)}/65536；等待设备校验";
            }
            if(state is "stopping" or "stopped") {
                service.StopOta();
                if(state=="stopped") {
                    var d=service._otaDelivery!;
                    service.ObserveOtaStopped(JsonSerializer.SerializeToElement(new{deviceId=d.Device,hostId=d.Host,firmware=d.OriginalVersion,otaDiag="0,0,0,0"}),service.CaptureOtaStopProbe("preview",d.StopAt+1));
                }
            }
        }
        internal static async Task RunAsync()
        {
            if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
            string path=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ota-stop-"+Guid.NewGuid().ToString("N"));
            using var serverStop=new CancellationTokenSource();
            Task? serving=null;
            try {
                var store=new Tab5PairingStore(path);var pair=store.Pair("001122aabbcc",@"USB\OTA-STOP-FIXTURE");
                byte[] key=Convert.FromBase64String(pair.Key);
                using var service=new Tab5Service(store);service.UsbDevices=()=>[];
                var snapshot=new StatusSnapshot(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null));
                service.Publish(snapshot);service.ObserveFirmware(pair.DeviceId,"0.2.254-ui");
                var portProbe=new TcpListener(IPAddress.Loopback,0);portProbe.Start();
                int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
                var listener=new LanStatusServer(new LanPairing(IPAddress.Loopback,port,"test-only"),tab5:service,legacyEnabled:()=>false);
                var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serving=listener.RunAsync(()=>snapshot,serverStop.Token,()=>ready.TrySetResult());
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(3));
                using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
                var rpcUrl=new Uri($"http://127.0.0.1:{port}/tab5/v1/rpc");

                string nonce=Guid.NewGuid().ToString("N");service.Respond(pair.DeviceId,nonce,Tab5Protocol.Proof(key,$"GET|{pair.DeviceId}|{nonce}"));
                var image=new Tab5OtaPackage(new byte[65536],new string('a',64),"0.2.255-ui",new string('b',32));
                JsonElement Frame(){using var doc=JsonDocument.Parse(service.CurrentFrame!);return doc.RootElement.Clone();}
                async Task<JsonElement> Rpc(object payload,string transport="BLE") {
                    string n=Guid.NewGuid().ToString("N");var packet=Tab5Protocol.Encrypt(key,n,JsonSerializer.SerializeToUtf8Bytes(payload,JsonDefaults.Options));
                    string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{n}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
                    (int Status,byte[]? Packet) result;
                    if(transport=="Wi-Fi") {
                        using var request=new HttpRequestMessage(HttpMethod.Post,rpcUrl);
                        request.Headers.Add("X-AIBot-Device",pair.DeviceId);
                        request.Headers.Add("X-AIBot-Nonce",n);request.Headers.Add("X-AIBot-Proof",proof);
                        request.Content=new ByteArrayContent(packet);
                        using var response=await http.SendAsync(request);
                        result=((int)response.StatusCode,await response.Content.ReadAsByteArrayAsync());
                    }else result=await service.RpcAsync(pair.DeviceId,n,proof,packet,CancellationToken.None,transport:transport);
                    if(result.Status!=200)throw new Exception("Fixture transport rejected");
                    using var reply=JsonDocument.Parse(Tab5Protocol.Decrypt(key,n,result.Packet!));return reply.RootElement.Clone();
                }
                Task<JsonElement> Range(Tab5OtaPackage offer,int offset,int count,string transport="BLE")=>Rpc(new{kind="ota",session=Frame().GetProperty("session").GetString(),issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),offerId=offer.OfferId,sha256=offer.Sha256,offset,count},transport);
                Task<JsonElement> Ready(JsonElement command,string stage="ready",string error="")=>Rpc(new{kind="ota-control",session=Frame().GetProperty("session").GetString(),issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    requestId=command.GetProperty("requestId").GetString(),offerId=command.GetProperty("offerId").GetString(),sha256=command.GetProperty("sha256").GetString(),stage,error});
                JsonElement Idle(string? host=null,string? version=null,string diagnostic="0,0,0,0")=>JsonSerializer.SerializeToElement(new{deviceId=pair.DeviceId,hostId=host??Tab5HostBinding.Id(pair),firmware=version??"0.2.254-ui",otaDiag=diagnostic});
                JsonElement IdleUsb(long sequence,string? host=null,bool owned=true)=>JsonSerializer.SerializeToElement(new {
                    deviceId=pair.DeviceId,currentHostId=host??Tab5HostBinding.Id(pair),usbHostAuthenticated=owned,
                    firmware="0.2.254-ui",otaDiag="0,0,0,0",sequence});
                OtaStopProbe? Probe(long started,long? sequence=null)=>service.CaptureOtaStopProbe(Guid.NewGuid().ToString("N"),started,sequence);
                async Task RuntimeProbeResult(long started) {
                    var probe=new RuntimeProbe(pair.DeviceId,Tab5HostBinding.Id(pair),Guid.NewGuid().ToString("N"),"BLE",
                        DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds(),started,new(TaskCreationOptions.RunContinuationsAsynchronously));
                    lock(service._runtimeDiagnosticLock)service._runtimeProbe=probe;
                    var fetching=service.FetchRuntimeDiagnosticsAsync(probe);
                    var snapshot=JsonSerializer.SerializeToElement(new {
                        diagnosticVersion=1,sampleAgeMs=0,deviceId=pair.DeviceId,hostId=Tab5HostBinding.Id(pair),firmware="0.2.254-ui",
                        uptimeMs=123456,uiAgeMs=0,flushCount=1,flushErrors=0,lcdUnderruns=0,
                        displayDiag="0",cameraDiag="0",photoDiag="0",photoUploadDiag="0",otaDiag="0,0,0,0"});
                    var report=await Rpc(new {kind="runtime-diagnostics",session=Frame().GetProperty("session").GetString(),
                        issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),requestId=probe.Id,snapshot});
                    if(report.GetProperty("status").GetInt32()!=200)throw new Exception("Challenged runtime fixture rejected");
                    await fetching;
                }
                void Check(bool value,string message){if(!value)throw new Exception(message);}
                void MustReject(Action action,string message){try{action();throw new Exception(message);}catch(InvalidOperationException){}}
                service.OfferOta(image);var pending=service.StartOtaAsync(CancellationToken.None);
                var command=Frame().GetProperty("data").GetProperty("connectionHealth").GetProperty("otaCommand").Clone();
                Check(service.CanStopOta,"Pending start cannot stop");service.StopOta();
                try{await pending;throw new Exception("Stopped start succeeded");}catch(IOException){}
                Check((await Ready(command)).GetProperty("status").GetInt32()==409&&!service.HasOta&&!service.Busy,"Stopped intent still commits");
                Check(service.UpgradeStatusText.State=="已停止","Uncommitted request not locally stopped");
                foreach(string transport in new[]{"BLE","USB","Wi-Fi"}) {
                    service.OfferOta(image);var offer=service._ota!;
                    Check(offer.OfferId!=image.OfferId,"Stopped offer identity reused");image=offer;
                    Check((await Range(offer,0,2048,transport)).GetProperty("status").GetInt32()==200&&service.CanStopOta,"Range cannot stop");
                    // Lost replies may repeat an immutable range under a new
                    // nonce. Offset/data and offer scope must stay exact.
                    var repeated=await Range(offer,0,2048,transport);
                    Check(repeated.GetProperty("status").GetInt32()==200&&
                        repeated.GetProperty("body").GetProperty("offset").GetInt32()==0&&
                        Convert.FromBase64String(repeated.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(offer.Image[..2048]),
                        "Repeated range changed bytes or offset");
                    Check((await Range(offer with {Sha256=new string('c',64)},2048,2048,transport)).GetProperty("status").GetInt32()==404,
                        "Resume mixed another image hash");

                    service.StopOta();long stopped=service._otaDelivery!.StopAt;
                    Check(service.UpgradeStatusText.State=="正在停止"&&!service.HasOta,"Early cancellation success");
                    MustReject(()=>service.OfferOta(image),"New image armed before stop confirmation");
                    Check((await Range(offer,2048,2048,transport)).GetProperty("status").GetInt32()==404,"Cancelled range still served");
                    service.ObserveOtaStopped(Idle(),Probe(stopped));service.ObserveOtaStopped(Idle(host:new string('c',32)),Probe(stopped+1));
                    service.ObserveOtaStopped(Idle(version:offer.Version),Probe(stopped+1));service.ObserveOtaStopped(Idle(diagnostic:"0,0,0,1"),Probe(stopped+1));
                    service.ObserveOtaStopped(Idle(diagnostic:"0,bad,0,0"),Probe(stopped+1));
                    service.ObserveOtaStopped(Idle(),null);
                    Check(service.OtaStopPending,"Stale, busy, wrong-host or wrong-version confirmation accepted");
                    if(transport=="USB") {
                        var usbProbe=Probe(stopped+1,7);
                        service.ObserveOtaStopped(Idle(),usbProbe,usb:true);
                        service.ObserveOtaStopped(IdleUsb(6),usbProbe,usb:true);
                        service.ObserveOtaStopped(IdleUsb(7,host:new string('c',32)),usbProbe,usb:true);
                        service.ObserveOtaStopped(IdleUsb(7,owned:false),usbProbe,usb:true);
                        Check(service.OtaStopPending,"Unmatched/unauthenticated USB ACK confirmed stop");
                        service.ObserveOtaStopped(IdleUsb(7),usbProbe,usb:true);
                    }else {
                        await RuntimeProbeResult(stopped-1);
                        Check(service.OtaStopPending,"A valid but pre-stop wireless challenge confirmed stop");
                        await RuntimeProbeResult(Math.Max(Environment.TickCount64,stopped+1));
                    }
                    Check(service.UpgradeStatusText.State=="已停止"&&service.CanSelectOta,"Fresh same-host idle confirmation lost");
                }
                // A committed start is different from an uncommitted intention.
                service.OfferOta(image);pending=service.StartOtaAsync(CancellationToken.None);
                command=Frame().GetProperty("data").GetProperty("connectionHealth").GetProperty("otaCommand").Clone();
                await Ready(command);service.StopOta();try{await pending;throw new Exception("Committed stop succeeded as start");}catch(IOException){}
                Check(service.OtaStopPending,"Commit race claimed immediate stop");
                Check((await Ready(command)).GetProperty("status").GetInt32()==409,"Late ready rearmed stopped request");
                var committedStop=service._otaDelivery!;
                Check(committedStop.ConfirmAfter>committedStop.StopAt+5000,"Commit-expiry grace was not retained");
                Check(Probe(committedStop.StopAt+1) is null,"Probe armed in pre-install idle interval");
                service.ObserveOtaStopped(Idle(),new(committedStop,"old-probe",committedStop.StopAt-1));
                service.ObserveOtaStopped(Idle(),new(committedStop,"early-probe",committedStop.StopAt+1));
                Check(service.OtaStopPending,"Delayed commit/pre-stop probe falsely confirmed stop");
                service.ObserveOtaStopped(Idle(),Probe(committedStop.ConfirmAfter+1));
                // An explicit rejection is not an active transfer, and a late
                // ready cannot turn that completed request into a new commit.
                service.OfferOta(image);pending=service.StartOtaAsync(CancellationToken.None);
                command=Frame().GetProperty("data").GetProperty("connectionHealth").GetProperty("otaCommand").Clone();
                await Ready(command,"rejected","draft_pending");
                try{await pending;throw new Exception("Rejected request succeeded");}catch(IOException){}
                Check(!service.OtaStarted&&!service.CanStopOta&&service.UpgradeStatusText.State!="正在传输","Rejected start still displayed as a transfer");
                Check((await Ready(command)).GetProperty("status").GetInt32()==409,"Late ready rearmed rejected request");
                service.OfferOta(image);image=service._ota!;
                Check((await Range(image,image.Image.Length-2048,2048)).GetProperty("status").GetInt32()==200,"Final range failed");
                Check(!service.CanStopOta,"Stop allowed after final range");MustReject(service.StopOta,"Final range was withdrawn");
                Check(service.HasOta&&service.UpgradeStatusText.State=="等待校验","Final phase offer or guidance lost");
                service.ObserveFirmware(pair.DeviceId,image.Version);
                foreach(bool compressed in new[]{false,true}) {
                    byte[] pixels=RandomNumberGenerator.GetBytes(65536);service.OfferOta(image with{Image=pixels,OfferId=Guid.NewGuid().ToString("N")});image=service._ota!;
                    service.ObserveFirmware(pair.DeviceId,"0.2.254-ui");
                    using var stream=new PausedStream();var sending=service.TransferOtaAsync(stream,pixels,pair.DeviceId,"tcp-v2",CancellationToken.None,compressed?Tab5OtaCompression.StreamEncoding:"");
                    await stream.Body.WaitAsync(TimeSpan.FromSeconds(2));Check(service.CanStopOta,"HTTP transfer cannot stop");service.StopOta();
                    try{await sending;throw new Exception("HTTP stop still sent full image");}catch(OperationCanceledException){}
                    Check(!service.Busy&&service.OtaStopPending&&stream.Bytes==0,"Stream not cancelled, prematurely confirmed, or full body leaked");
                    service.ObserveOtaStopped(Idle(),Probe(service._otaDelivery!.StopAt+1));
                    try{await service.TransferOtaAsync(Stream.Null,pixels,pair.DeviceId,"tcp-v2",CancellationToken.None);throw new Exception("Prechecked HTTP response restarted after cancellation");}catch(OperationCanceledException){}
                }
                byte[] last=new byte[1024];service.OfferOta(image with{Image=last,OfferId=Guid.NewGuid().ToString("N")});
                using(var stream=new PausedStream()) {
                    var sending=service.TransferOtaAsync(stream,last,pair.DeviceId,"tcp-v2",CancellationToken.None);
                    await stream.Body.WaitAsync(TimeSpan.FromSeconds(2));Check(!service.CanStopOta,"Final HTTP write can race stop");
                    MustReject(service.StopOta,"Final HTTP write stopped");stream.Release();await sending;
                    Check(stream.Bytes==last.Length&&service.HasOta,"Rejected stop changed final stream");
                }
                Console.WriteLine("TAB5_OTA_STOP_OK encrypted BLE/USB and actual Wi-Fi HTTP ranges, repeated offsets, wrong-hash resume rejection, pending/committed ready races, stale offer rejection, HTTP raw/compressed cancellation, final-byte exclusion, post-stop challenged diagnostics, delayed-commit expiry, exact authenticated USB ACK, rejected-start recovery; no hardware flash claim");
            }finally{serverStop.Cancel();if(serving is not null)await serving;if(File.Exists(path))File.Delete(path);}
        }
        private sealed class PausedStream : MemoryStream
        {
            private readonly TaskCompletionSource _body=new(TaskCreationOptions.RunContinuationsAsynchronously),_release=new(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _headers;
            internal Task Body=>_body.Task;
            internal int Bytes;
            internal void Release()=>_release.TrySetResult();
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default) {
                if(!_headers){_headers=true;return;}
                _body.TrySetResult();await _release.Task.WaitAsync(token);Bytes+=buffer.Length;
            }
        }
    }
}
