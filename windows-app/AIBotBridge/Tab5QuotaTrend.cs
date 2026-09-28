namespace AIBotBridge;

// Reuse the bridge's observed quota history; never infer missing days or token-to-quota conversions.
internal static class Tab5QuotaTrend
{
    internal sealed record Day(string Date, string Label, double? Used, bool Partial);
    internal sealed record Trend(string Provider, string Unit, string Timezone, Day[] Days, bool StorageWarning);
    private static readonly object Gate = new();
    private static Trend? _cached;
    private static long _readAt;
    private static DateOnly _day;
    internal static Trend Snapshot()
    {
        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, QuotaHistory.StatisticsZone).DateTime);
            if (_cached is null || day != _day || Environment.TickCount64 - _readAt >= 30000)
            {
                _cached = Build(QuotaHistory.Shared.Read(), now, QuotaHistory.Shared.Error is not null);
                _readAt = Environment.TickCount64;
                _day = day;
            }
            return _cached;
        }
    }
    internal static Trend Build(IReadOnlyList<QuotaObservation> rows, DateTimeOffset now, bool warning = false)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, QuotaHistory.StatisticsZone).DateTime);
        return new("codex", "weekly_percentage_points", "Asia/Shanghai",
            QuotaHistory.Daily(rows, today, 7, QuotaHistory.StatisticsZone)
                .Select(d => new Day(d.Day.ToString("yyyy-MM-dd"), d.Day.ToString("MM-dd"), d.Growth, d.Partial)).ToArray(), warning);
    }
}
