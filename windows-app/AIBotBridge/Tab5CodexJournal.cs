using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5SendRecord(string Id,string TaskId,string Hash,string Operation,string ExpectedTurn,
    string State,string TurnId,long UpdatedAt,int Status=0,string Error="",string Device="",string ResolutionSource="",ClaudeNativeReceipt? NativeReceipt=null,string NativeScope="",string? NativeFailureStage=null);
internal sealed record Tab5DraftRecord(string Text,long UpdatedAt,string[]? Images=null);

// User content stays in an atomic, current-Windows-user encrypted file. A corrupt
// journal fails closed for writes; it must never erase receipt/deduplication data.
internal sealed class Tab5CodexJournal
{
    private sealed record Data(int Version,Dictionary<string,Tab5DraftRecord> Drafts,Dictionary<string,Tab5SendRecord> Sends);
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("AI-bot TAB5 drafts and receipts v1");
    private readonly object _gate=new();
    private readonly string _path;
    private Data _data=new(1,new(),new());
    private bool _damaged;
    internal Tab5CodexJournal(string? path=null) {
        _path=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-codex.dat");
        if(!File.Exists(_path))return;
        try {
            byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(_path),Entropy,DataProtectionScope.CurrentUser);
            try {
                var data=JsonSerializer.Deserialize<Data>(clear);
                if(data is null||data.Version!=1||data.Drafts is null||data.Sends is null||data.Drafts.Count>80||data.Sends.Count>512)
                    throw new InvalidDataException();
                _data=data;
            }finally{CryptographicOperations.ZeroMemory(clear);}
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException) {_damaged=true;}
    }
    internal string Draft(string id) {lock(_gate){Check();return _data.Drafts.TryGetValue(id,out var value)?value.Text:"";}}
    internal string[] Images(string id) {lock(_gate){Check();return _data.Drafts.GetValueOrDefault(id)?.Images??[];}}
    internal Tab5SendRecord? Find(string id) {lock(_gate){Check();return _data.Sends.GetValueOrDefault(id);}}
    internal bool ConfirmsNativeStop(string task,string turn) {
        lock(_gate)return !_damaged&&turn.Length>0&&_data.Sends.Values.Any(r=>
            r.TaskId==task&&r.Operation=="interrupt"&&r.ExpectedTurn==turn&&r.TurnId==turn&&r.Status==202&&
            r.State is "accepted" or "completed"&&r.ResolutionSource is "native_interrupt_confirmed" or "session_api_pending");
    }
    internal bool CanConfirm(Tab5SendRecord record,string device) {
        lock(_gate) {
            Check();var draft=_data.Drafts.GetValueOrDefault(record.TaskId);
            bool confirmable=record.Operation=="steer"&&Guid.TryParse(record.ExpectedTurn,out _)||
                record.Operation=="send"&&record.ResolutionSource=="native_pending"&&record.ExpectedTurn.Length==0;
            return device.Length>0&&record.Device==device&&record.State=="dispatching"&&confirmable&&_data.Sends.GetValueOrDefault(record.Id)==record&&
                draft is not null&&draft.Text.Length>0&&(draft.Images?.Length??0)==0&&
                record.Hash==Fingerprint(record.TaskId,draft.Text,record.Operation,record.ExpectedTurn,draft.Images);
        }
    }
    internal Tab5SendRecord? ConfirmDelivered(string id,string task,string device,string text,string expected,string[] images) {
        lock(_gate) {
            Check();var record=_data.Sends.GetValueOrDefault(id);
            if(record is null||record.TaskId!=task||record.Device!=device||device.Length==0)return null;
            if(record.State is "accepted" or "completed")return record;
            if(!CanConfirm(record,device)||images.Length!=0||record.ExpectedTurn!=expected||
                record.Hash!=Fingerprint(task,text,record.Operation,expected,images))return null;
            // A user's confirmation of a native send proves delivery, not the
            // identity of its execution turn. Do not invent a turn or watch one.
            var accepted=record with {State=record.Operation=="send"?"completed":"accepted",Status=202,TurnId=record.ExpectedTurn,
                UpdatedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),ResolutionSource="device_user_confirmed_delivered"};
            Save(accepted);return accepted;
        }
    }
    internal Tab5SendRecord? Pending(string task) {lock(_gate){Check();return _data.Sends.Values.Where(x=>x.TaskId==task&&x.State=="dispatching").OrderByDescending(x=>x.UpdatedAt).FirstOrDefault();}}
    internal Tab5SendRecord[] Accepted() {lock(_gate)return _damaged?[]:_data.Sends.Values.Where(x=>x.State=="accepted").ToArray();}
    internal void SaveDraft(string task,string text,string[]? images=null,string device="") {
        if(!Guid.TryParse(task,out _)||Encoding.UTF8.GetByteCount(text)>2000)throw new ArgumentException("Invalid draft");
        lock(_gate) {
            Check();images??=[];
            var released=text.Length==0&&images.Length==0&&device.Length>0?
                _data.Sends.Values.Where(v=>v.TaskId==task&&v.Device==device&&v.State=="dispatching"&&v.Operation!="interrupt").ToArray():[];
            if(released.Length==0&&(_data.Drafts.GetValueOrDefault(task)?.Text??"")==text&&Images(task).SequenceEqual(images))return;
            var drafts=new Dictionary<string,Tab5DraftRecord>(_data.Drafts);
            if(text.Length==0&&images.Length==0)drafts.Remove(task);else drafts[task]=new(text,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),images);
            // Never evict an unsent draft to make room for another.
            if(drafts.Count>80)throw new IOException("Draft storage full");
            var sends=new Dictionary<string,Tab5SendRecord>(_data.Sends);
            // Explicit empty draft-save releases this device's composer. Keep
            // the unknown receipt and its ID forever subject to the storage cap.
            foreach(var record in released)sends[record.Id]=record with {State="abandoned",Status=504,Error="delivery_unknown",
                ResolutionSource="device_user_discarded_draft",UpdatedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
            Commit(_data with {Drafts=drafts,Sends=sends});
        }
    }
    internal void Save(Tab5SendRecord record,string? draft=null,string[]? images=null) {
        lock(_gate) {
            Check();var sends=new Dictionary<string,Tab5SendRecord>(_data.Sends){[record.Id]=record};
            if(_data.Sends.GetValueOrDefault(record.Id)?.State=="abandoned")return;
            // Uncertain and accepted receipts are never automatically evicted.
            foreach(var old in sends.Values.Where(v=>v.State is "completed" or "rejected").OrderBy(v=>v.UpdatedAt).ToArray()) {
                if(sends.Count<=512)break;sends.Remove(old.Id);
            }
            if(sends.Count>512)throw new IOException("Receipt storage full");
            var drafts=new Dictionary<string,Tab5DraftRecord>(_data.Drafts);
            if(draft is not null&&record.Operation!="interrupt") {
                if(Encoding.UTF8.GetByteCount(draft)>2000)throw new IOException("Draft too long");
                drafts[record.TaskId]=new(draft,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),images);
                if(drafts.Count>80)throw new IOException("Draft storage full");
            }
            if((record.State=="accepted"||record.State=="completed"&&record.ResolutionSource=="device_user_confirmed_delivered")&&record.Operation!="interrupt"&&drafts.TryGetValue(record.TaskId,out var saved)&&
                record.Hash==Fingerprint(record.TaskId,saved.Text,record.Operation,record.ExpectedTurn,saved.Images))drafts.Remove(record.TaskId);
            Commit(_data with {Sends=sends,Drafts=drafts});
        }
    }
    internal void Finish(string task,string turn) {
        lock(_gate) {
            Check();var changed=_data.Sends.Values.Where(v=>v.TaskId==task&&v.TurnId==turn&&v.State=="accepted").ToArray();
            if(changed.Length==0)return;
            var sends=new Dictionary<string,Tab5SendRecord>(_data.Sends);
            foreach(var value in changed)sends[value.Id]=value with {State="completed",UpdatedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
            Commit(_data with {Sends=sends});
        }
    }
    private void Check(){if(_damaged)throw new IOException("Draft/receipt journal is unavailable");}
    private void Commit(Data next) {
        byte[] clear=JsonSerializer.SerializeToUtf8Bytes(next);
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            byte[] encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
            using(var file=new FileStream(_path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)) {
                file.Write(encrypted);file.Flush(true);
            }
            File.Move(_path+".tmp",_path,true);_data=next;
        }finally{CryptographicOperations.ZeroMemory(clear);}
    }
    internal static string Fingerprint(string task,string message,string operation,string expectedTurn,string[]? images=null)=>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(images is {Length:>0}
            ?JsonSerializer.Serialize(new{task,message,operation,expectedTurn,images})
            :JsonSerializer.Serialize(new{task,message,operation,expectedTurn}))));
}
