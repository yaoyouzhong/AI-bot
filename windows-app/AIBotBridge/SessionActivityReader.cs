namespace AIBotBridge;

internal static class SessionActivityReader
{
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan IdleWindow = TimeSpan.FromMinutes(15);
    private static readonly CodexLifecycleTracker CodexLifecycle = new();
    private static readonly LocalUsageReader Usage = new();
    internal static readonly ActivitySignals Signals = new();
    private static IReadOnlyDictionary<string,LocalProviderUsage> _usage = new Dictionary<string,LocalProviderUsage>();
    private static long _nextUsageScan;
    private static readonly object Sync = new();
    private static long _nextScan;
    private static ToolState _codex = new("offline", null), _claude = new("offline", null);

    internal static StatusSnapshot Capture()
    {
        var now = DateTimeOffset.Now;
        var home = AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile);
        lock (Sync)
        {
            if (Environment.TickCount64 >= _nextScan)
            {
                _nextScan = Environment.TickCount64 + 1000;
                _codex = CodexLifecycle.Capture(Path.Combine(home, ".codex", "sessions"));
                if (Environment.TickCount64 >= _nextUsageScan)
                {
                    _usage = Usage.Capture(Path.Combine(home,".claude","projects"),Path.Combine(home,".codex","sessions"));
                    _nextUsageScan = Environment.TickCount64 + 5000;
                }
                var activity = _usage.GetValueOrDefault("claude");
                long? age = activity is null ? null : Math.Max(0,now.ToUnixTimeSeconds()-activity.LastActivity);
                _claude = new(age is null ? "offline" : age<90?"working":age<=900?"idle":"offline",age,TokensToday:activity?.TokensToday??0);
            }
        var domestic = Domestic(now);
        return new StatusSnapshot(
            Version: 1,
            Time: now.ToString("HH:mm:ss"),
            EpochUtc: now.ToUnixTimeSeconds(),
            UtcOffsetSeconds: (int)now.Offset.TotalSeconds,
            CapturedAt: now,
            Codex: Signals.Apply("codex",_codex with { TokensToday=_usage.GetValueOrDefault("codex")?.TokensToday??0 }),
            Claude: domestic.State != "offline" ? _claude : Signals.Apply("claude",_claude),
            DomesticActivity: domestic);
        }
    }
    private static DomesticActivitySnapshot Domestic(DateTimeOffset now)
    {
        var values=_usage.Where(p=>p.Key is not ("claude" or "codex")).ToDictionary();
        var latest=values.OrderByDescending(p=>p.Value.LastActivity).FirstOrDefault();
        long age=latest.Value is null?long.MaxValue:Math.Max(0,now.ToUnixTimeSeconds()-latest.Value.LastActivity);
        var state=Signals.Apply("claude",new(age<90?"working":age<=900?"idle":"offline",age));
        return new(latest.Key??"",age<=900?state.State:"offline",age<=900&&state.NeedsInput,values);
    }

    private static ToolState ReadTool(string root)
    {
        var newest = FindNewestJsonl(root);
        if (newest is null)
            return new ToolState("offline", null);

        var age = DateTimeOffset.UtcNow - newest.Value;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        var state = age < WorkingWindow
            ? "working"
            : age <= IdleWindow ? "idle" : "offline";

        return new ToolState(state, (long)age.TotalSeconds);
    }

    private static DateTimeOffset? FindNewestJsonl(string root)
    {
        if (!Directory.Exists(root))
            return null;

        DateTimeOffset? newest = null;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl"))
                {
                    var modified = File.GetLastWriteTimeUtc(file);
                    if (newest is null || modified > newest.Value.UtcDateTime)
                        newest = new DateTimeOffset(modified, TimeSpan.Zero);
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                    pending.Push(child);
            }
            catch (UnauthorizedAccessException)
            {
                // One unreadable directory must not hide activity from readable ones.
            }
            catch (IOException)
            {
                // Session files may rotate while the snapshot is being computed.
            }
        }

        return newest;
    }
}
