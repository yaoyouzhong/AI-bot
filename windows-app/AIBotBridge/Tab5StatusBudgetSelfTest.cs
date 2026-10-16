using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5StatusBudgetSelfTest
{
    internal static async Task RunAsync()
    {
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("TAB5 status budget: "+name);}
        var catalog=Enumerable.Range(0,80).Select(i=>new Tab5CodexTask(Guid.NewGuid().ToString(),new string('题',60),"测试项目 "+i/8,1000-i,"project-"+i/8)).ToArray();
        long clock=0;string turn=Guid.NewGuid().ToString(),reply=new('答',730);
        var unread=catalog.Take(40).Select(t=>t.Id).ToHashSet();
        var readState=new Tab5CodexReadState(()=>new("budget-test",unread),()=>clock);
        Task<JsonElement> Call(string method,object args,CancellationToken token)=>Task.FromResult(JsonSerializer.SerializeToElement(method=="turn/start"
            ?(object)new{result=new{turn=new{id=turn}}}
            :new{result=new{thread=new{status=new{type="idle"},turns=new[]{new{id=turn,status="completed",completedAt=1,items=new[]{new{type="agentMessage",phase="final_answer",text=reply}}}}}}}));
        using var tasks=new Tab5CodexTasks(Call,()=>catalog,readState);
        foreach(var task in catalog) {
            Check((await tasks.SubmitAsync(task.Id,"synthetic fixture",CancellationToken.None)).Status==202,"mock turn accepted");
            tasks.Completed(task.Id,turn,"completed",reply);
        }
        Check((await tasks.ReadAsync(catalog[^1].Id,CancellationToken.None)).Status==200,"viewed reply loaded");
        _=tasks.Snapshot();clock=5000;
        var limited=JsonSerializer.SerializeToElement(tasks.SnapshotLimited(24),JsonDefaults.Options);
        var ids=limited.GetProperty("tasks").EnumerateArray().Select(t=>t.GetProperty("id").GetString()!).ToHashSet();
        Check(ids.Contains(catalog[0].Id)&&ids.Contains(catalog[^1].Id),"overview and viewed task retained");
        foreach(string map in new[]{"repliesReady","repliesRead","repliesUnread","receipts","responses"})
            Check(limited.GetProperty(map).EnumerateObject().All(p=>ids.Contains(p.Name)),map+" bounded to transmitted tasks");
        var restored=JsonSerializer.SerializeToElement(tasks.Snapshot(),JsonDefaults.Options);
        Check(restored.GetProperty("repliesRead").EnumerateObject().Count()==40&&restored.GetProperty("repliesUnread").EnumerateObject().Count()==40,"budgeting preserves complete desktop read state");
        string directory=Path.Combine(Path.GetTempPath(),"tab5-budget-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var store=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=store.Pair("001122334455",@"USB\BUDGET_TEST");
            using var service=new Tab5Service(store,tasks);
            var now=DateTimeOffset.UtcNow;
            var snapshot=new StatusSnapshot(1,"12:00:00",now.ToUnixTimeSeconds(),28800,now,new("working",0),new("idle",0));
            var health=new RuntimeHealthSnapshot("health",1,now,[new("vpn","网络","error","连接失败","中文详情 <script> & 😀","检查连接",now)],[]);
            snapshot=snapshot with {RuntimeHealth=health};
            byte[] plain=Tab5Protocol.Snapshot(snapshot,pair.DeviceId,"unicode",1,codexTasks:tasks.Snapshot());
            using(var decoded=JsonDocument.Parse(plain)) {
                Check(decoded.RootElement.GetProperty("data").GetProperty("runtimeHealth").GetProperty("checks")[0].GetProperty("detail").GetString()==health.Checks[0].Detail,"Unicode and HTML-sensitive characters round trip");
                Check(Encoding.UTF8.GetString(plain).Contains("中文详情"),"Chinese travels as UTF-8");
            }
            // Make the non-catalog payload leave room for fewer than 24 tasks.
            // The published frame must still include both the current overview
            // and viewed reply, with fresh sequence/time, on every transport.
            snapshot=snapshot with {RuntimeHealth=health with {Checks=[health.Checks[0] with {Detail=new string('x',21000)}]}};
            for(int iteration=0;iteration<3;iteration++) {
                snapshot=snapshot with {EpochUtc=snapshot.EpochUtc+1,CapturedAt=snapshot.CapturedAt.AddSeconds(1)};
                service.Publish(snapshot);
                byte[] frame=service.CurrentFrame??throw new Exception("Over-budget catalog stopped fresh publication: "+service.Summary);
                Check(frame.Length+28<=Tab5Protocol.MaximumFrame,"encrypted frame fits 32 KiB");
                using var state=JsonDocument.Parse(frame);var data=state.RootElement.GetProperty("data");
                Check(data.GetProperty("epochUtc").GetInt64()==snapshot.EpochUtc,"fresh data continues after overflow");
                var sent=data.GetProperty("codexTasks");var sentIds=sent.GetProperty("tasks").EnumerateArray().Select(t=>t.GetProperty("id").GetString()!).ToHashSet();
                Check(sentIds.Count<24&&sentIds.Contains(catalog[0].Id)&&sentIds.Contains(catalog[^1].Id),"budget can shrink below 24 without losing current tasks");
                Check(sent.GetProperty("responses").EnumerateObject().Any(),"reply content retained");
                string nonce=Guid.NewGuid().ToString("N");byte[] key=Convert.FromBase64String(pair.Key);
                Check(Tab5Protocol.Decrypt(key,nonce,Tab5Protocol.Encrypt(key,nonce,frame)).SequenceEqual(frame),"shared USB/Wi-Fi/BLE encrypted frame round trip");
            }
            // A payload too big even with two tasks must self-recover without
            // a process/device restart, including when no baseline exists yet.
            using var recovery=new Tab5Service(store,tasks);
            var normal=snapshot with {RuntimeHealth=health};recovery.Publish(normal);
            using var before=JsonDocument.Parse(recovery.CurrentFrame!);
            long sequence=before.RootElement.GetProperty("sequence").GetInt64();
            int normalTasks=before.RootElement.GetProperty("data").GetProperty("codexTasks").GetProperty("tasks").GetArrayLength();
            var huge=normal with {EpochUtc=normal.EpochUtc+30,RuntimeHealth=health with {Checks=[health.Checks[0] with {Detail=new string('坏',40000)}]}};
            for(int failure=0;failure<3;failure++) {
                recovery.Publish(huge with {EpochUtc=huge.EpochUtc+failure});
                byte[] recovered=recovery.CurrentFrame??throw new Exception("Recovery heartbeat stopped");
                using var received=JsonDocument.Parse(recovered);var data=received.RootElement.GetProperty("data");
                Check(recovered.Length+28<=Tab5Protocol.MaximumFrame&&received.RootElement.GetProperty("sequence").GetInt64()>sequence,"oversize fallback advances bounded authenticated status");
                sequence=received.RootElement.GetProperty("sequence").GetInt64();
                Check(data.GetProperty("epochUtc").GetInt64()==huge.EpochUtc+failure,"recovery clock stays live");
                Check(data.GetProperty("runtimeHealth").GetProperty("epochUtc").GetInt64()==health.EpochUtc,"cached monitor sample timestamp is not renewed");
            }
            Check(recovery.Summary.Contains("degraded=True")&&recovery.Summary.Contains("fallbacks=1"),"one degradation episode diagnosed: "+recovery.Summary);
            recovery.Publish(normal);
            Check(recovery.Summary.Contains("degraded=False")&&recovery.Summary.Contains("recoveries=1"),"full state recovers automatically on next normal publication");
            using(var full=JsonDocument.Parse(recovery.CurrentFrame!))
                Check(full.RootElement.GetProperty("data").GetProperty("codexTasks").GetProperty("tasks").GetArrayLength()==normalTasks&&normalTasks>24,"normal catalog capacity restored after pressure ends");
            using var cold=new Tab5Service(store,tasks);cold.Publish(huge);
            byte[] boot=cold.CurrentFrame??throw new Exception("Oversize at startup blocked all channels");
            Check(boot.Length+28<=Tab5Protocol.MaximumFrame,"cold-start recovery is bounded");
            using(var received=JsonDocument.Parse(boot))
                Check(!received.RootElement.GetProperty("data").TryGetProperty("runtimeHealth",out var monitor)||monitor.ValueKind==JsonValueKind.Null,"unavailable monitor remains unavailable, not healthy");
            cold.Publish(normal);Check(cold.Summary.Contains("recoveries=1"),"cold start restores full status without restart");
            var legacy=System.Text.Json.Nodes.JsonNode.Parse(Tab5Protocol.Snapshot(normal,pair.DeviceId,"old",1))!;
            legacy["data"]!["ota"]=JsonSerializer.SerializeToNode(new{command="old-install"});
            legacy["data"]!["connectionHealth"]=JsonSerializer.SerializeToNode(new{displayCommand="old-display"});
            legacy["data"]!["padding"]="";
            int empty=JsonSerializer.SerializeToUtf8Bytes(legacy,Tab5Protocol.StatusJson).Length;
            legacy["data"]!["padding"]=new string('x',Tab5Protocol.MaximumFrame-28-empty);
            byte[] nearLimit=JsonSerializer.SerializeToUtf8Bytes(legacy,Tab5Protocol.StatusJson);
            Check(nearLimit.Length==Tab5Protocol.MaximumFrame-28,"legacy maximum-sized baseline fixture");
            byte[] bootstrapped=Tab5StatusRecovery.Build(Tab5Protocol.Snapshot(huge,pair.DeviceId,"new",2),nearLimit,out _);
            Check(bootstrapped.Length+28<=Tab5Protocol.MaximumFrame,"recovery never grows a maximum-sized legacy baseline past the limit");
            using(var received=JsonDocument.Parse(bootstrapped)) {
                Check(received.RootElement.GetProperty("session").GetString()=="new","recovery uses current authenticated session");
                Check(received.RootElement.GetProperty("data").GetProperty("ota").ValueKind==JsonValueKind.Null&&
                    received.RootElement.GetProperty("data").GetProperty("connectionHealth").ValueKind==JsonValueKind.Null,"old cached commands are not replayed");
            }
            Console.WriteLine("TAB5_STATUS_BUDGET_OK 80 completed tasks, 40 read/40 unread, UTF-8 fidelity, below-24 fallback, viewed reply, repeated fresh publication, encrypted wire limit");
            Console.WriteLine("TAB5_STATUS_SELF_RECOVERY_OK normal/oversize/fresh fallback/automatic full recovery, cached sample timestamps, one episode, cold start, no restart");
        }finally{Directory.Delete(directory,true);}
    }
}
