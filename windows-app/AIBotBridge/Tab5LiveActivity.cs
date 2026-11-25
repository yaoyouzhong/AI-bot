using System.Collections.Concurrent;
using System.Text.Json;

namespace AIBotBridge;

// Desktop sessions can buffer rollout writes for an entire turn. Read their
// existing desktop owner; this path never opens a thread or sends a message.
internal sealed class Tab5LiveActivity(Func<string,CancellationToken,Task<Tab5CodexDesktop.State>> read,
    Func<string,string?>? localDisplayActivity=null,Func<long>? clock=null)
{
    private sealed record Entry(CodexLifecycleTracker.TaskActivity Activity,long CheckedAt,string? DisplayFallback=null);
    private readonly ConcurrentDictionary<string,Entry> _entries=new();
    private long Now=>clock?.Invoke()??Environment.TickCount64;
    private long _next;
    private int _busy,_cursor;
    internal IReadOnlyList<CodexLifecycleTracker.TaskActivity> Fresh()=>_entries.Values
        .Where(e=>Now-e.CheckedAt<60000&&e.Activity.CompanionState!="unknown").Select(e=>e.Activity).ToArray();
    // A failed/expired owner probe remains uncertain for non-display consumers.
    internal IReadOnlyList<string> Uncertain()=>_entries.Where(e=>e.Value.Activity.CompanionState=="unknown"||Now-e.Value.CheckedAt>=60000)
        .Select(e=>e.Key).ToArray();
    // Keep disk fallback out of Fresh(), global status, reply readiness and
    // control. Only a recent failed connection may supply companion metadata.
    internal void ApplyProjection(Dictionary<string,CodexLifecycleTracker.TaskActivity> activity) {
        long now=Now;
        foreach(var entry in _entries.ToArray()) {
            var value=entry.Value;long age=now-value.CheckedAt;
            if(age<60000&&value.Activity.CompanionState!="unknown")activity[entry.Key]=value.Activity;
            else activity[entry.Key]=(activity.GetValueOrDefault(entry.Key)??new(entry.Key,"unknown",0)) with {
                CompanionState=age is >=0 and <15000&&value.DisplayFallback is ("working" or "idle")?value.DisplayFallback:"unknown"
            };
        }
    }
    internal void Refresh(IReadOnlyList<Tab5CodexTask> catalog,CancellationToken stop) {
        if(Environment.TickCount64<Interlocked.Read(ref _next)||Interlocked.CompareExchange(ref _busy,1,0)!=0)return;
        Interlocked.Exchange(ref _next,Environment.TickCount64+10000);
        var known=catalog.Select(t=>t.Id).ToHashSet();
        foreach(var id in _entries.Keys)if(!known.Contains(id))_entries.TryRemove(id,out _);
        var ordered=catalog.OrderByDescending(t=>t.UpdatedAt).ToArray();
        var batch=ordered.Take(8).Concat(ordered.Where(t=>_entries.TryGetValue(t.Id,out var entry)&&(entry.Activity.State=="working"||entry.DisplayFallback=="working")))
            .Concat(ordered.Skip(_cursor).Take(16)).DistinctBy(t=>t.Id).ToArray();
        _cursor=ordered.Length==0?0:(_cursor+16)%ordered.Length;
        _=Task.Run(async()=>{try{await RefreshAsync(batch,stop);}finally{Volatile.Write(ref _busy,0);}});
    }
    internal async Task RefreshAsync(IReadOnlyList<Tab5CodexTask> tasks,CancellationToken stop) {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(8000);
        try {
            await Parallel.ForEachAsync(tasks,new ParallelOptions{MaxDegreeOfParallelism=2,CancellationToken=deadline.Token},async(task,ct)=> {
                using var request=CancellationTokenSource.CreateLinkedTokenSource(ct);request.CancelAfter(2500);
                try {
                    var state=(await read(task.Id,request.Token)).Current;
                    string status=state.Waiting?"waiting":state.Runtime=="active"?"working":state.Runtime=="idle"?"idle":"unknown";
                    var stamp=Tab5CodexDesktop.Child(state.Turn,"turnStartedAtMs");
                    long at=stamp.ValueKind==JsonValueKind.Number&&stamp.TryGetDouble(out var ms)?(long)(ms/1000):0;
                    string actual=state.Runtime=="systemError"?"blocked":status;
                    _entries[task.Id]=new(new(task.Id,status,Math.Max(task.UpdatedAt,at),state.TurnId,state.Runtime=="idle"&&state.Status=="completed",
                        CompanionState:actual,WaitingKind:state.WaitingKind,Outcome:state.Status),Now);
                }catch(TimeoutException) {
                    // ReadActivityAsync's named-pipe ConnectAsync times out
                    // before connecting; snapshot/request timeouts use cancellation.
                    string? fallback=ct.IsCancellationRequested||stop.IsCancellationRequested?null:
                        (localDisplayActivity??Tab5CodexCatalog.LocalDisplayActivity)(task.Id);
                    _entries[task.Id]=new(new(task.Id,"unknown",task.UpdatedAt,CompanionState:"unknown"),Now,fallback);
                }catch(Exception ex) when(ex is IOException or OperationCanceledException or JsonException or InvalidOperationException or UnauthorizedAccessException) {
                    _entries[task.Id]=new(new(task.Id,"unknown",task.UpdatedAt,CompanionState:"unknown"),Now);
                }
            });
        }catch(OperationCanceledException) { }
    }
}
