using System.Text.Json;

namespace AIBotBridge;

internal static class ClaudeHarnessSelfTest
{
    private sealed class Session(ClaudeSessionSource source):IClaudeNativeSession
    {
        public ClaudeSessionSource Source=>source;
        public string State=>"idle";
        public string Scope()=>"fixture-only";
        public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>
            throw new InvalidOperationException("Read-only Harness must not dispatch");
    }
    private sealed class Adapter(ClaudeSessionSource source,Guid session,string directory,IClaudeNativeSession native):IClaudeHarnessAdapter
    {
        public ClaudeSessionSource Source=>source;
        public IEnumerable<ClaudeSessionObservation> Discover()=>[new(session,DateTimeOffset.UtcNow,"Custom Harness fixture",directory)];
        public IEnumerable<Guid> LiveSessions()=>[session];
        public IClaudeNativeSession? Resolve(Guid id)=>id==session?native:null;
    }
    internal static async Task RunAsync(){
        string temp=Path.GetFullPath(Path.GetTempPath()),root=Path.Combine(temp,"aibot-harness-"+Guid.NewGuid().ToString("N"));
        string marker=Guid.NewGuid().ToString();Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".owned"),marker);
        int checks=0;void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        try{
            bool TerminalRecord(string? version,string kind="interactive",string entrypoint="cli",string? jobId=null)=>
                ClaudeNativeTerminal.IsInteractiveRecord(JsonSerializer.SerializeToElement(new{version,kind,entrypoint,jobId}));
            Check(TerminalRecord("2.1.318")&&TerminalRecord("2.1.322")&&TerminalRecord("3.0.0")&&TerminalRecord(null),
                "An interactive terminal record must not depend on the reported version");
            Check(!TerminalRecord("2.1.322","background")&&!TerminalRecord("2.1.322",entrypoint:"claude-vscode")&&
                !TerminalRecord("2.1.322",jobId:"background-job"),"Non-terminal session accepted by the console adapter");
            using(var process=System.Diagnostics.Process.GetCurrentProcess()){
                string image=Environment.ProcessPath??throw new InvalidOperationException("Process image unavailable");
                string start=process.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(System.Globalization.CultureInfo.InvariantCulture);
                Check(ClaudeNativeTerminal.ProcessIdentityMatches(process.Id,image,start),"Live limited-query process identity was not recognized");
                Check(!ClaudeNativeTerminal.ProcessIdentityMatches(process.Id,image+".other",start)&&
                    !ClaudeNativeTerminal.ProcessIdentityMatches(process.Id,image,"0")&&
                    !ClaudeNativeTerminal.ProcessIdentityMatches(int.MaxValue,image,start),"Wrong process image, generation or PID was accepted");
            }
            Guid sid=Guid.NewGuid();var custom=new ClaudeSessionSource("example.workflow","Example Harness","harness");
            var native=new Session(custom);var adapter=new Adapter(custom,sid,root,native);var registry=new ClaudeHarnessAdapters([adapter]);
            Check(registry.Discover().Single().Id==sid&&registry.LiveSessions().SetEquals([sid])&&ReferenceEquals(registry.Resolve(sid),native),"Custom discovery/identity was not retained");
            Check(registry.Resolve(Guid.NewGuid()) is null&&!((IClaudeNativeSession)native).CanAddressInput,"Custom source enabled an unsupported input");
            bool duplicate=false;try{_=registry.With(adapter);}catch(ArgumentException){duplicate=true;}Check(duplicate,"Duplicate adapter ID was accepted");
            var otherSource=new ClaudeSessionSource("example.second","Second Harness","harness");
            var multiple=registry.With(new Adapter(otherSource,sid,root,new Session(otherSource))).Resolve(sid)!;
            Check(multiple.Source==ClaudeSessionSource.Multiple&&!multiple.CanAddressInput&&!multiple.CanAppend&&!multiple.CanInterrupt,"Ambiguous owners retained input capability");
            bool mismatch=false;try{_=new ClaudeHarnessAdapters([new Adapter(custom,sid,root,new Session(otherSource))]).Resolve(sid);}catch(IOException){mismatch=true;}Check(mismatch,"Source identity mismatch accepted");
            Check(!new ClaudeSessionSource("example.bad","bad\u202ename","harness").Valid&&!new ClaudeSessionSource("../bad","Bad","harness").Valid,"Unsafe label or ID accepted");
            string alias=Guid.NewGuid().ToString(),folder=Path.Combine(root,"cache");var cache=new ClaudeSessionSources(folder);
            cache.Observe(alias,custom);cache.Save();Check(new ClaudeSessionSources(folder).Get(alias)==custom,"Confirmed source lost across restart");
            cache.Observe(alias,ClaudeSessionSource.Unknown);cache.Save();Check(cache.Get(alias)==custom,"Transient absence erased source metadata");
            Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"session-sources.dat"))).Contains("Example Harness"),"Source metadata persisted in clear text");
            File.WriteAllText(Path.Combine(root,sid+".jsonl"),JsonSerializer.Serialize(new{type="user",uuid=Guid.NewGuid(),sessionId=sid,parentUuid=(Guid?)null,cwd=root,message=new{role="user",content="local fixture"},timestamp=DateTimeOffset.UtcNow.ToString("O")})+"\n");
            using(var workspace=new ClaudeWorkspace(()=>true,root,Path.Combine(root,"private"),harnesses:registry)){
                workspace.Start();JsonElement snapshot=default;string? task=null;
                for(int i=0;i<150;i++){
                    task=workspace.Alias(sid);snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot());
                    if(task is not null&&snapshot.GetProperty("controls").TryGetProperty(task,out var control)&&control.ValueKind==JsonValueKind.Object)break;
                    await Task.Delay(20);
                }
                var row=snapshot.GetProperty("tasks").EnumerateArray().Single(t=>t.GetProperty("id").GetString()==task);
                Check(row.GetProperty("source").GetProperty("id").GetString()==custom.Id&&!snapshot.GetProperty("controls").GetProperty(task!).GetProperty("canSend").GetBoolean(),"Third-party source did not reach the real workspace or enabled sending");
            }
            Console.WriteLine($"CLAUDE_HARNESS_SOURCE_OK {checks} checks; registered read-only adapter, unique owner, persistent display metadata; zero sends");
        }finally{
            string resolved=Path.GetFullPath(root);
            if(resolved.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(resolved).StartsWith("aibot-harness-",StringComparison.Ordinal)&&File.ReadAllText(Path.Combine(resolved,".owned"))==marker)Directory.Delete(resolved,true);
        }
    }
}
