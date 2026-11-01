using static AIBotBridge.Tab5CodexComposer;

namespace AIBotBridge;

internal static class Tab5NativeAppendSelfTest
{
    internal static async Task RunAsync() {
        int checks=0;
        void Require(bool ok,string message){checks++;if(!ok)throw new InvalidOperationException("Native append: "+message);}
        LayoutNode Node(string kind,string name="",string classes="",params LayoutNode[] children)=>new(kind,name,classes,children);
        var editor=Node("Edit",classes:"ProseMirror");
        LayoutNode Layout(params LayoutNode[] siblings)=>Node("Group",children:[Node("Group",classes:"_ComposerLayoutBody_fixture",children:[editor]),..siblings]);
        Require(AttachmentFree(Layout(Node("Button","Model",children:[Node("Image","icon")]))),"decorative toolbar icon allowed");
        Require(!AttachmentFree(Layout(Node("Image","attachment"))),"real image blocked");
        Require(!AttachmentFree(Layout(Node("Button","file",children:[Node("Group",children:[Node("Button","Remove file")])]))),"nested removal action inspected");
        Require(!AttachmentFree(Layout(Node("ComboBox","file",children:[Node("Button","open")]))),"nested controls not exempt");
        Require(!AttachmentFree(Layout(Node("Hyperlink","quote"))),"reference blocked");
        Require(!AttachmentFree(Layout(Node("Group",classes:"_ComposerLayoutReplyContext_fixture",children:[Node("Text","quote")]))),"outside-body quote blocked");
        Require(!AttachmentFree(Layout(Node("Group",classes:"_ComposerLayoutAttachments_fixture",children:[Node("Text","file")]))),"outside-body attachment blocked");
        Require(!AttachmentFree(Layout(editor)),"second editor blocked");
        Require(AttachmentFree(Layout(Node("Group",classes:"_ComposerLayoutAttachments_fixture",children:[Node("Group")]))),"empty attachment wrappers allowed");
        Require(AttachmentFree(Layout(Node("Text","Please remove the example"))),"message text is not a removal action");
        Require(!AttachmentFree(Layout(Node("Group",classes:"@container/request-card"))),"question card blocked");
        Require(!AttachmentFree(Layout(Enumerable.Range(0,301).Select(_=>Node("Group")).ToArray())),"bounded inspection fails closed");
        int writes=0;var draft=new DraftSnapshot("main:fixture","user draft");
        var calls=new StageCalls(()=>draft,(before,text)=>{writes++;if(draft!=before)return false;draft=draft with {Text=text};return true;},_=>Task.CompletedTask);
        var blocked=await AppendAsync(calls,"new message",CancellationToken.None,true);
        Require(!blocked.Attempted&&blocked.Error=="desktop_draft_present"&&writes==0&&draft.Text=="user draft","preserve existing desktop draft");
        draft=draft with {Text=""};
        var staged=await AppendAsync(calls,"first\nsecond!",CancellationToken.None,true);
        Require(staged.Staged&&writes==1&&draft.Text=="first\nsecond!","one write exact text");
        int invokes=0;
        var unknown=await InvokeOnceAsync(new(()=>true,()=>{invokes++;throw new InvalidOperationException();},_=>Task.FromResult(false)),CancellationToken.None);
        Require(unknown.Attempted&&!unknown.Submitted&&invokes==1,"uncertain native action never retried");
        long now=0;var cache=new Tab5NativeReceiptCache(()=>now);
        var baseline=new Tab5CodexDeliveryReceipt("task","owner","scope","hash","turn",1,[],[],new([],[]),new([],[]));
        var complete=baseline with {TextHash=Tab5CodexDeliveryReceipt.Digest("draft")};
        Require(HasBaseline(complete,"task","draft"),"complete baseline bound to exact task and text");
        Require(!HasBaseline(null,"task","draft")&&!HasBaseline(complete with {Server=null},"task","draft")&&
            !HasBaseline(complete with {Local=null},"task","draft"),"timeout or incomplete baseline refuses native submit");
        Require(!HasBaseline(complete,"other","draft")&&!HasBaseline(complete,"task","changed"),"cannot reuse another draft baseline");
        int unconfirmedClicks=0;
        var refused=await InvokeOnceAsync(new(()=>HasBaseline(complete with {Server=null},"task","draft"),
            ()=>unconfirmedClicks++,_=>Task.FromResult(true)),default);
        Require(!refused.Attempted&&!refused.Submitted&&unconfirmedClicks==0,"missing baseline never crosses the native send boundary");
        cache.Put("incomplete",baseline with {Local=null});Require(cache.Count==0,"partial baseline not retained");
        for(int i=0;i<65;i++){now=i;cache.Put(i.ToString(),baseline);}
        Require(cache.Count==64&&cache.Get("0") is null&&cache.Get("64") is not null,"bounded 64 baselines");
        now=60064;Require(cache.Count==0,"expire after 60 seconds");
        cache.Put("pending",baseline);Require(new Tab5NativeReceiptCache().Get("pending") is null,"restart cannot recreate evidence");
        var migrated=baseline with {Local=new(["old"],[]),Server=new(["old"],[])};
        Require((migrated with {Accepted=[new(["old"])]}).Confirm(migrated) is null,"old queue migration is not new delivery");
        Require((migrated with {Server=new(["new"],[new(["new"])])}).Confirm(migrated)=="message_saved_in_queue","durable queue accepted before execution");
        Require((migrated with {Owner="other",Accepted=[new(["new"])]}).Confirm(migrated) is null,"owner change rejected");
        int serverReads=0,receiptReads=0,waits=0;
        Task<Tab5CodexDeliveryReceipt.Queue?> Server(CancellationToken _) {
            serverReads++;return Task.FromResult<Tab5CodexDeliveryReceipt.Queue?>(new(["server-new"],[new(["server-new"])]));
        }
        var accepted=baseline with {Accepted=[new(["accepted-new"])],Local=null,Server=null};
        Require((await Tab5CodexDeliveryReceipt.CompleteAfterAsync(accepted,baseline,Server,default))?.Confirm(baseline)=="message_accepted"&&serverReads==0,
            "accepted message needs no post-action server reader or UI-clear proof");
        var locallyQueued=baseline with {Local=new(["local-new"],[new(["local-new"])]),Server=null};
        Require((await Tab5CodexDeliveryReceipt.CompleteAfterAsync(locallyQueued,baseline,Server,default))?.Confirm(baseline)=="message_saved_in_queue"&&serverReads==0,
            "durable local queue succeeds before execution without a server round trip");
        Require((await Tab5CodexDeliveryReceipt.CompleteAfterAsync(baseline,baseline,Server,default))?.Confirm(baseline)=="message_saved_in_queue"&&serverReads==1,
            "no local proof still queries the server queue");
        Require(await Tab5CodexDeliveryReceipt.CompleteAfterAsync(accepted,baseline with {Server=null},Server,default) is null&&serverReads==1,
            "fast read never bypasses complete pre-action baseline");
        Require((await Tab5CodexDeliveryReceipt.CompleteAfterAsync(accepted with {Owner="other"},baseline,Server,default))?.Confirm(baseline) is null,
            "server evidence cannot override another owner");
        Task Wait(CancellationToken _){waits++;return Task.CompletedTask;}
        var delayed=await Tab5CodexDeliveryReceipt.WaitForConfirmationAsync(baseline,
            _=>Task.FromResult<Tab5CodexDeliveryReceipt?>(++receiptReads==1?baseline:accepted),default,Wait);
        Require(delayed?.Confirm(baseline)=="message_accepted"&&receiptReads==2&&waits==1,
            "lagging published receipt is queried again within the same send response");
        receiptReads=waits=0;
        Require(await Tab5CodexDeliveryReceipt.WaitForConfirmationAsync(baseline,_=>{receiptReads++;return Task.FromResult<Tab5CodexDeliveryReceipt?>(baseline);},default,Wait) is null&&receiptReads==4&&waits==3,
            "persistent unknown remains bounded and never becomes success");
        receiptReads=waits=0;
        using(var cancelled=new CancellationTokenSource()) {
            cancelled.Cancel();
            Require(await Tab5CodexDeliveryReceipt.WaitForConfirmationAsync(baseline,_=>{receiptReads++;return Task.FromResult<Tab5CodexDeliveryReceipt?>(accepted);},cancelled.Token,Wait) is null&&receiptReads==0,
                "cancelled receipt observation does not restart reads");
        }
        receiptReads=0;int delayedInvokes=0;
        var delayedSubmit=await InvokeOnceAsync(new(()=>true,()=>delayedInvokes++,async ct=>
            await Tab5CodexDeliveryReceipt.WaitForConfirmationAsync(baseline,_=>Task.FromResult<Tab5CodexDeliveryReceipt?>(++receiptReads<3?null:locallyQueued),ct,Wait) is not null),default);
        Require(delayedSubmit.Submitted&&delayedInvokes==1&&receiptReads==3,"multiple receipt observations never repeat the native action");
        string folder=Path.Combine(Path.GetTempPath(),"tab5-diag-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            string path=Path.Combine(folder,"test.log");File.WriteAllText(path,new string('0',Tab5LocalDiagnostics.MaximumBytes));
            Tab5LocalDiagnostics.Write("fixed=1 elapsed=12",path);
            Require(new FileInfo(path).Length<256&&new FileInfo(path+".previous").Length==Tab5LocalDiagnostics.MaximumBytes,"bounded diagnostic rotation");
            long length=new FileInfo(path).Length;Tab5LocalDiagnostics.Write("invalid\nline",path);
            Require(new FileInfo(path).Length==length,"reject multiline diagnostic data");
        }finally{Directory.Delete(folder,true);}
        Console.WriteLine($"Native append self-test passed ({checks} checks).");
    }
}
