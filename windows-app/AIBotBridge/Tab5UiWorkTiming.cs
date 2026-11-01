namespace AIBotBridge;

// Fixed phase names and durations only. A single pending writer keeps tracing
// from blocking the UI thread or accumulating disk work during a slow drive.
internal sealed class Tab5UiWorkTiming(string name) : IDisposable
{
    private readonly long _started=Environment.TickCount64;
    private long _phaseStarted=Environment.TickCount64;
    private readonly System.Text.StringBuilder _phases=new();
    private static int _writing;
    private static readonly bool TraceTray= !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG"));
    internal void Step(string phase) {
        long now=Environment.TickCount64;
        _phases.Append(' ').Append(phase).Append('=').Append(now-_phaseStarted);
        _phaseStarted=now;
    }
    public void Dispose() {
        long total=Environment.TickCount64-_started;
        if((total<80&&!(TraceTray&&name=="tray"))||Interlocked.CompareExchange(ref _writing,1,0)!=0)return;
        string line=$"UI_WORK name={name} totalMs={total}{_phases}";
        _=Task.Run(()=>{try{Tab5LocalDiagnostics.Write(line);}finally{Volatile.Write(ref _writing,0);}});
    }
}
