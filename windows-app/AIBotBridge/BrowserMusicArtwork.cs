using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// The optional browser companion supplies current playback and artwork. No
// browser history, account credentials or video identifiers are persisted.
internal sealed class BrowserMusicArtwork(Func<string,CancellationToken,Task<byte[]>>? fetch=null,Func<DateTimeOffset>? clock=null)
{
    private sealed record Cover(string Source,byte[] Bytes);
    private readonly Dictionary<string,Cover> _covers=[];
    private readonly object _sync=new();
    private readonly SemaphoreSlim _downloads=new(1,1);
    private readonly Func<string,CancellationToken,Task<byte[]>> _fetch=fetch??DownloadAsync;
    private readonly Func<DateTimeOffset> _now=clock??(()=>DateTimeOffset.UtcNow);
    private readonly Dictionary<string,MediaPlaybackSource> _playback=[];
    private readonly Dictionary<string,DateTimeOffset> _lastPacket=[];
    internal MediaPlaybackSource[] PlaybackSources()
    {
        lock(_sync){var now=_now();return _playback.Values.Where(s=>(now-s.Music.UpdatedAt).TotalSeconds is >=0 and <=3)
            .Select(s=>s with {Music=NowPlayingService.ProjectClock(s.Music,(now-s.Music.UpdatedAt).TotalSeconds) with {UpdatedAt=now}}).ToArray();}
    }
    internal static string KeyPath=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","browser-music-key.dat");
    internal static bool Authorized(string? origin,string? token) {
        if(token is null||!Regex.IsMatch(token,"^[0-9a-f]{64}$")||
           (origin is not null&&!Regex.IsMatch(origin,"^chrome-extension://[a-p]{32}$")))return false;
        try {
            if(!File.Exists(KeyPath))return false;
            string expected=File.ReadAllText(KeyPath).Trim();
            return expected.Length==64&&CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected),Encoding.ASCII.GetBytes(token));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return false;}
    }
    internal byte[]? Find(string title,string artist,string? source=null,string album="") {lock(_sync)return _covers.GetValueOrDefault((source is null?"":source+"\n")+Key(title,artist)+(source is null?"":"\n"+album.Trim().Normalize()))?.Bytes;}
    private static string Key(string title,string artist)=>title.Trim().Normalize()+"\n"+artist.Trim().Normalize();
    internal async Task<bool> AcceptAsync(string json,CancellationToken token) {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        string Text(string field)=>root.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
        string title=Text("title"),artist=Text("artist"),video=Text("youtubeVideoId");
        if(title.Length is 0 or >512||artist.Length>256)return false;
        bool playback=AcceptPlayback(root,title,artist);
        if(root.TryGetProperty("sampleTimeMs",out _)&&!playback)return false;
        var candidates=new List<string>();
        if(Regex.IsMatch(video,"^[A-Za-z0-9_-]{11}$")) {
            candidates.Add($"https://i.ytimg.com/vi/{video}/maxresdefault.jpg");
            candidates.Add($"https://i.ytimg.com/vi/{video}/hqdefault.jpg");
        }
        if(root.TryGetProperty("artwork",out var art)&&art.ValueKind==JsonValueKind.Array)
            candidates.AddRange(art.EnumerateArray().Take(8).Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).Where(v=>v.Length<=2048));
        candidates=candidates.Distinct().Take(10).ToList();
        if(candidates.Count==0)return playback;
        string session=Text("sessionId"),host=Text("source");
        string scope=Regex.IsMatch(session,@"^[A-Za-z0-9:_-]{1,80}$")&&Regex.IsMatch(host,@"^[A-Za-z0-9.-]{1,253}$")?"browser-companion:"+host+":"+session+"\n":"";
        string key=scope+Key(title,artist)+(scope.Length==0?"":"\n"+Text("album").Trim().Normalize()),source=string.Join("\n",candidates);
        await _downloads.WaitAsync(token);
        try {
            lock(_sync)if(_covers.GetValueOrDefault(key)?.Source==source)return playback;
            byte[]? best=null;int width=0;
            foreach(string url in candidates) {
                try {
                    var bytes=await _fetch(url,token);
                    if(bytes.Length is 0 or >2_000_000)continue;
                    using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream);
                    if(image.Width>4096||image.Height>4096||(long)image.Width*image.Height>16_777_216)continue;
                    if(image.Width>width){width=image.Width;best=bytes;}
                    if(width>=560)break;
                }catch(OperationCanceledException) when(!token.IsCancellationRequested){ }
                catch(Exception ex) when(ex is HttpRequestException or IOException or ArgumentException){ }
            }
            if(best is null)return playback; // A cover failure must not discard accepted playback.
            lock(_sync) {
                if(_covers.Count>=16&&!_covers.ContainsKey(key))_covers.Remove(_covers.Keys.First());
                _covers[key]=new(source,best);
            }
            return true;
        }finally{_downloads.Release();}
    }
    private bool AcceptPlayback(JsonElement root,string title,string artist)
    {
        string Text(string name)=>root.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()!:"";
        string session=Text("sessionId"),source=Text("source"),album=Text("album");
        if(!Regex.IsMatch(session,@"^[A-Za-z0-9:_-]{1,80}$")||!Regex.IsMatch(source,@"^[A-Za-z0-9.-]{1,253}$")||album.Length>512)return false;
        if(!root.TryGetProperty("ended",out var ended)||ended.ValueKind is not (JsonValueKind.True or JsonValueKind.False))return false;
        string id="browser-companion:"+source+":"+session;
        var now=_now();
        if(!root.TryGetProperty("sampleTimeMs",out var stamp)||stamp.ValueKind!=JsonValueKind.Number||!stamp.TryGetInt64(out long milliseconds)||milliseconds<0||milliseconds>253402300799999)return false;
        var at=DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);double age=(now-at).TotalSeconds;if(age is < -1 or >3)return false;if(at>now)at=now;
        if(ended.GetBoolean())return Store(null);
        if(!root.TryGetProperty("playing",out var playing)||playing.ValueKind is not (JsonValueKind.True or JsonValueKind.False))return false;
        double Number(string name)=>root.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetDouble(out var n)?n:double.NaN;
        double elapsed=Number("elapsedSeconds"),duration=Number("durationSeconds"),rate=Number("playbackRate");
        // Live streams omit a finite duration; their clock remains explicitly unavailable.
        bool valid=double.IsFinite(elapsed)&&elapsed is >=0 and <=86400&&double.IsFinite(duration)&&duration is >0 and <=86400&&elapsed<=duration+2;
        if(!double.IsFinite(rate)||rate is <=0 or >16)return false;
        var song=new MusicSnapshot(title,artist,album,playing.GetBoolean(),valid?Math.Min(elapsed,duration):0,valid?duration:0,at){TimelineAvailable=valid,PlaybackRate=rate};
        return Store(new(id,title+"\n"+artist+"\n"+album,song,"browser media element; explicitly enabled site"));
        bool Store(MediaPlaybackSource? sample){lock(_sync){
            foreach(var expired in _lastPacket.Where(s=>(now-s.Value).TotalSeconds>3).Select(s=>s.Key).ToArray()){_lastPacket.Remove(expired);_playback.Remove(expired);}
            if(_lastPacket.TryGetValue(id,out var last)&&at<last)return false;
            if(_lastPacket.Count>=32&&!_lastPacket.ContainsKey(id))return false;
            _lastPacket[id]=at;if(sample is null)_playback.Remove(id);else _playback[id]=sample;
            return true;}}
    }
    internal static bool PublicAddress(IPAddress ip) {
        if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();
        if(IPAddress.IsLoopback(ip))return false;
        byte[] b=ip.GetAddressBytes();
        return b.Length==4?b[0] is not (0 or 10 or 127)&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1]==168)&&b[0]<224:
            !ip.Equals(IPAddress.IPv6Any)&&!ip.IsIPv6LinkLocal&&!ip.IsIPv6Multicast&&(b[0]&0xfe)!=0xfc;
    }
    private static async Task<byte[]> DownloadAsync(string url,CancellationToken token) {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(6));token=timeout.Token;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0)
            throw new InvalidDataException("Invalid public artwork URL");
        var addresses=await Dns.GetHostAddressesAsync(uri.DnsSafeHost,token);
        if(addresses.Length==0||addresses.Any(a=>!PublicAddress(a)))throw new InvalidDataException("Non-public artwork host");
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(6)};
        using var reply=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);reply.EnsureSuccessStatusCode();
        if(reply.Content.Headers.ContentLength>2_000_000)throw new InvalidDataException("Artwork too large");
        using var input=await reply.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();
        byte[] buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>2_000_000)throw new InvalidDataException("Artwork too large");output.Write(buffer,0,count);}
        return output.ToArray();
    }
}
