namespace AIBotBridge;

internal static class SessionActivityReader
{
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan IdleWindow = TimeSpan.FromMinutes(15);

    internal static StatusSnapshot Capture()
    {
        var now = DateTimeOffset.Now;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new StatusSnapshot(
            Version: 1,
            Time: now.ToString("HH:mm:ss"),
            EpochUtc: now.ToUnixTimeSeconds(),
            UtcOffsetSeconds: (int)now.Offset.TotalSeconds,
            CapturedAt: now,
            Codex: ReadTool(Path.Combine(home, ".codex", "sessions")),
            Claude: ReadTool(Path.Combine(home, ".claude", "projects")));
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
