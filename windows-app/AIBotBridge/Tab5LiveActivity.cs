using System.Collections.Concurrent;
using System.Text.Json;

namespace AIBotBridge;

// Desktop sessions can buffer rollout writes for an entire turn. Read their
// existing desktop owner; this path never opens a thread or sends a message.
internal sealed class Tab5LiveActivity(Func<string,CancellationToken,Task<Tab5CodexDesktop.State>> read)
{
    private sealed record Entry(CodexLifecycleTracker.TaskActivity Activity,long CheckedAt);
    private readonly ConcurrentDictionary<string,Entry> _entries=new();
    private long _next;
    private int _busy,_cursor;
    internal IReadOnlyList<CodexLifecycleTracker.TaskActivity> Fresh()=>_entries.Values
        .Where(e=>Environment.TickCount64-e.CheckedAt<60000).Select(e=>e.Activity).ToArray();
    internal void Refresh(IReadOnlyList<Tab5CodexTask> catalog,CancellationToken stop) {
        if(Environment.TickCount64<Interlocked.Read(ref _next)||Interlocked.CompareExchange(ref _busy,1,0)!=0)return;
        Interlocked.Exchange(ref _next,Environment.TickCount64+10000);
        var ordered=catalog.OrderByDescending(t=>t.UpdatedAt).ToArray();
        var batch=ordered.Take(8).Concat(ordered.Where(t=>_entries.TryGetValue(t.Id,out var entry)&&entry.Activity.State=="working"))
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
                    _entries[task.Id]=new(new(task.Id,status,Math.Max(task.UpdatedAt,at),state.TurnId,state.Runtime=="idle"&&state.Status=="completed"),Environment.TickCount64);
                }catch(Exception ex) when(ex is IOException or OperationCanceledException or JsonException or InvalidOperationException or UnauthorizedAccessException) {
                    _entries.TryRemove(task.Id,out _);
                }
            });
        }catch(OperationCanceledException) { }
    }
}
