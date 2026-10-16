using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// All files are curated local assets; authenticated callers cannot supply paths or URLs.
internal sealed class Tab5Gallery
{
    internal sealed record Artwork(string Id,string Category,string Title,string Author,string Source,string License,string[] Frames,string? WorkId=null,string[]? PortraitFrames=null);
    private readonly string _directory;
    private readonly string? _packs;
    private readonly object _gate=new();
    private readonly Dictionary<string,(long Stamp,long Length,Artwork[] Works)> _catalogs=[];
    private readonly Dictionary<string,(long Stamp,long Length,byte[] Bytes,string Sha,long Used)> _images=[];
    private readonly Dictionary<string,(long Stamp,long Length,IReadOnlyDictionary<string,Tab5ArtGuides.Guide> Guides)> _guides=[];
    private readonly Dictionary<string,(long Stamp,long Length,string Digest)> _digests=[];
    private long _revision=-1;
    private long _imageUse;
    internal sealed record BrowserCatalog(string Category,string? Version,Artwork[] Works);
    internal sealed record BrowserPage(Artwork Work,Tab5ArtGuides.Guide? Guide,byte[] Jpeg);
    internal Tab5Gallery(string? directory=null) {
        _directory=directory??Path.Combine(AppContext.BaseDirectory,"Assets","DailyArt");
        _packs=directory is null?GalleryPack.Root:null;
    }
    internal Tab5Gallery(string legacyDirectory,string packsDirectory):this(legacyDirectory){_packs=packsDirectory;}
    private Artwork[] Works(string directory) {
        string path=Path.Combine(directory,"catalog.json");var file=new FileInfo(path);
        if(!file.Exists)return [];
        lock(_gate) {
            if(!_catalogs.TryGetValue(path,out var cached)||cached.Stamp!=file.LastWriteTimeUtc.Ticks||cached.Length!=file.Length) {
                if(file.Length>8*1024*1024)throw new InvalidDataException("Gallery catalog too large");
                cached=(file.LastWriteTimeUtc.Ticks,file.Length,JsonSerializer.Deserialize<Artwork[]>(File.ReadAllText(path),JsonDefaults.Options)??[]);
                _catalogs[path]=cached;
            }
            return cached.Works;
        }
    }
    internal static int DayIndex(DateOnly date,int count)=>((date.DayNumber-new DateOnly(2026,1,1).DayNumber)%count+count)%count;
    private (byte[] Bytes,string Sha) Image(string path) {
        lock(_gate) {
            var file=new FileInfo(path);
            if(!file.Exists){_images.Remove(path);throw new FileNotFoundException();}
            if(file.Length is <16 or >1048576)throw new InvalidDataException("Gallery image size invalid");
            if(!_images.TryGetValue(path,out var cached)||cached.Stamp!=file.LastWriteTimeUtc.Ticks||cached.Length!=file.Length) {
                byte[] bytes=File.ReadAllBytes(path);
                if(bytes.Length is <16 or >1048576)throw new InvalidDataException("Gallery image size invalid");
                cached=(file.LastWriteTimeUtc.Ticks,file.Length,bytes,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),0);
                // Four validated JPEGs cover both layouts of both categories.
                // Every range still checks metadata, but reads/hashes only on change.
                if(!_images.ContainsKey(path)&&_images.Count>=4)_images.Remove(_images.MinBy(entry=>entry.Value.Used).Key);
            }
            cached.Used=++_imageUse;_images[path]=cached;
            return(cached.Bytes,cached.Sha);
        }
    }
    internal (int Status,object Body) Handle(JsonElement request,bool binary,bool bulk,bool stream=false)
    {
        lock(GalleryPack.Gate) {
            RefreshRevision();
            return HandleCore(request,binary,bulk,stream);
        }
    }
    private void RefreshRevision() {
        if(_revision==GalleryPack.Revision)return;
        lock(_gate){_catalogs.Clear();_images.Clear();_guides.Clear();_digests.Clear();_revision=GalleryPack.Revision;}
    }
    private string ContentDigest(string category) {
        if(_packs is null)return "";
        var file=new FileInfo(Path.Combine(_packs,category,"PACK.json"));
        if(!file.Exists){_digests.Remove(category);return "";}
        lock(_gate) {
            if(!_digests.TryGetValue(category,out var cached)||cached.Stamp!=file.LastWriteTimeUtc.Ticks||cached.Length!=file.Length) {
                var manifest=GalleryPack.Installed(category,_packs);
                if(manifest is null)return "";
                cached=(file.LastWriteTimeUtc.Ticks,file.Length,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest,JsonDefaults.Options))).ToLowerInvariant());
                _digests[category]=cached;
            }
            return cached.Digest;
        }
    }
    private string CategoryDirectory(string category)=>_packs is not null&&File.Exists(Path.Combine(_packs,category,"catalog.json"))?Path.Combine(_packs,category):_directory;
    private Artwork[] BrowserWorks(string directory,string category) {
        var works=Works(directory).Where(w=>w.Category==category).ToArray();
        if(works.Length>4096||works.Any(w=>string.IsNullOrWhiteSpace(w.Id)||w.Frames is null||w.Frames.Length is <1 or >64)||works.Select(w=>w.Id).Distinct(StringComparer.Ordinal).Count()!=works.Length)
            throw new InvalidDataException("图库目录无效，请重新导入图库包。");
        return works;
    }
    internal BrowserCatalog Browse(string category) {
        if(!GalleryPack.Category(category))throw new ArgumentException("未知图库类别。");
        lock(GalleryPack.Gate) {
            RefreshRevision();string directory=CategoryDirectory(category);
            var works=BrowserWorks(directory,category);
            // Use the same verified captions for the list, search and detail view.
            works=works.Select(work=>{
                var guide=Guide(directory,work,0);
                return work with{Title=string.IsNullOrWhiteSpace(guide?.Title)?work.Title:guide.Title,Author=string.IsNullOrWhiteSpace(guide?.Author)?work.Author:guide.Author};
            }).ToArray();
            return new(category,_packs is not null?GalleryPack.Installed(category,_packs)?.Version:null,works);
        }
    }
    internal BrowserPage BrowsePage(string category,string id,int frame) {
        if(!GalleryPack.Category(category))throw new ArgumentException("未知图库类别。");
        lock(GalleryPack.Gate) {
            RefreshRevision();string directory=CategoryDirectory(category);
            var work=BrowserWorks(directory,category).SingleOrDefault(w=>w.Id==id)??throw new InvalidDataException("作品已更新，请刷新目录。");
            if(frame<0||frame>=work.Frames.Length)throw new ArgumentOutOfRangeException(nameof(frame));
            string filename=work.Frames[frame];
            if(filename!=Path.GetFileName(filename)||!filename.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图库图片路径无效。");
            var image=Image(Path.Combine(directory,filename));
            return new(work,Guide(directory,work,0),image.Bytes);
        }
    }
    private Tab5ArtGuides.Guide? Guide(string directory,Artwork work,int version) {
        string path=Path.Combine(directory,"guides.zh.json");var file=new FileInfo(path);
        if(!file.Exists) {
            var pack=_packs is not null&&directory==Path.Combine(_packs,work.Category)?GalleryPack.Installed(work.Category,_packs):null;
            if(pack?.SchemaVersion==2)throw new InvalidDataException("Gallery guide missing");
            return Tab5ArtGuides.ForReader(work.WorkId??work.Source,version);
        }
        lock(_gate) {
            if(!_guides.TryGetValue(path,out var cached)||cached.Stamp!=file.LastWriteTimeUtc.Ticks||cached.Length!=file.Length) {
                if(file.Length>8*1024*1024)throw new InvalidDataException("Gallery guide too large");
                using var document=JsonDocument.Parse(File.ReadAllBytes(path));
                cached=(file.LastWriteTimeUtc.Ticks,file.Length,Tab5ArtGuides.ReadPack(document.RootElement,Works(directory).Where(w=>w.Category==work.Category).ToArray()));
                _guides[path]=cached;
            }
            return Tab5ArtGuides.ForReader(cached.Guides.GetValueOrDefault(work.WorkId??work.Source),version);
        }
    }
    private (int Status,object Body) HandleCore(JsonElement request,bool binary,bool bulk,bool stream)
    {
        string Text(string key)=>request.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
        try {
            string category=Text("category");
            DateOnly date=default;
            if(category is not ("painting" or "calligraphy")||(Text("op")!="catalog"&&!DateOnly.TryParseExact(Text("date"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)))return(400,new{error="invalid_gallery_date"});
            string directory=CategoryDirectory(category);
            var works=Works(directory).Where(w=>w.Category==category).ToArray();
            if(works.Length==0)return(404,new{error="gallery_pack_missing",category});
            if(Text("op")=="catalog") {
                var installed=_packs is not null?GalleryPack.Installed(category,_packs):null;
                if(installed is null)return(409,new{error="gallery_catalog_requires_installed_pack"});
                if(works.Length!=installed.Works||works.Length>4096||works.Any(w=>w.Frames.Length is <1 or >64||w.PortraitFrames?.Length!=w.Frames.Length))
                    return(503,new{error="invalid_gallery_catalog"});
                string revision=ContentDigest(category);
                return(200,new{syncVersion=1,works=works.Length,revision,baseDate="2026-01-01"});
            }
            var work=works[DayIndex(date,works.Length)];
            if(Text("op")=="guidePage") {
                var full=Guide(directory,work,0);
                if(full is null||Text("workId")!=full.WorkId||Text("sha256")!=Tab5ArtGuides.TextHash(full.Transcription))
                    return(409,new{error="gallery_guide_changed"});
                var pages=Tab5ArtGuides.TextPages(full.Transcription);
                if(!request.TryGetProperty("page",out var requestedPage)||!requestedPage.TryGetInt32(out int page)||page<0||page>=pages.Length)
                    return(400,new{error="invalid_gallery_guide_page"});
                return(200,new{workId=full.WorkId,page,pages=pages.Length,sha256=Text("sha256"),pageSha256=Tab5ArtGuides.TextHash(pages[page]),text=pages[page]});
            }
            string orientation=Text("orientation");
            if(orientation is not ("" or "landscape" or "portrait"))return(400,new{error="invalid_gallery_orientation"});
            bool portrait=orientation=="portrait";
            var displayFrames=portrait?work.PortraitFrames:work.Frames;
            if(displayFrames is null||displayFrames.Length!=work.Frames.Length)return(503,new{error="gallery_portrait_unavailable"});
            int frame=request.TryGetProperty("frame",out var f)&&f.TryGetInt32(out var n)?n:0;
            if(frame<0||frame>=work.Frames.Length)return(400,new{error="invalid_gallery_frame"});
            string filename=displayFrames[frame];
            if(filename!=Path.GetFileName(filename)||!filename.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase))return(503,new{error="invalid_gallery_catalog"});
            var (data,sha)=Image(Path.Combine(directory,filename));
            if(Text("op")=="manifest") {
                bool include=request.TryGetProperty("includeGuide",out var requestedGuide)&&requestedGuide.ValueKind==JsonValueKind.True;
                int guideVersion=request.TryGetProperty("guideVersion",out var requestedVersion)&&requestedVersion.ValueKind==JsonValueKind.Number&&requestedVersion.TryGetInt32(out var v)&&v is 2 or 3?v:1;
                var guide=include?Guide(directory,work,guideVersion):null;
                // Only a client holding this exact category revision and work
                // may reuse text. Legacy clients continue receiving full guides.
                bool guideUnchanged=guideVersion==3&&guide is not null&&Text("guideWorkId")==guide.WorkId&&
                    Text("guideRevision").Length==64&&Text("guideRevision")==ContentDigest(category);
                return(200,new{work.Id,work.Title,work.Author,work.Source,work.License,date=Text("date"),size=data.Length,sha256=sha,frames=work.Frames.Length,width=1280,height=720,orientation=portrait?"portrait":"landscape",guideVersion,workId=guide?.WorkId,guideUnchanged,guide=guideUnchanged?null:guide});
            }
            if(Text("op")!="read"||!binary)return(400,new{error="gallery_requires_binary"});
            if(Text("sha256")!=sha)return(409,new{error="gallery_changed"});
            if(!request.TryGetProperty("offset",out var o)||!o.TryGetInt32(out var offset)||!request.TryGetProperty("count",out var c)||!c.TryGetInt32(out var count)||offset<0||offset>=data.Length||count<=0||count>(stream?Tab5GalleryStream.MaximumImage:bulk?49152:8192))return(400,new{error="invalid_gallery_range"});
            int size=Math.Min(count,data.Length-offset);
            return(200,new Tab5RpcDataBody(new{offset,count=size},data.AsMemory(offset,size)));
        }catch(FileNotFoundException) {return(503,new{error="gallery_pack_incomplete"});}
        catch(InvalidDataException) {return(503,new{error="invalid_gallery_image"});}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) {
            return(503,new{error="gallery_unavailable"});
        }
    }
}
