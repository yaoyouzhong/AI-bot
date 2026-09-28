using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace AIBotBridge;

// Desktop IPC is versioned but not a public API. Fail closed on an incompatible
// owner; never start an independent CLI agent for a desktop conversation.
internal sealed class Tab5CodexDesktop(string pipeName = "codex-ipc", Func<string, bool>? openThread = null)
{
    private readonly Func<string,bool> _openThread=openThread??(pipeName=="codex-ipc"?OpenThread:_=>false);
    private readonly SemaphoreSlim _loadGate=new(1,1);
    private readonly Dictionary<string,long> _loadAttempts=new();
    internal static string ThreadUri(string id)=>$"codex://threads/{Guid.Parse(id):D}";
    private static bool OpenThread(string id) {
        try {using var process=Process.Start(new ProcessStartInfo(ThreadUri(id)){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});return true;}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException) {return false;}
    }
    private async Task LoadAsync(string id,CancellationToken token) {
        await _loadGate.WaitAsync(token);
        try {
            long now=Environment.TickCount64;
            if(_loadAttempts.TryGetValue(id,out var last)&&now-last<30000)return;
            // Only invoked by an explicit device read/send, never by background watchers.
            token.ThrowIfCancellationRequested();
            _loadAttempts[id]=now;
            if(!_openThread(id))throw new Rejected("desktop_load_failed");
        }finally{_loadGate.Release();}
    }
    internal sealed class Rejected(string code) : IOException(code) { internal string Code => Message; }
    internal sealed record State(string Runtime, string Path, JsonElement Turn, bool Waiting)
    {
        internal JsonElement LatestTurn {get;init;}
        internal bool IsHistory {get;init;}
        internal bool HasPrevious {get;init;}
        internal bool HasNext {get;init;}
        internal State Current=>LatestTurn.ValueKind==JsonValueKind.Object?this with {Turn=LatestTurn,IsHistory=false}:this;
        internal string TurnId => Text(Turn,"turnId");
        internal string Status => Text(Turn,"status");
        internal bool CanSend => Runtime == "idle" && !Waiting &&
            Status is "completed" or "interrupted" or "failed";
        internal string Receipt => IsHistory ? "历史 · "+(Status=="completed"?"已完成":Status=="interrupted"?"已中断":Status=="failed"?"任务失败":"执行记录") : Waiting ? "等待电脑端确认" : Runtime == "active" ? "运行中" :
            Status switch { "completed" => "已完成", "failed" => "任务失败，请在电脑查看", "interrupted" => "已中断", "stored" => "已保存记录", _ => "暂无动态" };
    }

    internal async Task<State> ReadAsync(string taskId,CancellationToken token,string? turnId=null,bool ensureLoaded=false,int turnOffset=0)
    {
        await using var connection = new Connection(pipeName);
        await connection.OpenAsync(taskId,token,ensureLoaded?LoadAsync:null);
        var snapshot=await connection.ReadySnapshotAsync(taskId,token);
        if(turnId is {Length:>0}) {
            var turns=Turns(snapshot);int index=turns.FindIndex(t=>Text(t,"turnId")==turnId);
            bool older=HasOlder(snapshot);
            if(index<0||(turnOffset<0&&index==0&&older)) {
                // Load through the desktop owner so obsolete/orphan rollout turns cannot appear.
                var loaded=await connection.RequestAsync("thread-follower-load-complete-history",1,new {conversationId=taskId},token,connection.Owner,10000);
                CheckResponse(loaded);snapshot=await connection.SnapshotAsync(taskId,token);
            }
        }
        return ParseState(snapshot,turnId,turnOffset);
    }

    internal async Task<string> SendAsync(string taskId,string text,Action dispatch,CancellationToken token,
        string? requestId=null,string operation="send",string expectedTurn="",string[]? imagePaths=null)
    {
        await using var connection = new Connection(pipeName);
        await connection.OpenAsync(taskId,token,operation=="send"?LoadAsync:null);
        var snapshot=await connection.ReadySnapshotAsync(taskId,token);
        var state=ParseState(snapshot);
        if(operation=="send"&&!state.CanSend)throw new Rejected("task_busy_or_state_unknown");
        if(operation is "steer" or "interrupt") {
            if(state.Status!="inProgress"||state.Runtime!="active"||state.TurnId!=expectedTurn)throw new Rejected("turn_changed");
            if(state.Waiting&&operation=="steer")throw new Rejected("task_waiting_for_input");
        } else if(operation!="send")throw new Rejected("invalid_operation");
        string messageId=requestId is null?Guid.NewGuid().ToString():Guid.Parse(requestId).ToString();
        var input=new List<object>();
        if(!string.IsNullOrWhiteSpace(text))input.Add(new {type="text",text,text_elements=Array.Empty<object>()});
        foreach(string path in imagePaths??[])input.Add(new {type="localImage",path});
        string method="thread-follower-start-turn";int version=2;
        object parameters=new {
            conversationId=taskId,
            turnStart=new { request=new {threadId=taskId,input,clientUserMessageId=messageId},context=new {inheritThreadSettings=true} }
        };
        if(operation=="steer") {
            method="thread-follower-steer-turn";version=1;
            string cwd=Text(snapshot,"cwd");
            parameters=new {conversationId=taskId,input,clientUserMessageId=messageId,attachments=Array.Empty<object>(),
                restoreMessage=new {id=messageId,text,cwd,createdAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    context=new{prompt=text,addedFiles=Array.Empty<object>(),fileAttachments=Array.Empty<object>(),imageAttachments=Array.Empty<object>(),workspaceRoots=new[]{cwd}}}};
        }else if(operation=="interrupt") {
            method="thread-follower-interrupt-turn";version=4;
            parameters=new{conversationId=taskId,mode="user-stop",expectedTurnId=expectedTurn};
        }
        token.ThrowIfCancellationRequested();
        dispatch();
        var response=await connection.RequestAsync(method,version,parameters,token,connection.Owner,20000);
        // Router transport errors after writing do not prove rejection.
        CheckResponse(response,true);
        var result=Child(Child(response,"result"),"result");
        string id=operation=="interrupt"?Text(Child(response,"result"),"interruptedTurnId"):
            operation=="steer"?Text(result,"turnId"):Text(Child(result,"turn"),"id");
        if(operation=="interrupt"&&string.IsNullOrEmpty(id))throw new Rejected("turn_changed");
        if(!Guid.TryParse(id,out _))throw new IOException("Incomplete desktop acknowledgement");
        return id;
    }

    internal async Task<string?> FindAcceptedAsync(string taskId,string requestId,CancellationToken token) {
        await using var connection=new Connection(pipeName);await connection.OpenAsync(taskId,token);
        var snapshot=await connection.ReadySnapshotAsync(taskId,token);
        string messageId=Guid.Parse(requestId).ToString();
        foreach(var turn in Turns(snapshot)) {
            if(Text(Child(turn,"params"),"clientUserMessageId")==messageId)return Text(turn,"turnId");
            var items=Child(turn,"items");
            if(items.ValueKind!=JsonValueKind.Array)continue;
            foreach(var item in items.EnumerateArray()) {
                bool matches=Text(item,"clientId")==messageId||Text(item,"clientUserMessageId")==messageId;
                if(matches&&(Text(item,"type")=="userMessage"||Text(item,"type")=="steeringUserMessage"&&Text(item,"status")=="accepted"))
                    return Text(turn,"turnId");
            }
        }
        return null; // Absence is not proof of rejection; history may be paginated.
    }

    internal static State ParseState(JsonElement state,string? turnId=null,int turnOffset=0)
    {
        var turns=Turns(state);
        int selected=string.IsNullOrEmpty(turnId)?turns.Count-1:turns.FindIndex(t=>Text(t,"turnId")==turnId);
        if(!string.IsNullOrEmpty(turnId)&&selected<0)throw new Rejected("history_turn_unavailable");
        if(turns.Count>0)selected=Math.Clamp(selected+turnOffset,0,turns.Count-1);
        var turn=selected>=0?turns[selected]:default;
        var requests=Child(state,"requests");
        return new(Text(Child(state,"threadRuntimeStatus"),"type"),Text(state,"rolloutPath"),turn,
            requests.ValueKind==JsonValueKind.Array&&requests.GetArrayLength()>0) {
                LatestTurn=turns.LastOrDefault(),IsHistory=selected>=0&&selected<turns.Count-1,
                HasPrevious=selected>0||HasOlder(state),
                HasNext=selected>=0&&selected<turns.Count-1
            };
    }
    private static bool HasOlder(JsonElement state) {
        var canonical=Child(Child(Child(state,"turnHistory"),"history"),"isComplete");
        return canonical.ValueKind is JsonValueKind.True or JsonValueKind.False ? !canonical.GetBoolean() : Child(Child(state,"turnsPagination"),"hasLoadedOldest").ValueKind==JsonValueKind.False;
    }
    private static List<JsonElement> Turns(JsonElement state) {
        var turns=new List<JsonElement>();
        var legacy=Child(state,"turns");
        if(legacy.ValueKind==JsonValueKind.Array)turns.AddRange(legacy.EnumerateArray());
        var entities=Child(Child(Child(state,"turnHistory"),"history"),"entitiesByKey");
        if(entities.ValueKind==JsonValueKind.Object)turns.AddRange(entities.EnumerateObject().Select(p=>p.Value));
        return turns.Where(t=>Text(t,"turnId").Length>0).GroupBy(t=>Text(t,"turnId")).Select(g=>g.Last()).OrderBy(t=>Child(t,"turnStartedAtMs").TryGetDoubleSafe()).ToList();
    }
    internal static JsonElement Child(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var child)?child:default;
    internal static string Text(JsonElement value,string key)=>Child(value,key) is var child&&child.ValueKind==JsonValueKind.String?child.GetString()!:"";
    private static void CheckResponse(JsonElement response,bool dispatched=false)
    {
        if(Text(response,"resultType")=="success")return;
        string failure=Text(response,"error");
        if(!dispatched)throw new Rejected(failure=="no-client-found"?"desktop_owner_unavailable":"desktop_unavailable");
        // Only router pre-dispatch errors prove that no turn was delivered.
        // A handler timeout/exception may happen after the owner starts a turn.
        if(failure is "no-client-found" or "no-handler-for-request")throw new Rejected("desktop_owner_unavailable");
        if(failure=="request-version-mismatch")throw new Rejected("desktop_version_unsupported");
        throw new IOException("Desktop delivery unconfirmed");
    }

    private sealed class Connection(string name) : IAsyncDisposable
    {
        private readonly NamedPipeClientStream _pipe=new(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
        private string _client="initializing-client";
        internal string Owner {get;private set;}="";
        internal async Task OpenAsync(string taskId,CancellationToken token,Func<string,CancellationToken,Task>? load=null)
        {
            await _pipe.ConnectAsync(1500,token);
            var init=await RequestAsync("initialize",0,new {clientType="tab5-bridge"},token);
            CheckResponse(init);_client=Text(Child(init,"result"),"clientId");
            if(!Guid.TryParse(_client,out _))throw new IOException("Missing IPC identity");
            var owner=await RequestAsync("thread-owner-discovery",1,new {hostId="local",conversationId=taskId},token);
            if(Text(owner,"resultType")!="success"&&Text(owner,"error")=="no-client-found"&&load is not null) {
                await load(taskId,token);
                long began=Environment.TickCount64;
                do {
                    await Task.Delay(350,token);
                    owner=await RequestAsync("thread-owner-discovery",1,new {hostId="local",conversationId=taskId},token,timeoutMs:1500);
                }while(Text(owner,"resultType")!="success"&&Text(owner,"error")=="no-client-found"&&Environment.TickCount64-began<8000);
                if(Text(owner,"resultType")!="success"&&Text(owner,"error")=="no-client-found")throw new Rejected("desktop_load_timeout");
            }
            CheckResponse(owner);Owner=Text(owner,"handledByClientId");
            if(!Guid.TryParse(Owner,out _)||Child(Child(owner,"result"),"supportsUntrustedAppInput").ValueKind!=JsonValueKind.True)
                throw new Rejected("desktop_owner_unavailable");
        }
        internal async Task<JsonElement> ReadySnapshotAsync(string taskId,CancellationToken token) {
            long began=Environment.TickCount64;
            do {
                var snapshot=await SnapshotAsync(taskId,token);
                var state=ParseState(snapshot);
                if(state.Runtime is "idle" or "active" && state.TurnId.Length>0 && state.Status.Length>0)return snapshot;
                await Task.Delay(250,token);
            }while(Environment.TickCount64-began<8000);
            throw new Rejected("desktop_load_timeout");
        }
        internal async Task<JsonElement> SnapshotAsync(string taskId,CancellationToken token)
        {
            await WriteAsync(new {type="broadcast",sourceClientId=_client,version=1,method="thread-stream-following-changed",
                @params=new {conversationId=taskId,hostId="local",following=true},targetClientIds=new[]{Owner}},token);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(5000);
            while(true) {
                var msg=await ReceiveAsync(timeout.Token);
                if(Text(msg,"method")!="thread-stream-state-changed"||Text(msg,"sourceClientId")!=Owner)continue;
                var p=Child(msg,"params");var change=Child(p,"change");
                if(Text(p,"conversationId")!=taskId||Text(p,"hostId")!="local"||Text(change,"type")!="snapshot")continue;
                if(Child(msg,"version").GetInt32()!=11)throw new Rejected("desktop_version_unsupported");
                return Child(change,"conversationState");
            }
        }
        internal async Task<JsonElement> RequestAsync(string method,int version,object parameters,CancellationToken token,string? target=null,int timeoutMs=5000)
        {
            string id=Guid.NewGuid().ToString();
            await WriteAsync(new {type="request",requestId=id,sourceClientId=_client,version,method,@params=parameters,targetClientId=target,timeoutMs},token);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(timeoutMs+1000);
            while(true) {
                var msg=await ReceiveAsync(timeout.Token);
                if(Text(msg,"type")=="response"&&Text(msg,"requestId")==id)return msg;
            }
        }
        private async Task<JsonElement> ReceiveAsync(CancellationToken token)
        {
            while(true) {
                byte[] header=new byte[4];await _pipe.ReadExactlyAsync(header,token);
                uint length=BinaryPrimitives.ReadUInt32LittleEndian(header);
                if(length==0||length>32*1024*1024)throw new IOException("Invalid IPC frame size");
                byte[] bytes=new byte[length];await _pipe.ReadExactlyAsync(bytes,token);
                using var doc=JsonDocument.Parse(bytes);var value=doc.RootElement.Clone();
                if(Text(value,"type")=="client-discovery-request") {
                    await WriteAsync(new {type="client-discovery-response",requestId=Text(value,"requestId"),response=new{canHandle=false}},token);
                    continue;
                }
                return value;
            }
        }
        private async Task WriteAsync(object value,CancellationToken token)
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value),header=new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(header,(uint)bytes.Length);
            await _pipe.WriteAsync(header,token);await _pipe.WriteAsync(bytes,token);await _pipe.FlushAsync(token);
        }
        public ValueTask DisposeAsync()=>_pipe.DisposeAsync();
    }
}

internal static class Tab5JsonNumber
{
    internal static double TryGetDoubleSafe(this JsonElement value)=>value.ValueKind==JsonValueKind.Number&&value.TryGetDouble(out double n)?n:0;
}
