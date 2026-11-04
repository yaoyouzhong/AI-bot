using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;

internal static class GalleryCollectionSelfTest
{
    internal static async Task RunInstalledFull(string root,string category) {
        GalleryPreferences.Set(true,true);
        var hd=GalleryHdPack.Installed(category,root)??throw new Exception("HD collection missing");
        var packs=Path.Combine(root,"standard-gallery");var manifest=GalleryPack.Installed(category,packs)??throw new Exception("Standard collection missing");
        var gallery=new Tab5Gallery(Path.Combine(root,"no-fallback"),packs);var full=gallery.Browse(category);
        if(manifest.Collection!="full"||full.Works.Length!=manifest.Works)throw new Exception("Full range is incomplete");
        static (int Count,string Revision) Catalog(Tab5Gallery gallery,string category) {
            var result=gallery.Handle(JsonSerializer.SerializeToElement(new{op="catalog",category}),true,true);
            if(result.Status!=200)throw new Exception("Device catalog unavailable: "+result.Status);
            var body=JsonSerializer.SerializeToElement(result.Body,JsonDefaults.Options);
            return(body.GetProperty("works").GetInt32(),body.GetProperty("revision").GetString()!);
        }
        var a=Catalog(gallery,category);GalleryPreferences.Set(false,true);
        var annual=gallery.Browse(category);var b=Catalog(gallery,category);
        var expected=GalleryPack.ReadAnnual(Path.Combine(packs,category),category,full.Works).Ids;
        if(!annual.Works.Select(w=>w.Id).SequenceEqual(expected)||!full.Works.Take(expected.Length).Select(w=>w.Id).SequenceEqual(expected)||a.Count!=full.Works.Length||b.Count!=expected.Length||a.Revision==b.Revision)throw new Exception("Annual order, shared full positions or device content revision differs");
        GalleryPreferences.Set(true,true);var loader=new GalleryDesktopImages(Path.Combine(hd.Directory,"desktop-originals.json"));
        foreach(int i in new[]{0,full.Works.Length/2,full.Works.Length-1}.Distinct()) {
            var page=gallery.BrowsePage(category,full.Works[i].Id,0);
            using var image=(await loader.LoadAsync(page,0,default))?.Image;
            if(image is null)throw new Exception("HD decode failed: "+full.Works[i].Id);
        }
        Console.WriteLine($"FULL_COLLECTION_READBACK_OK category={category} full={a.Count} annual={b.Count}; ordered index, distinct device revisions, three HD images");
    }
    internal static async Task RunOneImage(string indexPath,string previewPath) {
        var index=JsonSerializer.Deserialize<GalleryDesktopImages.Index>(File.ReadAllBytes(indexPath),JsonDefaults.Options)??throw new Exception("Index missing");
        var mapping=index.Pages.Single();byte[] preview=File.ReadAllBytes(previewPath);
        var work=new Tab5Gallery.Artwork(mapping.Id,mapping.Category,"测试","测试",mapping.WorkId,"test",[mapping.Frame],mapping.WorkId,[mapping.Frame]);
        var loader=new GalleryDesktopImages(indexPath);var result=await loader.LoadAsync(new(work,null,preview),0,default);
        if(result is null)throw new Exception("HD decode failed: "+loader.LastFailure);
        using var bitmap=result.Image;Console.WriteLine($"HD_SINGLE_IMAGE_OK source={mapping.Width}x{mapping.Height} decoded={bitmap.Width}x{bitmap.Height}");
    }
    internal static async Task RunFullPack(string archive,string output) {
        Directory.CreateDirectory(output);
        GalleryPreferences.Set(true,true);
        var result=GalleryHdPack.Install(archive,output,expectedCollection:"full");
        var standard=GalleryPack.Installed(result.Category,Path.Combine(output,"standard-gallery"))??throw new Exception("Embedded full collection missing");
        var gallery=new Tab5Gallery(Path.Combine(output,"no-fallback"),Path.Combine(output,"standard-gallery"));
        var catalog=gallery.Browse(result.Category);
        if(standard.Collection!="full"||catalog.Works.Length!=result.Works)throw new Exception("Full collection identity differs");
        var loader=new GalleryDesktopImages(Path.Combine(result.Directory,"desktop-originals.json"));
        if(loader.LastFailure is not null)throw new Exception(loader.LastFailure);
        var indices=new[]{0,catalog.Works.Length/2,catalog.Works.Length-1}.Distinct();
        foreach(int i in indices) {
            var work=catalog.Works[i];var page=gallery.BrowsePage(result.Category,work.Id,0);
            using var image=(await loader.LoadAsync(page,0,default))?.Image;
            if(image is null)throw new Exception("HD image not readable: "+work.Id);
        }
        Console.WriteLine($"FULL_HD_IMPORT_OK category={result.Category} works={catalog.Works.Length} frames={standard.Frames}; three source images decoded");
    }
    internal static void Run(string output) {
        Directory.CreateDirectory(output);
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        string archive=Path.Combine(output,"annual-fixture.zip"),full=Path.Combine(output,"full-fixture.zip");
        File.WriteAllBytes(archive,GalleryUpdateSelfTest.Package("painting","2026.10.09","年度作品"));
        var files=new Dictionary<string,byte[]>();
        using(var source=ZipFile.OpenRead(archive))foreach(var entry in source.Entries){using var input=entry.Open();using var data=new MemoryStream();input.CopyTo(data);files[entry.FullName]=data.ToArray();}
        var catalog=JsonNode.Parse(files["catalog.json"])!.AsArray();var second=catalog[0]!.DeepClone().AsObject();
        second["id"]="second";second["workId"]="two";second["title"]="全量作品";second["frames"]=new JsonArray("second.jpg");second["portraitFrames"]=new JsonArray("second-portrait.jpg");catalog.Insert(0,second);
        files["catalog.json"]=JsonSerializer.SerializeToUtf8Bytes(catalog);
        var guides=JsonNode.Parse(files["guides.zh.json"])!.AsObject();var rows=guides["works"]!.AsArray();var extra=rows[0]!.DeepClone().AsObject();extra["id"]="second";extra["workId"]="two";extra["title"]="全量作品";rows.Add(extra);files["guides.zh.json"]=JsonSerializer.SerializeToUtf8Bytes(guides);
        files["second.jpg"]=files["work.jpg"];files["second-portrait.jpg"]=files["work-portrait.jpg"];
        files["annual-index.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryAnnualIndex(1,"painting",["fixture"]),JsonDefaults.Options);
        var hashes=files.Where(p=>p.Key!="PACK.json").ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(p.Value)).ToLowerInvariant());
        files["PACK.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryPackManifest(2,"2026.11.03","painting",2,4,hashes,"full"),JsonDefaults.Options);
        using(var target=ZipFile.Open(full,ZipArchiveMode.Create))foreach(var (name,bytes) in files){using var dest=target.CreateEntry(name).Open();dest.Write(bytes);}
        string badIndex=Path.Combine(output,"bad-index.zip");var badFiles=new Dictionary<string,byte[]>(files);
        badFiles["annual-index.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryAnnualIndex(1,"painting",["missing"]),JsonDefaults.Options);
        var badHashes=hashes.ToDictionary(p=>p.Key,p=>p.Value);badHashes["annual-index.json"]=Convert.ToHexString(SHA256.HashData(badFiles["annual-index.json"])).ToLowerInvariant();
        badFiles["PACK.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryPackManifest(2,"2026.11.03","painting",2,4,badHashes,"full"),JsonDefaults.Options);
        using(var target=ZipFile.Open(badIndex,ZipArchiveMode.Create))foreach(var (name,bytes) in badFiles){using var dest=target.CreateEntry(name).Open();dest.Write(bytes);}
        GalleryPack.Install(archive,expectedCollection:"annual");
        var gallery=new Tab5Gallery();Check(gallery.Browse("painting").Works.Length==1,"Annual gallery not available");
        string root=GalleryPack.Root;
        GalleryPreferences.Set(true,false);Check(GalleryPack.Root==root&&!GalleryPreferences.High,"Collection or quality did not change");
        Check(gallery.Browse("painting").Works.Length==0,"Uninstalled full collection fell back to annual data");
        try{GalleryPack.Install(archive,expectedCollection:"full");throw new Exception("Wrong collection accepted");}catch(InvalidDataException){}
        GalleryPack.Install(full,expectedCollection:"full");Check(gallery.Browse("painting").Works.Select(w=>w.Id).SequenceEqual(["fixture","second"]),"Full collection did not keep annual works at the same positions");
        try{GalleryPack.Install(badIndex);throw new Exception("Invalid annual index accepted");}catch(InvalidDataException){}
        Check(gallery.Browse("painting").Works.Length==2,"Invalid index changed the installed collection");
        string fullRevision=GalleryPack.Digest(GalleryPack.Installed("painting")!);
        GalleryPreferences.Set(false,true);Check(GalleryPack.Root==root&&gallery.Browse("painting").Works.Select(w=>w.Id).SequenceEqual(["fixture"]),"Annual index did not select the shared artwork");
        Check(fullRevision!=GalleryPack.Digest(GalleryPack.Installed("painting")!),"Device revision did not change with the selection");
        var annualHandler=new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("[]")})};
        using(var service=new UpdateService(annualHandler))using(var annualForm=new GalleryPackForm(service)) {
            string displayed=annualForm.Controls.Find("painting-installed",true).Single().Text;
            Check(displayed.Contains("1 件")&&!displayed.Contains("按年度索引显示"),"Annual collection count or label is incorrect");
            Check(annualForm.Controls.Find("painting-latest",true).Single().Text=="","Unnecessary collection note remained visible");
            var quality=(ComboBox)annualForm.Controls.Find("gallery-quality",true).Single();
            var collection=(ComboBox)annualForm.Controls.Find("gallery-collection",true).Single();
            var message=(Label)annualForm.Controls.Find("gallery-state",true).Single();
            quality.SelectedIndex=0;collection.SelectedIndex=1;
            Check(message.Text=="","Switching to the full collection left a redundant message");
            collection.SelectedIndex=0;
            Check(message.Text=="","Switching to the annual collection left a redundant message");
            static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
            Check(!Descendants(annualForm).Any(c=>c.Text.Contains("普通版和高清版的 TAB5 显示相同")),"Unnecessary TAB5 note remained visible");
            annualForm.StartPosition=FormStartPosition.Manual;annualForm.Location=new(-30000,-30000);annualForm.ShowInTaskbar=false;
            annualForm.Show();Application.DoEvents();annualForm.PerformLayout();
            using var image=new Bitmap(annualForm.Width,annualForm.Height);annualForm.DrawToBitmap(image,new(Point.Empty,annualForm.Size));image.Save(Path.Combine(output,"gallery-annual.png"));
            annualForm.Close();
        }
        GalleryPreferences.Set(false,true);
        try{GalleryPack.Install(archive);throw new Exception("Annual package overwrote full collection");}catch(InvalidDataException){}
        var settings=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","gallery-preferences.json"))).RootElement;
        Check(!settings.GetProperty("full").GetBoolean()&&settings.GetProperty("high").GetBoolean(),"Preferences were not persisted");
        GalleryPreferences.Set(true,false);
        using(var form=new GalleryPackForm()) {
            Check(form.Controls.Find("painting-installed",true).Single().Text.Contains("2 件"),"Full collection count is incorrect");
            Check(form.Controls.Find("painting-latest",true).Single().Text=="","Unnecessary collection note remained visible");
            form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();
            Application.DoEvents();form.PerformLayout();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"gallery-collections.png"));
            form.Close();
        }
        Console.WriteLine("GALLERY_COLLECTIONS_OK isolated profile; shared images, ordered annual index, switching, persistence, device revision, overwrite protection and offscreen UI");
    }
}
