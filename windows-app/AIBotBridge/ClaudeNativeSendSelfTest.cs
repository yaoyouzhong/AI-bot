using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal static class ClaudeNativeSendSelfTest
{
    private sealed class Fixture
    {
        internal readonly Guid Session=Guid.NewGuid(),User=Guid.NewGuid();
        internal string Scope="host-a",Text="";internal bool Idle=true,Allowed=true,Accepted=true,Limited;
        internal bool ChangeAfterType,ThrowSubmit,ThrowType,ClearAfterSubmit,RevokeAfterType,ProviderGone;internal int Types,Submits,Dispatches,Reads;
        internal string? UserEdit,ActualScope;internal string SubmittedText="";internal Action? BeforeSecondRead;
        internal bool Current=true,ChangeTurnAfterType,PhotosConfirmed=true;
        internal ClaudeNativeSend.View Read(){if(++Reads==2)BeforeSecondRead?.Invoke();return new(Session,Scope,"composer-a",Text,Text.Length==0,Idle,Text.Length>0&&PhotosConfirmed);}
        internal ClaudeHistory.Value History(){
            var known=new HashSet<string>();var hashes=new Dictionary<string,string>();
            if(Submits>0&&Accepted){known.Add(User.ToString());hashes[User.ToString()]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SubmittedText)));}
            return new([],Limited,known,hashes);
        }
        internal ClaudeNativeSend.Calls Calls()=>new(Read,()=>ActualScope??Scope,value=>{
            Types++;Text=value;if(UserEdit is not null)Text=UserEdit;if(ChangeAfterType)Scope="host-b";if(RevokeAfterType)Allowed=false;
            if(ChangeTurnAfterType)Current=false;
            if(ThrowType)throw new IOException("Partial input possible");
        },()=>{Submits++;SubmittedText=Text;if(ClearAfterSubmit)Text="";if(ProviderGone)throw new System.Windows.Automation.ElementNotAvailableException();if(ThrowSubmit)throw new IOException("Provider lost after invocation");},History,
            ()=>Allowed,()=>Dispatches++,_=>Task.CompletedTask,Current:()=>Current);
    }
    internal static async Task RunAsync()
    {
        const string text="原生用户输入🙂";int count=0;
        void Require(bool ok,string reason){count++;if(!ok)throw new InvalidOperationException(reason);}
        Require(ClaudeNativeCode.AttachmentName("blue-probe.png 96×96 Remove attachment")=="blue-probe.png","Actual attachment pill lost its filename");
        Require(ClaudeNativeCode.AttachmentName("设计 图片 96×96.png 1280×720 Remove attachment")=="设计 图片 96×96.png","Attachment filename spaces were truncated");
        Require(ClaudeNativeCode.AttachmentName("ctrl esc to focus or unfocus Claude") is null,"Composer hint was accepted as attachment filename");
        Require(ClaudeNativeCode.AttachmentName("test.png 0×96 Remove attachment") is null&&
            ClaudeNativeCode.AttachmentName("test.png 96×96") is null,"Incomplete attachment metadata accepted");
        async Task<ClaudeNativeSend.Result> Run(Fixture f,CancellationToken token=default)=>await ClaudeNativeSend.SendAsync(f.Calls(),text,token);
        var success=new Fixture();var result=await Run(success);
        Require(result.Delivered&&result.User==success.User&&success.Types==1&&success.Submits==1&&success.Dispatches==1,"Native single dispatch/receipt failed");
        var cleared=new Fixture{ClearAfterSubmit=true};result=await Run(cleared);
        Require(result.Delivered&&cleared.Text.Length==0&&cleared.Submits==1,"Native composer clearing replaced receipt observation");
        var existing=new Fixture{Text="PC user draft"};result=await Run(existing);
        Require(!result.StagingAttempted&&existing.Types==0&&existing.Submits==0&&existing.Text=="PC user draft","Existing desktop draft was changed");
        var denied=new Fixture{Allowed=false};result=await Run(denied);
        Require(!result.StagingAttempted&&denied.Reads==0&&denied.Dispatches==0,"Unauthorized request reached native state");
        var changed=new Fixture();changed.BeforeSecondRead=()=>changed.Text="New PC draft";result=await Run(changed);
        Require(!result.StagingAttempted&&changed.Text=="New PC draft"&&changed.Types==0,"Intervening PC draft was overwritten");
        var stale=new Fixture();stale.BeforeSecondRead=()=>stale.ActualScope="host-b";result=await Run(stale);
        Require(!result.StagingAttempted&&stale.Types==0,"A stale UI identity bypassed the independent native host check");
        var scope=new Fixture{ChangeAfterType=true};result=await Run(scope);
        Require(!result.Delivered&&scope.Types==1&&scope.Submits==0,"Replaced original host was submitted");
        var edit=new Fixture{UserEdit="Later input"};result=await Run(edit);
        Require(!result.Delivered&&edit.Submits==0&&edit.Types==1&&edit.Text=="Later input","Later desktop input was discarded");
        var revoked=new Fixture{RevokeAfterType=true};result=await Run(revoked);
        Require(!result.Delivered&&revoked.Types==1&&revoked.Submits==0,"Revoked authorization reached native submit");
        var lost=new Fixture{Accepted=false};result=await Run(lost);
        Require(result.SubmitAttempted&&!result.Delivered&&lost.Submits==1&&lost.Types==1,"Unknown delivery was retried or marked successful");
        var threw=new Fixture{ThrowSubmit=true};result=await Run(threw);
        Require(result.Delivered&&threw.Submits==1&&threw.Types==1,"Provider failure retried a native submission or lost its actual receipt");
        var providerGone=new Fixture{ProviderGone=true};result=await Run(providerGone);
        Require(result.Delivered&&providerGone.Submits==1&&providerGone.Types==1,"An invalidated UI provider lost an actual receipt or caused a duplicate native action");
        var partial=new Fixture{ThrowType=true};result=await Run(partial);
        Require(result.StagingAttempted&&!result.SubmitAttempted&&partial.Types==1&&partial.Submits==0,"Partial typing was retried or submitted");
        var limited=new Fixture{Limited=true};result=await Run(limited);
        Require(!result.StagingAttempted&&limited.Types==0&&limited.Dispatches==0,"Missing history baseline allowed native input");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();var stopped=new Fixture();result=await Run(stopped,cancelled.Token);
        Require(!result.StagingAttempted&&stopped.Types==0&&stopped.Submits==0,"Cancelled request modified the native composer");
        var changedTurn=new Fixture{ChangeTurnAfterType=true};result=await Run(changedTurn);
        Require(result.StagingAttempted&&!result.SubmitAttempted&&changedTurn.Types==1&&changedTurn.Submits==0,"An append raced into a different original turn");
        var missingPhotos=new Fixture{PhotosConfirmed=false};result=await Run(missingPhotos);
        Require(result.StagingAttempted&&!result.SubmitAttempted&&missingPhotos.Types==1&&missingPhotos.Submits==0,"Unconfirmed photo staging reached native submission");
        ClaudeNativeTerminal.View Parse(string prompt,int caret=2,string previous="────────",string below="────────"){
            string[] rows=["> previous transcript input",previous,prompt.PadRight(48),below];
            return ClaudeNativeConsole.ParseRows(y=>rows[y],(_,_)=>true,0,3,2,caret,"fixture");
        }
        Require(Parse("> ").Empty&&Parse("❯\u00a0").Empty,"Cleared console was confused with an earlier transcript prompt");
        Require(!Parse(">   ",4).Empty,"Moved whitespace caret was mistaken for an empty composer");
        Require(!Parse("> Try an actual draft").Empty&&Parse("> Try an actual draft").DimSuggestion,"Dim hint-like words were silently discarded");
        Require(Parse("> ",previous:new string('─',48)+" agents ─").Empty&&
            Parse("> ordinary task",15,previous:new string('─',48)+" agents ─").Text=="ordinary task",
            "Foreground status embedded in the upper border hid the native prompt");
        Require(!Parse("> [Image #1]").Empty,"Image-only draft was mistaken for an empty composer");
        string[] wrapped=["────────","> @path","  查看照片","────────"];
        var wrap=ClaudeNativeConsole.ParseRows(y=>wrapped[y],(_,_)=>false,0,3,2,10,"test");
        Require(wrap.HasHiddenSpace("@path 查看照片")&&!wrap.HasHiddenSpace("@path  查看照片")&&!wrap.HasHiddenSpace("@path 查看别的"),"A wrapped file reference must match every visible character");
        var coloredHint=ClaudeNativeConsole.ParseRows(y=>y switch{0=>"────────",1=>"> Try a task",_=>"────────"},(_,_)=>false,0,2,1,2,"test");
        Require(!coloredHint.Empty&&coloredHint.SuggestionCandidate&&!coloredHint.DimSuggestion,"A hint's color must not imply an empty composer");
        Require(!wrap.HasHiddenSpace("@path 查\u0301看照片")&&!((wrap with{WrappedText="@path\n查看\n照片"}).HasHiddenSpace("@path 查看 照片")),"Ambiguous glyphs or multiple wraps bypassed exact readback");
        bool Rejects(Func<ClaudeNativeTerminal.View> f){try{_=f();return false;}catch(IOException){return true;}}
        Require(Rejects(()=>Parse("> ",previous:"unrelated output"))&&Rejects(()=>Parse("> ",below:"  more draft")),"Unbounded or multiline prompt was treated as empty");
        var before=Parse("> ");var chip=Parse("> [Image #1]",12);
        long elapsed=0;
        ClaudeNativeTerminal.View AfterDelay(){
            if(elapsed<1000)return before;
            if(elapsed<3000)throw new IOException("Native composer boundary unavailable");
            return chip;
        }
        ClaudeNativeTerminal.View Observe(Func<ClaudeNativeTerminal.View> read,Action? check=null)=>
            ClaudeNativeConsole.WaitForPaste(before,read,check??(()=>{}),()=>elapsed,ms=>elapsed+=ms);
        Require(Observe(AfterDelay)==chip&&elapsed>=3000,"Slow image reader or temporary redraw released the clipboard early");
        elapsed=0;Require(Rejects(()=>Observe(()=>before))&&elapsed==20000,"Missing image was accepted or wait was unbounded");
        elapsed=0;Require(Rejects(()=>Observe(AfterDelay,()=>{if(elapsed>=600)throw new IOException("Clipboard ownership changed");}))&&elapsed<1000,"Changed clipboard continued to wait or was accepted");
        Require(Rejects(()=>Observe(()=>chip with{Text="later draft"}))&&Rejects(()=>Observe(()=>chip with{Target="another console"})),"Changed target or draft accepted as photo acknowledgement");
        var photoFailure=new Fixture();var failing=photoFailure.Calls() with{Type=_=>throw new ClaudeNativeTerminal.ConsoleFailure("native_console_clipboard_format_unconfirmed")};
        result=await ClaudeNativeSend.SendAsync(failing,text,default);
        Require(result.FailureStage=="native_console_clipboard_format_unconfirmed"&&result.StagingAttempted&&!result.SubmitAttempted&&photoFailure.Submits==0,"Paste stage was lost or submitted");
        Console.WriteLine($"CLAUDE_NATIVE_SEND_SELF_TEST_OK {count} draft/identity/authorization/single-dispatch/unknown-delivery guards; no native client contacted");
    }
}
