using System.Text;

namespace AIBotBridge;

internal static class Tab5CodexJournalSelfTest
{
    internal static void Run() {
        string folder=Path.Combine(Path.GetTempPath(),"tab5-journal-test-"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(folder,"journal.dat"),task=Guid.NewGuid().ToString(),id=Guid.NewGuid().ToString(),turn=Guid.NewGuid().ToString();
        var journal=new Tab5CodexJournal(path);
        try {
            const string text="离线草稿保存测试";
            journal.SaveDraft(task,text);
            if(new Tab5CodexJournal(path).Draft(task)!=text)throw new Exception("Draft must survive bridge restart");
            if(Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(text))throw new Exception("Journal must not contain plaintext user content");
            var record=new Tab5SendRecord(id,task,Tab5CodexJournal.Fingerprint(task,text,"send",""),"send","","dispatching","",1);
            journal.Save(record,text);
            journal=new Tab5CodexJournal(path);
            if(journal.Pending(task)?.Id!=id||journal.Draft(task)!=text)throw new Exception("Uncertain send and draft must survive together");
            journal.Save(record with{State="accepted",TurnId=turn});
            journal=new Tab5CodexJournal(path);
            if(journal.Draft(task)!=""||journal.Accepted().Length!=1)throw new Exception("Only acknowledged send clears draft");
            journal.SaveDraft(task,"下一条草稿");
            journal.Save(record with{Id=Guid.NewGuid().ToString(),Operation="interrupt",State="accepted",TurnId=turn});
            if(journal.Draft(task)!="下一条草稿")throw new Exception("Stopping must preserve the draft");
            journal.Finish(task,turn);
            if(new Tab5CodexJournal(path).Find(id)?.State!="completed")throw new Exception("Completion receipt must persist");
            var pending=new Tab5SendRecord(Guid.NewGuid().ToString(),task,Tab5CodexJournal.Fingerprint(task,text,"steer",turn),
                "steer",turn,"dispatching","",2,Device:"fixture");
            journal.Save(pending,text);
            if(!journal.CanConfirm(pending,"fixture")||journal.CanConfirm(pending,"other"))throw new Exception("Recovery device binding");
            if(journal.ConfirmDelivered(pending.Id,task,"fixture","wrong",turn,[]) is not null)throw new Exception("Recovery content binding");
            journal.SaveDraft(task,"new draft");
            if(journal.CanConfirm(pending,"fixture"))throw new Exception("Changed draft cannot be confirmed");
            journal.SaveDraft(task,text);
            var confirmed=journal.ConfirmDelivered(pending.Id,task,"fixture",text,turn,[]);
            if(confirmed?.Status!=202||journal.Draft(task)!=""||confirmed.ResolutionSource!="device_user_confirmed_delivered")throw new Exception("Recovery clears only matching draft");
            journal.SaveDraft(task,"another draft");
            if(journal.ConfirmDelivered(pending.Id,task,"fixture",text,turn,[])?.Status!=202||journal.Draft(task)!="another draft")throw new Exception("Recovery replay must preserve new draft");
            journal.Save(pending with {Id=Guid.NewGuid().ToString(),State="accepted",TurnId=turn});
            if(journal.Draft(task)!="another draft")throw new Exception("Delayed acceptance cannot clear a changed draft");
            var native=record with {Id=Guid.NewGuid().ToString(),Device="fixture",ResolutionSource="native_pending"};
            journal.Save(native,text);
            if(!journal.CanConfirm(native,"fixture"))throw new Exception("Ordinary native send must have an explicit recovery exit");
            if(journal.ConfirmDelivered(native.Id,task,"fixture",text,"wrong",[]) is not null)throw new Exception("Send recovery must bind exact expected turn");
            var nativeConfirmed=journal.ConfirmDelivered(native.Id,task,"fixture",text,"",[]);
            if(nativeConfirmed is not {State:"completed",Status:202,TurnId:""}||journal.Draft(task)!="")throw new Exception("Explicit native confirmation clears only draft, never invents execution turn");
            journal.SaveDraft(task,"new after confirmation");
            if(journal.ConfirmDelivered(native.Id,task,"fixture",text,"",[])?.Status!=202||journal.Draft(task)!="new after confirmation")throw new Exception("Confirmation replay cannot delete new text");
            var discard=native with {Id=Guid.NewGuid().ToString()};journal.Save(discard,text);
            journal.SaveDraft(task,"",device:"other");
            if(journal.Find(discard.Id)?.State!="dispatching")throw new Exception("Different device cannot retire a receipt");
            journal.SaveDraft(task,text);journal.SaveDraft(task,"",device:"fixture");
            journal=new Tab5CodexJournal(path);
            if(journal.Find(discard.Id) is not {State:"abandoned",Status:504}||journal.Pending(task) is not null||journal.Draft(task)!="")throw new Exception("Explicit clear must release pending composer across restart without claiming delivery");
            journal.SaveDraft(task,text);journal.Save(discard with {State="accepted",TurnId=turn});
            if(journal.Draft(task)!=text||journal.Find(discard.Id)?.State!="abandoned")throw new Exception("Late receipt cannot resurrect retired request or erase same-text new draft");
            File.WriteAllBytes(path,[1,2,3,4]);journal=new Tab5CodexJournal(path);
            try{journal.SaveDraft(task,"must not overwrite corruption");throw new Exception("Corrupt journal accepted a write");}catch(IOException) { }
            if(File.ReadAllBytes(path).Length!=4)throw new Exception("Corrupt journal must remain recoverable");
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        Console.WriteLine("TAB5 journal: encrypted drafts, restart recovery, receipt lifecycle, stop preserves draft, corruption fails closed passed");
    }
}
