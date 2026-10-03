using System.Text.Json;

namespace AIBotBridge;

internal sealed record ComponentUpdate(string Component, string Version, Version Number, string Notes);

internal static class ComponentUpdateCatalog
{
    internal static Dictionary<string, ComponentUpdate> Read(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array) throw new InvalidDataException("发布列表格式错误。");
        var result = new Dictionary<string, ComponentUpdate>(StringComparer.Ordinal);
        foreach (var release in releases.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            string notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "暂无更新说明。" : "暂无更新说明。";
            foreach (var (component, prefix, assetPrefix, assetSuffix) in new[] {
                ("bridge", "bridge-v", "AIBotBridge-", "-setup-win-x64.exe"),
                ("esp8266", "esp8266-v", "AI-bot-", "-firmware-materials.zip"),
                ("tab5", "tab5-v", "TAB5-upgrade-", ".zip") })
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
                    if (!result.TryGetValue(component, out var previous) || number > previous.Number)
                        result[component] = new(component, version, number, notes);
                }
            }
        }
        return result;
    }

    private static bool TryNumber(string text, bool tab5, out Version number)
    {
        if (tab5 && text.EndsWith("-ui", StringComparison.Ordinal)) text = text[..^3];
        number = new Version(0, 0, 0);
        if (text.Split('.').Length != 3 || !Version.TryParse(text, out var parsed)) return false;
        number = parsed;
        return true;
    }
}
