namespace AIBotBridge;

internal static class Tab5VoiceFocusRecoverySelfTest
{
    internal static void Run() {
        static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException("Voice focus: "+message);}
        long now=1000;uint? input=10;bool safe=false,owns=true,interactive=true,activate=false;int calls=0;
        var recovery=new Tab5VoiceFocusRecovery(()=>safe,()=>owns,()=>interactive,()=>input,()=>{calls++;return safe=activate;},()=>now);
        recovery.Begin();
        Check(!recovery.TryRestore()&&recovery.Pending&&calls==1,"first asynchronous activation must yield, not terminate");
        now+=20;Check(!recovery.TryRestore()&&calls==1,"retry is rate limited independent of incoming PCM rate");
        now+=100;activate=true;
        Check(recovery.TryRestore()&&!recovery.Pending&&calls==2,"same live take recovers on a later UI tick");
        safe=false;activate=false;recovery.Begin();
        recovery.TryRestore();now+=Tab5VoiceFocusRecovery.TimeoutMs;
        Check(!recovery.TryRestore()&&!recovery.Pending&&recovery.Outcome=="timeout","persistent failure has a hard deadline");
        int before=calls;
        foreach(string reason in new[]{"input","secure","unknown","idle"}) {
            safe=false;owns=interactive=true;input=10;recovery.Begin();
            if(reason=="input")input=11;
            if(reason=="unknown")input=null;
            if(reason=="secure")interactive=false;
            if(reason=="idle")owns=false;
            Check(!recovery.TryRestore()&&!recovery.Pending&&calls==before,"must not reactivate after "+reason);
            if(reason!="idle")Check(!recovery.CanStopOwnedCapture,"unrelated input or desktop cannot be stopped");
        }
        owns=interactive=true;input=10;recovery.Begin();recovery.Finish();
        Check(!recovery.TryRestore()&&!recovery.CanStopOwnedCapture,"completed take cannot reclaim focus or stop a later take");
        Console.WriteLine("TAB5_VOICE_FOCUS_POLICY_OK asynchronous recovery; bounded/rate-limited; desktop input/secure/unknown/idle isolation; synthetic only");
    }
}
