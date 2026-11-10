using System.Text.Json;

namespace AIBotBridge;

internal sealed record UpdateAsset(string Name, Uri Url, long Size);
internal sealed record ComponentUpdate(string Component, string Version, Version Number, string Notes,
    UpdateAsset? Package=null, UpdateAsset? Checksums=null);

internal static class ComponentUpdateCatalog
{
    internal static bool Prefer(ComponentUpdate candidate, ComponentUpdate previous) =>
        candidate.Number > previous.Number ||
        candidate.Number == previous.Number && candidate.Component == "esp8266" &&
        EspPackagePriority(candidate.Package) > EspPackagePriority(previous.Package);

    private static int EspPackagePriority(UpdateAsset? asset) => asset?.Name switch {
        null => -1,
        var name when name.EndsWith("-flash.zip",StringComparison.Ordinal) => 2,
        var name when name.EndsWith("-firmware.zip",StringComparison.Ordinal) => 1,
        _ => 0
    };

    internal static UpdateAsset? ReadAsset(string tag,JsonElement item) {
        string file=item.GetProperty("name").GetString()??"";
        if(!item.TryGetProperty("browser_download_url",out var url)||
           !Uri.TryCreate(url.GetString(),UriKind.Absolute,out var uri)||
           uri.Scheme!="https"||uri.Host!="github.com"||uri.Port!=443||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||
           uri.AbsolutePath!=$"/yaoyouzhong/AI-bot/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(file)}")return null;
        long size=item.TryGetProperty("size",out var bytes)&&bytes.TryGetInt64(out var length)?length:0;
        return new(file,uri,size);
    }
    internal static Dictionary<string, ComponentUpdate> Read(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array) throw new InvalidDataException("发布列表格式错误。");
        var result = new Dictionary<string, ComponentUpdate>(StringComparer.Ordinal);
        foreach (var release in releases.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            string notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "暂无更新说明。" : "暂无更新说明。";
            foreach (var (component, prefix, assetPrefix, assetSuffix, canonical) in new[] {
                ("bridge", "bridge-v", "AI-bot-bridge-v", "-windows-x64-setup.exe", true),
                ("esp8266", "esp8266-v", "AI-bot-esp8266-", "-flash.zip", true),
                ("esp8266", "esp8266-v", "AI-bot-esp8266-", "-firmware.zip", true),
                ("tab5", "tab5-v", "AI-bot-tab5-", "-upgrade.zip", true),
                ("bridge", "bridge-v", "AIBotBridge-", "-setup-win-x64.exe", false),
                ("esp8266", "esp8266-v", "AI-bot-", "-firmware-materials.zip", false),
                ("tab5", "tab5-v", "TAB5-upgrade-", ".zip", false) })
            {
                bool ownTag = tag.StartsWith(prefix, StringComparison.Ordinal);
                bool legacy = tag.StartsWith('v') && TryNumber(tag[1..], false, out _);
                if (!ownTag && !legacy) continue;
                foreach (var asset in release.GetProperty("assets").EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Length <= assetPrefix.Length + assetSuffix.Length) continue;
                    if (!name.StartsWith(assetPrefix, StringComparison.Ordinal) || !name.EndsWith(assetSuffix, StringComparison.Ordinal)) continue;
                    string version = name[assetPrefix.Length..^assetSuffix.Length];
                    if (!TryNumber(version, component == "tab5", out var number)) continue;
                    // Legacy bundle assets carry their own versions. A component tag must match its package.
                    if (ownTag && tag[prefix.Length..] != version) continue;
                    var package=ReadAsset(tag,asset);
                    bool compact=assetSuffix=="-flash.zip";
                    if(compact && (!ownTag || package is null || package.Size<=0 || package.Size>256L*1024*1024)) continue;
                    var checksum=release.GetProperty("assets").EnumerateArray()
                        .Where(a=>a.GetProperty("name").GetString()==name+".sha256"||!compact&&a.GetProperty("name").GetString()=="SHA256SUMS.txt")
                        .OrderBy(a=>a.GetProperty("name").GetString()==name+".sha256"?0:1)
                        .Select(a=>ReadAsset(tag,a)).FirstOrDefault(a=>a is not null&&(!compact||a.Size is >0 and <=256*1024));
                    if(compact && checksum is null) continue;
                    var candidate=new ComponentUpdate(component,version,number,notes,package,checksum);
                    if (!result.TryGetValue(component, out var previous) || Prefer(candidate,previous) ||
                        component!="esp8266" && number == previous.Number && canonical && package is not null && previous.Package?.Name.StartsWith(assetPrefix,StringComparison.Ordinal)!=true)
                        result[component] = candidate;
                }
            }
        }
        return result;
    }

    internal static bool TryNumber(string text, bool tab5, out Version number)
    {
        if (tab5 && text.EndsWith("-ui", StringComparison.Ordinal)) text = text[..^3];
        number = new Version(0, 0, 0);
        if (text.Split('.').Length != 3 || !Version.TryParse(text, out var parsed)) return false;
        number = parsed;
        return true;
    }
}
