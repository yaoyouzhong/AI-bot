using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private volatile int _usbRpcVersion;
    private volatile int _usbRpcChunk=2048;
    private volatile bool _usbRpcBinary;
    private volatile bool _usbRxBatch;
    private volatile int _usbRpcWait;
    private volatile string _usbRpcTiming="尚无请求";
    private long _usbRpcGateWaitMs,_usbRpcGateWaitPeakMs;
    private void RecordUsbRpcGateWait(long waited)
    {
        Interlocked.Add(ref _usbRpcGateWaitMs,waited);
        long peak=Interlocked.Read(ref _usbRpcGateWaitPeakMs);
        while(waited>peak){long seen=Interlocked.CompareExchange(ref _usbRpcGateWaitPeakMs,waited,peak);if(seen==peak)break;peak=seen;}
    }
    private Task<byte[]> UsbRpcPacketAsync(byte[]? data,long generation,CancellationToken token) {
        var profile=ActiveGalleryHostProfile;long queued=Tab5GalleryHostProfile.Now;
        return Tab5UsbRecovery.InSessionAsync(_usbGate,generation,()=>Interlocked.Read(ref _usbGeneration),async ()=> {
            long acquired=Tab5GalleryHostProfile.Now;
            var pair=_store.Current;
            if(_usbRpcVersion!=1||pair is null||_usbPort is not {} port)throw new IOException("USB RPC disconnected");
            if(_usbRpcBinary) {
                int chunk=_usbRpcChunk;
                if(data?.Length>chunk+9)throw new IOException("USB binary request too large");
                uint binaryTag=Tab5UsbBinary.Tag();
                Send(port,new{version=1,type="tab5_rpc_binary",deviceId=pair.DeviceId,tag=binaryTag,count=data?.Length??0,chunk,
                    rxBatch=_usbRxBatch?4096:0,waitMs=data is null?_usbRpcWait:0});
                long commandSent=Tab5GalleryHostProfile.Now;
                if(data is not null)Tab5UsbShortPacket.Write(port,data);
                long bodySent=Tab5GalleryHostProfile.Now;
                long deadline=Environment.TickCount64+4000;
                var header=await Tab5UsbBinary.ReadAsync(port,16,deadline,token);
                long headerRead=Tab5GalleryHostProfile.Now;
                int size=Tab5UsbBinary.Length(header,binaryTag);
                if(data is not null&&size!=0||data is null&&(size<8||size>chunk+8))throw new IOException("USB binary reply length mismatch");
                var binaryReply=await Tab5UsbBinary.ReadAsync(port,size,deadline,token);
                long finished=Tab5GalleryHostProfile.Now;
                if(profile is not null) {
                    if(data is null&&binaryReply.Length>=8&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(binaryReply)==0)
                        profile.Idle(Tab5GalleryHostProfile.Us(queued,finished));
                    else profile.Add(new{kind="usb",tx=data?.Length??0,rx=size,gateUs=Tab5GalleryHostProfile.Us(queued,acquired),
                        commandUs=Tab5GalleryHostProfile.Us(acquired,commandSent),writeUs=Tab5GalleryHostProfile.Us(commandSent,bodySent),
                        headerUs=Tab5GalleryHostProfile.Us(bodySent,headerRead),readUs=Tab5GalleryHostProfile.Us(headerRead,finished)});
                }
                return binaryReply;
            }
            string tag=Guid.NewGuid().ToString("N");
            Send(port,new{version=1,type="tab5_rpc",deviceId=pair.DeviceId,tag,chunk=_usbRpcChunk,data=data is null?null:Convert.ToBase64String(data)});
            using var reply=await ReadReplyAsync(port,"tab5_rpc",token);var r=reply.RootElement;
            if(r.GetProperty("deviceId").GetString()!=pair.DeviceId)throw new IOException("USB RPC device mismatch");
            if(r.GetProperty("tag").GetString()!=tag)throw new IOException("USB RPC stale acknowledgement");
            if(!r.GetProperty("ok").GetBoolean())throw new IOException("USB RPC device rejected fragment");
            return data is null?Convert.FromBase64String(r.GetProperty("data").GetString()!):[];
        },CloseUsbPort,RecordUsbRpcGateWait,token);
    }
    private async Task RunUsbRpcAsync(CancellationToken token)
    {
        // Gate is held for one serial transaction only. A slow desktop request
        // executes outside it, so status/identity/configuration retain access.
        Tab5BleVoice? pump=null;int pumpChunk=0;long pumpGeneration=-1;
        while(!token.IsCancellationRequested) {
            if(_usbRpcVersion!=1){pump=null;await Task.Delay(100,token);continue;}
            long generation=Interlocked.Read(ref _usbGeneration);
            int chunk=_usbRpcChunk;if(pumpChunk!=chunk||pumpGeneration!=generation){pump=null;pumpChunk=chunk;pumpGeneration=generation;}
            pump??=new Tab5BleVoice(ct=>UsbRpcPacketAsync(null,generation,ct),async(data,ct)=>{await UsbRpcPacketAsync(data,generation,ct);},
                (nonce,proof,packet,ct)=>RpcAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct),247,Tab5RpcBinary.MailboxMaximum,75,chunk,chunk,maximumRequest:Tab5RpcBinary.MailboxMaximum);
            _usbRpcPump=pump;
            uint before=pump.CompletedCount;
            try{if(await pump.PumpAsync(token))_usbRpcTiming=$"{DateTimeOffset.Now:HH:mm:ss} chunk={chunk}; {pump.Timing}";}
            catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception ex) when(Tab5UsbRecovery.IsDisconnect(ex,token)||ex is FormatException) {
                string reason=ex.Message.StartsWith("USB RPC ")||ex.Message.StartsWith("USB binary ")||ex.Message.StartsWith("RPC response rejected:")?"; "+ex.Message:
                    "; at "+new System.Diagnostics.StackTrace(ex).GetFrame(0)?.GetMethod()?.DeclaringType?.FullName;
                _usbRpcLastFailure=$"{DateTimeOffset.Now:HH:mm:ss} {pump.Progress}; {ex.GetType().Name} / 0x{ex.HResult:X8}{reason}; auth={_rpcAuthDiagnostic}";
                if(_usbRpcFirstFailure=="无")_usbRpcFirstFailure=_usbRpcLastFailure;
                pump=null;await Task.Delay(500,token);
            }
            finally {_usbRpcPump=null;}
            if(pump is not null&&pump.CompletedCount!=before)continue; // The device may already have queued the next range.
            if(pump is not null&&_usbRpcWait>0)continue; // The peer bounded-waits for a new request instead of sleeping after an empty poll.
            await Task.Delay(pump is not null && Environment.TickCount64-pump.LastActive<1000?5:20,token);
        }
    }
}
