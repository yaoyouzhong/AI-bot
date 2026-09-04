using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class BridgeSettings
{
    private static readonly HashSet<string> LegacyAllowList = new(StringComparer.Ordinal)
    {
        "device_host", "display_cycle_enabled", "display_cycle_interval_seconds",
        "display_cycle_pages", "domestic_provider", "qweather_api_host",
        "screensaver_previous_mode", "screensaver_timeout_minutes", "serial_port",
        "stock_symbols", "weather_animation", "weather_auto_location", "weather_city",
        "weather_latitude", "weather_longitude"
    };

    private readonly Dictionary<string, string> _values;

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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var currentPath = Path.Combine(appData, "AI-bot", "settings.json");
        var current = Read(currentPath);
        if (current.Count > 0)
            return new BridgeSettings(current);

        var legacy = Read(Path.Combine(appData, "AIClockBridge", "settings.json"));
        return new BridgeSettings(legacy
            .Where(pair => LegacyAllowList.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
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
