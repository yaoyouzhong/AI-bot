namespace AIBotBridge;

internal static class Tab5VoiceFocusPreparation
{
    // Called only after an explicit recording request. Yield once for normal
    // activation, then use the guarded fallback without a separate 250ms delay.
    internal static async Task<bool> WaitAsync(Func<bool> focused,Func<bool> activate,Action select,
        CancellationToken token,Func<long>? clock=null,Func<int,CancellationToken,Task>? delay=null,
        Action<string,long>? observe=null)
    {
        clock??=()=>Environment.TickCount64;delay??=Task.Delay;
        long start=clock(),stable=-1;bool attempted=false,seenFocus=false;
        while(clock()-start<2200) {
            long resumeAt=clock();
            await delay(50,token);token.ThrowIfCancellationRequested();
            if(clock()-resumeAt>=100)observe?.Invoke("resume-delay",clock()-resumeAt);
            if(focused()) {
                if(!seenFocus){observe?.Invoke("first",clock()-start);seenFocus=true;}
                if(stable<0)stable=clock();
                if(clock()-stable>=350)return true;
            } else {
                stable=-1;
                if(!attempted) {
                    // A held key/secure desktop may defer the attempt; never
                    // repeat an actual Alt tap, including a partial injection.
                    long activationAt=clock();
                    attempted=activate();
                    observe?.Invoke("activate-call",clock()-activationAt);
                    if(attempted)observe?.Invoke("activation",clock()-start);
                }
                long selectionAt=clock();select();
                if(clock()-selectionAt>=50)observe?.Invoke("select-call",clock()-selectionAt);
            }
        }
        return false;
    }
}
