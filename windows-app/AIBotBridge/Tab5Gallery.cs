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
    private long _imageUse;
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
    internal (int Status,object Body) Handle(JsonElement request,bool binary,bool bulk)
    {
        string Text(string key)=>request.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
        try {
            string category=Text("category");
            if(category is not ("painting" or "calligraphy")||!DateOnly.TryParseExact(Text("date"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return(400,new{error="invalid_gallery_date"});
            string directory=_packs is not null&&File.Exists(Path.Combine(_packs,category,"catalog.json"))?Path.Combine(_packs,category):_directory;
            var works=Works(directory).Where(w=>w.Category==category).ToArray();
            if(works.Length==0)return(404,new{error="gallery_pack_missing",category});
            var work=works[DayIndex(date,works.Length)];
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
            if(Text("op")=="manifest")return(200,new{work.Id,work.Title,work.Author,work.Source,work.License,date=Text("date"),size=data.Length,sha256=sha,frames=work.Frames.Length,width=1280,height=720,orientation=portrait?"portrait":"landscape"});
            if(Text("op")!="read"||!binary)return(400,new{error="gallery_requires_binary"});
            if(Text("sha256")!=sha)return(409,new{error="gallery_changed"});
            if(!request.TryGetProperty("offset",out var o)||!o.TryGetInt32(out var offset)||!request.TryGetProperty("count",out var c)||!c.TryGetInt32(out var count)||offset<0||offset>=data.Length||count<=0||count>(bulk?49152:8192))return(400,new{error="invalid_gallery_range"});
            int size=Math.Min(count,data.Length-offset);
            return(200,new Tab5RpcDataBody(new{offset,count=size},data.AsMemory(offset,size)));
        }catch(FileNotFoundException) {return(503,new{error="gallery_pack_incomplete"});}
        catch(InvalidDataException) {return(503,new{error="invalid_gallery_image"});}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) {
            return(503,new{error="gallery_unavailable"});
        }
    }
}
