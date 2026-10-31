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
            File.WriteAllBytes(path,[1,2,3,4]);journal=new Tab5CodexJournal(path);
            try{journal.SaveDraft(task,"must not overwrite corruption");throw new Exception("Corrupt journal accepted a write");}catch(IOException) { }
            if(File.ReadAllBytes(path).Length!=4)throw new Exception("Corrupt journal must remain recoverable");
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        Console.WriteLine("TAB5 journal: encrypted drafts, restart recovery, receipt lifecycle, stop preserves draft, corruption fails closed passed");
    }
}
