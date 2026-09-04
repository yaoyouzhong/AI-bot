using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly BridgeSettings _settings;
    private readonly bool _persistCache;
    private readonly object _sync = new();
    private WeatherSnapshot? _snapshot;

    internal WeatherService(BridgeSettings settings, bool persistCache = true)
    {
        _settings = settings;
        _persistCache = persistCache;
        var cached = persistCache ? SnapshotCache.Load<WeatherSnapshot>("weather-cache.json") : null;
        if (cached is not null && cached.UpdatedAt != default)
            _snapshot = cached with { Stale = true };
    }

    internal WeatherSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var location = await ResolveLocationAsync(cancellationToken);
            if (location is null) return;

            var coordinate = FormattableString.Invariant(
                $"latitude={location.Value.Latitude:F6}&longitude={location.Value.Longitude:F6}");
            var forecastUrl = "https://api.open-meteo.com/v1/forecast?" + coordinate +
                "&current=temperature_2m,relative_humidity_2m,weather_code" +
                "&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto";
            var airUrl = "https://air-quality-api.open-meteo.com/v1/air-quality?" + coordinate +
                "&current=us_aqi,pm2_5";

            var forecastTask = Http.GetStringAsync(forecastUrl, cancellationToken);
            var airTask = Http.GetStringAsync(airUrl, cancellationToken);
            await Task.WhenAll(forecastTask, airTask);
            var next = Parse(forecastTask.Result, airTask.Result, location.Value.Label);
            lock (_sync) _snapshot = next;
            if (_persistCache) SnapshotCache.Save("weather-cache.json", next);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            lock (_sync)
                if (_snapshot is not null) _snapshot = _snapshot with { Stale = true };
        }
    }

    internal static WeatherSnapshot Parse(string forecastJson, string airJson, string city)
    {
        using var forecast = JsonDocument.Parse(forecastJson);
        using var air = JsonDocument.Parse(airJson);
        var current = forecast.RootElement.GetProperty("current");
        var daily = forecast.RootElement.GetProperty("daily");
        var airCurrent = air.RootElement.TryGetProperty("current", out var currentAir)
            ? currentAir : default;
        var code = current.GetProperty("weather_code").GetInt32();

        return new WeatherSnapshot(
            City: city,
            Condition: ConditionFor(code),
            Temperature: current.GetProperty("temperature_2m").GetDouble(),
            High: FirstNumber(daily, "temperature_2m_max"),
            Low: FirstNumber(daily, "temperature_2m_min"),
            Humidity: current.GetProperty("relative_humidity_2m").GetInt32(),
            WeatherCode: code,
            Pm25: OptionalNumber(airCurrent, "pm2_5"),
            AirQualityIndex: OptionalInteger(airCurrent, "us_aqi"),
            Source: "Open-Meteo",
            UpdatedAt: DateTimeOffset.UtcNow,
            Stale: false);
    }

    private async Task<(double Latitude, double Longitude, string Label)?> ResolveLocationAsync(
        CancellationToken cancellationToken)
    {
        var latitude = _settings.GetDouble("weather_latitude");
        var longitude = _settings.GetDouble("weather_longitude");
        var city = _settings.Get("weather_city").Trim();
        if (latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
            return (latitude.Value, longitude.Value, city.Length > 0 ? city : "当前位置");
        if (city.Length == 0) return null;

        var url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=zh&format=json&name=" +
            Uri.EscapeDataString(city);
        using var document = JsonDocument.Parse(await Http.GetStringAsync(url, cancellationToken));
        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;
        var result = results[0];
        var label = result.TryGetProperty("name", out var name) ? name.GetString() ?? city : city;
        return (result.GetProperty("latitude").GetDouble(),
            result.GetProperty("longitude").GetDouble(), label);
    }

    private static double FirstNumber(JsonElement parent, string property)
    {
        var values = parent.GetProperty(property);
        if (values.GetArrayLength() == 0) throw new JsonException($"{property} is empty.");
        return values[0].GetDouble();
    }

    private static double? OptionalNumber(JsonElement parent, string property)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }

    private static int? OptionalInteger(JsonElement parent, string property)
    {
        var value = OptionalNumber(parent, property);
        return value is null ? null : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private static string ConditionFor(int code) => code switch
    {
        0 => "晴",
        1 or 2 => "多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 or 56 or 57 => "毛毛雨",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "雨",
        71 or 73 or 75 or 77 or 85 or 86 => "雪",
        95 or 96 or 99 => "雷雨",
        _ => "未知"
    };
}
