using System.Runtime.InteropServices;
using System.Text.Json;

namespace AIBotBridge;

// Read only song metadata from the installed client's database. No credentials,
// process injection, database writes, or inference of playback position.
internal static class NeteaseLocalDuration
{
    private const string SelectRecent = "SELECT jsonStr FROM historyTracks ORDER BY playtime DESC LIMIT 32";
    internal static double? Read(string source, string title, string artist, string album)
    {
        if(!source.Contains("cloudmusic",StringComparison.OrdinalIgnoreCase) &&
           !source.Contains("netease",StringComparison.OrdinalIgnoreCase)) return null;
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetEase","CloudMusic","Library","webdb.dat");
        if(!File.Exists(path))return null;
        return ReadDatabase(path,title,artist,album);
    }
    internal static double? ReadDatabase(string path,string title,string artist,string album)
    {
        nint db=0,statement=0;
        try {
            if(sqlite3_open_v2(path,out db,1,0)!=0)return null; // SQLITE_OPEN_READONLY
            sqlite3_busy_timeout(db,40);
            if(sqlite3_prepare_v2(db,SelectRecent,-1,out statement,0)!=0)return null;
            while(sqlite3_step(statement)==100) {
                var bytes=sqlite3_column_bytes(statement,0);
                if(bytes is <=0 or >262144)continue;
                var json=Marshal.PtrToStringUTF8(sqlite3_column_text(statement,0),bytes);
                if(json is not null && Parse(json,title,artist,album) is {} seconds)return seconds;
            }
        } catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or JsonException or IOException or UnauthorizedAccessException) {}
        finally {if(statement!=0)sqlite3_finalize(statement);if(db!=0)sqlite3_close(db);}
        return null;
    }
    internal static double? Parse(string json,string title,string artist,string album)
    {
        try {
            using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
            static string Text(JsonElement e,string key)=>e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()?.Trim()??"":"";
            if(root.ValueKind!=JsonValueKind.Object||string.IsNullOrWhiteSpace(title)||Text(root,"name")!=title.Trim())return null;
            if(!root.TryGetProperty("artists",out var artists)||artists.ValueKind!=JsonValueKind.Array)return null;
            var names=artists.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.Object).Select(v=>Text(v,"name")).Where(v=>v.Length>0).ToArray();
            var wanted=artist.Split(new[]{'/',',',';','、'},StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            if(wanted.Length==0||!wanted.Order().SequenceEqual(names.Order(),StringComparer.OrdinalIgnoreCase))return null;
            if(album.Length>0&&(!root.TryGetProperty("album",out var a)||a.ValueKind!=JsonValueKind.Object||Text(a,"name")!=album.Trim()))return null;
            if(!root.TryGetProperty("duration",out var d)||d.ValueKind!=JsonValueKind.Number||!d.TryGetDouble(out var ms)||!double.IsFinite(ms)||ms is <=0 or >86400000)return null;
            return ms/1000;
        } catch(JsonException) {return null;}
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path,out nint db,int flags,nint vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(nint db,int ms);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int length,out nint statement,nint tail);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_step(nint statement);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern nint sqlite3_column_text(nint statement,int column);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(nint statement,int column);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_finalize(nint statement);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_close(nint db);
}
