using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5WindowsFollowupSelfTest
{
    private static int _checks;
    private static void Check(bool value,string label){_checks++;if(!value)throw new InvalidOperationException("W29: "+label);}
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
    internal static async Task RunAsync() {
        _checks=0;
        foreach(string scenario in new[]{"already-end","move","changed-before","changed-on-focus","changed-after-select"}) {
            int focus=0,select=0;bool ready=scenario!="changed-before",atEnd=scenario=="already-end";
            bool result=Tab5CodexComposer.PositionCaretAtEnd(new(()=>ready,()=>atEnd,()=> {
                focus++;if(scenario=="changed-on-focus")ready=false;return true;
            },()=>{select++;atEnd=true;if(scenario=="changed-after-select")ready=false;}));
            Check(result==(scenario is "already-end" or "move"),"caret verification: "+scenario);
            Check(focus==(scenario is "already-end" or "changed-before"?0:1)&&select==(scenario is "move" or "changed-after-select"?1:0),
                "caret does not refocus an existing end or select a changed target: "+scenario);
        }
        foreach(string action in new[]{"Next","Send","Send update","Submit","下一步","发送","发送更新","提交"})
            Check(Tab5CodexComposer.QuestionInfo(["Question","Choose a destination"],["Previous question",action],1)?.Action==action,"question action "+action);
        var empty=Tab5CodexComposer.QuestionInfo(["问题","选哪个目录？"],["跳过"],1);
        Check(empty is {Action:""},"empty immediate-response question can be bound before it exposes Submit");
        Check(empty?.Text==Tab5CodexComposer.QuestionInfo(["问题","选哪个目录？"],["提交"],1)?.Text,"typing cannot change the question identity");
        Check(Tab5CodexComposer.QuestionInfo(["问题","第一题"],["下一步","提交"],1) is null,"ambiguous action rejected");
        Check(Tab5CodexComposer.QuestionInfo(["问题","第一题"],["批准"],1) is null,"approval is never a question action");
        Check(Tab5CodexComposer.QuestionInfo(["问题","第一题"],["提交"],2) is null,"two answer fields rejected");
        Check(Tab5CodexComposer.QuestionInfo(["Title","body"],["提交"],1) is null,"unrecognized cards rejected");
        Check(!Tab5CodexComposer.IsSubmitAction("Stop")&&!Tab5CodexComposer.IsSubmitAction("停止")&&!Tab5CodexComposer.IsQuestionAction("Skip"),"cannot stop, approve or skip");
        Check(Tab5CodexComposer.QuestionActionAcknowledged("Send","Send update")&&Tab5CodexComposer.QuestionActionAcknowledged("发送","发送更新"),"question send-to-update transition acknowledges the native action");
        Check(!Tab5CodexComposer.QuestionActionAcknowledged("Skip","Submit")&&!Tab5CodexComposer.QuestionActionAcknowledged("Next","Next"),"typing and unchanged action do not acknowledge submission");
        var clearBefore=new Tab5CodexComposer.DraftSnapshot("question:first","answer");int clearReads=0;
        foreach(string reason in new[]{"staged_target_or_text_changed","composer_changed"}) {
            var rejected=new Tab5CodexComposer.SubmitResult(false,false,reason);
            Check(Tab5QuickConsole.ContextualSubmitResult(rejected,clearBefore).Error=="question_target_or_text_changed","question rejection uses staged question context");
            Check(Tab5QuickConsole.ContextualSubmitResult(rejected,new("main:editor","draft")).Error=="conversation_target_or_text_changed","conversation rejection does not claim question changed");
            Check(Tab5QuickConsole.ContextualSubmitResult(rejected with{Attempted=true},clearBefore).Error==reason,"attempted action cannot become definite no-send by wording");
        }
        foreach(string identity in new[]{"question:first","main:editor-one"}) {
            var staged=new Tab5CodexComposer.DraftSnapshot(identity,"录音原文");
            var edited=staged with {Text="电脑校对，第一行。\r\n第二行！"};
            Check(Tab5CodexComposer.SubmissionSnapshot(staged,edited,true)==edited,"explicit direct click adopts exact desktop edits including newlines");
            Check(Tab5CodexComposer.SubmissionSnapshot(staged,edited,false) is null,"task-page strict staged text unchanged");
            Check(Tab5CodexComposer.SubmissionSnapshot(staged,edited with{Identity="different-target"},true) is null,"desktop edits never cross question or editor identity");
            Check(Tab5CodexComposer.SubmissionSnapshot(staged,null,true) is null,"unreadable target cannot send");
            Check(Tab5CodexComposer.SubmissionSnapshot(staged,edited with{Text=" \r\n"},true) is null,"cleared desktop draft cannot send stale recording");
            var frozen=Tab5CodexComposer.SubmissionSnapshot(staged,edited,true);int clicks=0;
            var latest=edited with{Text="changed during preparation"};
            var raced=await Tab5CodexComposer.InvokeOnceAsync(new(()=>latest==frozen,()=>clicks++,_=>Task.FromResult(true)),default);
            Check(!raced.Attempted&&clicks==0,"editing again after snapshot prevents native click");
        }
        var wrongClear=await Tab5CodexComposer.ClearDraftAsync(new(()=>++clearReads==1?clearBefore:new("question:next",""),_=>true,_=>Task.CompletedTask),default);
        Check(!wrongClear.Cleared&&wrongClear.Attempted,"next question's empty input cannot confirm clearing the previous answer");
        Check(Tab5CodexComposer.AppendText("第一段，\r\n保留标点。","第二段！\n末尾？")=="第一段，\r\n保留标点。 第二段！\n末尾？","two takes preserve original newlines and punctuation");
        int invokes=0;bool received=false;
        var native=new Tab5CodexComposer.NativeCalls(()=>true,()=>{invokes++;received=true;},_=>Task.FromResult(received));
        Check((await Tab5CodexComposer.InvokeOnceAsync(native with{Ready=()=>false},default)).Attempted==false&&invokes==0,"changed target prevents native action");
        Check((await Tab5CodexComposer.InvokeOnceAsync(native,default)).Submitted&&invokes==1,"native action delivers once regardless of Enter preference");
        Check((await Tab5CodexComposer.InvokeOnceAsync(native with{Invoke=()=>{invokes++;received=true;throw new InvalidOperationException();}},default)).Submitted&&invokes==2,"provider exception reconciled by observation only");
        Check(!(await Tab5CodexComposer.InvokeOnceAsync(native with{Confirm=_=>Task.FromResult(false)},default)).Submitted&&invokes==3,"unknown result does not invoke another action");
        Check((await Tab5CodexComposer.InvokeOnceAsync(native with{Confirm=_=>throw new OperationCanceledException()},default)).Attempted&&invokes==4,"cancellation after action stays consumed");
        ReceiptChecks();
        await ControllerChecks();
        JournalBoundaryChecks();
        await FailureChecks();
        Console.WriteLine($"WINDOWS_W29_SELF_TEST_OK checks={_checks} question_binding exact_text device_scope native_once queue_aliases encrypted_restart");
    }
    private static void ReceiptChecks() {
        string task=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString(),turn=Guid.NewGuid().ToString();
        const string expected="第一段。\n第二段！";
        object Message(string id,string type="userMessage",string status="accepted",string? text=null,string? client=null,bool attachment=false)=>new {
            id,type,status,clientUserMessageId=client,content=attachment?new object[]{new{type="text",text=text??expected},new{type="localImage",path="fixture"}}:new object[]{new{type="text",text=text??expected}},
            input=new[]{new{type="text",text=text??expected}}};
        Tab5CodexDeliveryReceipt? State(object[] items,string? t=null,string? own=null,long time=1)=>Tab5CodexDeliveryReceipt.Parse(Json(new {
            id=task,turns=new[]{new{turnId=t??turn,turnStartedAtMs=time,items}}}),task,own??owner,"scope",expected);
        var baseline=State([Message("old"),Message("pending","steeringUserMessage","pending")])! with {Local=new([],[]),Server=new([],[])};
        Check(baseline is not null,"valid owner state parsed");
        Check(State([Message("old")])!.Confirm(baseline!) is null,"historical same text is not new acceptance");
        Check(State([Message("pending","steeringUserMessage")])!.Confirm(baseline!) is null,"old pending becoming accepted is not a new send");
        Check(State([Message("new")])!.Confirm(baseline!)=="message_accepted","new exact plain text accepted");
        Check(State([Message("new",text:expected+"\n")])!.Confirm(baseline!)=="message_accepted","native receipt trailing paragraph LF accepted");
        Check(State([Message("new",text:expected+"\r\n\r\n")])!.Confirm(baseline!)=="message_accepted","native receipt trailing CRLF accepted");
        Check(State([Message("old",text:expected+"\n")])!.Confirm(baseline!) is null,"paragraph normalization cannot revive historical same text");
        foreach(string changed in new[]{" "+expected,expected+" ",expected.Replace("\n"," "),expected.Replace("！","。"),expected+"\u2028"})
            Check(State([Message("new",text:changed)])!.Confirm(baseline!) is null,"receipt still rejects spaces/internal breaks/punctuation/unproven separators");
        Check(Tab5CodexDeliveryReceipt.ParagraphText(" first\r\nsecond! \r\n")==" first\r\nsecond! ","only terminal CR/LF framing is removed");
        Check(State([Message("new",text:"不同文字")])!.Confirm(baseline!) is null,"different text rejected");
        Check(State([Message("new",attachment:true)])!.Confirm(baseline!) is null,"partial text with an attachment rejected");
        Check(State([Message("new")],own:Guid.NewGuid().ToString())!.Confirm(baseline!) is null,"another owner rejected");
        Check(State([Message("new")],t:Guid.NewGuid().ToString())!.Confirm(baseline!) is null,"tied turn clock cannot prove recency");
        Check(State([Message("new")])!.Confirm(baseline! with {Local=null}) is null,"missing local baseline prevents all fallbacks");
        Check(State([Message("new")])!.Confirm(baseline! with {Server=null}) is null,"missing server baseline prevents all fallbacks");
        object Local(string id,string status="pending",bool attachment=false,string host="local",string? alias=null,string? prompt=null)=>new {
            id,clientUserMessageId=alias,submission=new{hostId=host,status},context=new{prompt=prompt??expected,addedFiles=Array.Empty<object>(),
                fileAttachments=attachment?new object[]{new{name="fixture"}}:[],imageAttachments=Array.Empty<object>()}};
        Tab5CodexDeliveryReceipt.Queue? Queue(params object[] entries)=>Tab5CodexDeliveryReceipt.ParseLocal(Json(new Dictionary<string,object>{["queued-follow-ups"]=new Dictionary<string,object>{[task]=entries}}),task,expected);
        var queued=State([])! with{Local=Queue(Local("queued"))};
        Check(queued.Confirm(baseline!)=="message_saved_in_queue","pending local saved queue is success before execution");
        Check((queued with {Local=Queue(Local("new-local",prompt:expected+"\n"))}).Confirm(baseline!)=="message_saved_in_queue","local queue paragraph framing accepted");
        Check((queued with {Local=Queue(Local("new-local",prompt:expected+" "))}).Confirm(baseline!) is null,"local queue trailing space remains significant");
        var serverParagraph=Tab5CodexDeliveryReceipt.ParseServer(Json(new{data=new[]{new{id="new-server",clientUserMessageId="new-client",input=new[]{new{type="text",text=expected+"\n"}}}},nextCursor=(string?)null}),expected);
        Check((queued with {Local=null,Server=serverParagraph}).Confirm(baseline!)=="message_saved_in_queue","server queue paragraph framing accepted");
        Check((queued with {Local=Queue(Local("queued","failed"))}).Confirm(baseline!) is null,"failed queue not accepted");
        Check((queued with {Local=Queue(Local("queued",host:"remote"))}).Confirm(baseline!) is null,"another host queue not accepted");
        Check((queued with {Local=Queue(Local("queued",attachment:true))}).Confirm(baseline!) is null,"local attachment partial match rejected");
        var before=baseline! with{Local=Queue(Local("old-local",alias:"client"))};
        var migration=Tab5CodexDeliveryReceipt.ParseServer(Json(new{data=new[]{new{id="new-server-id",clientUserMessageId="client",input=new[]{new{type="text",text=expected}}}},nextCursor=(string?)null}),expected);
        Check((queued with{Local=null,Server=migration}).Confirm(before) is null,"local to server migration preserves old aliases");
        Check(State([Message("accepted-id",client:"client")])!.Confirm(before) is null,"local to accepted migration preserves old aliases");
        Check(Tab5CodexDeliveryReceipt.ParseServer(Json(new{data=Array.Empty<object>(),nextCursor="page2"}),expected) is null,"incomplete server pagination unavailable");
        Check(Tab5CodexDeliveryReceipt.ParseLocal(Json(new{}),task,expected) is null,"missing local schema is unknown not empty");
        Check(Queue(Local("dup"),Local("dup")) is null,"duplicate queue identities invalidate baseline");
        Check((queued with{TaskId=Guid.NewGuid().ToString()}).Confirm(baseline!) is null,"another task rejected");
        Check((queued with{Scope="changed"}).Confirm(baseline!) is null,"another account scope rejected");
        Check((queued with{TextHash="changed"}).Confirm(baseline!) is null,"another confirmed draft rejected");
    }
    private static async Task ControllerChecks() {
        string directory=Path.Combine(Path.GetTempPath(),"aibot-w29-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            string path=Path.Combine(directory,"receipts.dat"),task=Guid.NewGuid().ToString();
            var catalog=new[]{new Tab5CodexTask(task,"Fixture","Fixture",1)};
            var current=new Tab5CodexComposer.DraftSnapshot("question:first","");int writes=0,sends=0;string sentText="";
            var journal=new Tab5QuickConsoleJournal(path);
            Tab5QuickConsole Controller(Tab5QuickConsoleJournal storage)=>new(()=>catalog,capture:_=>current,journal:storage,
                stageBound:async (_,text,target,ct)=> {
                    if(target is not null&&target!=current.Identity)return new(false,false,"voice_target_changed_preview_retained");
                    return await Tab5CodexComposer.AppendAsync(new(()=>current,(before,value)=>{if(before!=current)return false;writes++;current=before with{Text=value};return true;},_=>Task.CompletedTask,_=>true),text,ct);
                },submit:(_,binding,_)=> {
                    if(Tab5CodexComposer.SubmissionSnapshot(binding,current,true) is null)return Task.FromResult(new Tab5CodexComposer.SubmitResult(false,false,"staged_target_or_text_changed"));
                    sentText=current.Text;sends++;return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,true,""));
                });
            var controller=Controller(journal);
            string RequestId()=>Guid.NewGuid().ToString();
            JsonElement Stage(string request,string text)=>Json(new{op="stage-draft",taskId=task,requestId=request,text});
            JsonElement Submit(string request,string draft)=>Json(new{op="submit-draft",taskId=task,requestId=request,draftRequestId=draft});
            Check((await controller.HandleAsync(Stage(RequestId(),"old pending transcript"),default,"device")).Status==409&&writes==0,"missing recording binding cannot stage into the current question after restart");
            string prepare=controller.PrepareVoice(task)!;
            var voice=new Tab5VoiceReply("take1",task,"tab5","recording","","",SourceName:"TAB5 内置麦克风");
            controller.ObserveVoice("device",voice,prepare);
            controller.ObserveVoice("device",voice with {State="review",Text="第一段！",CanInsert=true});
            current=new("question:second","");
            Check((await controller.HandleAsync(Stage(RequestId(),"第一段！"),default,"device")).Status==409&&writes==0,"old question voice preserves preview and cannot fall back to main/new question");
            current=new("question:first","");string draft1=RequestId();
            Check((await controller.HandleAsync(Stage(draft1,"第一段！"),default,"device")).Status==200&&current.Text=="第一段！","same question voice stages once");
            Check((await controller.HandleAsync(Stage(draft1,"第一段！"),default,"device")).Status==200&&writes==1,"lost stage reply not appended twice");
            Check((await controller.HandleAsync(Stage(RequestId(),"第一段！"),default,"device")).Status==409&&writes==1,"new request cannot replay the same recorded take");
            Check((await controller.HandleAsync(Submit(RequestId(),draft1),default,"other-device")).Status==409&&sends==0,"other device cannot consume staged text");
            current=current with{Text="用户编辑后的新草稿"};
            current=current with{Identity="question:second"};
            Check((await controller.HandleAsync(Submit(RequestId(),draft1),default,"device")).Status==409&&sends==0,"old ticket cannot submit another question's edited text");
            current=current with{Identity="question:first"};
            voice=voice with{VoiceId="take2"};controller.ObserveVoice("device",voice,controller.PrepareVoice(task));
            controller.ObserveVoice("device",voice with{State="review",Text="补充。\n换行！",CanInsert=true});
            string draft2=RequestId();await controller.HandleAsync(Stage(draft2,"补充。\n换行！"),default,"device");
            Check(current.Text=="用户编辑后的新草稿 补充。\n换行！","new explicit recording confirms the edited draft and preserves both segments");
            string send=RequestId();
            current=current with{Text=current.Text+"（电脑校对）"};
            Check((await controller.HandleAsync(Submit(send,draft2),default,"device")).Status==200&&sends==1,"latest exact target and confirmed text submitted");
            Check(sentText=="用户编辑后的新草稿 补充。\n换行！（电脑校对）"&&writes==2,"send preserves corrected desktop text without writing old recording back");
            var restarted=Controller(new(path));
            Check((await restarted.HandleAsync(Submit(send,draft2),default,"device")).Status==200&&sends==1,"restart returns saved successful receipt");
            Check((await restarted.HandleAsync(Submit(RequestId(),draft2),default,"device")).Status==409&&sends==1,"restart cannot reuse consumed draft under another request");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(task)&&!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("submitted"),"receipt file encrypted with Windows user protection");
            string unknown=RequestId(),unknownDraft=RequestId();journal.Save("device",unknown,"fingerprint",unknownDraft,409,new{error="submit_unconfirmed_check_desktop",attempted=true});
            var restored=new Tab5QuickConsoleJournal(path);
            Check(restored.Find("device",unknown)?.Status==409&&restored.Used("device",unknownDraft),"crash after reservation retains unknown receipt and consumption");
            Check(restored.Find("other-device",unknown) is null&&!restored.Used("other-device",unknownDraft),"persistent receipts bound to device");
            File.WriteAllBytes(path,[1,2,3]);bool failed=false;
            try{new Tab5QuickConsoleJournal(path).Find("device",send);}catch(IOException){failed=true;}
            Check(failed,"corrupt receipt storage fails closed");
        }finally {Directory.Delete(directory,true);}
    }
    private static void JournalBoundaryChecks() {
        string directory=Path.Combine(Path.GetTempPath(),"aibot-w29-journal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            long now=100000000;string path=Path.Combine(directory,"receipts.dat"),request=Guid.NewGuid().ToString(),draft=Guid.NewGuid().ToString();
            var journal=new Tab5QuickConsoleJournal(path,()=>now);
            journal.Save("device",request,"fingerprint",draft,409,new{attempted=true,submitted=false});
            byte[] before=File.ReadAllBytes(path);
            using(var locked=new FileStream(path+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None)) {
                bool failed=false;
                try{journal.Save("device",request,"fingerprint",draft,200,new{submitted=true});}catch(IOException){failed=true;}
                Check(failed&&journal.Find("device",request)?.Status==409&&before.SequenceEqual(File.ReadAllBytes(path)),"failed receipt update keeps durable unknown and existing bytes");
            }
            journal.Save("device",request,"fingerprint",draft,200,new{submitted=true});
            Check(new Tab5QuickConsoleJournal(path,()=>now).Find("device",request)?.Status==200,"receipt update recovers after temporary writer contention");
            now+=86399999;Check(journal.Used("device",draft),"receipt retained until the 24 hour boundary");
            now++;Check(journal.Find("device",request) is null&&!journal.Used("device",draft),"receipt expires exactly at 24 hours");
            var ids=new List<(string Request,string Draft)>();
            for(int i=0;i<512;i++) {
                var entry=(Guid.NewGuid().ToString(),Guid.NewGuid().ToString());ids.Add(entry);
                journal.Save("device",entry.Item1,"fingerprint",entry.Item2,409,new{attempted=true});
            }
            before=File.ReadAllBytes(path);bool full=false;
            try{journal.Save("device",Guid.NewGuid().ToString(),"fingerprint",Guid.NewGuid().ToString(),409,new{attempted=true});}catch(IOException){full=true;}
            Check(full&&before.SequenceEqual(File.ReadAllBytes(path))&&journal.Used("device",ids[0].Draft),"capacity rejects a new receipt without evicting unexpired consumed drafts");
            journal.Save("device",ids[0].Request,"fingerprint",ids[0].Draft,200,new{submitted=true});
            Check(new Tab5QuickConsoleJournal(path,()=>now).Find("device",ids[0].Request)?.Status==200,"completion of a reserved receipt still succeeds at capacity");
            now+=86400000;journal.Save("device",request,"fingerprint",draft,409,new{attempted=true});
            Check(journal.Find("device",request)?.Status==409&&!journal.Used("device",ids[0].Draft),"expired capacity is reclaimed without retaining old consumption");
            using(var oversized=new FileStream(Path.Combine(directory,"oversized.dat"),FileMode.Create)){oversized.SetLength(1024*1024+1);}
            bool rejected=false;try{new Tab5QuickConsoleJournal(Path.Combine(directory,"oversized.dat")).Used("device",draft);}catch(IOException){rejected=true;}
            Check(rejected,"oversized journal rejected before decrypting or parsing");
        }finally {Directory.Delete(directory,true);}
    }
    private static async Task FailureChecks() {
        string directory=Path.Combine(Path.GetTempPath(),"aibot-w29-faults-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            string task=Guid.NewGuid().ToString();var catalog=new[]{new Tab5CodexTask(task,"Fixture","Fixture",1)};
            JsonElement Stage(string request,string text="确认的文字")=>Json(new{op="stage-draft",taskId=task,requestId=request,text});
            JsonElement Submit(string request,string draft)=>Json(new{op="submit-draft",taskId=task,requestId=request,draftRequestId=draft});
            string Error((int Status,object Body) result)=>Tab5CodexDesktop.Text(Json(result.Body),"error");
            foreach(string fault in new[]{"unknown","provider-error","cancelled","receipt-write"}) {
                string path=Path.Combine(directory,fault+".dat"),draft=Guid.NewGuid().ToString(),request=Guid.NewGuid().ToString();int sends=0;
                FileStream? blocked=null;
                Tab5QuickConsole Controller()=>new(()=>catalog,journal:new(path),requireVoiceTarget:false,
                    stage:(_,text,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"",Binding:new("main:fixture",text))),
                    submit:(_,_,_)=> {
                        sends++;
                        if(fault=="provider-error")throw new InvalidOperationException("simulated provider failure after action");
                        if(fault=="cancelled")throw new OperationCanceledException();
                        if(fault=="receipt-write")blocked=new FileStream(path+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None);
                        return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,fault=="receipt-write","submit_unconfirmed_check_desktop"));
                    });
                var controller=Controller();
                Check((await controller.HandleAsync(Stage(draft),default,"device")).Status==200,"fault fixture staged: "+fault);
                try {await controller.HandleAsync(Submit(request,draft),default,"device");}
                catch(OperationCanceledException) when(fault=="cancelled"){}
                finally {blocked?.Dispose();}
                var replay=await controller.HandleAsync(Submit(request,draft),default,"device");
                Check(sends==1&&(fault=="receipt-write"?replay.Status==200:replay.Status==409),"same process retry never repeats native action: "+fault);
                var restarted=Controller();
                Check((await restarted.HandleAsync(Submit(request,draft),default,"device")).Status==409&&sends==1,"restart returns durable unknown without resending: "+fault);
                Check((await restarted.HandleAsync(Submit(Guid.NewGuid().ToString(),draft),default,"device")).Status==409&&sends==1,"changed request cannot reuse consumed draft after failure: "+fault);
                var conflict=await restarted.HandleAsync(Submit(request,Guid.NewGuid().ToString()),default,"device");
                Check(conflict.Status==409&&Error(conflict)=="request_conflict"&&sends==1,"persisted request rejects a changed draft: "+fault);
            }
            int actions=0;string lockedPath=Path.Combine(directory,"before-action.dat");
            var lockedController=new Tab5QuickConsole(()=>catalog,journal:new(lockedPath),requireVoiceTarget:false,
                stage:(_,text,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"",Binding:new("main:fixture",text))),
                submit:(_,_,_)=>{actions++;return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,true,""));});
            string lockedDraft=Guid.NewGuid().ToString();await lockedController.HandleAsync(Stage(lockedDraft),default,"device");
            using(var locked=new FileStream(lockedPath+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None)) {
                Check((await lockedController.HandleAsync(Submit(Guid.NewGuid().ToString(),lockedDraft),default,"device")).Status==503&&actions==0,"reservation persistence failure prevents all native input");
            }
            int writes=0;var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var concurrent=new Tab5QuickConsole(()=>catalog,journal:new(Path.Combine(directory,"concurrent.dat")),requireVoiceTarget:false,
                stage:async(_,text,ct)=>{writes++;started.SetResult();await release.Task.WaitAsync(ct);return new(true,true,"",Binding:new("main:fixture",text));});
            string stageId=Guid.NewGuid().ToString();var pending=concurrent.HandleAsync(Stage(stageId),default,"device");await started.Task;
            var busy=await concurrent.HandleAsync(Stage(stageId),default,"device");
            Check(busy.Status==409&&Error(busy)=="desktop_busy"&&writes==1,"concurrent replay does not start another write");
            release.SetResult();await pending;
            Check((await concurrent.HandleAsync(Stage(stageId),default,"device")).Status==200&&writes==1,"replay after busy returns completed stage without append");
            var changed=await concurrent.HandleAsync(Stage(stageId,"不同文字"),default,"device");
            Check(changed.Status==409&&Error(changed)=="request_conflict"&&writes==1,"same stage request cannot change confirmed text");
        }finally {Directory.Delete(directory,true);}
    }
}
