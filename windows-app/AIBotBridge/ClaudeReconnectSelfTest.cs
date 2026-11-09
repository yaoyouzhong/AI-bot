using System.Reflection;
using System.Text.Json;

namespace AIBotBridge;

internal static class ClaudeReconnectSelfTest
{
    private sealed class Client(ClaudeSessionSource source,Func<bool> ready):IClaudeNativeSession
    {
        public ClaudeSessionSource Source=>source;
        public string State=>"idle";
        public string Scope()=>"isolated-reconnect-fixture";
        public bool CanAddressInput=>ready();
        public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>
            throw new InvalidOperationException("Readiness checks must not send input");
    }
    internal static async Task RunAsync(){
        await CheckAsync(ClaudeSessionSource.Terminal,ClaudeSessionSource.VSCode);
        await CheckAsync(ClaudeSessionSource.VSCode,ClaudeSessionSource.Terminal);
    }
    private static async Task CheckAsync(ClaudeSessionSource selectedSource,ClaudeSessionSource slowSource){
        string root=Path.Combine(Path.GetTempPath(),"aibot-reconnect-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var release=new ManualResetEventSlim();
        var occupied=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int slowProbes=0,connected=1;
        bool Slow(){if(Interlocked.Increment(ref slowProbes)==2)occupied.TrySetResult();
            if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("fixture_probe_timeout");return true;}
        var selected=new Client(selectedSource,()=>true);var other=new Client(slowSource,Slow);
        Guid target=Guid.NewGuid(),slow1=Guid.NewGuid(),slow2=Guid.NewGuid();
        DateTimeOffset now=DateTimeOffset.UtcNow;
        foreach(var (session,age) in new[]{(target,3),(slow1,2),(slow2,1)}){
            Guid user=Guid.NewGuid();
            object[] turns=[new{type="user",uuid=user,sessionId=session,parentUuid=(Guid?)null,cwd=root,timestamp=now.AddMinutes(-age),message=new{role="user",content="fixture"}},
                new{type="assistant",uuid=Guid.NewGuid(),sessionId=session,parentUuid=user,timestamp=now.AddMinutes(-age),message=new{role="assistant",stop_reason="end_turn",content=new[]{new{type="text",text="ready"}}}}];
            File.WriteAllText(Path.Combine(root,session+".jsonl"),string.Join('\n',turns.Select(row=>JsonSerializer.Serialize(row)))+"\n");
        }
        var workspace=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),nativeFactory:id=>id==target?
            Volatile.Read(ref connected)==1?selected:null:other);
        var refresh=typeof(ClaudeWorkspace).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var sweep=Task.Run(()=>refresh.Invoke(workspace,null));
        try{
            await occupied.Task.WaitAsync(TimeSpan.FromSeconds(8));
            string task=workspace.Alias(target)??throw new InvalidOperationException("Catalog was held by unrelated native probes");
            async Task Load(){var result=await workspace.HandleAsync(JsonSerializer.SerializeToElement(new{op="draft-load",taskId=task,requestId=Guid.NewGuid()}),"device",default).WaitAsync(TimeSpan.FromSeconds(4));
                if(result.Status!=200)throw new InvalidOperationException("Draft load failed while another terminal was probing");}
            bool? Ready(){var status=JsonSerializer.SerializeToElement(workspace.Snapshot(limit:1,device:"device"));
                var control=status.GetProperty("controls");if(!control.TryGetProperty(task,out var view))throw new InvalidOperationException("Selected task was omitted from bounded frame");
                return view.ValueKind==JsonValueKind.Object?view.GetProperty("canSend").GetBoolean():null;}
            async Task Expect(bool? expected){long deadline=Environment.TickCount64+4000;
                while(Environment.TickCount64<deadline){if(Ready()==expected)return;await Task.Delay(15);}
                throw new InvalidOperationException("Selected readiness did not update independently: "+selectedSource.Id);}
            long start=Environment.TickCount64;await Load();await Expect(true);long elapsed=Environment.TickCount64-start;
            if(release.IsSet||sweep.IsCompleted)throw new InvalidOperationException("Test did not verify independence from slow probes");
            Volatile.Write(ref connected,0);await Load();await Expect(null);
            Volatile.Write(ref connected,1);await Load();await Expect(true);
            Console.WriteLine($"CLAUDE_RECONNECT_OK source={selectedSource.Id} selectedReadyMs={elapsed} unrelatedProbesBlocked=True rebind=True noInput=True");
        }finally{
            release.Set();await sweep;workspace.Dispose();Directory.Delete(root,true);
        }
    }
}
