using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5QuickConsoleSelfTest
{
    private static void Check(bool value,string label){if(!value)throw new Exception("Quick console: "+label);}
    internal static async Task RunAsync() {
        string prefix=new('长',60);
        var longA=Tab5CodexCatalog.WithDisplayTitle(new("long-a",prefix+"甲","project",2));
        var longB=Tab5CodexCatalog.WithDisplayTitle(new("long-b",prefix+"乙","project",1));
        Check(longA.Title==longB.Title&&longA.Title.Length==60,"display stays bounded for identical long prefixes");
        Check(Tab5CodexComposer.UniqueTitle(longA.Id,[longA,longB])==prefix+"甲","identity retains full title despite identical display prefix");
        Check(Tab5CodexComposer.UniqueTitle(longB.Id,[longA,longB])==prefix+"乙","different suffix selects its own exact identity");
        Check(Tab5CodexComposer.UniqueTitle(longA.Id,[longA,longA with {Id="outside-list",ProjectId="other"}]) is null,"same full title outside selected project is rejected");
        Check(Tab5CodexComposer.UniqueTitle("missing",[longA]) is null,"unknown identity never picks another task");
        var emoji=Tab5CodexCatalog.WithDisplayTitle(new("emoji",new string('a',59)+"😀suffix","project",1));
        Check(emoji.Title.Length==59&&emoji.IdentityTitle.EndsWith("😀suffix"),"display truncation never splits a surrogate pair");
        var wire=JsonSerializer.SerializeToElement(longA);
        Check(!wire.TryGetProperty("IdentityTitle",out _)&&wire.GetProperty("Title").GetString()==prefix,"full identity remains host-only with unchanged device title");
        nint foreground=1;bool tapped=false;int taps=0,releases=0,activations=0;
        var focus=new Tab5DesktopActivation.Calls(window=>{activations++;if(tapped)foreground=window;return tapped;},
            ()=>foreground,()=>false,()=>{taps++;tapped=true;return 2;},()=>releases++);
        var activation=Tab5DesktopActivation.Raise(2,calls:focus);
        Check(activation.Foreground&&activation.Tapped&&taps==1&&activations==2,"one guarded Alt tap permits activation with actual readback");
        Check(Tab5DesktopActivation.Raise(2,calls:focus).Foreground&&taps==1&&activations==2,"already foreground avoids input");
        foreground=1;tapped=false;
        Check(!Tab5DesktopActivation.Raise(2,false,focus).Foreground&&taps==1,"input fallback occurs at most once per navigation");
        Check(Tab5DesktopActivation.Raise(2,calls:focus with {InputHeld=()=>true}).InputBlocked&&taps==1,"held keys or mouse prevent an Alt tap");
        Check(!Tab5DesktopActivation.Raise(2,calls:focus with {TapAlt=()=>0}).Foreground,"rejected input is not activation success");
        Check(Tab5DesktopActivation.Raise(2,calls:focus with {TapAlt=()=>1}).PartialTap&&releases==1,"partial Alt input releases the key without retrying navigation");
        Check(!Tab5DesktopActivation.Raise(0,calls:focus).Foreground,"missing window rejected");
        Check(Tab5DesktopActivation.Raise(2,calls:focus with {Foreground=()=>0}).InputBlocked&&taps==1,"inaccessible desktop prevents input");
        Check(Tab5CodexComposer.HasClass("ProseMirror ProseMirror-focused","ProseMirror"),"focused editor remains a composer");
        Check(!Tab5CodexComposer.HasClass("ProseMirror-trailingBreak","ProseMirror"),"decoration is not a composer");
        Check(Tab5CodexComposer.IsComposerBodyClass("relative _ComposerLayoutBody_gcdh7_2"),"main composer container identified");
        Check(!Tab5CodexComposer.IsComposerBodyClass("@container/request-card"),"question response is not the main composer");
        foreach(var example in new (string Before,string Take,string Expected)[]{
            ("","第一句","第一句"),("已有草稿","","已有草稿"),
            ("先检查连接","再看日志","先检查连接。再看日志"),
            ("先检查连接。","再看日志","先检查连接。再看日志"),
            ("先检查连接，","再看日志","先检查连接，再看日志"),
            ("连接正常吗？","再看日志","连接正常吗？再看日志"),
            ("检查结果：","连接正常","检查结果：连接正常"),
            ("他说“连接正常。”","继续检查","他说“连接正常。”继续检查"),
            ("检查“连接”","继续检查","检查“连接”。继续检查"),
            ("连接正常  ","继续检查","连接正常。  继续检查"),
            ("连接正常！  ","继续检查","连接正常！  继续检查"),
            ("手动分段\r\n  ","第二段","手动分段\r\n  第二段"),
            ("第一段","\n第二段","第一段\n第二段"),
            ("   ","第一句","   第一句"),
            ("完成😀","继续检查","完成😀。继续检查")
        })Check(Tab5CodexComposer.AppendText(example.Before,example.Take)==example.Expected,"dictation separator: "+example.Before);
        foreach(char punctuation in "。！？；：，、.!?;:,…—–")
            Check(Tab5CodexComposer.AppendText("前文"+punctuation,"后文")=="前文"+punctuation+"后文","existing punctuation is not duplicated");
        string draftValue="原来的草稿\n";int draftWrites=0;bool readable=true,changed=false;
        var appendCalls=new Tab5CodexComposer.StageCalls(
            ()=>readable?new("main",draftValue):null,
            (before,value)=>{if(changed){draftValue="用户刚刚修改的草稿";return false;}Check(before.Text==draftValue,"append compares original draft");draftWrites++;draftValue=value;return true;},
            _=>Task.CompletedTask);
        var append=await Tab5CodexComposer.AppendAsync(appendCalls,"补录中文 & # ?",default);
        Check(append.Staged&&append.Appended&&draftWrites==1&&draftValue=="原来的草稿\n补录中文 & # ?","append preserves old text and stages one new take");
        append=await Tab5CodexComposer.AppendAsync(appendCalls,"第二段补录",default);
        Check(append.Staged&&draftWrites==2&&draftValue.EndsWith("补录中文 & # ?第二段补录"),"sequential takes continue after existing punctuation without newline");
        changed=true;append=await Tab5CodexComposer.AppendAsync(appendCalls,"不能覆盖",default);
        Check(!append.Attempted&&!append.Staged&&draftWrites==2&&draftValue=="用户刚刚修改的草稿","concurrent editing prevents replacement");
        changed=false;
        append=await Tab5CodexComposer.AppendAsync(appendCalls with {Write=(before,value)=>{draftWrites++;draftValue=value;readable=false;return true;}},"回执丢失",default);
        Check(append.Attempted&&!append.Staged&&draftWrites==3,"lost append readback never writes twice");
        readable=true;draftValue="";
        append=await Tab5CodexComposer.AppendAsync(appendCalls,"第一段",default);
        Check(append.Staged&&!append.Appended&&draftValue=="第一段","empty composer receives no leading newline or append hint");
        int unavailableReads=0;draftValue="已有草稿";int writesBeforeException=draftWrites;
        var uncertainWrite=appendCalls with {
            Read=()=>{if(unavailableReads-->0)throw new InvalidOperationException("provider refreshed");return new("main-refreshed",draftValue);},
            Write=(before,value)=>{draftWrites++;draftValue=value;unavailableReads=2;throw new InvalidOperationException("provider invalidated after mutation");}
        };
        append=await Tab5CodexComposer.AppendAsync(uncertainWrite,"补录已写入",default);
        Check(append.Attempted&&append.Staged&&append.Appended&&draftWrites==writesBeforeException+1&&draftValue=="已有草稿。补录已写入",
            "write-side provider exception reconciles exact appended text after transient reads without repeating input");
        draftValue="仍是原草稿";writesBeforeException=draftWrites;
        append=await Tab5CodexComposer.AppendAsync(uncertainWrite with {
            Write=(before,value)=>{draftWrites++;throw new InvalidOperationException("provider failed before mutation");}
        },"没有写入",default);
        Check(append.Attempted&&!append.Staged&&append.Error=="draft_write_unconfirmed"&&draftWrites==writesBeforeException+1&&draftValue=="仍是原草稿",
            "write-side exception without exact text remains unconfirmed and never retries input");
        writesBeforeException=draftWrites;
        append=await Tab5CodexComposer.AppendAsync(uncertainWrite with {
            Write=(before,value)=>{draftWrites++;draftValue="用户修改后的草稿";throw new InvalidOperationException("provider changed during edit");}
        },"不能重复或覆盖",default);
        Check(!append.Staged&&draftWrites==writesBeforeException+1&&draftValue=="用户修改后的草稿",
            "write reconciliation never overwrites subsequent edits or treats different text as success");
        int clearWrites=0;
        int caretMoves=0;string caretText="";
        draftValue="已有草稿\n第二行😀";readable=true;
        var caretCalls=appendCalls with {MoveCaretToEnd=snapshot=>{
            Check(snapshot.Text==draftValue,"caret uses the confirmed merged draft");
            caretMoves++;caretText=snapshot.Text;return true;
        }};
        append=await Tab5CodexComposer.AppendAsync(caretCalls,"继续补充",default);
        Check(append.Staged&&append.CaretAtEnd==true&&caretMoves==1&&caretText=="已有草稿\n第二行😀。继续补充",
            "multiline and emoji append places caret after the complete merged draft once");
        draftValue="";
        append=await Tab5CodexComposer.AppendAsync(caretCalls,"首次输入",default);
        Check(append.Staged&&append.CaretAtEnd==true&&caretMoves==2&&caretText=="首次输入","first take also places caret at end");
        writesBeforeException=draftWrites;
        append=await Tab5CodexComposer.AppendAsync(caretCalls with {MoveCaretToEnd=_=>throw new InvalidOperationException("selection unavailable")},"已写入",default);
        Check(append.Staged&&append.CaretAtEnd==false&&draftWrites==writesBeforeException+1,
            "selection failure preserves successful staging without rewriting");
        append=await Tab5CodexComposer.AppendAsync(caretCalls with {Write=(_,_)=>false},"用户正在编辑",default);
        Check(!append.Staged&&caretMoves==2,"rejected append never moves the caret");
        append=await Tab5CodexComposer.AppendAsync(caretCalls with {Write=(_,value)=>{draftWrites++;draftValue=value;readable=false;return true;}},"未确认",default);
        Check(!append.Staged&&caretMoves==2,"unconfirmed readback never moves the caret");
        readable=true;
        var clearCalls=new Tab5CodexComposer.ClearDraftCalls(
            ()=>new("main",draftValue),before=>{Check(before.Text==draftValue,"clear checks the original draft");clearWrites++;draftValue="";return true;},_=>Task.CompletedTask);
        var cleared=await Tab5CodexComposer.ClearDraftAsync(clearCalls,default);
        Check(cleared.Attempted&&cleared.Cleared&&clearWrites==1&&draftValue=="","clear writes once and verifies an empty draft without Enter");
        Check((await Tab5CodexComposer.ClearDraftAsync(clearCalls,default)).Cleared&&clearWrites==1,"already empty composer needs no write");
        draftValue="用户新改的草稿";
        cleared=await Tab5CodexComposer.ClearDraftAsync(clearCalls with {Write=_=>false},default);
        Check(!cleared.Attempted&&!cleared.Cleared&&clearWrites==1&&draftValue=="用户新改的草稿","changed draft is preserved");
        cleared=await Tab5CodexComposer.ClearDraftAsync(clearCalls with {Read=()=>null},default);
        Check(!cleared.Attempted&&!cleared.Cleared&&clearWrites==1,"unconfirmed foreground/target never clears");
        cleared=await Tab5CodexComposer.ClearDraftAsync(clearCalls with {Write=_=>{clearWrites++;return true;}},default);
        Check(cleared.Attempted&&!cleared.Cleared&&clearWrites==2,"unconfirmed clear readback never repeats the write");
        var a=new Tab5CodexTask(Guid.NewGuid().ToString(),"old","project",10);
        var b=new Tab5CodexTask(Guid.NewGuid().ToString(),"new","project",30);
        var c=new Tab5CodexTask(Guid.NewGuid().ToString(),"middle","project",20);
        Tab5CodexTask[] tasks=[a,b,c];
        var reconciled=Tab5CodexCatalog.ReconcileActivity(tasks,new Dictionary<string,Tab5CodexCatalog.Activity> {
            [a.Id]=new(40,false),[c.Id]=new(50,true),[Guid.NewGuid().ToString()]=new(100,false)});
        Check(Tab5QuickConsole.Select(reconciled,"")?.Id==a.Id,"running conversation uses live timestamp instead of stale catalog order");
        Check(reconciled.Count==2&&reconciled.All(t=>t.Id!=c.Id),"archived and internal-only conversations are not imported");
        Check(Tab5CodexCatalog.ReconcileActivity(tasks,new Dictionary<string,Tab5CodexCatalog.Activity> {[b.Id]=new(5,false)})[1].UpdatedAt==30,"older live timestamp never rolls back catalog recency");
        Check(Tab5QuickConsole.Select(tasks,"")==b,"latest uses timestamp, not catalog/pin order");
        Check(Tab5QuickConsole.Select(tasks,c.Id)==c,"explicit older choice preserved");
        Check(Tab5QuickConsole.Select(tasks,Guid.NewGuid().ToString()) is null,"unknown choice never falls back to another task");
        Check(Tab5QuickConsole.Select([],"") is null,"empty catalog");
        int latestOpens=0;string latestOpened="";
        Tab5CodexTask[] changing=[a];
        var latestController=new Tab5QuickConsole(()=>changing,(id,_)=>{latestOpens++;latestOpened=id;return Task.FromResult(true);});
        string latestRequest=Guid.NewGuid().ToString();
        JsonElement Latest(string requestId)=>JsonSerializer.SerializeToElement(new{op="open-recent",requestId});
        Check((await latestController.HandleAsync(Latest(latestRequest),default)).Status==200&&latestOpened==a.Id,"resolve latest at click time");
        changing=[a,b];
        Check((await latestController.HandleAsync(Latest(latestRequest),default)).Status==200&&latestOpens==1&&latestOpened==a.Id,"lost reply retains originally resolved target");
        var latestResult=await latestController.HandleAsync(Latest(Guid.NewGuid().ToString()),default);
        var latestBody=JsonSerializer.SerializeToElement(latestResult.Body);
        Check(latestResult.Status==200&&latestOpened==b.Id&&latestOpens==2,"next click resolves new latest");
        Check(latestBody.GetProperty("taskId").GetString()==b.Id&&latestBody.GetProperty("folder").GetString()==b.Folder&&latestBody.GetProperty("updatedAt").GetInt64()==b.UpdatedAt,"latest reply carries actual panel target");
        int opens=0;string opened="";
        var controller=new Tab5QuickConsole(()=>tasks,(id,_)=>{opens++;opened=id;return Task.FromResult(true);});
        string request=Guid.NewGuid().ToString();
        JsonElement Request(string id,string requestId)=>JsonSerializer.SerializeToElement(new{op="open-recent",taskId=id,requestId});
        var result=await controller.HandleAsync(Request(c.Id,request),default);
        Check(result.Status==200&&opened==c.Id&&opens==1,"selected conversation dispatched");
        tasks=[b,a];
        result=await controller.HandleAsync(Request(c.Id,request),default);
        Check(result.Status==200&&opens==1,"lost reply replay does not reopen or reselect");
        result=await controller.HandleAsync(Request(b.Id,request),default);
        Check(result.Status==409&&opens==1,"request id cannot change target");
        result=await controller.HandleAsync(Request(c.Id,Guid.NewGuid().ToString()),default);
        Check(result.Status==404&&opens==1,"removed conversation rejected");
        controller=new(()=>tasks,(_,_)=>Task.FromResult(false));
        Check((await controller.HandleAsync(Request(b.Id,Guid.NewGuid().ToString()),default)).Status==409,"foreground failure is not success");
        int stages=0;string stagedText="";
        controller=new(()=>tasks,(_,_)=>throw new Exception("draft must not call open-only delegate"),
            (id,text,_)=>{Check(id==b.Id,"draft target");stages++;stagedText=text;return Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"",stages>1));});
        JsonElement Draft(string text,string requestId,string? task=null)=>JsonSerializer.SerializeToElement(new{op="stage-draft",taskId=task??b.Id,requestId,text});
        request=Guid.NewGuid().ToString();
        result=await controller.HandleAsync(Draft("中文 & # ?\n等待确认",request),default);
        Check(result.Status==200&&stages==1&&stagedText.Contains("等待确认"),"draft placed without starting a turn");
        Check(!JsonSerializer.SerializeToElement(result.Body).GetProperty("appended").GetBoolean(),"first empty-composer stage reports no append");
        Check((await controller.HandleAsync(Draft(stagedText,request),default)).Status==200&&stages==1,"draft replay does not insert twice");
        Check((await controller.HandleAsync(Draft("changed",request),default)).Status==409&&stages==1,"draft replay content conflict");
        Check((await controller.HandleAsync(Draft("text",Guid.NewGuid().ToString(),""),default)).Status==400,"draft cannot silently select latest");
        Check((await controller.HandleAsync(Draft(new string('中',667),Guid.NewGuid().ToString()),default)).Status==400,"oversized draft rejected");
        result=await controller.HandleAsync(Draft("补录",Guid.NewGuid().ToString()),default);
        Check(result.Status==200&&JsonSerializer.SerializeToElement(result.Body).GetProperty("appended").GetBoolean(),"confirmed append is preserved in the desktop reply");
        string uri=Tab5CodexComposer.DraftUri(b.Id,stagedText);
        Check(Uri.UnescapeDataString(uri.Split("?prompt=")[1])==stagedText&&!uri.Contains("\n"),"draft URI preserves literal text");
        controller=new(()=>tasks,stage:(_,_,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(false,false,"composer_not_ready")));
        Check((await controller.HandleAsync(Draft("keep",Guid.NewGuid().ToString()),default)).Status==409,"unconfirmed draft is not success");
        int submits=0;
        bool acknowledged=true;
        controller=new(()=>tasks,stage:(_,_,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"")),submit:(target,_)=>{
            Check(target==b.Id,"submit binds the staged conversation");submits++;
            return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,acknowledged,acknowledged?"":"unknown"));});
        string staged=Guid.NewGuid().ToString(),sendId=Guid.NewGuid().ToString();
        JsonElement Submit(string id,string draft,string? target=null)=>JsonSerializer.SerializeToElement(new{op="submit-draft",taskId=target??b.Id,requestId=id,draftRequestId=draft});
        foreach(bool? empty in new bool?[]{false,null,true}) {
            int reads=0,sends=0;bool openedOk=true;
            var sync=new Tab5QuickConsole(()=>tasks,(_,_)=>Task.FromResult(openedOk),
                stage:(_,_,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"")),
                submit:(_,_)=>{sends++;return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,true,""));},
                readDraftEmpty:target=>{Check(target==b.Id,"inspect only the confirmed target");reads++;return empty;});
            string ticket=Guid.NewGuid().ToString(),openId=Guid.NewGuid().ToString();
            await sync.HandleAsync(Draft("retained draft",ticket),default);
            Check(reads==0,"staging does not run open-only observation");
            var synced=await sync.HandleAsync(Request(b.Id,openId),default);
            var state=JsonSerializer.SerializeToElement(synced.Body).GetProperty("draftEmpty");
            Check(synced.Status==200&&reads==1&&sends==0&&
                (empty is null?state.ValueKind==JsonValueKind.Null:state.GetBoolean()==empty.Value),"open reports empty, nonempty and unknown distinctly without sending");
            await sync.HandleAsync(Request(b.Id,openId),default);
            Check(reads==1,"replayed open keeps original observation");
            var submitAfterOpen=await sync.HandleAsync(Submit(Guid.NewGuid().ToString(),ticket),default);
            Check(empty==true?submitAfterOpen.Status==409&&sends==0:submitAfterOpen.Status==200&&sends==1,
                "only confirmed empty retires the old send ticket");
            openedOk=false;
            Check((await sync.HandleAsync(Request(b.Id,Guid.NewGuid().ToString()),default)).Status==409&&reads==1,
                "failed navigation never observes another composer");
        }
        Check((await controller.HandleAsync(Submit(sendId,staged),default)).Status==409&&submits==0,"cannot submit without successful staging");
        Check((await controller.HandleAsync(Draft("review",staged),default)).Status==200&&submits==0,"staging alone never submits");
        Check((await controller.HandleAsync(Submit(Guid.NewGuid().ToString(),staged,a.Id),default)).Status==409&&submits==0,"ticket cannot move to another conversation");
        Check((await controller.HandleAsync(Submit(sendId,staged),default)).Status==200&&submits==1,"explicit third action submits once");
        Check((await controller.HandleAsync(Submit(sendId,staged),default)).Status==200&&submits==1,"lost submit acknowledgement replay never repeats Enter");
        Check((await controller.HandleAsync(Submit(Guid.NewGuid().ToString(),staged),default)).Status==409&&submits==1,"new request cannot reuse consumed stage ticket");
        acknowledged=false;staged=Guid.NewGuid().ToString();
        await controller.HandleAsync(Draft("uncertain",staged),default);
        Check((await controller.HandleAsync(Submit(Guid.NewGuid().ToString(),staged),default)).Status==409&&submits==2,"unknown outcome is reported as unknown");
        Check((await controller.HandleAsync(Submit(Guid.NewGuid().ToString(),staged),default)).Status==409&&submits==2,"unknown outcome cannot be automatically retried");
        int clears=0;submits=0;
        controller=new(()=>tasks,open:(_,_)=>throw new Exception("clear must not navigate"),
            stage:(_,_,_)=>Task.FromResult(new Tab5CodexComposer.StageResult(true,true,"")),
            submit:(_,_)=>{submits++;return Task.FromResult(new Tab5CodexComposer.SubmitResult(true,true,""));},
            clear:(target,_)=>{clears++;return Task.FromResult(new Tab5CodexComposer.ClearDraftResult(target==b.Id,target==b.Id,target==b.Id?"":"clear_target_unconfirmed"));});
        JsonElement Clear(string id,string target)=>JsonSerializer.SerializeToElement(new{op="clear-draft",taskId=target,requestId=id});
        Check((await controller.HandleAsync(Clear(Guid.NewGuid().ToString(),""),default)).Status==400&&clears==0,"clear requires an explicit target");
        Check((await controller.HandleAsync(Clear(Guid.NewGuid().ToString(),a.Id),default)).Status==409,"wrong desktop target stays unconfirmed");
        staged=Guid.NewGuid().ToString();await controller.HandleAsync(Draft("to clear",staged),default);
        string clearId=Guid.NewGuid().ToString();
        result=await controller.HandleAsync(Clear(clearId,b.Id),default);
        Check(result.Status==200&&JsonSerializer.SerializeToElement(result.Body).GetProperty("cleared").GetBoolean()&&clears==2,"explicit clear is confirmed");
        Check((await controller.HandleAsync(Clear(clearId,b.Id),default)).Status==200&&clears==2,"lost clear acknowledgement does not clear a new draft again");
        Check((await controller.HandleAsync(Submit(Guid.NewGuid().ToString(),staged),default)).Status==409&&submits==0,"clear invalidates the old send ticket");
        int presses=0,keyUps=0;
        var enter=new Tab5CodexComposer.EnterCalls(()=>true,()=>{presses++;return 2;},()=>keyUps++,_=>Task.FromResult(true));
        Check((await Tab5CodexComposer.SendEnterAsync(enter with{Ready=()=>false},default)).Attempted==false&&presses==0,"wrong foreground/editor or held keys reject before input");
        Check((await Tab5CodexComposer.SendEnterAsync(enter,default)).Submitted&&presses==1,"one complete Enter pair with composer acknowledgement");
        Check((await Tab5CodexComposer.SendEnterAsync(enter with{Press=()=>1},default)).Attempted&&keyUps==1,"partial Enter releases the key and stays uncertain");
        Check((await Tab5CodexComposer.SendEnterAsync(enter with{Press=()=>1,Release=()=>throw new InvalidOperationException()},default)).Attempted,"failure releasing partial input remains uncertain");
        Check(!(await Tab5CodexComposer.SendEnterAsync(enter with{Confirm=_=>Task.FromResult(false)},default)).Submitted,"unchanged composer is not confirmed submission");
        Check((await Tab5CodexComposer.SendEnterAsync(enter with{Confirm=_=>throw new OperationCanceledException()},default)).Attempted,"cancellation after input cannot permit blind replay");
        int observations=0,waits=0,once=0;
        var refreshed=await Tab5CodexComposer.SendEnterAsync(enter with {
            Press=()=>{once++;return 2;},
            Confirm=async ct=>(await Tab5CodexComposer.ConfirmClearAsync(()=>{
                observations++;
                if(observations==1)throw new System.Windows.Automation.ElementNotAvailableException();
                if(observations==2)throw new System.Runtime.InteropServices.COMException();
                return observations==3?null:observations>=5;
            },_=>{waits++;return Task.CompletedTask;},ct)).Cleared
        },default);
        Check(refreshed.Submitted&&once==1&&observations==5&&waits==4,"composer replacement and transient UIA failure recover with exactly one Enter");
        var uncleared=await Tab5CodexComposer.ConfirmClearAsync(()=>false,_=>Task.CompletedTask,default);
        Check(!uncleared.Cleared&&uncleared.Reads==40,"nonempty draft never turns into confirmed submission");
        var missing=await Tab5CodexComposer.ConfirmClearAsync(()=>throw new System.Windows.Automation.ElementNotAvailableException(),_=>Task.CompletedTask,default);
        Check(!missing.Cleared&&missing.Unavailable==40,"persistent unavailable editor stays uncertain after bounded observation");
        Console.WriteLine("TAB5_QUICK_CONSOLE_OK latest, selection, foreground, staging, actual_append_hint, explicit_clear_once, clear_invalidates_send, explicit_submit, one_use_ticket, lost_reply_no_repeat, uncertain_no_retry, Enter_focus_and_partial_input");
    }
}
