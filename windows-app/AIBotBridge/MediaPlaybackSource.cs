using Windows.Storage.Streams;
namespace AIBotBridge;

internal sealed record MediaPlaybackSource(string Source,string Identity,MusicSnapshot Music,string Diagnostic,
    bool SystemCurrent=false,IRandomAccessStreamReference? Thumbnail=null,string? CoverUrl=null,string? CompactTitle=null)
{
    internal string ResourceKey=>Source+"\n"+Identity+"\n"+Music.Title+"\n"+Music.Artist+"\n"+Music.Album;
    internal static void AddQq(List<MediaPlaybackSource> sources,QqMusicPlaybackReader.Sample? qq,DateTimeOffset sampledAt,string diagnostic)
    {
        if(qq is null)return;
        for(int i=sources.Count-1;i>=0;i--)if(QqMusicPlaybackReader.IsQq(sources[i].Source)) {
            var candidate=sources[i];var song=candidate.Music;
            if(!qq.Matches(song.Title,song.Artist,song.Album)){sources.RemoveAt(i);continue;}
            sources[i]=candidate with {Music=song.TimelineAvailable?song:qq.Snapshot(sampledAt),
                CoverUrl=qq.CoverUrl,Diagnostic="smtc + "+diagnostic};
        }
        if(!sources.Any(s=>QqMusicPlaybackReader.IsQq(s.Source)))sources.Add(new("qqmusic-native",qq.Identity,qq.Snapshot(sampledAt),diagnostic,CoverUrl:qq.CoverUrl));
    }
    internal static MediaPlaybackSource? Select(IReadOnlyList<MediaPlaybackSource> sources,string previous)
    {
        var valid=sources.Where(s=>!string.IsNullOrWhiteSpace(s.Music.Title)).ToArray();
        var playing=valid.Where(s=>s.Music.Playing).ToArray();
        var choices=playing.Length>0?playing:valid;
        return choices.FirstOrDefault(s=>s.SystemCurrent)??choices.FirstOrDefault(s=>s.Source==previous)??choices.FirstOrDefault();
    }
    internal static string Name(string source)
    {
        if(source.StartsWith("browser-companion:",StringComparison.Ordinal))return "浏览器 · "+source.Split(':')[1];
        string s=source.ToLowerInvariant();
        foreach(var (id,name) in new[]{("qqmusic","QQ音乐"),("cloudmusic","网易云音乐"),("netease","网易云音乐"),("spotify","Spotify"),("applemusic","Apple Music"),("appleinc.applemusic","Apple Music"),("kugou","酷狗音乐"),("kwmusic","酷我音乐"),("kuwo","酷我音乐"),("foobar","foobar2000"),("vlc","VLC"),("potplayer","PotPlayer"),("msedge","Microsoft Edge"),("chrome","Google Chrome"),("firefox","Firefox"),("zunemusic","Windows 媒体播放器"),("wmplayer","Windows Media Player")})
            if(s.Contains(id,StringComparison.Ordinal))return name;
        return source;
    }
}
