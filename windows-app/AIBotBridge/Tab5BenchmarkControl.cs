using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private int _benchmarkRunning;
    private int _firmwareProbeRunning;
    private int _bleWriteWindowSupported;
    private volatile string _benchmarkDiagnostic="尚未测速";
    private volatile string _wifiPowerDiagnostic="未采样";
    private bool _bleOtaProbeSupported;
    private bool _bleOtaIsolationSupported;
    private bool _bleFirmwareProbeSupported;
    private volatile string _bleRadioDiagnostic="未采样",_wifiIsolationDiagnostic="未采样";
    private volatile string _bleOtaProbeDiagnostic="尚未预检";
    private readonly Tab5BleProbeTrace _bleProbeTrace=new();
    private readonly Tab5BleQueueComparison _bleQueueComparison=new();
    internal string BleQueueComparisonSummary=>_bleQueueComparison.Summary;
    internal string BleOtaProbeDiagnostic=>_bleOtaProbeDiagnostic;
    internal string BenchmarkDiagnostic=>_benchmarkDiagnostic;
    internal static string FormatBenchmark(string result) {
        string[] rows=result.Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(rows.Length==0)return "尚未测速";
        var lines=new List<string>{rows[0] switch {"running"=>"测速中（仅统计有效数据）", "complete"=>"已完成可用通道测速", "cancelled"=>"已停止", "failed"=>"测速未全部完成", "unavailable"=>"当前模式没有可测速的连接",
            "failed:isolation_mode"=>"暂停 Wi-Fi 的预检需要仅蓝牙模式，并保留 USB 连接。",
            "failed:isolation_busy"=>"Wi-Fi 正在恢复，请稍后重试。",
            "failed:isolation_prepare"=>"未确认 Wi-Fi 已暂停，本次未开始传输。",
            "failed:isolation_restore"=>"Wi-Fi 恢复尚未确认，请检查连接与网络。",
            _=>rows[0]}};
        foreach(string row in rows.Skip(1)) {
            var fields=row.Split(',');
            if(fields.Length==3&&fields[0]=="image") {lines.Add($"完整固件 SHA-256：{fields[1]}；{fields[2]} 字节");continue;}
            if(fields.Length>=4) {
                string direction=fields[1]=="up"?"上传":"下载";
                if(fields.Length>=6&&long.TryParse(fields[3],out long bytes)&&long.TryParse(fields[4],out long ms)) {
                    lines.Add($"{fields[0]} {direction} 第 {fields[2]} 次：{bytes/1024.0:F0} KiB，{ms/1000.0:F2} 秒，{(ms>0?bytes*1000.0/ms/1024:0):F1} KiB/s"+(fields[5]=="0"?"":$"，失败代码 {fields[5]}"));
                    if(fields.Length==15)lines.Add($"  编码 {fields[7]} ms · 传输等待 {fields[8]} ms · 解码 {fields[9]} ms；内部内存最低 {fields[12]} B，最大连续块最低 {fields[14]} B");
                }
                else lines.Add($"{fields[0]} {direction} 第 {fields[2]} 次：{fields[3]} 字节");
            } else lines.Add(row switch {"starting"=>"正在准备…","wifi_pause"=>"正在临时暂停 Wi-Fi…","wifi_restore"=>"正在恢复 Wi-Fi…",_=>row});
        }
        return string.Join(Environment.NewLine,lines);
    }
    internal static string ProbeAction(bool supported,bool isolationSupported,bool isolateWifi) {
        if(!supported)throw new NotSupportedException("此固件尚不支持短时预检，请先通过 USB 或 Wi-Fi 安装 0.2.111-ui 或更新版本。");
        if(isolateWifi&&!isolationSupported)throw new NotSupportedException("暂停 Wi-Fi 的对照预检需要 0.2.113-ui；可取消勾选先测原链路。");
        return isolateWifi?"ble_ota_probe_isolated":"ble_ota_probe";
    }
    internal static string NumericProbeDiagnostic(JsonElement reply,string name,int count) {
        if(!reply.TryGetProperty(name,out var value))return "unsupported";
        if(value.ValueKind!=JsonValueKind.String||value.GetString() is not {} text||text.Length>256)return "invalid";
        string[] parts=text.Split(',');
        return parts.Length==count&&parts[0]=="1"&&parts.All(x=>long.TryParse(x,System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out _))?text:"invalid";
    }
    private async Task<string> BenchmarkCommandAsync(string action,CancellationToken token) {
        await _usbGate.WaitAsync(token);
        try {
            var pair=_store.Current;
            if(pair is null)throw new IOException("TAB5 尚未配对。");
            var port=_usbPort;
            if(port is null) {
                var device=UsbDevices().FirstOrDefault(d=>d.Identity.Equals(pair.UsbIdentity,StringComparison.OrdinalIgnoreCase));
                if(device is null)throw new IOException("请连接已升级 TAB5 的 USB，USB 用于读取测速结果。");
                port=GetOrOpenUsbPort(device.Port);
                if(await ReadIdentityAsync(port,token)!=pair.DeviceId)throw new IOException("测速设备不匹配");
            }
            Send(port,new{version=1,type="tab5_benchmark",deviceId=pair.DeviceId,action});
            using var reply=await ReadReplyAsync(port,"tab5_benchmark",token);
            if(reply.RootElement.GetProperty("deviceId").GetString()!=pair.DeviceId)throw new IOException("测速设备不匹配");
            _wifiPowerDiagnostic=reply.RootElement.TryGetProperty("wifiPower",out var power)?power.GetString()??"invalid":"unsupported";
            _bleOtaProbeSupported=reply.RootElement.TryGetProperty("bleOtaProbe",out var probe)&&probe.ValueKind==JsonValueKind.True;
            _bleFirmwareProbeSupported=reply.RootElement.TryGetProperty("bleFirmwareProbe",out var firmware)&&firmware.ValueKind==JsonValueKind.True;
            _bleOtaIsolationSupported=reply.RootElement.TryGetProperty("bleOtaProbeIsolation",out var isolation)&&isolation.ValueKind==JsonValueKind.True;
            _bleRadioDiagnostic=NumericProbeDiagnostic(reply.RootElement,"bleRadio",16);
            _wifiIsolationDiagnostic=NumericProbeDiagnostic(reply.RootElement,"wifiIsolation",7);
            return reply.RootElement.GetProperty("result").GetString()??"invalid";
        }catch{CloseUsbPort();throw;}
        finally{_usbGate.Release();}
    }
    internal static async Task<string> ReadBenchmarkStatusAsync(Func<CancellationToken,Task<string>> read,CancellationToken token) {
        // Only readback is repeatable. Never resend a start command after a lost ACK.
        for(int attempt=0;;attempt++) {
            token.ThrowIfCancellationRequested();
            try{return await read(token);}
            catch(IOException) when(attempt<3&&!token.IsCancellationRequested) {await Task.Delay(250,token);}
            catch(TimeoutException) when(attempt<3&&!token.IsCancellationRequested) {await Task.Delay(250,token);}
        }
    }
    internal async Task<string> BenchmarkAsync(Action<string> progress,CancellationToken token,bool bleOtaProbe=false,bool isolateWifi=false,bool compareQueue=false,bool firmwareProbe=false) {
        if(firmwareProbe&&(!bleOtaProbe||compareQueue))throw new InvalidOperationException("完整固件预检必须单独运行。");
        if(compareQueue&&(!bleOtaProbe||!isolateWifi))throw new InvalidOperationException("窗口对照需要勾选「预检时暂停 Wi-Fi」。");
        if(Busy||Interlocked.CompareExchange(ref _benchmarkRunning,1,0)!=0)throw new InvalidOperationException("请等待当前操作结束后测速。");
        if(firmwareProbe)Interlocked.Exchange(ref _firmwareProbeRunning,1);
        try {
            if(!compareQueue)return await BenchmarkPassAsync(progress,token,bleOtaProbe,isolateWifi,firmwareProbe);
            int imageBytes=Volatile.Read(ref _ota)?.Image.Length??0;
            return await _bleQueueComparison.RunAsync(async(_,ct)=> {
                if(Volatile.Read(ref _bleWriteWindowSupported)<64)throw new NotSupportedException("窗口对照需要 0.2.114-ui，并等待蓝牙重新认证连接后再开始。");
                string value=await BenchmarkPassAsync(progress,ct,true,true);
                return new Tab5BleQueueProbe(value,_bleRadioDiagnostic,_wifiIsolationDiagnostic,_bleProbeTrace.Json,imageBytes);
            },token,compareWindow:true);
        }finally {Interlocked.Exchange(ref _firmwareProbeRunning,0);Interlocked.Exchange(ref _benchmarkRunning,0);}
    }
    private async Task<string> BenchmarkPassAsync(Action<string> progress,CancellationToken token,bool bleOtaProbe,bool isolateWifi,bool firmwareProbe=false) {
        bool started=false;
        var image=Volatile.Read(ref _ota);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(firmwareProbe?TimeSpan.FromSeconds(160):bleOtaProbe?TimeSpan.FromSeconds(isolateWifi?40:30):TimeSpan.FromMinutes(16));
        try {
            string action="start";
            if(bleOtaProbe) {
                _bleProbeTrace.Begin(Environment.TickCount64,firmwareProbe?160000:35000);
                _bleOtaProbeDiagnostic="预检中（仅蓝牙下载，不写 Flash）";
                await BenchmarkCommandAsync("status",deadline.Token);
                if(firmwareProbe) {
                    if(image is null)throw new InvalidOperationException("请先在固件升级页选择需要预检的固件。");
                    if(!_bleFirmwareProbeSupported)throw new NotSupportedException("完整固件预检需要先通过 USB 或 Wi-Fi 安装 0.2.116-ui。");
                    action=isolateWifi?"ble_firmware_probe_isolated":"ble_firmware_probe";
                } else action=ProbeAction(_bleOtaProbeSupported,_bleOtaIsolationSupported,isolateWifi);
            }
            string result=await BenchmarkCommandAsync(action,deadline.Token);started=true;
            while(true) {
                _benchmarkDiagnostic=result;progress(result);
                if(!result.StartsWith("running;",StringComparison.Ordinal)) {
                    if(bleOtaProbe)_bleOtaProbeDiagnostic=(isolateWifi?"暂停 Wi-Fi 预检。":"常规预检。")+(firmwareProbe?
                        Tab5FirmwareProbe.Format(result,image!.Sha256,image.Image.Length):Tab5BleOtaEstimate.Format(result,image?.Image.Length??0));
                    return result;
                }
                await Task.Delay(500,deadline.Token);result=await ReadBenchmarkStatusAsync(ct=>BenchmarkCommandAsync("status",ct),deadline.Token);
            }
        }finally {
            if(started&&_benchmarkDiagnostic.StartsWith("running;",StringComparison.Ordinal)) {
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try{_benchmarkDiagnostic=await BenchmarkCommandAsync("cancel",stop.Token);}catch(Exception ex) when(ex is IOException or OperationCanceledException or InvalidOperationException or TimeoutException){_benchmarkDiagnostic="停止请求未确认；设备分段超时后会结束，请查看设备连接。";}
            }
            if(bleOtaProbe)_bleProbeTrace.End(Environment.TickCount64);
            if(bleOtaProbe&&_bleOtaProbeDiagnostic.StartsWith("预检中",StringComparison.Ordinal))_bleOtaProbeDiagnostic="预检未完成，不安排蓝牙整包升级。请检查所选通道和连接。";
        }
    }
}
