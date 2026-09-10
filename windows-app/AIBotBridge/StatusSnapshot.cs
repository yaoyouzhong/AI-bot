using System.Text.Json;

namespace AIBotBridge;

internal sealed record ToolState(string State, long? AgeSeconds, long CompletionAt = 0, long CompletionSequence = 0,
    bool NeedsInput = false, bool CompletionActive = false, long TokensToday = 0);
internal sealed record DomesticActivitySnapshot(string ActiveProvider, string State, bool NeedsInput,
    IReadOnlyDictionary<string, LocalProviderUsage> Providers)
{
    public long TokensToday => Providers.GetValueOrDefault(ActiveProvider)?.TokensToday ?? 0;
}

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
    bool Stale,
    string? AirQualityLabel = null,
    string Animation = "robot",
    int HeaderCenterX = 61,
    int DateCenterX = 95,
    int RangeY = 34,
    int? AnimationIcon = null,
    int? UtcOffsetSeconds = null);

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
    bool Stale)
{
    public double? PlanPercent { get; init; }
    public DateTimeOffset? PlanResetsAt { get; init; }
}

internal sealed record DomesticQuotaSnapshot(
    DomesticProviderQuotaSnapshot? Alibaba,
    DomesticProviderQuotaSnapshot? Kimi,
    DomesticProviderQuotaSnapshot? MiniMax,
    DomesticProviderQuotaSnapshot? DeepSeek,
    DomesticProviderQuotaSnapshot? Zhipu = null);

internal sealed record SystemMetricsSnapshot(
    double CpuPercent,
    double MemoryPercent,
    long UploadBytesPerSecond,
    long DownloadBytesPerSecond,
    DateTimeOffset UpdatedAt,
    [property: System.Text.Json.Serialization.JsonIgnore] IReadOnlyList<NetworkSample>? History = null)
{
    public string? SampleSession { get; init; }
    public long SampleSequence { get; init; }
    public IReadOnlyList<NetworkSample>? Samples { get; init; }
}

internal sealed record NetworkSample(long Upload, long Download);

internal sealed record MusicSnapshot(
    string Title,
    string Artist,
    string Album,
    bool Playing,
    double ElapsedSeconds,
    double DurationSeconds,
    DateTimeOffset UpdatedAt)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? CoverRgb565 { get; init; }
    public bool HasArtwork => CoverRgb565 is {Length: > 0};
}

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
    SystemMetricsSnapshot? SystemMetrics = null,
    MusicSnapshot? Music = null,
    DisplayPolicy? DisplayPolicy = null,
    DomesticActivitySnapshot? DomesticActivity = null,
    string? FollowApp = null);

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
