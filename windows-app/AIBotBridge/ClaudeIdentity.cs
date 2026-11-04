using System.Text.Json;

namespace AIBotBridge;

// Per-session/per-prompt lifecycle. IDs stay local and never enter device frames.
internal sealed class ClaudeIdentity
{
    internal sealed record Selection(Guid SessionId,Guid PromptId,DateTimeOffset Activity,string State,string Outcome,bool NeedsInput,long CompletionAt,long CompletionSequence,string? OwnedTurn=null)
    {
        internal ToolState Tool => new(State,0,NeedsInput:NeedsInput,CompletionActive:CompletionAt>0,CompletionAt:CompletionAt,CompletionSequence:CompletionSequence);
    }
    private sealed record Turn(Guid Prompt,DateTimeOffset Started,DateTimeOffset Observed,DateTimeOffset Activity,string State="working",string Outcome="running",bool Waiting=false,long Completed=0,long Sequence=0)
    {
        internal bool Terminal=>Outcome is "completed" or "failed" or "interrupted" or "ended";
    }
    private readonly object _sync=new();
    private readonly Dictionary<Guid,Turn> _turns=new();
    private readonly HashSet<(Guid,Guid)> _retired=new();
    private readonly Queue<(Guid,Guid)> _retiredOrder=new();
    private long _sequence;
    internal bool Record(JsonElement e,DateTimeOffset? clock=null)
    {
        var now=clock??DateTimeOffset.UtcNow;
        if(Text(e,"agent")!="claude"||Text(e,"event") is not {} kind||!Guid.TryParse(Text(e,"sessionId"),out var session)||!Guid.TryParse(Text(e,"promptId"),out var prompt)||e.TryGetProperty("agentId",out _)||!e.TryGetProperty("emittedAt",out var stamp)||stamp.ValueKind!=JsonValueKind.Number||!stamp.TryGetDouble(out var ms)||!double.IsFinite(ms)||Math.Abs(now.ToUnixTimeMilliseconds()-ms)>120000)return false;
        var at=DateTimeOffset.FromUnixTimeMilliseconds((long)ms);
        lock(_sync) {
            if(kind=="UserPromptSubmit") {
                if(_retired.Contains((session,prompt)))return false;
                if(_turns.TryGetValue(session,out var old)) {
                    if(old.Prompt==prompt)return true;
                    if(at<=old.Started)return false;
                    _retired.Add((session,old.Prompt));_retiredOrder.Enqueue((session,old.Prompt));
                    if(_retiredOrder.Count>1024)_retired.Remove(_retiredOrder.Dequeue());
                }
                if(_turns.Count>=64&&!_turns.ContainsKey(session))return false;
                _turns[session]=new(prompt,at,at,at);return true;
            }
            if(!_turns.TryGetValue(session,out var t)||t.Prompt!=prompt||at<t.Started)return false;
            if(kind is "StopFailure" or "TurnInterrupted")t=t with {Outcome=kind=="StopFailure"?"failed":"interrupted",State=kind=="StopFailure"?"error":"idle",Waiting=false,Completed=0};
            else {
                if(t.Terminal&&!(t.Outcome=="ended"&&kind=="TurnResult"))return true;
                if(at<t.Observed)return false;
                switch(kind) {
                    case "PermissionRequest":case "Elicitation": t=t with {Waiting=true,State="working"};break;
                    case "PreToolUse":case "PostToolUse":case "PostToolUseFailure":case "PostToolBatch":t=t with {State="working",Waiting=false,Outcome="running",Activity=at};break;
                    case "Stop": t=t with {State="unknown",Outcome="unconfirmed"};break;
                    case "Notification":if(Text(e,"notificationType")!="idle_prompt")return false;t=t with {State="idle",Outcome="unconfirmed"};break;
                    case "SessionEnd":t=t with {State="idle",Outcome="ended",Waiting=false};break;
                    case "TurnResult":
                        var reason=Text(e,"terminalReason");if(reason is null)return false;
                        bool success=reason=="completed"&&e.TryGetProperty("isError",out var error)&&error.ValueKind==JsonValueKind.False&&e.TryGetProperty("numTurns",out var count)&&count.ValueKind==JsonValueKind.Number&&count.TryGetInt32(out var n)&&n>0;
                        if(success)t=t with {Completed=at.ToUnixTimeSeconds(),Sequence=++_sequence,State="idle",Outcome="completed",Waiting=false};
                        else {
                            var outcome=reason=="interrupted"?"interrupted":e.TryGetProperty("isError",out var failed)&&failed.ValueKind==JsonValueKind.True?"failed":"unconfirmed";
                            t=t with {Outcome=outcome,State=outcome=="failed"?"error":outcome=="interrupted"?"idle":"unknown",Waiting=false};
                        }
                        break;
                    default:return false;
                }
            }
            _turns[session]=t with {Observed=at>t.Observed?at:t.Observed,Activity=kind is "Notification" or "SessionEnd"?t.Activity:at>t.Activity?at:t.Activity};return true;
        }
    }
    internal Selection? Select(DateTimeOffset? clock=null)
    {
        var now=clock??DateTimeOffset.UtcNow;
        lock(_sync) {
            int Priority(Turn t)=>now-t.Observed<TimeSpan.FromSeconds(t.Waiting?300:600)&&!t.Terminal?(t.Waiting?2:t.State=="working"?1:0):0;
            var pair=_turns.OrderByDescending(p=>Priority(p.Value)).ThenByDescending(p=>p.Value.Activity).ThenBy(p=>p.Key.ToString(),StringComparer.Ordinal).FirstOrDefault();
            if(pair.Value is not {} t)return null;
            bool expired=!t.Terminal&&now-t.Observed>=TimeSpan.FromSeconds(t.Waiting?300:600);
            return new(pair.Key,t.Prompt,t.Activity,expired?"unknown":t.State,expired?"unconfirmed":t.Outcome,!expired&&t.Waiting,t.Completed,t.Sequence);
        }
    }
    internal static Selection? Resolve(Selection? bound,IEnumerable<Selection> owned)
    {
        var sessions=new Dictionary<Guid,Selection>();if(bound is not null)sessions[bound.SessionId]=bound;
        foreach(var value in owned)if(!sessions.TryGetValue(value.SessionId,out var old)||value.Activity>=old.Activity)sessions[value.SessionId]=value;
        return sessions.Values.OrderByDescending(v=>v.NeedsInput?2:v.State=="working"?1:0).ThenByDescending(v=>v.Activity).ThenBy(v=>v.SessionId.ToString(),StringComparer.Ordinal).FirstOrDefault();
    }
    internal void Acknowledge(){lock(_sync)foreach(var id in _turns.Keys.ToArray())_turns[id]=_turns[id] with {Completed=0};}
    internal void Invalidate(){lock(_sync)_turns.Clear();}
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
}
