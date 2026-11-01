using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Short-lived delivery receipts only. No drafts, prompts or raw queue items.
// Reserve durably before invoking the native action; fail closed on corruption.
internal sealed class Tab5QuickConsoleJournal
{
    private static readonly Lazy<Tab5QuickConsoleJournal> Default=new(()=>new());
    internal static Tab5QuickConsoleJournal Shared=>Default.Value;
    internal sealed record Entry(string Device,string Request,string Fingerprint,string Draft,long At,int Status,JsonElement Body);
    private sealed record Data(int Version,List<Entry> Entries);
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("AI-bot desktop delivery receipts v1");
    private readonly string _path;
    private readonly object _gate=new();
    private readonly Func<long> _clock;
    private List<Entry> _entries=[];
    private bool _damaged;
    internal Tab5QuickConsoleJournal(string? path=null,Func<long>? clock=null) {
        _path=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-desktop-receipts.dat");
        _clock=clock??(()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if(!File.Exists(_path))return;
        try {
            if(new FileInfo(_path).Length>1024*1024)throw new IOException();
            var clear=ProtectedData.Unprotect(File.ReadAllBytes(_path),Entropy,DataProtectionScope.CurrentUser);
            try {var data=JsonSerializer.Deserialize<Data>(clear);if(data is null||data.Version!=1||data.Entries is null||data.Entries.Count>512||data.Entries.Any(e=>e is null||e.Device is null||e.Fingerprint is null||!Guid.TryParse(e.Request,out _)||!Guid.TryParse(e.Draft,out _)||e.At<=0||e.Status is not (200 or 409)||e.Body.ValueKind!=JsonValueKind.Object))throw new IOException();_entries=data.Entries;}
            finally {CryptographicOperations.ZeroMemory(clear);}
        }catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or CryptographicException){_damaged=true;}
    }
    internal Entry? Find(string device,string request) {
        lock(_gate){Check();return _entries.LastOrDefault(e=>e.Device==device&&e.Request==request&&_clock()-e.At<86400000);}
    }
    internal bool Used(string device,string draft) {
        lock(_gate){Check();return _entries.Any(e=>e.Device==device&&e.Draft==draft&&_clock()-e.At<86400000);}
    }
    internal void Save(string device,string request,string fingerprint,string draft,int status,object body) {
        lock(_gate) {
            Check();long now=_clock();var entries=_entries.Where(e=>now-e.At<86400000&&!(e.Device==device&&e.Request==request)).ToList();
            if(entries.Count>=512)throw new IOException("Desktop receipt capacity reached");
            entries.Add(new(device,request,fingerprint,draft,now,status,JsonSerializer.SerializeToElement(body)));
            var clear=JsonSerializer.SerializeToUtf8Bytes(new Data(1,entries));
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                byte[] encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
                using(var stream=new FileStream(_path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){stream.Write(encrypted);stream.Flush(true);}
                File.Move(_path+".tmp",_path,true);_entries=entries;
            }finally{CryptographicOperations.ZeroMemory(clear);}
        }
    }
    private void Check(){if(_damaged)throw new IOException("Desktop receipt journal unavailable");}
}
