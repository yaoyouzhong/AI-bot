namespace AIBotBridge;

internal sealed class RuntimeHealthService(Func<SystemMetricsSnapshot?> metrics)
{
    private readonly object _sync=new();
    private RuntimeHealthPolicy _policy=new();
    private readonly Dictionary<string,RuntimeHealthCheck> _checks=[];
    private readonly RuntimeNetworkProbe _network=new();
    private RuntimeHealthOptions _options=new();
    private string? _settingsError;
    private RuntimeHealthSnapshot? _snapshot;
    private RuntimeLatencyPoint[]? _history;
    internal RuntimeHealthSnapshot? Snapshot=>Volatile.Read(ref _snapshot);
    internal RuntimeHealthSnapshot? Capture(RuntimeHealthCheck? guard) {
        if(guard is null)return Snapshot;
        lock(_sync) {
            var now=DateTimeOffset.UtcNow;var snapshot=_snapshot;
            bool fresh=snapshot is not null&&now-snapshot.UpdatedAt<=TimeSpan.FromSeconds(10);
            _checks["bridge"]=Options.Enabled?_policy.Apply(guard,2,TimeSpan.FromSeconds(guard.HandledExternally?0:30)):guard with {State="disabled",Summary="监控已关闭",Advice=""};
            var checks=(snapshot?.Checks??[]).Where(c=>c.Key!="bridge").Select(c=>snapshot is not null&&now-snapshot.UpdatedAt>TimeSpan.FromSeconds(10)?
                c with{State="unknown",Summary="监控采样已过期",Advice="检查桥接程序。",LatencyMs=null}:c).Append(_checks["bridge"]).ToArray();
            var alerts=Options.Enabled?_policy.Alerts.Where(a=>checks.Any(c=>c.Key==a.Key&&c.State is "warning" or "error")).ToArray():[];
            return new(_policy.Session,_policy.Sequence,now,checks,alerts,fresh?snapshot?.Pressure:null,snapshot?.ThroughputMbps,snapshot?.ThroughputAt,
                fresh?snapshot?.LatencyHistory:null);
        }
    }
    internal SystemMetricsSnapshot? Metrics=>metrics();
    internal RuntimeHealthOptions Options=>Volatile.Read(ref _options);
    internal void Reload() {
        try{var options=RuntimeHealthOptions.Load();Volatile.Write(ref _options,options);_settingsError=null;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){_settingsError="监控设置未能读取，请检查设置。";}
        lock(_sync){_checks.Clear();_history=null;_policy=new();Volatile.Write(ref _snapshot,null);}
    }
    internal async Task RunAsync(CancellationToken stop) {
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(stop);stop=lifetime.Token;
        Reload();
        using var pressure=new WindowsPressureSampler();
        var network=RunNetworkAsync(stop);
        try {
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));
            do {
                var options=Options;var now=DateTimeOffset.UtcNow;
                var sample=pressure.Capture(now);
                var observations=SystemChecks(metrics(),sample,options,now);
                var codex=CodexConnectionHealth.Shared.Capture(now);
                if(_settingsError is {} error)observations=observations.Append(new("settings","监控设置","unknown",error,"","",now)).ToArray();
                lock(_sync) {
                    foreach(var check in observations) _checks[check.Key]=options.Enabled?_policy.Apply(check,2,TimeSpan.FromSeconds(options.SustainedSeconds)):
                        check with {State="disabled",Summary="监控已关闭",Advice=""};
                    _checks["codex"]=options.Enabled?_policy.Apply(codex,1,TimeSpan.Zero):codex with {State="disabled",Summary="监控已关闭",Advice=""};
                    Publish(now,options.Enabled?sample:null);
                }
            }while(await timer.WaitForNextTickAsync(stop));
        }finally{lifetime.Cancel();try{await network;}catch(OperationCanceledException) when(stop.IsCancellationRequested){}}
    }
    private async Task RunNetworkAsync(CancellationToken stop) {
        while(!stop.IsCancellationRequested) {
            long started=Environment.TickCount64;
            var options=Options;
            if(options.Enabled) {
                RuntimeNetworkRound round;
                try{round=await _network.CaptureAsync(options,stop);}
                catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) {
                    var now=DateTimeOffset.UtcNow;round=new([new("vpn","VPN / 代理","unknown","网络检测未完成","检测器返回 "+ex.GetType().Name+"。","请查看代理配置或稍后重试。",now),
                        new("pac","PAC 路由","unknown","PAC 状态待确认","","",now)]);
                }
                stop.ThrowIfCancellationRequested();
                lock(_sync) {
                    // Discard a probe made under options replaced while in flight.
                    if(ReferenceEquals(options,Options)) {
                        if(round.RouteChanged&&_snapshot is {} changed)_snapshot=changed with {ThroughputMbps=null,ThroughputAt=null};
                        _history=round.LatencyHistory;
                        foreach(var check in round.Checks)_checks[check.Key]=_policy.Apply(check,check.State=="ok"?2:3,TimeSpan.Zero,TimeSpan.FromSeconds(options.ProbeSeconds*2+30));
                        if(round.ThroughputMbps.HasValue)_snapshot=_snapshot is {} previous?previous with {ThroughputMbps=round.ThroughputMbps,ThroughputAt=round.ThroughputAt}:
                            new(_policy.Session,_policy.Sequence,DateTimeOffset.UtcNow,[],[],ThroughputMbps:round.ThroughputMbps,ThroughputAt:round.ThroughputAt);
                        Publish(DateTimeOffset.UtcNow,_snapshot?.Pressure);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1000,options.ProbeSeconds*1000-(Environment.TickCount64-started))),stop);
        }
    }
    private void Publish(DateTimeOffset now,SystemPressure? pressure) {
        var options=Options;
        var checks=_checks.Values.Select(c=>now-c.UpdatedAt>TimeSpan.FromSeconds(Math.Max(90,options.ProbeSeconds*3))?
            c with {State="unknown",Summary="监控数据已过期",Advice="等待重新检测。"}:c).ToArray();
        var alerts=options.Enabled?_policy.Alerts.Where(a=>checks.Any(c=>c.Key==a.Key&&c.State is "warning" or "error")).ToArray():[];
        Volatile.Write(ref _snapshot,new(_policy.Session,_policy.Sequence,now,checks,alerts,pressure,
            _snapshot?.ThroughputMbps,_snapshot?.ThroughputAt,_history));
    }
    internal static RuntimeHealthCheck[] SystemChecks(SystemMetricsSnapshot? metrics,SystemPressure p,RuntimeHealthOptions o,DateTimeOffset now) {
        bool fresh=metrics is not null&&now-metrics.UpdatedAt<TimeSpan.FromSeconds(10);
        bool cpu=fresh&&metrics!.CpuPercent>=o.CpuPercent;
        bool physical=fresh&&(metrics!.MemoryPercent>=o.MemoryPercent||p.AvailableMemoryMb<512);
        bool commit=p.CommitPercent>=o.MemoryPercent;
        // Pages Input includes mapped files. Require memory pressure AND slow
        // disk I/O before interpreting sustained input as harmful paging.
        bool paging=p.PagesInputPerSecond>=1000&&(p.AvailableMemoryMb<1024||p.CommitPercent>=85)&&p.DiskLatencyMs>=20;
        bool memory=physical||commit||paging;
        bool memoryKnown=fresh||p.CommitPercent.HasValue;
        bool queue=fresh&&metrics!.CpuPercent>=80&&p.ProcessorQueue>=p.LogicalProcessors*2;
        bool disk=p.DiskLatencyMs>=50&&p.DiskQueue>=2;
        string Number(double? n,string unit="")=>n.HasValue?n.Value.ToString("0.#")+unit:"未采集";
        return [new("cpu","CPU",!fresh?"unknown":cpu?"warning":"ok",!fresh?"CPU 状态待确认":cpu?"系统繁忙":"CPU 正常",
                fresh?$"CPU {metrics!.CpuPercent:0}%。":"CPU 数据尚未就绪或已过期。",cpu?"检查持续占用 CPU 的程序。":"",now),
            new("memory","内存",!memoryKnown?"unknown":memory?"warning":"ok",!memoryKnown?"内存状态待确认":physical?"内存紧张":commit?"内存提交量偏高":paging?"内存换页压力偏高":"内存正常",
                $"内存 {(fresh?metrics!.MemoryPercent.ToString("0")+"%":"未采集")}；可用 {Number(p.AvailableMemoryMb," MB")}；提交 {Number(p.CommitPercent,"%")}"+
                $"（{Number(p.CommittedMemoryMb," MB")} / {Number(p.CommitLimitMb," MB")}）；换页读入 {Number(p.PagesInputPerSecond," 页/秒")}。",memory?"关闭不需要的程序，检查内存占用。":"",now),
            new("queue","CPU 等待队列",!fresh||p.ProcessorQueue is null?"unknown":queue?"warning":"ok",!fresh||p.ProcessorQueue is null?"队列状态待确认":queue?"任务等待偏多":"等待队列正常",
                $"待运行线程 {Number(p.ProcessorQueue)}；逻辑处理器 {p.LogicalProcessors}。这是系统调度队列，不是进程数量。",queue?"减少同时运行的重任务，查看 CPU 占用。":"",now),
            new("disk","磁盘响应",p.DiskLatencyMs is null||p.DiskQueue is null?"unknown":disk?"warning":"ok",p.DiskLatencyMs is null||p.DiskQueue is null?"磁盘状态待确认":disk?"磁盘响应慢":"磁盘响应正常",
                $"响应 {Number(p.DiskLatencyMs," ms")}；待处理 I/O {Number(p.DiskQueue)}。",disk?"检查大量读写磁盘的程序。":"",now)];
    }
}
