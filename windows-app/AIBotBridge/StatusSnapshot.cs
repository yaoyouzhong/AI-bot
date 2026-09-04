using System.Text.Json;

namespace AIBotBridge;

internal sealed record ToolState(string State, long? AgeSeconds);

internal sealed record WeatherSnapshot(
    string City,
    string Condition,
    double Temperature,
    double High,
    double Low,
    int Humidity,
    int WeatherCode,
    double? Pm25,
    int? AirQualityIndex,
    string Source,
    DateTimeOffset UpdatedAt,
    bool Stale);

internal sealed record StockQuote(
    string Symbol,
    string Code,
    string Name,
    string Price,
    string ChangePercent,
    int Trend);

internal sealed record StockSnapshot(
    IReadOnlyList<StockQuote> Quotes,
    DateTimeOffset UpdatedAt,
    bool Stale);

internal sealed record ProviderQuotaSnapshot(
    string Provider,
    string? Plan,
    double? PrimaryPercent,
    DateTimeOffset? PrimaryResetsAt,
    double? WeeklyPercent,
    DateTimeOffset? WeeklyResetsAt,
    int? ResetCreditsAvailable,
    IReadOnlyList<long> ResetCreditExpiresAt,
    DateTimeOffset UpdatedAt,
    bool Stale);

internal sealed record QuotaSnapshot(
    ProviderQuotaSnapshot? Claude,
    ProviderQuotaSnapshot? Codex);

internal sealed record DomesticProviderQuotaSnapshot(
    string Provider,
    string? Plan,
    double? PrimaryPercent,
    DateTimeOffset? PrimaryResetsAt,
    double? WeeklyPercent,
    DateTimeOffset? WeeklyResetsAt,
    double? Balance,
    double? UsedCost,
    string? Currency,
    DateTimeOffset UpdatedAt,
    bool Stale);

internal sealed record DomesticQuotaSnapshot(
    DomesticProviderQuotaSnapshot? Alibaba,
    DomesticProviderQuotaSnapshot? Kimi,
    DomesticProviderQuotaSnapshot? MiniMax,
    DomesticProviderQuotaSnapshot? DeepSeek);

internal sealed record SystemMetricsSnapshot(
    double CpuPercent,
    double MemoryPercent,
    long UploadBytesPerSecond,
    long DownloadBytesPerSecond,
    DateTimeOffset UpdatedAt);

internal sealed record StatusSnapshot(
    int Version,
    string Time,
    long EpochUtc,
    int UtcOffsetSeconds,
    DateTimeOffset CapturedAt,
    ToolState Codex,
    ToolState Claude,
    bool MusicPlaying = false,
    WeatherSnapshot? Weather = null,
    StockSnapshot? Stocks = null,
    QuotaSnapshot? Quotas = null,
    DomesticQuotaSnapshot? DomesticQuotas = null,
    SystemMetricsSnapshot? SystemMetrics = null);

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
