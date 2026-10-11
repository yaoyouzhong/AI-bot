using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// Read-only adapter for the desktop's account/host scoped thread read state.
// The desktop stores task IDs, so acknowledgements must bind to a stable turn.
internal sealed class Tab5CodexReadState
{
    internal sealed record Snapshot(string Scope,HashSet<string> Unread);
    private sealed record Observed(string Turn,long Since);
    private readonly Dictionary<string,Observed> _turns=new();
    private string _scope="";
    private readonly Func<Snapshot?> _read;
    private readonly Func<long> _clock;
    private readonly object _gate=new();
    private Dictionary<string,string> _unread=new();
    internal Dictionary<string,string> Unread {get {lock(_gate)return new(_unread);}}
    internal Tab5CodexReadState(Func<Snapshot?>? read=null,Func<long>? clock=null)
    { _read=read??ReadLocal;_clock=clock??(()=>Environment.TickCount64); }

    internal Dictionary<string,string> Synchronize(IReadOnlyDictionary<string,string> ready)
    { lock(_gate)return SynchronizeLocked(ready); }
    private Dictionary<string,string> SynchronizeLocked(IReadOnlyDictionary<string,string> ready)
    {
        var result=new Dictionary<string,string>();var snapshot=_read();_unread=new();
        if(snapshot is null) { _turns.Clear();_scope="";return result; }
        if(_scope!=snapshot.Scope) { _turns.Clear();_scope=snapshot.Scope; }
        long now=_clock();
        foreach(var task in _turns.Keys.Where(id=>!ready.ContainsKey(id)).ToArray())_turns.Remove(task);
        foreach(var (task,turn) in ready) {
            if(!_turns.TryGetValue(task,out var observed)||observed.Turn!=turn)
                _turns[task]=observed=new(turn,now);
            // Allow the desktop completion/unread write to settle before treating
            // an absent task as read. Never carry an old acknowledgement forward.
            if(snapshot.Unread.Contains(task)) {_turns[task]=new(turn,now);if(_unread.Count<80)_unread[task]=turn;}
            else if(now-observed.Since>=5000&&result.Count<80)result[task]=turn;
        }
        return result;
    }

    internal static Snapshot? ReadLocal()
    {
        try {
            if(Environment.GetEnvironmentVariable("CODEX_APP_SERVER_FORCE_CLI")!="1"&&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CODEX_APP_SERVER_WS_URL")))return null;
            string home=Tab5CodexCatalog.HomePath;
            return Parse(ReadSmall(Path.Combine(home,"auth.json")),ReadSmall(Path.Combine(home,".codex-global-state.json")));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException) { return null; }
    }
    private static string ReadSmall(string path) {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(stream.Length>16*1024*1024)throw new IOException("Codex state too large");
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
    internal static Snapshot? Parse(string authJson,string stateJson)
    {
        using var auth=JsonDocument.Parse(authJson);using var global=JsonDocument.Parse(stateJson);
        var tokens=Tab5CodexDesktop.Child(auth.RootElement,"tokens");
        string token=Tab5CodexDesktop.Text(tokens,"access_token");var parts=token.Split('.');
        if(parts.Length!=3)return null;
        string encoded=parts[1].Replace('-','+').Replace('_','/');encoded=encoded.PadRight((encoded.Length+3)/4*4,'=');
        using var claims=JsonDocument.Parse(Convert.FromBase64String(encoded));
        var expiry=Tab5CodexDesktop.Child(claims.RootElement,"exp");
        if(!expiry.TryGetInt64(out long expires)||expires<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())return null;
        var principal=Tab5CodexDesktop.Child(claims.RootElement,"https://api.openai.com/auth");
        string account=Tab5CodexDesktop.Text(principal,"chatgpt_account_id");if(account.Length==0)account=Tab5CodexDesktop.Text(principal,"account_id");
        string user=Tab5CodexDesktop.Text(principal,"user_id");if(user.Length==0)user=Tab5CodexDesktop.Text(principal,"chatgpt_user_id");
        if(account.Length==0||user.Length==0)return null;
        string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
        string identity=Hash(new[]{"chatgpt",account,user});
        // Standard local desktop host. A websocket host has a different key
        // and is deliberately unavailable rather than reading another bucket.
        string host="local:"+Hash(new object?[]{"local","local",null});
        var state=Tab5CodexDesktop.Child(global.RootElement,"electron-thread-read-state-v1");
        if(Tab5CodexDesktop.Child(state,"version").ValueKind!=JsonValueKind.Number||state.GetProperty("version").GetInt32()!=1)return null;
        var bucket=Tab5CodexDesktop.Child(Tab5CodexDesktop.Child(Tab5CodexDesktop.Child(state,"unreadByIdentity"),identity),host);
        if(bucket.ValueKind!=JsonValueKind.Array)return null;
        var unread=new HashSet<string>();
        foreach(var id in bucket.EnumerateArray()) {
            if(id.ValueKind!=JsonValueKind.String||!Guid.TryParse(id.GetString(),out var parsed))return null;
            unread.Add(parsed.ToString());
        }
        return new(identity+":"+host,unread);
    }
}
