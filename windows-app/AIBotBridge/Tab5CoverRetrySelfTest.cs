using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CoverRetrySelfTest
{
    internal static void Run() {
        long now=1000;int count=0;
        void Require(bool ok,string message){count++;if(!ok)throw new InvalidOperationException("Cover retry: "+message);}
        StatusSnapshot Snapshot(NowPlayingService.JpegCover jpeg)=>new(1,"",0,0,DateTimeOffset.UnixEpoch,new("idle",0),new("idle",0),
            Music:new MusicSnapshot("Test","","",true,0,1,DateTimeOffset.UnixEpoch){Tab5CoverJpeg=jpeg});
        NowPlayingService.JpegCover Jpeg(int width,int height) {
            using var bitmap=new Bitmap(width,height);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Blue);g.FillRectangle(Brushes.Red,0,0,width/2,height/2);}
            using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Jpeg);return new(stream.ToArray(),width,height);
        }
        var jpeg=Jpeg(64,32);var snapshot=Snapshot(jpeg);var sender=new Tab5Assets(()=>now);sender.ObserveFirmware("0.2.302");
        sender.Next(snapshot);sender.Acknowledge(string.Join(',',sender.Ids.Take(2).Concat(["-"])));
        int Drain() {
            int chunks=0;
            for(int i=0;i<1000;i++) {
                // Alternate status publication and USB/BLE batch consumption.
                var values=i%2==0?sender.NextBatch(1):sender.Next(snapshot) is {} v?new[]{v}:[];
                if(values.Length==0)break;
                chunks+=values.Count(v=>JsonSerializer.SerializeToElement(v,JsonDefaults.Options).GetProperty("slot").GetInt32()==2);
            }
            return chunks;
        }
        Drain();Require(sender.CoverRetryState.Passes==1&&!sender.HasPending,"first pass waits");
        now+=1999;Require(Drain()==0,"two-second wait");now++;
        Drain();Require(sender.CoverRetryState.Passes==2,"shared second pass");
        snapshot=Snapshot(jpeg with {Bytes=jpeg.Bytes.ToArray()});sender.Next(snapshot);
        Require(sender.CoverRetryState.Passes==2,"same bytes do not reset budget");
        now+=2000;Drain();Require(sender.CoverRetryState.Passes==3,"third pass bounded");
        Require(!sender.HasPending,"wait before fallback");now+=2000;Drain();
        var fallback=sender.Assets.Single(a=>a.Slot==2);
        Require(fallback.Encoding=="rle565"&&fallback.Width==64&&fallback.Height==32&&sender.CoverRetryState.Fallback,"same geometry fallback");
        using(var image=new Bitmap(new MemoryStream(jpeg.Bytes))) {
            var expected=Tab5Assets.Create(2,64,32,[1000],PetAssetImporter.EncodeRgb565(image));
            Require(expected.Packed.SequenceEqual(fallback.Packed),"pixel order and color match decoded JPEG");
        }
        sender.Acknowledge(string.Join(',',sender.Ids.Take(2).Concat([BinaryResourceProtocol.Crc32(jpeg.Bytes).ToString("x8")])));
        now+=2000;Drain();now+=2000;Drain();now+=2000;
        Require(!sender.HasPending&&Drain()==0&&sender.CoverRetryState.Passes==3,"late JPEG ack does not confirm or reset fallback");
        snapshot=Snapshot(Jpeg(80,40));sender.Next(snapshot);
        Require(sender.CoverRetryState.Passes<3&&!sender.CoverRetryState.Fallback,"new cover fresh budget");
        foreach(var cover in new[]{Jpeg(560,310),new NowPlayingService.JpegCover([1,2,3],64,32)}) {
            snapshot=Snapshot(cover);sender.Next(snapshot);Drain();now+=2000;Drain();now+=2000;Drain();now+=2000;Drain();
            var stopped=sender.Assets.Single(a=>a.Slot==2);
            Require(stopped.Encoding=="jpeg"&&stopped.Width==cover.Width&&!sender.HasPending,"wide or invalid JPEG retained without resize or endless retry");
        }
        Console.WriteLine($"Cover retry self-test passed ({count} checks).");
    }
}
