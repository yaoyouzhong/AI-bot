using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed record GalleryPackManifest(int SchemaVersion,string Version,string Category,int Works,int Frames,Dictionary<string,string> Files);
internal static class GalleryPack
{
    internal static string Root=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","DailyArt");
    internal static bool Category(string value)=>value is "painting" or "calligraphy";
    internal static string CategoryName(string value)=>value=="painting"?"每日名画":"每日书法";
    internal static GalleryPackManifest? Installed(string category,string? root=null) {
        if(!Category(category))throw new ArgumentException("Unknown gallery category");
        string path=Path.Combine(root??Root,category,"PACK.json");
        if(!File.Exists(path))return null;
        try{return JsonSerializer.Deserialize<GalleryPackManifest>(File.ReadAllText(path),JsonDefaults.Options);}
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){return null;}
    }
    internal static GalleryPackManifest Install(string archive,string? root=null,IProgress<int>? progress=null,CancellationToken cancellation=default) {
        string destination=Path.GetFullPath(root??Root);Directory.CreateDirectory(destination);
        string stage=Path.Combine(destination,".install-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try {
            using var zip=ZipFile.OpenRead(archive);
            if(zip.Entries.Count is <5 or >8192)throw new InvalidDataException("图库文件数量异常。");
            if(zip.Entries.Select(x=>x.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count)throw new InvalidDataException("图库含重复路径。");
            foreach(var entry in zip.Entries) {
                if(!Regex.IsMatch(entry.FullName,@"^[A-Za-z0-9][A-Za-z0-9_.-]*$",RegexOptions.CultureInvariant)||entry.FullName.Contains("..")||
                   ((entry.ExternalAttributes>>16)&0xf000)==0xa000||entry.Length<1||entry.Length>16*1024*1024)
                    throw new InvalidDataException("图库含无效路径或文件。");
            }
            if(zip.Entries.Sum(x=>x.Length)>2L*1024*1024*1024)throw new InvalidDataException("图库解压大小超限。");
            var entryManifest=zip.GetEntry("PACK.json")??throw new InvalidDataException("不是 AI-bot 图库包。");
            if(entryManifest.Length>2*1024*1024)throw new InvalidDataException("图库清单过大。");
            GalleryPackManifest manifest;
            using(var input=entryManifest.Open())manifest=JsonSerializer.Deserialize<GalleryPackManifest>(input,JsonDefaults.Options)??throw new InvalidDataException("图库清单为空。");
            if(manifest.SchemaVersion!=1||!Category(manifest.Category)||!Regex.IsMatch(manifest.Version??"",@"^\d{4}\.\d{2}\.\d{2}(?:\.\d+)?$")||
               manifest.Works is <1 or >2000||manifest.Frames is <1 or >8000||manifest.Files is null||
               !manifest.Files.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(zip.Entries.Where(x=>x.FullName!="PACK.json").Select(x=>x.FullName)))
                throw new InvalidDataException("图库清单与内容不一致。");
            foreach(string required in new[]{"catalog.json","provenance.json","selection.json","LICENSE","THIRD_PARTY_NOTICES.md"})
                if(!manifest.Files.ContainsKey(required))throw new InvalidDataException("图库缺少目录或来源声明。");
            int done=0;
            foreach(var entry in zip.Entries) {
                cancellation.ThrowIfCancellationRequested();
                string file=Path.Combine(stage,entry.FullName);
                using(var input=entry.Open())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write)) {
                    byte[] buffer=new byte[65536];long written=0;
                    for(int count;(count=input.Read(buffer))>0;) {
                        cancellation.ThrowIfCancellationRequested();written+=count;
                        if(written>entry.Length)throw new InvalidDataException("图库文件解压长度异常。");
                        output.Write(buffer,0,count);
                    }
                    if(written!=entry.Length)throw new InvalidDataException("图库文件不完整。");
                }
                if(entry.FullName!="PACK.json") {
                    using var input=File.OpenRead(file);string hash=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
                    if(!string.Equals(hash,manifest.Files[entry.FullName],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图库校验失败："+entry.FullName);
                }
                progress?.Report(++done*95/zip.Entries.Count);
            }
            var works=JsonSerializer.Deserialize<Tab5Gallery.Artwork[]>(File.ReadAllText(Path.Combine(stage,"catalog.json")),JsonDefaults.Options)??[];
            if(works.Length!=manifest.Works||works.Select(w=>w.Id).Distinct().Count()!=works.Length||works.Any(w=>w.Category!=manifest.Category||string.IsNullOrWhiteSpace(w.Title)||string.IsNullOrWhiteSpace(w.Author)||w.Frames is null||w.Frames.Length is <1 or >64||w.PortraitFrames is null||w.PortraitFrames.Length!=w.Frames.Length))
                throw new InvalidDataException("作品目录无效。");
            var images=works.SelectMany(w=>w.Frames.Concat(w.PortraitFrames!)).ToArray();
            if(images.Length!=manifest.Frames||images.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=images.Length||
               !images.ToHashSet(StringComparer.Ordinal).SetEquals(manifest.Files.Keys.Where(x=>x.EndsWith(".jpg",StringComparison.Ordinal))))
                throw new InvalidDataException("作品分页与图片清单不一致。");
            foreach(string name in images) {
                cancellation.ThrowIfCancellationRequested();
                if(new FileInfo(Path.Combine(stage,name)).Length>1048576)throw new InvalidDataException("图库图片超过设备传输上限。");
                // GDI+ filename APIs can report OutOfMemory for long Windows
                // profile paths. Let .NET open the path, then validate the stream.
                using var input=File.OpenRead(Path.Combine(stage,name));
                using var image=Image.FromStream(input,useEmbeddedColorManagement:false,validateImageData:true);
                if(image.Width!=1280||image.Height!=720||image.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.Jpeg.Guid)throw new InvalidDataException("图库图片尺寸不兼容。");
            }
            cancellation.ThrowIfCancellationRequested();
            string target=Path.Combine(destination,manifest.Category),backup=target+".backup-"+Guid.NewGuid().ToString("N");
            bool previous=Directory.Exists(target);if(previous)Directory.Move(target,backup);
            try{Directory.Move(stage,target);}catch{if(previous)Directory.Move(backup,target);throw;}
            // Previous installed data remains as a backup. Never remove user artwork.
            progress?.Report(100);return manifest;
        } finally {
            // Only this invocation's fresh staging directory can be removed.
            if(Directory.Exists(stage))Directory.Delete(stage,true);
        }
    }
}
