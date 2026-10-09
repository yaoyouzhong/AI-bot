using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Windows.Media.Control;

namespace AIBotBridge;

internal static class NeteaseMusicSelfTest
{
    internal static void Run()
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        byte[] json=Encoding.UTF8.GetBytes("""
            {"list":[{"id":"42","track":{"name":"Ballade Pour Adeline","transNames":["水边的阿狄丽娜"],"alias":["水边的阿狄丽娜"],"artists":[{"name":"Richard Clayderman"}],"album":{"name":"Piano"}}}]}
            """);
        var track=NeteaseTrackMetadata.Parse(json,"42")!;
        Check(track.DisplayTitle=="Ballade Pour Adeline\n水边的阿狄丽娜"&&track.CompactTitle=="水边的阿狄丽娜","Translation lost or duplicated");
        Check(track.Matches("Ballade Pour Adeline","Richard Clayderman","")&&track.Matches("水边的阿狄丽娜","Richard Clayderman","Piano"),"Correct native/SMTC song rejected");
        Check(!track.Matches("Other","Richard Clayderman","")&&!track.Matches(track.Name,"Other","")&&!track.Matches(track.Name,track.Artist,"Live"),"Different song/artist/album merged");
        Check(NeteaseTrackMetadata.Parse(json,"43") is null,"Previous track identity reused");
        Check(NeteasePlaybackReader.ValidClock("42",0,158,1)&&NeteasePlaybackReader.ValidClock("42",63,158,2),"Song beginning or pause rejected");
        Check(!NeteasePlaybackReader.ValidClock("",3,158,1)&&!NeteasePlaybackReader.ValidClock("42",-1,158,1)&&
            !NeteasePlaybackReader.ValidClock("42",double.NaN,158,1)&&!NeteasePlaybackReader.ValidClock("42",3,double.PositiveInfinity,1)&&
            !NeteasePlaybackReader.ValidClock("42",170,158,1)&&!NeteasePlaybackReader.ValidClock("42",3,158,3),"Invalid/buffering clock accepted");
        byte[] Pattern(string s)=>s.Split(' ').Select(v=>v=="?"?(byte)0:Convert.ToByte(v,16)).ToArray();
        var text=new byte[400];Pattern(NeteasePlaybackReader.AudioSignature).CopyTo(text,10);Pattern(NeteasePlaybackReader.ClockSignature).CopyTo(text,150);
        BinaryPrimitives.WriteInt32LittleEndian(text.AsSpan(13,4),6000-4096-10-7);
        BinaryPrimitives.WriteInt32LittleEndian(text.AsSpan(154,4),7000-4096-150-8);
        Check(NeteasePlaybackReader.Locate(text,4096,10000)==new NeteasePlaybackReader.Layout(6000,7000),"RIP displacement/layout mapping failed");
        Check(NeteasePlaybackReader.Locate(text,4096,6500) is null,"Out-of-image clock accepted");
        Pattern(NeteasePlaybackReader.AudioSignature).CopyTo(text,260);
        Check(NeteasePlaybackReader.Locate(text,4096,10000) is null,"Ambiguous instruction signature accepted");
        Check(NeteasePlaybackReader.FindUnique([1,2,3],"02 03")==1,"Signature ending at section boundary missed");
        using var music=new NowPlayingService();
        var sample=new MusicSnapshot(track.DisplayTitle,track.Artist,track.Album,true,63,158,DateTimeOffset.UtcNow){TimelineAvailable=true};
        music.ApplySample(sample,"native-track",null,compactTitle:track.CompactTitle);
        Check(music.Resources.Single(r=>r.Kind==BinaryResourceKind.TextBitmap).Data.SequenceEqual(NowPlayingService.RenderTextBitmap(track.CompactTitle,track.Artist)),"Small-screen translation bitmap lost");
        var status=new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",null),new("idle",null),Music:music.Snapshot);
        var small=DeviceStatusFrame.Create(status)["data"]!["music"]!;
        using var tab5=JsonDocument.Parse(Tab5Protocol.Snapshot(status,"aabbccddeeff","test",1));
        var large=tab5.RootElement.GetProperty("data").GetProperty("music");
        Check(small["elapsedSeconds"]!.GetValue<double>()==large.GetProperty("elapsedSeconds").GetDouble()&&
            small["durationSeconds"]!.GetValue<double>()==large.GetProperty("durationSeconds").GetDouble(),"Screens received different clocks");
        Check(large.GetProperty("album").GetString()=="Piano"&&music.Snapshot!.Album=="Piano","Album missing or shared metadata changed");
        Check(NowPlayingService.ProjectClock(sample,2).ElapsedSeconds==65,"Sampling delay not compensated");
        Check(NowPlayingService.ProjectClock(sample with {Playing=false},2).ElapsedSeconds==63&&
            NowPlayingService.ProjectClock(sample with {TimelineAvailable=false},2).ElapsedSeconds==63,"Paused or unknown clock advanced");
        Check(NowPlayingService.ProjectClock(sample,4).ElapsedSeconds==63&&!NowPlayingService.ProjectClock(sample,4).TimelineAvailable&&
            NowPlayingService.ProjectClock(sample,-1).ElapsedSeconds==63,"Stale or backward clock advanced");
        Check(NowPlayingService.ProjectClock(sample with {ElapsedSeconds=157},2).ElapsedSeconds==158,"Clock exceeded duration");
        long tick=10000;music.PlaybackClock=()=>tick;
        music.ApplySample(sample,"native-track",null,compactTitle:track.CompactTitle,sampledTick:tick-1000);
        Check(music.Snapshot!.ElapsedSeconds==64,"Cover processing delay not included");
        music.ApplySample(sample with {ElapsedSeconds=20,Playing=false},"native-track",null);
        tick+=1000;
        Check(music.Snapshot!.ElapsedSeconds==20,"Seek/pause reused old projected position");
        VerifyFastFrames(sample);
        status=status with {Music=sample with {TimelineAvailable=false}};
        Check(DeviceStatusFrame.Create(status)["data"]!["music"]!["durationSeconds"]!.GetValue<int>()==0,"Legacy screen fabricated an unknown clock");
        Console.WriteLine("NETEASE_MUSIC_SELF_TEST_OK identity/translation/compact-text/clock-validation/signature-bounds/dual-screen-wire");
    }

    private static void VerifyFastFrames(MusicSnapshot song)
    {
        string directory=Path.Combine(Path.GetTempPath(),"netease-wire-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var store=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));store.Pair("001122334455",@"USB\TEST");
            using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop(),()=>[],new Tab5CodexJournal(Path.Combine(directory,"draft.dat")));
            using var service=new Tab5Service(store,tasks);
            long tick=10000;service.PublicationClock=()=>tick;
            service.MusicCapture=()=>song;
            service.Publish(new(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",null),new("idle",null),Music:song));
            byte[] initial=service.CurrentFrame!;
            tick+=499;
            if(!ReferenceEquals(initial,service.CurrentFrame))throw new Exception("Music exceeded its publication cadence");
            tick++;song=song with {ElapsedSeconds=17,Playing=false};
            using var fresh=JsonDocument.Parse(service.CurrentFrame!);
            var data=fresh.RootElement.GetProperty("data").GetProperty("music");
            if(data.GetProperty("elapsedSeconds").GetDouble()!=17||data.GetProperty("playing").GetBoolean()||data.GetProperty("album").GetString()!="Piano")
                throw new Exception("Seek/pause/album waited for the tray tick");
            using var packed=JsonDocument.Parse(Tab5Service.PackTelemetry(service.CurrentFrame!));
            using var bytes=new MemoryStream(Convert.FromBase64String(packed.RootElement.GetProperty("payload").GetString()!));
            using var zip=new System.IO.Compression.ZLibStream(bytes,System.IO.Compression.CompressionMode.Decompress);
            using var restored=JsonDocument.Parse(zip);
            if(restored.RootElement.GetProperty("data").GetProperty("music").GetProperty("album").GetString()!="Piano")throw new Exception("Packed transport lost album");
            tick=15999;_ = service.CurrentFrame;
            tick=16000;
            if(service.CurrentFrame is not null)throw new Exception("Music renewed stale non-music state");
            Console.WriteLine("NETEASE_FAST_FRAME_OK 500ms publication; latest seek/pause; full album; lossless packed wire; stale state expires");
        } finally {Directory.Delete(directory,true);}
    }

    internal static async Task LiveAsync(string outputPath)
    {
        using var music=new NowPlayingService();
        var samples=new List<object>();
        int valid=0;
        for(int i=0;i<8;i++) {
            await music.RefreshAsync(CancellationToken.None);
            samples.Add(new {music=music.Snapshot,diagnostic=music.Diagnostic});
            if(music.Snapshot?.TimelineAvailable==true&&music.Diagnostic.Contains("netease-native"))valid++;
            if(i<7)await Task.Delay(1000);
        }
        await File.WriteAllTextAsync(outputPath,JsonSerializer.Serialize(samples,JsonDefaults.Options));
        Console.WriteLine("NETEASE_LIVE_READ_COMPLETE "+outputPath);
        // A natural song transition can put the last sample in buffering. Require
        // multiple real samples, not a fabricated valid clock during that transition.
        if(valid<3)throw new InvalidOperationException("Insufficient live native samples: "+music.Diagnostic);
    }
}
