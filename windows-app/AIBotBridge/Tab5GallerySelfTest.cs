using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;
internal static class Tab5GallerySelfTest
{
    internal static void Run() {
        var gallery=new Tab5Gallery();
        var catalog=JsonSerializer.Deserialize<Tab5Gallery.Artwork[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","DailyArt","catalog.json")),JsonDefaults.Options)!;
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        (int Status,JsonElement Body,ReadOnlyMemory<byte> Data) Call(object input,bool binary=true,bool bulk=true) {
            var request=JsonSerializer.SerializeToElement(input,JsonDefaults.Options);var reply=gallery.Handle(request,binary,bulk);
            return reply.Body is Tab5RpcDataBody raw?(reply.Status,JsonSerializer.SerializeToElement(raw.Metadata,JsonDefaults.Options),raw.Data):(reply.Status,JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options),ReadOnlyMemory<byte>.Empty);
        }
        foreach(string category in new[]{"painting","calligraphy"}) {
            int count=catalog.Count(w=>w.Category==category);Check(count>=2,"Daily rotation needs at least two artworks");
            Check(catalog.Where(w=>w.Category==category).Select(w=>w.WorkId??w.Source).Distinct().Count()==count,"Different views of one work must not count as separate daily artworks");
            var start=new DateOnly(2026,1,1);var ids=new HashSet<string>();
            for(int day=0;day<count;day++) {
                string date=start.AddDays(day).ToString("yyyy-MM-dd");
                var manifest=Call(new{op="manifest",category,date,frame=0});Check(manifest.Status==200,"Missing artwork");
                string id=manifest.Body.GetProperty("id").GetString()!;Check(ids.Add(id),"Repeated work before completing collection");
                var repeat=Call(new{op="manifest",category,date,frame=0});Check(repeat.Body.GetProperty("id").GetString()==id,"Unstable same-day selection");
                int frames=manifest.Body.GetProperty("frames").GetInt32();
                foreach(string orientation in new[]{"landscape","portrait"}) for(int frame=0;frame<frames;frame++) {
                    manifest=Call(new{op="manifest",category,date,frame,orientation});int size=manifest.Body.GetProperty("size").GetInt32();string sha=manifest.Body.GetProperty("sha256").GetString()!;
                    Check(manifest.Body.GetProperty("orientation").GetString()==orientation&&manifest.Body.GetProperty("id").GetString()==id,"Orientation changed daily artwork or returned wrong layout");
                    using var stream=new MemoryStream();
                    for(int offset=0;offset<size;offset+=49152) {
                        var chunk=Call(new{op="read",category,date,frame,orientation,sha256=sha,offset,count=49152});
                        Check(chunk.Status==200&&chunk.Body.GetProperty("offset").GetInt32()==offset,"Chunk offset mismatch");stream.Write(chunk.Data.Span);
                    }
                    Check(stream.Length==size&&Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()==sha,"Incomplete or corrupted image");
                    stream.Position=0;using var image=Image.FromStream(stream);Check(image.Width==1280&&image.Height==720,"Frame must match native display");
                    Check(Call(new{op="read",category,date,frame,orientation,sha256="stale",offset=0,count=100}).Status==409,"Changed artwork accepted");
                    Check(Call(new{op="read",category,date,frame,orientation,sha256=sha,offset=-1,count=100}).Status==400,"Invalid range accepted");
                    Check(Call(new{op="read",category,date,frame,orientation,sha256=sha,offset=0,count=49152},true,false).Status==400,"Oversized non-bulk reply accepted");
                }
                Check(Call(new{op="manifest",category,date,frame=frames}).Status==400,"Invalid frame accepted");
            }
            foreach(var date in new[]{new DateOnly(2024,2,28),new DateOnly(2024,2,29),new DateOnly(2026,12,31)})
                Check(Tab5Gallery.DayIndex(date.AddDays(1),count)==(Tab5Gallery.DayIndex(date,count)+1)%count,"Date boundary skipped/repeated work");
            if(count>=366) {
                foreach(int year in new[]{2024,2026,2028}) {
                    var first=new DateOnly(year,1,1);var last=new DateOnly(year,12,31);
                    var selected=Enumerable.Range(0,last.DayNumber-first.DayNumber+1).Select(day=>Tab5Gallery.DayIndex(first.AddDays(day),count));
                    Check(selected.Distinct().Count()==last.DayNumber-first.DayNumber+1,"Repeated artwork within a calendar year");
                }
            } else Console.WriteLine($"GALLERY_YEAR_PENDING {category}: {count}/366 distinct works; sample transport success is not annual acceptance");
        }
        Check(Call(new{op="manifest",category="../private",date="2026-10-06"}).Status==400,"Unknown category accepted");
        Check(Call(new{op="manifest",category="painting",date="2026-02-30"}).Status==400,"Invalid calendar date accepted");
        Check(Call(new{op="manifest",category="painting",date="2026-10-07",orientation="../portrait"}).Status==400,"Invalid orientation accepted");
        Console.WriteLine($"GALLERY_SELF_TEST_OK {catalog.Length} artworks: both orientations, same daily work, leap/year boundaries, all native JPEG frames, chunk/hash integrity, stale/range rejection");
    }
}
