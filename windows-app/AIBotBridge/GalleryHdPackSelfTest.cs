using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryHdPackSelfTest
{
    private sealed class InlineProgress(Action<int> action):IProgress<int>{public void Report(int value)=>action(value);}
    internal static void RunUi(string output,string zip){
        Directory.CreateDirectory(output);
        var handler=new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("[]")})};
        using var service=new UpdateService(handler);using var form=new GalleryPackForm(service,_=>{},()=>zip);
        form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;Exception? failure=null;
        form.Shown+=async(_,_)=>{try{
            var button=(Button)form.Controls.Find("gallery-import",true).Single();var status=(Label)form.Controls.Find("gallery-state",true).Single();
            async Task Wait(Func<bool> predicate){var end=DateTime.UtcNow.AddMinutes(5);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(status.Text);await Task.Delay(10);}}
            await Wait(()=>button.Enabled);button.PerformClick();await Wait(()=>button.Enabled&&status.Text.Contains("已启用"));
            if(form.OperationError is not null||!form.Controls.Find("painting-hd",true).Single().Text.Contains("高清画质"))throw new Exception("HD import UI readback failed");
            using var capture=new Bitmap(form.Width,form.Height);form.DrawToBitmap(capture,new(Point.Empty,form.Size));capture.Save(Path.Combine(output,"hd-import-complete.png"));
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);if(failure is not null)throw failure;Console.WriteLine("HD_IMPORT_UI_OK button -> install -> active status");
    }
    internal static async Task Run(string output,string delivery){
        Directory.CreateDirectory(output);string root=Path.Combine(output,"installed");
        void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        byte[] jpeg;using(var image=new Bitmap(32,32)){using var g=Graphics.FromImage(image);g.Clear(Color.Blue);using var m=new MemoryStream();image.Save(m,System.Drawing.Imaging.ImageFormat.Jpeg);jpeg=m.ToArray();}
        byte[] standard=GalleryUpdateSelfTest.Package("painting","2026.10.16","高清测试作品");
        byte[] preview;using(var a=new ZipArchive(new MemoryStream(standard)))using(var stream=a.GetEntry("work.jpg")!.Open()){using var m=new MemoryStream();stream.CopyTo(m);preview=m.ToArray();}
        string previewHash=Convert.ToHexString(SHA256.HashData(preview));
        string hash=Convert.ToHexString(SHA256.HashData(jpeg));
        var index=new GalleryDesktopImages.Index(1,new(){{"source",new("作品/a.jpg",hash,32,32)}},[new("painting","fixture","one","work.jpg",previewHash,32,32,[new("source",[0,0,32,32],[0,0,32,32],0)])]);
        string Package(string file,string? extra=null,bool corrupt=false,bool mismatch=false){
            var files=new Dictionary<string,byte[]>{{"standard.zip",mismatch?GalleryUpdateSelfTest.Package("calligraphy","2026.10.16","不匹配"):standard},{"catalog.json",Encoding.UTF8.GetBytes("{\"schema\":1,\"category\":\"painting\",\"edition\":\"test\",\"works\":[{}]}")},{"desktop-index.relative.json",JsonSerializer.SerializeToUtf8Bytes(index,JsonDefaults.Options)},{"作品/a.jpg",jpeg}};
            if(extra is not null)files[extra]=jpeg;
            string sums=string.Join("\n",files.Select(p=>Convert.ToHexString(SHA256.HashData(p.Value))+"  "+p.Key));
            if(corrupt)files["作品/a.jpg"]=[1,2,3];
            using var a=ZipFile.Open(file,ZipArchiveMode.Create);foreach(var p in files){using var st=a.CreateEntry("pack/"+p.Key).Open();st.Write(p.Value);}using(var st=a.CreateEntry("pack/SHA256SUMS.txt").Open())st.Write(Encoding.UTF8.GetBytes(sums));return file;
        }
        var good=Package(Path.Combine(output,"good.zip"));var installed=GalleryHdPack.Install(good,root);string old=GalleryHdPack.Current("painting",root)!;
        var work=new Tab5Gallery.Artwork("fixture","painting","test","a","test","test",["work.jpg"],"one");var page=new Tab5Gallery.BrowserPage(work,null,preview);
        using(var image=(await new GalleryDesktopImages(Path.Combine(old,"desktop-originals.json")).LoadAsync(page,0,default))?.Image)Check(image is not null,"Imported relative source not decoded");
        int n=0;foreach(string bad in new[]{"../escape.jpg","C:/escape.jpg","作品/A.jpg","aux.jpg","作品/a.jpg:stream"}){
            try{GalleryHdPack.Install(Package(Path.Combine(output,$"bad{n++}.zip"),bad),root);throw new Exception("Unsafe archive accepted: "+bad);}catch(InvalidDataException){}
            Check(GalleryHdPack.Current("painting",root)==old,"Bad input replaced good import");
        }
        try{GalleryHdPack.Install(Package(Path.Combine(output,"corrupt.zip"),corrupt:true),root);throw new Exception("Bad hash accepted");}catch(InvalidDataException){}
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{GalleryHdPack.Install(good,root,cancellation:cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(GalleryHdPack.Current("painting",root)==old&&Directory.GetDirectories(root).Length==2,"Failure leaked staged material or changed current");
        try{GalleryHdPack.Install(Package(Path.Combine(output,"mismatch.zip"),mismatch:true),root);throw new Exception("Mismatched embedded gallery accepted");}catch(InvalidDataException){}
        using(var cancel=new CancellationTokenSource()){
            try{GalleryHdPack.Install(good,root,new InlineProgress(v=>{if(v==98)cancel.Cancel();}),cancel.Token);throw new Exception("Late cancel ignored");}catch(OperationCanceledException){}
        }
        string pointer=Path.Combine(root,"painting.current");File.SetAttributes(pointer,FileAttributes.ReadOnly);
        try{try{GalleryHdPack.Install(good,root);throw new Exception("Expected pointer failure");}catch(UnauthorizedAccessException){}}
        finally{File.SetAttributes(pointer,FileAttributes.Normal);}
        Check(GalleryHdPack.Current("painting",root)==old&&GalleryPack.Installed("painting",Path.Combine(root,"standard-gallery"))?.Version=="2026.10.16","Rollback changed installed gallery");
        // Exercise default loader hot activation, replacement, and fallback within the isolated public self-test profile.
        var live=new GalleryDesktopImages();GalleryHdPack.Install(good);
        using(var image=(await live.LoadAsync(page,0,default))?.Image)Check(image is not null,"Default loader did not activate imported content");
        var replacement=GalleryHdPack.Install(good);File.Delete(Path.Combine(replacement.Directory,"作品","a.jpg"));
        Check(await live.LoadAsync(page,0,default) is null,"Replacement/missing source did not fall back");
        Console.WriteLine("HD_IMPORT_FIXTURES_OK relative paths, hashes, traversal, collisions, cancellation, replacement, fallback");
        if(delivery.EndsWith("fixture-only",StringComparison.Ordinal))return;
        foreach(string category in new[]{"Painting","Calligraphy"}){
            var result=GalleryHdPack.Install(Path.Combine(delivery,$"AI-bot-Gallery-HD-{category}-2026.10.16.zip"),root);
            Check(result.Works==366,"Actual work inventory differs");
            Check(GalleryPack.Installed(result.Category,Path.Combine(root,"standard-gallery"))?.Works==366,"Full HD package failed to install standard catalogue");
            var mapped=JsonSerializer.Deserialize<GalleryDesktopImages.Index>(File.ReadAllBytes(Path.Combine(result.Directory,"desktop-originals.json")),JsonDefaults.Options)!;
            Check(mapped.Sources.Values.All(s=>File.Exists(s.Path)&&s.Path.StartsWith(result.Directory)),"Portable paths unresolved");
            Console.WriteLine($"HD_IMPORT_ACTUAL_OK {category} works={result.Works} sources={mapped.Sources.Count} pages={mapped.Pages.Length}");
        }
        Check(GalleryHdPack.Installed("painting",root)?.Works==366&&GalleryHdPack.Installed("calligraphy",root)?.Works==366,"Categories overwrote each other");
    }
}
