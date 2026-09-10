using System.Text.Json;

namespace AIBotBridge;

internal static class SnapshotCache
{
    internal static T? Load<T>(string name, string? legacyName = null) where T : class
    {
        var appData = AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var paths = new List<string> { Path.Combine(appData, "AI-bot", name) };
        if (legacyName is not null)
            paths.Add(Path.Combine(appData, "AIClockBridge", legacyName));

        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (value is not null) return value;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }
        return null;
    }

    internal static void Save<T>(string name, T value)
    {
        try
        {
            var directory = Path.Combine(
                AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options));
            File.Move(temporary, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A cache failure must not stop live data delivery.
        }
    }
}
