namespace AIBotBridge;

internal sealed class Tab5FirmwareObservation(Func<long>? clock=null)
{
    private readonly Func<long> _clock=clock??(()=>Environment.TickCount64);
    private sealed record Seen(string Device,string Version,long At);
    private Seen? _last;
    internal void Observe(string device,string version) {
        if(!Tab5Protocol.ValidId(device)||version.Length is <1 or >31||version.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('.' or '-' or '_')))return;
        Volatile.Write(ref _last,new(device,version,_clock()));
    }
    internal string LastVersion(string? device) {var seen=Volatile.Read(ref _last);return seen is not null&&seen.Device==device?seen.Version:"待连接读取";}
    internal string Summary(string? device,string? offered,bool transferring) {
        if(offered is null)return "未提供固件";
        if(transferring)return offered+" · 正在传输";
        var seen=Volatile.Read(ref _last);
        if(seen is null||seen.Device!=device||_clock()-seen.At is <0 or >15000)return offered+" · 已提供，等待设备确认版本";
        return offered+(seen.Version==offered?" · 设备已运行此版本":" · 可供设备升级");
    }
}
