using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryPackSelfTest
{
    internal static void RunPackages(string painting,string calligraphy,string output) {
        Directory.CreateDirectory(output);
        var gallery=new Tab5Gallery();
        foreach(string archive in new[]{painting,calligraphy}) {
            var pack=GalleryPack.Install(archive);
            for(int day=0;day<366;day++)foreach(string orientation in new[]{"landscape","portrait"}) {
                var request=JsonSerializer.SerializeToElement(new{category=pack.Category,date=new DateOnly(2028,1,1).AddDays(day).ToString("yyyy-MM-dd"),op="manifest",orientation},JsonDefaults.Options);
                var result=gallery.Handle(request,true,true);
                if(result.Status!=200)throw new Exception("Installed annual gallery unavailable");
            }
            Console.WriteLine($"PACK_INSTALLED_OK category={pack.Category} works={pack.Works} images={pack.Frames} annual=366 orientations=2");
        }
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var form=new GalleryPackForm();form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();
            if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,$"gallery-packs-{scale}.png"));form.Close();
        }
        Console.WriteLine("GALLERY_PACKS_REAL_OK isolated profile; all JPEG files validated; annual service after hot import; 1x/1.5x/2x UI captures");
    }
    internal static void Run() {
        string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"gallery-test-"+Guid.NewGuid().ToString("N"));
        string packs=Path.Combine(root,"packs"),legacy=Path.Combine(root,"legacy");Directory.CreateDirectory(root);
        static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
        byte[] Jpeg(int width=1280){using var bitmap=new Bitmap(width,720);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.Beige);using var stream=new MemoryStream();bitmap.Save(stream,ImageFormat.Jpeg);return stream.ToArray();}
        Dictionary<string,byte[]> Files(string category,string title="作品")=>new(){
            ["catalog.json"]=JsonSerializer.SerializeToUtf8Bytes(new[]{new Tab5Gallery.Artwork("test",category,title,"作者","https://example.invalid/work","CC0",["work.jpg"],"one",["work-portrait.jpg"])},JsonDefaults.Options),
            ["provenance.json"]="[]"u8.ToArray(),["selection.json"]="[]"u8.ToArray(),["LICENSE"]="fixture"u8.ToArray(),["THIRD_PARTY_NOTICES.md"]="fixture"u8.ToArray(),
            ["work.jpg"]=Jpeg(),["work-portrait.jpg"]=Jpeg()};
        string Archive(Dictionary<string,byte[]> files,string category="painting",Action<Dictionary<string,byte[]>>? change=null){
            var hashes=files.ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(p.Value)).ToLowerInvariant());
            files["PACK.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryPackManifest(1,"2026.10.07",category,1,2,hashes),JsonDefaults.Options);change?.Invoke(files);
            string path=Path.Combine(root,Guid.NewGuid()+".zip");File.WriteAllBytes(path,UpdateSelfTest.Zip(files));return path;
        }
        void Reject(string path){try{GalleryPack.Install(path,packs);}catch(Exception ex) when(ex is InvalidDataException or ArgumentException){return;}throw new Exception("Invalid gallery accepted");}
        var gallery=new Tab5Gallery(legacy,packs);
        (int Status,JsonElement Body) Manifest(string category){var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{category,date="2026-10-07",op="manifest",frame=0},JsonDefaults.Options),true,true);return(reply.Status,JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options));}
        Check(Manifest("painting").Status==404,"Absent gallery must be explained");
        Console.WriteLine("GALLERY_PACK_TEST import/hot reload");
        GalleryPack.Install(Archive(Files("painting")),packs);
        Check(Manifest("painting").Status==200&&Manifest("calligraphy").Status==404,"Independent pack install/hot reload failed");
        var before=Manifest("painting").Body;string hash=before.GetProperty("sha256").GetString()!;
        var read=gallery.Handle(JsonSerializer.SerializeToElement(new{category="painting",date="2026-10-07",op="read",frame=0,sha256=hash,offset=0,count=49152},JsonDefaults.Options),true,true);
        Check(read.Status==200&&read.Body is Tab5RpcDataBody,"Imported image transport unavailable");
        Console.WriteLine("GALLERY_PACK_TEST reject corrupt/path/dimensions");
        Reject(Archive(Files("painting"),change:f=>f["work.jpg"][0]^=1));
        Reject(Archive(Files("painting"),change:f=>f["../escape.txt"]="bad"u8.ToArray()));
        var invalid=Files("painting");invalid["work.jpg"]=Jpeg(20);Reject(Archive(invalid));
        Check(Manifest("painting").Body.GetProperty("sha256").GetString()==hash,"Failed import replaced previous gallery");
        using(var stop=new CancellationTokenSource()){stop.Cancel();try{GalleryPack.Install(Archive(Files("painting")),packs,cancellation:stop.Token);throw new Exception("Cancelled import accepted");}catch(OperationCanceledException){}}
        Console.WriteLine("GALLERY_PACK_TEST replace/legacy/preserve");
        GalleryPack.Install(Archive(Files("painting","更新后的作品名称")),packs);
        Check(Manifest("painting").Body.GetProperty("title").GetString()=="更新后的作品名称","Catalog did not reload after replacement");
        Check(Directory.EnumerateDirectories(packs,"painting.backup-*").Any(),"Previous user data not retained");
        GalleryPack.Install(Archive(Files("calligraphy"),"calligraphy"),packs);
        Check(Manifest("calligraphy").Status==200,"Second category unavailable");
        Directory.CreateDirectory(legacy);foreach(var pair in Files("painting"))File.WriteAllBytes(Path.Combine(legacy,pair.Key),pair.Value);
        var old=new Tab5Gallery(legacy,Path.Combine(root,"empty"));
        Check(old.Handle(JsonSerializer.SerializeToElement(new{category="painting",date="2026-10-07",op="manifest"}),true,true).Status==200,"Existing local collection no longer works");
        Check(!Directory.EnumerateDirectories(packs,".install-*").Any(),"Temporary imports not cleaned");
        Console.WriteLine("GALLERY_PACK_OK independent optional packs, hot reload, real JPEG transport, legacy data, corruption/path/dimensions/cancellation rejection, prior data preserved");
    }
}
