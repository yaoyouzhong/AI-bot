using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

internal static class MediaPlayerSelfTest
{
    // Read-only client diagnostics; does not start a bridge, claim serial ports,
    // write a profile, control players or assert device/client acceptance.
    internal static async Task LiveAsync(string output)
    {
        using var music=new NowPlayingService();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var samples=new List<object>();
        for(int i=0;i<12;i++){
            await music.RefreshAsync(stop.Token);
            samples.Add(new{at=DateTimeOffset.UtcNow,music=music.Snapshot,diagnostic=music.Diagnostic});
            if(i<11)await Task.Delay(500,stop.Token);
        }
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{acceptance="diagnostics only; verify against actual playback and device display",samples},JsonDefaults.Options));
        Console.WriteLine("MEDIA_LIVE_DIAGNOSTICS_WRITTEN "+output);
    }
    internal static async Task RunAsync()
    {
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        var song=new MusicSnapshot("曲目","歌手","专辑",true,12,240,DateTimeOffset.UnixEpoch);
        var paused=new MediaPlaybackSource("chrome.exe","video",song with {Playing=false},"smtc",true);
        var spotify=new MediaPlaybackSource("Spotify.exe","song",song,"smtc");
        var apple=new MediaPlaybackSource("AppleInc.AppleMusic","apple",song,"smtc");
        Check(MediaPlaybackSource.Select([paused,spotify],"")==spotify,"Paused system default hides another playing app");
        Check(MediaPlaybackSource.Select([spotify with {SystemCurrent=true},apple],apple.Source)!.Source==spotify.Source,"Playing current session did not retain priority");
        Check(MediaPlaybackSource.Select([spotify,apple],apple.Source)==apple,"Multiple playing sources oscillate");
        Check(MediaPlaybackSource.Select([spotify with {Music=song with {Playing=false}},apple],spotify.Source)==apple,"Previous paused source hides a playing source");
        Check(MediaPlaybackSource.Select([spotify with {Music=song with {Title=""}}],"") is null,"Empty metadata chosen");
        Check(MediaPlaybackSource.Name("AppleInc.AppleMusic_abc") == "Apple Music"&&MediaPlaybackSource.Name("org.videolan.vlc")=="VLC","Source labels failed");
        Check(NowPlayingService.ProjectClock(song with {PlaybackRate=2},1).ElapsedSeconds==14,"Playback rate not applied");
        Check(NowPlayingService.ProjectClock(song with {Playing=false,PlaybackRate=2},1).ElapsedSeconds==12,"Paused playback advanced");
        Check(!NowPlayingService.ProjectClock(song,4).TimelineAvailable,"Stale samples fabricated a clock");

        const long address=0x200000,heap=0x300000;
        byte[] header=new byte[0x74];var strings=new Dictionary<long,byte[]>();
        void Text(int offset,string value){byte[] bytes=Encoding.UTF8.GetBytes(value);header.AsSpan(offset,24).Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset+0x10,4),(uint)bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset+0x14,4),(uint)(bytes.Length<=15?15:bytes.Length));
            if(bytes.Length<=15)bytes.CopyTo(header,offset);else{long ptr=heap+offset*256;BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(offset,4),(uint)ptr);strings[ptr]=bytes;}}
        Text(0,"长长的中文歌曲名字与实际QQ播放器标题");Text(0x18,"歌手 A / 歌手 B");Text(0x30,"Album");Text(0x48,"http://y.gtimg.cn/music/photo_new/album.jpg");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x60,4),42);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x68,4),240000);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x6c,4),12000);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x70,4),1);
        byte[] Read(long at,int count)=>at==address?header.ToArray():strings[at].Take(count).ToArray();
        var qq=QqMusicPlaybackReader.ReadCurrent(Read,address)!;
        Check(qq.Title.Contains("中文")&&qq.Artist=="歌手 A / 歌手 B"&&qq.Album=="Album"&&qq.Elapsed==12&&qq.Playing,"QQ inline/heap UTF-8 fields or millisecond clock lost");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x70,4),0);
        Check(!QqMusicPlaybackReader.ReadCurrent(Read,address)!.Playing,"QQ pause lost");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x70,4),3);
        Check(QqMusicPlaybackReader.ReadCurrent(Read,address) is null,"Buffering guessed as playing/paused");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x70,4),1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x6c,4),500000);
        Check(QqMusicPlaybackReader.ReadCurrent(Read,address) is null,"Invalid QQ progress accepted");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x6c,4),12000);
        int reads=0;
        Check(QqMusicPlaybackReader.ReadCurrent((at,n)=>{var b=Read(at,n);if(at==address&&++reads==2)b[0x60]++;return b;},address) is null,"Song transition combined different identities");
        uint oldLength=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x10,4));header[0x12]=1;
        bool rejected=false;try{QqMusicPlaybackReader.ReadCurrent(Read,address);}catch(IOException){rejected=true;}Check(rejected,"Unbounded QQ string read");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x10,4),oldLength);
        var list=new List<MediaPlaybackSource>{new("QQMusic.exe","old",song with {Title="previous song"},"smtc",true, CoverUrl:"https://y.gtimg.cn/old.jpg")};
        MediaPlaybackSource.AddQq(list,qq,DateTimeOffset.UnixEpoch,"native");
        Check(list.Count==1&&list[0].Source=="qqmusic-native"&&list[0].Music.Title==qq.Title&&list[0].CoverUrl==qq.CoverUrl,"QQ transition retained old title or cover");
        list=[new("QQMusic.exe","matching",qq.Snapshot(DateTimeOffset.UnixEpoch) with {TimelineAvailable=false},"smtc",true)];
        MediaPlaybackSource.AddQq(list,qq,DateTimeOffset.UnixEpoch,"native");
        Check(list.Count==1&&list[0].SystemCurrent&&list[0].Music.TimelineAvailable&&list[0].Music.ElapsedSeconds==12,"QQ native fallback did not enrich its exact SMTC song");
        list=[new("QQMusic.exe","matching",qq.Snapshot(DateTimeOffset.UnixEpoch) with {ElapsedSeconds=15},"smtc",true)];
        MediaPlaybackSource.AddQq(list,qq,DateTimeOffset.UnixEpoch,"native");
        Check(list[0].Music.ElapsedSeconds==15,"QQ native clock overwrote valid system clock");

        byte[] pattern=QqMusicPlaybackReader.Signature.Split(' ').Select(s=>s=="?"?(byte)0:Convert.ToByte(s,16)).ToArray();
        const uint preferred=0x10000000,structure=preferred+6000;
        foreach(var (offset,value) in new[]{(1,structure),(6,structure+0x28),(12,structure+0x2c),(16,15u),(21,structure+0x18),
            (26,structure+0x40),(32,structure+0x44),(36,15u),(41,structure+0x30)})BinaryPrimitives.WriteUInt32LittleEndian(pattern.AsSpan(offset,4),value);
        var code=new byte[256];pattern.CopyTo(code,20);
        Check(QqMusicPlaybackReader.Locate(code,preferred,10000)==6000,"QQ PE address did not relocate to its module RVA");
        Check(QqMusicPlaybackReader.Locate(code,preferred,6050) is null,"QQ structure escaped image bounds");
        BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(20+6,4),structure+0x10);
        Check(QqMusicPlaybackReader.Locate(code,preferred,10000) is null,"QQ initializer treated the next string size as the title size");
        pattern.CopyTo(code,20);
        pattern.CopyTo(code,140);Check(QqMusicPlaybackReader.Locate(code,preferred,10000) is null,"Ambiguous QQ signature selected an arbitrary address");

        var first=new TaskCompletionSource<byte[]?>();var second=new TaskCompletionSource<byte[]?>();int downloads=0;
        using var artwork=new QqMusicArtwork((uri,_)=>++downloads==1?first.Task:second.Task);
        Check(artwork.Read("A","http://y.gtimg.cn/a.jpg",CancellationToken.None) is null,"Pending cover blocked metadata");
        Check(artwork.Read("B","https://y.gtimg.cn/b.jpg",CancellationToken.None) is null,"New track reused old cover");
        first.SetResult([1]);await Task.Yield();Check(artwork.Read("B","https://y.gtimg.cn/b.jpg",CancellationToken.None) is null,"Abandoned cover replaced current track");
        second.SetResult([2]);await Task.Delay(10);Check(artwork.Read("B","https://y.gtimg.cn/b.jpg",CancellationToken.None)!.SequenceEqual(new byte[]{2}),"Current cover lost");
        Check(downloads==2,"Unchanged cover downloaded repeatedly");
        foreach(string url in new[]{"file:///a","https://127.0.0.1/a","https://y.gtimg.cn.example.org/a","https://u:p@y.gtimg.cn/a","https://y.gtimg.cn:8443/a"})Check(QqMusicArtwork.NormalizeUrl(url) is null,"Invalid cover origin accepted");
        Check(QqMusicArtwork.NormalizeUrl("http://y.gtimg.cn/a")!.Scheme=="https","Public QQ cover not upgraded to HTTPS");
        var now=DateTimeOffset.FromUnixTimeMilliseconds(1791453745000);
        var browser=new BrowserMusicArtwork((_,_)=>throw new Exception("This test must not fetch artwork"),()=>now);
        string BrowserPacket(bool playing=true,bool ended=false,double duration=240,long? stamp=null,string tab="browser:1")=>JsonSerializer.Serialize(new {
            title="网页歌曲",artist="网页歌手",album="网页专辑",source="music.example.org",sessionId=tab,playing,ended,
            elapsedSeconds=12,durationSeconds=duration,playbackRate=2,sampleTimeMs=stamp??now.ToUnixTimeMilliseconds(),artwork=Array.Empty<string>()});
        Check(await browser.AcceptAsync(BrowserPacket(),CancellationToken.None),"Browser playback without artwork rejected");
        now=now.AddSeconds(1);
        var web=browser.PlaybackSources().Single();Check(web.Music.Title=="网页歌曲"&&web.Music.ElapsedSeconds==14&&web.Music.Playing,"Browser metadata/rate-aware progress not delivered");
        Check(await browser.AcceptAsync(BrowserPacket(false),CancellationToken.None),"Browser pause rejected");
        now=now.AddSeconds(1);Check(browser.PlaybackSources().Single().Music.ElapsedSeconds==12,"Browser pause continued advancing");
        Check(!await browser.AcceptAsync(BrowserPacket(stamp:now.AddSeconds(-2).ToUnixTimeMilliseconds()),CancellationToken.None)&&!browser.PlaybackSources().Single().Music.Playing,"Out-of-order packet replaced a newer pause");
        Check(await browser.AcceptAsync(BrowserPacket(duration:0),CancellationToken.None)&&!browser.PlaybackSources().Single().Music.TimelineAvailable,"Live-stream duration fabricated");
        Check(await browser.AcceptAsync(BrowserPacket(ended:true),CancellationToken.None)&&browser.PlaybackSources().Length==0,"Ended browser source retained");
        Check(!await browser.AcceptAsync(BrowserPacket(stamp:now.AddMilliseconds(-500).ToUnixTimeMilliseconds()),CancellationToken.None)&&browser.PlaybackSources().Length==0,"Late packet revived an ended source");
        Check(!await browser.AcceptAsync(BrowserPacket(stamp:now.AddSeconds(-10).ToUnixTimeMilliseconds()),CancellationToken.None),"Delayed browser packet reset current clock");
        await browser.AcceptAsync(BrowserPacket(),CancellationToken.None);now=now.AddSeconds(4);Check(browser.PlaybackSources().Length==0,"Closed/stalled browser tab stayed active");
        using var cover=new Bitmap(640,360);using var coverBytes=new MemoryStream();cover.Save(coverBytes,System.Drawing.Imaging.ImageFormat.Png);
        var scoped=new BrowserMusicArtwork((_,_)=>Task.FromResult(coverBytes.ToArray()),()=>now);
        string CoverPacket(string tab,string album)=>JsonSerializer.Serialize(new{title="Same title",artist="Same artist",album,source="music.example.org",sessionId=tab,playing=true,ended=false,
            elapsedSeconds=12,durationSeconds=240,playbackRate=1,sampleTimeMs=now.ToUnixTimeMilliseconds(),artwork=new[]{"https://images.example.org/a.png"}});
        await scoped.AcceptAsync(CoverPacket("browser:1","Album A"),CancellationToken.None);
        Check(scoped.Find("Same title","Same artist","browser-companion:music.example.org:browser:1","Album A") is not null&&
            scoped.Find("Same title","Same artist","browser-companion:music.example.org:browser:1","Album B") is null&&
            scoped.Find("Same title","Same artist","browser-companion:music.example.org:browser:2","Album A") is null,"Artwork leaked between albums or tabs");
        using var music=new NowPlayingService();music.ApplySample(qq.Snapshot(DateTimeOffset.UnixEpoch),qq.Identity,null);
        var status=new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",null),new("idle",null),Music:music.Snapshot);
        var small=DeviceStatusFrame.Create(status)["data"]!["music"]!;
        using var big=JsonDocument.Parse(Tab5Protocol.Snapshot(status,"aabbccddeeff","test",1));
        Check(small["title"]!.GetValue<string>()==big.RootElement.GetProperty("data").GetProperty("music").GetProperty("title").GetString()&&
            small["durationSeconds"]!.GetValue<double>()==240,"Player data differs between devices");
        Console.WriteLine("MEDIA_PLAYERS_SELF_TEST_OK playing/current/stable-source arbitration; QQ bounded inline/heap metadata, clock/pause/buffering/transition/relocation; exact-song merge, asynchronous cover ownership/origin; rate/pause/staleness; unchanged dual-device protocol; synthetic sources only");
    }
}
