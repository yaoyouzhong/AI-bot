using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

// Independent Claude adapter using the same device schema and existing private
// draft/photo stores. It never opens a terminal or overrides Claude policies.
internal sealed class ClaudeWorkspace:IDisposable
{
    internal sealed record Approval(string Id,string Task,string Tool,JsonElement Input);
    private sealed class Message(string id,string turn){internal string Id=id,Turn=turn,Status="inProgress";internal long Started=DateTimeOffset.UtcNow.ToUnixTimeSeconds();internal DateTimeOffset Observed=DateTimeOffset.UtcNow;internal bool Accepted;internal int Bytes;internal List<object> Items=[];}
    private sealed class Owner{internal ClaudeStream? Stream;internal List<Message> Messages=[];internal List<Approval> Approvals=[];internal bool Ended;}
    private readonly object _sync=new();private readonly SemaphoreSlim _gate=new(1,1);
    private readonly Dictionary<string,Owner> _owners=[];
    private readonly Dictionary<string,string> _interruptions=[];
    private readonly string _root,_private;
    private readonly Func<bool> _enabled;private readonly Func<string?> _executable;private readonly Func<CancellationToken,Task<JsonElement>>? _live;
    private readonly Dictionary<string,Tab5CodexJournal> _journals=[];private readonly Tab5CodexImages _images;private readonly ClaudeCatalog _catalog;
    private readonly Dictionary<string,string> _ready=[],_read=[];
    private readonly Dictionary<string,(string Device,string Task,string Turn)> _readReceipts=[];
    private readonly Dictionary<string,object> _responses=[];
    private object? _view,_control,_draft;private string? _viewTask,_draftDevice;
    private readonly System.Threading.Timer _timer;private long _catalogAt;private byte[]? _key;private bool _disposed;
    internal ClaudeWorkspace(Func<bool> enabled,string? root=null,string? directory=null,Func<string?>? executable=null,Func<CancellationToken,Task<JsonElement>>? live=null){
        _enabled=enabled;_root=root??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
        _private=directory??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-claude");
        _images=new(Path.Combine(_private,"images"));_catalog=new(_root,Key);_executable=executable??Locate;_live=live;
        _timer=new(_=>Refresh(),null,Timeout.Infinite,Timeout.Infinite);
    }
    internal void Start()=>_timer.Change(0,4000);
    private byte[] Key(){lock(_sync){if(_key is not null)return _key;Directory.CreateDirectory(_private);string path=Path.Combine(_private,"alias-key.dat");
        if(File.Exists(path))return _key=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
        byte[] key=RandomNumberGenerator.GetBytes(32),sealedKey=ProtectedData.Protect(key,null,DataProtectionScope.CurrentUser);
        try{using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(sealedKey);file.Flush(true);}catch(IOException) when(File.Exists(path)){key=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);}
        if(key.Length!=32)throw new IOException("Private key unavailable");return _key=key;}}
    private static string? Locate(){var paths=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","bin","claude.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Claude","claude.exe")}.Concat((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Where(p=>p.Length>0).Select(p=>Path.Combine(p,"claude.exe")));return paths.FirstOrDefault(File.Exists);}
    private void Refresh(){if(!_enabled()||_disposed||!_gate.Wait(0))return;try{_catalog.Refresh();_catalogAt=Environment.TickCount64;}catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException){}finally{_gate.Release();}}
    internal string? Alias(Guid session)=>_catalog.Snapshot().FirstOrDefault(e=>e.Session==session)?.Alias;
    internal ClaudeIdentity.Selection? Selection(ClaudeIdentity.Selection? bound,DateTimeOffset now){
        var entries=_catalog.Snapshot();lock(_sync){var owned=new List<ClaudeIdentity.Selection>();foreach(var entry in entries){
            if(_owners.GetValueOrDefault(entry.Alias) is not {} owner||(owner.Messages.FirstOrDefault(m=>m.Status=="inProgress")??owner.Messages.LastOrDefault()) is not {} message||!Guid.TryParse(message.Id,out var prompt))continue;
            bool running=owner.Messages.Any(m=>m.Status=="inProgress"),waiting=running&&owner.Approvals.Count>0,expired=running&&(now-message.Observed).TotalSeconds>=(waiting?300:600);
            string state=expired?"unknown":waiting?"working":running?(message.Accepted?"working":"unknown"):message.Status=="failed"?"error":message.Status is "completed" or "interrupted"?"idle":"unknown";
            bool unread=_ready.GetValueOrDefault(entry.Alias)==message.Turn&&_read.GetValueOrDefault(entry.Alias)!=message.Turn;
            owned.Add(new(entry.Session,prompt,message.Observed,state,message.Status,waiting&&!expired,message.Status=="completed"&&unread?message.Observed.ToUnixTimeSeconds():0,0,message.Turn));
        }return ClaudeIdentity.Resolve(bound,owned);}
    }
    internal object? Snapshot(int limit=64,string? requiredTask=null,string device=""){
        if(!_enabled())return null;var full=_catalog.Snapshot();lock(_sync){
            var entries=full.Take(Math.Max(1,limit)).ToList();var pinned=new HashSet<string>();
            foreach(string? id in new[]{requiredTask,_viewTask})if(id is not null&&full.FirstOrDefault(e=>e.Alias==id) is {} entry){
                if(!entries.Any(e=>e.Alias==id)){int replace=entries.FindLastIndex(e=>!pinned.Contains(e.Alias));if(replace>=0)entries[replace]=entry;}pinned.Add(id);
            }
            var positions=new Dictionary<string,int>();var counts=entries.GroupBy(e=>e.Project).ToDictionary(g=>g.Key,g=>g.Count());
            var tasks=entries.Select(e=>{
                var owner=_owners.GetValueOrDefault(e.Alias);string state=Environment.TickCount64-_catalogAt>30000?"unknown":owner?.Approvals.Count>0?"waiting":owner?.Messages.Any(m=>m.Status=="inProgress")==true?"working":owner is {Ended:false}?"idle":"unknown";
                int position=positions.GetValueOrDefault(e.Project);positions[e.Project]=position+1;int page=position/8,total=(counts[e.Project]+7)/8;
                string project=page==0?e.Project:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.Project+":page:"+page))).ToLowerInvariant()[..16];
                return new{id=e.Alias,title=e.Title,folder=total>1?e.Folder+" · "+(page+1)+"/"+total:e.Folder,projectId=project,updatedAt=e.Updated,state,companionState=state,waitingKind=state=="waiting"?"confirm":""};
            }).ToArray();
            var ids=tasks.Select(t=>t.id).ToHashSet();
            return new{projects=tasks.GroupBy(t=>t.projectId).Take(24).Select(g=>new{id=g.Key,name=g.First().folder}).ToArray(),tasks,
                overviewTask=tasks.OrderByDescending(t=>t.state=="waiting"?3:t.state=="working"?1:0).ThenByDescending(t=>t.updatedAt).FirstOrDefault(),
                replyView=_view,control=_control,draftView=device==_draftDevice?_draft:null,responses=_responses.Where(p=>ids.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
                repliesReady=_ready.Where(p=>ids.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),repliesRead=new Dictionary<string,string>(_read),
                repliesUnread=_ready.Where(p=>ids.Contains(p.Key)&&_read.GetValueOrDefault(p.Key)!=p.Value).ToDictionary(p=>p.Key,p=>p.Value)};
        }
    }
    private Tab5CodexJournal Journal(string device){lock(_sync){if(!_journals.TryGetValue(device,out var journal)){if(_journals.Count>=64||device.Length is <1 or >128)throw new IOException("Private storage unavailable");string name=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device))).ToLowerInvariant();journal=new(Path.Combine(_private,"devices",name,"drafts.dat"));_journals[device]=journal;}return journal;}}
    private async Task<bool> ExternalAsync(ClaudeCatalog.Entry entry,CancellationToken token){
        JsonElement rows;
        if(_live is not null)rows=await _live(token);
        else{var info=new ProcessStartInfo(_executable()??throw new IOException("Claude unavailable")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("agents");info.ArgumentList.Add("--json");
            using var p=Process.Start(info)??throw new IOException("Claude unavailable");using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(8000);
            try{var output=p.StandardOutput.ReadToEndAsync(deadline.Token);var error=p.StandardError.ReadToEndAsync(deadline.Token);await p.WaitForExitAsync(deadline.Token);string value=await output;await error;if(p.ExitCode!=0||value.Length>2*1024*1024)throw new IOException("Claude state unavailable");using var doc=JsonDocument.Parse(value);rows=doc.RootElement.Clone();}
            finally{if(!p.HasExited)p.Kill(entireProcessTree:true);}}
        if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>2048)throw new IOException("Claude state unavailable");
        foreach(var row in rows.EnumerateArray()){
            if(Text(row,"kind") is not ("interactive" or "background"))throw new IOException("Claude state unavailable");
            if(Guid.TryParse(Text(row,"sessionId"),out var id)){if(id==entry.Session)return true;}
            else if(Text(row,"cwd").Length==0||Path.GetFullPath(Text(row,"cwd"))==entry.Directory)return true;
        }return false;
    }
    private async Task<Owner> AcquireAsync(ClaudeCatalog.Entry entry,CancellationToken token){lock(_sync)if(_owners.TryGetValue(entry.Alias,out var existing)&&!existing.Ended)return existing;
        if(entry.Directory.Length==0||!Directory.Exists(entry.Directory)||await ExternalAsync(entry,token))throw new IOException("Claude session owned elsewhere");
        lock(_sync)if(_owners.Count>=8&&!_owners.ContainsKey(entry.Alias))throw new IOException("Claude capacity reached");
        var owner=new Owner();var stream=new ClaudeStream(_executable()??throw new IOException("Claude unavailable"),entry.Directory,entry.Session,frame=>Receive(owner,entry,frame));owner.Stream=stream;
        lock(_sync)_owners[entry.Alias]=owner;try{await stream.StartAsync(token);return owner;}catch{owner.Ended=true;stream.Dispose();throw;}
    }
    private string Turn(ClaudeCatalog.Entry entry,string message)=>ClaudeCatalog.Alias("claude:turn:"+entry.Session+":"+message,Key());
    private void Receive(Owner owner,ClaudeCatalog.Entry entry,JsonElement frame){lock(_sync){
        string type=Text(frame,"type");if(type=="user"&&Guid.TryParse(Text(frame,"uuid"),out var user))foreach(var m in owner.Messages.Where(m=>m.Id==user.ToString()&&!m.Accepted&&m.Status=="inProgress")){m.Accepted=true;m.Observed=DateTimeOffset.UtcNow;}
        if(type=="assistant"&&(!frame.TryGetProperty("parent_tool_use_id",out var parent)||parent.ValueKind==JsonValueKind.Null)&&frame.TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var content)&&content.ValueKind==JsonValueKind.Array&&owner.Messages.FirstOrDefault(m=>m.Status=="inProgress") is {} active){
            string text=string.Join("\n",content.EnumerateArray().Where(c=>Text(c,"type")=="text").Select(c=>Text(c,"text")));int size=Encoding.UTF8.GetByteCount(text);if(text.Length>0&&size<=1024*1024-active.Bytes&&active.Items.Count<1024){active.Observed=DateTimeOffset.UtcNow;active.Items.Add(new{type="agentMessage",phase="final_answer",text});active.Bytes+=size;}}
        if(type=="result"){
            var ids=new HashSet<string>();if(Guid.TryParse(Text(frame,"user_message_uuid"),out var id))ids.Add(id.ToString());
            if(frame.TryGetProperty("user_message_uuids",out var uuids)&&uuids.ValueKind==JsonValueKind.Array)foreach(var value in uuids.EnumerateArray())if(value.ValueKind==JsonValueKind.String&&Guid.TryParse(value.GetString(),out id))ids.Add(id.ToString());
            foreach(var m in owner.Messages.Where(m=>ids.Contains(m.Id)&&m.Status=="inProgress")){m.Accepted=true;m.Observed=DateTimeOffset.UtcNow;bool error=frame.TryGetProperty("is_error",out var e)&&e.ValueKind==JsonValueKind.True;string reason=Text(frame,"terminal_reason");m.Status=reason=="completed"&&frame.TryGetProperty("is_error",out var successError)&&successError.ValueKind==JsonValueKind.False&&frame.TryGetProperty("num_turns",out var count)&&count.TryGetInt32(out int turns)&&turns>0?"completed":reason=="interrupted"?"interrupted":error?"failed":"unknown";if(m.Items.Count>0)_ready[entry.Alias]=m.Turn;}_=owner.Approvals.Count;owner.Approvals.Clear();}
        if(type=="control_request"&&frame.TryGetProperty("request",out var request)&&Text(request,"subtype")=="can_use_tool"&&request.TryGetProperty("input",out var input)&&owner.Approvals.Count<8){
            string requestId=Text(frame,"request_id");var existing=owner.Approvals.FirstOrDefault(a=>a.Id==requestId);
            if(existing is null){if(owner.Messages.LastOrDefault(m=>m.Status=="inProgress") is {} pending)pending.Observed=DateTimeOffset.UtcNow;owner.Approvals.Add(new(requestId,entry.Alias,Text(request,"tool_name"),input.Clone()));}
            else if(existing.Tool!=Text(request,"tool_name")||existing.Input.GetRawText()!=input.GetRawText())owner.Stream?.Dispose();
        }
        if(type=="control_cancel_request")owner.Approvals.RemoveAll(a=>a.Id==Text(frame,"request_id"));
        if(type is "bridge_exit" or "bridge_protocol_error"){owner.Ended=true;owner.Approvals.Clear();foreach(var m in owner.Messages.Where(m=>m.Status=="inProgress")){m.Status="unknown";m.Observed=DateTimeOffset.UtcNow;}}
    }}
    private string? Receipt(ClaudeCatalog.Entry entry,Tab5SendRecord record){lock(_sync){if(record.Operation=="interrupt")return _interruptions.GetValueOrDefault(record.Id);if(_owners.GetValueOrDefault(entry.Alias)?.Messages.FirstOrDefault(m=>m.Id==record.Id&&m.Accepted) is {} message)return message.Turn;}
        if(record.Operation=="interrupt")return null;try{if(!ClaudeHistory.Read(_root,entry.Session,Key()).Accepted.Contains(record.Id))return null;
            lock(_sync)if(_owners.GetValueOrDefault(entry.Alias)?.Messages.FirstOrDefault(m=>m.Id==record.Id) is {} accepted&&!accepted.Accepted){accepted.Accepted=true;accepted.Observed=DateTimeOffset.UtcNow;}
            return Turn(entry,record.Id);}catch(IOException){return null;}}
    internal (int Status,object Body) Upload(string device,string task,string id,byte[] bytes){if(!_enabled()||_catalog.Resolve(task) is null)return(404,new{error="task_not_in_recent_catalog"});try{return(200,_images.Upload(device,task,id,bytes));}catch(Exception e) when(e is IOException or ArgumentException or CryptographicException){return(400,new{error="invalid_image"});}}
    internal async Task<(int Status,object Body)> HandleAsync(JsonElement root,string device,CancellationToken token){
        if(!_enabled())return(403,new{error="claude_disabled"});await _gate.WaitAsync(token);
        try{
            var journal=Journal(device);
            string task=Text(root,"taskId"),op=Text(root,"op"),request=Text(root,"requestId"),text=Text(root,"message"),expected=Text(root,"expectedTurn");var entry=_catalog.Resolve(task);if(entry is null)return(404,new{error="task_not_in_recent_catalog"});
            if(op=="read")return await ReadAsync(entry,root,device,token);
            if(!Guid.TryParse(request,out var requestGuid))return(400,new{error="invalid_request"});request=requestGuid.ToString();
            string[] images=root.TryGetProperty("images",out var imageList)&&imageList.ValueKind==JsonValueKind.Array?imageList.EnumerateArray().Select(v=>v.GetString()??"").ToArray():[];
            if(images.Length>3||Encoding.UTF8.GetByteCount(text)>2000)return(400,new{error="invalid_request"});
            if(op is "draft-save" or "draft-load"){
                if(op=="draft-save"){_images.Resolve(device,task,images,false);journal.SaveDraft(task,text,images,device);}
                else{var pending=journal.Pending(task);lock(_sync){_draftDevice=device;_draft=new{taskId=task,requestId=request,text=journal.Draft(task),images=journal.Images(task),pendingRequest=pending?.Id??"",operation=pending?.Operation??"",expectedTurn=pending?.ExpectedTurn??""};}}
                return(200,new{status="saved",taskId=task,requestId=request});}
            if(op=="read-ack"){lock(_sync){if(!_readReceipts.TryGetValue(request,out var receipt)||receipt.Device!=device||receipt.Task!=task||receipt.Turn!=Text(root,"turnId")||_ready.GetValueOrDefault(task)!=receipt.Turn)return(409,new{error="read_receipt_expired"});_read[task]=receipt.Turn;}return(200,new{accepted=true});}
            var record=journal.Find(request);
            if(op=="receipt"||record is not null){
                if(record is null)return(404,new{error="receipt_not_found"});if(record.TaskId!=task||record.Device!=device||op!="receipt"&&record.Hash!=Tab5CodexJournal.Fingerprint(task,text,op,expected,images))return(409,new{error="request_id_conflict"});
                if(record.State=="rejected")return(record.Status,new{error=record.Error,deliveryFinal=record.Error=="message_cancelled"});if(record.State is "accepted" or "completed")return(202,new{status="submitted",taskId=task,requestId=request,turnId=record.TurnId});
                string? found=Receipt(entry,record);if(found is null)return(504,new{error="delivery_unknown"});journal.Save(record with{State="accepted",Status=202,TurnId=found});return(202,new{status="submitted",taskId=task,requestId=request,turnId=found});}
            if(op is not ("send" or "steer" or "interrupt")||op!="interrupt"&&string.IsNullOrWhiteSpace(text)&&images.Length==0||op!="send"&&!Guid.TryParse(expected,out _))return(400,new{error="invalid_request"});
            if(Environment.TickCount64-_catalogAt>30000)return(503,new{error="claude_catalog_stale"});
            if(op!="interrupt"&&journal.Pending(task) is not null)return(409,new{error="pending_delivery_requires_receipt"});
            Owner? owner;lock(_sync)_owners.TryGetValue(task,out owner);
            Message? active;lock(_sync)active=owner?.Messages.LastOrDefault(m=>m.Status=="inProgress");
            if(op=="send"?active is not null:active?.Turn!=expected)return(409,new{error="turn_changed"});
            if(op!="interrupt"&&owner?.Approvals.Count>0)return(409,new{error="task_waiting_for_input"});
            if(op=="send"&&(owner is null||owner.Ended)&&await ExternalAsync(entry,token))return(409,new{error="session_owned_elsewhere"});
            string[] paths=op=="interrupt"?[]:_images.Resolve(device,task,images,true);
            record=new(request,task,Tab5CodexJournal.Fingerprint(task,text,op,expected,images),op,expected,"dispatching","",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Device:device);
            journal.Save(record,op=="interrupt"?null:text,images); // durable BEFORE resume/write
            try{
                string turn;
                if(op=="interrupt"){
                    var interruption=await owner!.Stream!.InterruptAsync(token);
                    if(interruption.TryGetProperty("cancelled",out var cancelled)&&cancelled.ValueKind==JsonValueKind.Array)foreach(var value in cancelled.EnumerateArray())if(value.ValueKind==JsonValueKind.String&&Guid.TryParse(value.GetString(),out var id)) {
                        lock(_sync)foreach(var message in owner.Messages.Where(m=>m.Id==id.ToString()))message.Status="interrupted";
                        if(journal.Find(id.ToString()) is {} pending)journal.Save(pending with{State="rejected",Status=409,Error="message_cancelled"});
                    }
                    for(int i=0;i<20;i++){lock(_sync)if(!owner.Messages.Any(m=>m.Status=="inProgress")){_interruptions[request]=expected;break;}await Task.Delay(50,token);}
                    lock(_sync)if(!_interruptions.ContainsKey(request))return(504,new{error="delivery_unknown"});turn=expected;
                }else{
                    owner=await AcquireAsync(entry,token);turn=Turn(entry,request);lock(_sync){while(owner.Messages.Count>=128&&owner.Messages.FindIndex(m=>m.Status!="inProgress") is int old&&old>=0)owner.Messages.RemoveAt(old);if(owner.Messages.Count>=128)throw new IOException("Claude history capacity");owner.Messages.Add(new(request,turn));}
                    var blocks=paths.Select(path=>(object)new{type="image",source=new{type="base64",media_type=Path.GetExtension(path)==".png"?"image/png":"image/jpeg",data=Convert.ToBase64String(File.ReadAllBytes(path))}}).ToArray();
                    await owner.Stream!.SendAsync(requestGuid,text,blocks,token);
                    for(int i=0;i<20&&Receipt(entry,record) is null;i++)await Task.Delay(50,token);
                    if(Receipt(entry,record) is null)return(504,new{error="delivery_unknown"});
                }
                journal.Save(record with{State="accepted",Status=202,TurnId=turn});return(202,new{status="submitted",taskId=task,requestId=request,turnId=turn});
            }catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException or InvalidOperationException){return(504,new{error="delivery_unknown"});}
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException){return(503,new{error="claude_or_private_storage_unavailable"});}finally{_gate.Release();}
    }
    private async Task<(int Status,object Body)> ReadAsync(ClaudeCatalog.Entry entry,JsonElement root,string device,CancellationToken token){
        var history=ClaudeHistory.Read(_root,entry.Session,Key());var turns=history.Turns;Owner? owner;lock(_sync)_owners.TryGetValue(entry.Alias,out owner);
        lock(_sync){var owned=owner?.Messages??[];var ids=owned.Select(m=>m.Turn).ToHashSet();var persisted=turns.Where(t=>ids.Contains((string)t["id"])).ToDictionary(t=>(string)t["id"]);turns.RemoveAll(t=>ids.Contains((string)t["id"]));foreach(var m in owned){var row=new Dictionary<string,object>{{"id",m.Turn},{"status",m.Status},{"startedAt",m.Started},{"items",m.Items.Count==0&&persisted.TryGetValue(m.Turn,out var saved)?saved["items"]:m.Items.ToArray()}};turns.Add(row);}}
        if(turns.Count==0)return(404,new{error="reply_unavailable"});string selected=Text(root,"turnId");int index=selected.Length==0?turns.Count-1:turns.FindIndex(t=>(string)t["id"]==selected);
        int offset=root.TryGetProperty("turnOffset",out var ov)&&ov.TryGetInt32(out int n)?n:0;if(offset is < -1 or >1)return(400,new{error="invalid_turn"});index+=offset;if(index<0||index>=turns.Count)return(404,new{error="turn_not_in_available_history"});
        var turn=turns[index];using var items=JsonDocument.Parse(JsonSerializer.Serialize(turn["items"]));string text=string.Join("\n\n",items.RootElement.EnumerateArray().Where(i=>Text(i,"type")=="agentMessage").Select(i=>Text(i,"text")));if(Encoding.UTF8.GetByteCount(text)>1024*1024)return(413,new{error="reply_too_large"});
        bool hasText=text.Length>0;string state=(string)turn["status"],turnId=(string)turn["id"];if(!hasText)text=state=="inProgress"?"Claude 正在处理，等待新的动态…":"本轮尚无公开文字。";
        var pages=new List<string>();var pageText=new StringBuilder();int length=0;foreach(var rune in text.EnumerateRunes()){if(length+rune.Utf8SequenceLength>3500){pages.Add(pageText.ToString());pageText.Clear();length=0;}pageText.Append(rune.ToString());length+=rune.Utf8SequenceLength;}pages.Add(pageText.ToString());
        int page=root.TryGetProperty("page",out var pv)&&pv.TryGetInt32(out int number)?number:-1;if(page==-1)page=pages.Count-1;if(page<0||page>=pages.Count)return(400,new{error="invalid_page"});
        bool external=owner is null||owner.Ended;try{if(external)external=await ExternalAsync(entry,token);}catch(Exception e) when(e is IOException or JsonException or OperationCanceledException){external=true;}
        lock(_sync){string readReceipt="";bool running=owner?.Messages.Any(m=>m.Status=="inProgress")==true,waiting=owner?.Approvals.Count>0;
            if(hasText&&index==turns.Count-1&&state is "completed" or "failed" or "interrupted"){_ready[entry.Alias]=turnId;if(_readReceipts.Count<80){readReceipt=Guid.NewGuid().ToString();_readReceipts[readReceipt]=new(device,entry.Alias,turnId);}}
            var view=new{taskId=entry.Alias,turnId,page,pageCount=pages.Count,hasText,readReceipt,history=index!=turns.Count-1,hasPrevious=index>0,hasNext=index+1<turns.Count,previousTurnId=index>0?(string)turns[index-1]["id"]:"",nextTurnId=index+1<turns.Count?(string)turns[index+1]["id"]:"",state,startedAt=turn["startedAt"]};
            _view=view;_viewTask=entry.Alias;_responses.Clear();_responses[entry.Alias]=pages[page];_control=new{taskId=entry.Alias,turnId=owner?.Messages.LastOrDefault(m=>m.Status=="inProgress")?.Turn??"",running,waiting,canSend=!external&&!running,canSteer=!external&&running&&!waiting,canInterrupt=!external&&running};
            return(200,new{rpcReplyVersion=1,status="loaded",taskId=entry.Alias,turnId,replyView=view,text=pages[page],receipt=state=="completed"?"已完成":state=="inProgress"?waiting?"等待电脑端确认":"运行中":state=="interrupted"?"已中断":state=="failed"?"失败":"状态未知"});
        }
    }
    internal void StopOwners(){ClaudeStream[] streams;lock(_sync)streams=_owners.Values.Select(o=>o.Stream).OfType<ClaudeStream>().ToArray();foreach(var stream in streams)stream.Dispose();}
    internal Approval[] Approvals(){lock(_sync)return _owners.Values.SelectMany(o=>o.Approvals).ToArray();}
    internal async Task DecideAsync(Approval approval,bool allow,CancellationToken token=default){Owner? owner;lock(_sync){owner=_owners.GetValueOrDefault(approval.Task);if(owner?.Approvals.Any(a=>a.Id==approval.Id)!=true)throw new IOException("Approval changed");}
        await owner!.Stream!.RespondAsync(approval.Id,allow?(object)new{behavior="allow",updatedInput=approval.Input}:new{behavior="deny",message="用户拒绝了此操作。"},token);lock(_sync)owner.Approvals.RemoveAll(a=>a.Id==approval.Id);}
    public void Dispose(){_disposed=true;_timer.Dispose();lock(_sync)foreach(var owner in _owners.Values)owner.Stream?.Dispose();}
    private static string Text(JsonElement e,string key)=>ClaudeHistory.Text(e,key)??"";
}
