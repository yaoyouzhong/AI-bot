using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AIBotBridge;

internal sealed record GalleryUpdate(string Category,string Version,Version Number,UpdateAsset? Package,UpdateAsset? Checksums);
internal sealed record PreparedGalleryUpdate(GalleryUpdate Release,string File,string Sha256);
internal sealed record GalleryDownloadIdentity(int SchemaVersion,string Category,string Version,string Name,long Size,string Sha256);
internal static class GalleryUpdateCatalog
{
    internal static bool TryNumber(string text,out Version number) {
        number=new(0,0,0);
        if(!Regex.IsMatch(text,@"^\d{4}\.\d{2}\.\d{2}(?:\.\d+)?$",RegexOptions.CultureInvariant)||
           !DateOnly.TryParseExact(text[..10],"yyyy.MM.dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||
           !Version.TryParse(text,out var parsed))return false;
        number=new(parsed.Major,parsed.Minor,parsed.Build,Math.Max(0,parsed.Revision));return true;
    }
    internal static Dictionary<string,GalleryUpdate> Read(JsonElement releases) {
        if(releases.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("发布列表格式错误。");
        var result=new Dictionary<string,GalleryUpdate>(StringComparer.Ordinal);
        foreach(var release in releases.EnumerateArray()) {
            if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean())continue;
            string tag=release.GetProperty("tag_name").GetString()??"";
            var assets=release.GetProperty("assets").EnumerateArray().ToArray();
            foreach(var asset in assets) {
                string name=asset.GetProperty("name").GetString()??"";
                bool canonical=name.StartsWith("AI-bot-gallery-",StringComparison.Ordinal);
                var match=Regex.Match(name,canonical?@"^AI-bot-gallery-(painting|calligraphy)-standard-(.+)\.zip$":@"^AI-bot-Daily(Painting|Calligraphy)-Curated-(.+)\.zip$",RegexOptions.CultureInvariant);
                if(!match.Success||!TryNumber(match.Groups[2].Value,out var number))continue;
                string category=match.Groups[1].Value.ToLowerInvariant(),version=match.Groups[2].Value;
                var package=ComponentUpdateCatalog.ReadAsset(tag,asset);
                if(result.TryGetValue(category,out var old)&&(old.Number>number||old.Number==number&&(!canonical||package is null||old.Package?.Name.StartsWith("AI-bot-gallery-",StringComparison.Ordinal)==true)))continue;
                var checksum=assets.Where(a=>a.GetProperty("name").GetString()==name+".sha256"||a.GetProperty("name").GetString()=="SHA256SUMS.txt")
                    .OrderBy(a=>a.GetProperty("name").GetString()==name+".sha256"?0:1).Select(a=>ComponentUpdateCatalog.ReadAsset(tag,a)).FirstOrDefault(a=>a is not null);
                result[category]=new(category,version,number,package,checksum);
            }
        }
        return result;
    }
    internal static string? Blocked(GalleryPackManifest? installed,GalleryUpdate? release) {
        if(release is null)return "暂无可确认的正式精选集";
        if(installed is not null&&TryNumber(installed.Version,out var number)) {
            if(number==release.Number)return "已是最新版本";
            if(number>release.Number)return "本机版本较新";
        }
        return release.Package is null||release.Checksums is null||release.Package.Size is <1 or >2L*1024*1024*1024?"发布包缺少有效下载或校验信息":null;
    }
}
