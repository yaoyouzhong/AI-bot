using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CodexDesktopSelfTest
{
    internal static async Task RunAsync() {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        string pipe="tab5-ipc-test-"+Guid.NewGuid().ToString("N"),task=Guid.NewGuid().ToString(),turn=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString();
        int starts=0,steers=0,interrupts=0;bool completed=false,drop=false,busy=false;int snapshotVersion=11;string routerError="",lastMessage="";
        var dispatched=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accept=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clients=new List<Task>();string? expectedImage=null;
        Tab5CodexReadState.Identity? identity=new("synthetic-account",new {identity=new {kind="chatgpt",accountId="test-account",userId="test-user"},executionHostKey="local:test-host"});
        Tab5CodexReadState.Snapshot readState=new("synthetic-account",[task]);int readAcks=0;
        async Task Write(Stream s,object value) {
            byte[] b=JsonSerializer.SerializeToUtf8Bytes(value),h=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(h,(uint)b.Length);
            // Deliberately split the frame: production must tolerate partial reads.
            await s.WriteAsync(h.AsMemory(0,1),stop.Token);await s.WriteAsync(h.AsMemory(1),stop.Token);
            await s.WriteAsync(b,stop.Token);await s.FlushAsync(stop.Token);
        }
        async Task Serve(NamedPipeServerStream server) {
            await using var s=server;
            try {
                while(!stop.IsCancellationRequested) {
                    byte[] h=new byte[4];await s.ReadExactlyAsync(h,stop.Token);
                    byte[] b=new byte[BinaryPrimitives.ReadUInt32LittleEndian(h)];await s.ReadExactlyAsync(b,stop.Token);
                    using var d=JsonDocument.Parse(b);var m=d.RootElement;string method=Tab5CodexDesktop.Text(m,"method"),id=Tab5CodexDesktop.Text(m,"requestId");
                    var p=Tab5CodexDesktop.Child(m,"params");object result=new{};
                    if(method=="initialize")result=new{clientId=Guid.NewGuid().ToString()};
                    else if(method=="thread-owner-discovery")result=new{supportsUntrustedAppInput=true};
                    else if(method=="thread-read-state-changed") {
                        if(m.GetProperty("version").GetInt32()!=3||Tab5CodexDesktop.Text(p,"hostId")!="local"||Tab5CodexDesktop.Text(p,"conversationId")!=task||
                           p.GetProperty("hasUnreadTurn").GetBoolean()||Tab5CodexDesktop.Text(p.GetProperty("context"),"executionHostKey")!="local:test-host"||
                           Tab5CodexDesktop.Text(p.GetProperty("context").GetProperty("identity"),"accountId")!="test-account")throw new Exception("Read state IPC scope/version contract");
                        Interlocked.Increment(ref readAcks);readState=new("synthetic-account",[]);continue;
                    }
                    else if(method=="thread-stream-following-changed") {
                        object item=new {type="agentMessage",text=completed?"测试完成":"正在处理",phase=completed?"final_answer":"commentary"};
                        object live=new {turnId=turn,turnStartedAtMs=123000,status=completed?"completed":starts>0?"inProgress":"completed",items=new[]{item},@params=new{clientUserMessageId=lastMessage}};
                        await Write(s,new{type="broadcast",method="thread-stream-state-changed",version=snapshotVersion,sourceClientId=owner,
                            @params=new{conversationId=task,hostId="local",change=new{type="snapshot",conversationState=new {
                                threadRuntimeStatus=new{type=busy||starts>0&&!completed?"active":"idle"},rolloutPath="",cwd="C:/test",requests=Array.Empty<object>(),
                                turnHistory=new{history=new{entitiesByKey=new Dictionary<string,object>{{"turn:"+turn,live}}}}
                            }}}});continue;
                    } else if(method=="thread-follower-start-turn") {
                        var start=p.GetProperty("turnStart");var request=start.GetProperty("request");
                        if(m.GetProperty("version").GetInt32()!=2||m.GetProperty("targetClientId").GetString()!=owner||
                            !start.GetProperty("context").GetProperty("inheritThreadSettings").GetBoolean()||
                            request.GetProperty("threadId").GetString()!=task||request.GetProperty("input")[0].GetProperty("text").GetString()!="测试发送"||
                            request.TryGetProperty("model",out _)||request.TryGetProperty("approvalPolicy",out _))throw new Exception("Desktop routing/settings contract");
                        lastMessage=request.GetProperty("clientUserMessageId").GetString()!;
                        if(expectedImage is not null) {
                            var input=request.GetProperty("input");
                            if(input.GetArrayLength()!=2||input[1].GetProperty("type").GetString()!="localImage"||input[1].GetProperty("path").GetString()!=expectedImage)
                                throw new Exception("Image must be a real localImage input alongside text");
                            using var image=new Bitmap(expectedImage);
                            if(image.Width!=16||image.GetPixel(8,8).R<230)throw new Exception("Desktop must receive actual uploaded pixels");
                        }
                        starts++;dispatched.TrySetResult();await accept.Task.WaitAsync(stop.Token);
                        if(drop)return;
                        if(routerError.Length>0) {
                            await Write(s,new{type="response",requestId=id,resultType="error",error=routerError});continue;
                        }
                        result=new{result=new{turn=new{id=turn,status="inProgress"}}};
                    } else if(method=="thread-follower-steer-turn") {
                        if(m.GetProperty("version").GetInt32()!=1||p.GetProperty("conversationId").GetString()!=task||
                            p.GetProperty("restoreMessage").GetProperty("cwd").GetString()!="C:/test"||
                            p.GetProperty("restoreMessage").GetProperty("context").GetProperty("prompt").GetString()!="追加要求")throw new Exception("Steer IPC contract");
                        steers++;result=new{result=new{turnId=turn}};
                    } else if(method=="thread-follower-interrupt-turn") {
                        if(m.GetProperty("version").GetInt32()!=4||p.GetProperty("expectedTurnId").GetString()!=turn||p.GetProperty("mode").GetString()!="user-stop")throw new Exception("Interrupt IPC contract");
                        interrupts++;result=new{interruptedTurnId=turn,ok=true};
                    } else throw new Exception("Unexpected desktop IPC method: "+method);
                    await Write(s,new{type="response",requestId=id,resultType="success",handledByClientId=owner,result});
                }
            }catch(Exception ex) when(ex is IOException or OperationCanceledException) { }
        }
        var listener=Task.Run(async()=>{
            try {
                while(!stop.IsCancellationRequested) {
                    var s=new NamedPipeServerStream(pipe,PipeDirection.InOut,16,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
                    try{await s.WaitForConnectionAsync(stop.Token);}catch{s.Dispose();throw;}
                    clients.Add(Serve(s));
                }
            }catch(OperationCanceledException) { }
        });
        var desktop=new Tab5CodexDesktop(pipe,readIdentity:()=>identity,readState:()=>readState);
        using var bridge=new Tab5CodexTasks(desktop,()=>[new(task,"Synthetic desktop task","test",0)]);
        try {
            var page=await bridge.ReadPageAsync(task,0,stop.Token,includeView:true,device:"test-device");
            string receipt=JsonSerializer.SerializeToElement(page.Body).GetProperty("replyView").GetProperty("readReceipt").GetString()!;
            if(page.Status!=200||!Guid.TryParse(receipt,out _)||readAcks!=0)throw new Exception("Loading/prefetch must never acknowledge a read");
            if((await bridge.ConfirmReadAsync("other-device",task,turn,receipt,stop.Token)).Status!=409||readAcks!=0)throw new Exception("Read receipt is bound to its device");
            identity=identity! with {Scope="other-account"};
            if((await bridge.ConfirmReadAsync("test-device",task,turn,receipt,stop.Token)).Status!=409||readAcks!=0)throw new Exception("Account change invalidates read receipt");
            identity=identity with {Scope="synthetic-account"};
            string originalTurn=turn;turn=Guid.NewGuid().ToString();
            if((await bridge.ConfirmReadAsync("test-device",task,originalTurn,receipt,stop.Token)).Status!=409||readAcks!=0)throw new Exception("An older view cannot consume a newer turn");
            turn=originalTurn;
            if((await bridge.ConfirmReadAsync("test-device",task,turn,receipt,stop.Token)).Status!=200||readAcks!=1||readState.Unread.Contains(task))throw new Exception("Rendered exact turn must sync through desktop IPC");
            if((await bridge.ConfirmReadAsync("test-device",task,turn,receipt,stop.Token)).Status!=200||readAcks!=2)throw new Exception("Lost read acknowledgement can retry idempotently");
            Console.WriteLine("CODEX_REVERSE_READ_ACK_OK render_receipt, no_prefetch_write, device, identity, new_turn, ipc_v3, persisted_confirmation, retry");
            var sending=bridge.SubmitAsync(task,"测试发送",stop.Token);
            await dispatched.Task.WaitAsync(stop.Token);
            // A delayed send acknowledgement cannot block reading current activity.
            if((await bridge.ReadAsync(task,stop.Token)).Status!=200||sending.IsCompleted)throw new Exception("Read during send must remain independent");
            accept.SetResult();var sent=await sending;
            if(sent.Status!=202||completed||starts!=1)throw new Exception("Send acknowledgement must not wait for task completion");
            JsonElement Snapshot()=>JsonSerializer.SerializeToElement(bridge.Snapshot());
            if(Snapshot().GetProperty("repliesReady").EnumerateObject().Any())throw new Exception("Accepted is not completed");
            completed=true;
            for(int i=0;i<40&&!Snapshot().GetProperty("repliesReady").TryGetProperty(task,out _);i++)await Task.Delay(100,stop.Token);
            if(!Snapshot().GetProperty("repliesReady").TryGetProperty(task,out var ready)||ready.GetString()!=turn)throw new Exception("Completion notification without viewing reply");
            busy=true;
            if((await bridge.SubmitAsync(task,"测试发送",stop.Token)).Status!=409||starts!=1)throw new Exception("Busy task cannot dispatch");
            busy=false;snapshotVersion=99;
            if((await bridge.SubmitAsync(task,"测试发送",stop.Token)).Status!=503||starts!=1)throw new Exception("Unsupported desktop version must fail before dispatch");
            snapshotVersion=11;drop=true;
            var unknown=await bridge.SubmitAsync(task,"测试发送",stop.Token);
            if(unknown.Status!=504||starts!=2||JsonSerializer.SerializeToElement(unknown.Body).GetProperty("error").GetString()!="delivery_unknown")
                throw new Exception("Lost acknowledgement must not be retried or claimed as failed");
            drop=false;routerError="request-timeout";
            if((await bridge.SubmitAsync(task,"测试发送",stop.Token)).Status!=504||starts!=3)
                throw new Exception("Router timeout is also an unknown delivery outcome");
            string folder=Path.Combine(Path.GetTempPath(),"tab5-ipc-journal-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(folder,"journal.dat"),request=Guid.NewGuid().ToString();
            try {
                routerError="";drop=true;
                using(var first=new Tab5CodexTasks(new Tab5CodexDesktop(pipe),()=>[new(task,"test","test",0)],new Tab5CodexJournal(path))) {
                    if((await first.SubmitAsync(task,"测试发送",stop.Token,request)).Status!=504||starts!=4)throw new Exception("Persist uncertain delivery");
                }
                drop=false;
                var images=new Tab5CodexImages(Path.Combine(folder,"images"));
                using var restarted=new Tab5CodexTasks(new Tab5CodexDesktop(pipe),()=>[new(task,"test","test",0)],new Tab5CodexJournal(path),images);
                if((await restarted.ReceiptAsync(task,request,stop.Token)).Status!=202||starts!=4)throw new Exception("Recover lost ack from desktop message ID without resending");
                if((await restarted.SubmitAsync(task,"测试发送",stop.Token,request)).Status!=202||starts!=4)throw new Exception("Replayed request cannot dispatch twice");
                if((await restarted.SubmitAsync(task,"changed",stop.Token,request)).Status!=409||starts!=4)throw new Exception("Idempotency ID cannot change meaning");
                completed=false;
                if((await restarted.SubmitAsync(task,"追加要求",stop.Token,Guid.NewGuid().ToString(),"steer",turn)).Status!=202||steers!=1)throw new Exception("Append to running turn");
                if((await restarted.SubmitAsync(task,"",stop.Token,Guid.NewGuid().ToString(),"interrupt",Guid.NewGuid().ToString())).Status!=409||interrupts!=0)throw new Exception("Cannot stop a different turn");
                if((await restarted.SubmitAsync(task,"",stop.Token,Guid.NewGuid().ToString(),"interrupt",turn)).Status!=202||interrupts!=1)throw new Exception("Stop expected turn");
                string imageId=Guid.NewGuid().ToString();byte[] bytes;
                using(var bitmap=new Bitmap(16,16))using(var stream=new MemoryStream()) {
                    using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.Red);
                    bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Jpeg);bytes=stream.ToArray();
                }
                if(restarted.UploadImage("paired",task,imageId,Convert.ToBase64String(bytes)).Status!=200)throw new Exception("Photo staging");
                if(restarted.UploadImage("paired",task,Guid.NewGuid().ToString(),"bad base64!").Status!=400)throw new Exception("Reject malformed image");
                try{images.Resolve("other-device",task,[imageId],true);throw new Exception("Cross-device image accepted");}catch(ArgumentException) { }
                try{images.Resolve("paired",Guid.NewGuid().ToString(),[imageId],true);throw new Exception("Cross-task image accepted");}catch(ArgumentException) { }
                if(restarted.Draft(task,Guid.NewGuid().ToString(),"",[imageId],"paired").Status!=200||new Tab5CodexJournal(path).Images(task).Single()!=imageId)
                    throw new Exception("Image-only draft must survive restart");
                expectedImage=images.Resolve("paired",task,[imageId],true).Single();completed=true;
                string imageRequest=Guid.NewGuid().ToString();
                if((await restarted.SubmitAsync(task,"测试发送",stop.Token,imageRequest,images:[imageId],device:"paired")).Status!=202)throw new Exception("Text and photo same turn");
                if((await restarted.SubmitAsync(task,"测试发送",stop.Token,imageRequest,images:[],device:"paired")).Status!=409)throw new Exception("Removing image changes idempotency payload");
                if(new Tab5CodexJournal(path).Images(task).Length!=0||!File.ReadAllBytes(expectedImage).SequenceEqual(bytes))throw new Exception("Acknowledgement clears draft but keeps actual conversation image");
                expectedImage=null;
            }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }finally{stop.Cancel();await listener;await Task.WhenAll(clients);}
        Console.WriteLine("TAB5 desktop IPC: routing, read-during-send, immediate acceptance, completion notification, busy/version/lost-reply checks passed");
    }
}
