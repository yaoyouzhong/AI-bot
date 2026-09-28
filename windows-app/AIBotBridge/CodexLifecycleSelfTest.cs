using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class CodexLifecycleSelfTest
{
    internal static void Run()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, "artifacts", "lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "root.jsonl");
        const string meta = "{\"type\":\"session_meta\",\"payload\":{\"source\":\"cli\"}}\n";
        string Event(string type, string turn, DateTimeOffset time) => JsonSerializer.Serialize(new { type = "event_msg", timestamp = time, payload = new { type, turn_id = turn } }) + "\n";
        void Append(string target, ReadOnlySpan<byte> bytes) { using var file = new FileStream(target, FileMode.Append, FileAccess.Write); file.Write(bytes); }
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
        File.WriteAllText(path, meta + Event("task_complete", "old", DateTimeOffset.UtcNow.AddMinutes(-2)));
        var tracker = new CodexLifecycleTracker(()=>true);
        Check(CodexLifecycleTracker.ResolveState(7200,null,TimeSpan.Zero,true)=="idle","Open app without recent input became offline.");
        Check(CodexLifecycleTracker.ResolveState(null,null,TimeSpan.Zero,true)=="idle","Open app without session history became offline.");
        Check(CodexLifecycleTracker.ResolveState(70,"idle",TimeSpan.FromSeconds(70),true)=="idle","Completed turn reverted to working after 60 seconds.");
        Check(CodexLifecycleTracker.ResolveState(1,null,TimeSpan.Zero,false)=="offline","Closed app remained online from stale file timestamp.");
        Check(CodexLifecycleTracker.ResolveState(7200,"working",TimeSpan.FromSeconds(1),true)=="working","Explicit task start lost to old file timestamp.");
        Check(tracker.Capture(folder).CompletionSequence == 0, "Startup replayed historical completion.");
        Append(path, Encoding.UTF8.GetBytes(Event("task_started", "new", DateTimeOffset.UtcNow)));
        Check(tracker.Capture(folder).State == "working", "Explicit start not recognized.");
        var completed = Encoding.UTF8.GetBytes(Event("task_complete", "new", DateTimeOffset.UtcNow));
        Append(path, completed.AsSpan(0, completed.Length / 2));
        Check(tracker.Capture(folder).CompletionSequence == 0, "Partial JSON generated completion.");
        Append(path, completed.AsSpan(completed.Length / 2));
        var done = tracker.Capture(folder);
        Check(done.CompletionSequence == 1 && done.State == "idle", "Completed turn remains working.");
        Append(path, completed); Check(tracker.Capture(folder).CompletionSequence == 1, "Duplicate completion counted twice.");
        File.WriteAllText(Path.Combine(folder, "child.jsonl"), "{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{}}}}\n" + Event("task_complete", "child", DateTimeOffset.UtcNow));
        Check(tracker.Capture(folder).CompletionSequence == 1, "Subagent generated main completion.");
        Append(path, Encoding.UTF8.GetBytes(Event("task_complete", "another", DateTimeOffset.UtcNow)));
        Check(tracker.Capture(folder).CompletionSequence == 2, "Distinct same-second turn was lost.");
        File.WriteAllText(path, meta); Check(tracker.Capture(folder).CompletionSequence == 2, "Truncation replayed old events.");
        var overviewRoot=Path.Combine(folder,"overview");Directory.CreateDirectory(overviewRoot);
        string activeId=Guid.NewGuid().ToString(),idleId=Guid.NewGuid().ToString();
        string Meta(string id)=>JsonSerializer.Serialize(new {type="session_meta",payload=new {id,source="cli"}})+"\n";
        string activePath=Path.Combine(overviewRoot,"active.jsonl"),idlePath=Path.Combine(overviewRoot,"idle.jsonl");
        File.WriteAllText(activePath,Meta(activeId)+Event("task_started","running",DateTimeOffset.UtcNow.AddMinutes(-1))+
            JsonSerializer.Serialize(new {type="response_item",timestamp=DateTimeOffset.UtcNow,payload=new {type="function_call_output",output=new string('x',160000)}})+"\n");
        File.WriteAllText(idlePath,Meta(idleId)+Event("task_complete","done",DateTimeOffset.UtcNow));
        var taskTracker=new CodexLifecycleTracker(()=>true);taskTracker.Capture(overviewRoot);
        var states=taskTracker.TaskActivities.ToDictionary(t=>t.Id);
        Check(states[activeId].State=="working"&&states[idleId].State=="idle","Per-thread startup state or long-output scan failed");
        Tab5CodexTask[] catalog=[new(idleId,"Recent idle","",200),new(activeId,"Older working","",100)];
        Check(Tab5CodexTasks.SelectOverview(catalog,states)?.Id==activeId,"Recent idle hid working thread");
        states[idleId]=new(idleId,"working",states[activeId].UpdatedAt+1);
        Check(Tab5CodexTasks.SelectOverview(catalog,states)?.Id==idleId,"Newest working selection failed");
        Append(activePath,Encoding.UTF8.GetBytes(Event("task_complete","running",DateTimeOffset.UtcNow)));
        taskTracker.Capture(overviewRoot);states=taskTracker.TaskActivities.ToDictionary(t=>t.Id);
        Check(states[activeId].State=="idle","Completion did not clear working thread");
        Check(states[activeId].Completed&&states[activeId].TurnId=="running","Root completion turn identity lost");
        Append(activePath,Encoding.UTF8.GetBytes(Event("task_started","next",DateTimeOffset.UtcNow)));
        Append(activePath,Encoding.UTF8.GetBytes(Event("turn_aborted","next",DateTimeOffset.UtcNow)));
        taskTracker.Capture(overviewRoot);
        Check(!taskTracker.TaskActivities.Single(t=>t.Id==activeId).Completed,"Aborted turn advertised a completed reply");
        Check(Tab5CodexTasks.SelectOverview(catalog,new Dictionary<string,CodexLifecycleTracker.TaskActivity>())?.Id==idleId,"No active thread must select most recent catalog entry");
        var stopped=new CodexLifecycleTracker(()=>false);stopped.Capture(overviewRoot);
        Check(stopped.TaskActivities.All(t=>t.State!="working"),"Closed app advertised working thread");
        bool liveRunning=true,unavailable=false;
        var liveCache=new Tab5LiveActivity((id,ct)=>unavailable?Task.FromException<Tab5CodexDesktop.State>(new IOException("owner unavailable")):
            Task.FromResult(new Tab5CodexDesktop.State(liveRunning?"active":"idle","",JsonSerializer.SerializeToElement(new {turnId="current",status=liveRunning?"inProgress":"completed",turnStartedAtMs=100000}),false)));
        liveCache.RefreshAsync(catalog,CancellationToken.None).GetAwaiter().GetResult();
        Check(liveCache.Fresh().All(t=>t.State=="working"),"Desktop runtime must override stale rollout state");
        liveRunning=false;liveCache.RefreshAsync(catalog,CancellationToken.None).GetAwaiter().GetResult();
        Check(liveCache.Fresh().All(t=>t.State=="idle"),"Desktop completion must clear working state");
        Check(liveCache.Fresh().All(t=>t.Completed&&t.TurnId=="current"),"Desktop completion identity must reach devices for PC-started tasks");
        unavailable=true;liveCache.RefreshAsync(catalog,CancellationToken.None).GetAwaiter().GetResult();
        Check(liveCache.Fresh().Count==0,"Unavailable desktop owner must discard stale live state");
        Console.WriteLine("CODEX_LIFECYCLE_SELF_TEST_OK startup/explicit-state/partial/duplicate/subagent/same-second/truncate/overview-working-recent-long-output; no sound played");
    }
}
