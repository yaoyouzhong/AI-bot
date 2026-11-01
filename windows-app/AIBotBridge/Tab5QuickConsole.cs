using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AIBotBridge;

// Only explicit, authenticated device clicks enter this controller.
internal sealed class Tab5QuickConsole
{
    private static string _navigationDiagnostic="尚无窗口唤起请求";
    internal static string NavigationDiagnostic=>Volatile.Read(ref _navigationDiagnostic);
    private readonly Func<IReadOnlyList<Tab5CodexTask>> _catalog;
    private readonly Func<string,CancellationToken,Task<bool>> _open;
    private readonly Func<string,string,string?,CancellationToken,Task<Tab5CodexComposer.StageResult>> _stage;
    private readonly Func<string,Tab5CodexComposer.DraftSnapshot,CancellationToken,Task<Tab5CodexComposer.SubmitResult>> _submit;
    private readonly Func<string,CancellationToken,Task<Tab5CodexComposer.ClearDraftResult>> _clear;
    private readonly Func<string,bool?> _readDraftEmpty;
    private readonly Func<bool> _desktopInteractive;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly Dictionary<string,(int Status,object Body)> _receipts=[];
    private readonly Dictionary<string,(long At,string Target)> _requests=[];
    private sealed record Draft(long At,string Device,string Task,Tab5CodexComposer.DraftSnapshot Binding,bool Consumed=false);
    private sealed record VoiceTarget(string Task,string VoiceId,string Target,long At,string Text="",bool Ready=false,string Staged="");
    private readonly Dictionary<string,Draft> _drafts=[];
    private readonly Dictionary<string,VoiceTarget> _voices=[];
    private readonly object _voiceGate=new();
    private readonly Func<string,Tab5CodexComposer.DraftSnapshot?> _capture;
    private readonly Tab5QuickConsoleJournal _journal;
    private readonly bool _requireVoiceTarget;
    internal string? PrepareVoice(string task)=>task==Tab5VoiceSession.AgendaOwner?null:_capture(task)?.Identity??"unavailable";
    internal void ObserveVoice(string device,Tab5VoiceReply reply,string? prepared=null) {
        if(reply.TaskId==Tab5VoiceSession.AgendaOwner)return;
        lock(_voiceGate) {
            long now=Environment.TickCount64;
            foreach(var old in _voices.Where(p=>now-p.Value.At>900000).Select(p=>p.Key).ToArray())_voices.Remove(old);
            if(prepared is not null&&reply.State=="recording") {
                if(_voices.Count>=128)_voices.Remove(_voices.MinBy(p=>p.Value.At).Key);
                _voices[device]=new(reply.TaskId,reply.VoiceId,prepared,now);
            }
            if(_voices.TryGetValue(device,out var voice)&&voice.VoiceId==reply.VoiceId&&voice.Task==reply.TaskId)
                _voices[device]=voice with {Text=reply.Text,Ready=reply.State=="review"&&reply.CanInsert};
        }
    }
    internal Tab5QuickConsole(Func<IReadOnlyList<Tab5CodexTask>>? catalog=null,
        Func<string,CancellationToken,Task<bool>>? open=null,Func<string,string,CancellationToken,Task<Tab5CodexComposer.StageResult>>? stage=null,
        Func<string,Tab5CodexComposer.DraftSnapshot,CancellationToken,Task<Tab5CodexComposer.SubmitResult>>? submit=null,
        Func<string,CancellationToken,Task<Tab5CodexComposer.ClearDraftResult>>? clear=null,Func<string,bool?>? readDraftEmpty=null,
        Func<bool>? desktopInteractive=null,Func<string,Tab5CodexComposer.DraftSnapshot?>? capture=null,Tab5QuickConsoleJournal? journal=null,
        Func<string,string,string?,CancellationToken,Task<Tab5CodexComposer.StageResult>>? stageBound=null,bool requireVoiceTarget=true) {
        _catalog=catalog??Tab5CodexCatalog.Recent;_open=open??OpenAsync;
        _stage=stageBound??(stage is null?(task,text,target,ct)=>Tab5CodexComposer.StageBoundAsync(task,text,target,ct):(task,text,target,ct)=>stage(task,text,ct));
        _capture=capture??Tab5CodexComposer.Capture;_journal=journal??Tab5QuickConsoleJournal.Shared;_requireVoiceTarget=requireVoiceTarget;
        _submit=submit??((task,binding,ct)=>Tab5CodexComposer.SubmitAsync(task,binding,ct,allowDesktopEdits:true));
        _clear=clear??Tab5CodexComposer.ClearAsync;
        _readDraftEmpty=readDraftEmpty??Tab5CodexComposer.ReadDraftEmpty;
        _desktopInteractive=desktopInteractive??Tab5DesktopActivation.IsInteractive;
    }
    internal static Tab5CodexTask? Select(IReadOnlyList<Tab5CodexTask> tasks,string id)=>
        id.Length==0?tasks.MaxBy(t=>t.UpdatedAt):tasks.FirstOrDefault(t=>t.Id==id);
    internal async Task<(int Status,object Body)> HandleAsync(JsonElement request,CancellationToken token,string device="") {
        string op=Tab5CodexDesktop.Text(request,"op"),text=Tab5CodexDesktop.Text(request,"text");
        string draftRequest=Tab5CodexDesktop.Text(request,"draftRequestId");
        if(op is not ("open-recent" or "stage-draft" or "submit-draft" or "clear-draft")||
            !Guid.TryParseExact(Tab5CodexDesktop.Text(request,"requestId"),"D",out var id))return(400,new{error="invalid_operation"});
        if(op=="stage-draft"&&(string.IsNullOrWhiteSpace(text)||System.Text.Encoding.UTF8.GetByteCount(text)>2000))return(400,new{error="invalid_draft"});
        if(!await _gate.WaitAsync(0,token))return(409,new{error="desktop_busy"});
        try {
            string requestId=id.ToString();
            string receiptKey=device+":"+requestId;
            string target=Tab5CodexDesktop.Text(request,"taskId");
            if(target.Length>0&&!Guid.TryParseExact(target,"D",out _))return(400,new{error="invalid_task"});
            if(op=="stage-draft"&&target.Length==0)return(400,new{error="missing_draft_target"});
            if(op=="clear-draft"&&target.Length==0)return(400,new{error="missing_draft_target"});
            if(op=="submit-draft"&&(target.Length==0||!Guid.TryParseExact(draftRequest,"D",out _)))return(400,new{error="missing_staged_draft"});
            string fingerprint=device+":"+op+":"+target+":"+draftRequest+":"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
            long now=Environment.TickCount64;
            foreach(var old in _requests.Where(p=>now-p.Value.At>105000).Select(p=>p.Key).ToArray()) {_requests.Remove(old);_receipts.Remove(old);}
            foreach(var old in _drafts.Where(p=>now-p.Value.At>900000).Select(p=>p.Key).ToArray())_drafts.Remove(old);
            if(_receipts.TryGetValue(receiptKey,out var previous))return _requests[receiptKey].Target==fingerprint?previous:(409,new{error="request_conflict"});
            if(op=="submit-draft"&&_journal.Find(device,requestId) is {} persisted)
                return persisted.Fingerprint==fingerprint?(persisted.Status,persisted.Body):(409,new{error="request_conflict"});
            var task=Select(_catalog(),target);
            if(task is null)return(404,new{error="no_recent_task"});
            if(_receipts.Count>=128)return(429,new{error="desktop_request_limit"});
            if(op=="clear-draft") {
                _requests[receiptKey]=(now,fingerprint);
                _receipts[receiptKey]=(409,new{error="clear_unconfirmed",attempted=true});
                var cleared=await _clear(task.Id,token);
                if(cleared.Attempted||cleared.Cleared)
                    foreach(var old in _drafts.Where(p=>p.Value.Device==device&&p.Value.Task==task.Id).Select(p=>p.Key).ToArray())_drafts.Remove(old);
                var receipt=(Status:cleared.Cleared?200:409,Body:(object)new{taskId=task.Id,cleared=cleared.Cleared,attempted=cleared.Attempted,error=cleared.Error});
                _receipts[receiptKey]=receipt;return receipt;
            }
            if(op=="submit-draft") {
                if(!_drafts.TryGetValue(device+":"+draftRequest,out var draft)||draft.Device!=device||draft.Task!=task.Id||draft.Consumed||_journal.Used(device,draftRequest))
                    return(409,new{error="staged_draft_expired_or_used"});
                _drafts[device+":"+draftRequest]=draft with {Consumed=true};
                // Reserve before any input. A lost reply or a new request ID
                // must never invoke a native action again for the same staged draft.
                _requests[receiptKey]=(now,fingerprint);
                _receipts[receiptKey]=(409,new{error="submit_unconfirmed",attempted=true});
                _journal.Save(device,requestId,fingerprint,draftRequest,409,new{taskId=task.Id,submitted=false,attempted=true,error="submit_unconfirmed_check_desktop"});
                var sent=ContextualSubmitResult(await _submit(task.Id,draft.Binding,token),draft.Binding);
                // The composer freezes the latest text only on the original target.
                // This ticket stays consumed even if delivery is unknown.
                var receipt=(Status:sent.Submitted?200:409,Body:(object)new{taskId=task.Id,submitted=sent.Submitted,attempted=sent.Attempted,error=sent.Error});
                _receipts[receiptKey]=receipt;
                _journal.Save(device,requestId,fingerprint,draftRequest,receipt.Status,receipt.Body);return receipt;
            }
            // Reserve the request before opening the URI. A lost response cannot
            // resolve a different recent conversation on replay.
            _receipts[receiptKey]=(409,new{error="desktop_open_unconfirmed"});
            _requests[receiptKey]=(now,fingerprint);
            if(op=="open-recent"&&!_desktopInteractive()) {
                var blocked=(Status:409,Body:(object)new{error="desktop_not_interactive",foreground=false,taskId=task.Id});
                _receipts[receiptKey]=blocked;return blocked;
            }
            VoiceTarget? voice=null;
            if(op=="stage-draft")lock(_voiceGate)_voices.TryGetValue(device,out voice);
            if(op=="stage-draft"&&_requireVoiceTarget&&voice is null) {
                var rejected=(Status:409,Body:(object)new{error="voice_target_unavailable_preview_retained",staged=false});_receipts[receiptKey]=rejected;return rejected;
            }
            if(voice is not null&&(voice.Task!=task.Id||!voice.Ready||voice.Text!=text||voice.Staged.Length>0||Environment.TickCount64-voice.At>900000)) {
                var rejected=(Status:409,Body:(object)new{error="voice_preview_changed_or_used",staged=false});_receipts[receiptKey]=rejected;return rejected;
            }
            var stageResult=op=="stage-draft"?await _stage(task.Id,text,voice?.Target,token):null;
            if(voice is not null&&stageResult?.Attempted==true)lock(_voiceGate) {
                if(_voices.GetValueOrDefault(device)==voice)_voices[device]=voice with {Staged=requestId};
            }
            bool staged=stageResult?.Staged==true;
            if(staged&&stageResult!.Binding is not null) {
                // A new recording replaces the prior submission authority for
                // this device/chat, even when desktop editing is allowed.
                foreach(var old in _drafts.Where(p=>p.Value.Device==device&&p.Value.Task==task.Id).Select(p=>p.Key).ToArray())_drafts.Remove(old);
                if(_drafts.Count>=128)_drafts.Remove(_drafts.MinBy(p=>p.Value.At).Key);
                _drafts[receiptKey]=new(now,device,task.Id,stageResult.Binding);
            }
            bool foreground=op=="stage-draft"?staged:await _open(task.Id,token);
            bool? draftEmpty=op=="open-recent"&&foreground?_readDraftEmpty(task.Id):null;
            if(draftEmpty==true)
                foreach(var old in _drafts.Where(p=>p.Value.Device==device&&p.Value.Task==task.Id).Select(p=>p.Key).ToArray())_drafts.Remove(old);
            var result=(Status:foreground?200:409,Body:(object)new {
                taskId=task.Id,title=task.Title,projectId=task.ProjectId,folder=task.Folder,updatedAt=task.UpdatedAt,
                selection=target.Length==0?"updated":"selected",
                foreground,staged,appended=staged&&stageResult!.Appended,draftEmpty,
                error=foreground?"":op=="stage-draft"?stageResult!.Error:
                    !_desktopInteractive()?"desktop_not_interactive":"desktop_foreground_unconfirmed"
            });
            _receipts[receiptKey]=result;return result;
        }catch(Exception ex) when(ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) {
            return(503,new{error="desktop_unavailable"});
        }finally{_gate.Release();}
    }
    internal static Tab5CodexComposer.SubmitResult ContextualSubmitResult(Tab5CodexComposer.SubmitResult result,Tab5CodexComposer.DraftSnapshot binding)=>
        !result.Attempted&&!result.Submitted&&result.Error is "staged_target_or_text_changed" or "composer_changed"
            ?result with{Error=binding.Identity.StartsWith("question:",StringComparison.Ordinal)?"question_target_or_text_changed":"conversation_target_or_text_changed"}
            :result;
    internal static async Task<bool> OpenAsync(string id,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if(!Tab5DesktopActivation.IsInteractive())return false;
        long started=Environment.TickCount64;
        string? title=Tab5CodexComposer.UniqueTitle(id);
        bool tapped=false,inputBlocked=false,partialTap=false,foreground=false,target=false;
        int windowCount=0;bool fallbackUsed=false;
        bool Result(string reason,bool success=false) {
            if(success)Tab5CodexCatalog.Touch(id);
            Volatile.Write(ref _navigationDiagnostic,$"{DateTime.Now:HH:mm:ss} {reason}; elapsed={Environment.TickCount64-started}ms; windows={windowCount}; foreground={foreground}; target={target}; altTap={tapped}; inputBlocked={inputBlocked}; partialTap={partialTap}");
            return success;
        }
        void Raise(nint window) {
            if(IsIconic(window))ShowWindowAsync(window,9);
            var result=Tab5DesktopActivation.Raise(window,!fallbackUsed);
            fallbackUsed=true;
            tapped|=result.Tapped;inputBlocked|=result.InputBlocked;partialTap|=result.PartialTap;
        }
        bool Confirm() {
            if(!Tab5DesktopActivation.IsInteractive())return false;
            foreground=ForegroundObserver.CodexVisible();
            // Opening the explicit task URI remains available with duplicate
            // titles. Composer reads/writes/submission still require UniqueTitle.
            target=foreground&&(title is null||Tab5CodexComposer.WindowMatches(GetForegroundWindow(),title));
            return foreground&&target&&!partialTap;
        }
        var windows=DesktopWindows().ToArray();windowCount=windows.Length;
        // The voice draft window can be foreground while the target Codex
        // conversation is already open. Raise that window without navigating.
        if(title is not null)foreach(var window in windows) {
            if(!Tab5CodexComposer.WindowMatches(window,title))continue;
            Raise(window);
            if(Confirm())return Result("existing_target_verified",true);
        }
        if(!Tab5DesktopActivation.IsInteractive())return Result("desktop_not_interactive");
        using(var process=Process.Start(new ProcessStartInfo(Tab5CodexDesktop.ThreadUri(id)){UseShellExecute=true})) { }
        int attempts=windows.Length==0?100:40;
        for(int attempt=0;attempt<attempts;attempt++) {
            if(!Tab5DesktopActivation.IsInteractive())return Result("desktop_not_interactive");
            if(Confirm())return Result("navigated_target_verified",true);
            if(attempt%10==0){windows=DesktopWindows().ToArray();windowCount=windows.Length;}
            foreach(var window in windows) {
                // A single Codex window is the URI destination. With several
                // windows, activate only the one that exposes the target title.
                if(windows.Length!=1&&(title is null||!Tab5CodexComposer.WindowMatches(window,title)))continue;
                Raise(window);
            }
            await Task.Delay(80,token);
        }
        bool confirmed=Confirm();
        return Result(confirmed?"navigated_target_verified":foreground?"target_unconfirmed":"foreground_unconfirmed",confirmed);
    }
    private static IEnumerable<nint> DesktopWindows() {
        foreach(var process in Process.GetProcessesByName("Codex").Concat(Process.GetProcessesByName("ChatGPT")))using(process) {
            nint window=0;
            try {
                if(process.ProcessName.Equals("Codex",StringComparison.OrdinalIgnoreCase)||
                    (process.MainModule?.FileName.Contains("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)??false))window=process.MainWindowHandle;
            }catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            if(window!=0)yield return window;
        }
    }
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool ShowWindowAsync(nint window,int command);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint window);
}
