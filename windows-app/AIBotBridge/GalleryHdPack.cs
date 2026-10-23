using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace AIBotBridge;

// Full HD bundles contain a validated standard gallery plus desktop-only sources.
internal static class GalleryHdPack
{
    internal static string Root=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","DesktopArt");
    internal sealed record InstalledInfo(string Category,string Edition,int Works,string Directory);
    internal static string? Current(string category,string? root=null){
        if(category is not ("painting" or "calligraphy"))return null;
        try{string name=File.ReadAllText(Path.Combine(root??Root,category+".current")).Trim();
            return Guid.TryParseExact(name,"N",out _)?Path.Combine(root??Root,name):null;
        }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
    }
    internal static InstalledInfo? Installed(string category,string? root=null){
        try{var dir=Current(category,root);if(dir is null)return null;using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir,"catalog.json")));
            return new(category,doc.RootElement.GetProperty("edition").GetString()!,doc.RootElement.GetProperty("works").GetArrayLength(),dir);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException){return null;}
    }
    internal static string SafePath(string root,string relative){
        var pieces=relative.Split('/');
        if(relative.Length>240||pieces.Any(p=>p.Length==0||p is "." or ".."||p.EndsWith('.')||p.EndsWith(' ')||p.Any(c=>c<32||"\\:<>\"|?*".Contains(c))||System.Text.RegularExpressions.Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))throw new InvalidDataException("素材路径不合法。");
        return Path.Combine(root,Path.Combine(pieces));
    }
    internal static InstalledInfo Install(string zip,string? root=null,IProgress<int>? progress=null,CancellationToken cancellation=default){
        string galleryRoot=root is null?GalleryPack.Root:Path.Combine(root,"standard-gallery");
        root=Path.GetFullPath(root??Root);Directory.CreateDirectory(root);
        string name=Guid.NewGuid().ToString("N"),stage=Path.Combine(root,name),pointerTemp=Path.Combine(root,name+".tmp");bool committed=false;
        try{
            using var archive=ZipFile.OpenRead(zip);
            if(archive.Entries.Count is <4 or >20000)throw new InvalidDataException("高清素材包文件数量不正确。");
            string prefix=archive.Entries[0].FullName.Split('/')[0]+"/";SafePath(root,prefix.TrimEnd('/'));
            var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);long total=0;
            foreach(var entry in archive.Entries){
                if(!entry.FullName.StartsWith(prefix,StringComparison.Ordinal)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("素材包目录不正确。");
                string relative=entry.FullName[prefix.Length..];SafePath(stage,relative);
                if(!entries.TryAdd(relative,entry)||entry.Length<0||entry.Length>2L*1024*1024*1024)throw new InvalidDataException("素材文件重复或过大。");
                total=checked(total+entry.Length);
            }
            if(total>16L*1024*1024*1024)throw new InvalidDataException("素材包超过 16 GiB 上限。");
            if(!entries.TryGetValue("SHA256SUMS.txt",out var sums)||sums.Length>4*1024*1024)throw new InvalidDataException("缺少校验清单。");
            var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            using(var reader=new StreamReader(sums.Open()))while(reader.ReadLine() is {} line){
                if(line.Length<67||line.Substring(64,2)!="  "||!line[..64].All(Uri.IsHexDigit)||!hashes.TryAdd(line[66..],line[..64]))throw new InvalidDataException("校验清单格式不正确。");
            }
            if(hashes.Count!=entries.Count-1||hashes.Keys.Any(k=>!entries.ContainsKey(k)||k.Equals("SHA256SUMS.txt",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("素材与校验清单不一致。");
            Directory.CreateDirectory(stage);long done=0;int reported=-1;byte[] buffer=new byte[1024*1024];
            foreach(var (relative,entry) in entries){
                cancellation.ThrowIfCancellationRequested();string extracted=SafePath(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(extracted)!);
                using var input=entry.Open();using var output=File.Create(extracted);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long bytes=0;int count;
                while((count=input.Read(buffer))>0){cancellation.ThrowIfCancellationRequested();bytes+=count;if(bytes>entry.Length)throw new InvalidDataException("素材长度不正确。");output.Write(buffer,0,count);hash.AppendData(buffer,0,count);done+=count;int percent=(int)(done*85/Math.Max(1,total));if(percent!=reported){reported=percent;progress?.Report(percent);}}
                if(bytes!=entry.Length||(relative!="SHA256SUMS.txt"&&!Convert.ToHexString(hash.GetHashAndReset()).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("素材校验失败："+relative);
            }
            byte[] ReadMetadata(string file){string path=Path.Combine(stage,file);if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("素材索引过大。");return File.ReadAllBytes(path);}
            using var catalog=JsonDocument.Parse(ReadMetadata("catalog.json"));var c=catalog.RootElement;
            string category=c.GetProperty("category").GetString()!,edition=c.GetProperty("edition").GetString()!;
            if(c.GetProperty("schema").GetInt32()!=1||category is not ("painting" or "calligraphy")||string.IsNullOrWhiteSpace(edition)||edition.Length>64)throw new InvalidDataException("不支持的高清素材包。");
            var index=JsonSerializer.Deserialize<GalleryDesktopImages.Index>(ReadMetadata("desktop-index.relative.json"),JsonDefaults.Options)??throw new InvalidDataException("缺少高清索引。");
            if(index.Schema!=1||index.Sources.Count is <1 or >10000||index.Pages.Length is <1 or >10000||index.Pages.Any(p=>p.Category!=category))throw new InvalidDataException("高清索引不正确。");
            foreach(var source in index.Sources.Values){SafePath(stage,source.Path);if(!hashes.TryGetValue(source.Path,out var digest)||!digest.Equals(source.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("高清索引与素材不一致。");}
            var mapped=index with{Sources=index.Sources.ToDictionary(p=>p.Key,p=>p.Value with{Path=SafePath(stage,p.Value.Path)})};
            string resolved=Path.Combine(stage,"desktop-originals.json");File.WriteAllBytes(resolved,JsonSerializer.SerializeToUtf8Bytes(mapped,JsonDefaults.Options));
            var validator=new GalleryDesktopImages(resolved);if(validator.LastFailure is not null)throw new InvalidDataException("高清页面索引校验失败："+validator.LastFailure);
            int works=c.GetProperty("works").GetArrayLength();if(works is <1 or >10000)throw new InvalidDataException("作品数量不正确。");
            if(!entries.ContainsKey("standard.zip"))throw new InvalidDataException("这是旧版高清素材集，请选择包含作品和导读的高清完整包。");
            string prepared=Path.Combine(stage,"standard");
            var standard=GalleryPack.Install(Path.Combine(stage,"standard.zip"),prepared,cancellation:cancellation,notify:false);
            if(standard.Category!=category||standard.Works!=works||standard.SchemaVersion!=2)throw new InvalidDataException("高清图片与内置图库不一致。");
            var standardWorks=JsonSerializer.Deserialize<Tab5Gallery.Artwork[]>(File.ReadAllBytes(Path.Combine(prepared,category,"catalog.json")),JsonDefaults.Options)!;
            var lookup=standardWorks.ToDictionary(w=>w.Id);
            if(index.Pages.Length!=standardWorks.Sum(w=>w.Frames.Length))throw new InvalidDataException("高清分页不完整。");
            foreach(var page in index.Pages){
                if(!lookup.TryGetValue(page.Id,out var work)||page.WorkId!=(work.WorkId??work.Source)||!work.Frames.Contains(page.Frame)||!standard.Files[page.Frame].Equals(page.PreviewSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("高清页面与内置图库不匹配。");
            }
            progress?.Report(98);cancellation.ThrowIfCancellationRequested();Directory.CreateDirectory(galleryRoot);
            string target=Path.Combine(galleryRoot,category),backup=target+".backup-"+name;
            File.WriteAllText(pointerTemp,name);
            lock(GalleryPack.Gate){
                cancellation.ThrowIfCancellationRequested();bool previous=Directory.Exists(target);if(previous)Directory.Move(target,backup);
                bool moved=false;
                try{
                    Directory.Move(Path.Combine(prepared,category),target);moved=true;
                    File.Move(pointerTemp,Path.Combine(root,category+".current"),true);committed=true;GalleryPack.NotifyInstalled();
                }catch{if(moved)Directory.Move(target,Path.Combine(prepared,category));if(previous)Directory.Move(backup,target);throw;}
            }
            progress?.Report(100);
            return new(category,edition,works,stage);
        }finally{if(!committed&&Directory.Exists(stage))Directory.Delete(stage,true);if(File.Exists(pointerTemp))File.Delete(pointerTemp);}
    }
}
