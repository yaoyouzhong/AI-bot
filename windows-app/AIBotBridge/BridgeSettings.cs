using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class BridgeSettings
{
    private static readonly HashSet<string> LegacyAllowList = new(StringComparer.Ordinal)
    {
        "device_host", "display_mode", "display_cycle_enabled", "display_cycle_interval_seconds",
        "display_cycle_pages", "domestic_provider", "qweather_api_host", "kimi_membership",
        "screensaver_previous_mode", "screensaver_timeout_minutes", "serial_port",
        "stock_symbols", "weather_animation", "weather_auto_location", "weather_city",
        "weather_latitude", "weather_longitude"
    };

    private readonly Dictionary<string, string> _values;
    private static readonly object FileSync = new();

    private BridgeSettings(Dictionary<string, string> values)
    {
        _values = values;
    }

    internal static BridgeSettings CreatePublicSelfTestSettings() => new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["weather_city"] = "北京",
        ["weather_latitude"] = "39.904200",
        ["weather_longitude"] = "116.407400",
        ["stock_symbols"] = "sh000001"
    });

    internal static BridgeSettings Load()
    {
        var appData = AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var currentPath = Path.Combine(appData, "AI-bot", "settings.json");
        var current = Read(currentPath);
        var legacy = Read(Path.Combine(appData, "AIClockBridge", "settings.json"));
        var merged = legacy
            .Where(pair => LegacyAllowList.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in current.Where(pair => LegacyAllowList.Contains(pair.Key))) merged[pair.Key] = pair.Value;
        return new BridgeSettings(merged);
    }

    internal string Get(string key, string fallback = "") =>
        _values.TryGetValue(key, out var value) ? value : fallback;

    internal double? GetDouble(string key)
    {
        return double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : null;
    }

    internal string[] GetList(string key, string fallback)
    {
        return Get(key, fallback).Replace('，', ',')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal bool SaveEditable(IReadOnlyDictionary<string, string> editable, out string error,
        string? directoryOverride = null)
    {
        lock (FileSync) return SaveEditableCore(editable, out error, directoryOverride);
    }

    private bool SaveEditableCore(IReadOnlyDictionary<string, string> editable, out string error,
        string? directoryOverride)
    {
        var values = _values
            .Where(pair => LegacyAllowList.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in editable)
        {
            if (!LegacyAllowList.Contains(pair.Key))
            {
                error = $"不允许保存设置项：{pair.Key}";
                return false;
            }
            values[pair.Key] = pair.Value;
        }

        try
        {
            var directory = directoryOverride ?? Path.Combine(
                AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "settings.json");
            // Multiple settings windows/services must not overwrite newer fields
            // using their older in-memory settings snapshot.
            foreach (var pair in Read(path))
                if (LegacyAllowList.Contains(pair.Key) && !editable.ContainsKey(pair.Key)) values[pair.Key] = pair.Value;
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(values, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temporary, path, true);
            _values.Clear();
            foreach (var pair in values)
                _values[pair.Key] = pair.Value;
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "无法写入 %APPDATA%\\AI-bot\\settings.json。";
            return false;
        }
    }

    internal static BridgeSettings LoadCurrentFromDirectory(string directory) =>
        new(Read(Path.Combine(directory, "settings.json")));

    private static Dictionary<string, string> Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return new(StringComparer.Ordinal);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(StringComparer.Ordinal);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String)
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }
}
