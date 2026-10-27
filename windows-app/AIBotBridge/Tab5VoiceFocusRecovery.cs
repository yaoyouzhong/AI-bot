namespace AIBotBridge;

// A transient activation/TSF change is not a failed take. Yield to the UI loop,
// but never reclaim a take after new desktop input or a secure-desktop switch.
internal sealed class Tab5VoiceFocusRecovery(Func<bool> safe,Func<bool> ownsCapture,
    Func<bool> interactive,Func<uint?> inputStamp,Func<bool> restore,Func<long>? clock=null)
{
    internal const int TimeoutMs=1200;
    private readonly Func<long> _clock=clock??(()=>Environment.TickCount64);
    private uint? _inputAtStart;
    private long _lostAt=-1,_nextAttempt;
    private bool _begun;
    internal bool Pending {get;private set;}
    internal int Attempts {get;private set;}
    internal string Outcome {get;private set;}="not-started";
    internal bool CanStopOwnedCapture=>_begun&&interactive()&&_inputAtStart is {} stamp&&inputStamp()==stamp;
    internal void Begin() {
        _begun=true;_inputAtStart=inputStamp();_lostAt=-1;_nextAttempt=0;
        Pending=false;Attempts=0;Outcome="focused";
    }
    internal bool TryRestore() {
        Pending=false;
        if(!_begun){Outcome="not-started";return false;}
        if(!CanStopOwnedCapture){Outcome="desktop-input-or-unavailable";return false;}
        if(!ownsCapture()){Outcome="recognizer-not-recording";return false;}
        long now=_clock();
        if(_lostAt<0)_lostAt=now;
        if(now-_lostAt>=TimeoutMs){Outcome="timeout";return false;}
        if(now>=_nextAttempt) {
            _nextAttempt=now+100;Attempts++;
            // No synthetic keys, shortcut, IME restart or foreground-lock change.
            restore();
        }
        if(safe()&&ownsCapture()) {
            _lostAt=-1;_nextAttempt=0;Outcome="restored";return true;
        }
        Pending=true;Outcome="waiting";return false;
    }
    internal void ObserveSafeFocus() {
        if(Pending)Outcome="restored";
        Pending=false;_lostAt=-1;_nextAttempt=0;
    }
    internal void Finish(){_begun=false;Pending=false;}
}
