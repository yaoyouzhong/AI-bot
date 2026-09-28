using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CodexSelfTest
{
    internal static async Task RunAsync() {
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
        Tab5SavedProject[] saved=[new("work","Work",workRoot),new("boss","boss-resume-filter",bossRoot)];
        if(Tab5CodexCatalog.MatchProject(workRoot,saved)?.Id!="work"||
           Tab5CodexCatalog.MatchProject(bossRoot,saved)?.Id!="boss"||
           Tab5CodexCatalog.MatchProject(Path.Combine(bossRoot,"src"),saved)?.Id!="boss"||
           Tab5CodexCatalog.MatchProject(Path.Combine(workRoot,"hello"),saved) is not null||
           Tab5CodexCatalog.MatchProject(Path.Combine(Path.GetTempPath(),"hello","work"),saved) is not null||
           Tab5CodexCatalog.MatchProject(Path.Combine(Tab5CodexCatalog.HomePath,"worktrees","44ed","boss-resume-filter"),saved)?.Id!="boss")
            throw new InvalidOperationException("TAB5 Codex: only saved project roots and matching worktrees appear");
        var projectTasks=Enumerable.Range(0,10).Select(i=>new Tab5CodexTask(Guid.NewGuid().ToString(),"Project A task "+i,"Project A",100-i,"aaaaaaaaaaaaaaaa"))
            .Concat([new Tab5CodexTask(Guid.NewGuid().ToString(),"Project B task","Project B",50,"bbbbbbbbbbbbbbbb")]).ToArray();
        var grouped=Tab5CodexCatalog.SelectRecent(projectTasks);
        if(grouped.Count!=9||grouped.Count(t=>t.ProjectId=="aaaaaaaaaaaaaaaa")!=8||grouped.Count(t=>t.ProjectId=="bbbbbbbbbbbbbbbb")!=1)
            throw new InvalidOperationException("TAB5 Codex: per-project history limit");
        using(var catalog=new Tab5CodexTasks((_,_,_)=>Task.FromResult(default(JsonElement)),()=>grouped)) {
            var snapshot=JsonSerializer.SerializeToElement(catalog.Snapshot());
            var projects=snapshot.GetProperty("projects");
            if(projects.GetArrayLength()!=2||projects[0].GetProperty("name").GetString()!="Project A"||
               projects[1].GetProperty("name").GetString()!="Project B"||snapshot.GetProperty("tasks").GetArrayLength()!=9)
                throw new InvalidOperationException("TAB5 Codex: project selector catalog");
        }
        using(var catalog=new Tab5CodexTasks((_,_,_)=>Task.FromResult(default(JsonElement)),()=>grouped.Select(t=>t with {ProjectOrder=t.Folder=="Project B"?0:1}).ToArray())) {
            var projects=JsonSerializer.SerializeToElement(catalog.Snapshot()).GetProperty("projects");
            if(projects[0].GetProperty("name").GetString()!="Project A")
                throw new InvalidOperationException("TAB5 Codex: project order must follow recent activity");
        }
        var idle=Json("{\"result\":{\"thread\":{\"status\":{\"type\":\"idle\"},\"turns\":[{\"status\":\"completed\",\"completedAt\":1}]}}}");
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
            if(method!="thread/read")throw new Exception("History read must never resume or send a turn");
            readCalls++;
            return Task.FromResult(JsonSerializer.SerializeToElement(new {result=new {thread=new {
                status=new {type="idle"},turns=new[] {new {status=readState,items=readState=="completed"?new[]{new {type="agentMessage",text="已有会话的回复"}}:Array.Empty<object>()}}
            }}}));
        },()=>[new(taskId,"Synthetic","test",0)])) {
            if((await reader.ReadAsync(Guid.NewGuid().ToString(),CancellationToken.None)).Status!=404||readCalls!=0)
                throw new Exception("History read must reject unknown tasks before RPC");
            if((await reader.ReadAsync(taskId,CancellationToken.None)).Status!=200||readCalls!=1||
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
                return Task.FromResult(idle);
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
            return Task.FromResult(idle);
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
            return Task.FromResult(idle);
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
        using(var bridge=new Tab5CodexTasks((method,_,_)=>Task.FromResult(method=="turn/start"?
            Json("{\"result\":{\"turn\":{\"id\":\"synthetic-turn\"}}}"):idle),()=>[new(taskId,"Synthetic task","test",0)]))
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
        }
        using(var empty=new Tab5CodexTasks((method,_,_)=>Task.FromResult(method=="turn/start"?
            Json("{\"result\":{\"turn\":{\"id\":\"empty-turn\"}}}"):idle),()=>[new(taskId,"Synthetic","test",0)])) {
            await empty.SubmitAsync(taskId,"Synthetic",CancellationToken.None);
            await empty.ReadAsync(taskId,CancellationToken.None); // installs an empty-history placeholder
            empty.Completed(taskId,"empty-turn","completed");
            if(JsonSerializer.SerializeToElement(empty.Snapshot()).GetProperty("repliesReady").EnumerateObject().Any())
                throw new Exception("An empty completion must not announce a placeholder as a reply");
        }
        Console.WriteLine("TAB5_CODEX_SELF_TEST_OK dispatch failures, uncertain delivery, malformed replies, no automatic retry (synthetic RPC only)");
    }
}
