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
        Require(covers.Jpeg.Width==336&&covers.Jpeg.Height==336,"Square JPEG changed artwork geometry");
        Require(covers.Legacy.Length==112*112*2&&covers.Tab5.Length==336*336*2,"Artwork sizes changed legacy payload");
        int sample=(168*336+168)*2;
        Require(covers.Tab5[sample]!=covers.Tab5[sample+2]||covers.Tab5[sample+1]!=covers.Tab5[sample+3],"Native artwork lost single-pixel detail");
        using(var landscape=new Bitmap(150,83)) {
            using(var graphics=Graphics.FromImage(landscape)) {
                graphics.Clear(Color.Lime);
                graphics.FillRectangle(Brushes.Red,0,0,20,83);
                graphics.FillRectangle(Brushes.Blue,130,0,20,83);
            }
            var video=NowPlayingService.RenderCoverImages(landscape);
            Require(video.Jpeg.Width==560&&video.Jpeg.Height==310,"JPEG landscape lost native aspect ratio");
            using(var jpegStream=new MemoryStream(video.Jpeg.Bytes))using(var decoded=new Bitmap(jpegStream)) {
                Require(decoded.Width==560&&decoded.Height==310&&decoded.GetPixel(20,155).R>230&&decoded.GetPixel(540,155).B>230,"JPEG cropped landscape or swapped colors");
            }
            ushort Pixel(byte[] pixels,int width,int x,int y)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan((y*width+x)*2));
            Require(video.SourceWidth==150&&video.SourceHeight==83,"Artwork source dimensions lost");
            Require(Pixel(video.Tab5,336,20,168)==0xf800&&Pixel(video.Tab5,336,316,168)==0x001f,"TAB5 cropped the sides of landscape artwork");
            Require(Pixel(video.Tab5,336,168,10)==0&&Pixel(video.Tab5,336,168,325)==0&&Pixel(video.Tab5,336,168,168)==0x07e0,"TAB5 artwork did not preserve its aspect ratio with letterboxing");
            Require(Pixel(video.Legacy,112,10,56)==0x07e0&&Pixel(video.Legacy,112,101,56)==0x07e0,"Legacy square crop changed");
            var videoStatus=new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),Music:song with {ArtworkWidth=video.SourceWidth,ArtworkHeight=video.SourceHeight});
            using var frame=System.Text.Json.JsonDocument.Parse(Tab5Protocol.Snapshot(videoStatus,"aabbccddeeff","test",1));
            var metadata=frame.RootElement.GetProperty("data").GetProperty("music");
            Require(metadata.GetProperty("artworkWidth").GetInt32()==150&&metadata.GetProperty("artworkHeight").GetInt32()==83,"TAB5 lost original artwork aspect ratio");
            Require(DeviceStatusFrame.Create(videoStatus)["data"]?["music"]?["artworkWidth"] is null,"TAB5-only artwork metadata grew legacy USB frames");
        }
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
        service.ApplySample(song,"track",art,covers.Tab5,covers.Jpeg);
        var modern=new Tab5Assets();modern.ObserveFirmware("0.2.80-ui");
        modern.Next(new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),Music:service.Snapshot));
        var jpegAsset=modern.Assets.Single(a=>a.Slot==2);
        Require(jpegAsset.Encoding=="jpeg"&&jpegAsset.Packed.SequenceEqual(covers.Jpeg.Bytes),"Modern firmware did not receive JPEG artwork");
        modern.Acknowledge(string.Join(",",modern.Ids.Select(id=>id??"-")));
        Require(!modern.HasPending&&modern.NextBatch(4).Length==0,"Acknowledged JPEG was resent");
        modern.ObserveFirmware("0.2.78-ui");modern.Next(new StatusSnapshot(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),Music:service.Snapshot));
        Require(modern.Assets.Single(a=>a.Slot==2).Encoding=="rle565","Old firmware received unsupported JPEG");
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
        Console.WriteLine("MUSIC_LIFECYCLE_SELF_TEST_OK landscape-fit/legacy-crop/native-detail/missing/late-art/three-empties/stale-resources/restart/wire");
    }
}
