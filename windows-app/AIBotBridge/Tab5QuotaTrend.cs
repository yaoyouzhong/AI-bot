namespace AIBotBridge;

// Reuse the bridge's observed quota history; never infer missing days or token-to-quota conversions.
internal static class Tab5QuotaTrend
{
    internal sealed record Day(string Date, string Label, double? Used, bool Partial, double CarryOver = 0);
    internal sealed record Trend(string Provider, string Unit, string Timezone, Day[] Days, bool StorageWarning);
    internal sealed record Coverage(string Date, DateTimeOffset? FirstSample, DateTimeOffset? LastSample,
        double? FirstWeeklyUsed, double? LastWeeklyUsed, DateTimeOffset? PreviousSample, DateTimeOffset? NextSample);
    private static readonly object Gate = new();
    private static Trend? _cached;
    private static long _readAt;
    private static DateOnly _day;
    internal static object Diagnostics()
    {
        var rows=QuotaHistory.Shared.Read();
        var now=DateTimeOffset.UtcNow;
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,QuotaHistory.StatisticsZone).DateTime);
        return new {
            samples=rows.Length,firstSample=rows.FirstOrDefault()?.At,lastSample=rows.LastOrDefault()?.At,
            storageError=QuotaHistory.Shared.Error,
            daily=QuotaHistory.Daily(rows,today,7,QuotaHistory.StatisticsZone),
            coverage=CoverageFor(rows,today,7),
            transmitted=Snapshot()
        };
    }
    // Diagnostic bounds only: a sparse interval can still have a valid cumulative
    // difference. Do not turn sample count or covered hours into estimated usage.
    internal static Coverage[] CoverageFor(IReadOnlyList<QuotaObservation> rows, DateOnly today, int days) =>
        Enumerable.Range(1-days,days).Select(offset => {
            var day=today.AddDays(offset);
            var midnight=day.ToDateTime(TimeOnly.MinValue);
            var start=new DateTimeOffset(midnight,QuotaHistory.StatisticsZone.GetUtcOffset(midnight));
            var end=start.AddDays(1);
            var inside=rows.Where(x=>x.At>=start&&x.At<end).ToArray();
            return new Coverage(day.ToString("yyyy-MM-dd"),inside.FirstOrDefault()?.At,inside.LastOrDefault()?.At,
                inside.FirstOrDefault()?.Weekly,inside.LastOrDefault()?.Weekly,
                rows.LastOrDefault(x=>x.At<start)?.At,rows.FirstOrDefault(x=>x.At>=end)?.At);
        }).ToArray();
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
                .Select(d => new Day(d.Day.ToString("yyyy-MM-dd"), d.Day.ToString("MM-dd"), d.Growth, d.Partial, d.CarryOver)).ToArray(), warning);
    }
}
