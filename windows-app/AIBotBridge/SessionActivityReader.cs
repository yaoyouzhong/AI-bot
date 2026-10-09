namespace AIBotBridge;

internal static class SessionActivityReader
{
    private static readonly CodexLifecycleTracker CodexLifecycle = new();
    private static readonly Tab5CodexDesktop Desktop=new();
    internal static readonly Tab5LiveActivity LiveActivity=new((id,ct)=>Desktop.ReadAsync(id,ct));
    private static readonly LocalUsageReader Usage = new();
    internal static readonly ActivitySignals Signals = new();
    private static IReadOnlyDictionary<string, LocalProviderUsage> _usage = new Dictionary<string, LocalProviderUsage>();
    private static long _nextUsageScan;
    private static readonly BackgroundActivityCache Cache = new(Scan);

    private static ActivitySample Scan()
    {
        var home = AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codex = CodexLifecycle.Capture(Path.Combine(home, ".codex", "sessions"));
        if(codex.State!="offline")LiveActivity.Refresh(Tab5CodexCatalog.Recent(),CancellationToken.None);
        if (Environment.TickCount64 >= _nextUsageScan)
        {
            _usage = Usage.Capture(Path.Combine(home, ".claude", "projects"), Path.Combine(home, ".codex", "sessions"));
            _nextUsageScan = Environment.TickCount64 + 5000;
        }
        return new(codex, _usage);
    }

    internal static object Diagnostics() => Cache.Diagnostics();
    internal static IReadOnlyList<CodexLifecycleTracker.TaskActivity> TaskActivities=>CodexLifecycle.TaskActivities;
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
            Codex: MergeCodex(Signals.Apply("codex",sample.Codex with { TokensToday=usage.GetValueOrDefault("codex")?.TokensToday??0 }),CodexLifecycle.TaskActivities,LiveActivity.Fresh()),
            Claude: domestic.State != "offline" ? claude : Signals.Apply("claude",claude),
            DomesticActivity: domestic);
    }
    internal static ToolState MergeCodex(ToolState raw,IReadOnlyList<CodexLifecycleTracker.TaskActivity> logs,IReadOnlyList<CodexLifecycleTracker.TaskActivity> live)
    {
        if(raw.State=="offline")return raw;
        var tasks=logs.GroupBy(t=>t.Id).ToDictionary(g=>g.Key,g=>g.MaxBy(t=>t.UpdatedAt)!);
        foreach(var task in live.Where(t=>t.State is "working" or "waiting" or "idle"))tasks[task.Id]=task;
        if(tasks.Values.Any(t=>t.State=="working"))return raw with {State="working",AgeSeconds=0,NeedsInput=raw.NeedsInput||tasks.Values.Any(t=>t.State=="waiting")};
        if(tasks.Values.Any(t=>t.State=="waiting"))return raw with {State="working",AgeSeconds=0,NeedsInput=true};
        // An explicit live completion supersedes the same turn's buffered working log.
        if(live.Count>0&&tasks.Count>0&&tasks.Values.All(t=>t.State=="idle"))return raw with {State="idle",NeedsInput=false};
        return raw;
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
