using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private volatile int _usbRpcVersion;
    private volatile int _usbRpcChunk=2048;
    private volatile bool _usbRpcBinary;
    private volatile string _usbRpcTiming="尚无请求";
    private async Task<byte[]> UsbRpcPacketAsync(byte[]? data,CancellationToken token)
    {
        await _usbGate.WaitAsync(token);
        try {
            var pair=_store.Current;
            if(_usbRpcVersion!=1||pair is null||_usbPort is not {} port)throw new IOException("USB RPC disconnected");
            if(_usbRpcBinary) {
                if(data?.Length>Tab5UsbBinary.Maximum)throw new IOException("USB binary request too large");
                uint binaryTag=Tab5UsbBinary.Tag();
                try {
                    Send(port,new{version=1,type="tab5_rpc_binary",deviceId=pair.DeviceId,tag=binaryTag,count=data?.Length??0});
                    if(data is not null)port.Write(data,0,data.Length);
                    long deadline=Environment.TickCount64+4000;
                    var header=await Tab5UsbBinary.ReadAsync(port,16,deadline,token);
                    int size=Tab5UsbBinary.Length(header,binaryTag);
                    if(data is not null&&size!=0||data is null&&size<8)throw new IOException("USB binary reply length mismatch");
                    return await Tab5UsbBinary.ReadAsync(port,size,deadline,token);
                }catch{CloseUsbPort();throw;} // discard a partial stream before any new command
            }
            string tag=Guid.NewGuid().ToString("N");
            Send(port,new{version=1,type="tab5_rpc",deviceId=pair.DeviceId,tag,chunk=_usbRpcChunk,data=data is null?null:Convert.ToBase64String(data)});
            using var reply=await ReadReplyAsync(port,"tab5_rpc",token);var r=reply.RootElement;
            if(r.GetProperty("deviceId").GetString()!=pair.DeviceId)throw new IOException("USB RPC device mismatch");
            if(r.GetProperty("tag").GetString()!=tag)throw new IOException("USB RPC stale acknowledgement");
            if(!r.GetProperty("ok").GetBoolean())throw new IOException("USB RPC device rejected fragment");
            return data is null?Convert.FromBase64String(r.GetProperty("data").GetString()!):[];
        }finally{_usbGate.Release();}
    }
    private async Task RunUsbRpcAsync(CancellationToken token)
    {
        // Gate is held for one serial transaction only. A slow desktop request
        // executes outside it, so status/identity/configuration retain access.
        Tab5BleVoice? pump=null;int pumpChunk=0;
        while(!token.IsCancellationRequested) {
            if(_usbRpcVersion!=1){pump=null;await Task.Delay(100,token);continue;}
            int chunk=_usbRpcChunk;if(pumpChunk!=chunk){pump=null;pumpChunk=chunk;}
            pump??=new Tab5BleVoice(ct=>UsbRpcPacketAsync(null,ct),async(data,ct)=>{await UsbRpcPacketAsync(data,ct);},
                (nonce,proof,packet,ct)=>RpcAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct),247,Tab5RpcBinary.MailboxMaximum,75,chunk,chunk,maximumRequest:Tab5RpcBinary.MailboxMaximum);
            uint before=pump.CompletedCount;
            try{if(await pump.PumpAsync(token))_usbRpcTiming=$"{DateTimeOffset.Now:HH:mm:ss} chunk={chunk}; {pump.Timing}";}
            catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception ex) when(ex is IOException or InvalidOperationException or TimeoutException or OperationCanceledException or JsonException or KeyNotFoundException or FormatException or UnauthorizedAccessException) {
                string reason=ex.Message.StartsWith("USB RPC ")||ex.Message.StartsWith("RPC response rejected:")?"; "+ex.Message:
                    "; at "+new System.Diagnostics.StackTrace(ex).GetFrame(0)?.GetMethod()?.DeclaringType?.FullName;
                _usbRpcLastFailure=$"{DateTimeOffset.Now:HH:mm:ss} {pump.Progress}; {ex.GetType().Name} / 0x{ex.HResult:X8}{reason}; auth={_rpcAuthDiagnostic}";
                if(_usbRpcFirstFailure=="无")_usbRpcFirstFailure=_usbRpcLastFailure;
                pump=null;await Task.Delay(500,token);
            }
            if(pump is not null&&pump.CompletedCount!=before)continue; // The device may already have queued the next range.
            await Task.Delay(pump is not null && Environment.TickCount64-pump.LastActive<1000?5:20,token);
        }
    }
}
