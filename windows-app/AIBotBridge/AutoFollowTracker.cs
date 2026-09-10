namespace AIBotBridge;

// The last actual tool switch, not a wall-clock modulo, owns the dwell timer.
internal sealed class AutoFollowTracker
{
    private readonly object _sync=new();
    private string _current="claude";
    private long? _switchedAt;
    internal string Update(StatusSnapshot status,long monotonicMilliseconds)
    {
        lock(_sync)
        {
            _switchedAt ??= monotonicMilliseconds;
            if(monotonicMilliseconds<_switchedAt) _switchedAt=monotonicMilliseconds;
            string selected=status.DisplayPolicy?.SelectedMode??"auto";
            bool attention=status.Claude.NeedsInput||status.Codex.NeedsInput||status.Codex.CompletionActive;
            bool hidden=selected=="screensaver"||status.DomesticActivity?.NeedsInput==true||!attention&&
                (selected is not ("auto" or "claude" or "codex")||selected=="auto"&&
                (status.DisplayPolicy?.CycleEnabled==true||status.Music?.Playing==true||status.DomesticActivity?.State=="working"));
            if(hidden)return _current;
            string next=_current;
            if(status.Claude.NeedsInput!=status.Codex.NeedsInput)next=status.Claude.NeedsInput?"claude":"codex";
            else if(status.Codex.CompletionActive)next="codex";
            else if(selected is "claude" or "codex")next=selected;
            else if((status.Claude.State=="working")!=(status.Codex.State=="working"))next=status.Claude.State=="working"?"claude":"codex";
            else if(monotonicMilliseconds-_switchedAt >= (status.Claude.State=="working"?2000:6000))next=_current=="claude"?"codex":"claude";
            if(next!=_current){_current=next;_switchedAt=monotonicMilliseconds;}
            return _current;
        }
    }
}
