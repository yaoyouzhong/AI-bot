using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIBotBridge;

// Display provenance only. Never use this cache to route or authorize a send.
internal sealed record ClaudeSessionSource(
    [property:JsonPropertyName("id")] string Id,
    [property:JsonPropertyName("label")] string Label,
    [property:JsonPropertyName("kind")] string Kind)
{
    internal static readonly ClaudeSessionSource Unknown=new("unknown","来源未知","unknown");
    internal static readonly ClaudeSessionSource Multiple=new("multiple","多端打开","unknown");
    internal static readonly ClaudeSessionSource VSCode=new("claude.vscode","VS Code","ide");
    internal static readonly ClaudeSessionSource Terminal=new("claude.terminal","原生终端","terminal");
    internal static readonly ClaudeSessionSource Background=new("claude.background","后台会话","harness");
    internal bool Valid=>Id is {Length:>0 and <=64}&&Id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '-' or '_')&&
        Kind is "ide" or "terminal" or "harness" or "unknown"&&Label is {Length:>0 and <=48}&&Encoding.UTF8.GetByteCount(Label)<=80&&
        Label.EnumerateRunes().All(r=>Rune.GetUnicodeCategory(r) is not (System.Globalization.UnicodeCategory.Control or System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator));
}

internal sealed class ClaudeSessionSources(string directory)
{
    private readonly string _path=Path.Combine(directory,"session-sources.dat");
    private readonly Dictionary<string,ClaudeSessionSource> _sources=[];
    private bool _loaded,_dirty;
    private void Load(){
        if(_loaded)return;_loaded=true;
        try{
            if(!File.Exists(_path)||new FileInfo(_path).Length>256*1024)return;
            byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(_path),null,DataProtectionScope.CurrentUser);
            try{foreach(var pair in (JsonSerializer.Deserialize<Dictionary<string,ClaudeSessionSource>>(clear)??[]).Take(512))
                if(Guid.TryParse(pair.Key,out _)&&pair.Value is {Valid:true}&&pair.Value.Id!="unknown")_sources[pair.Key]=pair.Value;
            }finally{CryptographicOperations.ZeroMemory(clear);}
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException){}
    }
    internal ClaudeSessionSource Get(string task){Load();return _sources.GetValueOrDefault(task)??ClaudeSessionSource.Unknown;}
    internal void Observe(string task,ClaudeSessionSource source){
        Load();if(!Guid.TryParse(task,out _)||!source.Valid||source.Id=="unknown"||_sources.GetValueOrDefault(task)==source)return;
        _sources[task]=source;while(_sources.Count>512)_sources.Remove(_sources.Keys.First());_dirty=true;
    }
    internal void Save(){
        if(!_dirty)return;
        byte[] clear=JsonSerializer.SerializeToUtf8Bytes(_sources);
        try{
            byte[] sealedData=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllBytes(_path+".tmp",sealedData);File.Move(_path+".tmp",_path,true);_dirty=false;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException){}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
}
