using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5VoicePreparationSelfTest
{
    internal static void Run(){
        int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new InvalidOperationException("VOICE_PREPARATION: "+why);}
        JsonElement Json(object v)=>JsonSerializer.SerializeToElement(v);
        var owner=Guid.NewGuid().ToString();long now=0;
        var start=Json(new{op="start",taskId=owner,client="claude"});
        using var preparation=new Tab5VoicePreparation(start,CancellationToken.None,()=>now);
        var first=preparation.Snapshot();
        Check(first.State=="recording"&&!first.Ready&&first.Source==""&&first.Text=="","legacy firmware prepares without capturing or showing an old draft");
        now=6000;preparation.Tick();Check(!preparation.Token.IsCancellationRequested,"slow initial setup is not mistaken for a lost poll before acknowledgment");
        preparation.Published();preparation.PermissionWaiting=true;
        Check(preparation.Snapshot().Message.Contains("权限"),"pending permission has a specific status");
        var poll=Json(new{op="poll",voiceId=first.VoiceId,client="claude"});
        Check(!preparation.Matches(Json(new{op="cancel",voiceId=first.VoiceId,client="codex"})),"another client cannot cancel");
        Check(!preparation.Matches(Json(new{op="cancel",voiceId="old",client="claude"})),"stale voice cannot cancel");
        now=10000;Check(preparation.Handle(poll).VoiceId==first.VoiceId,"same take survives permission polling");
        now=14999;preparation.Tick();Check(!preparation.Finished,"poll grants bounded liveness");
        now=15000;preparation.Tick();Check(preparation.Token.IsCancellationRequested&&preparation.Snapshot().State=="cancelled","disconnect stops pending preparation");
        preparation.Complete(first with{State="recording",Ready=true});
        Check(!preparation.Snapshot().Ready,"late completion cannot restart cancelled preparation");
        now=0;using var completed=new Tab5VoicePreparation(start,CancellationToken.None,()=>now);
        completed.Complete(new("other",owner,"dji","recording","ready","",true));
        Check(completed.Snapshot().VoiceId==completed.VoiceId&&completed.Snapshot().Ready,"completed start keeps the published identity");
        now=0;using var deadline=new Tab5VoicePreparation(start,CancellationToken.None,()=>now);
        for(now=1000;now<60000;now+=1000)deadline.Handle(Json(new{op="poll",voiceId=deadline.VoiceId,client="claude"}));
        deadline.Tick();Check(deadline.Finished&&deadline.Token.IsCancellationRequested,"polling cannot extend authorization beyond 60 seconds");
        using var cancelled=new Tab5VoicePreparation(start,CancellationToken.None);
        cancelled.Handle(Json(new{op="cancel",voiceId=cancelled.VoiceId,client="claude"}));
        Check(cancelled.Token.IsCancellationRequested,"device cancellation reaches the waiting operation");
        var policy=new Tab5VoiceStartupFocus.Policy();
        policy.Observe(Tab5VoiceStartupFocus.Owner.Previous,false);
        policy.Observe(Tab5VoiceStartupFocus.Owner.Previous,false);
        Check(!policy.Revoked,"pointer movement without key/button input preserves the explicit take");
        policy.Observe(Tab5VoiceStartupFocus.Owner.PendingPermission,false);
        Check(policy.Waiting&&!policy.Revoked&&!policy.SawPermission,"dimming window waits without claiming permission");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Permission,true);
        Check(policy.Waiting&&!policy.Revoked,"permission window is observed without activating over it");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Permission,true);
        policy.Observe(Tab5VoiceStartupFocus.Owner.Unavailable,false);
        Check(policy.Waiting&&!policy.Revoked,"temporary empty foreground after owned permission must wait");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Previous,true);
        Check(policy.Waiting&&!policy.Revoked,"permission click must be released before recovery");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Previous,false);
        Check(policy.SawPermission&&!policy.Waiting&&!policy.Revoked,"permission click may return to original application");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Draft,false);
        Check(!policy.Revoked,"owned editor can resume the same requested take");
        policy.Observe(Tab5VoiceStartupFocus.Owner.Other,false);
        Check(policy.Revoked,"unrelated editor revokes even without observed input");
        var input=new Tab5VoiceStartupFocus.Policy();input.Observe(Tab5VoiceStartupFocus.Owner.Previous,false);input.Observe(Tab5VoiceStartupFocus.Owner.Previous,true);
        Check(input.Revoked,"user editing original app is not a permission completion");
        var locked=new Tab5VoiceStartupFocus.Policy();locked.Observe(Tab5VoiceStartupFocus.Owner.Unavailable,false);
        Check(locked.Revoked,"unknown or secure desktop cannot be activated");
        var lockAfterPrompt=new Tab5VoiceStartupFocus.Policy();lockAfterPrompt.Observe(Tab5VoiceStartupFocus.Owner.Permission,false);
        for(int i=0;i<81;i++)lockAfterPrompt.Observe(Tab5VoiceStartupFocus.Owner.Unavailable,false);
        Check(lockAfterPrompt.Revoked,"missing foreground after permission cannot wait indefinitely");
        var unconfirmed=new Tab5VoiceStartupFocus.Policy();unconfirmed.Observe(Tab5VoiceStartupFocus.Owner.PendingPermission,false);unconfirmed.Observe(Tab5VoiceStartupFocus.Owner.Previous,false);
        Check(unconfirmed.Revoked,"unverified dimming window cannot authorize focus restoration");
        var dimClick=new Tab5VoiceStartupFocus.Policy();dimClick.Observe(Tab5VoiceStartupFocus.Owner.PendingPermission,true);
        Check(dimClick.Revoked,"input in unverified dimming window cannot authorize recovery");
        var dimTimeout=new Tab5VoiceStartupFocus.Policy();for(int i=0;i<81;i++)dimTimeout.Observe(Tab5VoiceStartupFocus.Owner.PendingPermission,false);
        Check(dimTimeout.Revoked,"unverified dimming window wait is bounded");
        Check(Tab5VoiceStartupFocus.PermissionProcess(@"C:\Windows\System32\RuntimeBroker.exe",@"C:\Windows"),"protected Windows permission host recognized");
        Check(!Tab5VoiceStartupFocus.PermissionProcess(@"C:\Temp\RuntimeBroker.exe",@"C:\Windows"),"lookalike executable not trusted");
        Check(!Tab5VoiceStartupFocus.PermissionProcess(@"C:\Windows\System32\notepad.exe",@"C:\Windows"),"ordinary application not a permission host");
        string picker=@"C:\Windows\System32\PickerHost.exe",win=@"C:\Windows";string[] names=["AIBotBridge"];
        Check(Tab5VoiceStartupFocus.OwnedMicrophonePicker(picker,win,"Shell_SystemDialog","允许 AIBotBridge 访问你的麦克风?",names),"observed system microphone prompt recognized");
        Check(!Tab5VoiceStartupFocus.OwnedMicrophonePicker(picker,win,"Shell_SystemDialog","允许 OtherApp 访问你的麦克风?",names),"another app microphone prompt not trusted");
        Check(!Tab5VoiceStartupFocus.OwnedMicrophonePicker(picker,win,"Shell_SystemDialog","允许 AIBotBridge 访问你的位置?",names),"another capability not treated as this authorization");
        Check(!Tab5VoiceStartupFocus.OwnedMicrophonePicker(picker,win,"OtherClass","允许 AIBotBridge 访问你的麦克风?",names),"unrelated picker window not trusted");
        Check(!Tab5VoiceStartupFocus.OwnedMicrophonePicker(@"C:\Temp\PickerHost.exe",win,"Shell_SystemDialog","允许 AIBotBridge 访问你的麦克风?",names),"untrusted picker executable rejected");
        using(var began=new ManualResetEventSlim())using(var finish=new ManualResetEventSlim())using(var cancel=new CancellationTokenSource()){
            var work=Tab5VoicePreparation.RunAudioAsync(()=>{began.Set();finish.Wait();return "opened";},cancel.Token);
            Check(began.Wait(TimeSpan.FromSeconds(3)),"audio open did not run in background");cancel.Cancel();
            Check(!work.IsCompleted,"cancel abandoned a still-running audio open");finish.Set();bool cancelledWork=false;
            try{work.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelledWork=true;}
            Check(cancelledWork,"cancelled audio preparation could continue to recognition");
        }
        Console.WriteLine($"TAB5_VOICE_PREPARATION_OK checks={checks} same_take; cancel; disconnect; permission; foreign_input; protected_identity (synthetic)");
    }
}
