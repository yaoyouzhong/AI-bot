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
    internal string? TakeWarning(Func<string,bool>? eligible = null)
    {
        lock (_failures) {
            var count = _pending.Count;
            while (count-- > 0 && _pending.TryDequeue(out var provider)) {
                if (!_failures.TryGetValue(provider,out var message)) { _queued.Remove(provider); continue; }
                if (eligible is not null && !eligible(provider)) { _pending.Enqueue(provider); continue; }
                _queued.Remove(provider);
                return message;
            }
        }
        return null;
    }
}
