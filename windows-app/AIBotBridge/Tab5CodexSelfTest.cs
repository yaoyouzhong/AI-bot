using System.Text.Json;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class Tab5CodexSelfTest
{
    internal static async Task RunAsync() {
        await VerifyOverviewFallbackAsync();
        await Tab5QueueTransportSelfTest.RunAsync();
        Tab5CodexCatalogSelfTest.Run();
        await Tab5NativeAppendSelfTest.RunAsync();
        await Tab5CodexPublicHistorySelfTest.RunAsync();
        Tab5CodexReadStateSelfTest.Run();
        Tab5CodexJournalSelfTest.Run();
        await Tab5CodexDesktopSelfTest.RunAsync();
        await Tab5HistorySelfTest.RunAsync();
        var taskId=Guid.NewGuid().ToString();
        if(!Tab5CodexTasks.TierNeedsCompatibility("service_tier = \"priority\"")||
           !Tab5CodexTasks.TierNeedsCompatibility("service_tier = 'default' # desktop")||
           Tab5CodexTasks.TierNeedsCompatibility("service_tier = \"fast\"")||
           Tab5CodexTasks.TierNeedsCompatibility("service_tier = \"flex\"")||
           Tab5CodexTasks.TierNeedsCompatibility("# service_tier = \"priority\""))
            throw new Exception("CLI tier compatibility must preserve supported and commented settings");
        JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
        var workRoot=Path.Combine(Path.GetTempPath(),"tab5-projects","Work");
        var bossRoot=Path.Combine(workRoot,"boss-resume-filter");
        Tab5SavedProject[] saved=[new("work","Work",workRoot,-1),new("boss","boss-resume-filter",bossRoot)];
        if(Tab5CodexCatalog.MatchProject(workRoot,saved)?.Id!="work"||
           Tab5CodexCatalog.MatchProject(bossRoot,saved)?.Id!="boss"||
           Tab5CodexCatalog.MatchProject(Path.Combine(bossRoot,"src"),saved)?.Id!="boss"||
           Tab5CodexCatalog.MatchProject(Path.Combine(workRoot,"hello"),saved) is not null||
           Tab5CodexCatalog.MatchProject(Path.Combine(Path.GetTempPath(),"hello","work"),saved) is not null||
           Tab5CodexCatalog.MatchProject(Path.Combine(Tab5CodexCatalog.HomePath,"worktrees","44ed","boss-resume-filter"),saved) is not null)
            throw new InvalidOperationException("TAB5 Codex: only saved project roots and matching worktrees appear");
        var projectTasks=Enumerable.Range(0,10).Select(i=>new Tab5CodexTask(Guid.NewGuid().ToString(),"Project A task "+i,"Project A",100-i,"aaaaaaaaaaaaaaaa"))
            .Concat([new Tab5CodexTask(Guid.NewGuid().ToString(),"Project B task","Project B",50,"bbbbbbbbbbbbbbbb")]).ToArray();
        var grouped=Tab5CodexCatalog.SelectRecent(projectTasks);
        if(grouped.Count!=11||grouped.Count(t=>t.ProjectId=="aaaaaaaaaaaaaaaa")!=10||grouped.Count(t=>t.ProjectId=="bbbbbbbbbbbbbbbb")!=1)
            throw new InvalidOperationException("TAB5 Codex: retain older sessions and small projects");
        using(var catalog=new Tab5CodexTasks((_,_,_)=>Task.FromResult(default(JsonElement)),()=>grouped)) {
            var snapshot=JsonSerializer.SerializeToElement(catalog.Snapshot());
            var projects=snapshot.GetProperty("projects");
            if(projects.GetArrayLength()!=3||projects[0].GetProperty("name").GetString()!="1/2 · Project A"||
               projects[1].GetProperty("name").GetString()!="2/2 · Project A"||
               projects[1].GetProperty("id").GetString()!=Tab5GitProject.Id("aaaaaaaaaaaaaaaa:page:1")||
               projects[2].GetProperty("name").GetString()!="Project B"||
               !snapshot.GetProperty("tasks").EnumerateArray().Select(t=>t.GetProperty("Id").GetString()).ToHashSet().SetEquals(grouped.Select(t=>t.Id)))
                throw new InvalidOperationException("TAB5 Codex: project selector catalog");
        }
        using(var catalog=new Tab5CodexTasks((_,_,_)=>Task.FromResult(default(JsonElement)),()=>grouped.Select(t=>t with {ProjectOrder=t.Folder=="Project B"?0:1}).ToArray())) {
            var projects=JsonSerializer.SerializeToElement(catalog.Snapshot()).GetProperty("projects");
            if(projects[0].GetProperty("name").GetString()!="1/2 · Project A")
                throw new InvalidOperationException("TAB5 Codex: project order must follow recent activity");
        }
        var idle=Json("{\"result\":{\"thread\":{\"status\":{\"type\":\"idle\"},\"turns\":[{\"id\":\"synthetic-turn\",\"status\":\"completed\",\"completedAt\":1}]}}}");
        JsonElement DirectoryPage()=>Json("{\"result\":{\"data\":[{\"id\":\"synthetic-turn\",\"status\":\"completed\",\"completedAt\":1}],\"nextCursor\":null}}");
        string Record(object payload,string type="event_msg")=>JsonSerializer.Serialize(new{type,timestamp="2026-09-26T03:05:23Z",payload});
        var activity=new Tab5CodexActivity();
        activity.Accept(Record(new{type="task_started",turn_id="old"}));
        activity.Accept(Record(new{type="turn_aborted",turn_id="old"}));
        activity.Accept(Record(new{type="task_started",turn_id="live"}));
        activity.Accept(Record(new{type="item_completed",turn_id="live",item=new{type="Reasoning",id="secret",raw_content="PRIVATE_REASONING"}}));
        var progress=new{type="message",role="assistant",id="public-1",phase="commentary",content=new[]{new{type="output_text",text="正在检查接口"}}};
        activity.Accept(Record(progress,"response_item"));
        activity.Accept(Record(new{type="item_completed",turn_id="live",item=new{type="AgentMessage",id="public-1",phase="commentary",content=progress.content}}));
        activity.Accept(Record(new{type="item_completed",turn_id="live",item=new{type="CommandExecution",id="tool",status="completed",command="SECRET_ARGUMENT",stdout="SECRET_OUTPUT"}}));
        activity.Accept(Record(new{type="turn_aborted",turn_id="old"}));
        var live=activity.View()!;
        if(live.State!="inProgress"||live.TurnId!="live"||!live.Text.Contains("正在检查接口")||live.Text.Contains("执行命令")||
           live.Text.Contains("SECRET")||live.Text.Contains("PRIVATE")||live.Text.Split("正在检查接口").Length!=2)
            throw new Exception("Public activity must deduplicate items, ignore reasoning/arguments/output and isolate lifecycle turns");
        var longText=string.Concat(Enumerable.Repeat("连续的中文动态。",650));
        // Current desktop writes AgentMessage/Text first, then response_item/output_text.
        activity.Accept(Record(new{type="item_completed",turn_id="live",item=new{type="AgentMessage",id="public-capital",phase="commentary",content=new[]{new{type="Text",text="实时公开进度"}}}}));
        activity.Accept(Record(new{type="message",role="assistant",id="public-capital",phase="commentary",content=new[]{new{type="output_text",text="实时公开进度"}}},"response_item"));
        activity.Accept(Record(new{type="item_completed",turn_id="live",item=new{type="AgentMessage",id="public-later",phase="commentary",content=new[]{new{type="unknown",text="PRIVATE_UNSUPPORTED"}}}}));
        activity.Accept(Record(new{type="message",role="assistant",id="public-later",phase="commentary",content=new[]{new{type="output_text",text="稍后可读副本"}}},"response_item"));
        if(activity.View()!.Text.Split("实时公开进度").Length!=2||!activity.View()!.Text.Contains("稍后可读副本")||activity.View()!.Text.Contains("PRIVATE_UNSUPPORTED"))
            throw new Exception("Desktop Text records must be readable once; unsupported copies cannot consume message IDs");
        activity.Accept(Record(new{type="message",role="assistant",id="long",phase="commentary",content=new[]{new{type="output_text",text=longText}}},"response_item"));
        string allPages=string.Concat(Enumerable.Range(0,activity.View()!.PageCount).Select(p=>activity.View(p)!.Text));
        if(!allPages.Contains(longText)||Enumerable.Range(0,activity.View()!.PageCount).Any(p=>System.Text.Encoding.UTF8.GetByteCount(activity.View(p)!.Text)>2200))
            throw new Exception("Activity pagination must preserve full public text within the transport bound");
        activity.Accept(Record(new{type="task_complete",turn_id="live",last_agent_message="已修复"}));
        if(activity.View()!.State!="completed"||!activity.View()!.Text.Contains("已修复"))throw new Exception("Activity completion lifecycle");
        var testHome=Path.Combine(Path.GetTempPath(),"tab5-activity-"+Guid.NewGuid().ToString("N"));
        var sessionDirectory=Path.Combine(testHome,"sessions");Directory.CreateDirectory(sessionDirectory);
        var sessionPath=Path.Combine(sessionDirectory,"synthetic.jsonl");
        try {
            var incremental=new Tab5CodexActivity();
            string start=Record(new{type="task_started",turn_id="incremental"});
            File.WriteAllText(sessionPath,start+"\r\n"+Record(progress,"response_item"));
            if(incremental.Read(sessionPath,testHome)!.Text.Contains("正在检查接口"))throw new Exception("Partial line must wait for completion");
            File.AppendAllText(sessionPath,"\n");
            if(!incremental.Read(sessionPath,testHome)!.Text.Contains("正在检查接口")||incremental.Read(sessionPath,testHome)!.Text.Split("正在检查接口").Length!=2)
                throw new Exception("Incremental CRLF/LF reads must neither drop nor duplicate entries");
            File.AppendAllText(sessionPath,Record(new{type="task_started",turn_id="orphan-cli"})+"\n"+
                Record(new{type="task_complete",turn_id="orphan-cli",last_agent_message="不属于桌面会话的答复"})+"\n");
            var desktopOnly=incremental.Read(sessionPath,testHome,-1,"incremental")!;
            if(desktopOnly.TurnId!="incremental"||desktopOnly.Text.Contains("不属于")||!desktopOnly.Text.Contains("正在检查接口"))
                throw new Exception("Rollout must retain the desktop-owned turn and ignore orphan CLI turns");
        }finally{Directory.Delete(testHome,true);}
        int readCalls=0;
        var history=JsonSerializer.Serialize(new {type="event_msg",payload=new {type="task_complete",turn_id="older",last_agent_message="旧答复"}})+"\n"+
            JsonSerializer.Serialize(new {type="response_item",payload=new {type="reasoning",text="不可显示的内部内容"}})+"\n"+
            "{incomplete\n"+JsonSerializer.Serialize(new {type="event_msg",payload=new {type="task_complete",turn_id="current",last_agent_message="本轮答复"}});
        if(Tab5CodexReplyHistory.Parse(history,"current")!="本轮答复"||Tab5CodexReplyHistory.Parse(history,"missing") is not null||
           Tab5CodexReplyHistory.Read(Path.Combine(Path.GetTempPath(),"credentials.jsonl"),"current",Tab5CodexCatalog.HomePath) is not null)
            throw new Exception("History fallback must isolate the requested turn and allowed directory");
        string readState="completed";
        using(var reader=new Tab5CodexTasks((method,_,_)=> {
            readCalls++;
            if(method=="thread/read")return Task.FromResult(JsonSerializer.SerializeToElement(new{result=new{thread=new{status=new{type="idle"},turns=Array.Empty<object>()}}}));
            if(method=="thread/turns/list")return Task.FromResult(JsonSerializer.SerializeToElement(new{result=new{data=new[]{new{id="synthetic-turn",status=readState}},nextCursor=(string?)null}}));
            if(method=="thread/turns/items/list")return Task.FromResult(JsonSerializer.SerializeToElement(new{result=new{data=readState=="completed"?new[]{new{turnId="synthetic-turn",item=new{type="agentMessage",text="已有会话的回复"}}}:Array.Empty<object>(),nextCursor=(string?)null}}));
            throw new Exception("History read must never resume or send a turn");
        },()=>[new(taskId,"Synthetic","test",0)])) {
            if((await reader.ReadAsync(Guid.NewGuid().ToString(),CancellationToken.None)).Status!=404||readCalls!=0)
                throw new Exception("History read must reject unknown tasks before RPC");
            if((await reader.ReadAsync(taskId,CancellationToken.None)).Status!=200||readCalls!=3||
               JsonSerializer.SerializeToElement(reader.Snapshot()).GetProperty("responses").GetProperty(taskId).GetString()!="已有会话的回复")
                throw new Exception("Read-only history reaches the device snapshot");
            readState="inProgress";await reader.ReadAsync(taskId,CancellationToken.None);
            if(JsonSerializer.SerializeToElement(reader.Snapshot()).GetProperty("repliesReady").EnumerateObject().Any())
                throw new Exception("Reading existing history must not announce a newly received reply");
            if(JsonSerializer.SerializeToElement(reader.Snapshot()).GetProperty("responses").GetProperty(taskId).GetString()=="已有会话的回复")
                throw new Exception("A new empty turn cannot be presented as its previous answer");
        }
        async Task Verify(string name,string failureAt,Func<JsonElement> response,int expected,string? error,int expectedStarts) {
            int starts=0;
            using var bridge=new Tab5CodexTasks((method,_,_)=> {
                if(method=="turn/start")starts++;
                if(method==failureAt)return Task.FromResult(response());
                return Task.FromResult(method=="thread/turns/list"?DirectoryPage():method=="thread/turns/list"?DirectoryPage():method=="thread/turns/items/list"?Json("{\"result\":{\"data\":[],\"nextCursor\":null}}"):idle);
            },()=>[new(taskId,"Synthetic task","test",0)]);
            var result=await bridge.SubmitAsync(taskId,"Synthetic input",CancellationToken.None);
            var body=JsonSerializer.SerializeToElement(result.Body);
            if(result.Status!=expected||starts!=expectedStarts||
                (error is not null&&body.GetProperty("error").GetString()!=error))
                throw new InvalidOperationException("TAB5 Codex: "+name);
        }
        await Verify("disconnect before dispatch is unavailable","thread/read",()=>throw new IOException(),503,"codex_unavailable",0);
        await Verify("timeout before dispatch is unavailable","thread/resume",()=>throw new TimeoutException(),503,"codex_unavailable",0);
        await Verify("missing resume state cannot send","thread/resume",()=>Json("{\"result\":{}}"),409,"task_busy_or_state_unknown",0);
        await Verify("non-object response cannot send","thread/read",()=>Json("null"),409,"task_busy_or_state_unknown",0);
        await Verify("lost acknowledgement is uncertain","turn/start",()=>throw new IOException(),504,"delivery_unknown",1);
        await Verify("cancelled wait is uncertain","turn/start",()=>throw new OperationCanceledException(),504,"delivery_unknown",1);
        await Verify("missing turn ID is uncertain","turn/start",()=>Json("{\"result\":{\"turn\":{}}}"),504,"delivery_unknown",1);
        await Verify("wrong turn ID type is uncertain","turn/start",()=>Json("{\"result\":{\"turn\":{\"id\":3}}}"),504,"delivery_unknown",1);
        await Verify("explicit rejection is not acceptance","turn/start",()=>Json("{\"error\":{\"code\":-1}}"),503,"turn_start_failed",1);
        await Verify("valid acknowledgement is accepted","turn/start",()=>Json("{\"result\":{\"turn\":{\"id\":\"synthetic-turn\"}}}"),202,null,1);
        var startedCall=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgement=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var bridge=new Tab5CodexTasks((method,_,_)=> {
            if(method=="turn/start"){startedCall.SetResult();return acknowledgement.Task;}
            return Task.FromResult(method=="thread/turns/list"?DirectoryPage():method=="thread/turns/list"?DirectoryPage():method=="thread/turns/items/list"?Json("{\"result\":{\"data\":[],\"nextCursor\":null}}"):idle);
        },()=>[new(taskId,"Synthetic task","test",0)])) {
            var submission=bridge.SubmitAsync(taskId,"Synthetic input",CancellationToken.None);
            await startedCall.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bridge.Completed(taskId,"turn-early","completed","提前完成的回复");
            acknowledgement.SetResult(Json("{\"result\":{\"turn\":{\"id\":\"turn-early\"}}}"));
            if((await submission).Status!=202)throw new InvalidOperationException("TAB5 Codex: early completion acknowledgement");
            var receipts=JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("receipts");
            if(!receipts.GetProperty(taskId).GetString()!.StartsWith("Codex 已完成"))
                throw new InvalidOperationException("TAB5 Codex: early completion must survive submit acknowledgement");
            if(JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("responses").GetProperty(taskId).GetString()!="提前完成的回复")
                throw new InvalidOperationException("TAB5 Codex: early final response must survive acknowledgement");
            bridge.Completed(taskId,"older-turn","failed");
            var ready=JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("repliesReady");
            if(ready.GetProperty(taskId).GetString()!="turn-early")throw new Exception("Early reply notice must identify the accepted turn");
            bridge.Completed(taskId,"turn-early","completed","重复完成通知");
            if(JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("repliesReady").GetProperty(taskId).GetString()!="turn-early")
                throw new Exception("Repeated completion must retain the same notice identity");
            receipts=JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("receipts");
            if(!receipts.GetProperty(taskId).GetString()!.StartsWith("Codex 已完成"))
                throw new InvalidOperationException("TAB5 Codex: old turn cannot replace current receipt");
        }
        var approvalStart=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalAck=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var bridge=new Tab5CodexTasks((method,_,_)=> {
            if(method=="turn/start"){approvalStart.SetResult();return approvalAck.Task;}
            return Task.FromResult(method=="thread/turns/list"?DirectoryPage():method=="thread/turns/list"?DirectoryPage():method=="thread/turns/items/list"?Json("{\"result\":{\"data\":[],\"nextCursor\":null}}"):idle);
        },()=>[new(taskId,"Synthetic task","test",0)])) {
            var submission=bridge.SubmitAsync(taskId,"Synthetic input",CancellationToken.None);
            await approvalStart.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bridge.InteractionRequired(taskId,"权限操作已取消，请在电脑端重试");
            bridge.Completed(taskId,"approval-turn","interrupted");
            approvalAck.SetResult(Json("{\"result\":{\"turn\":{\"id\":\"approval-turn\"}}}"));
            if((await submission).Status!=202)throw new InvalidOperationException("TAB5 Codex: approval acknowledgement");
            bridge.Completed(taskId,"approval-turn","interrupted");
            var receipt=JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("receipts").GetProperty(taskId).GetString();
            if(receipt!="权限操作已取消，请在电脑端重试")
                throw new InvalidOperationException("TAB5 Codex: early approval must survive acknowledgement and interruption");
            if(JsonSerializer.SerializeToElement(bridge.Snapshot()).GetProperty("repliesReady").EnumerateObject().Any())
                throw new Exception("Interrupted approval is not a new reply");
        }
        long readClock=0;
        var desktopRead=new Tab5CodexReadState(()=>new("synthetic-account",[]),()=>readClock);
        using(var bridge=new Tab5CodexTasks((method,_,_)=>Task.FromResult(method=="turn/start"?
            Json("{\"result\":{\"turn\":{\"id\":\"synthetic-turn\"}}}"):method=="thread/turns/list"?DirectoryPage():method=="thread/turns/items/list"?Json("{\"result\":{\"data\":[],\"nextCursor\":null}}"):idle),()=>[new(taskId,"Synthetic task","test",0)],desktopRead))
        using(var input=new MemoryStream())
        using(var writer=new StreamWriter(input))
        using(var output=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("null\n{not-json\n{\"method\":42}\n{\"method\":\"turn/completed\",\"params\":null}\n"+
            JsonSerializer.Serialize(new{method="turn/completed",@params=new{threadId=taskId,turn=new{id="synthetic-turn",status="completed",items=new[]{new{id="answer",type="agentMessage",text="已修好，可以查看回复"}}}}})+"\n")))
        using(var reader=new StreamReader(output)) {
            if((await bridge.SubmitAsync(taskId,"Synthetic input",CancellationToken.None)).Status!=202)
                throw new InvalidOperationException("TAB5 Codex: synthetic submission failed");
            await bridge.ReadOutputAsync(reader,writer);
            var snapshot=JsonSerializer.SerializeToElement(bridge.Snapshot());
            if(!snapshot.GetProperty("receipts").GetProperty(taskId).GetString()!.StartsWith("Codex 已完成"))
                throw new InvalidOperationException("TAB5 Codex: malformed notification must not hide later completion");
            if(snapshot.GetProperty("responses").GetProperty(taskId).GetString()!="已修好，可以查看回复")
                throw new InvalidOperationException("TAB5 Codex: final reply must be visible to TAB5");
            if(snapshot.GetProperty("repliesReady").GetProperty(taskId).GetString()!="synthetic-turn")
                throw new Exception("Completed submitted turn must carry a reply notice");
            readClock=5000;
            snapshot=JsonSerializer.SerializeToElement(bridge.SnapshotLimited(1));
            if(snapshot.GetProperty("repliesRead").GetProperty(taskId).GetString()!="synthetic-turn")
                throw new Exception("Desktop read acknowledgement lost during snapshot limiting");
            var status=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0));
            using var wire=JsonDocument.Parse(Tab5Protocol.Snapshot(status,"test","session",1,codexTasks:snapshot));
            if(wire.RootElement.GetProperty("data").GetProperty("codexTasks").GetProperty("repliesRead").GetProperty(taskId).GetString()!="synthetic-turn")
                throw new Exception("Desktop read acknowledgement lost on shared transport wire");
        }
        using(var empty=new Tab5CodexTasks((method,_,_)=>Task.FromResult(method=="turn/start"?
            Json("{\"result\":{\"turn\":{\"id\":\"empty-turn\"}}}"):method=="thread/turns/list"?DirectoryPage():method=="thread/turns/items/list"?Json("{\"result\":{\"data\":[],\"nextCursor\":null}}"):idle),()=>[new(taskId,"Synthetic","test",0)])) {
            await empty.SubmitAsync(taskId,"Synthetic",CancellationToken.None);
            await empty.ReadAsync(taskId,CancellationToken.None); // installs an empty-history placeholder
            empty.Completed(taskId,"empty-turn","completed");
            if(JsonSerializer.SerializeToElement(empty.Snapshot()).GetProperty("repliesReady").EnumerateObject().Any())
                throw new Exception("An empty completion must not announce a placeholder as a reply");
        }
        Console.WriteLine("TAB5_CODEX_SELF_TEST_OK dispatch failures, uncertain delivery, malformed replies, no automatic retry (synthetic RPC only)");
    }

    private static async Task VerifyOverviewFallbackAsync() {
        string home=Path.Combine(Path.GetTempPath(),"aibot-overview-"+Guid.NewGuid().ToString("N"));
        string locks=Path.Combine(home,"thread-writer-locks");Directory.CreateDirectory(locks);
        string older=Guid.NewGuid().ToString(),newer=Guid.NewGuid().ToString();
        string writerPath=Path.Combine(locks,older+".lock");File.WriteAllBytes(writerPath,[]);
        string dbPath=Path.Combine(home,"thread_history_1.sqlite");
        void Require(bool value,string message){if(!value)throw new InvalidOperationException("Overview: "+message);}
        void Sql(string query) {
            nint db=0;
            try {
                Require(sqlite3_open_v2(dbPath,out db,6,0)==0,"fixture open");
                Require(sqlite3_exec(db,query,0,0,0)==0,"fixture SQL");
            }finally{if(db!=0)sqlite3_close(db);}
        }
        try {
            Sql("CREATE TABLE thread_turns(thread_id TEXT,turn_id TEXT,root_turn_id TEXT,rollout_ordinal INTEGER,status TEXT,started_at INTEGER,completed_at INTEGER);"+
                $"INSERT INTO thread_turns VALUES('{older}','root','root',1,'inProgress',101,NULL),('{older}','child','root',2,'completed',102,103),('{newer}','other','other',3,'inProgress',105,NULL);");
            string? Local(string task,long launch=100)=>Tab5CodexCatalog.ReadLocalDisplayActivity(home,task,launch,110);
            Require(Local(older) is null,"unlocked file is not an active writer");
            using var writer=new FileStream(writerPath,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite|FileShare.Delete);
            writer.Lock(0,1);
            Require(Local(older)=="working"&&Local(newer) is null&&Local(older,102) is null,"exact root, writer and current desktop lifetime required");
            Sql("UPDATE thread_turns SET status='interrupted' WHERE turn_id='root'");
            Require(Local(older) is null,"missing end cannot imply idle");
            Sql("UPDATE thread_turns SET status='completed',completed_at=108 WHERE turn_id='root'");
            Require(Local(older)=="idle","explicit completion establishes idle");
            writer.Unlock(0,1);
            Require(Local(older) is null,"completed data without a current writer is not reliable");
            writer.Lock(0,1);
            Sql("UPDATE thread_turns SET completed_at=111 WHERE turn_id='root'");
            Require(Local(older) is null,"future completion is not reliable");
            Sql("UPDATE thread_turns SET status='inProgress',completed_at=NULL WHERE turn_id='root'");
            Console.WriteLine("ALIGNMENT-PASS WinOverviewLocalEvidence");

            Tab5CodexTask[] catalog=[new(newer,"最新会话","测试",2),new(older,"工作会话","测试",1)];
            string mode="live";long tick=100;int localReads=0;
            var live=new Tab5LiveActivity((id,_)=>mode switch {
                "live"=>Task.FromResult(new Tab5CodexDesktop.State(id==newer?"active":"idle","",default,false)),
                "unavailable"=>Task.FromException<Tab5CodexDesktop.State>(new TimeoutException()),
                _=>Task.FromException<Tab5CodexDesktop.State>(new Tab5CodexDesktop.Rejected("desktop_snapshot_incompatible"))
            },id=>{Interlocked.Increment(ref localReads);return Local(id);},()=>tick);
            // Initial refresh sets the production scheduler gate; subsequent
            // fixture transitions refresh synchronously without a real desktop.
            live.Refresh(catalog,CancellationToken.None);
            for(int i=0;i<200&&live.Fresh().Count!=2;i++)await Task.Delay(10);
            Require(live.Fresh().Count==2,"initial live refresh");
            using var bridge=new Tab5CodexTasks(new Tab5CodexDesktop("aibot-overview-"+Guid.NewGuid().ToString("N")),()=>catalog,
                rolloutPath:_=>null,historyHome:home,liveActivity:live);
            JsonElement Snapshot()=>JsonSerializer.SerializeToElement(bridge.SnapshotLimited(1));
            void Overview(string id,string companion,string title) {
                var snapshot=Snapshot();var overview=snapshot.GetProperty("overviewTask");
                Require(overview.GetProperty("Id").GetString()==id&&overview.GetProperty("Title").GetString()==title&&
                    overview.GetProperty("CompanionState").GetString()==companion,"visible overview title and companion: "+companion);
                Require(snapshot.GetProperty("tasks")[0].GetProperty("Id").GetString()==id,"overview retained under transport limit");
            }
            Overview(newer,"working","最新会话");
            Require(localReads==0,"live snapshot takes precedence over disk");
            mode="unavailable";await live.RefreshAsync(catalog,CancellationToken.None);
            Overview(older,"working","工作会话");
            var fallback=Snapshot();
            Require(fallback.GetProperty("overviewTask").GetProperty("State").GetString()=="unknown"&&
                fallback.GetProperty("control").ValueKind==JsonValueKind.Null&&
                !fallback.GetProperty("repliesReady").EnumerateObject().Any()&&live.Fresh().Count==0,
                "display fallback cannot authorize control, publish replies or affect global live state");
            Sql("UPDATE thread_turns SET status='completed',completed_at=109 WHERE turn_id='root'");
            await live.RefreshAsync([catalog[1]],CancellationToken.None);
            // With no active task, the newer task is selected. Its missing
            // writer cannot invent an idle or working state.
            Overview(newer,"unknown","最新会话");
            var both=JsonSerializer.SerializeToElement(bridge.SnapshotLimited(2)).GetProperty("tasks");
            Require(both.EnumerateArray().Single(t=>t.GetProperty("Id").GetString()==older).GetProperty("CompanionState").GetString()=="idle",
                "explicit completion reaches the same task's companion");
            catalog[1]=catalog[1] with {UpdatedAt=3};
            Overview(older,"idle","工作会话");
            Require(!Snapshot().GetProperty("repliesReady").EnumerateObject().Any()&&
                Snapshot().GetProperty("control").ValueKind==JsonValueKind.Null,"local completion does not announce replies or enable control");
            catalog[1]=catalog[1] with {UpdatedAt=1};
            Sql("UPDATE thread_turns SET status='inProgress',completed_at=NULL WHERE turn_id='root'");
            await live.RefreshAsync(catalog,CancellationToken.None);
            Overview(older,"working","工作会话");
            tick+=15000;Overview(newer,"unknown","最新会话");
            await live.RefreshAsync(catalog,CancellationToken.None);
            writer.Unlock(0,1);await live.RefreshAsync(catalog,CancellationToken.None);
            Overview(newer,"unknown","最新会话");
            writer.Lock(0,1);mode="incompatible";await live.RefreshAsync(catalog,CancellationToken.None);
            Overview(newer,"unknown","最新会话");
            var incompatible=Snapshot();
            Require(incompatible.GetProperty("overviewTask").GetProperty("State").GetString()=="unknown"&&
                incompatible.GetProperty("control").ValueKind==JsonValueKind.Null&&
                !incompatible.GetProperty("repliesReady").EnumerateObject().Any()&&live.Fresh().Count==0,
                "incompatible live interface must neither use local display state nor grant control");
            Console.WriteLine("ALIGNMENT-PASS WinOverviewDisplayOnly");
            mode="live";await live.RefreshAsync(catalog,CancellationToken.None);
            Overview(newer,"working","最新会话");
            Console.WriteLine("ALIGNMENT-PASS WinOverviewLiveFallbackTransitions");
        }finally{Directory.Delete(home,true);}
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out nint db,int flags,nint vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,nint callback,nint arg,nint error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_close(nint db);
}
