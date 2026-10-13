namespace AIBotBridge;

internal sealed record RuntimeHealthOptions(bool Enabled=true, bool Network=true, bool ExpectChatGptProxy=true,
    int ProbeSeconds=30, int SlowMilliseconds=2500, int CpuPercent=90, int MemoryPercent=90,
    int SustainedSeconds=30, bool Throughput=true)
{
    internal void Validate() {
        if(ProbeSeconds is <15 or >300 || SlowMilliseconds is <500 or >15000 || CpuPercent is <50 or >100 ||
            MemoryPercent is <50 or >100 || SustainedSeconds is <10 or >300)
            throw new InvalidOperationException("检测间隔、持续时间或告警阈值超出范围。");
    }
    internal static RuntimeHealthOptions Load() {
        var value=UserPreferenceFile.Read<RuntimeHealthOptions>("runtime-health.json")??new();value.Validate();return value;
    }
}
internal sealed record RuntimeHealthCheck(string Key,string Label,string State,string Summary,string Detail,string Advice,
    DateTimeOffset UpdatedAt,double? LatencyMs=null,string? Route=null)
{
    public double? LastSuccessfulLatencyMs {get;init;}
    public DateTimeOffset? LastSuccessfulAt {get;init;}
}
internal sealed record RuntimeHealthAlert(string Id,string Key,string State,string Title,string Advice,DateTimeOffset Since)
{
    public string ShortAdvice=>Key switch {
        "pac"=>Advice.Contains("重启")?"建议重启 VPN 客户端":"检查 PAC 规则",
        "vpn"=>Title=="目标响应异常"?"检查目标服务":"检查或更换节点",
        "memory"=>"检查内存占用","disk"=>"检查磁盘读写","cpu" or "queue"=>"减少并行任务",_=>"检查监控设置"
    };
}
internal sealed record RuntimeLatencyPoint(long EpochUtc,double? Milliseconds,bool Reachable);
internal sealed record SystemPressure(double? ProcessorQueue,double? DiskQueue,double? DiskLatencyMs,
    double? PagesInputPerSecond,long? AvailableMemoryMb,int LogicalProcessors,DateTimeOffset UpdatedAt);
internal sealed record RuntimeHealthSnapshot(string Session,long Sequence,DateTimeOffset UpdatedAt,
    RuntimeHealthCheck[] Checks,RuntimeHealthAlert[] Alerts,SystemPressure? Pressure=null,
    double? ThroughputMbps=null,DateTimeOffset? ThroughputAt=null,RuntimeLatencyPoint[]? LatencyHistory=null)
{
    public long EpochUtc=>UpdatedAt.ToUnixTimeSeconds();
}

// Alarm lifetime is independent of page refreshes. Brief spikes never create an
// event; two healthy observations clear an event and permit a later recurrence.
internal sealed class RuntimeHealthPolicy
{
    private sealed class Entry {
        internal string Stable="unknown",Candidate="";internal int Count;internal DateTimeOffset Since,Last;
        internal RuntimeHealthAlert? Alert;
    }
    private readonly Dictionary<string,Entry> _entries=[];
    internal string Session {get;}=Guid.NewGuid().ToString("N");
    internal long Sequence {get;private set;}
    internal RuntimeHealthCheck Apply(RuntimeHealthCheck check,int confirmations,TimeSpan sustained,TimeSpan? maximumGap=null) {
        if(!_entries.TryGetValue(check.Key,out var entry))_entries[check.Key]=entry=new();
        if(check.UpdatedAt<=entry.Last)return check with {State=entry.Stable};
        // Sleep/resume or a stopped collector must not count as sustained load.
        if(entry.Last!=default&&check.UpdatedAt-entry.Last>(maximumGap??TimeSpan.FromSeconds(10))) {entry.Candidate="";entry.Count=0;}
        entry.Last=check.UpdatedAt;
        if(check.State is "unknown" or "disabled") {entry.Stable=check.State;entry.Candidate="";entry.Count=0;entry.Alert=null;return check;}
        if(entry.Candidate!=check.State){entry.Candidate=check.State;entry.Count=0;entry.Since=check.UpdatedAt;}
        entry.Count++;
        bool confirmed=entry.Count>=confirmations && (check.State=="ok"||check.UpdatedAt-entry.Since>=sustained);
        if(confirmed&&entry.Stable!=check.State) {
            entry.Stable=check.State;
            entry.Alert=check.State is "warning" or "error"
                ?new(Session+":"+(++Sequence),check.Key,check.State,check.Summary,check.Advice,entry.Since):null;
        }
        if(entry.Alert is {} alert) {
            if(check.State==entry.Stable)entry.Alert=alert with {Advice=check.Advice,Title=check.Summary};
            else return check with {State=entry.Stable,Summary=alert.Title,Advice="正在确认恢复或状态变化。"+alert.Advice};
        }
        return check with {State=confirmed||entry.Stable is "warning" or "error"?entry.Stable:"observing"};
    }
    internal RuntimeHealthAlert[] Alerts=>_entries.Values.Where(e=>e.Alert is not null).Select(e=>e.Alert!)
        .OrderByDescending(a=>a.State=="error").ThenBy(a=>a.Since).ToArray();
}
