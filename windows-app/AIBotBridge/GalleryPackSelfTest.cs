using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryPackSelfTest
{
    internal static void RunAnnualPack(string archive,string output) {
        Directory.CreateDirectory(output);
        var installed=GalleryPack.Install(archive);var gallery=new Tab5Gallery();
        if(installed.Category!="calligraphy"||installed.Works!=366)throw new Exception("Annual pack must contain exactly 366 independent calligraphy works");
        var catalog=gallery.Browse("calligraphy");
        string directory=Path.Combine(GalleryPack.Root,"calligraphy");
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"guides.zh.json")));
        var rows=document.RootElement.GetProperty("works").EnumerateArray().ToDictionary(row=>row.GetProperty("id").GetString()!,StringComparer.Ordinal);
        var proof=new List<object>();var native=new List<object>();int images=0;
        JsonElement Body((int Status,object Body) reply) {
            if(reply.Status!=200)throw new Exception("Annual gallery request failed: "+reply.Status);
            return JsonSerializer.SerializeToElement(reply.Body,Tab5ArtGuides.WireOptions);
        }
        for(int day=0;day<catalog.Works.Length;day++) {
            var work=catalog.Works[day];string date=new DateOnly(2026,1,1).AddDays(day).ToString("yyyy-MM-dd");
            var original=rows[work.Id].GetProperty("transcription");
            string text=original.TryGetProperty("completeReferenceText",out var full)?full.GetString()!:original.GetProperty("text").GetString()!;
            bool reviewed=original.GetProperty("status").GetString() is "source_verified" or "reference_verified";
            if(!reviewed)text="";
            var local=gallery.BrowsePage("calligraphy",work.Id,0);
            if(local.Guide?.Transcription!=text)throw new Exception("Windows browser lost complete original: "+work.Id);
            foreach(string orientation in new[]{"landscape","portrait"})for(int frame=0;frame<work.Frames.Length;frame++) {
                var manifest=Body(gallery.Handle(JsonSerializer.SerializeToElement(new{op="manifest",category="calligraphy",date,orientation,frame,includeGuide=true,guideVersion=3}),true,true));
                if(manifest.GetProperty("id").GetString()!=work.Id||manifest.GetProperty("orientation").GetString()!=orientation)throw new Exception("Annual frame identity/orientation mismatch");
                if(JsonSerializer.SerializeToUtf8Bytes(new{status=200,body=manifest},Tab5ArtGuides.WireOptions).Length>16384)throw new Exception("Annual manifest exceeds wire budget");
                var image=gallery.Handle(JsonSerializer.SerializeToElement(new{op="read",category="calligraphy",date,orientation,frame,sha256=manifest.GetProperty("sha256").GetString(),offset=0,count=49152}),true,true);
                if(image.Status!=200||image.Body is not Tab5RpcDataBody)throw new Exception("Annual image transport unavailable");images++;
            }
            if(Encoding.UTF8.GetByteCount(text)<8192)continue;
            foreach(int version in new[]{1,2}) {
                var legacy=Body(gallery.Handle(JsonSerializer.SerializeToElement(new{op="manifest",category="calligraphy",date,includeGuide=true,guideVersion=version}),true,true));
                if(!legacy.GetProperty("guide").GetProperty("transcription").GetString()!.Contains("当前设备版本")||legacy.GetProperty("guide").GetProperty("explanation").GetString()!="")throw new Exception("Legacy reader received excerpt labelled as complete text");
            }
            var first=Body(gallery.Handle(JsonSerializer.SerializeToElement(new{op="manifest",category="calligraphy",date,includeGuide=true,guideVersion=3}),true,true)).GetProperty("guide");
            int count=first.GetProperty("transcriptionPages").GetInt32();string sha=first.GetProperty("transcriptionSha256").GetString()!;
            var pages=new List<JsonElement>();var rebuilt=new StringBuilder();
            for(int page=0;page<count;page++) {
                var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{op="guidePage",category="calligraphy",date,workId=work.WorkId,sha256=sha,page}),true,true);
                var body=Body(reply);string part=body.GetProperty("text").GetString()!;
                if(body.GetProperty("page").GetInt32()!=page||body.GetProperty("pages").GetInt32()!=count||body.GetProperty("pageSha256").GetString()!=Tab5ArtGuides.TextHash(part)||Encoding.UTF8.GetByteCount(part)>4095||JsonSerializer.SerializeToUtf8Bytes(new{status=200,body},Tab5ArtGuides.WireOptions).Length>8192)throw new Exception("Invalid bounded original page");
                pages.Add(body.Clone());rebuilt.Append(part);
            }
            if(rebuilt.ToString()!=text||sha!=Tab5ArtGuides.TextHash(text)||first.GetProperty("transcription").GetString()!=pages[0].GetProperty("text").GetString())throw new Exception("Complete original page assembly changed text");
            foreach(var invalid in new[]{new{workId=work.WorkId,sha256=new string('0',64),page=0},new{workId=(string?)"unrelated",sha256=sha,page=0},new{workId=work.WorkId,sha256=sha,page=count},new{workId=work.WorkId,sha256=sha,page=-1}}) {
                int status=gallery.Handle(JsonSerializer.SerializeToElement(new{op="guidePage",category="calligraphy",date,invalid.workId,invalid.sha256,invalid.page}),true,true).Status;
                if(status is not (400 or 409))throw new Exception("Invalid/stale original page accepted");
            }
            proof.Add(new{work.Id,bytes=Encoding.UTF8.GetByteCount(text),pages=count,sha256=sha,legacyUpgradeNotice=true});
            native.Add(new{work.Id,date,guide=first,pages,fullText=text});
        }
        if(proof.Count!=7)throw new Exception("Annual pack must expose all seven complete long originals");
        File.WriteAllBytes(Path.Combine(output,"native-long-readers.json"),JsonSerializer.SerializeToUtf8Bytes(native,Tab5ArtGuides.WireOptions));
        File.WriteAllBytes(Path.Combine(output,"annual-pack-verification.json"),JsonSerializer.SerializeToUtf8Bytes(new{works=366,images,longOriginals=proof,isolatedProfile=true},Tab5ArtGuides.WireOptions));
        Console.WriteLine($"ANNUAL_CALLIGRAPHY_PACK_OK works=366 images={images} longOriginals=7; production isolated install; full Windows text, v1/v2 notice, bounded v3 pages, exact reassembly SHA-256 and invalid/stale requests rejected");
    }
    internal static void RunGuidePackages(params string[] archives) {
        var gallery=new Tab5Gallery();
        foreach(string archive in archives) {
            var pack=GalleryPack.Install(archive);
            string directory=Path.Combine(GalleryPack.Root,pack.Category);
            var works=JsonSerializer.Deserialize<Tab5Gallery.Artwork[]>(File.ReadAllBytes(Path.Combine(directory,"catalog.json")),JsonDefaults.Options)!;
            using var source=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"guides.zh.json")));
            var expected=Tab5ArtGuides.ReadPack(source.RootElement,works);
            for(int day=0;day<works.Length;day++)foreach(string orientation in new[]{"landscape","portrait"}) {
                string date=new DateOnly(2026,1,1).AddDays(day).ToString("yyyy-MM-dd");
                var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{op="manifest",category=pack.Category,date,orientation,includeGuide=true,guideVersion=2},JsonDefaults.Options),true,true);
                var body=JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options);
                if(reply.Status!=200||body.GetProperty("id").GetString()!=works[day].Id)throw new Exception("Generated package daily identity mismatch");
                var actual=body.GetProperty("guide").Deserialize<Tab5ArtGuides.Guide>(JsonDefaults.Options);
                if(actual!=expected[works[day].WorkId??works[day].Source])throw new Exception("Generated package guide not served verbatim");
                string sha=body.GetProperty("sha256").GetString()!;
                var image=gallery.Handle(JsonSerializer.SerializeToElement(new{op="read",category=pack.Category,date,orientation,offset=0,count=49152,sha256=sha},JsonDefaults.Options),true,true);
                if(image.Status!=200||image.Body is not Tab5RpcDataBody)throw new Exception("Generated package image unavailable");
            }
            Console.WriteLine($"GENERATED_GUIDE_PACK_OK category={pack.Category} works={works.Length} images={pack.Frames} originals={expected.Values.Count(g=>g.Transcription.Length>0)}; production import, both orientations, exact text and image transport");
        }
    }
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
        string Archive(Dictionary<string,byte[]> files,string category="painting",Action<Dictionary<string,byte[]>>? change=null,int schema=1,string version="2026.10.07"){
            var hashes=files.ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(p.Value)).ToLowerInvariant());
            files["PACK.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryPackManifest(schema,version,category,1,2,hashes),JsonDefaults.Options);change?.Invoke(files);
            string path=Path.Combine(root,Guid.NewGuid()+".zip");File.WriteAllBytes(path,UpdateSelfTest.Zip(files));return path;
        }
        void Reject(string path){try{GalleryPack.Install(path,packs);}catch(Exception ex) when(ex is InvalidDataException or ArgumentException or JsonException){return;}throw new Exception("Invalid gallery accepted");}
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
        Console.WriteLine("GALLERY_PACK_TEST bundled guides/identity/hot replacement");
        byte[] Guides(string category,string overview="新版简介",string original="已核实原文",string status="source_verified",string identity="one")=>JsonSerializer.SerializeToUtf8Bytes(new{
            schemaVersion=1,language="zh-CN",works=new[]{new{id="test",category,workId=identity,title="作品",author="作者",period="年代",museum="藏馆",brief="简要导读",overview,looking="欣赏提示",
                transcription=new{status,text=original,explanation="原文说明"},sources=new[]{new{id="museum",url="https://example.invalid/work"}}}}
        },JsonDefaults.Options);
        Dictionary<string,byte[]> WithGuide(string category,string overview="新版简介",string original="已核实原文",string status="source_verified") {
            var files=Files(category);files["guides.zh.json"]=Guides(category,overview,original,status);return files;
        }
        (int Status,JsonElement Body) Reading(string category,int version=2,string guideRevision="",string guideWorkId="") {
            var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{category,date="2026-10-07",op="manifest",includeGuide=true,guideVersion=version,guideRevision,guideWorkId},JsonDefaults.Options),true,true);
            return(reply.Status,JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options));
        }
        string CatalogRevision(string category) {
            var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{category,op="catalog"},JsonDefaults.Options),true,true);
            Check(reply.Status==200,"Installed catalog unavailable");
            return JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options).GetProperty("revision").GetString()!;
        }
        GalleryPack.Install(Archive(WithGuide("painting"),schema:2),packs);
        Check(Reading("painting").Body.GetProperty("guide").GetProperty("overview").GetString()=="新版简介","Pack introduction not served");
        GalleryPack.Install(Archive(WithGuide("calligraphy",original:new string('文',1000)),"calligraphy",schema:2),packs);
        Check(Reading("calligraphy").Body.GetProperty("guide").GetProperty("transcription").GetString()==new string('文',1000),"Full original text lost");
        Check(Reading("calligraphy",1).Body.GetProperty("guide").GetProperty("transcription").GetString()!.Contains("当前设备版本"),"Old device compatibility lost");
        string guideRevision=CatalogRevision("painting");
        var reference=Reading("painting",3,guideRevision,"one").Body;
        Check(reference.GetProperty("guideUnchanged").GetBoolean()&&reference.GetProperty("guide").ValueKind==JsonValueKind.Null&&reference.GetProperty("workId").GetString()=="one","Unchanged work guide not omitted");
        foreach(var hint in new[]{("", "one"),(new string('0',64),"one"),(guideRevision,"another-work")})
            Check(!Reading("painting",3,hint.Item1,hint.Item2).Body.GetProperty("guideUnchanged").GetBoolean(),"Unproven guide reused");
        Check(Reading("painting",2,guideRevision,"one").Body.GetProperty("guide").ValueKind==JsonValueKind.Object,"Legacy reader guide omitted");
        string guidePath=Path.Combine(packs,"painting","guides.zh.json"),catalogPath=Path.Combine(packs,"painting","catalog.json");
        DateTime guideStamp=File.GetLastWriteTimeUtc(guidePath),catalogStamp=File.GetLastWriteTimeUtc(catalogPath);
        long guideLength=new FileInfo(guidePath).Length;
        GalleryPack.Install(Archive(WithGuide("painting","更新简介","更新后原文"),schema:2,version:"2026.10.08"),packs);
        File.SetLastWriteTimeUtc(guidePath,guideStamp);File.SetLastWriteTimeUtc(catalogPath,catalogStamp);
        Check(new FileInfo(guidePath).Length==guideLength,"Replacement fixture must have identical length");
        var updated=Reading("painting").Body.GetProperty("guide");
        Check(updated.GetProperty("overview").GetString()=="更新简介"&&updated.GetProperty("looking").GetString()=="欣赏提示"&&updated.GetProperty("transcription").GetString()=="更新后原文","Same-size/timestamp text-only update not hot loaded");
        var changed=Reading("painting",3,guideRevision,"one").Body;
        Check(!changed.GetProperty("guideUnchanged").GetBoolean()&&changed.GetProperty("guide").GetProperty("overview").GetString()=="更新简介"&&CatalogRevision("painting")!=guideRevision,"Stale guide hint survived text-only hot update");
        Check(Reading("painting",3,CatalogRevision("painting"),"one").Body.GetProperty("guideUnchanged").GetBoolean(),"Updated guide hint not reusable");
        Console.WriteLine("GALLERY_GUIDE_REFERENCE_OK exact revision/work only; old readers and stale/wrong hints receive full text; same-size/time update invalidates digest");
        Check(Reading("calligraphy").Body.GetProperty("guide").GetProperty("transcription").GetString()==new string('文',1000),"Painting update replaced calligraphy text");
        var mismatched=WithGuide("painting");mismatched["guides.zh.json"]=Guides("painting",identity:"different-work");Reject(Archive(mismatched,schema:2));
        var crossCategory=WithGuide("painting");crossCategory["guides.zh.json"]=Guides("calligraphy");Reject(Archive(crossCategory,schema:2));
        var longText=WithGuide("painting",original:new string('文',2731));Reject(Archive(longText,schema:2));
        Reject(Archive(WithGuide("painting",original:new string('<',2730)),schema:2));
        Reject(Archive(WithGuide("painting",original:""),schema:2));
        var malformed=WithGuide("painting");malformed["guides.zh.json"]="{}"u8.ToArray();Reject(Archive(malformed,schema:2));
        var duplicate=WithGuide("painting");var document=System.Text.Json.Nodes.JsonNode.Parse(duplicate["guides.zh.json"])!;
        var rows=document["works"]!.AsArray();rows.Add(rows[0]!.DeepClone());duplicate["guides.zh.json"]=JsonSerializer.SerializeToUtf8Bytes(document,JsonDefaults.Options);Reject(Archive(duplicate,schema:2));
        Reject(Archive(Files("painting"),schema:2));
        Reject(Archive(WithGuide("painting"),schema:2,change:f=>f["guides.zh.json"][0]^=1));
        using(var stop=new CancellationTokenSource()){stop.Cancel();try{GalleryPack.Install(Archive(WithGuide("painting","取消更新"),schema:2),packs,cancellation:stop.Token);throw new Exception("Cancelled text update accepted");}catch(OperationCanceledException){}}
        Check(Reading("painting").Body.GetProperty("guide").GetProperty("overview").GetString()=="更新简介","Invalid/cancelled guide import replaced installed text");
        foreach(string status in new[]{"not_available","withheld_pending_review"}) {
            GalleryPack.Install(Archive(WithGuide("painting",status:status),schema:2),packs);
            Check(Reading("painting").Body.GetProperty("guide").GetProperty("transcription").GetString()=="","Unreviewed original leaked to reader");
        }
        // Old packages still use the existing reviewed embedded fallback.
        var legacyFiles=Files("painting");var legacyWork=new Tab5Gallery.Artwork("test","painting","作品","作者","https://example.invalid/work","CC0",["work.jpg"],"https://www.artic.edu/artworks/16568",["work-portrait.jpg"]);
        legacyFiles["catalog.json"]=JsonSerializer.SerializeToUtf8Bytes(new[]{legacyWork},JsonDefaults.Options);GalleryPack.Install(Archive(legacyFiles),packs);
        var legacyGuide=Reading("painting");
        Check(legacyGuide.Status==200,"Legacy image-only pack unavailable");
        var embedded=Tab5ArtGuides.Find(legacyWork.WorkId!);
        Check(embedded is null?legacyGuide.Body.GetProperty("guide").ValueKind==JsonValueKind.Null:
            legacyGuide.Body.GetProperty("guide").GetProperty("overview").GetString()==embedded.Overview,"Legacy package fallback lost or unreleased text leaked");
        // Missing v2 guide must never silently fall back to stale embedded text.
        GalleryPack.Install(Archive(WithGuide("painting"),schema:2),packs);File.Delete(guidePath);
        Check(Reading("painting").Status==503,"Missing v2 guide silently substituted embedded text");
        Check(!Directory.EnumerateDirectories(packs,".install-*").Any(),"Temporary imports not cleaned");
        Console.WriteLine("GALLERY_PACK_GUIDES_OK both categories; images and text installed atomically; same-size/time text-only hot update; v1/v2 readers; verified-only originals; identity/hash/bounds/missing/cancellation rejection; legacy fallback; backups");
        Console.WriteLine("GALLERY_PACK_OK independent optional packs, hot reload, real JPEG transport, legacy data, corruption/path/dimensions/cancellation rejection, prior data preserved");
    }
}
