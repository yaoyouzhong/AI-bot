namespace AIBotBridge;

internal sealed class Tab5BleStatusRecovery
{
    // TAB5 expires a partially assembled status frame after five seconds
    // without a write on the status characteristic.
    internal const int FragmentSilenceMs=5100;
    private const int MaximumRecoveries=2;
    private int _timeouts;
    private long _resumeAt;

    internal int TotalRecoveries {get;private set;}
    internal bool Waiting(long now)=>now<_resumeAt;

    internal bool TryRecover(long now,bool connected)
    {
        if(!connected||_timeouts>=MaximumRecoveries)return false;
        _timeouts++;
        TotalRecoveries++;
        _resumeAt=now+FragmentSilenceMs;
        return true;
    }

    internal void Confirmed()
    {
        _timeouts=0;
        _resumeAt=0;
    }
}
