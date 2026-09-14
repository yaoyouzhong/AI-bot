namespace AIBotBridge;

internal static class SessionActivityReader
{
    private static readonly CodexLifecycleTracker CodexLifecycle = new();
    private static readonly LocalUsageReader Usage = new();
    internal static readonly ActivitySignals Signals = new();
    private static IReadOnlyDictionary<string, LocalProviderUsage> _usage = new Dictionary<string, LocalProviderUsage>();
    private static long _nextUsageScan;
    private static readonly BackgroundActivityCache Cache = new(Scan);

    private static ActivitySample Scan()
    {
        var home = AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codex = CodexLifecycle.Capture(Path.Combine(home, ".codex", "sessions"));
        if (Environment.TickCount64 >= _nextUsageScan)
        {
            _usage = Usage.Capture(Path.Combine(home, ".claude", "projects"), Path.Combine(home, ".codex", "sessions"));
            _nextUsageScan = Environment.TickCount64 + 5000;
        }
        return new(codex, _usage);
    }

    internal static object Diagnostics() => Cache.Diagnostics();
    internal static Task WaitForInitialScanAsync() => Cache.WaitForInitialScanAsync();

    internal static StatusSnapshot Capture() => Capture(Cache);

    internal static StatusSnapshot Capture(BackgroundActivityCache cache)
    {
        var now = DateTimeOffset.Now;
        var sample = cache.Read();
        var usage = sample.Usage;
        var activity = usage.GetValueOrDefault("claude");
        long? age = activity is null ? null : Math.Max(0, now.ToUnixTimeSeconds() - activity.LastActivity);
        var claude = new ToolState(age is null ? "offline" : age < 90 ? "working" : age <= 900 ? "idle" : "offline",
            age, TokensToday: activity?.TokensToday ?? 0);
        var domestic = Domestic(now, usage);
        return new StatusSnapshot(
            Version: 1,
            Time: now.ToString("HH:mm:ss"),
            EpochUtc: now.ToUnixTimeSeconds(),
            UtcOffsetSeconds: (int)now.Offset.TotalSeconds,
            CapturedAt: now,
            Codex: Signals.Apply("codex",sample.Codex with { TokensToday=usage.GetValueOrDefault("codex")?.TokensToday??0 }),
            Claude: domestic.State != "offline" ? claude : Signals.Apply("claude",claude),
            DomesticActivity: domestic);
    }
    private static DomesticActivitySnapshot Domestic(DateTimeOffset now, IReadOnlyDictionary<string, LocalProviderUsage> usage)
    {
        var values=usage.Where(p=>p.Key is not ("claude" or "codex")).ToDictionary();
        var latest=values.OrderByDescending(p=>p.Value.LastActivity).FirstOrDefault();
        long age=latest.Value is null?long.MaxValue:Math.Max(0,now.ToUnixTimeSeconds()-latest.Value.LastActivity);
        var state=Signals.Apply("claude",new(age<90?"working":age<=900?"idle":"offline",age));
        return new(latest.Key??"",age<=900?state.State:"offline",age<=900&&state.NeedsInput,values);
    }

}
