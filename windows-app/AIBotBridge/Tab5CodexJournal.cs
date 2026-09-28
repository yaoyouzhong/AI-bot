using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5SendRecord(string Id,string TaskId,string Hash,string Operation,string ExpectedTurn,
    string State,string TurnId,long UpdatedAt,int Status=0,string Error="");
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
    internal Tab5SendRecord? Pending(string task) {lock(_gate){Check();return _data.Sends.Values.Where(x=>x.TaskId==task&&x.State=="dispatching").OrderByDescending(x=>x.UpdatedAt).FirstOrDefault();}}
    internal Tab5SendRecord[] Accepted() {lock(_gate)return _damaged?[]:_data.Sends.Values.Where(x=>x.State=="accepted").ToArray();}
    internal void SaveDraft(string task,string text,string[]? images=null) {
        if(!Guid.TryParse(task,out _)||Encoding.UTF8.GetByteCount(text)>2000)throw new ArgumentException("Invalid draft");
        lock(_gate) {
            Check();images??=[];if((_data.Drafts.GetValueOrDefault(task)?.Text??"")==text&&Images(task).SequenceEqual(images))return;
            var drafts=new Dictionary<string,Tab5DraftRecord>(_data.Drafts);
            if(text.Length==0&&images.Length==0)drafts.Remove(task);else drafts[task]=new(text,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),images);
            // Never evict an unsent draft to make room for another.
            if(drafts.Count>80)throw new IOException("Draft storage full");
            Commit(_data with {Drafts=drafts});
        }
    }
    internal void Save(Tab5SendRecord record,string? draft=null,string[]? images=null) {
        lock(_gate) {
            Check();var sends=new Dictionary<string,Tab5SendRecord>(_data.Sends){[record.Id]=record};
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
            if(record.State=="accepted"&&record.Operation!="interrupt")drafts.Remove(record.TaskId);
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
