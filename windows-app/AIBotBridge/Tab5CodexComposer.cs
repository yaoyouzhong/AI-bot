using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace AIBotBridge;

// Staging never submits. Enter is a separate explicit device action, guarded
// by the staged target, current editor focus and one-use controller receipt.
// Accessibility identifies the main composer, preserves its current draft and
// verifies append-only staging. No staging path synthesizes Enter.
internal static class Tab5CodexComposer
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
    internal sealed record SubmitResult(bool Attempted,bool Submitted,string Error);
    internal sealed record EnterCalls(Func<bool> Ready,Func<uint> Press,Action Release,Func<CancellationToken,Task<bool>> Confirm);
    internal sealed record ClearResult(bool Cleared,int Reads,int Unavailable,string Last);
    internal static async Task<ClearResult> ConfirmClearAsync(Func<bool?> read,Func<CancellationToken,Task> wait,CancellationToken token) {
        long started=Environment.TickCount64;int unavailable=0,reads=0;string last="not_read";
        for(int attempt=0;attempt<40&&Environment.TickCount64-started<5000;attempt++) {
            token.ThrowIfCancellationRequested();reads++;
            try {
                bool? empty=read();last=empty is null?"editor_missing":empty.Value?"empty":"not_empty";
                if(empty==true)return new(true,reads,unavailable,last);
            }catch(Exception ex) when(IsUnavailable(ex)) {
                // Enter can replace Chromium's composer accessibility nodes.
                // Retry only observation with a newly located editor, never input.
                unavailable++;last=ex.GetType().Name;
            }
            if(attempt<39)await wait(token);
        }
        return new(false,reads,unavailable,last);
    }
    internal static async Task<SubmitResult> SendEnterAsync(EnterCalls calls,CancellationToken token) {
        if(!calls.Ready())return new(false,false,"composer_focus_unconfirmed");
        token.ThrowIfCancellationRequested();
        uint pressed;
        try {
            pressed=calls.Press();
            if(pressed==0)return new(false,false,"enter_rejected");
            if(pressed!=2) {calls.Release();return new(true,false,"enter_partial_check_desktop");}
        }catch(Exception ex) when(IsUnavailable(ex)) {return new(true,false,"submit_unconfirmed_check_desktop");}
        try {return await calls.Confirm(token)?new(true,true,""):new(true,false,"submit_unconfirmed_check_desktop");}
        catch(Exception ex) when(ex is OperationCanceledException||IsUnavailable(ex)) {return new(true,false,"submit_unconfirmed_check_desktop");}
    }
    internal static async Task<SubmitResult> SubmitAsync(string task,CancellationToken token) {
        long started=Environment.TickCount64;
        string confirmation="not_started";
        SubmitResult Finish(SubmitResult result) {
            Volatile.Write(ref _submitDiagnostic,$"{DateTime.Now:HH:mm:ss} {(result.Submitted?"enter_composer_cleared":result.Error)}; attempted={result.Attempted}; confirm={confirmation}; elapsed={Environment.TickCount64-started}ms");
            return result;
        }
        string? title=UniqueTitle(task);
        if(title is null||!await Tab5QuickConsole.OpenAsync(task,token))return Finish(new(false,false,"submit_target_unconfirmed"));
        try {
            var editor=FindEditor(title);
            if(editor is null||IsEmpty(editor))return Finish(new(false,false,"composer_empty_or_unavailable"));
            nint window=GetForegroundWindow();int[] identity=editor.GetRuntimeId();string current=Read(editor);
            editor.SetFocus();
            bool Ready()=>GetForegroundWindow()==window&&WindowMatches(window,title)&&
                !new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0)&&
                AutomationElement.FocusedElement is { } focus&&identity.SequenceEqual(focus.GetRuntimeId())&&Read(editor)==current;
            async Task<bool> Confirm(CancellationToken ct) {
                var result=await ConfirmClearAsync(()=>{var after=FindEditor(title);return after is null?null:IsEmpty(after);},
                    cancel=>Task.Delay(80,cancel),ct);
                confirmation=$"reads={result.Reads},unavailable={result.Unavailable},last={result.Last}";
                return result.Cleared;
            }
            return Finish(await SendEnterAsync(new(Ready,
                ()=>SendInput(2,[Enter(false),Enter(true)],Marshal.SizeOf<KeyboardInput>()),
                ()=>SendInput(1,[Enter(true)],Marshal.SizeOf<KeyboardInput>()),Confirm),token));
        }catch(Exception ex) when(IsUnavailable(ex)) {return Finish(new(false,false,"composer_unavailable"));}
    }
    // INPUT includes a 32-byte union on x64. Use the same native layout as
    // the activation helper; a KEYBDINPUT-only size is rejected by Windows.
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)]private struct InputUnion {
        [FieldOffset(0)]public KeyInput Keyboard;
        [FieldOffset(0)]public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]private struct KeyInput {public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    private static KeyboardInput Enter(bool up)=>new(){Type=1,Data=new(){Keyboard=new(){Key=0x0D,Flags=up?2u:0u}}};
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,KeyboardInput[] inputs,int size);
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
    internal sealed record StageResult(bool Attempted,bool Staged,string Error,bool Appended=false,bool? CaretAtEnd=null);
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
            var result=await ConfirmClearAsync(()=>{var after=calls.Read();return after is null?null:after.Text.Length==0;},calls.Wait,token);
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
            return editor is null?null:new(string.Join(',',editor.GetRuntimeId()),IsEmpty(editor)?"":Read(editor));
        }
        bool WriteDraft(DraftSnapshot before) {
            if(ReadDraft()!=before||new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))return false;
            var editor=FindEditor(title);
            if(editor is null||string.Join(',',editor.GetRuntimeId())!=before.Identity||
               !editor.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)||((ValuePattern)pattern).Current.IsReadOnly)return false;
            token.ThrowIfCancellationRequested();
            if(ReadDraft()!=before)return false;
            ((ValuePattern)pattern).SetValue("");return true;
        }
        return Finish(await ClearDraftAsync(new(ReadDraft,WriteDraft,ct=>Task.Delay(80,ct)),token));
    }
    internal sealed record StageCalls(Func<DraftSnapshot?> Read,Func<DraftSnapshot,string,bool> Write,
        Func<CancellationToken,Task> Wait,Func<DraftSnapshot,bool>? MoveCaretToEnd=null);
    internal static string AppendText(string existing,string text) {
        if(existing.Length==0||text.Length==0)return existing+text;
        int end=existing.Length;
        while(end>0&&char.IsWhiteSpace(existing[end-1]))end--;
        // Preserve explicit paragraph breaks and trailing spaces. A pause in
        // dictation is not itself a request for a new paragraph.
        if(end==0||existing.AsSpan(end).IndexOfAny('\r','\n')>=0||text[0] is '\r' or '\n')return existing+text;
        int punctuation=end-1;
        while(punctuation>0&&"\"'”’」』）》】)]}".Contains(existing[punctuation]))punctuation--;
        if("。！？；：，、.!?;:,…—–".Contains(existing[punctuation]))return existing+text;
        return existing.Insert(end,"。")+text;
    }
    internal static async Task<StageResult> AppendAsync(StageCalls calls,string text,CancellationToken token) {
        DraftSnapshot? previous=null;
        for(int attempt=0;attempt<35;attempt++) {
            token.ThrowIfCancellationRequested();
            DraftSnapshot? current;
            try {current=calls.Read();}
            catch(Exception ex) when(IsUnavailable(ex)){current=null;}
            if(current is not null&&current==previous) {
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
                        if(after is not null&&after.Text.TrimEnd('\r','\n')==combined.TrimEnd('\r','\n')) {
                            // The write is already confirmed. A selection failure
                            // must not make callers retry or append the take twice.
                            bool? caret=null;
                            if(calls.MoveCaretToEnd is not null) {
                                caret=false;
                                try {if(!token.IsCancellationRequested)caret=calls.MoveCaretToEnd(after);}
                                catch(Exception ex) when(IsUnavailable(ex)||ex is NotSupportedException){ }
                            }
                            return new(true,true,"",current.Text.Length>0,caret);
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
    internal static async Task<StageResult> StageWithResultAsync(string task,string text,CancellationToken token) {
        long started=Environment.TickCount64;
        string? title=UniqueTitle(task);
        // The accessibility document exposes the conversation title, not its ID.
        // Ambiguous titles cannot establish that navigation has finished.
        StageResult Finish(string reason,StageResult? result=null){
            result??=new(false,false,reason);Volatile.Write(ref _stageError,result.Staged?"":reason);
            Result(result.CaretAtEnd is null?reason:reason+"; caretEnd="+result.CaretAtEnd,started,result.Staged);return result;
        }
        if(title is null)return Finish("target_title_unconfirmed");
        if(!await Tab5QuickConsole.OpenAsync(task,token))return Finish("target_navigation_unconfirmed");
        try {
            DraftSnapshot Snapshot(AutomationElement editor)=>new(string.Join(',',editor.GetRuntimeId()),IsEmpty(editor)?"":Read(editor));
            DraftSnapshot? ReadDraft(){var editor=FindEditor(title);return editor is null?null:Snapshot(editor);}
            bool WriteDraft(DraftSnapshot before,string combined) {
                var editor=FindEditor(title);
                if(editor is null||Snapshot(editor)!=before||
                   new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))return false;
                if(!editor.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)||((ValuePattern)pattern).Current.IsReadOnly)return false;
                // Plain text can be appended without losing its content. Rich
                // inline objects cannot be round-tripped through ValuePattern.
                if(editor.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>()
                    .Any(child=>child.Current.ControlType!=ControlType.Text&&child.Current.ControlType!=ControlType.Group))return false;
                token.ThrowIfCancellationRequested();
                if(!ForegroundObserver.CodexVisible()||!WindowMatches(GetForegroundWindow(),title)||Snapshot(editor)!=before)return false;
                ((ValuePattern)pattern).SetValue(combined);return true;
            }
            bool MoveCaretToEnd(DraftSnapshot confirmed) {
                var editor=FindEditor(title);
                if(editor is null||Snapshot(editor)!=confirmed||
                   !editor.TryGetCurrentPattern(TextPattern.Pattern,out var pattern))return false;
                nint window=GetForegroundWindow();
                bool Unchanged()=>!token.IsCancellationRequested&&GetForegroundWindow()==window&&
                    WindowMatches(window,title)&&Snapshot(editor)==confirmed&&
                    !new[]{0x10,0x11,0x12,0x5B,0x5C,0x0D,0x01,0x02}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0);
                if(!Unchanged())return false;
                editor.SetFocus();
                if(!Unchanged()||AutomationElement.FocusedElement is not { } focus||
                    !focus.GetRuntimeId().SequenceEqual(editor.GetRuntimeId()))return false;
                var textPattern=(TextPattern)pattern;
                var end=textPattern.DocumentRange;
                end.MoveEndpointByRange(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End);
                if(!Unchanged())return false;
                // Select an empty range at the document end: no keystrokes,
                // text replacement, newline insertion or send action.
                end.Select();
                var selection=textPattern.GetSelection();
                return Unchanged()&&selection.Length==1&&
                    selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start,end,TextPatternRangeEndpoint.End)==0&&
                    selection[0].CompareEndpoints(TextPatternRangeEndpoint.End,end,TextPatternRangeEndpoint.End)==0;
            }
            var result=await AppendAsync(new(ReadDraft,WriteDraft,ct=>Task.Delay(80,ct),MoveCaretToEnd),text,token);
            return Finish(result.Staged?"draft_append_verified":result.Error,result);
        }catch(Exception ex) when(IsUnavailable(ex)) {return Finish("composer_unavailable");}
    }
    internal static bool IsComposerBodyClass(string classes)=>classes.Split(' ',StringSplitOptions.RemoveEmptyEntries)
        .Any(name=>name.StartsWith("_ComposerLayoutBody_",StringComparison.Ordinal));
    private static bool IsMainComposer(AutomationElement editor) {
        var parent=TreeWalker.RawViewWalker.GetParent(editor);
        for(int depth=0;depth<24&&parent is not null;depth++) {
            if(parent.Current.ClassName.Contains("@container/request-card",StringComparison.Ordinal))return false;
            if(IsComposerBodyClass(parent.Current.ClassName))return true;
            if(parent.Current.ControlType==ControlType.Document)break;
            parent=TreeWalker.RawViewWalker.GetParent(parent);
        }
        return false;
    }
    private static AutomationElement? FindEditor(string title) {
        if(!ForegroundObserver.CodexVisible()){Volatile.Write(ref _editorDiagnostic,"foreground_not_codex");return null;}
        var window=AutomationElement.FromHandle(GetForegroundWindow());
        // Chromium lazily materializes its accessibility subtree. A filtered
        // Edit query can return no controls until an unfiltered walk warms it.
        window.FindAll(TreeScope.Children,Condition.TrueCondition);
        var editors=window.FindAll(TreeScope.Descendants,Condition.TrueCondition)
            .Cast<AutomationElement>().Where(e=>e.Current.ControlType==ControlType.Edit&&HasClass(e.Current.ClassName,"ProseMirror")&&e.Current.IsEnabled&&!e.Current.IsOffscreen&&IsMainComposer(e)).ToArray();
        if(editors.Length!=1){Volatile.Write(ref _editorDiagnostic,$"main_editor_count={editors.Length}");return null;}
        var parent=editors[0];
        for(int depth=0;depth<16&&parent is not null;depth++) {
            if(parent.Current.ControlType==ControlType.Document) {
                bool matched=parent.Current.AutomationId=="RootWebArea"&&parent.Current.Name==title;
                Volatile.Write(ref _editorDiagnostic,matched?"main_editor_verified":"editor_document_mismatch");return matched?editors[0]:null;
            }
            parent=TreeWalker.ControlViewWalker.GetParent(parent);
        }
        Volatile.Write(ref _editorDiagnostic,"editor_document_missing");return null;
    }
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
