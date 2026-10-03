using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private int _benchmarkRunning;
    private volatile string _benchmarkDiagnostic="尚未测速";
    private volatile string _wifiPowerDiagnostic="未采样";
    internal string BenchmarkDiagnostic=>_benchmarkDiagnostic;
    internal static string FormatBenchmark(string result) {
        string[] rows=result.Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(rows.Length==0)return "尚未测速";
        var lines=new List<string>{rows[0] switch {"running"=>"测速中（仅统计有效数据）", "complete"=>"已完成可用通道测速", "cancelled"=>"已停止", "failed"=>"测速未全部完成", "unavailable"=>"当前模式没有可测速的连接", _=>rows[0]}};
        foreach(string row in rows.Skip(1)) {
            var fields=row.Split(',');
            if(fields.Length>=4) {
                string direction=fields[1]=="up"?"上传":"下载";
                if(fields.Length>=6&&long.TryParse(fields[3],out long bytes)&&long.TryParse(fields[4],out long ms)) {
                    lines.Add($"{fields[0]} {direction} 第 {fields[2]} 次：{bytes/1024.0:F0} KiB，{ms/1000.0:F2} 秒，{(ms>0?bytes*1000.0/ms/1024:0):F1} KiB/s"+(fields[5]=="0"?"":$"，失败代码 {fields[5]}"));
                    if(fields.Length==15)lines.Add($"  编码 {fields[7]} ms · 传输等待 {fields[8]} ms · 解码 {fields[9]} ms；内部内存最低 {fields[12]} B，最大连续块最低 {fields[14]} B");
                }
                else lines.Add($"{fields[0]} {direction} 第 {fields[2]} 次：{fields[3]} 字节");
            } else lines.Add(row=="starting"?"正在准备…":row);
        }
        return string.Join(Environment.NewLine,lines);
    }
    private async Task<string> BenchmarkCommandAsync(string action,CancellationToken token) {
        await _usbGate.WaitAsync(token);
        try {
            var pair=_store.Current;
            if(!_usbRpcBinary||pair is null||_usbPort is not {} port)throw new IOException("请连接已升级 TAB5 的 USB，USB 用于读取测速结果。");
            Send(port,new{version=1,type="tab5_benchmark",deviceId=pair.DeviceId,action});
            using var reply=await ReadReplyAsync(port,"tab5_benchmark",token);
            if(reply.RootElement.GetProperty("deviceId").GetString()!=pair.DeviceId)throw new IOException("测速设备不匹配");
            _wifiPowerDiagnostic=reply.RootElement.TryGetProperty("wifiPower",out var power)?power.GetString()??"invalid":"unsupported";
            return reply.RootElement.GetProperty("result").GetString()??"invalid";
        }catch{CloseUsbPort();throw;}
        finally{_usbGate.Release();}
    }
    internal async Task<string> BenchmarkAsync(Action<string> progress,CancellationToken token) {
        if(Busy||Interlocked.CompareExchange(ref _benchmarkRunning,1,0)!=0)throw new InvalidOperationException("请等待当前操作结束后测速。");
        bool started=false;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(16));
        try {
            string result=await BenchmarkCommandAsync("start",deadline.Token);started=true;
            while(true) {
                _benchmarkDiagnostic=result;progress(result);
                if(!result.StartsWith("running;",StringComparison.Ordinal))return result;
                await Task.Delay(500,deadline.Token);result=await BenchmarkCommandAsync("status",deadline.Token);
            }
        }finally {
            if(started&&_benchmarkDiagnostic.StartsWith("running;",StringComparison.Ordinal)) {
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try{_benchmarkDiagnostic=await BenchmarkCommandAsync("cancel",stop.Token);}catch(Exception ex) when(ex is IOException or OperationCanceledException or InvalidOperationException or TimeoutException){_benchmarkDiagnostic="停止请求未确认；设备分段超时后会结束，请查看设备连接。";}
            }
            Interlocked.Exchange(ref _benchmarkRunning,0);
        }
    }
}
