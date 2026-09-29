using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5HistorySelfTest
{
    internal static async Task RunAsync() {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        string pipe="tab5-history-test-"+Guid.NewGuid().ToString("N"),task=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString();
        string older=Guid.NewGuid().ToString(),middle=Guid.NewGuid().ToString(),latest=Guid.NewGuid().ToString();
        bool loaded=false,complete=false;int opens=0,starts=0,historyLoads=0,snapshots=0;
        var clients=new List<Task>();
        async Task Write(Stream stream,object value) {
            byte[] b=JsonSerializer.SerializeToUtf8Bytes(value),header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,b.Length);
            await stream.WriteAsync(header,stop.Token);await stream.WriteAsync(b,stop.Token);
        }
        async Task Serve(NamedPipeServerStream stream) {
            await using var s=stream;
            try {
                while(!stop.IsCancellationRequested) {
                    byte[] h=new byte[4];await s.ReadExactlyAsync(h,stop.Token);
                    byte[] b=new byte[BinaryPrimitives.ReadUInt32LittleEndian(h)];await s.ReadExactlyAsync(b,stop.Token);
                    using var doc=JsonDocument.Parse(b);var m=doc.RootElement;string method=Tab5CodexDesktop.Text(m,"method"),id=Tab5CodexDesktop.Text(m,"requestId");
                    var p=Tab5CodexDesktop.Child(m,"params");object result=new{};
                    if(method=="initialize")result=new{clientId=Guid.NewGuid().ToString()};
                    else if(method=="thread-owner-discovery") {
                        if(!loaded){await Write(s,new{type="response",requestId=id,resultType="error",error="no-client-found"});continue;}
                        result=new{supportsUntrustedAppInput=true};
                    }else if(method=="thread-follower-load-complete-history") {
                        if(m.GetProperty("version").GetInt32()!=1||p.GetProperty("conversationId").GetString()!=task||m.GetProperty("targetClientId").GetString()!=owner)throw new Exception("History routing");
                        complete=true;historyLoads++;result=new{revision=2};
                    }else if(method=="thread-stream-following-changed") {
                        if(snapshots++==0) {
                            await Write(s,new{type="broadcast",method="thread-stream-state-changed",version=11,sourceClientId=owner,
                                @params=new{conversationId=task,hostId="local",change=new{type="snapshot",conversationState=new {resumeState="resuming"}}}});
                            continue;
                        }
                        object Turn(string tid,int order,string text)=>new{turnId=tid,turnStartedAtMs=order*1000,status="completed",items=new[]{new{type="agentMessage",phase="final_answer",text}}};
                        // Deliberately unordered canonical map; legacy duplicate must not add a turn.
                        var turns=new Dictionary<string,object>{{latest,Turn(latest,3,"最新回复")},{middle,Turn(middle,2,string.Concat(Enumerable.Repeat("上一轮回复：检查连接、缓存及历史分页。\n",240)))}};
                        if(complete)turns.Add(older,Turn(older,1,"最早回复"));
                        await Write(s,new{type="broadcast",method="thread-stream-state-changed",version=11,sourceClientId=owner,
                            @params=new{conversationId=task,hostId="local",change=new{type="snapshot",conversationState=new {
                                threadRuntimeStatus=new{type="idle"},rolloutPath="",requests=Array.Empty<object>(),
                                turns=new[]{Turn(latest,3,"重复旧值")},turnsPagination=new{hasLoadedOldest=false},
                                turnHistory=new{history=new{entitiesByKey=turns,isComplete=complete}}
                            }}}});continue;
                    }else if(method=="thread-follower-start-turn") {
                        if(!loaded)throw new Exception("Cannot dispatch before load");starts++;result=new{result=new{turn=new{id=latest}}};
                    }else throw new Exception("Unexpected history test method "+method);
                    await Write(s,new{type="response",requestId=id,resultType="success",handledByClientId=owner,result});
                }
            }catch(Exception ex) when(ex is IOException or OperationCanceledException) { }
        }
        var listener=Task.Run(async()=>{
            try{while(!stop.IsCancellationRequested){var s=new NamedPipeServerStream(pipe,PipeDirection.InOut,8,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);try{await s.WaitForConnectionAsync(stop.Token);}catch{s.Dispose();throw;}clients.Add(Serve(s));}}
            catch(OperationCanceledException) { }
        });
        string journalRoot=Path.Combine(Path.GetTempPath(),"tab5-history-journal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(journalRoot);
        var desktop=new Tab5CodexDesktop(pipe,id=>{if(id!=task)throw new Exception("Wrong thread launch");opens++;loaded=true;return true;});
        try {
            try{await desktop.ReadAsync(task,stop.Token);throw new Exception("Watcher opened unloaded thread");}catch(Tab5CodexDesktop.Rejected) { }
            if(opens!=0)throw new Exception("Background must not launch");
            await desktop.SendAsync(task,"manual",()=>{},stop.Token);
            if(opens!=1||starts!=1)throw new Exception("Load then exactly one send");
            var current=await desktop.ReadAsync(task,stop.Token,ensureLoaded:true);
            if(current.TurnId!=latest||current.IsHistory||!current.HasPrevious||current.HasNext)throw new Exception("Latest selection");
            var prior=await desktop.ReadAsync(task,stop.Token,latest,true,-1);
            if(prior.TurnId!=middle||!prior.IsHistory||!prior.HasPrevious||!prior.HasNext||historyLoads!=0||prior.Current.TurnId!=latest)throw new Exception("Previous turn and live control isolation");
            var oldest=await desktop.ReadAsync(task,stop.Token,middle,true,-1);
            if(oldest.TurnId!=older||oldest.HasPrevious||historyLoads!=1||!oldest.HasNext)throw new Exception("Fetch beyond loaded boundary");
            var next=await desktop.ReadAsync(task,stop.Token,older,true,1);
            if(next.TurnId!=middle)throw new Exception("Next turn");
            var pinned=await desktop.ReadAsync(task,stop.Token,middle,true);
            if(pinned.TurnId!=middle||!pinned.Receipt.StartsWith("历史")||starts!=1)throw new Exception("History read stays pinned and never sends");
            try{await desktop.ReadAsync(task,stop.Token,Guid.NewGuid().ToString(),true);throw new Exception("Unknown history silently substituted");}catch(Tab5CodexDesktop.Rejected ex) when(ex.Code=="history_turn_unavailable") { }
            using(var tasks=new Tab5CodexTasks(desktop,()=>[new(task,"test","test",0)],new Tab5CodexJournal(Path.Combine(journalRoot,"draft.dat")))) {
                int beforeOpen=opens;loaded=false;
                if((await tasks.ReadPageAsync(task,-1,stop.Token)).Status!=503||opens!=beforeOpen)
                    throw new Exception("Viewing unloaded thread must never navigate the desktop");
                loaded=true;
                string temp=Path.Combine(Path.GetTempPath(),"tab5-inline-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
                try {
                    var store=new Tab5PairingStore(Path.Combine(temp,"pair.dat"));var pair=store.Pair("001122334455",@"USB\TEST");
                    var key=Convert.FromBase64String(pair.Key);
                    using var service=new Tab5Service(store,tasks);
                    service.Publish(new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("offline",null)));
                    using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
                    int legacyBytes=0;
                    int multiPageBytes=0;
                    for(int n=0;n<12;n++) {
                        int snapshotsBefore=snapshots;
                        string nonce=Guid.NewGuid().ToString("N");
                        var packet=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new {kind="codex",op="read",session,taskId=task,message="",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),inlineReply=true,rpcReplyVersion=n<3||n>=6?1:0,packedReply=n>=9,page=-1,turnId=n%3==1?middle:""}));
                        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packet)).ToLowerInvariant();
                        (int Status,object Body) response;
                        if(n<3)response=await service.SubmitCodexAsync(pair.DeviceId,nonce,Tab5Protocol.Proof(key,$"POST|{pair.DeviceId}|{nonce}|{hash}"),packet,stop.Token,true);
                        else {
                            var rpc=await service.RpcAsync(pair.DeviceId,nonce,Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{hash}"),packet,stop.Token);
                            try {Tab5Protocol.Decrypt(key,new string('f',32),rpc.Packet!);throw new Exception("Wrong RPC nonce accepted");}catch(System.Security.Cryptography.CryptographicException) { }
                            if(n==3)legacyBytes=rpc.Packet!.Length;
                            if(n==7)multiPageBytes=rpc.Packet!.Length;
                            if(n==10) {
                                if(rpc.Packet!.Length>=multiPageBytes/2)throw new Exception("Packed batch response failed to reduce long reply traffic");
                                Console.WriteLine($"TAB5_FIRST_PAGE_BYTES withNeighbors={multiPageBytes} packedBatch={rpc.Packet.Length}");
                            }
                            if(n==6) {
                                if(rpc.Packet!.Length>=legacyBytes)throw new Exception("Compact RPC did not reduce page bytes");
                                Console.WriteLine($"TAB5_PAGE_WIRE_BYTES legacy={legacyBytes} compact={rpc.Packet.Length}");
                            }
                            using var body=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,rpc.Packet!));
                            response=(body.RootElement.GetProperty("status").GetInt32(),body.RootElement.GetProperty("body").Clone());
                        }
                        if(response.Status!=200)throw new Exception("Sequential page click rejected: "+response.Status);
                        if(snapshots!=snapshotsBefore+1)throw new Exception("Neighbor prefetch opened extra owner snapshots");
                        var envelope=JsonSerializer.SerializeToElement(response.Body);
                        if(n>=9&&envelope.TryGetProperty("packedReply",out var packedFlag)&&packedFlag.GetBoolean()) {
                            if(n==10&&Environment.GetEnvironmentVariable("AIBOT_TEST_PACKED_PAGE") is {} output)File.WriteAllText(output,envelope.GetRawText());
                            using var input=new MemoryStream(Convert.FromBase64String(envelope.GetProperty("payload").GetString()!));
                            using var unzip=new System.IO.Compression.ZLibStream(input,System.IO.Compression.CompressionMode.Decompress);
                            using var unpacked=new MemoryStream();await unzip.CopyToAsync(unpacked,stop.Token);
                            if(unpacked.Length!=envelope.GetProperty("size").GetInt32())throw new Exception("Packed page size mismatch");
                            using var decoded=JsonDocument.Parse(unpacked.ToArray());envelope=decoded.RootElement.Clone();
                        }
                        if(n==10&&envelope.GetProperty("neighbors").GetArrayLength()==0)throw new Exception("Packed reply dropped all prefetched neighbors");
                        if(n==10) {
                            var v=envelope.GetProperty("replyView");
                            if(v.GetProperty("previousTurnId").GetString()!=older||v.GetProperty("nextTurnId").GetString()!=latest||
                               !envelope.GetProperty("neighbors").EnumerateArray().Any(x=>x.GetProperty("replyView").GetProperty("turnId").GetString()==older)||
                               !envelope.GetProperty("neighbors").EnumerateArray().Any(x=>x.GetProperty("replyView").GetProperty("turnId").GetString()==latest))throw new Exception("Batch lost adjacent turn content or order");
                        }
                        byte[] clearBytes;
                        if(n>=6) {
                            if(envelope.GetProperty("rpcReplyVersion").GetInt32()!=1||envelope.TryGetProperty("encrypted",out _))throw new Exception("Compact RPC format mismatch");
                            clearBytes=JsonSerializer.SerializeToUtf8Bytes(envelope);
                        }else {
                            if(envelope.TryGetProperty("text",out _)||!envelope.TryGetProperty("encrypted",out var encrypted))throw new Exception("HTTP and legacy inline text must be encrypted");
                            var wire=Convert.FromBase64String(encrypted.GetString()!);
                            clearBytes=Tab5Protocol.Decrypt(key,nonce,wire);
                            try {Tab5Protocol.Decrypt(key,new string('f',32),wire);throw new Exception("Wrong response nonce accepted");}catch(System.Security.Cryptography.CryptographicException) { }
                        }
                        using var clear=JsonDocument.Parse(clearBytes);
                        string expected=n%3==1?middle:latest;
                        if(clear.RootElement.GetProperty("replyView").GetProperty("turnId").GetString()!=expected||!clear.RootElement.GetProperty("text").GetString()!.Contains(n%3==1?"上一轮回复":"最新回复"))throw new Exception("Inline reply and page disagree");
                        await Task.Delay(120,stop.Token);
                    }
                    // Exercise the same public send route through encrypted RPC.
                    string sendId=Guid.NewGuid().ToString();int sentBefore=starts;
                    async Task<int> SendRpc() {
                        string n=Guid.NewGuid().ToString("N");
                        byte[] b=Tab5Protocol.Encrypt(key,n,JsonSerializer.SerializeToUtf8Bytes(new{kind="codex",op="send",session,taskId=task,requestId=sendId,message="synthetic RPC send",issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                        string h=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)).ToLowerInvariant();
                        var r=await service.RpcAsync(pair.DeviceId,n,Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{n}|{h}"),b,stop.Token);
                        using var reply=JsonDocument.Parse(Tab5Protocol.Decrypt(key,n,r.Packet!));return reply.RootElement.GetProperty("status").GetInt32();
                    }
                    if(await SendRpc()!=202||starts!=sentBefore+1)throw new Exception("RPC send did not dispatch once");
                    await Task.Delay(1100,stop.Token);
                    int duplicate=await SendRpc();
                    if(duplicate!=202||starts!=sentBefore+1)throw new Exception("RPC requestId retry dispatched twice");
                    // Read public text from an unloaded thread without an owner or UI action.
                    string sessions=Path.Combine(temp,"sessions");Directory.CreateDirectory(sessions);
                    string log=Path.Combine(sessions,"test.jsonl");
                    string Event(object payload)=>JsonSerializer.Serialize(new {type="event_msg",timestamp="2026-09-27T01:00:00Z",payload});
                    File.WriteAllLines(log,[Event(new{type="task_started",turn_id=older}),Event(new{type="task_complete",turn_id=older,last_agent_message="旧回复"}),
                        Event(new{type="task_started",turn_id=latest}),JsonSerializer.Serialize(new {type="response_item",payload=new{type="message",role="assistant",phase="commentary",content=new[]{new{type="output_text",text="新文字"}}}}),
                        Event(new{type="item_completed",item=new{type="CommandExecution",id="tool",status="completed",command="SECRET",stdout="SECRET"}})]);
                    var stored=new Tab5StoredReplies();var local=stored.Read(log,temp,"",0,-1)!;
                    if(local.Page.Text.Trim()!="新文字"||local.Page.State!="stored"||!local.HasPrevious)throw new Exception("Silent local text/status");
                    var previous=stored.Read(log,temp,latest,-1,0)!;
                    if(previous.Page.Text.Trim()!="旧回复"||previous.HasPrevious||!previous.HasNext)throw new Exception("Silent history navigation");
                    if(stored.Read(log,Path.Combine(temp,"other"),"",0,0)!=null)throw new Exception("History root boundary");
                    string[] batchTurns=Enumerable.Range(0,5).Select(_=>Guid.NewGuid().ToString()).ToArray();
                    string batchLog=Path.Combine(sessions,"batch.jsonl");
                    File.WriteAllLines(batchLog,batchTurns.SelectMany((id,index)=>new[]{Event(new{type="task_started",turn_id=id}),Event(new{type="task_complete",turn_id=id,last_agent_message=$"缓存轮次 {index}"})}));
                    loaded=false;
                    using(var localTasks=new Tab5CodexTasks(desktop,()=>[new(task,"test","test",0)],rolloutPath:_=>batchLog,historyHome:temp)) {
                        var read=await localTasks.ReadPageAsync(task,0,stop.Token,includeView:true);
                        var body=JsonSerializer.SerializeToElement(read.Body);
                        if(read.Status!=200||!localTasks.ReadDiagnostic.Contains("source=stored"))throw new Exception("Local history route was not exercised");
                        var neighbors=body.GetProperty("neighbors").EnumerateArray().ToArray();
                        if(neighbors.Length!=3||!neighbors.Select(x=>x.GetProperty("replyView").GetProperty("turnId").GetString()).SequenceEqual(batchTurns.Skip(1).Take(3).Reverse()))throw new Exception("Stored history did not prefetch three older turns");
                        if(body.GetProperty("replyView").GetProperty("previousTurnId").GetString()!=batchTurns[3]||
                           neighbors[0].GetProperty("replyView").GetProperty("previousTurnId").GetString()!=batchTurns[2]||
                           neighbors[0].GetProperty("replyView").GetProperty("nextTurnId").GetString()!=batchTurns[4])throw new Exception("Stored prefetch adjacency missing");
                        var middleRead=await localTasks.ReadPageAsync(task,0,stop.Token,batchTurns[2],includeView:true);
                        var middleBody=JsonSerializer.SerializeToElement(middleRead.Body);
                        if(!middleBody.GetProperty("neighbors").EnumerateArray().Select(x=>x.GetProperty("replyView").GetProperty("turnId").GetString()).SequenceEqual(new[]{batchTurns[1],batchTurns[3],batchTurns[0]}))throw new Exception("Stored older/newer priority incorrect");
                        Console.WriteLine("TAB5_STORED_BATCH_OK owner unavailable, three older turns, both adjacency IDs, no desktop launch");
                    }
                    loaded=true;
                    var bounded=Tab5Service.BoundReadBatch(new {text="current remains",neighbors=new[]{new {turn="previous",text=new string('x',5000)},new {turn="next",text=new string('y',5000)},new {turn="older2",text=new string('z',5000)},new {turn="same-page",text=new string('p',5000)}}});
                    using(var trimmed=JsonDocument.Parse(bounded)) {
                        var kept=trimmed.RootElement.GetProperty("neighbors");
                        if(bounded.Length>16000||kept.GetArrayLength()!=3||kept[0].GetProperty("turn").GetString()!="previous"||trimmed.RootElement.GetProperty("text").GetString()!="current remains")throw new Exception("Batch trimming lost priority or current page");
                    }
                    Console.WriteLine("TAB5_BATCH_BUDGET_OK current page unchanged; immediate previous survives 16KB trimming");
                }finally{Directory.Delete(temp,true);}
                if(opens!=beforeOpen)throw new Exception("Read-only navigation changed desktop selection");
            }
            loaded=false;
            var failed=new Tab5CodexDesktop(pipe,_=>false);
            try{await failed.SendAsync(task,"manual",()=>{throw new Exception("Dispatched failed launch");},stop.Token);throw new Exception("Launch failure accepted");}catch(Tab5CodexDesktop.Rejected ex) when(ex.Code=="desktop_load_failed") { }
            if(starts!=2)throw new Exception("Failed load must not send");
            if(Tab5CodexDesktop.ThreadUri(task)!="codex://threads/"+task)throw new Exception("URI contract");
            try{Tab5CodexDesktop.ThreadUri("bad?prompt=send");throw new Exception("URI injection");}catch(FormatException) { }
        }finally{stop.Cancel();await listener;await Task.WhenAll(clients);Directory.Delete(journalRoot,true);}
        Console.WriteLine("TAB5_HISTORY_PASS: lazy load, no background launch, exact-once dispatch, canonical history, older boundary, pinned selection, failure preservation");
    }
}
