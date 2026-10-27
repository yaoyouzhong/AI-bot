using System.Text.Json;

namespace AIBotBridge;

internal sealed record QuotaResetEvidence(string IdHash, DateTimeOffset RedeemedAt);
internal sealed record QuotaObservation(DateTimeOffset At, string? Plan, double? Weekly,
    DateTimeOffset? WeeklyReset, double? FiveHour, DateTimeOffset? FiveHourReset, string? AccountFingerprint = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public QuotaResetEvidence[]? ResetCredits { get; init; }
}
internal sealed record QuotaDay(DateOnly Day, double? Growth, int Samples, bool Partial, int Resets,
    int UncertainResets = 0, int Gaps = 0, double CarryOver = 0, int WindowChanges = 0);

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
    internal string? PersistenceDiagnostic { get; private set; }
    private DateTimeOffset? _resetHistoryCheckedAt;
    private bool _resetHistoryComplete;
    private string? _resetHistoryError;
    internal object ResetCreditHistoryStatus { get { lock(_sync) return new {checkedAt=_resetHistoryCheckedAt,complete=_resetHistoryComplete,error=_resetHistoryError}; } }
    internal void ResetHistoryChecked(bool complete,string? error)
    { lock(_sync) { _resetHistoryCheckedAt=DateTimeOffset.UtcNow;_resetHistoryComplete=complete;_resetHistoryError=error; } }
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
    private static bool Valid(QuotaObservation s) => s.At > DateTimeOffset.UnixEpoch && Percent(s.Weekly) && Percent(s.FiveHour) &&
        (s.ResetCredits is null || s.ResetCredits.Length <= 512 && s.ResetCredits.All(x => x is not null &&
            x.IdHash is { Length: 64 } && x.IdHash.All(char.IsAsciiHexDigit) && x.RedeemedAt > DateTimeOffset.UnixEpoch && x.RedeemedAt <= s.At));
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
            Save();
        }
    }

    internal void RecordResetEvidence(ProviderQuotaSnapshot q, string? fingerprint)
    {
        if (q.Stale || q.Provider != "codex" || string.IsNullOrEmpty(fingerprint) || q.ResetCreditRedemptions.Count == 0) return;
        lock (_sync)
        {
            int index = _samples.FindLastIndex(x => x.At == q.UpdatedAt && x.Plan == q.Plan && x.AccountFingerprint == fingerprint);
            if (index < 0) return; // Optional metadata can enrich only its own successful usage sample.
            var known = _samples.Where(x => x.AccountFingerprint == fingerprint).SelectMany(x => x.ResetCredits ?? []).Select(x => x.IdHash).ToHashSet();
            var fresh = q.ResetCreditRedemptions.Where(x => x.RedeemedAt > q.UpdatedAt.AddDays(-90) && x.RedeemedAt <= q.UpdatedAt &&
                x.IdHash is { Length: 64 } && x.IdHash.All(char.IsAsciiHexDigit) && !known.Contains(x.IdHash)).DistinctBy(x => x.IdHash).Take(512).ToArray();
            if (fresh.Length == 0) return;
            _samples[index] = _samples[index] with { ResetCredits = (_samples[index].ResetCredits ?? []).Concat(fresh).Take(512).ToArray() };
            Save();
        }
    }
    private void Save()
    {
        if (_preserveOriginal) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_samples));
            AtomicFileReplacement.Move(_path + ".tmp", _path);
            Error = null; PersistenceDiagnostic = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Error = "历史保存失败，数据暂存在内存；下次采样自动重试，请检查数据目录权限。"; PersistenceDiagnostic = $"{ex.GetType().Name}/0x{ex.HResult:x8}"; }
    }

    internal static QuotaDay[] Daily(IReadOnlyList<QuotaObservation> rows, DateOnly today, int days, TimeZoneInfo zone)
    {
        var result = new List<QuotaDay>();
        var creditEvidence = rows.Where(x => !string.IsNullOrEmpty(x.AccountFingerprint)).SelectMany(x =>
            (x.ResetCredits ?? []).Select(e => (x.AccountFingerprint, x.Plan, Evidence: e))).DistinctBy(x => (x.AccountFingerprint, x.Evidence.IdHash)).ToArray();
        for (int d = days - 1; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(day.ToDateTime(TimeOnly.MinValue)));
            var end = start.AddDays(1);
            var indexes = Enumerable.Range(0, rows.Count).Where(i => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(rows[i].At, zone).DateTime) == day).ToArray();
            double total = 0, carryOver = 0; int comparable = 0, resets = 0, uncertainResets = 0, gaps = 0, windowChanges = 0;
            // Carry the previous valid cumulative reading into the next observed
            // date. Long cross-midnight intervals are explicitly marked, never
            // silently discarded or presented as exact calendar-day consumption.
            var intervals = Enumerable.Range(0, rows.Count).Where(i => rows[i].At >= start && rows[i].At < end).ToArray();
            bool partial = indexes.Length == 0 || day >= today || !BoundaryCovered(rows, start) || !BoundaryCovered(rows, end);
            foreach (int i in intervals)
            {
                if (i == 0) continue;
                var current = rows[i];
                int baseline = i - 1;
                while (baseline > 0 && rows[baseline].Weekly is null &&
                    !string.IsNullOrEmpty(current.AccountFingerprint) &&
                    rows[baseline].AccountFingerprint == current.AccountFingerprint && rows[baseline].Plan == current.Plan)
                    baseline--;
                var previous = rows[baseline];
                bool sameIdentity = !string.IsNullOrEmpty(previous.AccountFingerprint) && previous.AccountFingerprint == current.AccountFingerprint;
                if (string.IsNullOrEmpty(previous.AccountFingerprint) || previous.AccountFingerprint != current.AccountFingerprint) partial = true;
                if (previous.AccountFingerprint is not null && current.AccountFingerprint is not null && previous.AccountFingerprint != current.AccountFingerprint)
                { gaps++; continue; }
                bool carried = previous.At < start && current.At - previous.At > TimeSpan.FromMinutes(5);
                if (previous.At < start && !sameIdentity)
                { partial = true; continue; }
                if (previous.Weekly is not double before || current.Weekly is not double after || previous.Plan != current.Plan)
                { gaps++; partial = true; continue; }
                if (current.At <= previous.At) { gaps++; partial = true; continue; }
                // Allow up to a minute of timestamp rounding/jitter while both deadlines
                // are still in the future. A usage drop remains anomalous regardless.
                bool sameWindow = SameWindow(previous, current);
                bool expired = !sameWindow && previous.WeeklyReset is { } expiry && expiry > previous.At && expiry <= current.At && current.WeeklyReset > expiry;
                bool observedReset = !sameWindow && after < before && sameIdentity &&
                    previous.WeeklyReset.HasValue && current.WeeklyReset > previous.WeeklyReset && current.WeeklyReset > current.At;
                bool creditedReset = !sameWindow && sameIdentity && previous.WeeklyReset.HasValue && current.WeeklyReset > current.At &&
                    creditEvidence.Any(x => x.AccountFingerprint == current.AccountFingerprint && x.Plan == current.Plan &&
                        x.Evidence.RedeemedAt > previous.At && x.Evidence.RedeemedAt <= current.At);
                bool reset = expired || observedReset || creditedReset;
                // A future deadline change alone does not prove a reset. Preserve
                // the observed positive difference as a lower bound, with an explicit
                // uncertainty marker; never count the entire new reading as usage.
                bool zeroBaseline = before == 0 && after == 0 && sameIdentity;
                bool shiftedGrowth = !sameWindow && !reset && !zeroBaseline && sameIdentity && after >= before &&
                    previous.WeeklyReset > current.At && current.WeeklyReset > current.At;
                if (current.At - previous.At > TimeSpan.FromMinutes(5))
                {
                    gaps++;
                    // A verified cumulative difference survives a collection pause.
                    if (!sameIdentity || (!sameWindow || after < before) && !reset && !shiftedGrowth && !zeroBaseline)
                    { partial = true; continue; }
                }
                if (reset)
                {
                    // A new window starts at zero: include its first observation even
                    // when polling missed zero. Never fill the old segment up to 100.
                    resets++; total += after; comparable++;
                    if (current.At - previous.At > TimeSpan.FromMinutes(5)) partial = true;
                    if (carried) carryOver += after;
                }
                else if (sameWindow && after >= before || shiftedGrowth || before == 0 && after == 0 &&
                    !string.IsNullOrEmpty(previous.AccountFingerprint))
                {
                    total += after - before; comparable++; if (carried) carryOver += after - before;
                    if (shiftedGrowth) { windowChanges++; partial = true; }
                }
                else { uncertainResets++; partial = true; }
                // A drop without reset-window evidence may be a correction. Start a
                // baseline without inventing consumption or inferring resets from credits.
            }
            result.Add(new(day, comparable == 0 ? null : Math.Round(total, 2), indexes.Length, partial || carryOver > 0,
                resets, uncertainResets, gaps, Math.Round(carryOver, 2), windowChanges));
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
