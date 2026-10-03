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
    private readonly bool _preserveOriginal;
    internal string? Error { get; private set; }
    internal QuotaHistory(string? directory = null)
    {
        _path = Path.Combine(directory ?? Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot"), "codex-quota-history.json");
        try
        {
            if (File.Exists(_path))
            {
                if (new FileInfo(_path).Length > 64 * 1024 * 1024) throw new InvalidDataException();
                var rows = JsonSerializer.Deserialize<List<QuotaObservation>>(File.ReadAllText(_path)) ?? throw new InvalidDataException();
                if (rows.Any(x => x is null || !Valid(x))) throw new InvalidDataException();
                _samples.AddRange(rows.OrderBy(x => x.At).DistinctBy(x => x.At).TakeLast(140000));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { _preserveOriginal = true; Error = "历史文件不可读，已保留原文件；本次只在内存记录。"; }
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
            if (_samples.Count > 140000) _samples.RemoveRange(0, _samples.Count - 140000);
            if (_preserveOriginal) return; // Never overwrite damaged evidence; transient save failures can retry.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_samples));
                File.Move(_path + ".tmp", _path, true);
                Error = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Error = "历史保存失败，数据暂存在内存；下次采样自动重试，请检查数据目录权限。"; }
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
            // Attribute each short observed increment to the date of its later sample.
            // This also gives midnight a deterministic owner without splitting rounded percentages.
            var intervals = Enumerable.Range(0, rows.Count).Where(i => rows[i].At >= start && rows[i].At < end).ToArray();
            bool partial = indexes.Length == 0 || day >= today || !BoundaryCovered(rows, start) || !BoundaryCovered(rows, end);
            foreach (int i in intervals)
            {
                if (i == 0) continue;
                var previous = rows[i - 1]; var current = rows[i];
                if (string.IsNullOrEmpty(previous.AccountFingerprint) || previous.AccountFingerprint != current.AccountFingerprint) partial = true;
                if (previous.AccountFingerprint is not null && current.AccountFingerprint is not null && previous.AccountFingerprint != current.AccountFingerprint)
                { gaps++; continue; }
                // User policy: normal short polling intervals spanning midnight belong to
                // the later sample's day. Never pull multi-day downtime into today's total.
                if (previous.At < start && (current.At - previous.At > TimeSpan.FromMinutes(5) ||
                    string.IsNullOrEmpty(previous.AccountFingerprint)) && !VerifiedFlatInterval(previous,current))
                { partial = true; continue; }
                if (previous.Weekly is not double before || current.Weekly is not double after || previous.Plan != current.Plan)
                { gaps++; partial = true; continue; }
                if (current.At <= previous.At) { gaps++; partial = true; continue; }
                // Allow up to a minute of timestamp rounding/jitter while both deadlines
                // are still in the future. A usage drop remains anomalous regardless.
                bool sameWindow = SameWindow(previous, current);
                bool expired = !sameWindow && previous.WeeklyReset is { } expiry && expiry > previous.At && expiry <= current.At && current.WeeklyReset > expiry;
                bool manualReset = !sameWindow && after < before && !string.IsNullOrEmpty(previous.AccountFingerprint) &&
                    previous.WeeklyReset.HasValue && current.WeeklyReset > previous.WeeklyReset && current.WeeklyReset > current.At;
                bool reset = expired || manualReset;
                if (current.At - previous.At > TimeSpan.FromMinutes(5))
                {
                    gaps++;
                    // A same-day, same-account cumulative difference remains observable
                    // after a pause. Sampling continuity is not daily-total completeness:
                    // only unresolvable intervals make the daily amount partial.
                    if (string.IsNullOrEmpty(previous.AccountFingerprint) || (!sameWindow || after < before) && !reset)
                    { partial = true; continue; }
                }
                if (reset)
                {
                    // A new window starts at zero: include its first observation even
                    // when polling missed zero. Never fill the old segment up to 100.
                    resets++; total += after; comparable++;
                }
                else if (sameWindow && after >= before || before == 0 && after == 0 &&
                    !string.IsNullOrEmpty(previous.AccountFingerprint))
                { total += after - before; comparable++; }
                else { uncertainResets++; partial = true; }
                // A drop without reset-window evidence may be a correction. Start a
                // baseline without inventing consumption or inferring resets from credits.
            }
            result.Add(new(day, comparable == 0 ? null : Math.Round(total, 2), indexes.Length, partial, resets, uncertainResets, gaps));
        }
        return result.ToArray();
    }
    private static bool SameWindow(QuotaObservation previous, QuotaObservation current) =>
        previous.WeeklyReset is { } previousReset && current.WeeklyReset is { } currentReset &&
        (currentReset == previousReset || previousReset > current.At && currentReset > current.At &&
            (currentReset - previousReset).Duration() <= TimeSpan.FromMinutes(1));

    private static bool VerifiedFlatInterval(QuotaObservation previous, QuotaObservation current) =>
        !string.IsNullOrEmpty(previous.AccountFingerprint) && previous.AccountFingerprint == current.AccountFingerprint &&
        previous.Plan == current.Plan && previous.Weekly.HasValue && previous.Weekly == current.Weekly && SameWindow(previous,current);

    private static bool BoundaryCovered(IReadOnlyList<QuotaObservation> rows, DateTimeOffset boundary)
    {
        if (rows.Any(x => x.At == boundary && x.Weekly.HasValue && !string.IsNullOrEmpty(x.AccountFingerprint))) return true;
        var before = rows.LastOrDefault(x => x.At < boundary);
        var after = rows.FirstOrDefault(x => x.At > boundary);
        // A flat cumulative interval also covers midnight without guessing which day
        // owns usage. An increase over a long overnight pause remains ambiguous.
        return before is not null && after is not null &&
            (after.At - before.At <= TimeSpan.FromMinutes(5) || VerifiedFlatInterval(before,after)) &&
            !string.IsNullOrEmpty(before.AccountFingerprint) && before.AccountFingerprint == after.AccountFingerprint &&
            before.Plan == after.Plan && before.Weekly.HasValue && after.Weekly >= before.Weekly && SameWindow(before, after);
    }
    internal static (double? Value, int Days) AverageRecorded(IEnumerable<QuotaDay> days, DateOnly today)
    {
        var eligible = days.Where(x => x.Day < today && !x.Partial && x.Growth.HasValue).ToArray();
        return (eligible.Length == 0 ? null : Math.Round(eligible.Average(x => x.Growth!.Value), 2), eligible.Length);
    }
}
