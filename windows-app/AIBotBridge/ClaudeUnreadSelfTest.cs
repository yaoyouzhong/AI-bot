using System.Text.Json;

namespace AIBotBridge;

internal static class ClaudeUnreadSelfTest
{
    private sealed class Native(ClaudeSessionSource source):IClaudeNativeSession {
        public ClaudeSessionSource Source=>source;public string State {get;set;}="idle";public bool HasSessionApi=>true;
        public string Scope()=>"fixture";
        public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>throw new InvalidOperationException("No sends in unread test");
    }
    internal static async Task RunAsync()
    {
        string root=Path.Combine(Path.GetTempPath(),"aibot-unread-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Guid session=Guid.NewGuid(),user=Guid.NewGuid(),answer=Guid.NewGuid();
        string path=Path.Combine(root,session+".jsonl");
        void Write(object row)=>File.AppendAllText(path,JsonSerializer.Serialize(row)+"\n");
        void Prompt(Guid id,Guid? parent)=>Write(new{type="user",uuid=id,parentUuid=parent,sessionId=session,cwd=root,timestamp=DateTimeOffset.UtcNow,message=new{role="user",content="Owned unread test"}});
        void Reply(Guid id,Guid parent)=>Write(new{type="assistant",uuid=id,parentUuid=parent,sessionId=session,timestamp=DateTimeOffset.UtcNow,message=new{role="assistant",stop_reason="end_turn",content=new[]{new{type="text",text="Readable test reply"}}}});
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Claude unread: "+reason);}
        try {
            Prompt(user,null);Reply(answer,user);
            using var workspace=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),nativeFactory:_=>null);
            workspace.Start();
            for(int i=0;i<150&&workspace.Alias(session) is null;i++)await Task.Delay(20);
            string task=workspace.Alias(session)??throw new InvalidOperationException("Unread fixture catalog unavailable");
            async Task<(int Status,JsonElement Body)> Rpc(string op="read",string turn="",string? receipt=null,string device="device-a"){
                var result=await workspace.HandleAsync(JsonSerializer.SerializeToElement(new{op,taskId=task,turnId=turn,requestId=receipt??Guid.NewGuid().ToString()}),device,default);
                return(result.Status,JsonSerializer.SerializeToElement(result.Body));
            }
            var first=await Rpc();Check(first.Status==200,"initial read");
            string unread=first.Body.GetProperty("turnId").GetString()!;
            string initialReceipt=first.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()!;
            Check(initialReceipt.Length>0,"initial receipt missing");
            var ready=(Dictionary<string,string>)typeof(ClaudeWorkspace).GetField("_ready",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(workspace)!;
            var initialSnapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(!initialSnapshot.GetProperty("repliesReady").TryGetProperty(task,out _),"Reading an old reply invented a new notice");
            Check((await Rpc("read-ack",unread,initialReceipt)).Status==200,"Local unread could not be acknowledged after bridge restart");
            using(var restarted=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),nativeFactory:_=>null)){
                restarted.Start();for(int i=0;i<150&&restarted.Alias(session) is null;i++)await Task.Delay(20);
                var request=JsonSerializer.SerializeToElement(new{op="read",taskId=task,turnId=unread,requestId=Guid.NewGuid().ToString()});
                var reread=await restarted.HandleAsync(request,"device-a",default);
                var restartedSnapshot=JsonSerializer.SerializeToElement(restarted.Snapshot());
                Check(reread.Status==200&&!restartedSnapshot.GetProperty("repliesReady").TryGetProperty(task,out _)&&
                    !restartedSnapshot.GetProperty("repliesUnread").TryGetProperty(task,out _),"Restart and history reread resurrected a read reply");
            }
            // Simulate a real completion independently of reading it.
            ready[task]=unread;
            for(int i=0;i<90;i++){
                var poll=await Rpc();
                Check(poll.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()==initialReceipt,"polling exhausted or churned receipts");
            }
            Guid next=Guid.NewGuid();Prompt(next,answer);
            var empty=await Rpc();
            Check(empty.Status==200&&!empty.Body.GetProperty("replyView").GetProperty("hasText").GetBoolean()&&empty.Body.GetProperty("receipt").GetString()=="状态未知","empty latest reproduction");
            Check(empty.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()=="","empty turn received acknowledgement");
            var targeted=await Rpc(turn:unread);
            Check(targeted.Status==200&&targeted.Body.GetProperty("replyView").GetProperty("history").GetBoolean()&&targeted.Body.GetProperty("text").GetString()=="Readable test reply","unread target not rendered");
            string receipt=targeted.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()!;
            Check(receipt.Length>0,"historical unread cannot be acknowledged");
            Check((await Rpc("read-ack",unread,receipt,"device-b")).Status==409,"cross-device acknowledgement");
            Check((await Rpc("read-ack",Guid.NewGuid().ToString(),receipt)).Status==409,"wrong turn acknowledgement");
            Check((await Rpc("read-ack",unread,receipt)).Status==200,"exact historical acknowledgement failed");
            var snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(!snapshot.GetProperty("repliesUnread").TryGetProperty(task,out _),"unread remained after acknowledgement");
            Guid latestAnswer=Guid.NewGuid();Reply(latestAnswer,next);
            var newer=await Rpc();string newerTurn=newer.Body.GetProperty("turnId").GetString()!;
            ready[task]=newerTurn;
            var old=await Rpc(turn:unread);
            Check(old.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()=="","older history re-armed a stale notice");
            Check((await Rpc("read-ack",unread,receipt)).Status==409,"old receipt cleared a new reply");
            snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(snapshot.GetProperty("repliesUnread").GetProperty(task).GetString()==newerTurn,"new unread lost");
            // Bounded receipt storage must keep issuing receipts after many turns.
            for(int i=0;i<82;i++){
                Guid prompt=Guid.NewGuid();Prompt(prompt,latestAnswer);latestAnswer=Guid.NewGuid();Reply(latestAnswer,prompt);
                var bounded=await Rpc();Check(bounded.Status==200&&bounded.Body.GetProperty("replyView").GetProperty("readReceipt").GetString()!.Length>0,"receipt capacity disabled acknowledgement");
            }
            await NextDesktopPromptAsync(ClaudeSessionSource.Terminal,ClaudeSessionSource.VSCode);
            await NextDesktopPromptAsync(ClaudeSessionSource.VSCode,ClaudeSessionSource.Terminal);
            Console.WriteLine("CLAUDE_UNREAD_OK old reads, restart, next desktop prompt, new reply, session isolation, exact ack; no sends");
        } finally {Directory.Delete(root,true);}
    }
    private static async Task NextDesktopPromptAsync(ClaudeSessionSource first,ClaudeSessionSource second){
        string root=Path.Combine(Path.GetTempPath(),"aibot-unread-next-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Guid a=Guid.NewGuid(),b=Guid.NewGuid(),pa=Guid.NewGuid(),pb=Guid.NewGuid(),ra=Guid.NewGuid(),rb=Guid.NewGuid();
        void Write(Guid session,object row)=>File.AppendAllText(Path.Combine(root,session+".jsonl"),JsonSerializer.Serialize(row)+"\n");
        void Prompt(Guid session,Guid id,Guid? parent)=>Write(session,new{type="user",uuid=id,parentUuid=parent,sessionId=session,cwd=root,timestamp=DateTimeOffset.UtcNow,message=new{role="user",content="fixture prompt"}});
        void Reply(Guid session,Guid id,Guid parent)=>Write(session,new{type="assistant",uuid=id,parentUuid=parent,sessionId=session,timestamp=DateTimeOffset.UtcNow,message=new{role="assistant",stop_reason="end_turn",content=new[]{new{type="text",text="fixture reply"}}}});
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Claude unread next: "+why);}
        var terminal=new Native(first);var code=new Native(second);
        try{
            Prompt(a,pa,null);Reply(a,ra,pa);Prompt(b,pb,null);Reply(b,rb,pb);
            using var workspace=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),nativeFactory:id=>id==a?terminal:id==b?code:null);
            typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(workspace,null);
            string ta=workspace.Alias(a)??throw new InvalidOperationException("Claude unread next: catalog a"),tb=workspace.Alias(b)??throw new InvalidOperationException("Claude unread next: catalog b");
            async Task<JsonElement> Read(string task){var r=await workspace.HandleAsync(JsonSerializer.SerializeToElement(new{op="read",taskId=task}),"device-a",default);Check(r.Status==200,"read");return JsonSerializer.SerializeToElement(r.Body);}
            string oldA=(await Read(ta)).GetProperty("turnId").GetString()!,oldB=(await Read(tb)).GetProperty("turnId").GetString()!;
            var ready=(Dictionary<string,string>)typeof(ClaudeWorkspace).GetField("_ready",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(workspace)!;
            ready[ta]=oldA;ready[tb]=oldB;
            Write(a,new{type="user",uuid=Guid.NewGuid(),parentUuid=ra,sessionId=a,cwd=root,isSidechain=true,timestamp=DateTimeOffset.UtcNow,message=new{role="user",content="unrelated sidechain"}});
            typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(workspace,null);
            var before=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(before.GetProperty("repliesUnread").GetProperty(ta).GetString()==oldA&&before.GetProperty("repliesUnread").GetProperty(tb).GetString()==oldB,"unrelated sidechain cleared unread");
            Guid next=Guid.NewGuid();Prompt(a,next,ra);terminal.State="working";
            typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(workspace,null);
            var snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(snapshot.GetProperty("repliesRead").GetProperty(ta).GetString()==oldA&&!snapshot.GetProperty("repliesUnread").TryGetProperty(ta,out _)&&snapshot.GetProperty("repliesUnread").GetProperty(tb).GetString()==oldB,"next prompt did not clear only its own old unread");
            using(var restarted=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),nativeFactory:id=>id==a?terminal:id==b?code:null)){
                restarted.Start();for(int i=0;i<150&&restarted.Alias(a) is null;i++)await Task.Delay(20);
                bool restored=false;
                for(int i=0;i<150&&!restored;i++){
                    typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(restarted,null);
                    var restart=JsonSerializer.SerializeToElement(restarted.Snapshot());
                    restored=restart.GetProperty("repliesRead").TryGetProperty(ta,out var value)&&value.GetString()==oldA;
                    if(!restored)await Task.Delay(20);
                }
                Check(restored,"next prompt failed to clear prior device notice after restart");
            }
            Guid answer=Guid.NewGuid();Reply(a,answer,next);terminal.State="idle";
            // TAB5's read can observe completion before the next desktop
            // scan. The new reply must still publish independently of reading.
            var completed=await Read(ta);
            Check(completed.GetProperty("replyView").GetProperty("state").GetString()=="completed","read did not observe completion before the scan");
            typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(workspace,null);
            snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(snapshot.GetProperty("repliesUnread").TryGetProperty(ta,out var newNotice),"a read before the completion scan swallowed the new notice");
            string newA=newNotice.GetString()!;
            Check(newA!=oldA&&snapshot.GetProperty("repliesUnread").GetProperty(tb).GetString()==oldB,"new reply or other session unread was lost");
            var read=await Read(ta);string receipt=read.GetProperty("replyView").GetProperty("readReceipt").GetString()!;
            var ack=await workspace.HandleAsync(JsonSerializer.SerializeToElement(new{op="read-ack",taskId=ta,turnId=newA,requestId=receipt}),"device-a",default);Check(ack.Status==200,"new reply ack");
            typeof(ClaudeWorkspace).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(workspace,null);
            snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());Check(!snapshot.GetProperty("repliesUnread").TryGetProperty(ta,out _)&&snapshot.GetProperty("repliesUnread").GetProperty(tb).GetString()==oldB,"refresh resurrected new reply or cleared other session");
        }finally{Directory.Delete(root,true);}
    }
}
