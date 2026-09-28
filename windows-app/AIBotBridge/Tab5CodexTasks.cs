using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed record Tab5CodexTask(string Id, string Title, string Folder, long UpdatedAt, string ProjectId = "", int ProjectOrder = int.MaxValue);
internal sealed record Tab5SavedProject(string Id, string Name, string Root, int Order = 0);

// The desktop catalog is only an index. Writes go through its live desktop owner.
internal static class Tab5CodexCatalog
{
    internal static string HomePath => Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } home ? home :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    private const string Sql = "SELECT thread_id,display_title,cwd,source_updated_at FROM local_thread_catalog WHERE host_id='local' AND COALESCE(missing_candidate,0)=0 AND thread_id IS NOT NULL AND display_title IS NOT NULL ORDER BY source_updated_at DESC LIMIT 512";
    private const string ProjectsSql = "SELECT p.id,p.name,r.path FROM projects p JOIN project_roots r ON r.project_id=p.id ORDER BY p.position,r.position";
    internal static IReadOnlyList<Tab5CodexTask> Recent()
    {
        var projects=SavedProjects();
        if(projects.Count==0)return [];
        var path = Path.Combine(HomePath, "sqlite", "codex-dev.db");
        if (!File.Exists(path)) return [];
        nint db = 0, statement = 0;
        var tasks = new List<Tab5CodexTask>();
        try
        {
            if (sqlite3_open_v2(path, out db, 1, 0) != 0) return [];
            sqlite3_busy_timeout(db, 100);
            if (sqlite3_prepare_v2(db, Sql, -1, out statement, 0) != 0) return [];
            while (sqlite3_step(statement) == 100)
            {
                string id = Column(statement, 0), title = Column(statement, 1), folder = Column(statement, 2);
                if (!Guid.TryParse(id, out _) || string.IsNullOrWhiteSpace(title)) continue;
                var project=MatchProject(folder,projects);
                if(project is null)continue;
                title = new string(title.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
                tasks.Add(new(id, title.Length > 60 ? title[..60] : title, project.Name,
                    (long)sqlite3_column_double(statement, 3), project.Id,project.Order));
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException) { return []; }
        finally { if (statement != 0) sqlite3_finalize(statement); if (db != 0) sqlite3_close(db); }
        return SelectRecent(tasks);
    }
    internal static IReadOnlyList<Tab5SavedProject> SavedProjects()
    {
        var path=Path.Combine(HomePath,"state_5.sqlite");
        if(!File.Exists(path))return [];
        nint db=0,statement=0;
        var projects=new List<Tab5SavedProject>();
        try {
            if(sqlite3_open_v2(path,out db,1,0)!=0)return [];
            sqlite3_busy_timeout(db,100);
            if(sqlite3_prepare_v2(db,ProjectsSql,-1,out statement,0)!=0)return [];
            while(sqlite3_step(statement)==100) {
                string id=Column(statement,0),name=Column(statement,1),root=Column(statement,2);
                if(!Guid.TryParse(id,out _)||string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(root))continue;
                try { root=NormalizePath(root); } catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException) {continue;}
                if(name=="m5stack TAB5")name="M5Stack TAB5";
                name=new string(name.Select(c=>char.IsControl(c)?' ':c).ToArray());
                string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant()[..16];
                projects.Add(new(key,name,root,string.Equals(name,"Work",StringComparison.OrdinalIgnoreCase)?-1:projects.Count));
            }
        }
        catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException) {return [];}
        finally {if(statement!=0)sqlite3_finalize(statement);if(db!=0)sqlite3_close(db);}
        return projects;
    }
    internal static Tab5SavedProject? MatchProject(string cwd,IReadOnlyList<Tab5SavedProject> projects)
    {
        if(string.IsNullOrWhiteSpace(cwd))return null;
        string folder;
        try {folder=NormalizePath(cwd);} catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException) {return null;}
        var exact=projects.FirstOrDefault(p=>string.Equals(folder,p.Root,StringComparison.OrdinalIgnoreCase));
        if(exact is not null)return exact;
        var nested=projects.Where(p=>!string.Equals(p.Name,"Work",StringComparison.OrdinalIgnoreCase)&&
                folder.StartsWith(p.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p=>p.Root.Length).FirstOrDefault();
        if(nested is not null)return nested;
        string worktrees=Path.Combine(HomePath,"worktrees")+Path.DirectorySeparatorChar;
        if(!folder.StartsWith(worktrees,StringComparison.OrdinalIgnoreCase))return null;
        string name=Path.GetFileName(folder);
        var matches=projects.Where(p=>string.Equals(Path.GetFileName(p.Root),name,StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length==1?matches[0]:null;
    }
    private static string NormalizePath(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    internal static IReadOnlyList<Tab5CodexTask> SelectRecent(IReadOnlyList<Tab5CodexTask> tasks)
    {
        // Give each project a place in the frame before adding older sessions.
        // A bounded catalog fits the existing TAB5 status packet on all links.
        var groups = tasks.GroupBy(t => t.ProjectId).OrderByDescending(g => g.Max(t => t.UpdatedAt))
            .ThenBy(g => g.Min(t => t.ProjectOrder))
            .Take(24).Select(g => g.OrderByDescending(t => t.UpdatedAt).Take(8).ToArray()).ToArray();
        var selected = new List<Tab5CodexTask>();
        for (int session = 0; session < 8 && selected.Count < 80; session++)
            foreach (var group in groups)
                if (session < group.Length && selected.Count < 80) selected.Add(group[session]);
        return selected.OrderByDescending(t => t.UpdatedAt).ToArray();
    }
    internal static string? RolloutPath(string id) {
        if(!Guid.TryParse(id,out var guid))return null;
        nint db=0,statement=0;
        try {
            if(sqlite3_open_v2(Path.Combine(HomePath,"state_5.sqlite"),out db,1,0)!=0)return null;
            sqlite3_busy_timeout(db,100);
            string sql=$"SELECT rollout_path FROM threads WHERE id='{guid:D}' LIMIT 1";
            if(sqlite3_prepare_v2(db,sql,-1,out statement,0)!=0||sqlite3_step(statement)!=100)return null;
            return Column(statement,0);
        }catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException){return null;}
        finally{if(statement!=0)sqlite3_finalize(statement);if(db!=0)sqlite3_close(db);}
    }
    private static string Column(nint statement, int index)
    {
        int length = sqlite3_column_bytes(statement, index);
        return length is > 0 and <= 1024 ? Marshal.PtrToStringUTF8(sqlite3_column_text(statement, index), length) ?? "" : "";
    }
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint db, int flags, nint vfs);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(nint db, int ms);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out nint statement, nint tail);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(nint statement);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern nint sqlite3_column_text(nint statement, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(nint statement, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern double sqlite3_column_double(nint statement, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(nint statement);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(nint db);
}

internal sealed class Tab5CodexTasks : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Process? _server;
    private StreamWriter? _input;
    private sealed record PendingCall(StreamWriter Writer,TaskCompletionSource<JsonElement> Completion);
    private readonly ConcurrentDictionary<int, PendingCall> _pending = new();
    private readonly ConcurrentDictionary<string, string> _receipts = new();
    private readonly ConcurrentDictionary<string, string> _responses = new();
    private readonly ConcurrentDictionary<string, string> _readyReplies = new();
    private readonly Dictionary<string, string> _terminalStates = new();
    private readonly Dictionary<string, string> _responseTurns = new();
    private readonly object _receiptLock = new();
    private readonly Dictionary<string,string> _activeTurns = new();
    private readonly Dictionary<string,(string TurnId,string Receipt,string? Response,string Status)> _earlyCompletions = new();
    private readonly Dictionary<string,string> _earlyInteractions = new();
    private string? _sendingTask;
    private string? _viewedTask;
    private readonly Dictionary<string,string> _historyPaths=new();
    private readonly Tab5CodexActivity _activity=new();
    private readonly Tab5StoredReplies _storedReplies=new();
    private Tab5ActivityPage? _view;
    private bool _viewHistory,_viewPrevious,_viewNext;
    private int _nextId;
    private readonly Func<string, object, CancellationToken, Task<JsonElement>> _call;
    private readonly Func<CancellationToken, Task> _connect;
    private readonly Func<IReadOnlyList<Tab5CodexTask>> _catalog;
    private readonly Tab5CodexDesktop? _desktop;
    private readonly Tab5LiveActivity? _liveActivity;
    private readonly Tab5CodexJournal? _journal;
    private readonly Tab5CodexImages _images;
    private object? _draftView,_control;
    private readonly SemaphoreSlim _submitGate=new(1,1);
    private readonly CancellationTokenSource _lifetime=new();

    internal Tab5CodexTasks() : this(new Tab5CodexDesktop(),Tab5CodexCatalog.Recent,new Tab5CodexJournal()) { }
    internal Tab5CodexTasks(Tab5CodexDesktop desktop,Func<IReadOnlyList<Tab5CodexTask>> catalog,Tab5CodexJournal? journal=null,Tab5CodexImages? images=null)
    {
        _desktop=desktop;_liveActivity=new((id,ct)=>desktop.ReadAsync(id,ct));_call=CallAsync;_connect=EnsureServerAsync;_catalog=catalog;_journal=journal;_images=images??new();
        foreach(var record in journal?.Accepted()??[]) {
            _activeTurns[record.TaskId]=record.TurnId;_receipts[record.TaskId]="已发送 · 恢复动态";
        }
        foreach(var entry in _activeTurns)_=WatchDesktopAsync(entry.Key,entry.Value,_lifetime.Token);
    }
    // Allows protocol failures to be tested without sending a real Codex turn.
    internal Tab5CodexTasks(Func<string,object,CancellationToken,Task<JsonElement>> call, Func<IReadOnlyList<Tab5CodexTask>> catalog)
    { _call=call; _connect=_=>Task.CompletedTask; _catalog=catalog;_images=new(); }

    internal object Snapshot()=>SnapshotLimited(80);
    internal static Tab5CodexTask? SelectOverview(IEnumerable<Tab5CodexTask> catalog,IReadOnlyDictionary<string,CodexLifecycleTracker.TaskActivity> activity)=>
        catalog.OrderByDescending(t=>activity.GetValueOrDefault(t.Id)?.State=="working")
            .ThenByDescending(t=>Math.Max(t.UpdatedAt,activity.GetValueOrDefault(t.Id)?.UpdatedAt??0)).FirstOrDefault();
    internal object SnapshotLimited(int taskLimit)
    {
        var catalog = _catalog();
        var activity=SessionActivityReader.TaskActivities.GroupBy(a=>a.Id).ToDictionary(g=>g.Key,g=>g.MaxBy(a=>a.UpdatedAt)!);
        _liveActivity?.Refresh(catalog,_lifetime.Token);
        foreach(var live in _liveActivity?.Fresh()??[])activity[live.Id]=live;
        // Publish completed turn identities from both desktop IPC and root logs.
        // The device owns read acknowledgement, after that exact turn renders.
        foreach(var task in catalog)if(activity.TryGetValue(task.Id,out var state)&&state.Completed&&state.TurnId.Length>0)
            _readyReplies[task.Id]=state.TurnId;
        // The overview is independent from a task pinned by the reply reader.
        var overview=SelectOverview(catalog,activity);
        string? viewed;Tab5ActivityPage? view;Dictionary<string,string> responses,ready;bool viewHistory,viewPrevious,viewNext;
        lock(_receiptLock) {
            viewed=_viewedTask;view=_view;viewHistory=_viewHistory;viewPrevious=_viewPrevious;viewNext=_viewNext;
            responses=_responses.ToArray().ToDictionary(p=>p.Key,p=>p.Value);
            ready=_readyReplies.ToArray().ToDictionary(p=>p.Key,p=>p.Value);
        }
        // Under transport pressure retain each project plus the viewed task first.
        var essentials=catalog.GroupBy(t=>t.ProjectId).Select(g=>g.First().Id).ToHashSet();
        var tasks=catalog.OrderByDescending(t=>t.Id==overview?.Id).ThenByDescending(t=>t.Id==viewed).ThenByDescending(t=>essentials.Contains(t.Id)).ThenByDescending(t=>ready.ContainsKey(t.Id)).Take(taskLimit).ToArray();
        if(view is not null&&viewed is not null)responses[viewed]=view.Text;
        var projects = tasks.GroupBy(t => t.ProjectId).OrderByDescending(g => g.Max(t => t.UpdatedAt))
            .ThenBy(g => g.Min(t => t.ProjectOrder))
            .Select(g => new { id = g.Key, name = g.First().Folder }).ToArray();
        return new { projects, tasks, overviewTask=overview is null?null:new {id=overview.Id,title=overview.Title,folder=overview.Folder,projectId=overview.ProjectId,state=activity.GetValueOrDefault(overview.Id)?.State??"idle"}, draftView=Volatile.Read(ref _draftView),control=Volatile.Read(ref _control),
            replyView=view is null?null:new {taskId=viewed,turnId=view.TurnId,page=view.Page,pageCount=view.PageCount,hasText=view.HasText,history=viewHistory,hasPrevious=viewPrevious,hasNext=viewNext,startedAt=view.StartedAt,startedLabel=view.StartedAt>0?DateTimeOffset.FromUnixTimeSeconds(view.StartedAt).ToLocalTime().ToString("MM-dd HH:mm"):"",state=view.State},
            repliesReady = tasks.Where(t=>ready.ContainsKey(t.Id)).Take(80).ToDictionary(t=>t.Id,t=>ready[t.Id]),
            receipts = tasks.Where(t => _receipts.ContainsKey(t.Id)).Take(16).ToDictionary(t => t.Id, t => _receipts[t.Id]),
            responses = tasks.Where(t => responses.ContainsKey(t.Id)).Take(1).ToDictionary(t => t.Id, t => responses[t.Id]) };
    }

    internal Task<(int Status, object Body)> ReadAsync(string taskId,CancellationToken token)=>ReadPageAsync(taskId,-1,token);
    internal async Task<(int Status, object Body)> ReadPageAsync(string taskId,int page,CancellationToken token,string selectedTurn="",int turnOffset=0,bool includeView=false) {
        if(!Guid.TryParse(taskId,out _))return (400,new {error="invalid_request"});
        if(turnOffset is < -1 or > 1||selectedTurn.Length>0&&!Guid.TryParse(selectedTurn,out _)||turnOffset!=0&&selectedTurn.Length==0)return (400,new {error="invalid_turn"});
        if(page < -1)return (400,new {error="invalid_page"});
        var allowed=_catalog().Select(t=>t.Id).ToHashSet();
        if(!allowed.Contains(taskId))return (404,new {error="task_not_in_recent_catalog"});
        await _gate.WaitAsync(token);
        try {
            if(_desktop is not null) {
                Tab5CodexDesktop.State desktopState;Tab5ActivityPage? storedPage=null;
                bool stored=false;
                try {desktopState=await _desktop.ReadAsync(taskId,token,selectedTurn.Length==0?null:selectedTurn,ensureLoaded:false,turnOffset:turnOffset);}
                catch(Tab5CodexDesktop.Rejected ex) when(ex.Code=="desktop_owner_unavailable") {
                    // Viewing never opens/navigates the desktop. Unloaded threads
                    // are read from their own local public reply log instead.
                    var local=_storedReplies.Read(Tab5CodexCatalog.RolloutPath(taskId),Tab5CodexCatalog.HomePath,selectedTurn,turnOffset,page);
                    if(local is null)return(503,new {error="reply_unavailable"});
                    storedPage=local.Page;stored=true;
                    JsonElement Turn(string id,string status)=>JsonSerializer.SerializeToElement(new {turnId=id,status});
                    desktopState=new("notLoaded","",Turn(local.Page.TurnId,local.Page.State),false) {
                        LatestTurn=Turn(local.LatestId,local.LatestState),IsHistory=local.Page.TurnId!=local.LatestId,
                        HasPrevious=local.HasPrevious,HasNext=local.HasNext};
                }
                var activityView=storedPage??(string.IsNullOrEmpty(desktopState.TurnId)?null:_activity.Read(desktopState.Path,Tab5CodexCatalog.HomePath,page,desktopState.TurnId));
                var desktopView=Tab5CodexActivity.FromDesktop(desktopState,page);
                // Live snapshots include streamed public text before its completed
                // rollout item is written. Empty log placeholders are not content.
                bool useDesktopView=storedPage is null&&desktopView.HasText&&
                    (desktopState.Status=="inProgress"||activityView?.HasText!=true);
                if(useDesktopView)activityView=desktopView;
                activityView ??= Tab5CodexActivity.FromDesktop(desktopState,page);
                // The desktop lifecycle owns status; an unrelated CLI rollout
                // may contain an orphan turn or a generic task_complete marker.
                activityView=activityView with {State=desktopState.Status};
                lock(_receiptLock) {
                    _viewHistory=desktopState.IsHistory;_viewPrevious=desktopState.HasPrevious;_viewNext=desktopState.HasNext;
                    _view=activityView;_viewedTask=taskId;_responses[taskId]=activityView.Text;_receipts[taskId]=desktopState.Current.Receipt;
                }
                UpdateControl(taskId,desktopState.Current);
                var current=desktopState.Current;if(!stored)Completed(taskId,current.TurnId,current.Status,FinalResponse(current.Turn));
                if(includeView) {
                    object Payload(Tab5ActivityPage v)=>new {taskId,turnId=desktopState.TurnId,
                        replyView=new {taskId,turnId=desktopState.TurnId,page=v.Page,pageCount=v.PageCount,hasText=v.HasText,
                            history=desktopState.IsHistory,hasPrevious=desktopState.HasPrevious,hasNext=desktopState.HasNext,
                            startedLabel=v.StartedAt>0?DateTimeOffset.FromUnixTimeSeconds(v.StartedAt).ToLocalTime().ToString("MM-dd HH:mm"):"",state=desktopState.Status},
                        text=v.Text,receipt=desktopState.Receipt};
                    var neighbors=new List<object>();
                    foreach(int adjacent in new[]{activityView.Page-1,activityView.Page+1}) {
                        if(adjacent<0||adjacent>=activityView.PageCount)continue;
                        var neighbor=stored?_storedReplies.Read(Tab5CodexCatalog.RolloutPath(taskId),Tab5CodexCatalog.HomePath,desktopState.TurnId,0,adjacent)?.Page:
                            useDesktopView?Tab5CodexActivity.FromDesktop(desktopState,adjacent):_activity.View(adjacent);
                        if(neighbor is null||neighbor.TurnId!=desktopState.TurnId)neighbor=Tab5CodexActivity.FromDesktop(desktopState,adjacent);
                        neighbors.Add(Payload(neighbor));
                    }
                    return (200,new {status="loaded",taskId,turnId=desktopState.TurnId,
                        replyView=JsonSerializer.SerializeToElement(Payload(activityView)).GetProperty("replyView"),
                        text=activityView.Text,receipt=desktopState.Receipt,neighbors});
                }
                return (200,new {status="loaded",taskId,turnId=desktopState.TurnId});
            }
            Tab5ActivityPage? activity=null;
            if(_historyPaths.TryGetValue(taskId,out var knownPath))activity=_activity.Read(knownPath,Tab5CodexCatalog.HomePath,page);
            JsonElement last=default,thread=default;
            if(activity is null) {
            await _connect(token);
            var read=await _call("thread/read",new {threadId=taskId,includeTurns=true},token);
            if(!TryChild(read,"result",out var result)||!TryChild(result,"thread",out thread)||
                !TryChild(thread,"turns",out var turns)||turns.ValueKind!=JsonValueKind.Array)
                return (503,new {error="reply_unavailable"});
            last=turns.GetArrayLength()>0?turns[turns.GetArrayLength()-1]:default;
            if(TryChild(thread,"path",out var historyPath)&&historyPath.ValueKind==JsonValueKind.String) {
                _historyPaths[taskId]=historyPath.GetString()!;
                activity=_activity.Read(historyPath.GetString()!,Tab5CodexCatalog.HomePath,page);
            }
            }
            var response=FinalResponse(last);
            if(response is null&&TryChild(last,"id",out var turnId)&&turnId.ValueKind==JsonValueKind.String&&
               TryChild(thread,"path",out var path)&&path.ValueKind==JsonValueKind.String)
                response=Tab5CodexReplyHistory.Read(path.GetString()!,turnId.GetString()!,Tab5CodexCatalog.HomePath);
            string status=activity?.State??(TryChild(last,"status",out var state)&&state.ValueKind==JsonValueKind.String?state.GetString()!:"");
            string receipt=status switch {"completed"=>"已完成", "inProgress"=>"运行中", "failed"=>"失败", "interrupted"=>"已中断", _=>"暂无回复"};
            if(activity is null&&TryChild(thread,"status",out var threadState)&&TryChild(threadState,"type",out var kind)&&kind.GetString()=="active") {
                receipt="运行中";
                if(TryChild(threadState,"activeFlags",out var flags)&&flags.ValueKind==JsonValueKind.Array&&
                    flags.EnumerateArray().Any(f=>f.ValueKind==JsonValueKind.String&&f.GetString() is "waitingOnApproval" or "waitingOnUserInput"))
                    receipt="等待电脑端确认";
            }
            lock(_receiptLock) {
                // Never label the previous turn's answer as the current turn.
                _responses[taskId]=response??(receipt=="运行中"?"Codex 正在处理，尚无文字回复。":"本轮尚无文字回复。");
                _receipts[taskId]=receipt;
                string completedTurn=activity?.TurnId??(TryChild(last,"id",out var completedId)&&completedId.ValueKind==JsonValueKind.String?completedId.GetString()!:"");
                if(status=="completed"&&(response is {Length:>0}||activity?.HasReply==true)&&
                   _activeTurns.TryGetValue(taskId,out var active)&&active==completedTurn)
                    _readyReplies[taskId]=active;
                Volatile.Write(ref _view,activity);
                Volatile.Write(ref _viewedTask,taskId);
                if(_responses.Count>80)foreach(var key in _responses.Keys.Where(k=>!allowed.Contains(k)))_responses.TryRemove(key,out _);
            }
            return (200,new {status="loaded",taskId});
        }catch(Exception ex) when(ex is TimeoutException or OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception) {
            return (503,new {error="reply_unavailable"});
        }finally{_gate.Release();}
    }

    internal async Task<(int Status, object Body)> SubmitAsync(string taskId, string message, CancellationToken token,
        string? requestId=null,string operation="send",string expectedTurn="",string[]? images=null,string device="")
    {
        images??=[];
        if (!Guid.TryParse(taskId, out _) || message.Length>2000 || Encoding.UTF8.GetByteCount(message) > 2000 ||
            (operation!="interrupt"&&string.IsNullOrWhiteSpace(message)&&images.Length==0)||operation is not ("send" or "steer" or "interrupt")||
            images.Length>Tab5CodexImages.MaxCount||operation=="interrupt"&&images.Length>0||
            requestId is not null&&!Guid.TryParse(requestId,out _)||operation is "steer" or "interrupt"&&!Guid.TryParse(expectedTurn,out _))
            return (400, new { error = "invalid_request" });
        if (!_catalog().Any(t => t.Id == taskId))
            return (404, new { error = "task_not_in_recent_catalog" });
        if(_desktop is not null)return await SubmitDesktopAsync(taskId,message,token,requestId??Guid.NewGuid().ToString(),operation,expectedTurn,images,device);
        if(images.Length>0)return(503,new{error="desktop_images_unavailable"});
        await _gate.WaitAsync(token);
        bool dispatched=false;
        string stage="connect";long began=Stopwatch.GetTimestamp();
        (int Status,object Body) Rejected(int code,string reason) {
            SetSubmitDiagnostic($"stage={stage} status={code} reason={reason} ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F0}");
            return(code,new {error=reason});
        }
        SetSubmitDiagnostic("stage=connect");
        try
        {
            await _connect(token);
            stage="read";SetSubmitDiagnostic("stage=read");
            var read = await _call("thread/read", new { threadId = taskId, includeTurns = true }, token);
            if (!TryChild(read,"result",out var current) || !TryChild(current,"thread",out var currentThread) || !CanContinue(currentThread))
                return Rejected(409,"task_busy_or_state_unknown");
            if(TryChild(currentThread,"path",out var currentPath)&&currentPath.ValueKind==JsonValueKind.String&&
               _activity.Read(currentPath.GetString()!,Tab5CodexCatalog.HomePath)?.State=="inProgress")
                return Rejected(409,"task_busy_or_state_unknown");
            stage="resume";SetSubmitDiagnostic("stage=resume");
            var resumed = await _call("thread/resume", new { threadId = taskId }, token);
            if (TryChild(resumed,"error", out _)) return Rejected(503,"task_resume_failed");
            if (!TryChild(resumed,"result",out current)||!TryChild(current,"thread",out currentThread)||!CanContinue(currentThread))
                return Rejected(409,"task_busy_or_state_unknown");
            token.ThrowIfCancellationRequested();
            // Once dispatch begins, a broken pipe or incomplete response cannot
            // prove rejection. Preserve the draft and never encourage a blind retry.
            lock(_receiptLock) { _sendingTask=taskId; _earlyCompletions.Remove(taskId); _earlyInteractions.Remove(taskId); _responses.TryRemove(taskId,out _); _readyReplies.TryRemove(taskId,out _); _terminalStates.Remove(taskId); if(_viewedTask==taskId)_view=null; }
            dispatched=true;
            stage="start";SetSubmitDiagnostic("stage=start");
            var started = await _call("turn/start", new { threadId = taskId, input = new[] { new { type = "text", text = message } } }, token);
            if (TryChild(started,"error", out _)) return Rejected(503,"turn_start_failed");
            if (!TryChild(started,"result",out var result)||!TryChild(result,"turn",out var turn)||
                !TryChild(turn,"id",out var id)||id.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(id.GetString()))
                throw new IOException("Codex start acknowledgement is incomplete");
            var turnId=id.GetString()!;
            lock(_receiptLock) {
                _activeTurns[taskId]=turnId;
                _sendingTask=null;
                if(_earlyInteractions.Remove(taskId,out var interaction))
                    _receipts[taskId]=interaction;
                else if(_earlyCompletions.Remove(taskId,out var early)&&early.TurnId==turnId) {
                    _receipts[taskId]=early.Receipt;
                    if(early.Response is { Length: > 0 })_responses[taskId]=early.Response;
                    _terminalStates[taskId]=early.Status;
                    if(early.Status=="completed"&&early.Response is {Length:>0})_readyReplies[taskId]=turnId;
                }
                else _receipts[taskId]="已交给 Codex · "+DateTimeOffset.Now.ToString("HH:mm");
            }
            SetSubmitDiagnostic($"stage=accepted status=202 ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F0}");
            return (202, new { status = "submitted", taskId, turnId });
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception)
        {
            if(dispatched) {
                _receipts[taskId] = "发送结果不确定，请先在电脑端查看，勿重复发送";
                return Rejected(504,"delivery_unknown");
            }
            _receipts[taskId] = "发送失败，请在电脑端查看";
            return Rejected(503,"codex_unavailable");
        }
        finally {
            lock(_receiptLock) {
                if(_sendingTask==taskId)_sendingTask=null;
                _earlyCompletions.Remove(taskId);
                _earlyInteractions.Remove(taskId);
            }
            _gate.Release();
        }
    }

    private async Task<(int Status,object Body)> SubmitDesktopAsync(string taskId,string message,CancellationToken token,string requestId,string operation,string expectedTurn,string[] images,string device) {
        if(!await _submitGate.WaitAsync(0,token))return(409,new {error="task_busy_or_state_unknown"});
        bool dispatched=false;long began=Stopwatch.GetTimestamp();
        SetSubmitDiagnostic("stage=desktop_connect");
        requestId=Guid.Parse(requestId).ToString();
        var record=new Tab5SendRecord(requestId,taskId,Tab5CodexJournal.Fingerprint(taskId,message,operation,expectedTurn,images),operation,expectedTurn,
            "prepared","",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        try {
            if(_journal?.Find(requestId) is { } existing) {
                if(existing.Hash!=record.Hash||existing.TaskId!=taskId)return(409,new {error="request_id_conflict"});
                return await RecoverReceiptAsync(existing,token);
            }
            var imagePaths=_images.Resolve(device,taskId,images,true);
            var turnId=await _desktop!.SendAsync(taskId,message,()=>{
                // Persist before crossing the process boundary. A crash from this
                // point is reconciled by message ID, never automatically resent.
                _journal?.Save(record with {State="dispatching"},message,images);
                dispatched=true;SetSubmitDiagnostic("stage=desktop_start");
            },token,requestId,operation,expectedTurn,imagePaths);
            _journal?.Save(record with {State="accepted",TurnId=turnId,Status=202});
            lock(_receiptLock) {
                _activeTurns[taskId]=turnId;_readyReplies.TryRemove(taskId,out _);_responses.TryRemove(taskId,out _);
                _terminalStates.Remove(taskId);_responseTurns.Remove(taskId);
                if(_viewedTask==taskId)_view=null;
                _receipts[taskId]=operation=="interrupt"?"已请求停止":operation=="steer"?"已追加":"已发送";
            }
            SetSubmitDiagnostic($"stage=accepted transport=desktop status=202 ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F0}");
            _=WatchDesktopAsync(taskId,turnId,_lifetime.Token);
            return(202,new {status="submitted",taskId,turnId,requestId});
        }catch(ArgumentException) {return(400,new{error="invalid_images"});}
        catch(Tab5CodexDesktop.Rejected ex) {
            int code=ex.Code is "task_busy_or_state_unknown" or "turn_changed" or "task_waiting_for_input"?409:503;
            try{_journal?.Save(record with {State="rejected",Status=code,Error=ex.Code});}catch(IOException) { }
            SetSubmitDiagnostic($"stage=desktop_rejected status={code} reason={ex.Code}");
            _receipts[taskId]=code==409?"会话状态已变化，请查看动态":"发送失败，文字已保留";
            return(code,new {error=ex.Code});
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or TimeoutException or OperationCanceledException or InvalidOperationException or JsonException) {
            string reason=dispatched?"delivery_unknown":"desktop_unavailable";
            SetSubmitDiagnostic($"stage=desktop_error reason={reason} ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F0}");
            _receipts[taskId]=dispatched?"送达未确认，请先在电脑查看":"电脑 Codex 暂不可用";
            return(dispatched?504:503,new {error=reason});
        }finally{_submitGate.Release();}
    }
    private void UpdateControl(string taskId,Tab5CodexDesktop.State state)=>Volatile.Write(ref _control,
        new {taskId,turnId=state.TurnId,running=state.Runtime=="active"&&state.Status=="inProgress",waiting=state.Waiting});
    internal (int Status,object Body) UploadImage(string device,string taskId,string requestId,string base64) {
        if(!_catalog().Any(t=>t.Id==taskId))return(404,new{error="task_not_in_recent_catalog"});
        try{return(200,_images.Upload(device,taskId,requestId,Convert.FromBase64String(base64)));}
        catch(Exception ex) when(ex is ArgumentException or FormatException or OutOfMemoryException){return(400,new{error="invalid_image"});}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or System.Runtime.InteropServices.ExternalException)
        {return(503,new{error="image_storage_unavailable"});}
    }
    internal (int Status,object Body) Draft(string taskId,string requestId,string? text=null,string[]? images=null,string device="") {
        if(!Guid.TryParse(taskId,out _)||!Guid.TryParse(requestId,out _)||text is not null&&Encoding.UTF8.GetByteCount(text)>2000)
            return(400,new{error="invalid_request"});
        if(!_catalog().Any(t=>t.Id==taskId))return(404,new{error="task_not_in_recent_catalog"});
        try {
            if(_journal is null)throw new IOException();
            if(text is not null){_images.Resolve(device,taskId,images??[],false);_journal.SaveDraft(taskId,text,images);}
            else {
                var pending=_journal.Pending(taskId);
                Volatile.Write(ref _draftView,new{taskId,requestId,text=_journal.Draft(taskId),images=_journal.Images(taskId),pendingRequest=pending?.Id??"",operation=pending?.Operation??"",expectedTurn=pending?.ExpectedTurn??""});
            }
            return(200,new{status="saved",taskId,requestId});
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {return(503,new{error="draft_storage_unavailable"});}
    }
    internal async Task<(int Status,object Body)> ReceiptAsync(string taskId,string requestId,CancellationToken token) {
        if(!Guid.TryParse(taskId,out _)||!Guid.TryParse(requestId,out _))return(400,new{error="invalid_request"});
        if(!_catalog().Any(t=>t.Id==taskId))return(404,new{error="task_not_in_recent_catalog"});
        try {
            var record=_journal?.Find(Guid.Parse(requestId).ToString());
            if(record is null)return(404,new{error="receipt_not_found"});
            if(record.TaskId!=taskId)return(409,new{error="request_id_conflict"});
            return await RecoverReceiptAsync(record,token);
        }catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException or JsonException or InvalidOperationException)
        {return(504,new{error="delivery_unknown"});}
    }
    private async Task<(int Status,object Body)> RecoverReceiptAsync(Tab5SendRecord record,CancellationToken token) {
        if(record.State=="rejected")return(record.Status,new{error=record.Error});
        if(record.State is "accepted" or "completed")return(202,new{status="submitted",taskId=record.TaskId,turnId=record.TurnId,requestId=record.Id});
        string? turn=record.Operation=="interrupt"?null:await _desktop!.FindAcceptedAsync(record.TaskId,record.Id,token);
        if(!Guid.TryParse(turn,out _))return(504,new{error="delivery_unknown"});
        _journal!.Save(record with {State="accepted",TurnId=turn!,Status=202});
        lock(_receiptLock){_activeTurns[record.TaskId]=turn!;_receipts[record.TaskId]="已确认送达";}
        _=WatchDesktopAsync(record.TaskId,turn!,_lifetime.Token);
        return(202,new{status="submitted",taskId=record.TaskId,turnId=turn,requestId=record.Id});
    }
    private async Task WatchDesktopAsync(string taskId,string turnId,CancellationToken token) {
        // Completion notifications continue even when the device leaves the viewer.
        int delay=2000;
        while(!token.IsCancellationRequested) {
            try {
                await Task.Delay(delay,token);
                lock(_receiptLock)if(!_activeTurns.TryGetValue(taskId,out var active)||active!=turnId)return;
                var state=await _desktop!.ReadAsync(taskId,token,turnId);
                if(state.TurnId!=turnId){delay=5000;continue;}
                _receipts[taskId]=state.Receipt;
                if(Volatile.Read(ref _viewedTask)==taskId)UpdateControl(taskId,state);
                Completed(taskId,turnId,state.Status,FinalResponse(state.Turn));
                if(state.Status is "completed" or "failed" or "interrupted") {_journal?.Finish(taskId,turnId);return;}
                delay=2000;
            }catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException or InvalidOperationException or JsonException) {
                if(token.IsCancellationRequested)return;
                _receipts[taskId]="已发送 · 等待电脑连接恢复";delay=10000;
            }
        }
    }

    private volatile string _submitDiagnostic="尚无发送请求";
    internal string SubmitDiagnostic=>_submitDiagnostic;
    private void SetSubmitDiagnostic(string metadata) {
        _submitDiagnostic=metadata;
        if(Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG") is { } path)
            try{File.AppendAllText(path,$"{DateTimeOffset.Now:O} CODEX_SEND {metadata}{Environment.NewLine}");}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool TryChild(JsonElement value,string name,out JsonElement child) {
        child=default;
        return value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out child);
    }

    private static JsonElement ParseOutputLine(string line) {
        try { using var doc=JsonDocument.Parse(line);return doc.RootElement.Clone(); }
        catch(JsonException) { return default; }
    }

    internal void Completed(string taskId,string turnId,string status,string? response=null) {
        if(string.IsNullOrWhiteSpace(taskId)||string.IsNullOrWhiteSpace(turnId)||status is not ("completed" or "interrupted" or "failed"))return;
        lock(_receiptLock) {
            if(_sendingTask==taskId&&_activeTurns.TryGetValue(taskId,out var previousTurn)&&previousTurn==turnId)return;
            string value=status=="completed"?"Codex 已完成 · "+DateTimeOffset.Now.ToString("HH:mm"):
                status=="interrupted"?"任务已中断，请在电脑上查看":"任务失败，请在电脑上查看";
            if(_activeTurns.TryGetValue(taskId,out var active)&&active==turnId) {
                if(status=="interrupted"&&_receipts.TryGetValue(taskId,out var previous)&&
                    (previous.StartsWith("权限操作已取消",StringComparison.Ordinal)||previous.StartsWith("需要电脑端交互",StringComparison.Ordinal)))return;
                _receipts[taskId]=value;
                _terminalStates[taskId]=status;
                if(response is { Length: > 0 }){_responses[taskId]=response;_responseTurns[taskId]=turnId;}
                if(status=="completed"&&_responseTurns.TryGetValue(taskId,out var answeredTurn)&&answeredTurn==turnId)
                    _readyReplies[taskId]=turnId;
            }else if(_sendingTask==taskId) _earlyCompletions[taskId]=(turnId,value,response,status);
        }
    }

    private static string? ResponseText(JsonElement item) {
        if(!TryChild(item,"type",out var type)||type.ValueKind!=JsonValueKind.String||type.GetString()!="agentMessage"||
           !TryChild(item,"text",out var text)||text.ValueKind!=JsonValueKind.String)return null;
        if(TryChild(item,"phase",out var phase)&&phase.ValueKind==JsonValueKind.String&&phase.GetString()=="commentary")return null;
        return BoundResponse(text.GetString());
    }
    internal static string? BoundResponse(string? value) {
        if(string.IsNullOrWhiteSpace(value))return null;
        var bytes=Encoding.UTF8.GetBytes(value);
        return bytes.Length<=2500?value:
            Encoding.UTF8.GetString(bytes.AsSpan(0,2490)).TrimEnd('\uFFFD')+"…\n内容较长，请在电脑端查看全文。";
    }
    internal static string? FinalResponse(JsonElement turn) {
        if(!TryChild(turn,"items",out var items)||items.ValueKind!=JsonValueKind.Array)return null;
        for(int i=items.GetArrayLength()-1;i>=0;i--)
            if(ResponseText(items[i]) is { } response)return response;
        return null;
    }
    private void ItemCompleted(string taskId,string turnId,JsonElement item) {
        if(ResponseText(item) is not { } response)return;
        lock(_receiptLock)
            if(_activeTurns.TryGetValue(taskId,out var active)&&active==turnId) {
                _responses[taskId]=response;
                _responseTurns[taskId]=turnId;
                if(_terminalStates.TryGetValue(taskId,out var status)&&status=="completed")_readyReplies[taskId]=turnId;
            }
    }

    internal void InteractionRequired(string taskId,string receipt) {
        if(string.IsNullOrWhiteSpace(taskId))return;
        lock(_receiptLock) {
            if(_sendingTask==taskId)_earlyInteractions[taskId]=receipt;
            else _receipts[taskId]=receipt;
        }
    }

    internal static bool CanContinue(JsonElement thread)
    {
        if (thread.ValueKind != JsonValueKind.Object || !thread.TryGetProperty("status", out var state) || state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() is not ("idle" or "notLoaded") ||
            !thread.TryGetProperty("turns", out var turns) || turns.ValueKind != JsonValueKind.Array || turns.GetArrayLength() == 0)
            return false;
        var last = turns[turns.GetArrayLength() - 1];
        return last.ValueKind == JsonValueKind.Object && last.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
            (status.GetString() is "completed" or "failed" or "interrupted") &&
            last.TryGetProperty("completedAt", out var ended) && ended.ValueKind == JsonValueKind.Number;
    }

    private async Task EnsureServerAsync(CancellationToken token)
    {
        if (_server is { HasExited: false } && _input is not null) return;
        _server?.Dispose();
        _pending.Clear();
        var start = new ProcessStartInfo("codex")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("app-server");
        // The installed CLI calls the desktop's priority tier "fast". Apply
        // compatibility to this child only; never rewrite the user's config.
        if (NeedsTierCompatibility())
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("service_tier=fast");
        }
        _server = Process.Start(start) ?? throw new IOException("Codex app-server did not start");
        _input = _server.StandardInput;
        _ = ReadOutputAsync(_server.StandardOutput, _input);
        _ = DrainErrorAsync(_server.StandardError);
        var init = await CallAsync("initialize", new { clientInfo = new { name = "tab5-bridge", version = "0.1" }, capabilities = new { } }, token);
        if (init.TryGetProperty("error", out _)) throw new IOException("Codex initialization failed");
        await SendAsync(new { method = "initialized" });
    }

    private static bool NeedsTierCompatibility()
    {
        try
        {
            var path = Path.Combine(Tab5CodexCatalog.HomePath, "config.toml");
            if (!File.Exists(path)) return false;
            foreach (var line in File.ReadLines(path))
            {
                if (line.TrimStart().StartsWith('[')) break;
                if (TierNeedsCompatibility(line)) return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return false;
    }

    internal static bool TierNeedsCompatibility(string line)=>
        Regex.IsMatch(line,"^\\s*service_tier\\s*=\\s*[\"'](?:default|priority)[\"']\\s*(#.*)?$");

    private static async Task DrainErrorAsync(StreamReader reader)
    {
        try { while (await reader.ReadLineAsync() is not null) { } }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    private async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken token)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var input=_input??throw new IOException("Codex is disconnected");
        _pending[id] = new(input,completion);
        try
        {
            await SendAsync(new { id, method, @params = parameters },input);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(25), token);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task SendAsync(object value, StreamWriter? writer = null)
    {
        await _writeGate.WaitAsync();
        try { await (writer ?? _input ?? throw new IOException("Codex is disconnected")).WriteLineAsync(JsonSerializer.Serialize(value, JsonDefaults.Options)); }
        finally { _writeGate.Release(); }
    }

    internal async Task ReadOutputAsync(StreamReader output, StreamWriter input)
    {
        try
        {
            while (await output.ReadLineAsync() is { } line)
            {
                var root = ParseOutputLine(line);
                if(root.ValueKind!=JsonValueKind.Object)continue;
                if (!root.TryGetProperty("method", out _) && root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number &&
                    id.TryGetInt32(out var responseId) && _pending.TryGetValue(responseId, out var pending) && ReferenceEquals(pending.Writer,input) &&
                    _pending.TryRemove(responseId,out _))
                    pending.Completion.TrySetResult(root.Clone());
                else if (root.TryGetProperty("id", out id) && root.TryGetProperty("method", out var approval) &&
                    approval.ValueKind == JsonValueKind.String && approval.GetString() is "item/commandExecution/requestApproval" or "item/fileChange/requestApproval")
                {
                    if (TryChild(root,"params",out var request)&&TryChild(request,"threadId",out var task)&&task.ValueKind==JsonValueKind.String)
                        InteractionRequired(task.GetString() ?? "","权限操作已取消，请在电脑端重试");
                    await SendAsync(new { id, result = new { decision = "cancel" } }, input);
                }
                else if (root.TryGetProperty("id", out id) && root.TryGetProperty("method", out _) )
                {
                    if (TryChild(root,"params",out var request)&&TryChild(request,"threadId",out var task)&&task.ValueKind==JsonValueKind.String)
                        InteractionRequired(task.GetString() ?? "","需要电脑端交互，请在电脑端重试");
                    await SendAsync(new { id, error = new { code = -32601, message = "This interaction is not supported by TAB5. Continue from the desktop client." } }, input);
                }
                else if (root.TryGetProperty("method",out var method) && method.ValueKind==JsonValueKind.String&&method.GetString()=="item/completed"&&
                    TryChild(root,"params",out var p)&&TryChild(p,"threadId",out var thread)&&thread.ValueKind==JsonValueKind.String&&
                    TryChild(p,"turnId",out var turnId)&&turnId.ValueKind==JsonValueKind.String&&TryChild(p,"item",out var item))
                {
                    ItemCompleted(thread.GetString()??"",turnId.GetString()??"",item);
                }
                else if (root.TryGetProperty("method", out method) && method.ValueKind==JsonValueKind.String&&method.GetString() == "turn/completed" &&
                    TryChild(root,"params",out var p2)&&TryChild(p2,"threadId",out var thread2)&&thread2.ValueKind==JsonValueKind.String&&
                    TryChild(p2,"turn",out var turn)&&TryChild(turn,"id",out var turnId2)&&turnId2.ValueKind==JsonValueKind.String&&
                    TryChild(turn,"status",out var state)&&state.ValueKind==JsonValueKind.String)
                {
                    Completed(thread2.GetString()??"",turnId2.GetString()??"",state.GetString()??"",FinalResponse(turn));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or ObjectDisposedException) { }
        finally {
            // A retired reader must never cancel calls on its replacement server.
            foreach(var pending in _pending.Values.Where(p=>ReferenceEquals(p.Writer,input)))
                pending.Completion.TrySetException(new IOException("Codex app-server disconnected"));
        }
    }

    public void Dispose() { _lifetime.Cancel();try { if (_server is { HasExited: false }) _server.Kill(); } catch (InvalidOperationException) { } _server?.Dispose(); }
}
