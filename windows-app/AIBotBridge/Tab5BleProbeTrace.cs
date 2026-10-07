using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5BleRpcTiming(long StartedMs,long CompletedMs,long RequestMs,long HandlerMs,long ResponseMs,int RequestBytes,int ResponseBytes);
internal sealed record Tab5BleGattTiming(long ReadMs,long CommandCount,long CommandWaitSumMs,long AckCount,long AckWaitSumMs,long Batches,long BatchWallMs,long QueuedBatches,long NativeLimit=0,long WriteWindow=0);
internal sealed record Tab5BleRpcSample(Tab5BleRpcTiming Rpc,long QueueMs,Tab5BleGattTiming? Gatt,string Link);

// One bounded, in-memory trace per explicitly started BLE RAM preflight.
// No request IDs, payloads, pairing details or user content are retained.
internal sealed class Tab5BleProbeTrace
{
    private readonly object _gate=new();
    private readonly List<Tab5BleRpcSample> _samples=[];
    private long _started=-1,_stopped=-1,_firstRpc=-1,_lastRpc=-1;
    private long _generation;
    private int _duration=35000;
    private long _count,_dropped,_requestMs,_handlerMs,_responseMs,_queueMs,_responseBytes,_gapMs,_maxGapMs;
    private long _ackCount,_ackMs,_commandWaitMs,_batchMs;
    private long _telemetryCount,_telemetryMs,_voicePolls,_voicePollMs,_rpcIdlePolls,_rpcIdlePollMs,_failures;
    internal void Begin(long now,int duration=35000) {lock(_gate) {
        _duration=Math.Clamp(duration,35000,160000);
        _generation++;_started=now;_stopped=_firstRpc=_lastRpc=-1;_samples.Clear();
        _count=_dropped=_requestMs=_handlerMs=_responseMs=_queueMs=_responseBytes=_gapMs=_maxGapMs=0;
        _ackCount=_ackMs=_commandWaitMs=_batchMs=0;
        _telemetryCount=_telemetryMs=_voicePolls=_voicePollMs=_rpcIdlePolls=_rpcIdlePollMs=_failures=0;
    }}
    internal void End(long now) {lock(_gate){if(_started>=0&&_stopped<0)_stopped=Math.Max(_started,now);}}
    // A device may finish its last RAM check before Windows completes the final
    // ATT write. Retain that in-flight sample, but never include later traffic.
    internal long Ticket(long now) {lock(_gate){return _started>=0&&_stopped<0&&now>=_started&&now-_started<_duration?_generation:0;}}
    private bool Includes(long ticket,long start,long end)=>ticket!=0&&ticket==_generation&&start>=_started&&end>=start&&start-_started<_duration;
    internal void RecordRpc(long ticket,Tab5BleRpcSample sample) {lock(_gate) {
        var rpc=sample.Rpc;if(!Includes(ticket,rpc.StartedMs,rpc.CompletedMs))return;
        if(_firstRpc<0)_firstRpc=rpc.StartedMs;
        if(_lastRpc>=0){long gap=Math.Max(0,rpc.StartedMs-_lastRpc);_gapMs+=gap;_maxGapMs=Math.Max(_maxGapMs,gap);}
        _lastRpc=Math.Max(_lastRpc,rpc.CompletedMs);_count++;
        _requestMs+=rpc.RequestMs;_handlerMs+=rpc.HandlerMs;_responseMs+=rpc.ResponseMs;
        _responseBytes+=rpc.ResponseBytes;_queueMs+=sample.QueueMs;
        if(sample.Gatt is {} g){_ackCount+=g.AckCount;_ackMs+=g.AckWaitSumMs;_commandWaitMs+=g.CommandWaitSumMs;_batchMs+=g.BatchWallMs;}
        if(_samples.Count<(_duration>35000?160:32))_samples.Add(sample);else _dropped++;
    }}
    internal void RecordActivity(long ticket,string kind,long started,long completed) {lock(_gate) {
        if(!Includes(ticket,started,completed))return;long ms=completed-started;
        switch(kind) {
            case "telemetry":_telemetryCount++;_telemetryMs+=ms;break;
            case "voice-poll":_voicePolls++;_voicePollMs+=ms;break;
            case "rpc-idle":_rpcIdlePolls++;_rpcIdlePollMs+=ms;break;
            case "failure":_failures++;break;
        }
    }}
    internal string Json {get {lock(_gate) {
        if(_started<0)return "尚未预检";
        return JsonSerializer.Serialize(new {
            schema=1,running=_stopped<0,controlMs=_stopped<0?(long?)null:_stopped-_started,
            firstRpcDelayMs=_firstRpc<0?(long?)null:_firstRpc-_started,
            rpcCount=_count,droppedSamples=_dropped,responseBytes=_responseBytes,
            requestMs=_requestMs,handlerMs=_handlerMs,responseMs=_responseMs,queueMs=_queueMs,
            interRpcGapMs=_gapMs,maxInterRpcGapMs=_maxGapMs,ackCount=_ackCount,ackWaitSumMs=_ackMs,
            commandWaitSumMs=_commandWaitMs,batchWallMs=_batchMs,
            telemetryCount=_telemetryCount,telemetryWallMs=_telemetryMs,voicePollCount=_voicePolls,voicePollWallMs=_voicePollMs,
            rpcIdlePollCount=_rpcIdlePolls,rpcIdlePollWallMs=_rpcIdlePollMs,failures=_failures,
            samples=_samples.Select(s=>new {
                startMs=s.Rpc.StartedMs-_started,endMs=s.Rpc.CompletedMs-_started,
                requestMs=s.Rpc.RequestMs,handlerMs=s.Rpc.HandlerMs,responseMs=s.Rpc.ResponseMs,
                requestBytes=s.Rpc.RequestBytes,responseBytes=s.Rpc.ResponseBytes,queueMs=s.QueueMs,gatt=s.Gatt,link=s.Link
            })
        });
    }}}
}
