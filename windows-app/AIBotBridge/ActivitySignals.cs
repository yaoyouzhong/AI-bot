namespace AIBotBridge;

internal sealed class ActivitySignals
{
    private sealed record Signal(string? State, DateTimeOffset At, DateTimeOffset? Attention);
    private readonly object _sync=new();
    private readonly Dictionary<string,Signal> _signals=new();
    private long _acknowledged, _lastSequence;
    private bool _completed;
    private long _externalSequence, _externalAt;
    internal bool Record(string agent,string kind,string? message=null,DateTimeOffset? at=null)
    {
        if(agent is not ("claude" or "codex"))return false;
        var now=at??DateTimeOffset.UtcNow;
        lock(_sync)
        {
            var old=_signals.GetValueOrDefault(agent,new(null,now,null));
            if(agent=="codex"&&kind=="TaskComplete")
            { _externalSequence++; _externalAt=now.ToUnixTimeSeconds(); _signals.Remove(agent); return true; }
            if(kind is "Elicitation" or "PermissionRequest" || kind=="Notification" && new[]{"permission","approve","approval"}.Any(s=>(message??"").Contains(s,StringComparison.OrdinalIgnoreCase)))
            { _signals[agent]=old with { Attention=now }; return true; }
            string? state=kind switch { "UserPromptSubmit" or "PreToolUse" or "PostToolUse" or "SubagentStart" or "SubagentStop" or "PreCompact" or "PostCompact" or "WorktreeCreate"=>"working", "Stop" or "SessionEnd" or "SessionStart"=>"idle", _=>null };
            if(state is null)return false;
            _signals[agent]=new(state,now,null);
            if(agent=="codex"&&state=="working") { _completed=false; _acknowledged=_lastSequence; }
            return true;
        }
    }
    internal ToolState Apply(string agent,ToolState raw,DateTimeOffset? at=null)
    {
        var now=at??DateTimeOffset.UtcNow;
        lock(_sync)
        {
            if(agent=="codex")
            {
                raw=raw with { CompletionSequence=raw.CompletionSequence+_externalSequence, CompletionAt=Math.Max(raw.CompletionAt,_externalAt) };
                if(raw.CompletionSequence>_lastSequence) { _lastSequence=raw.CompletionSequence; _completed=true; _signals[agent]=new("idle",now,null); }
                // A stale file-mtime "working" must not erase a just received completion.
                // Explicit work hooks clear it immediately; a newer lifecycle task does too.
                bool completionSettled=raw.AgeSeconds.HasValue && now.ToUnixTimeSeconds()-raw.AgeSeconds.Value>raw.CompletionAt;
                if(raw.State=="working"&&completionSettled) { _completed=false; _signals.Remove(agent); }
                raw=raw with { CompletionActive=_completed&&raw.CompletionSequence>_acknowledged };
            }
            if(!_signals.TryGetValue(agent,out var signal))return raw;
            bool needs=signal.Attention.HasValue&&now>=signal.Attention.Value&&now-signal.Attention.Value<TimeSpan.FromMinutes(5);
            return raw with { NeedsInput=needs, State=signal.State is not null&&now-signal.At<TimeSpan.FromSeconds(signal.State=="working"?600:60)?signal.State:raw.State };
        }
    }
    internal void Acknowledge() { lock(_sync) { _acknowledged=_lastSequence; _completed=false; } }
}
