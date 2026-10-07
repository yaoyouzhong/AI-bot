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
    private readonly Func<string,string,CancellationToken,Task<Tab5CodexComposer.StageResult>> _stage;
    private readonly Func<string,CancellationToken,Task<Tab5CodexComposer.SubmitResult>> _submit;
    private readonly Func<string,CancellationToken,Task<Tab5CodexComposer.ClearDraftResult>> _clear;
    private readonly Func<string,bool?> _readDraftEmpty;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly Dictionary<string,(int Status,object Body)> _receipts=[];
    private readonly Dictionary<string,(long At,string Target)> _requests=[];
    private readonly Dictionary<string,(long At,string Task,bool Consumed)> _drafts=[];
    internal Tab5QuickConsole(Func<IReadOnlyList<Tab5CodexTask>>? catalog=null,
        Func<string,CancellationToken,Task<bool>>? open=null,Func<string,string,CancellationToken,Task<Tab5CodexComposer.StageResult>>? stage=null,
        Func<string,CancellationToken,Task<Tab5CodexComposer.SubmitResult>>? submit=null,
        Func<string,CancellationToken,Task<Tab5CodexComposer.ClearDraftResult>>? clear=null,Func<string,bool?>? readDraftEmpty=null) {
        _catalog=catalog??Tab5CodexCatalog.Recent;_open=open??OpenAsync;_stage=stage??Tab5CodexComposer.StageWithResultAsync;
        _submit=submit??Tab5CodexComposer.SubmitAsync;
        _clear=clear??Tab5CodexComposer.ClearAsync;
        _readDraftEmpty=readDraftEmpty??Tab5CodexComposer.ReadDraftEmpty;
    }
    internal static Tab5CodexTask? Select(IReadOnlyList<Tab5CodexTask> tasks,string id)=>
        id.Length==0?tasks.MaxBy(t=>t.UpdatedAt):tasks.FirstOrDefault(t=>t.Id==id);
    internal async Task<(int Status,object Body)> HandleAsync(JsonElement request,CancellationToken token) {
        string op=Tab5CodexDesktop.Text(request,"op"),text=Tab5CodexDesktop.Text(request,"text");
        string draftRequest=Tab5CodexDesktop.Text(request,"draftRequestId");
        if(op is not ("open-recent" or "stage-draft" or "submit-draft" or "clear-draft")||
            !Guid.TryParseExact(Tab5CodexDesktop.Text(request,"requestId"),"D",out var id))return(400,new{error="invalid_operation"});
        if(op=="stage-draft"&&(string.IsNullOrWhiteSpace(text)||System.Text.Encoding.UTF8.GetByteCount(text)>2000))return(400,new{error="invalid_draft"});
        if(!await _gate.WaitAsync(0,token))return(409,new{error="desktop_busy"});
        try {
            string requestId=id.ToString();
            string target=Tab5CodexDesktop.Text(request,"taskId");
            if(target.Length>0&&!Guid.TryParseExact(target,"D",out _))return(400,new{error="invalid_task"});
            if(op=="stage-draft"&&target.Length==0)return(400,new{error="missing_draft_target"});
            if(op=="clear-draft"&&target.Length==0)return(400,new{error="missing_draft_target"});
            if(op=="submit-draft"&&(target.Length==0||!Guid.TryParseExact(draftRequest,"D",out _)))return(400,new{error="missing_staged_draft"});
            string fingerprint=op+":"+target+":"+draftRequest+":"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
            long now=Environment.TickCount64;
            foreach(var old in _requests.Where(p=>now-p.Value.At>105000).Select(p=>p.Key).ToArray()) {_requests.Remove(old);_receipts.Remove(old);}
            foreach(var old in _drafts.Where(p=>now-p.Value.At>900000).Select(p=>p.Key).ToArray())_drafts.Remove(old);
            if(_receipts.TryGetValue(requestId,out var previous))return _requests[requestId].Target==fingerprint?previous:(409,new{error="request_conflict"});
            var task=Select(_catalog(),target);
            if(task is null)return(404,new{error="no_recent_task"});
            if(_receipts.Count>=128)return(429,new{error="desktop_request_limit"});
            if(op=="clear-draft") {
                _requests[requestId]=(now,fingerprint);
                _receipts[requestId]=(409,new{error="clear_unconfirmed",attempted=true});
                var cleared=await _clear(task.Id,token);
                if(cleared.Attempted||cleared.Cleared)
                    foreach(var old in _drafts.Where(p=>p.Value.Task==task.Id).Select(p=>p.Key).ToArray())_drafts.Remove(old);
                var receipt=(Status:cleared.Cleared?200:409,Body:(object)new{taskId=task.Id,cleared=cleared.Cleared,attempted=cleared.Attempted,error=cleared.Error});
                _receipts[requestId]=receipt;return receipt;
            }
            if(op=="submit-draft") {
                if(!_drafts.TryGetValue(draftRequest,out var draft)||draft.Task!=task.Id||draft.Consumed)
                    return(409,new{error="staged_draft_expired_or_used"});
                _drafts[draftRequest]=(draft.At,draft.Task,true);
                // Reserve before any input. A lost reply or a new request ID
                // must never press Enter again for the same staged draft.
                _requests[requestId]=(now,fingerprint);
                _receipts[requestId]=(409,new{error="submit_unconfirmed",attempted=true});
                var sent=await _submit(task.Id,token);
                if(!sent.Attempted)_drafts[draftRequest]=draft;
                var receipt=(Status:sent.Submitted?200:409,Body:(object)new{taskId=task.Id,submitted=sent.Submitted,attempted=sent.Attempted,error=sent.Error});
                _receipts[requestId]=receipt;return receipt;
            }
            // Reserve the request before opening the URI. A lost response cannot
            // resolve a different recent conversation on replay.
            _receipts[requestId]=(409,new{error="desktop_open_unconfirmed"});
            _requests[requestId]=(now,fingerprint);
            var stageResult=op=="stage-draft"?await _stage(task.Id,text,token):null;
            bool staged=stageResult?.Staged==true;
            if(staged) {
                if(_drafts.Count>=128)_drafts.Remove(_drafts.MinBy(p=>p.Value.At).Key);
                _drafts[requestId]=(now,task.Id,false);
            }
            bool foreground=op=="stage-draft"?staged:await _open(task.Id,token);
            bool? draftEmpty=op=="open-recent"&&foreground?_readDraftEmpty(task.Id):null;
            if(draftEmpty==true)
                foreach(var old in _drafts.Where(p=>p.Value.Task==task.Id).Select(p=>p.Key).ToArray())_drafts.Remove(old);
            var result=(Status:foreground?200:409,Body:(object)new {
                taskId=task.Id,title=task.Title,projectId=task.ProjectId,folder=task.Folder,updatedAt=task.UpdatedAt,
                selection=target.Length==0?"updated":"selected",
                foreground,staged,appended=staged&&stageResult!.Appended,draftEmpty,
                error=foreground?"":op=="stage-draft"?stageResult!.Error:"desktop_foreground_unconfirmed"
            });
            _receipts[requestId]=result;return result;
        }catch(Exception ex) when(ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) {
            return(503,new{error="desktop_unavailable"});
        }finally{_gate.Release();}
    }
    internal static async Task<bool> OpenAsync(string id,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        long started=Environment.TickCount64;
        string? title=Tab5CodexComposer.UniqueTitle(id);
        bool tapped=false,inputBlocked=false,partialTap=false,foreground=false,target=false;
        int windowCount=0;bool fallbackUsed=false;
        bool Result(string reason,bool success=false) {
            Volatile.Write(ref _navigationDiagnostic,$"{DateTime.Now:HH:mm:ss} {reason}; elapsed={Environment.TickCount64-started}ms; taskId={id}; windows={windowCount}; foreground={foreground}; target={target}; altTap={tapped}; inputBlocked={inputBlocked}; partialTap={partialTap}");
            return success;
        }
        void Raise(nint window) {
            if(IsIconic(window))ShowWindowAsync(window,9);
            var result=Tab5DesktopActivation.Raise(window,!fallbackUsed);
            fallbackUsed=true;
            tapped|=result.Tapped;inputBlocked|=result.InputBlocked;partialTap|=result.PartialTap;
        }
        bool Confirm() {
            foreground=ForegroundObserver.CodexVisible();
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
        using(var process=Process.Start(new ProcessStartInfo(Tab5CodexDesktop.ThreadUri(id)){UseShellExecute=true})) { }
        int attempts=windows.Length==0?100:40;
        for(int attempt=0;attempt<attempts;attempt++) {
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
