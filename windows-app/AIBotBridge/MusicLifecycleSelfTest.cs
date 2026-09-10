namespace AIBotBridge;

internal static class MusicLifecycleSelfTest
{
    internal static void Run()
    {
        var service=new NowPlayingService();
        var song=new MusicSnapshot("Song","Artist","",true,3,120,DateTimeOffset.UtcNow);
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        service.ApplySample(song,"track",null);
        Require(service.Snapshot?.HasArtwork==false&&!service.Resources.Any(r=>r.Kind==BinaryResourceKind.MusicCover),"Missing artwork became black pixels");
        var art=Enumerable.Repeat((byte)120,PetAnimation.FrameBytes).ToArray();
        service.ApplySample(song,"track",art);
        Require(service.Snapshot?.HasArtwork==true&&service.Resources.Any(r=>r.Kind==BinaryResourceKind.MusicCover),"Late artwork did not refresh");
        service.ApplyEmpty();service.ApplyEmpty();
        Require(service.Snapshot?.Title=="Song"&&service.Snapshot.HasArtwork,"Transient empties erased music");
        service.ApplyEmpty();
        Require(service.Snapshot?.Title==""&&service.Snapshot.HasArtwork==false&&!service.Resources.Any(r=>r.Kind==BinaryResourceKind.MusicCover),"Ended playback retained artwork");
        Require(service.Resources.Single(r=>r.Kind==BinaryResourceKind.TextBitmap).Data.SequenceEqual(NowPlayingService.RenderTextBitmap("No Music","")),"Ended playback retained title pixels");
        service.ApplySample(song,"track",null);
        Require(service.Snapshot?.Title=="Song"&&!service.Snapshot.HasArtwork,"Same-track restart failed");
        var s=new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),Music:service.Snapshot);
        Require(DeviceStatusFrame.Create(s)["data"]?["music"]?["hasArtwork"]?.GetValue<bool>()==false,"Device missing-artwork state lost");
        Console.WriteLine("MUSIC_LIFECYCLE_SELF_TEST_OK missing/late-art/three-empties/stale-resources/restart/wire");
    }
}
