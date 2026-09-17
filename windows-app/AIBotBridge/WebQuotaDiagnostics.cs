namespace AIBotBridge;

// Only fixed stage labels and numeric HTTP codes, never URLs, page text or credentials.
internal static class WebQuotaDiagnostics
{
    internal sealed record Entry(DateTimeOffset At, string Provider, string Stage, int? Status);
    private static readonly Queue<Entry> Events = new();
    internal static void Record(string provider, string stage, int? status = null)
    {
        lock (Events) {
            Events.Enqueue(new(DateTimeOffset.Now, provider, stage, status));
            while (Events.Count > 80) Events.Dequeue();
        }
    }
    internal static Entry[] Snapshot() { lock (Events) return Events.ToArray(); }
}
