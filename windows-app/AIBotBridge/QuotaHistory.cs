using System.Text.Json;

namespace AIBotBridge;

internal sealed record QuotaObservation(DateTimeOffset At, string? Plan, double? Weekly,
    DateTimeOffset? WeeklyReset, double? FiveHour, DateTimeOffset? FiveHourReset, string? AccountFingerprint = null);
internal sealed record QuotaDay(DateOnly Day, double? Growth, int Samples, bool Partial, int Resets,
    int UncertainResets = 0, int Gaps = 0);

// Display-only observations: no credentials, response bodies or conversation data.
internal sealed class QuotaHistory
{
    internal static TimeZoneInfo StatisticsZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    internal static QuotaHistory Shared { get; } = new();
    private readonly object _sync = new();
    private readonly string _path;
    private readonly List<QuotaObservation> _samples = new();
    internal string? Error { get; private set; }
    internal QuotaHistory(string? directory = null)
    {
        _path = Path.Combine(directory ?? Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot"), "codex-quota-history.json");
        try
        {
            if (File.Exists(_path))
            {
                if (new FileInfo(_path).Length > 32 * 1024 * 1024) throw new InvalidDataException();
                var rows = JsonSerializer.Deserialize<List<QuotaObservation>>(File.ReadAllText(_path)) ?? throw new InvalidDataException();
                if (rows.Any(x => x is null || !Valid(x))) throw new InvalidDataException();
                _samples.AddRange(rows.OrderBy(x => x.At).DistinctBy(x => x.At).TakeLast(70000));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { Error = "历史文件不可读，已保留原文件；本次只在内存记录。"; }
    }
    private static bool Percent(double? v) => v is null || double.IsFinite(v.Value) && v >= 0 && v <= 100;
    private static bool Valid(QuotaObservation s) => s.At > DateTimeOffset.UnixEpoch && Percent(s.Weekly) && Percent(s.FiveHour);
    internal QuotaObservation[] Read() { lock (_sync) return _samples.ToArray(); }
    internal void Record(ProviderQuotaSnapshot? q, string? accountFingerprint = null)
    {
        if (q is null || q.Stale || q.Provider != "codex" || q.WeeklyPercent is null && q.PrimaryPercent is null) return;
        var row = new QuotaObservation(q.UpdatedAt, q.Plan, q.WeeklyPercent, q.WeeklyResetsAt, q.PrimaryPercent, q.PrimaryResetsAt, accountFingerprint);
        if (!Valid(row)) return;
        lock (_sync)
        {
            if (_samples.LastOrDefault() is { } last && row.At <= last.At) return;
            _samples.Add(row);
            _samples.RemoveAll(x => x.At < row.At.AddDays(-90));
            if (_samples.Count > 70000) _samples.RemoveRange(0, _samples.Count - 70000);
            if (Error is not null) return; // Never overwrite damaged evidence.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_samples));
                File.Move(_path + ".tmp", _path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Error = "历史保存失败，本次仅在内存记录；请检查数据目录权限。"; }
        }
    }

    internal static QuotaDay[] Daily(IReadOnlyList<QuotaObservation> rows, DateOnly today, int days, TimeZoneInfo zone)
    {
        var result = new List<QuotaDay>();
        for (int d = days - 1; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(day.ToDateTime(TimeOnly.MinValue)));
            var end = start.AddDays(1);
            var indexes = Enumerable.Range(0, rows.Count).Where(i => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(rows[i].At, zone).DateTime) == day).ToArray();
            double total = 0; int comparable = 0, resets = 0, uncertainResets = 0, gaps = 0;
            // Closing midnight sample closes the previous day and is the next day's baseline.
            var intervals = Enumerable.Range(0, rows.Count).Where(i => rows[i].At > start && rows[i].At <= end).ToArray();
            bool partial = day >= today || !rows.Any(x => x.At == start && x.Weekly.HasValue) || !rows.Any(x => x.At == end && x.Weekly.HasValue);
            foreach (int i in intervals)
            {
                if (i == 0) continue;
                var previous = rows[i - 1]; var current = rows[i];
                if (string.IsNullOrEmpty(previous.AccountFingerprint) || previous.AccountFingerprint != current.AccountFingerprint) partial = true;
                if (previous.AccountFingerprint is not null && current.AccountFingerprint is not null && previous.AccountFingerprint != current.AccountFingerprint)
                { gaps++; continue; }
                // Crossing midnight cannot allocate consumption accurately to either day.
                if (previous.At < start) { partial = true; continue; }
                if (previous.Weekly is not double before || current.Weekly is not double after || previous.Plan != current.Plan)
                { gaps++; partial = true; continue; }
                bool expired = previous.WeeklyReset is { } expiry && expiry > previous.At && expiry <= current.At && current.WeeklyReset > expiry;
                bool changed = current.WeeklyReset != previous.WeeklyReset || after < before;
                if (expired) { resets++; partial = true; }
                else if (changed) { uncertainResets++; partial = true; }
                if (current.At <= previous.At || current.At - previous.At > TimeSpan.FromMinutes(5)) { gaps++; partial = true; continue; }
                if (previous.WeeklyReset is not null && current.WeeklyReset == previous.WeeklyReset && after >= before)
                { total += after - before; comparable++; }
                else if (expired)
                { total += after; comparable++; }
                else partial = true;
                // Unconfirmed manual resets/corrections start a baseline. Never add 100-before,
                // subtract usage, or assume a spent/expired reset credit proves a quota reset.
            }
            result.Add(new(day, comparable == 0 ? null : Math.Round(total, 2), indexes.Length, partial, resets, uncertainResets, gaps));
        }
        return result.ToArray();
    }
    internal static (double? Value, int Days) AverageRecorded(IEnumerable<QuotaDay> days, DateOnly today)
    {
        var eligible = days.Where(x => x.Day < today && !x.Partial && x.Growth.HasValue).ToArray();
        return (eligible.Length == 0 ? null : Math.Round(eligible.Average(x => x.Growth!.Value), 2), eligible.Length);
    }
}
