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
        using var source=new Bitmap(336,336);
        using(var graphics=Graphics.FromImage(source)) {
            graphics.Clear(Color.Red);
            for(int x=0;x<336;x+=2)graphics.FillRectangle(Brushes.Blue,x,0,1,336);
        }
        var covers=NowPlayingService.RenderCoverImages(source);
        Require(covers.Legacy.Length==112*112*2&&covers.Tab5.Length==336*336*2,"Artwork sizes changed legacy payload");
        int sample=(168*336+168)*2;
        Require(covers.Tab5[sample]!=covers.Tab5[sample+2]||covers.Tab5[sample+1]!=covers.Tab5[sample+3],"Native artwork lost single-pixel detail");
        service.ApplySample(song,"track",art,covers.Tab5);
        var largeReference=service.Snapshot!.Tab5CoverRgb565;
        var smallReference=service.Snapshot.CoverRgb565;
        service.ApplySample(song,"track",art.ToArray(),covers.Tab5.ToArray());
        Require(ReferenceEquals(largeReference,service.Snapshot!.Tab5CoverRgb565)&&ReferenceEquals(smallReference,service.Snapshot.CoverRgb565),"Unchanged art replaced cached source references");
        Require(service.Resources.Single(r=>r.Kind==BinaryResourceKind.MusicCover).Data.Length==112*112*2,"ESP8266 cover grew");
        var sender=new Tab5Assets();
        sender.Next(new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),Music:service.Snapshot));
        var asset=sender.Assets.Single(a=>a.Slot==2);
        Require(asset.Width==336&&asset.Height==336&&asset.Frames==1&&asset.Packed.Length<=524288,"TAB5 high-resolution artwork not selected");
        var restored=new List<byte>();
        for(int at=0;at<asset.Packed.Length;at+=4) {
            int count=asset.Packed[at]|asset.Packed[at+1]<<8;
            for(int n=0;n<count;n++){restored.Add(asset.Packed[at+2]);restored.Add(asset.Packed[at+3]);}
        }
        Require(restored.SequenceEqual(covers.Tab5),"Large artwork RLE lost pixels");
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
