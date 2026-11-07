using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class ClaudeNativeWorkspaceSelfTest
{
    private sealed class Native(Guid session,string path,Guid initial):IClaudeNativeSession
    {
        public ClaudeSessionSource Source {get;set;}=ClaudeSessionSource.VSCode;
        internal string DesktopText="",Epoch="original-native-a";internal bool Accept=true;
        internal int Types,Submits,Interrupts;internal Guid LastUser;private Guid _leaf=initial;private string? _queued;
        internal string Runtime="idle";internal bool CurrentAtType=true;
        internal Action<string>? OnType;
        public string State=>Runtime;public string Scope()=>Epoch;
        public bool HasSessionApi {get;set;}=true;
        internal bool DirectTerminalInput;
        public bool CanAddressInput=>HasSessionApi||DirectTerminalInput;
        public bool CanAppend=>true;public bool CanInterrupt=>true;
        internal void Deliver(){if(_queued is not {} text)return;_queued=null;LastUser=Guid.NewGuid();var context=Guid.NewGuid();var answer=Guid.NewGuid();
            object[] rows=[new{type="user",uuid=LastUser,sessionId=session,parentUuid=_leaf,isSidechain=false,origin=new{kind="human"},message=new{role="user",content=new[]{new{type="text",text}}},timestamp=DateTimeOffset.UtcNow.ToString("O")},
                new{type="attachment",uuid=context,sessionId=session,parentUuid=LastUser,message=new{role="assistant",content=new[]{new{type="text",text="PRIVATE_NATIVE_CONTEXT"}}}},
                new{type="assistant",uuid=answer,sessionId=session,parentUuid=context,message=new{role="assistant",content=new[]{new{type="text",text="Native public reply"}},stop_reason="end_turn"},timestamp=DateTimeOffset.UtcNow.ToString("O")}];
            File.AppendAllText(path,string.Join("\n",rows.Select(r=>JsonSerializer.Serialize(r)))+"\n",new UTF8Encoding(false));_leaf=answer;
        }
        public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
            return ExecuteAsync(new("send",text,[],[],()=>true),history,dispatch,authorized,token);
        }
        public Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
            if(request.Operation=="interrupt"){
                if(!authorized()||!request.Current())return Task.FromResult(new ClaudeNativeSend.Result(false,false,null,"turn_changed"));
                dispatch();Interrupts++;Runtime="idle";return Task.FromResult(new ClaudeNativeSend.Result(false,true,null,"",true));
            }
            var calls=new ClaudeNativeSend.Calls(()=>new(session,Epoch,"native-editor",DesktopText,DesktopText.Length==0,true,DesktopText.Length>0),()=>Epoch,
                value=>{Types++;DesktopText=value;OnType?.Invoke(value);},()=>{Submits++;_queued=DesktopText;DesktopText="";if(Accept)Deliver();},
                history,authorized,dispatch,_=>Task.CompletedTask,request.ImageDigests,()=>CurrentAtType&&request.Current());
            return ClaudeNativeSend.SendAsync(calls,request.Text,token);
        }
    }
    internal static async Task RunAsync(){
        await ClaudeHarnessSelfTest.RunAsync();
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"aibot-native-workspace-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);string marker=Guid.NewGuid().ToString();File.WriteAllText(Path.Combine(root,".owned-fixture"),marker);
        Guid session=Guid.NewGuid(),initial=Guid.NewGuid();string path=Path.Combine(root,session+".jsonl");
        File.WriteAllText(path,JsonSerializer.Serialize(new{type="user",uuid=initial,sessionId=session,parentUuid=(Guid?)null,cwd=root,message=new{role="user",content="Private initial native input"},timestamp=DateTimeOffset.UtcNow.ToString("O")})+"\n");
        Guid secondSession=Guid.NewGuid(),secondPrompt=Guid.NewGuid();string secondPath=Path.Combine(root,secondSession+".jsonl");
        File.WriteAllText(secondPath,JsonSerializer.Serialize(new{type="user",uuid=secondPrompt,sessionId=secondSession,parentUuid=(Guid?)null,cwd=root,message=new{role="user",content="Other native fixture"},timestamp=DateTimeOffset.UtcNow.ToString("O")})+"\n");
        var native=new Native(session,path,initial);var secondNative=new Native(secondSession,secondPath,secondPrompt){Source=ClaudeSessionSource.Terminal};
        int checks=0,cliLookups=0;bool allowed=true,connected=true;ClaudeWorkspace? workspace=null;
        void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        async Task Open(){workspace=new(()=>true,root,Path.Combine(root,"sealed"),executable:()=>{cliLookups++;throw new InvalidOperationException("A native session must never invoke a CLI owner");},nativeFactory:id=>!connected?null:id==session?native:id==secondSession?secondNative:null);workspace.Start();for(int i=0;i<150&&workspace.Alias(session) is null;i++)await Task.Delay(20);Check(workspace.Alias(session) is not null,"Native fixture catalog not available");}
        try {
            var newId=Guid.NewGuid();var secret=new byte[32];
            Check(ClaudeHistory.Read(root,newId,secret,()=>true).Accepted.Count==0,"Verified blank session had a fabricated user turn");
            bool historyUnavailable=false;try{ClaudeHistory.Read(root,newId,secret,()=>false);}catch(IOException){historyUnavailable=true;}
            Check(historyUnavailable,"Missing transcript without a live blank-panel marker was accepted");
            File.WriteAllText(Path.Combine(root,newId+".jsonl"),"incomplete");
            historyUnavailable=false;try{ClaudeHistory.Read(root,newId,secret,()=>true);}catch(IOException){historyUnavailable=true;}
            Check(historyUnavailable,"Malformed existing transcript was treated as a blank panel");
            await Open();string task=workspace!.Alias(session)!;
            string secondTask=workspace.Alias(secondSession)!;
            for(int i=0;i<150;i++){
                var early=JsonSerializer.SerializeToElement(workspace.Snapshot()).GetProperty("controls");
                if(early.TryGetProperty(task,out var a)&&a.ValueKind==JsonValueKind.Object&&early.TryGetProperty(secondTask,out var b)&&b.ValueKind==JsonValueKind.Object)break;
                await Task.Delay(20);
            }
            var unviewed=JsonSerializer.SerializeToElement(workspace.Snapshot());
            Check(unviewed.GetProperty("tasks").EnumerateArray().Single(t=>t.GetProperty("id").GetString()==task).GetProperty("source").GetProperty("id").GetString()=="claude.vscode"&&
                unviewed.GetProperty("tasks").EnumerateArray().Single(t=>t.GetProperty("id").GetString()==secondTask).GetProperty("source").GetProperty("id").GetString()=="claude.terminal","Source crossed sessions or depended on title/read");
            Check(unviewed.GetProperty("controls").GetProperty(task).GetProperty("canSend").GetBoolean()&&unviewed.GetProperty("controls").GetProperty(secondTask).GetProperty("canSend").GetBoolean()&&native.Submits==0&&secondNative.Submits==0,"Unviewed native sessions require a reply read before enabling input");
            async Task<(int Status,JsonElement Body)> Rpc(string op,string text="",string? request=null,string device="native-device-a",string[]? images=null,string expected=""){var result=await workspace!.HandleAsync(JsonSerializer.SerializeToElement(new{op,taskId=task,requestId=request??Guid.NewGuid().ToString(),message=text,images=images??[],expectedTurn=expected}),device,default,()=>allowed);return(result.Status,JsonSerializer.SerializeToElement(result.Body));}
            native.OnType=text=>{string journalPath=Directory.GetFiles(Path.Combine(root,"sealed","devices"),"drafts.dat",SearchOption.AllDirectories).Single();var journal=new Tab5CodexJournal(journalPath);
                var pending=journal.Pending(task);Check(pending is {NativeReceipt:not null}&&pending.NativeReceipt.Session==session&&pending.NativeReceipt.Scope==native.Epoch,"Native baseline was not durably sealed before typing");
                Check(!Encoding.UTF8.GetString(File.ReadAllBytes(journalPath)).Contains(text),"Native journal leaked plaintext");};
            var sent=await Rpc("send","原客户端文字",request:Guid.NewGuid().ToString());
            Check(sent.Status==202&&native.Types==1&&native.Submits==1,"Native send did not dispatch once");
            string actualTurn=sent.Body.GetProperty("turnId").GetString()!;Check(actualTurn!=native.LastUser.ToString(),"Native raw user ID leaked");
            Check((await Rpc("read")).Status==200,"Native reply read failed");
            var snapshot=JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a"));
            Check(snapshot.GetProperty("replyView").GetProperty("state").GetString()=="completed"&&snapshot.GetProperty("control").GetProperty("canSend").GetBoolean(),"Native completion/control projection failed");
            Check(snapshot.GetProperty("controls").GetProperty(secondTask).GetProperty("taskId").GetString()==secondTask&&snapshot.GetProperty("controls").GetProperty(secondTask).GetProperty("canSend").GetBoolean(),"Reading one session hid another session's input capability");
            var bounded=JsonSerializer.SerializeToElement(workspace.Snapshot(limit:1,requiredTask:task));
            Check(bounded.GetProperty("controls").EnumerateObject().Count()==bounded.GetProperty("tasks").GetArrayLength(),"Controls escaped the bounded transport catalog");
            Check(snapshot.GetProperty("receipts").GetProperty(task).GetString()=="已完成"&&snapshot.GetProperty("responses").GetProperty(task).GetString()=="Native public reply","Periodic snapshot lost the visible reply or its completion receipt");
            Check(snapshot.GetProperty("replyView").GetProperty("startedLabel").GetString() is {Length:11},"Native history date label differs from the Codex reply contract");
            Check(!snapshot.GetRawText().Contains("PRIVATE_NATIVE_CONTEXT")&&!snapshot.GetRawText().Contains(session.ToString()),"Native private context or raw session leaked");
            Check(workspace.Approvals().Length==0,"Native original session was routed to bridge approval");
            native.DesktopText="PC draft";await Rpc("draft-save","Device draft");
            Check((await Rpc("send","Device draft")).Status==409&&native.DesktopText=="PC draft"&&native.Types==1,"Existing PC draft was overwritten");
            await Rpc("draft-load");Check(JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Device draft","Rejected native send erased the device draft");
            native.DesktopText="";allowed=false;Check((await Rpc("send","Not authorized")).Status==403&&native.Types==1,"Revoked device authorization reached native input");allowed=true;
            native.Accept=false;string unknown=Guid.NewGuid().ToString();
            var pendingReply=await Rpc("send","Pending native delivery",unknown);
            Check(pendingReply.Status==504&&pendingReply.Body.GetProperty("canConfirmDelivered").GetBoolean()&&native.Types==2&&native.Submits==2,"Unknown native delivery was incorrectly accepted or lost its guarded recovery");
            Check((await Rpc("send","Do not resend")).Status==409&&native.Types==2,"Pending native request was automatically repeated");
            Check((await Rpc("receipt",request:unknown,device:"native-device-b")).Status==404,"Native receipt crossed devices");
            workspace.Dispose();workspace=null;await Open();Check(workspace!.Alias(session)==task,"Native alias changed across restart");
            await Rpc("draft-save","Newer device draft");native.Deliver();
            var recovered=await Rpc("receipt",request:unknown);Check(recovered.Status==202&&native.Types==2&&native.Submits==2,"Restored native receipt did not recover without sending");
            await Rpc("draft-load");Check(JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Newer device draft","Native receipt recovery cleared a newer draft");
            Check((await Rpc("send","Conflicting body",unknown)).Status==409&&native.Types==2,"Native duplicate request accepted changed content");
            string manual=Guid.NewGuid().ToString();const string manualText="Manual native delivery confirmation";
            Check((await Rpc("send",manualText,manual)).Status==504&&native.Types==3&&native.Submits==3,"Second native unknown send missing");
            Check((await Rpc("receipt-confirm",manualText,manual,device:"native-device-b")).Status==409,"A different device confirmed the native request");
            Check((await Rpc("receipt-confirm","Different body",manual)).Status==409,"Changed native body was confirmed");
            await Rpc("draft-save","Newer than confirmation");
            Check((await Rpc("receipt-confirm",manualText,manual)).Status==409,"Confirmation ignored a newer device draft");
            await Rpc("draft-load");Check(JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Newer than confirmation","Rejected confirmation erased newer input");
            await Rpc("draft-save",manualText);var confirmed=await Rpc("receipt-confirm",manualText,manual);
            Check(confirmed.Status==202&&confirmed.Body.GetProperty("turnId").GetString()==""&&native.Types==3&&native.Submits==3,"User confirmation invented an execution turn or resent input");
            Check((await Rpc("receipt",request:manual)).Status==202&&native.Submits==3,"Confirmed native receipt was not retained");
            native.Accept=true;native.OnType=null;
            var fresh=await Rpc("send","Start current turn");string current=fresh.Body.GetProperty("turnId").GetString()!;native.Runtime="busy";
            int beforeTypes=native.Types,beforeSubmits=native.Submits;
            Check((await Rpc("steer","Stale append",expected:actualTurn)).Status==409&&native.Types==beforeTypes,"Append ignored expected original turn");
            Check((await Rpc("interrupt",expected:actualTurn)).Status==409&&native.Interrupts==0,"Interrupt targeted an old turn");
            var appended=await Rpc("steer","Append to original",expected:current);
            Check(appended.Status==202&&native.Types==beforeTypes+1&&native.Submits==beforeSubmits+1,"Append did not reach original session once");
            current=appended.Body.GetProperty("turnId").GetString()!;
            await Rpc("draft-save","Keep device draft during stop");native.DesktopText="Keep PC draft during stop";
            string stop=Guid.NewGuid().ToString();var stopped=await Rpc("interrupt",request:stop,expected:current);
            Check(stopped.Status==202&&native.Interrupts==1&&native.DesktopText=="Keep PC draft during stop","Stop changed native draft or did not execute once");
            Check((await Rpc("interrupt",request:stop,expected:current)).Status==202&&native.Interrupts==1,"Retry repeated native interrupt");
            var stoppedView=JsonSerializer.SerializeToElement((await Rpc("read")).Body);
            Check(stoppedView.GetProperty("turnId").GetString()==current&&stoppedView.GetProperty("replyView").GetProperty("state").GetString()=="interrupted","Confirmed stop disappeared when re-reading the same turn");
            Check(JsonSerializer.SerializeToElement(workspace.Snapshot()).GetProperty("receipts").GetProperty(task).GetString()=="已中断","Periodic reply snapshot retained a previous completion receipt after stop");
            await Rpc("draft-load");Check(JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Keep device draft during stop","Interrupt cleared device draft");
            native.DesktopText="";native.Runtime="busy";native.CurrentAtType=false;beforeTypes=native.Types;
            Check((await Rpc("steer","Raced original turn",expected:current)).Status==409&&native.Types==beforeTypes,"Changed turn reached native staging");
            workspace.StopOwners();Check(native.Epoch=="original-native-a","Bridge shutdown took ownership of original client");
            native.Runtime="idle";native.CurrentAtType=true;native.DesktopText="";
            native.OnType=_=>throw new IOException("native_composer_changed");
            var noSubmit=Guid.NewGuid().ToString();
            beforeSubmits=native.Submits;
            var refused=await Rpc("send","q",noSubmit);
            Check(refused.Status==409&&refused.Body.GetProperty("deliveryFinal").GetBoolean()&&native.Submits==beforeSubmits,"Pre-submit rejection left the device locked");
            await Rpc("draft-save","Edited after rejection");
            workspace.Dispose();workspace=null;await Open();
            var rejectedReceipt=await Rpc("receipt",request:noSubmit);
            Check(rejectedReceipt.Status==409&&rejectedReceipt.Body.GetProperty("deliveryFinal").GetBoolean(),"Pre-submit result did not survive restart");
            var duplicate=await Rpc("send","q",noSubmit);
            Check(duplicate.Status==409&&native.Submits==beforeSubmits,"Rejected request was replayed");
            await Rpc("draft-load");
            Check(JsonSerializer.SerializeToElement(workspace!.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Edited after rejection"&&native.DesktopText=="q","Receipt erased device or desktop draft");
            native.DesktopText="";native.OnType=null;
            Check((await Rpc("send","Fresh send")).Status==202,"Terminal pre-submit receipt still blocked a fresh send");
            native.HasSessionApi=false;beforeTypes=native.Types;beforeSubmits=native.Submits;
            await Rpc("read");
            Check(!JsonSerializer.SerializeToElement(workspace.Snapshot()).GetProperty("controls").GetProperty(task).GetProperty("canSend").GetBoolean(),"Native input capability remained enabled after input became unavailable");
            await Rpc("draft-save","Kept without a session API");
            var unavailable=await Rpc("send","Kept without a session API");
            Check(unavailable.Status==409&&unavailable.Body.GetProperty("error").GetString()=="native_session_api_unavailable"&&native.Types==beforeTypes&&native.Submits==beforeSubmits,"Unconnected session attempted desktop input");
            await Rpc("draft-load");
            Check(JsonSerializer.SerializeToElement(workspace.Snapshot(device:"native-device-a")).GetProperty("draftView").GetProperty("text").GetString()=="Kept without a session API","Missing API discarded the device draft");
            native.DirectTerminalInput=true;
            Check((await Rpc("send","Direct foreground input")).Status==202&&native.Submits==beforeSubmits+1,"Addressable foreground session did not receive its turn");
            native.DirectTerminalInput=false;beforeSubmits=native.Submits;
            await Rpc("draft-save","Kept without a session API");
            var archive=workspace.Journal("native-device-a");
            string original="Append text from the former host";
            var baseline=ClaudeNativeReceipt.Capture(ClaudeHistory.Read(root,session,secret),session,native.Epoch,original);
            string id=Guid.NewGuid().ToString(),otherId=Guid.NewGuid().ToString();
            var record=new Tab5SendRecord(id,task,Tab5CodexJournal.Fingerprint(task,original,"send",""),"send","","dispatching","",0,
                Device:"native-device-a",ResolutionSource:"session_api_pending",NativeReceipt:baseline,NativeScope:native.Epoch);
            archive.Save(record);
            archive.Save(record with{Id=otherId});
            File.AppendAllText(path,JsonSerializer.Serialize(new{type="user",uuid=id,parentUuid=native.LastUser,sessionId=session,
                origin=new{kind="human"},message=new{role="user",content=original}})+"\n");
            connected=false;
            await Rpc("read");
            Check(JsonSerializer.SerializeToElement(workspace.Snapshot()).GetProperty("controls").GetProperty(task).ValueKind==JsonValueKind.Null,"Disconnected session inherited its previous positive control");
            Check(JsonSerializer.SerializeToElement(workspace.Snapshot()).GetProperty("tasks").EnumerateArray().Single(t=>t.GetProperty("id").GetString()==task).GetProperty("source").GetProperty("id").GetString()=="claude.vscode","Disconnect erased verified source metadata");
            Check((await Rpc("receipt",request:otherId)).Status==504,"An equal prompt with a different UUID settled an old request");
            Check((await Rpc("receipt",request:id)).Status==202,"Original native row could not settle its request after host exit");
            archive.Save(record with{Id=otherId,State="rejected",Status=409,Error="tracking_ended_delivery_unknown",ResolutionSource="test_tracking_closed_unknown"});
            var closed=await Rpc("receipt",request:otherId);
            Check(closed.Status==409&&closed.Body.GetProperty("deliveryFinal").GetBoolean()&&
                archive.Draft(task)=="Kept without a session API"&&native.Submits==beforeSubmits,"Closing tracking lost a draft or reported delivery");
            Check(cliLookups==0,"Native reads or sends fell back to a CLI owner");
            Console.WriteLine($"CLAUDE_NATIVE_WORKSPACE_SELF_TEST_OK {checks} native dispatch, sealed baseline, exact real-user alias, draft/device/restart isolation; in-process native fixture only");
        }finally{
            workspace?.Dispose();
            string prefix=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;
            if(!root.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0||File.ReadAllText(Path.Combine(root,".owned-fixture"))!=marker)throw new IOException("Owned native fixture cleanup path changed");
            Directory.Delete(root,true);
        }
    }
}
