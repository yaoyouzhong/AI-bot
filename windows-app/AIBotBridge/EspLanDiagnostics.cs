namespace AIBotBridge;

// Bounded counters only: no request headers, pairing token, addresses or bodies.
internal static class EspLanDiagnostics
{
    private static readonly object Gate=new();
    private static long _requests,_authorized,_sent,_failed;
    private static int _lastBytes;
    private static DateTimeOffset? _lastRequest,_lastSent;
    internal static void Request(bool authorized) {lock(Gate){_requests++;if(authorized)_authorized++;_lastRequest=DateTimeOffset.UtcNow;}}
    internal static void Complete(bool sent,int bytes) {lock(Gate){if(sent){_sent++;_lastSent=DateTimeOffset.UtcNow;_lastBytes=bytes;}else _failed++;}}
    internal static object Snapshot() {lock(Gate)return new {requests=_requests,authorized=_authorized,sent=_sent,failed=_failed,lastBytes=_lastBytes,lastRequest=_lastRequest,lastSent=_lastSent};}
}
