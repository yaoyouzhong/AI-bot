namespace AIBotBridge;

// Native-client operation only. Platform adapters own exact identity, focus and
// original-client input; no CLI launch, permission decision or fallback lives here.
internal static class ClaudeNativeSend
{
    internal sealed record View(Guid Session,string Scope,string Target,string Text,bool Empty,bool Idle,bool CanSubmit);
    internal sealed record Calls(Func<View> Read,Func<string> Scope,Action<string> Type,Action Submit,
        Func<ClaudeHistory.Value> History,Func<bool> Authorized,Action Dispatch,
        Func<CancellationToken,Task> Wait,IReadOnlyList<string>? ImageDigests=null,Func<bool>? Current=null);
    internal sealed record Result(bool StagingAttempted,bool SubmitAttempted,Guid? User,string Error,bool Stopped=false)
    { internal bool Delivered=>User is not null; internal string? FailureStage {get;init;} }
    private static string FailureAt(Exception error,string phase)=>error is ClaudeNativeTerminal.ConsoleFailure
        || (error is IOException && error.Message is "native_attachment_picker_unconfirmed" or "native_attachment_picker_ambiguous"
            or "native_attachment_picker_changed" or "native_attachments_unconfirmed" or "native_image_changed"
            or "native_composer_changed" or "native_editor_focus_lost" or "native_input_unconfirmed" or "turn_changed") ? error.Message:phase;

    internal static async Task<Result> SendAsync(Calls calls,string text,CancellationToken token)
    {
        bool staging=false,submitted=false;
        string phase="preflight";
        string? submitFailure=null;
        try {
            token.ThrowIfCancellationRequested();
            if(!calls.Authorized())return new(false,false,null,"unauthorized");
            if(calls.Current?.Invoke()==false)return new(false,false,null,"turn_changed");
            var first=calls.Read();
            if(!first.Idle||!first.Empty||first.Text.Length!=0)return new(false,false,null,"desktop_draft_present_or_busy");
            var baseline=ClaudeNativeReceipt.Capture(calls.History(),first.Session,first.Scope,text,calls.ImageDigests);
            if(baseline.Limited)return new(false,false,null,"native_history_unconfirmed");
            bool Same(View view)=>view.Session==first.Session&&view.Scope==first.Scope&&view.Target==first.Target&&view.Idle&&calls.Current?.Invoke()!=false;
            var ready=calls.Read();
            if(!Same(ready)||!ready.Empty||ready.Text.Length!=0||calls.Scope()!=first.Scope||!calls.Authorized())return new(false,false,null,"composer_changed");
            token.ThrowIfCancellationRequested();
            calls.Dispatch(); // Persist request/draft before any native input.
            phase="stage_input";staging=true;calls.Type(text); // Exactly once; never repair by typing again.
            phase="input_readback";
            View? staged=null;
            for(int i=0;i<20;i++) {
                token.ThrowIfCancellationRequested();var view=calls.Read();
                if(!Same(view))return new(true,false,null,"native_session_changed");
                if(view.Text==text&&view.CanSubmit){staged=view;break;}
                // Rendering may lag input; retry observation only.
                if(i<19)await calls.Wait(token);
            }
            if(staged is null)return new(true,false,null,"native_input_unconfirmed");
            var final=calls.Read();
            if(!Same(final)||final.Text!=text||!final.CanSubmit||calls.Scope()!=first.Scope||!calls.Authorized())return new(true,false,null,"composer_changed");
            token.ThrowIfCancellationRequested();
            phase="submit";submitted=true;
            try{calls.Submit();}catch(Exception e) when(e is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or System.Windows.Automation.ElementNotAvailableException or System.Windows.Automation.ElementNotEnabledException) {
                // Native invocation may have succeeded before a provider error.
                submitFailure=FailureAt(e,"submit");
            }
            phase="receipt";
            for(int i=0;i<50;i++) {
                token.ThrowIfCancellationRequested();
                if(calls.Scope()!=first.Scope)return new(true,true,null,"native_session_changed");
                try {
                    var after=ClaudeNativeReceipt.Capture(calls.History(),first.Session,first.Scope,text,calls.ImageDigests);
                    if(after.Confirm(baseline) is {} user)return new(true,true,user,"");
                }catch(IOException){ /* Receipt observation can race a file append. */ }
                if(i<49)await calls.Wait(token);
            }
            return new(true,true,null,"delivery_unknown"){FailureStage=submitFailure??phase};
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException or OperationCanceledException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or System.Windows.Automation.ElementNotAvailableException or System.Windows.Automation.ElementNotEnabledException) {
            return new(staging,submitted,null,staging?"delivery_unknown":"native_session_unavailable"){
                FailureStage=FailureAt(e,phase)};
        }
    }
}
