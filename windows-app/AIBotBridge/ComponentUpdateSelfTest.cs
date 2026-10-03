using System.Text.Json;

namespace AIBotBridge;

internal static class ComponentUpdateSelfTest
{
    internal static void Run()
    {
        object Release(string tag, string[] assets, bool draft = false, bool prerelease = false) =>
            new { tag_name = tag, body = tag, draft, prerelease, assets = assets.Select(name => new { name }).ToArray() };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new[] {
            Release("esp8266-v9.0.0", ["AI-bot-9.0.0-firmware-materials.zip"]),
            Release("tab5-v0.2.90-ui", ["TAB5-upgrade-0.2.90-ui.zip"]),
            Release("bridge-v0.6.0", ["AIBotBridge-0.6.0-setup-win-x64.exe"]),
            Release("bridge-v0.4.1", ["AIBotBridge-0.4.1-setup-win-x64.exe"]),
            Release("bridge-v8.0.0", ["AIBotBridge-8.0.0-setup-win-x64.exe"], prerelease: true),
            Release("bridge-v7.0.0", ["AIBotBridge-7.0.0-setup-win-x64.exe"], draft: true),
            Release("bridge-v6.0.0", ["AIBotBridge-5.0.0-setup-win-x64.exe"]),
            Release("v0.5.0", ["AIBotBridge-0.5.0-setup-win-x64.exe", "AI-bot-0.5.0-firmware-materials.zip", "TAB5-upgrade-0.2.89-ui.zip"])
        }));
        var found = ComponentUpdateCatalog.Read(doc.RootElement);
        if (found["bridge"].Version != "0.6.0" || found["esp8266"].Version != "9.0.0" || found["tab5"].Version != "0.2.90-ui")
            throw new InvalidOperationException("Component releases must not replace one another; drafts, mismatched packages and older backports must not win.");
        using var legacy = JsonDocument.Parse(JsonSerializer.Serialize(new[] {
            Release("v0.5.0", ["AIBotBridge-0.5.0-setup-win-x64.exe", "AI-bot-0.4.0-firmware-materials.zip", "TAB5-upgrade-0.2.89-ui.zip"])
        }));
        found = ComponentUpdateCatalog.Read(legacy.RootElement);
        if (found["bridge"].Version != "0.5.0" || found["esp8266"].Version != "0.4.0" || found["tab5"].Version != "0.2.89-ui")
            throw new InvalidOperationException("Legacy bundles must retain each component's actual package version.");
        Console.WriteLine("COMPONENT_UPDATE_SELF_TEST_OK independent versions, stable selection, legacy bundles");
    }
}
