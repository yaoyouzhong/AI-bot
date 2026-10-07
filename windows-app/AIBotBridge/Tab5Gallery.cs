using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// All files are curated local assets; authenticated callers cannot supply paths or URLs.
internal sealed class Tab5Gallery
{
    internal sealed record Artwork(string Id,string Category,string Title,string Author,string Source,string License,string[] Frames,string? WorkId=null,string[]? PortraitFrames=null);
    private readonly string _directory;
    private readonly Lazy<Artwork[]> _works;
    internal Tab5Gallery(string? directory=null) {
        _directory=directory??Path.Combine(AppContext.BaseDirectory,"Assets","DailyArt");
        _works=new(()=>JsonSerializer.Deserialize<Artwork[]>(File.ReadAllText(Path.Combine(_directory,"catalog.json")),JsonDefaults.Options)??[]);
    }
    internal static int DayIndex(DateOnly date,int count)=>((date.DayNumber-new DateOnly(2026,1,1).DayNumber)%count+count)%count;
    internal (int Status,object Body) Handle(JsonElement request,bool binary,bool bulk)
    {
        string Text(string key)=>request.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
        try {
            string category=Text("category");
            if(category is not ("painting" or "calligraphy")||!DateOnly.TryParseExact(Text("date"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return(400,new{error="invalid_gallery_date"});
            var works=_works.Value.Where(w=>w.Category==category).ToArray();
            if(works.Length==0)return(404,new{error="gallery_unavailable"});
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
            byte[] data=File.ReadAllBytes(Path.Combine(_directory,filename));
            if(data.Length is <16 or >1048576)return(503,new{error="invalid_gallery_image"});
            string sha=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            if(Text("op")=="manifest")return(200,new{work.Id,work.Title,work.Author,work.Source,work.License,date=Text("date"),size=data.Length,sha256=sha,frames=work.Frames.Length,width=1280,height=720,orientation=portrait?"portrait":"landscape"});
            if(Text("op")!="read"||!binary)return(400,new{error="gallery_requires_binary"});
            if(Text("sha256")!=sha)return(409,new{error="gallery_changed"});
            if(!request.TryGetProperty("offset",out var o)||!o.TryGetInt32(out var offset)||!request.TryGetProperty("count",out var c)||!c.TryGetInt32(out var count)||offset<0||offset>=data.Length||count<=0||count>(bulk?49152:8192))return(400,new{error="invalid_gallery_range"});
            int size=Math.Min(count,data.Length-offset);
            return(200,new Tab5RpcDataBody(new{offset,count=size},data.AsMemory(offset,size)));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) {
            return(503,new{error="gallery_unavailable"});
        }
    }
}
