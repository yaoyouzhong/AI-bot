namespace AIBotBridge;

// Presentation only. Callers must resolve operations against a fresh catalog.
internal sealed class Tab5CatalogDisplay<T>(Func<long>? clock=null)
{
    private readonly object _gate=new();
    private IReadOnlyList<T> _last=[];
    private long _lastAt;
    private long? _emptySince;

    internal (IReadOnlyList<T> Items,bool Fresh) Observe(IReadOnlyList<T>? current)
    {
        lock(_gate) {
            long now=clock?.Invoke()??Environment.TickCount64;
            if(current is {Count:>0}) {
                _last=current;_lastAt=now;_emptySince=null;
                return (current,true);
            }
            if(current is null)_emptySince=null;
            else _emptySince??=now;
            if(_last.Count>0&&now-_lastAt<30000&&(_emptySince is null||now-_emptySince.Value<8000))
                return (_last,false);
            _last=[];
            return ([],current is not null);
        }
    }

    internal void Clear(){lock(_gate){_last=[];_emptySince=null;}}
}
