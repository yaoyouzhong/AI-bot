namespace AIBotBridge;

internal static class Tab5VoiceFocusPreparationSelfTest
{
    internal static async Task RunAsync() {
        long now=0;int calls=0,selects=0;bool focused=true;
        Task Delay(int ms,CancellationToken token){token.ThrowIfCancellationRequested();now+=ms;return Task.CompletedTask;}
        async Task<bool> Run(Func<bool> focus,Func<bool> activate,CancellationToken token=default)=>
            await Tab5VoiceFocusPreparation.WaitAsync(focus,activate,()=>selects++,token,()=>now,Delay);
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("VOICE_FOCUS: "+name);}
        Check(await Run(()=>focused,()=>{calls++;return true;})&&now==400&&calls==0&&selects==0,"normal activation keeps 350ms stability");
        now=0;focused=false;
        Check(await Run(()=>focused,()=>{calls++;focused=true;return true;})&&now==450&&calls==1,"early fallback without 250ms polling delay");
        now=0;calls=0;focused=false;
        Check(await Run(()=>focused,()=>{calls++;if(now<200)return false;focused=true;return true;})&&now==600&&calls==4,"held input defers injection");
        now=0;calls=0;
        Check(!await Run(()=>false,()=>{calls++;return true;})&&now==2200&&calls==1,"failed activation never repeats injection");
        now=0;calls=0;
        Check(await Run(()=>now!=250,()=>{calls++;return true;})&&now==650&&calls==1,"focus loss resets stability interval");
        using var cancel=new CancellationTokenSource();cancel.Cancel();now=0;calls=0;
        try {await Run(()=>false,()=>{calls++;return true;},cancel.Token);throw new InvalidOperationException("cancellation ignored");}
        catch(OperationCanceledException){Check(calls==0,"cancelled preparation cannot activate");}
        Console.WriteLine("TAB5_VOICE_FOCUS_PREPARATION_OK guards=6 fallback=450ms stable=350ms (synthetic clock)");
    }
}
