using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// The optional browser companion supplies current Media Session artwork. No
// browser history, account credentials or video identifiers are persisted.
internal sealed class BrowserMusicArtwork(Func<string,CancellationToken,Task<byte[]>>? fetch=null)
{
    private sealed record Cover(string Source,byte[] Bytes);
    private readonly Dictionary<string,Cover> _covers=[];
    private readonly object _sync=new();
    private readonly SemaphoreSlim _downloads=new(1,1);
    private readonly Func<string,CancellationToken,Task<byte[]>> _fetch=fetch??DownloadAsync;
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
    internal byte[]? Find(string title,string artist) {lock(_sync)return _covers.GetValueOrDefault(Key(title,artist))?.Bytes;}
    private static string Key(string title,string artist)=>title.Trim().Normalize()+"\n"+artist.Trim().Normalize();
    internal async Task<bool> AcceptAsync(string json,CancellationToken token) {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        string Text(string field)=>root.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
        string title=Text("title"),artist=Text("artist"),video=Text("youtubeVideoId");
        if(title.Length is 0 or >512||artist.Length>256)return false;
        var candidates=new List<string>();
        if(Regex.IsMatch(video,"^[A-Za-z0-9_-]{11}$")) {
            candidates.Add($"https://i.ytimg.com/vi/{video}/maxresdefault.jpg");
            candidates.Add($"https://i.ytimg.com/vi/{video}/hqdefault.jpg");
        }
        if(root.TryGetProperty("artwork",out var art)&&art.ValueKind==JsonValueKind.Array)
            candidates.AddRange(art.EnumerateArray().Take(8).Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).Where(v=>v.Length<=2048));
        candidates=candidates.Distinct().Take(10).ToList();
        if(candidates.Count==0)return false;
        string key=Key(title,artist),source=string.Join("\n",candidates);
        await _downloads.WaitAsync(token);
        try {
            lock(_sync)if(_covers.GetValueOrDefault(key)?.Source==source)return false;
            byte[]? best=null;int width=0;
            foreach(string url in candidates) {
                try {
                    var bytes=await _fetch(url,token);
                    if(bytes.Length is 0 or >2_000_000)continue;
                    using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream);
                    if(image.Width>4096||image.Height>4096||(long)image.Width*image.Height>16_777_216)continue;
                    if(image.Width>width){width=image.Width;best=bytes;}
                    if(width>=560)break;
                }catch(Exception ex) when(ex is HttpRequestException or IOException or ArgumentException){ }
            }
            if(best is null)return false; // Keep the last successful cover after a network failure.
            lock(_sync) {
                if(_covers.Count>=16&&!_covers.ContainsKey(key))_covers.Remove(_covers.Keys.First());
                _covers[key]=new(source,best);
            }
            return true;
        }finally{_downloads.Release();}
    }
    internal static bool PublicAddress(IPAddress ip) {
        if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();
        if(IPAddress.IsLoopback(ip))return false;
        byte[] b=ip.GetAddressBytes();
        return b.Length==4?b[0] is not (0 or 10 or 127)&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1]==168)&&b[0]<224:
            !ip.Equals(IPAddress.IPv6Any)&&!ip.IsIPv6LinkLocal&&!ip.IsIPv6Multicast&&(b[0]&0xfe)!=0xfc;
    }
    private static async Task<byte[]> DownloadAsync(string url,CancellationToken token) {
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
