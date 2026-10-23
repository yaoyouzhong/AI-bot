using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

// Separate from pairing data and ordinary settings. No plaintext export or backup.
internal sealed class Tab5PinStore(string? path=null)
{
    private static readonly object Gate=new();
    private readonly string _path=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-unlock-pin.dat");
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("AI-bot TAB5 unlock PIN v1");
    internal bool Exists {get{lock(Gate)return File.Exists(_path)||File.Exists(_path+".tmp");}}
    private static byte[] Binding(Tab5Pairing pair)=>SHA256.HashData(Encoding.UTF8.GetBytes(pair.DeviceId+"|"+pair.Key));
    internal static bool Valid(ReadOnlySpan<byte> pin){if(pin.Length is <4 or >64)return false;foreach(byte value in pin)if(value<'0'||value>'9')return false;return true;}
    internal bool Has(Tab5Pairing? pair)=>Available(pair,false);
    internal bool IsEnabled(Tab5Pairing? pair)=>Available(pair,true);
    private bool Available(Tab5Pairing? pair,bool enabledOnly){if(pair is null)return false;try{var pin=Read(pair,enabledOnly);try{return pin is not null;}finally{if(pin is not null)CryptographicOperations.ZeroMemory(pin);}}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return false;}}
    internal bool Enabled(Tab5Pairing? pair){if(pair is null)return false;try{lock(Gate){var record=Record(pair);try{return record is {Length:>=34}&&record[0]==2&&record[33]==1;}finally{if(record is not null)CryptographicOperations.ZeroMemory(record);}}}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return false;}}
    internal void Save(Tab5Pairing pair,ReadOnlySpan<byte> pin)
    {
        if(!Valid(pin))throw new ArgumentException("请输入 4–64 位数字 PIN。");
        lock(Gate)Write(pair,pin,Enabled(pair));
    }
    internal void SetEnabled(Tab5Pairing pair,bool enabled)
    {
        lock(Gate){var pin=Read(pair);try{Write(pair,pin??[],enabled);}finally{if(pin is not null)CryptographicOperations.ZeroMemory(pin);}}
    }
    private void Write(Tab5Pairing pair,ReadOnlySpan<byte> pin,bool enabled)
    {
        byte[] clear=new byte[34+pin.Length];clear[0]=2;Binding(pair).CopyTo(clear,1);clear[33]=enabled?(byte)1:(byte)0;pin.CopyTo(clear.AsSpan(34));
        try{Directory.CreateDirectory(Path.GetDirectoryName(_path)!);byte[] encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path+".tmp",encrypted);AtomicFileReplacement.Move(_path+".tmp",_path);}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
    // v1 candidate records retain their PIN but default to disabled. v2 stores
    // the independent enable flag and optional PIN in one atomic DPAPI record.
    private byte[]? Record(Tab5Pairing pair)
    {
        if(!File.Exists(_path))return null;
        if(new FileInfo(_path).Length>4096)throw new CryptographicException("Invalid PIN record");
        byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(_path),Entropy,DataProtectionScope.CurrentUser);
        int offset=clear.Length>0&&clear[0]==1?33:34;
        if(clear.Length>=offset&&(clear[0]==1||clear[0]==2)&&CryptographicOperations.FixedTimeEquals(clear.AsSpan(1,32),Binding(pair))&&
           (clear[0]!=2||clear[33]<=1)&&(clear.Length==offset||Valid(clear.AsSpan(offset))))return clear;
        CryptographicOperations.ZeroMemory(clear);return null;
    }
    internal byte[]? Read(Tab5Pairing pair,bool requireEnabled=false)
    {
        lock(Gate){var clear=Record(pair);if(clear is null)return null;
            try{if(requireEnabled&&(clear[0]!=2||clear[33]!=1))return null;int offset=clear[0]==1?33:34;return Valid(clear.AsSpan(offset))?clear.AsSpan(offset).ToArray():null;}
            finally{CryptographicOperations.ZeroMemory(clear);}
        }
    }
    internal void Delete(){lock(Gate){File.Delete(_path);File.Delete(_path+".tmp");}}
}

internal sealed class Tab5PinBroker(Tab5PinStore store,Func<ComputerLockScreenState>? screen=null,TimeProvider? clock=null)
{
    private readonly object _gate=new();
    private readonly Dictionary<string,DateTimeOffset> _used=[];
    internal (int Status,object Body) Handle(System.Text.Json.JsonElement root,Tab5Pairing pair,string transport)
    {
        string Text(string name)=>root.TryGetProperty(name,out var v)&&v.ValueKind==System.Text.Json.JsonValueKind.String?v.GetString()!:"";
        string id=Text("requestId");
        string[] allowed=["kind","op","requestId","session","issuedAt","binaryReply","bulkReply"];
        if(transport is not ("USB" or "BLE")||Text("op")!="read"||!Guid.TryParseExact(id,"D",out _)||
            root.EnumerateObject().Any(x=>!allowed.Contains(x.Name))||root.EnumerateObject().GroupBy(x=>x.Name).Any(g=>g.Count()!=1))return(400,new{error="invalid_pin_request"});
        lock(_gate){
            var now=(clock??TimeProvider.System).GetUtcNow();
            foreach(var expired in _used.Where(x=>now-x.Value>TimeSpan.FromMinutes(2)).Select(x=>x.Key).ToArray())_used.Remove(expired);
            if(_used.Count>=256||!_used.TryAdd(id,now))return(409,new{error="pin_request_consumed"});
            var state=(screen??WindowsLockScreen.Read)();
            // Existing detector positively identifies the wallpaper only; null is
            // not proof of a PIN field. The device still runs its normal preparation.
            if(state.Locked!=true||state.DisplayState is not (1 or 2)||state.LockScreenCover==true)return(409,new{error="screen_not_ready"});
            byte[]? pin=null;byte[] key=Convert.FromBase64String(pair.Key);
            try{
                pin=store.Read(pair,requireEnabled:true);if(pin is null)return(404,new{error="pin_not_configured"});
                // Inner encryption keeps the PIN out of JSON objects, RPC receipts and diagnostics.
                byte[] packet=Tab5Protocol.Encrypt(key,id.Replace("-",""),pin,128);
                return(200,new{version=1,requestId=id,packet=Convert.ToBase64String(packet)});
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return(503,new{error="pin_unavailable"});}
            finally{if(pin is not null)CryptographicOperations.ZeroMemory(pin);CryptographicOperations.ZeroMemory(key);}
        }
    }
}
