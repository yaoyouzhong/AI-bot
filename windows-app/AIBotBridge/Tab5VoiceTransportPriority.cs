namespace AIBotBridge;

// Recognition can finish between device polls. Keep background assets out of
// that gap, then release only the matching terminal USB response's wire ACK.
internal sealed class Tab5VoiceTransportPriority(Func<long>? clock=null)
{
    private readonly object _gate=new();
    private string _voice="",_pendingNonce="";
    private long _seen;
    private bool _served=true;
    private long Now=>clock?.Invoke()??Environment.TickCount64;
    internal bool Active {get {lock(_gate)return !_served&&Now>=_seen&&Now-_seen<30000;}}
    internal void Observe(string op,Tab5VoiceReply reply,string transport,string nonce) {
        lock(_gate) {
            if(op=="start"&&reply.VoiceId.Length>0&&reply.State=="recording") {
                _voice=reply.VoiceId;_pendingNonce="";_served=false;
            }
            if(_served||reply.VoiceId.Length==0||reply.VoiceId!=_voice)return;
            _seen=Now;
            if(reply.State is not ("review" or "error" or "cancelled"))return;
            if(transport=="USB")_pendingNonce=nonce;
            else _served=true;
        }
    }
    internal void Acknowledge(string nonce) {
        lock(_gate)if(nonce.Length>0&&nonce==_pendingNonce){_served=true;_pendingNonce="";}
    }
}
