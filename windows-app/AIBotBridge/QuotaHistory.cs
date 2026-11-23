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
    private const string HistoryFileName = "codex-quota-history.json";
    private const int CompactAt = 256 * 1024;
    private readonly string _path;
    private readonly string _journalPath;
    private readonly List<QuotaObservation> _samples = new();
    private readonly Dictionary<DateTimeOffset,QuotaObservation> _pending = new();
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
        _path = Path.Combine(directory ?? Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-bot"), HistoryFileName);
        _journalPath = Path.ChangeExtension(_path, ".jsonl");
        List<QuotaObservation> journal = [];
        // Read the journal first. Compaction publishes the snapshot before
        // retiring the journal, so a concurrent reader cannot miss both.
        try { journal = ReadJournal(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { _preserveOriginal = true; }
        var byTime = new Dictionary<DateTimeOffset,QuotaObservation>();
        try
        {
            if (File.Exists(_path))
            {
                using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (file.Length > 64 * 1024 * 1024) throw new InvalidDataException();
                var rows = JsonSerializer.Deserialize<List<QuotaObservation>>(file) ?? throw new InvalidDataException();
                if (rows.Any(x => x is null || !Valid(x))) throw new InvalidDataException();
                foreach (var row in rows) byTime.TryAdd(row.At, row);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { _preserveOriginal = true; }
        foreach (var row in journal)
        {
            if (byTime.TryAdd(row.At, row)) continue;
            var previous = byTime[row.At];
            if (row.ResetCredits is { Length: > 0 } && previous.Plan == row.Plan && previous.AccountFingerprint == row.AccountFingerprint)
                byTime[row.At] = previous with { ResetCredits = (previous.ResetCredits ?? []).Concat(row.ResetCredits).DistinctBy(x => x.IdHash).Take(512).ToArray() };
        }
        _samples.AddRange(byTime.Values.OrderBy(x => x.At).TakeLast(140000));
        if (_samples.Count > 0) Trim(_samples[^1].At);
        if (_preserveOriginal) Error = "历史文件不可读，已保留原文件；本次只在内存记录。";
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
            Trim(row.At);
            Save(row);
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
            Save(_samples[index]);
        }
    }
    private void Trim(DateTimeOffset latest)
    {
        var cutoff = latest.AddDays(-90);
        int remove = Math.Max(0, _samples.Count - 140000);
        while (remove < _samples.Count && _samples[remove].At < cutoff) remove++;
        if (remove > 0) _samples.RemoveRange(0, remove);
    }
    private List<QuotaObservation> ReadJournal()
    {
        FileStream file;
        try { file = new(_journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        using (file)
        {
            long length = file.Length;
            if (length > 8 * 1024 * 1024) throw new InvalidDataException();
            var bytes = new byte[(int)length];
            int count = file.ReadAtLeast(bytes, bytes.Length, false);
            var rows = new List<QuotaObservation>();
            var remaining = bytes.AsSpan(0, count);
            int newline;
            // A final fragment without its commit newline is an interrupted append.
            while ((newline = remaining.IndexOf((byte)'\n')) >= 0)
            {
                var row = JsonSerializer.Deserialize<QuotaObservation>(remaining[..newline]);
                if (row is null || !Valid(row)) throw new InvalidDataException();
                rows.Add(row);
                remaining = remaining[(newline + 1)..];
            }
            return rows;
        }
    }
    private long AppendPending()
    {
        using var file = new FileStream(_journalPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        long end = file.Length;
        long tailLimit = Math.Max(0, end - 128 * 1024);
        while (end > 0)
        {
            file.Position = end - 1;
            if (file.ReadByte() == '\n') break;
            end--;
            if (end < tailLimit) throw new InvalidDataException("Quota journal tail exceeds the observation limit.");
        }
        if (end != file.Length) file.SetLength(end);
        file.Position = end;
        foreach (var row in _pending.Values)
        {
            file.Write(JsonSerializer.SerializeToUtf8Bytes(row));
            file.WriteByte((byte)'\n');
        }
        file.Flush(flushToDisk: true);
        return file.Length;
    }
    private void WriteSnapshot()
    {
        string temporary = _path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, _samples);
            file.Flush(flushToDisk: true);
        }
        AtomicFileReplacement.Move(temporary, _path);
        File.Delete(_journalPath);
    }
    private void Save(QuotaObservation row)
    {
        if (_preserveOriginal) return;
        _pending[row.At] = row;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (!File.Exists(_path)) WriteSnapshot();
            else
            {
                long journalLength = AppendPending();
                _pending.Clear();
                if (journalLength >= CompactAt) WriteSnapshot();
            }
            _pending.Clear();
            Error = null; PersistenceDiagnostic = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
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
