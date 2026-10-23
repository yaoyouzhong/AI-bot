using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryDesktopImagesSelfTest
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    internal static async Task Run(string output,string indexPath,string packs){
        Directory.CreateDirectory(output);string original=Path.Combine(output,"synthetic-original.png"),index=Path.Combine(output,"synthetic-index.json");
        using(var image=new Bitmap(2400,1600)){using var g=Graphics.FromImage(image);g.Clear(Color.Red);g.FillRectangle(Brushes.Lime,1200,0,1200,1600);image.Save(original,ImageFormat.Png);}
        byte[] preview;using(var image=new Bitmap(1280,720)){using var stream=new MemoryStream();image.Save(stream,ImageFormat.Jpeg);preview=stream.ToArray();}
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),previewHash=Convert.ToHexString(SHA256.HashData(preview));
        var work=new Tab5Gallery.Artwork("test","painting","test","author","test:work","test",["test.jpg"],"test:work");
        var page=new Tab5Gallery.BrowserPage(work,null,preview);
        var source=new GalleryDesktopImages.Source(original,hash,2400,1600);
        var mapped=new GalleryDesktopImages.Page("painting","test","test:work","test.jpg",previewHash,1200,1600,[new("source",[1200,0,1200,1600],[0,0,1200,1600],0)]);
        void Write(GalleryDesktopImages.Page mapping,GalleryDesktopImages.Source? replacement=null)=>File.WriteAllText(index,JsonSerializer.Serialize(new GalleryDesktopImages.Index(1,new(){{"source",replacement??source}},[mapping]),JsonDefaults.Options));
        Write(mapped);var reader=new GalleryDesktopImages(index);
        using(var result=(await reader.LoadAsync(page,0,default))?.Image){Check(result is not null&&result.Size==new Size(1200,1600)&&result.GetPixel(600,800).G>240,"Original crop or native resolution lost");}
        Write(mapped with{Width=2400,Parts=[new("source",[1200,0,1200,1600],[0,0,1200,1600],0),new("source",[0,0,1200,1600],[1200,0,1200,1600],0)]});
        using(var result=(await new GalleryDesktopImages(index).LoadAsync(page,0,default))?.Image){Check(result is not null&&result.GetPixel(600,800).G>240&&result.GetPixel(1800,800).R>240,"Grouped artwork ordering wrong");}
        Write(mapped with{Width=2400,Parts=[new("source",[0,0,2400,1600],[0,0,2400,1600],180)]});
        using(var result=(await new GalleryDesktopImages(index).LoadAsync(page,0,default))?.Image){Check(result is not null&&result.GetPixel(600,800).G>240,"Original rotation wrong");}
        Check(await new GalleryDesktopImages(Path.Combine(output,"missing-index.json")).LoadAsync(page,0,default) is null,"Missing index did not fall back");
        Write(mapped,source with{Path=original+".missing"});Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Missing original did not fall back");
        Write(mapped,source with{Sha256=new string('0',64)});Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Wrong original hash did not fall back");
        Write(mapped with{PreviewSha256=new string('0',64)});Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Changed gallery page did not fall back");
        Write(mapped with{Parts=[new("source",[-1,0,1200,1600],[0,0,1200,1600],0)]});Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Invalid crop did not fall back");
        File.WriteAllText(index,"broken");Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Malformed index did not fall back");
        string broken=Path.Combine(output,"broken.png");File.WriteAllText(broken,new string('x',64));Write(mapped,source with{Path=broken,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(broken)))});Check(await new GalleryDesktopImages(index).LoadAsync(page,0,default) is null,"Corrupt decoder input did not fall back");
        Write(mapped);using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await new GalleryDesktopImages(index).LoadAsync(page,0,cancelled.Token);throw new InvalidDataException("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine("DESKTOP_HD_FALLBACK_OK crop/group/rotation; absent/corrupt/mismatched/bad bounds; cancellation");
        var actual=JsonSerializer.Deserialize<GalleryDesktopImages.Index>(File.ReadAllBytes(indexPath),JsonDefaults.Options)!;
        var gallery=new Tab5Gallery(Path.Combine(output,"empty"),packs);var originals=new GalleryDesktopImages(indexPath);
        var samples=actual.Pages.Where(p=>p.Parts.Length>1).Take(3)
            .Concat(actual.Pages.OrderByDescending(p=>(long)p.Width*p.Height).Take(3))
            .Concat(actual.Pages.Where(p=>p.Id=="expansion-npm-zixu").Take(3))
            .Concat(actual.Pages.Where(p=>p.Id=="calligraphy-04").Take(3))
            .Concat(actual.Pages.Where((_,i)=>i%97==0)).DistinctBy(p=>p.Category+"/"+p.Frame).ToArray();
        foreach(var mapping in samples){
            var catalog=gallery.Browse(mapping.Category);var w=catalog.Works.Single(w=>w.Id==mapping.Id);int frame=Array.IndexOf(w.Frames,mapping.Frame);
            var p=gallery.BrowsePage(mapping.Category,mapping.Id,frame);var result=await originals.LoadAsync(p,frame,default);Check(result is not null,"Actual HD failed: "+mapping.Frame+" "+originals.LastFailure);
            using var bitmap=result!.Image;Check((long)bitmap.Width*bitmap.Height<=16010000&&bitmap.Width<=8192&&bitmap.Height<=8192,"Decode memory bounds exceeded");
            if(mapping==samples[0]||mapping.Id=="expansion-npm-zixu"&&frame==0)bitmap.Save(Path.Combine(output,mapping.Frame+".png"),ImageFormat.Png);
        }
        Console.WriteLine($"DESKTOP_HD_ACTUAL_OK {samples.Length} pages including huge scroll, cropped detail, grouped panels, album; bounded decode; device previews unchanged");
    }
}
