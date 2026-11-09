using InputAvailability = AIBotBridge.Tab5DesktopActivation.InputAvailability;

namespace AIBotBridge;

internal static class ClaudeNativeFocusPreparation
{
    // A normal Windows desktop can briefly have no foreground window while
    // the bridge's voice editor closes. Wait without activating or typing.
    // A desktop that does not receive input, held input, or any user action
    // during the wait is a hard stop, not a reason to try again.
    internal static async Task<InputAvailability> WaitForForegroundAsync(Func<InputAvailability> observe,Func<long?> lastInput,long? baseline,
        Func<bool> authorized,CancellationToken token,Func<CancellationToken,Task>? wait=null,Func<bool>? targetReady=null)
    {
        for(int i=0;i<=6;i++){
            token.ThrowIfCancellationRequested();
            if(!authorized()||baseline is null)return InputAvailability.DesktopUnavailable;
            if(lastInput()!=baseline)return InputAvailability.InputActive;
            var state=observe();
            if(state==InputAvailability.Ready&&targetReady?.Invoke()==false)state=InputAvailability.ForegroundPending;
            if(state!=InputAvailability.ForegroundPending||i==6)return state;
            await (wait?.Invoke(token)??Task.Delay(50,token));
        }
        return InputAvailability.ForegroundPending;
    }

    internal static async Task<bool> RestoreAsync(Func<bool> minimized,Action restore,Func<bool> current,
        CancellationToken token,Func<CancellationToken,Task>? wait=null)
    {
        token.ThrowIfCancellationRequested();if(!current())return false;
        if(!minimized())return true;
        restore(); // One restore request; no keys, text or repeated activation.
        for(int i=0;i<40;i++){
            token.ThrowIfCancellationRequested();if(!current())return false;
            if(!minimized())return true;
            await (wait?.Invoke(token)??Task.Delay(50,token));
        }
        return false;
    }
    internal static async Task SelfTestAsync(){
        bool minimized=false,current=true;int restores=0,waits=0;
        async Task<bool> Run()=>await RestoreAsync(()=>minimized,()=>restores++,()=>current,CancellationToken.None,_=>{if(++waits==3)minimized=false;return Task.CompletedTask;});
        if(!await Run()||restores!=0)throw new Exception("Visible window must not be restored");
        minimized=true;if(!await Run()||restores!=1||waits!=3)throw new Exception("Minimized restore must settle once");
        minimized=true;current=false;if(await Run()||restores!=1)throw new Exception("Revoked identity must not restore");
        int attempts=0;bool ok=await RestoreAsync(()=>true,()=>attempts++,()=>true,CancellationToken.None,_=>Task.CompletedTask);
        if(ok||attempts!=1)throw new Exception("Stuck restore is bounded and not successful");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{await RestoreAsync(()=>true,()=>attempts++,()=>true,cancelled.Token);throw new Exception("Cancellation ignored");}
        catch(OperationCanceledException){if(attempts!=1)throw new Exception("Cancelled request restored a window");}
        Console.WriteLine("CLAUDE_NATIVE_WINDOW_RESTORE_OK normal minimized changed timeout cancelled (synthetic)");
        InputAvailability desktop=InputAvailability.ForegroundPending;
        long? input=5;
        bool authorized=true;
        int samples=0;
        Task<InputAvailability> Wait(Func<int,Task> step,CancellationToken cancellation=default)=>WaitForForegroundAsync(
            ()=>desktop,()=>input,5,()=>authorized,cancellation,_=>step(++samples));
        if(await Wait(n=>{if(n==2)desktop=InputAvailability.Ready;return Task.CompletedTask;})!=InputAvailability.Ready||samples!=2)
            throw new Exception("A brief foreground gap must recover in the same request");
        samples=0;desktop=InputAvailability.ForegroundPending;
        if(await Wait(_=>Task.CompletedTask)!=InputAvailability.ForegroundPending||samples!=6)
            throw new Exception("An absent foreground must time out");
        samples=0;
        if(await Wait(_=>{desktop=InputAvailability.Ready;input=6;return Task.CompletedTask;})!=InputAvailability.InputActive||samples!=1)
            throw new Exception("New desktop input must stop recovery even when the foreground is back");
        samples=0;input=5;desktop=InputAvailability.ForegroundPending;
        if(await Wait(_=>{desktop=InputAvailability.DesktopUnavailable;return Task.CompletedTask;})!=InputAvailability.DesktopUnavailable||samples!=1)
            throw new Exception("A protected or locked desktop must stop recovery");
        samples=0;desktop=InputAvailability.InputActive;
        if(await Wait(_=>Task.CompletedTask)!=InputAvailability.InputActive||samples!=0)
            throw new Exception("Held desktop input must not trigger a wait");
        samples=0;desktop=InputAvailability.ForegroundPending;
        if(await Wait(_=>{authorized=false;return Task.CompletedTask;})!=InputAvailability.DesktopUnavailable||samples!=1)
            throw new Exception("Revoked authorization must stop recovery");
        desktop=InputAvailability.Ready;samples=0;bool selectedWindow=false;
        if(await WaitForForegroundAsync(()=>desktop,()=>5,5,()=>true,default,
            _=>{selectedWindow=++samples==2;return Task.CompletedTask;},()=>selectedWindow)!=InputAvailability.Ready||samples!=2)
            throw new Exception("A visible VS Code window must wait for the exact foreground, without activating again");
        Console.WriteLine("CLAUDE_VSCODE_FOREGROUND_WAIT_OK gap timeout changed_input locked held revoked (synthetic)");
    }
}
