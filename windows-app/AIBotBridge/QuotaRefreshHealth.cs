namespace AIBotBridge;

// Keep only safe user-facing messages. Remind once per failure episode, recover on success.
internal sealed class QuotaRefreshHealth
{
    private readonly Dictionary<string, string> _failures = new();
    private readonly Queue<string> _pending = new();
    private readonly HashSet<string> _queued = new();
    internal void Fail(string provider, string message)
    {
        lock (_failures)
        {
            if (!_failures.ContainsKey(provider) && _queued.Add(provider)) _pending.Enqueue(provider);
            _failures[provider] = message;
        }
    }
    internal void Recover(string provider) { lock (_failures) _failures.Remove(provider); }
    internal bool Failed(string provider) { lock (_failures) return _failures.ContainsKey(provider); }
    internal string? TakeWarning()
    {
        lock (_failures)
            while (_pending.TryDequeue(out var provider)) {
                _queued.Remove(provider);
                if (_failures.TryGetValue(provider,out var message)) return message;
            }
        return null;
    }
}
