using System.Text.Json;

namespace AIBotBridge;

internal static class WeatherMigrationPreview
{
    // Explicit comparison tool only: read public display data from the running
    // legacy bridge. Never starts a bridge, opens USB, or changes user settings.
    internal static async Task RunAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var json = JsonDocument.Parse(await http.GetStringAsync("http://127.0.0.1:8765/weather"));
        var w = json.RootElement;
        var now = DateTimeOffset.Now;
        var weather = new WeatherSnapshot(w.GetProperty("city").GetString()!, w.GetProperty("condition").GetString()!,
            w.GetProperty("temp").GetDouble(), w.GetProperty("high").GetDouble(), w.GetProperty("low").GetDouble(),
            w.GetProperty("humidity").GetInt32(), w.GetProperty("icon").GetInt32(), w.GetProperty("pm25").GetDouble(),
            null, w.GetProperty("source").GetString()!, DateTimeOffset.FromUnixTimeSeconds(w.GetProperty("updated_utc").GetInt64()),
            w.GetProperty("stale").GetBoolean(),
            w.GetProperty("air").GetString(),
            w.TryGetProperty("animation", out var animation) ? animation.GetString() ?? "robot" : "robot",
            AnimationIcon: w.GetProperty("icon").GetInt32(), UtcOffsetSeconds: w.GetProperty("utc_offset_s").GetInt32());
        var status = new StatusSnapshot(1, now.ToString("HH:mm:ss"), now.ToUnixTimeSeconds(),
            w.GetProperty("utc_offset_s").GetInt32(), now, new("offline", null), new("offline", null), Weather: weather);
        using var bitmap = new Bitmap(240, 240);
        using (var g = Graphics.FromImage(bitmap)) WeatherSceneRenderer.Draw(g, status, w.GetProperty("air").GetString());
        var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "weather-migration-candidate.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        bitmap.Save(output);
        Console.WriteLine("WEATHER_LAYOUT_PREVIEW " + output);
        Console.WriteLine("OFFLINE_MIRROR_CANDIDATE: legacy display data, desktop mirror layout; no device/app replaced. Device animation remains in firmware.");
    }
}
