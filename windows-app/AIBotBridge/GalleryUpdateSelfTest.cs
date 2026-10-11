using System.Drawing.Imaging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryUpdateSelfTest
{
    internal static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    internal static byte[] Package(string category,string version,string title) {
        using var bitmap=new Bitmap(1280,720);using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.Beige);
        using var image=new MemoryStream();bitmap.Save(image,ImageFormat.Jpeg);
        var files=new Dictionary<string,byte[]> {
            ["catalog.json"]=JsonSerializer.SerializeToUtf8Bytes(new[]{new Tab5Gallery.Artwork("fixture",category,title,"作者","https://example.invalid/work","CC0",["work.jpg"],"one",["work-portrait.jpg"])},JsonDefaults.Options),
            ["guides.zh.json"]=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,language="zh-CN",works=new[]{new{id="fixture",category,workId="one",title,author="作者",period="年代",museum="藏馆",brief="导读",overview=title+"简介",looking=title+"欣赏提示",
                transcription=new{status=category=="calligraphy"?"source_verified":"not_available",text=title+"原文",explanation="说明"},sources=new[]{new{id="museum",url="https://example.invalid/work"}}}}},JsonDefaults.Options),
            ["provenance.json"]="[]"u8.ToArray(),["selection.json"]="[]"u8.ToArray(),["LICENSE"]="fixture"u8.ToArray(),["THIRD_PARTY_NOTICES.md"]="fixture"u8.ToArray(),
            ["work.jpg"]=image.ToArray(),["work-portrait.jpg"]=image.ToArray()
        };
        var hashes=files.ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(p.Value)).ToLowerInvariant());
        files["PACK.json"]=JsonSerializer.SerializeToUtf8Bytes(new GalleryPackManifest(2,version,category,1,2,hashes),JsonDefaults.Options);
        return UpdateSelfTest.Zip(files);
    }
    internal static object Release(string category,string version,byte[] package,bool draft=false,bool prerelease=false,string? url=null,bool curated=true,bool checks=true,string tag="gallery-annual") {
        string name=$"AI-bot-Daily{(category=="painting"?"Painting":"Calligraphy")}-{(curated?"Curated-":"")}{version}.zip";
        var assets=new List<object>{new{name,browser_download_url=url??$"https://github.com/yaoyouzhong/AI-bot/releases/download/{tag}/{name}",size=package.LongLength}};
        if(checks)assets.Add(new{name="SHA256SUMS.txt",browser_download_url=$"https://github.com/yaoyouzhong/AI-bot/releases/download/{tag}/SHA256SUMS.txt",size=0L});
        return new{tag_name=tag,draft,prerelease,assets,body="精选集"};
    }
    private static Dictionary<string,GalleryUpdate> Read(params object[] releases){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(releases));return GalleryUpdateCatalog.Read(doc.RootElement);}
    internal static async Task RunAsync() {
        byte[] painting=Package("painting","2026.10.09","新名画"),calligraphy=Package("calligraphy","2026.10.09","新书法");
        var releases=Read(Release("painting","2026.10.08",painting),Release("painting","2026.10.09",painting),Release("painting","2026.10.09.1",painting),
            Release("painting","2099.01.01",painting,draft:true),Release("painting","2099.01.02",painting,prerelease:true),
            Release("painting","2099.01.03",painting,curated:false),Release("painting","2026.02.30",painting),Release("calligraphy","2026.10.09",calligraphy));
        Check(releases["painting"].Version=="2026.10.09.1"&&releases["calligraphy"].Version=="2026.10.09","Independent curated editions, dates, revisions or formal-release filter failed");
        var latest=releases["calligraphy"];
        var old=new GalleryPackManifest(1,"2026.10.07","calligraphy",411,3000,new());
        Check(GalleryUpdateCatalog.Blocked(old,latest) is null&&GalleryUpdateCatalog.Blocked(null,latest) is null,"New gallery install blocked");
        Check(GalleryUpdateCatalog.Blocked(old with{Version="2026.10.09.0"},latest)=="已是最新版本","Equivalent date revision not equal");
        Check(GalleryUpdateCatalog.Blocked(old with{Version="2026.10.10"},latest)=="本机版本较新","Gallery downgrade allowed");
        foreach(string url in new[]{"https://example.org/pack.zip","https://github.com:444/yaoyouzhong/AI-bot/releases/download/gallery-annual/pack.zip"})
            Check(GalleryUpdateCatalog.Blocked(null,Read(Release("painting","2026.10.09",painting,url:url))["painting"]) is not null,"Unsafe gallery download offered");
        Check(GalleryUpdateCatalog.Blocked(null,Read(Release("painting","2026.10.09",painting,checks:false))["painting"]) is not null,"Gallery without checksum offered");
        Check(GalleryUpdateCatalog.Blocked(null,latest with{Package=latest.Package! with{Size=447081384}}) is null,"Current calligraphy size rejected");
        string json=JsonSerializer.Serialize(new[]{Release("painting","2026.10.09",painting),Release("calligraphy","2026.10.09",calligraphy)});
        bool corrupt=false,fail=false,redirect=false;
        var handler=new UpdateSelfTest.Handler();
        handler.Reply=(request,token)=> {
            if(fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            string path=request.RequestUri!.AbsolutePath;
            if(redirect&&path.EndsWith(".zip"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.org/pack.zip")}});
            byte[] data=path.EndsWith("SHA256SUMS.txt")?Encoding.UTF8.GetBytes(string.Join("\n",new[]{("Painting",painting),("Calligraphy",calligraphy)}.Select(p=>Convert.ToHexString(SHA256.HashData(p.Item2))+"  AI-bot-Daily"+p.Item1+"-Curated-2026.10.09.zip"))):
                path.EndsWith(".zip")?(path.Contains("Painting")?painting:calligraphy):Encoding.UTF8.GetBytes(json);
            if(corrupt&&path.EndsWith(".zip"))data=data.Select((b,i)=>i==0?(byte)(b^1):b).ToArray();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(data)});
        };
        using var service=new UpdateService(handler);await service.CheckAsync(default);
        var update=service.Galleries["painting"];var ready=await service.DownloadGalleryAsync(update,null,default);
        string oldZip=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"old.zip");File.WriteAllBytes(oldZip,Package("painting","2026.10.07","旧名画"));GalleryPack.Install(oldZip);
        string otherZip=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"other.zip");File.WriteAllBytes(otherZip,Package("calligraphy","2026.10.07","旧书法"));GalleryPack.Install(otherZip);
        async Task Reject(Func<Task> action){try{await action();}catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or HttpRequestException or OperationCanceledException){return;}throw new Exception("Invalid gallery update accepted");}
        await Reject(()=>{GalleryPack.Install(ready.File,expected:update with{Version="2026.10.10"});return Task.CompletedTask;});
        await Reject(()=>{GalleryPack.Install(ready.File,expected:update with{Category="calligraphy"});return Task.CompletedTask;});
        Check(GalleryPack.Installed("painting")!.Version=="2026.10.07","Mismatched package replaced installed gallery");
        GalleryPack.Install(ready.File,expected:update);
        Check(GalleryPack.Installed("painting")!.Version=="2026.10.09"&&GalleryPack.Installed("calligraphy")!.Version=="2026.10.07","Category-isolated gallery upgrade failed");
        string backup=Directory.GetDirectories(GalleryPack.Root,"painting.backup-*").Single();
        using(var saved=JsonDocument.Parse(File.ReadAllText(Path.Combine(backup,"catalog.json"))))
            Check(saved.RootElement[0].GetProperty("title").GetString()=="旧名画","Old collection backup changed");
        var gallery=new Tab5Gallery();var response=gallery.Handle(JsonSerializer.SerializeToElement(new{category="painting",date="2026-10-09",op="manifest"}),true,true);
        Check(response.Status==200&&JsonSerializer.SerializeToElement(response.Body,JsonDefaults.Options).GetProperty("title").GetString()=="新名画","Gallery service did not hot reload upgraded works");
        var reading=gallery.Handle(JsonSerializer.SerializeToElement(new{category="painting",date="2026-10-09",op="manifest",includeGuide=true,guideVersion=2}),true,true);
        Check(reading.Status==200&&JsonSerializer.SerializeToElement(reading.Body,JsonDefaults.Options).GetProperty("guide").GetProperty("overview").GetString()=="新名画简介","Downloaded pack guide not installed with images");
        string downloads=Path.GetDirectoryName(Path.GetDirectoryName(ready.File))!;UpdateService.ClearGalleryDownload(ready);
        corrupt=true;await Reject(async()=>await service.DownloadGalleryAsync(update,null,default));corrupt=false;
        redirect=true;await Reject(async()=>await service.DownloadGalleryAsync(update,null,default));redirect=false;
        using(var stop=new CancellationTokenSource()){stop.Cancel();await Reject(async()=>await service.DownloadGalleryAsync(update,null,stop.Token));}
        Check(Directory.GetFiles(downloads,"*.part",SearchOption.AllDirectories).All(p=>new FileInfo(p).Length==0),"Corrupt gallery bytes were retained as a resumable checkpoint");
        fail=true;await Reject(()=>service.CheckAsync(default));
        Check(service.Galleries["painting"]==update&&service.Error is not null,"Failed check replaced known gallery metadata");
        Check(GalleryPack.Installed("painting")!.Version=="2026.10.09"&&GalleryPack.Installed("calligraphy")!.Version=="2026.10.07","Failure changed installed collections");
        byte[] large=new byte[5*1024*1024+7];for(int i=0;i<large.Length;i++)large[i]=(byte)(i%251);
        var ranged=Read(Release("painting","2026.10.09",large))["painting"];int chunks=0;bool invalidRange=false,interrupt=false;var starts=new List<long>();using var interrupted=new CancellationTokenSource();
        var served=ranged;
        var rangeHandler=new UpdateSelfTest.Handler();
        rangeHandler.Reply=(request,token)=>{
            if(request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Convert.ToHexString(SHA256.HashData(large))+"  "+served.Package!.Name)});
            var range=request.Headers.Range?.Ranges.Single()??throw new Exception("Large gallery was not ranged");
            int from=checked((int)range.From!.Value),to=checked((int)range.To!.Value);chunks++;starts.Add(from);
            // An incomplete second chunk is retried from its original offset, not appended twice.
            byte[] data=large[from..(to+1)];if(chunks==2)data=data[..^1];
            var response=new HttpResponseMessage(HttpStatusCode.PartialContent){Content=interrupt?new StreamContent(new InterruptedStream(data,interrupted)):new ByteArrayContent(data)};
            response.Content.Headers.ContentLength=to-from+1;response.Content.Headers.ContentRange=new(from+(invalidRange?1:0),to,large.Length);
            return Task.FromResult(response);
        };
        using var rangedService=new UpdateService(rangeHandler);var rangedReady=await rangedService.DownloadGalleryAsync(ranged,null,default);
        Check(chunks==4&&File.ReadAllBytes(rangedReady.File).SequenceEqual(large),"Chunk retry duplicated or lost gallery bytes");
        UpdateService.ClearGalleryDownload(rangedReady);
        invalidRange=true;await Reject(async()=>await rangedService.DownloadGalleryAsync(ranged,null,default));
        invalidRange=false;interrupt=true;await Reject(async()=>await rangedService.DownloadGalleryAsync(ranged,null,interrupted.Token));interrupt=false;
        string checkpoint=Directory.GetFiles(downloads,"*.part",SearchOption.AllDirectories).Single(p=>new FileInfo(p).Length>0);
        long checkpointBytes=new FileInfo(checkpoint).Length;Check(checkpointBytes==65536,"Interrupted download did not persist its byte offset");int startCount=starts.Count;
        // A new service instance models closing/reopening the app, using only the persisted cache.
        using(var reopened=new UpdateService(rangeHandler)) {
            var resumed=await reopened.DownloadGalleryAsync(ranged,null,default);
            Check(starts[startCount]==checkpointBytes&&File.ReadAllBytes(resumed.File).SequenceEqual(large),"Reopened downloader did not resume the exact persisted offset");
            int completeCount=starts.Count;await reopened.DownloadGalleryAsync(ranged,null,default);Check(starts.Count==completeCount,"Verified complete cache was downloaded again");
            large[0]^=1;var changed=Read(Release("painting","2026.10.10",large))["painting"];served=changed;int changedCount=starts.Count;
            var changedReady=await reopened.DownloadGalleryAsync(changed,null,default);
            Check(starts[changedCount]==0&&changedReady.File!=resumed.File&&File.ReadAllBytes(changedReady.File).SequenceEqual(large),"Different release/hash mixed old checkpoint bytes");
        }
        Console.WriteLine("GALLERY_UPDATE_OK curated formal releases, independent dates/revisions, trusted URLs, size/checksum gates, real download/import/hot reload, old backup, category/version mismatch, corruption rejection, bounded chunk retry, persisted cancellation/reopen resume, completed cache reuse, hash isolation, cached metadata; isolated profile");
    }
    private sealed class InterruptedStream(byte[] bytes,CancellationTokenSource stop):MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default) {
            if(Position>=65536){stop.Cancel();return ValueTask.FromCanceled<int>(stop.Token);}
            return base.ReadAsync(buffer[..Math.Min(buffer.Length,65536-(int)Position)],cancellationToken);
        }
    }
}
