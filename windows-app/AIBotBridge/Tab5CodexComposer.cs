using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace AIBotBridge;

// Staging never submits. The native action is a separate explicit device click,
// bound to the confirmed target and exact text by a one-use controller receipt.
internal static partial class Tab5CodexComposer
{
    private static string _diagnostic="尚无草稿填入请求";
    private static string _submitDiagnostic="尚无快捷发送请求";
    private static string _clearDiagnostic="尚无清空请求";
    private static string _stageError="composer_unconfirmed";
    private static string _editorDiagnostic="not_checked";
    internal static string EditorDiagnostic=>Volatile.Read(ref _editorDiagnostic);
    internal static string StageError=>Volatile.Read(ref _stageError);
    internal static string Diagnostic=>Volatile.Read(ref _diagnostic);
    internal static string SubmitDiagnostic=>Volatile.Read(ref _submitDiagnostic);
    internal static string ClearDiagnostic=>Volatile.Read(ref _clearDiagnostic);
    // Observation only: never focus, write to, or submit the composer.
    internal static bool? ReadDraftEmpty(string task) {
        string? title=UniqueTitle(task);
        if(title is null)return null;
        try {
            nint window=GetForegroundWindow();
            var editor=FindEditor(title);
            if(editor is null)return null;
            bool empty=IsEmpty(editor);
            var after=FindEditor(title);
            if(GetForegroundWindow()!=window||after is null||!editor.GetRuntimeId().SequenceEqual(after.GetRuntimeId()))return null;
            return empty&&IsEmpty(after);
        }catch(Exception ex) when(IsUnavailable(ex)){return null;}
    }
    private static bool Result(string reason,long started,bool success=false) {
        Volatile.Write(ref _diagnostic,$"{DateTime.Now:HH:mm:ss} {reason}; elapsed={Environment.TickCount64-started}ms");return success;
    }
    internal sealed record SubmitResult(bool Attempted,bool Submitted,string Error,string TurnId="");
    internal sealed record ClearResult(bool Cleared,int Reads,int Unavailable,string Last);
    internal static async Task<ClearResult> ConfirmClearAsync(Func<bool?> read,Func<CancellationToken,Task> wait,CancellationToken token) {
        long started=Environment.TickCount64;int unavailable=0,reads=0;string last="not_read";
        for(int attempt=0;attempt<40&&Environment.TickCount64-started<5000;attempt++) {
            token.ThrowIfCancellationRequested();reads++;
            try {
                bool? empty=read();last=empty is null?"editor_missing":empty.Value?"empty":"not_empty";
                if(empty==true)return new(true,reads,unavailable,last);
            }catch(Exception ex) when(IsUnavailable(ex)) {
                // Native submission can replace Chromium's accessibility nodes.
                // Retry only observation with a newly located editor, never input.
                unavailable++;last=ex.GetType().Name;
            }
            if(attempt<39)await wait(token);
        }
        return new(false,reads,unavailable,last);
    }
    internal sealed record NativeCalls(Func<bool> Ready,Action Invoke,Func<CancellationToken,Task<bool>> Confirm);
    internal static async Task<SubmitResult> InvokeOnceAsync(NativeCalls calls,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if(!calls.Ready())return new(false,false,"composer_changed");
        // Even a provider exception can occur after the native action took effect.
        try {calls.Invoke();}
        catch(Exception ex) when(IsUnavailable(ex)){ /* Observe only; the native action may have been accepted. */ }
        try {return await calls.Confirm(token)?new(true,true,""):new(true,false,"submit_unconfirmed_check_desktop");}
        catch(Exception ex) when(ex is OperationCanceledException||IsUnavailable(ex)){return new(true,false,"submit_unconfirmed_check_desktop");}
    }
    internal static async Task<SubmitResult> SubmitAsync(string task,DraftSnapshot expected,CancellationToken token,
        Action<Tab5CodexDeliveryReceipt>? captureBaseline=null,bool requireReceipt=false,Func<string>? guard=null,
        Tab5CodexDeliveryReceipt? preparedBaseline=null,bool allowDesktopEdits=false) {
        long started=Environment.TickCount64;string confirmation="not_started",readiness="not_checked",receiptTurn="";
        SubmitResult Finish(SubmitResult result) {
            Volatile.Write(ref _submitDiagnostic,$"{DateTime.Now:HH:mm:ss} {(result.Submitted?"native_action_confirmed":result.Error)}; attempted={result.Attempted}; ready={readiness}; confirm={confirmation}; elapsed={Environment.TickCount64-started}ms");
            return result with {TurnId=receiptTurn};
        }
        string? title=UniqueTitle(task);
        if(title is null)return Finish(new(false,false,"target_title_missing_or_duplicate"));
        // Submission never navigates to a different conversation or adopts its draft.
        try {
            var target=FindTarget(title);
            var submission=SubmissionSnapshot(expected,target is null?null:TargetDraft(target),allowDesktopEdits);
            if(target is null||submission is null)
                return Finish(new(false,false,"staged_target_or_text_changed"));
            // The explicit device click confirms the current desktop text, once.
            // Freeze it here; the final readiness check must still match exactly.
            expected=submission;
            var button=FindAction(target);
            if(button is null||!button.Current.IsEnabled||!button.TryGetCurrentPattern(InvokePattern.Pattern,out var action))
                return Finish(new(false,false,"native_submit_unavailable"));
            nint window=GetForegroundWindow();int[] actionId=button.GetRuntimeId();string actionName=button.Current.Name;
            var baseline=target.Question?null:preparedBaseline??await Tab5CodexDeliveryReceipt.ReadAsync(task,expected.Text,token);
            if(requireReceipt&&!target.Question&&!HasBaseline(baseline,task,expected.Text))
                return Finish(new(false,false,"delivery_baseline_unavailable"));
            bool Ready() {
                if(guard is not null&&(readiness=guard()).Length>0)return false;
                readiness=SubmitReadiness(task,title,expected,window,actionId);
                if(readiness=="ready"&&baseline is {Local:not null,Server:not null})captureBaseline?.Invoke(baseline);
                return readiness=="ready";
            }
            async Task<bool> Confirm(CancellationToken ct) {
                if(requireReceipt&&!target.Question) {
                    if(baseline is null)return false;
                    var delivered=await Tab5CodexDeliveryReceipt.WaitForConfirmationAsync(baseline,
                        cancel=>Tab5CodexDeliveryReceipt.ReadAfterAsync(task,expected.Text,baseline,cancel),ct);
                    if(delivered is null){confirmation="receipt_unconfirmed";return false;}
                    confirmation=delivered.Confirm(baseline)!;receiptTurn=delivered.Turn;return true;
                }
                var result=await ConfirmClearAsync(()=> {
                    var after=Capture(task);if(after is null)return null;
                    if(!target.Question)return after.Identity==expected.Identity&&after.Text.Length==0;
                    if(after.Identity!=expected.Identity)return true;
                    var current=FindTarget(title);
                    return current is {Question:true}&&current.Key==expected.Identity&&after.Text==expected.Text&&
                        QuestionActionAcknowledged(actionName,QuestionInfo(current.Container)?.Signature.Action??"");
                },cancel=>Task.Delay(80,cancel),ct);
                confirmation=$"ui_reads={result.Reads},last={result.Last}";
                if(result.Cleared&&!requireReceipt)return true;
                if(baseline is null||target.Question)return false;
                var after=await Tab5CodexDeliveryReceipt.ReadAsync(task,expected.Text,ct);
                string? receipt=after?.Confirm(baseline);
                if(receipt is not null){confirmation=receipt;receiptTurn=after!.Turn;return true;}
                return false;
            }
            return Finish(await InvokeOnceAsync(new(Ready,()=>((InvokePattern)action).Invoke(),Confirm),token));
        }catch(OperationCanceledException){return Finish(new(false,false,"submit_cancelled_before_action"));}
        catch(Exception ex) when(IsUnavailable(ex)){return Finish(new(false,false,"composer_unavailable"));}
    }
    internal static bool HasBaseline(Tab5CodexDeliveryReceipt? baseline,string task,string text)=>
        baseline is {Local:not null,Server:not null}&&baseline.TaskId==task&&baseline.TextHash==Tab5CodexDeliveryReceipt.Digest(text);
    // Read the same guards used immediately before Invoke, without changing UI
    // state or logging the user's text, title, identities or queue contents.
    private static string SubmitReadiness(string task,string title,DraftSnapshot expected,nint window,int[] actionId) {
        if(GetForegroundWindow()!=window)return "foreground_changed";
        if(UniqueTitle(task)!=title)return "target_title_changed";
        var current=FindTarget(title);
        if(current is null||TargetDraft(current)!=expected)return "binding_changed:"+EditorDiagnostic;
        foreach(int key in new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02})
            if((GetAsyncKeyState(key)&0x8000)!=0)return "input_down_"+key.ToString("X2");
        if(current.Key!=expected.Identity)return "target_changed";
        var action=FindAction(current);
        if(action is null)return "action_unavailable";
        if(!action.Current.IsEnabled)return "action_disabled";
        if(!action.GetRuntimeId().SequenceEqual(actionId))return "action_replaced";
        if(GetForegroundWindow()!=window)return "foreground_changed_final";
        if(!WindowMatches(window,title))return "document_changed";
        return Read(current.Editor)==expected.Text?"ready":"text_changed";
    }
    internal static async Task<object> InspectSubmitReadinessAsync(string task,CancellationToken token) {
        string? title=UniqueTitle(task);
        if(title is null)return new{readiness="title_unavailable"};
        try {
            var target=FindTarget(title);var expected=Capture(task);
            if(target is null||expected is null)return new{readiness="target_unavailable",diagnostic=EditorDiagnostic};
            var action=FindAction(target);
            if(action is null)return new{readiness="action_unavailable"};
            nint window=GetForegroundWindow();int[] actionId=action.GetRuntimeId();
            bool textPattern=target.Editor.TryGetCurrentPattern(TextPattern.Pattern,out var pattern);
            bool focused=target.Editor.Current.HasKeyboardFocus;bool? caretAtEnd=null;
            if(textPattern) {
                var text=(TextPattern)pattern;var end=text.DocumentRange;var selection=text.GetSelection();
                caretAtEnd=selection.Length==1&&selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End)==0&&
                    selection[0].CompareEndpoints(TextPatternRangeEndpoint.End,end,TextPatternRangeEndpoint.End)==0;
            }
            var baseline=target.Question?null:await Tab5CodexDeliveryReceipt.ReadAsync(task,expected.Text,token);
            return new{readiness=SubmitReadiness(task,title,expected,window,actionId),textPattern,focused,caretAtEnd,
                baselineAvailable=baseline is not null,actionInvokable=action.TryGetCurrentPattern(InvokePattern.Pattern,out _)};
        }catch(Exception ex) when(IsUnavailable(ex)){return new{readiness="unavailable",error=ex.GetType().Name};}
    }
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    internal static bool HasClass(string classes,string name)=>classes.Split(' ',StringSplitOptions.RemoveEmptyEntries).Contains(name,StringComparer.Ordinal);
    internal static string? UniqueTitle(string task)=>UniqueTitle(task,Tab5CodexCatalog.ConfirmationCatalog());
    internal static string? UniqueTitle(string task,IReadOnlyList<Tab5CodexTask> catalog) {
        string? title=catalog.FirstOrDefault(t=>t.Id==task)?.IdentityTitle;
        return !string.IsNullOrWhiteSpace(title)&&catalog.Count(t=>t.IdentityTitle==title)==1?title:null;
    }
    internal static bool WindowMatches(nint handle,string title) {
        try {
            var window=AutomationElement.FromHandle(handle);
            var document=window.FindFirst(TreeScope.Descendants,new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document),
                new PropertyCondition(AutomationElement.AutomationIdProperty,"RootWebArea")));
            return document is not null&&document.Current.Name==title;
        }catch(Exception ex) when(IsUnavailable(ex)){return false;}
    }
    internal static string DraftUri(string task,string text)=>Tab5CodexDesktop.ThreadUri(task)+"?prompt="+Uri.EscapeDataString(text);
    internal sealed record DraftSnapshot(string Identity,string Text);
    internal static DraftSnapshot? SubmissionSnapshot(DraftSnapshot staged,DraftSnapshot? current,bool allowDesktopEdits)=>
        current is not null&&current.Identity==staged.Identity&&!string.IsNullOrWhiteSpace(current.Text)&&
        (allowDesktopEdits||current==staged)?current:null;
    internal sealed record StageResult(bool Attempted,bool Staged,string Error,bool Appended=false,bool? CaretAtEnd=null,DraftSnapshot? Binding=null);
    internal sealed record ClearDraftResult(bool Attempted,bool Cleared,string Error);
    internal sealed record ClearDraftCalls(Func<DraftSnapshot?> Read,Func<DraftSnapshot,bool> Write,
        Func<CancellationToken,Task> Wait);
    internal static async Task<ClearDraftResult> ClearDraftAsync(ClearDraftCalls calls,CancellationToken token) {
        token.ThrowIfCancellationRequested();DraftSnapshot? before;
        try {before=calls.Read();}
        catch(Exception ex) when(IsUnavailable(ex)){return new(false,false,"composer_unavailable");}
        if(before is null)return new(false,false,"clear_target_unconfirmed");
        if(before.Text.Length==0)return new(false,true,"");
        try {if(!calls.Write(before))return new(false,false,"composer_changed");}
        catch(Exception ex) when(IsUnavailable(ex)){return new(true,false,"clear_write_unconfirmed");}
        try {
            var result=await ConfirmClearAsync(()=>{var after=calls.Read();return after is null?null:after.Identity==before.Identity&&after.Text.Length==0;},calls.Wait,token);
            return new(true,result.Cleared,result.Cleared?"":"clear_readback_unconfirmed");
        }catch(OperationCanceledException){return new(true,false,"clear_readback_unconfirmed");}
    }
    internal static async Task<ClearDraftResult> ClearAsync(string task,CancellationToken token) {
        long started=Environment.TickCount64;string? title=UniqueTitle(task);
        ClearDraftResult Finish(ClearDraftResult result) {
            Volatile.Write(ref _clearDiagnostic,$"{DateTime.Now:HH:mm:ss} {(result.Cleared?"composer_clear_verified":result.Error)}; attempted={result.Attempted}; elapsed={Environment.TickCount64-started}ms");
            return result;
        }
        if(title is null)return Finish(new(false,false,"clear_target_unconfirmed"));
        DraftSnapshot? ReadDraft() {
            if(!ForegroundObserver.CodexVisible()||!WindowMatches(GetForegroundWindow(),title))return null;
            var editor=FindEditor(title);
            return editor is null?null:Capture(task);
        }
        bool WriteDraft(DraftSnapshot before) {
            if(ReadDraft()!=before||new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))return false;
            var editor=FindEditor(title);
            if(editor is null||Capture(task)!=before||
               !editor.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)||((ValuePattern)pattern).Current.IsReadOnly)return false;
            token.ThrowIfCancellationRequested();
            if(ReadDraft()!=before)return false;
            ((ValuePattern)pattern).SetValue("");return true;
        }
        return Finish(await ClearDraftAsync(new(ReadDraft,WriteDraft,ct=>Task.Delay(80,ct)),token));
    }
    internal sealed record StageCalls(Func<DraftSnapshot?> Read,Func<DraftSnapshot,string,bool> Write,
        Func<CancellationToken,Task> Wait,Func<DraftSnapshot,bool>? MoveCaretToEnd=null);
    internal sealed record CaretCalls(Func<bool> Ready,Func<bool> AtEnd,Func<bool> Focus,Action SelectEnd);
    internal static bool PositionCaretAtEnd(CaretCalls calls) {
        if(!calls.Ready())return false;
        // SetValue can already leave the selection at the end. Avoid a focus
        // mutation and its accessibility round trips when observation proves it.
        if(calls.AtEnd())return calls.Ready()&&calls.AtEnd();
        if(!calls.Focus()||!calls.Ready())return false;
        if(calls.AtEnd())return calls.Ready()&&calls.AtEnd();
        calls.SelectEnd();
        return calls.Ready()&&calls.AtEnd();
    }
    internal static string AppendText(string existing,string text) {
        if(existing.Length==0||text.Length==0||char.IsWhiteSpace(existing[^1])||char.IsWhiteSpace(text[0]))return existing+text;
        // Keep both takes verbatim. A pause does not imply a period or paragraph.
        return existing+" "+text;
    }
    internal static async Task<StageResult> AppendAsync(StageCalls calls,string text,CancellationToken token,bool requireEmpty=false,DraftSnapshot? initial=null) {
        DraftSnapshot? previous=null;
        for(int attempt=0;attempt<35;attempt++) {
            token.ThrowIfCancellationRequested();
            DraftSnapshot? current;
            try {current=attempt==0&&initial is not null?initial:calls.Read();}
            catch(Exception ex) when(IsUnavailable(ex)){current=null;}
            if(current is not null&&current==previous) {
                if(requireEmpty&&current.Text.Length>0)return new(false,false,"desktop_draft_present");
                string combined=AppendText(current.Text,text);
                if(System.Text.Encoding.UTF8.GetByteCount(combined)>32000)return new(false,false,"composer_too_long");
                // Write must recheck this exact snapshot immediately before the
                // synchronous UIA mutation. A changed draft is never overwritten.
                string unconfirmed="draft_readback_unconfirmed";
                try {if(!calls.Write(current,combined))return new(false,false,"composer_changed");}
                catch(Exception ex) when(IsUnavailable(ex)) {
                    // Chromium can invalidate the provider after SetValue has
                    // taken effect. Reconcile by reading a freshly found editor;
                    // an exception alone proves neither success nor failure.
                    unconfirmed="draft_write_unconfirmed";
                }
                for(int readback=0;readback<40;readback++) {
                    try {
                        var after=calls.Read();
                        if(after is not null&&after.Identity==current.Identity&&after.Text==combined) {
                            // The write is already confirmed. A selection failure
                            // must not make callers retry or append the take twice.
                            bool? caret=null;
                            if(calls.MoveCaretToEnd is not null) {
                                caret=false;
                                try {if(!token.IsCancellationRequested)caret=calls.MoveCaretToEnd(after);}
                                catch(Exception ex) when(IsUnavailable(ex)||ex is NotSupportedException){ }
                            }
                            return new(true,true,"",current.Text.Length>0,caret,after);
                        }
                    }catch(Exception ex) when(IsUnavailable(ex)){ }
                    // Once attempted, never write again, even if acknowledgement
                    // is delayed, the user edits, or the operation is cancelled.
                    try {await calls.Wait(token);}
                    catch(OperationCanceledException){return new(true,false,unconfirmed);}
                }
                return new(true,false,unconfirmed);
            }
            previous=current;await calls.Wait(token);
        }
        return new(false,false,"composer_not_ready");
    }
    internal static async Task<bool> StageAsync(string task,string text,CancellationToken token)=>
        (await StageWithResultAsync(task,text,token)).Staged;
    internal static Task<StageResult> StageWithResultAsync(string task,string text,CancellationToken token)=>StageBoundAsync(task,text,null,token);
    internal static async Task<StageResult> StageBoundAsync(string task,string text,string? expectedTarget,CancellationToken token,
        bool requireEmpty=false,Func<string>? guard=null,Action? dispatch=null,bool positionCaret=true) {
        long started=Environment.TickCount64;string writeReadiness="not_checked";
        string? title=UniqueTitle(task);
        // The accessibility document exposes the conversation title, not its ID.
        // Ambiguous titles cannot establish that navigation has finished.
        StageResult Finish(string reason,StageResult? result=null){
            result??=new(false,false,reason);Volatile.Write(ref _stageError,result.Staged?"":reason);
            Tab5VoiceTiming.Log("desktop-draft result="+reason+" writeReady="+writeReadiness+" attempted="+result.Attempted,started);
            Result((result.CaretAtEnd is null?reason:reason+"; caretEnd="+result.CaretAtEnd)+"; writeReady="+writeReadiness+"; attempted="+result.Attempted,started,result.Staged);return result;
        }
        if(title is null)return Finish("target_title_missing_or_duplicate");
        if(expectedTarget is null&&!await Tab5QuickConsole.OpenAsync(task,token))return Finish("target_navigation_unconfirmed");
        var initial=expectedTarget is null?null:Capture(task);
        if(expectedTarget is not null&&initial?.Identity!=expectedTarget)return Finish("voice_target_changed_preview_retained");
        try {
            DraftSnapshot? Snapshot(AutomationElement editor) {
                var current=FindTarget(title);
                if(current is null||!current.Editor.GetRuntimeId().SequenceEqual(editor.GetRuntimeId())||
                    expectedTarget is not null&&current.Key!=expectedTarget)return null;
                return new(current.Key,IsEmpty(editor)?"":Read(editor));
            }
            // One fresh target scan per observation. AppendAsync still requires
            // two equal snapshots, and WriteDraft independently rechecks the
            // exact editor, target and text immediately before its single write.
            DraftSnapshot? ReadDraft()=>ReadTargetDraft(title,expectedTarget);
            bool WriteDraft(DraftSnapshot before,string combined) {
                long phaseStarted=Environment.TickCount64;
                var target=FindTarget(title);
                writeReadiness=DraftWriteReadiness(title,expectedTarget,before,target,out var writable);
                Tab5VoiceTiming.Log("desktop-draft-write-check result="+writeReadiness,phaseStarted);
                if(writeReadiness!="ready")return false;
                if(requireEmpty&&before.Text.Length>0)return false;
                if(guard is not null&&(writeReadiness=guard()).Length>0)return false;
                if(requireEmpty&&ReadTargetDraft(title,expectedTarget)!=before)return false;
                token.ThrowIfCancellationRequested();
                dispatch?.Invoke();
                phaseStarted=Environment.TickCount64;
                writable!.SetValue(combined);
                Tab5VoiceTiming.Log("desktop-draft-set-value",phaseStarted);return true;
            }
            bool MoveCaretToEnd(DraftSnapshot confirmed) {
                // Reuse this fresh discovery for the initial observation, as in
                // ReadTargetDraft. Later pre-action checks still rescan live.
                var target=FindTarget(title);var editor=target?.Editor;
                if(editor is null||TargetDraft(target!)!=confirmed||
                   !editor.TryGetCurrentPattern(TextPattern.Pattern,out var pattern))return false;
                nint window=GetForegroundWindow();
                bool Unchanged()=>!token.IsCancellationRequested&&GetForegroundWindow()==window&&
                    WindowMatches(window,title)&&Snapshot(editor)==confirmed&&
                    !new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0);
                var textPattern=(TextPattern)pattern;
                bool AtEnd() {
                    var end=textPattern.DocumentRange;var selection=textPattern.GetSelection();
                    return selection.Length==1&&
                        selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End)==0&&
                        selection[0].CompareEndpoints(TextPatternRangeEndpoint.End,end,TextPatternRangeEndpoint.End)==0;
                }
                bool Focus() {
                    editor.SetFocus();
                    return AutomationElement.FocusedElement is { } focus&&focus.GetRuntimeId().SequenceEqual(editor.GetRuntimeId());
                }
                void SelectEnd() {
                    var end=textPattern.DocumentRange;
                    end.MoveEndpointByRange(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End);
                    // Empty selection only: never keys, text replacement or send.
                    end.Select();
                }
                return PositionCaretAtEnd(new(Unchanged,AtEnd,Focus,SelectEnd));
            }
            bool TimedCaret(DraftSnapshot confirmed) {
                long phaseStarted=Environment.TickCount64;
                try{return MoveCaretToEnd(confirmed);}
                finally{Tab5VoiceTiming.Log("desktop-draft-caret",phaseStarted);}
            }
            // The immediately preceding binding read is the first observation.
            // Still wait, reread live, and revalidate before the single write.
            var result=await AppendAsync(new(ReadDraft,WriteDraft,ct=>Task.Delay(80,ct),positionCaret?TimedCaret:null),text,token,requireEmpty,initial);
            if(!result.Attempted&&!result.Staged&&guard is not null&&writeReadiness.Length>0&&writeReadiness!="ready")
                result=result with {Error=writeReadiness};
            return Finish(result.Staged?"draft_append_verified":result.Error,result);
        }catch(Exception ex) when(IsUnavailable(ex)) {return Finish("composer_unavailable");}
    }
    private static string DraftWriteReadiness(string title,string? expectedTarget,DraftSnapshot before,Target? initial,out ValuePattern? writable) {
        writable=null;
        var editor=initial?.Editor;
        if(editor is null)return "editor_unavailable:"+EditorDiagnostic;
        DraftSnapshot? Snapshot() {
            var current=FindTarget(title);
            if(current is null||!current.Editor.GetRuntimeId().SequenceEqual(editor.GetRuntimeId())||
                expectedTarget is not null&&current.Key!=expectedTarget)return null;
            return new(current.Key,IsEmpty(editor)?"":Read(editor));
        }
        // The caller just discovered this exact target. Reuse that observation
        // for the initial check; retain the independent final scan before writing.
        if(expectedTarget is not null&&initial!.Key!=expectedTarget||TargetDraft(initial!)!=before)return "snapshot_changed_initial:"+EditorDiagnostic;
        foreach(int key in new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02})
            if((GetAsyncKeyState(key)&0x8000)!=0)return "input_down_"+key.ToString("X2");
        if(!editor.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern))return "value_pattern_unavailable";
        if(((ValuePattern)pattern).Current.IsReadOnly)return "editor_read_only";
        writable=(ValuePattern)pattern;
        // Do not round-trip rich inline objects through plain ValuePattern text.
        foreach(AutomationElement child in editor.FindAll(TreeScope.Descendants,Condition.TrueCondition))
            if(child.Current.ControlType!=ControlType.Text&&child.Current.ControlType!=ControlType.Group)
                return "inline_object_"+child.Current.ControlType.Id;
        if(!ForegroundObserver.CodexVisible())return "foreground_not_codex";
        if(!WindowMatches(GetForegroundWindow(),title))return "document_changed";
        return Snapshot()==before?"ready":"snapshot_changed_final:"+EditorDiagnostic;
    }
    internal static object InspectDraftWriteReadiness(string task) {
        string? title=UniqueTitle(task);
        if(title is null)return new{writeReady="title_unavailable"};
        try {
            var target=FindTarget(title);
            if(target is null)return new{writeReady="target_unavailable",diagnostic=EditorDiagnostic};
            var before=TargetDraft(target);
            return new{writeReady=DraftWriteReadiness(title,target.Key,before,target,out _),characters=before.Text.Length,
                lineBreaks=before.Text.Count(c=>c=='\n'),hasFocus=target.Editor.Current.HasKeyboardFocus};
        }catch(Exception ex) when(IsUnavailable(ex)){return new{writeReady="unavailable",error=ex.GetType().Name};}
    }
    internal static bool IsComposerBodyClass(string classes)=>classes.Split(' ',StringSplitOptions.RemoveEmptyEntries)
        .Any(name=>name.StartsWith("_ComposerLayoutBody_",StringComparison.Ordinal));
    private static AutomationElement? FindEditor(string title)=>FindTarget(title)?.Editor;
    private static bool IsEmpty(AutomationElement editor) {
        string value=Read(editor);
        if(string.IsNullOrWhiteSpace(value))return true;
        // Chromium includes the non-editable placeholder in ValuePattern. Only
        // exclude it when the raw tree proves it is a placeholder decoration;
        // typing the same words as the hint must still count as a real draft.
        if(value.Trim()!=editor.Current.Name)return false;
        bool placeholder=false;
        foreach(AutomationElement leaf in editor.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text))) {
            if(HasClass(leaf.Current.ClassName,"ProseMirror-trailingBreak"))continue;
            bool decorated=false;var parent=TreeWalker.RawViewWalker.GetParent(leaf);
            for(int depth=0;depth<8&&parent is not null;depth++) {
                if(parent.GetRuntimeId().SequenceEqual(editor.GetRuntimeId()))break;
                if(HasClass(parent.Current.ClassName,"placeholder")){decorated=true;break;}
                parent=TreeWalker.RawViewWalker.GetParent(parent);
            }
            if(!decorated||leaf.Current.Name!=editor.Current.Name)return false;
            placeholder=true;
        }
        return placeholder;
    }
    private static string Read(AutomationElement editor)=>
        editor.TryGetCurrentPattern(ValuePattern.Pattern,out var value)?((ValuePattern)value).Current.Value:
        throw new InvalidOperationException("Composer is not readable");
    private static bool IsUnavailable(Exception ex)=>ex is ElementNotAvailableException or InvalidOperationException or COMException or System.ComponentModel.Win32Exception;
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
}
