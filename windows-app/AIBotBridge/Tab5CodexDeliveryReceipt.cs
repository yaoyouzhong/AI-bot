using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static AIBotBridge.Tab5CodexDesktop;

namespace AIBotBridge;

// Ephemeral, read-only delivery evidence. Retain hashes, never raw messages or
// queue identities. Queue acceptance means delivered, not executed/completed.
internal sealed record Tab5CodexDeliveryReceipt(string TaskId,string Owner,string Scope,string TextHash,
    string Turn,long Started,HashSet<string> Known,List<HashSet<string>> Accepted,
    Tab5CodexDeliveryReceipt.Queue? Local=null,Tab5CodexDeliveryReceipt.Queue? Server=null)
{
    internal sealed record Queue(HashSet<string> Known,List<HashSet<string>> Matching);
    internal static string Digest(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // Codex serializes native composer paragraphs with trailing CR/LF. Compare
    // that receipt representation only; never normalize the staged draft,
    // journal fingerprint, internal line breaks, spaces or punctuation.
    internal static string ParagraphText(string value)=>value.TrimEnd('\r','\n');
    internal string? Confirm(Tab5CodexDeliveryReceipt before) {
        if(TaskId!=before.TaskId||Owner!=before.Owner||Scope!=before.Scope||TextHash!=before.TextHash||
            before.Local is null||before.Server is null||Started<before.Started||Started==before.Started&&Turn!=before.Turn)return null;
        var known=new HashSet<string>(before.Known);known.UnionWith(before.Local.Known);known.UnionWith(before.Server.Known);
        bool New(List<HashSet<string>>? items)=>items?.Any(ids=>ids.Count>0&&!ids.Overlaps(known))==true;
        return New(Accepted)?"message_accepted":New(Local?.Matching)||New(Server?.Matching)?"message_saved_in_queue":null;
    }
    private static HashSet<string>? Aliases(JsonElement item,params string[] optional) {
        string id=Text(item,"id");if(id.Length==0||Encoding.UTF8.GetByteCount(id)>256)return null;
        var aliases=new HashSet<string>{Digest(id)};
        foreach(string key in optional) {
            var raw=Child(item,key);if(raw.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)continue;
            if(raw.ValueKind!=JsonValueKind.String||raw.GetString() is not {Length:>0} value||Encoding.UTF8.GetByteCount(value)>256)return null;
            aliases.Add(Digest(value));
        }
        return aliases;
    }
    private static bool MatchesInput(JsonElement input,string expected) {
        if(input.ValueKind!=JsonValueKind.Array||input.GetArrayLength() is <1 or >16)return false;
        var body=new StringBuilder();
        foreach(var part in input.EnumerateArray()) {
            if(Text(part,"type")!="text"||Child(part,"truncated").ValueKind==JsonValueKind.True||Child(part,"text").ValueKind!=JsonValueKind.String)return false;
            body.Append(Text(part,"text"));if(body.Length>32000)return false;
        }
        return ParagraphText(body.ToString())==ParagraphText(expected);
    }
    internal static Tab5CodexDeliveryReceipt? Parse(JsonElement state,string task,string owner,string scope,string expected) {
        if(Text(state,"id")!=task||!Guid.TryParse(owner,out _)||scope.Length==0||expected.Length==0||Encoding.UTF8.GetByteCount(expected)>32000)return null;
        var turns=new Dictionary<string,JsonElement>();
        var legacy=Child(state,"turns");var canonical=Child(Child(Child(state,"turnHistory"),"history"),"entitiesByKey");
        bool Add(JsonElement item) {string id=Text(item,"turnId");if(!Guid.TryParse(id,out _))return false;turns[id]=item;return turns.Count<=10000;}
        if(legacy.ValueKind==JsonValueKind.Array)foreach(var item in legacy.EnumerateArray())if(!Add(item))return null;
        if(canonical.ValueKind==JsonValueKind.Object)foreach(var item in canonical.EnumerateObject())if(!Add(item.Value))return null;
        if(turns.Count==0)return null;
        long started=0;string latest="";bool tied=false;
        foreach(var (id,turn) in turns) {
            if(!Child(turn,"turnStartedAtMs").TryGetInt64Safe(out long time)||time<=0)return null;
            if(time>started){started=time;latest=id;tied=false;}else if(time==started)tied=true;
        }
        if(tied)return null;
        var known=new HashSet<string>();var accepted=new List<HashSet<string>>();int visited=0;
        foreach(var (id,turn) in turns) {
            var items=Child(turn,"items");if(items.ValueKind!=JsonValueKind.Array)return null;
            foreach(var item in items.EnumerateArray()) {
                if(++visited>100000)return null;string kind=Text(item,"type");
                if(kind is not ("userMessage" or "steeringUserMessage"))continue;
                var aliases=Aliases(item,"clientId","clientUserMessageId","serverUserMessageId","serverClientUserMessageId");
                if(aliases is null)return null;known.UnionWith(aliases);
                if(id==latest&&(kind=="userMessage"||Text(item,"status")=="accepted")&&MatchesInput(Child(item,kind=="userMessage"?"content":"input"),expected))accepted.Add(aliases);
            }
        }
        return new(task,owner,scope,Digest(expected),latest,started,known,accepted);
    }
    internal static Queue? ParseServer(JsonElement root,string expected) {
        var entries=Child(root,"data");
        if(entries.ValueKind!=JsonValueKind.Array||entries.GetArrayLength()>1024||Child(root,"nextCursor").ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))return null;
        var known=new HashSet<string>();var matching=new List<HashSet<string>>();var ids=new HashSet<string>();
        foreach(var entry in entries.EnumerateArray()) {
            var aliases=Aliases(entry,"clientUserMessageId");
            if(aliases is null||Text(entry,"clientUserMessageId").Length==0||!ids.Add(Text(entry,"id")))return null;
            known.UnionWith(aliases);if(MatchesInput(Child(entry,"input"),expected))matching.Add(aliases);
        }
        return new(known,matching);
    }
    internal static Queue? ParseLocal(JsonElement root,string task,string expected) {
        var queues=Child(root,"queued-follow-ups");if(queues.ValueKind!=JsonValueKind.Object)return null;
        var entries=Child(queues,task);if(entries.ValueKind==JsonValueKind.Undefined)return new([],[]);
        if(entries.ValueKind!=JsonValueKind.Array||entries.GetArrayLength()>1024)return null;
        var known=new HashSet<string>();var matching=new List<HashSet<string>>();var ids=new HashSet<string>();
        foreach(var entry in entries.EnumerateArray()) {
            var aliases=Aliases(entry,"clientId","clientUserMessageId","serverQueuedMessageId","serverUserMessageId");
            if(aliases is null||!ids.Add(Text(entry,"id")))return null;known.UnionWith(aliases);
            var submission=Child(entry,"submission");
            if(submission.ValueKind!=JsonValueKind.Undefined&&(Text(submission,"hostId")!="local"||Text(submission,"status") is not ("pending" or "queued")))continue;
            var options=Child(entry,"submissionOptions");
            if(options.ValueKind!=JsonValueKind.Undefined&&(options.ValueKind!=JsonValueKind.Object||Child(options,"executionHostId").ValueKind!=JsonValueKind.Undefined&&Text(options,"executionHostId")!="local"))continue;
            if(Child(entry,"pausedReason").ValueKind!=JsonValueKind.Undefined||Child(entry,"serverQueuedMessageId").ValueKind!=JsonValueKind.Undefined)continue;
            var context=Child(entry,"context");if(ParagraphText(Text(context,"prompt"))!=ParagraphText(expected))continue;
            bool Empty(string key,bool required=false) {var value=Child(context,key);return !required&&value.ValueKind==JsonValueKind.Undefined||value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()==0;}
            if(!new[]{"addedFiles","fileAttachments","imageAttachments"}.All(k=>Empty(k,true))||
                !new[]{"pastedTextAttachments","uploadedFileAttachments","mcpAppModelContextAttachments","commentAttachments","imageCommentDrafts","selectedTextAttachments","responseTextAnnotations","threadReferences","chatGptConversationContexts","appshotContexts"}.All(k=>Empty(k))||
                !new[]{"aeonReplyQuote","untrustedAppMessage","messageSender","restoredPromptContext","imageCommentDraft"}.All(k=>Child(context,k).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined))continue;
            matching.Add(aliases);
        }
        return new(known,matching);
    }
    internal static Queue? ReadLocal(string task,string expected) {
        try {
            string path=Path.Combine(Tab5CodexCatalog.HomePath,".codex-global-state.json");
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return null;
            // A short non-writable shared handle prevents partial writes or path
            // replacement while observing the committed file. No exports/copies.
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <1 or >16777216)return null;
            using var json=JsonDocument.Parse(stream);return ParseLocal(json.RootElement,task,expected);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){return null;}
    }
    internal static Task<Tab5CodexDeliveryReceipt?> ReadAsync(string task,string expected,CancellationToken token)=>
        ReadCoreAsync(task,expected,null,token);
    internal static Task<Tab5CodexDeliveryReceipt?> ReadAfterAsync(string task,string expected,Tab5CodexDeliveryReceipt before,CancellationToken token)=>
        ReadCoreAsync(task,expected,before,token);
    // The pre-action baseline must include both queues. After the action, a new
    // accepted message or committed local queue entry already proves delivery;
    // do not wait for another app-server process to repeat that evidence.
    internal static async Task<Tab5CodexDeliveryReceipt?> CompleteAfterAsync(Tab5CodexDeliveryReceipt? after,Tab5CodexDeliveryReceipt before,
        Func<CancellationToken,Task<Queue?>> readServer,CancellationToken token) {
        if(after is null||before.Local is null||before.Server is null)return null;
        if(after.Confirm(before) is not null)return after;
        return after with {Server=await readServer(token)};
    }
    internal static async Task<Tab5CodexDeliveryReceipt?> WaitForConfirmationAsync(Tab5CodexDeliveryReceipt before,
        Func<CancellationToken,Task<Tab5CodexDeliveryReceipt?>> read,CancellationToken token,
        Func<CancellationToken,Task>? wait=null) {
        if(before.Local is null||before.Server is null)return null;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(3500);
        try {
            for(int attempt=0;attempt<4;attempt++) {
                deadline.Token.ThrowIfCancellationRequested();
                var after=await read(deadline.Token);
                if(after?.Confirm(before) is not null)return after;
                if(attempt<3)await (wait?.Invoke(deadline.Token)??Task.Delay(120,deadline.Token));
            }
        }catch(OperationCanceledException) { }
        return null;
    }
    private static async Task<Tab5CodexDeliveryReceipt?> ReadCoreAsync(string task,string expected,Tab5CodexDeliveryReceipt? before,CancellationToken token) {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(before is null?3500:1800);
        long began=Environment.TickCount64;bool fast=false;
        try {
            var identity=Tab5CodexReadState.ReadIdentity();if(identity is null)return null;
            var desktop=new Tab5CodexDesktop();
            var snapshotTask=desktop.SubmissionSnapshotAsync(task,deadline.Token);
            var serverTask=before is null?ReadServerAsync(task,expected,deadline.Token):null;
            var local=ReadLocal(task,expected);
            var snapshot=await snapshotTask.ConfigureAwait(false);
            if(Tab5CodexReadState.ReadIdentity()?.Scope!=identity.Scope)return null;
            var receipt=Parse(snapshot.State,task,snapshot.Owner,identity.Scope,expected);
            if(receipt is null)return null;
            receipt=receipt with {Local=local};
            if(before is null)receipt=receipt with {Server=await serverTask!.ConfigureAwait(false)};
            else {
                fast=receipt.Confirm(before) is not null;
                receipt=await CompleteAfterAsync(receipt,before,ct=>ReadServerAsync(task,expected,ct),deadline.Token);
            }
            return Tab5CodexReadState.ReadIdentity()?.Scope==identity.Scope?receipt:null;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception){return null;}
        finally {
            deadline.Cancel();
            Tab5LocalDiagnostics.Write($"CODEX_RECEIPT phase={(before is null?"baseline":"after")} fast={fast} ms={Environment.TickCount64-began}");
        }
    }
    // Dedicated bounded read-only child: never waits behind history reads and
    // never sends turn/start, steer, approvals or any queue mutation.
    private static readonly Tab5QueueReceiptReader QueueReader=new();
    internal static Task<Queue?> ReadServerAsync(string task,string expected,CancellationToken token)=>QueueReader.ReadAsync(task,expected,token);
    internal static Task PrepareTransportAsync(CancellationToken token)=>QueueReader.PrepareAsync(token);
    internal sealed class Tab5QueueReceiptReader : IDisposable {
        private readonly SemaphoreSlim _gate=new(1,1);
        private Process? _process;
        private Tab5BoundedLines? _lines;
        private CancellationTokenSource? _drain;
        private string _scope="";
        private int _nextId;
        private readonly Func<string?> _identity;
        private readonly Func<ProcessStartInfo>? _start;
        internal Tab5QueueReceiptReader(Func<string?>? identity=null,Func<ProcessStartInfo>? start=null){
            _identity=identity??(()=>Tab5CodexReadState.ReadIdentity()?.Scope);_start=start;
            AppDomain.CurrentDomain.ProcessExit+=OnExit;
        }
        private void OnExit(object? sender,EventArgs args)=>Dispose();
        internal Task PrepareAsync(CancellationToken token)=>ReadCoreAsync(null,"",token);
        internal Task<Queue?> ReadAsync(string task,string expected,CancellationToken token)=>ReadCoreAsync(task,expected,token);
        private async Task<Queue?> ReadCoreAsync(string? task,string expected,CancellationToken token) {
        bool entered=false;long began=Environment.TickCount64;bool reused=false;
        try {
            await _gate.WaitAsync(token).ConfigureAwait(false);entered=true;
            string? scope=_identity();if(scope is null){Stop();return null;}
            if(_process is not {HasExited:false}||_scope!=scope) {
            Stop();
            var start=_start?.Invoke();
            if(start is null) {
                start=new ProcessStartInfo(QueueExecutable()){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8};
                start.ArgumentList.Add("app-server");start.ArgumentList.Add("--listen");start.ArgumentList.Add("stdio://");
            }
            _process=Process.Start(start)??throw new IOException();_scope=scope;
            // Drain without logging possibly sensitive diagnostics.
            _drain=new();_=DrainAsync(_process.StandardError,_drain.Token);
            _lines=new Tab5BoundedLines(_process.StandardOutput);
            int initialize=++_nextId;
            await Write(new{id=initialize,method="initialize",@params=new{clientInfo=new{name="aibot_queue_receipt",version="1.0"},capabilities=new{experimentalApi=true}}});await Response(initialize);
            await Write(new{method="initialized"});
            }else reused=true;
            async Task Write(object value)=>await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value).AsMemory(),token).ConfigureAwait(false);
            async Task<JsonElement> Response(int id) {
                while(await _lines!.ReadAsync(token).ConfigureAwait(false) is {} line) {
                    using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
                    if(Child(root,"id").TryGetInt64Safe(out long got)&&got==id) {
                        if(Child(root,"error").ValueKind!=JsonValueKind.Undefined)throw new IOException("Queue unavailable");
                        return Child(root,"result").Clone();
                    }
                    if(Child(root,"id").ValueKind!=JsonValueKind.Undefined&&Child(root,"method").ValueKind!=JsonValueKind.Undefined)throw new IOException("Read-only reader received a request");
                }
                throw new IOException("Queue reader closed");
            }
            // Preparation only initializes a transport. It does not read or cache
            // queues/history or carry any text, task identity or delivery baseline.
            if(task is null){if(_identity()!=scope)Stop();return null;}
            int id=++_nextId;
            await Write(new{id,method="thread/queue/list",@params=new{threadId=task,cursor=(string?)null}});
            var result=ParseServer(await Response(id),expected);
            if(_identity()!=scope)throw new IOException("Receipt scope changed");
            if(result is null)Stop();return result;
        }catch(Exception ex) when(ex is IOException or JsonException or OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception){if(entered)Stop();return null;}
        finally {if(entered)_gate.Release();Tab5LocalDiagnostics.Write($"CODEX_QUEUE phase={(task is null?"prepare":"read")} reused={reused} ms={Environment.TickCount64-began}");}
        }
        private void Stop() {
            _drain?.Cancel();_drain?.Dispose();_drain=null;_lines=null;
            var process=_process;_process=null;_scope="";
            if(process is not null){try{if(!process.HasExited)process.Kill();}catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception){}process.Dispose();}
        }
        public void Dispose(){AppDomain.CurrentDomain.ProcessExit-=OnExit;_gate.Wait();try{Stop();}finally{_gate.Release();}}
    }
    private static async Task DrainAsync(StreamReader reader,CancellationToken token) {
        try {var lines=new Tab5BoundedLines(reader,65536);while(await lines.ReadAsync(token) is not null){} }
        catch(Exception ex) when(ex is IOException or ObjectDisposedException or OperationCanceledException){}
    }
    internal static string QueueExecutable() {
        foreach(var process in Process.GetProcessesByName("ChatGPT").Concat(Process.GetProcessesByName("Codex")))using(process) {
            try {string? path=process.MainModule?.FileName;if(path?.Contains("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)==true) {
                string candidate=Path.Combine(Path.GetDirectoryName(path)!,"resources","codex.exe");if(File.Exists(candidate))return candidate;
            }}catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception){}
        }
        return "codex";
    }
}
internal static class Tab5ReceiptJsonNumber {
    internal static bool TryGetInt64Safe(this JsonElement value,out long number) {number=0;return value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out number);}
}
