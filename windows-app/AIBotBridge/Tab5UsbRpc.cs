using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private volatile int _usbRpcVersion;
    private volatile int _usbRpcChunk=2048;
    private async Task<byte[]> UsbRpcPacketAsync(byte[]? data,CancellationToken token)
    {
        await _usbGate.WaitAsync(token);
        try {
            var pair=_store.Current;
            if(_usbRpcVersion!=1||pair is null||_usbPort is not {} port)throw new IOException("USB RPC disconnected");
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
                (nonce,proof,packet,ct)=>RpcAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct),247,32768,75,chunk,chunk);
            uint before=pump.CompletedCount;
            try{await pump.PumpAsync(token);}
            catch(OperationCanceledException) when(token.IsCancellationRequested){return;}
            catch(Exception ex) when(ex is IOException or InvalidOperationException or TimeoutException or OperationCanceledException or JsonException or KeyNotFoundException or FormatException or UnauthorizedAccessException) {
                string reason=ex.Message.StartsWith("USB RPC ")?"; "+ex.Message:"";
                _usbRpcLastFailure=$"{DateTimeOffset.Now:HH:mm:ss} {pump.Progress}; {ex.GetType().Name} / 0x{ex.HResult:X8}{reason}";
                if(_usbRpcFirstFailure=="无")_usbRpcFirstFailure=_usbRpcLastFailure;
                pump=null;await Task.Delay(500,token);
            }
            if(pump is not null&&pump.CompletedCount!=before)continue; // The device may already have queued the next range.
            await Task.Delay(pump is not null && Environment.TickCount64-pump.LastActive<1000?5:20,token);
        }
    }
}
