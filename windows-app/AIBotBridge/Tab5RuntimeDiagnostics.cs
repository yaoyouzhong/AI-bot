using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    private readonly object _runtimeDiagnosticLock=new();
    private sealed record RuntimeProbe(string DeviceId,string HostId,string Id,string Transport,long Expires,long Started,TaskCompletionSource<JsonElement> Completion);
    private sealed record RuntimeSample(string DeviceId,string Transport,long Tick,DateTimeOffset Received,string Body);
    private RuntimeProbe? _runtimeProbe;
    private RuntimeSample? _runtimeSample;
    private Task? _runtimeRefresh;
    private volatile string _runtimeDiagnosticResult="";
    internal static bool SupportsRuntimeDiagnostics(string firmware)=>firmware.EndsWith("-ui",StringComparison.Ordinal)&&
        Version.TryParse(firmware[..^3],out var version)&&version>=new Version(0,2,250);
    private object? RuntimeDiagnosticCommand {get{lock(_runtimeDiagnosticLock)return _runtimeProbe is {} p?
        new{version=1,requestId=p.Id,transport=p.Transport,expiresAt=p.Expires}:null;}}
    private string DeviceHealthDiagnostic {
        get {
            var sample=Volatile.Read(ref _runtimeSample);
            if(sample is null||sample.DeviceId!=PairedId)return "设备运行：等待当前绑定设备的诊断"+(_runtimeDiagnosticResult.Length>0?"；"+_runtimeDiagnosticResult:"");
            long age=Math.Max(0,Environment.TickCount64-sample.Tick);
            return $"诊断采样：{sample.Transport}；接收时间：{sample.Received:yyyy-MM-dd HH:mm:ss}；年龄：{age} ms；{(age>10000?"已过期，仅供参考":"实时")}\n"+
                sample.Body+(_runtimeDiagnosticResult.Length>0?"\n诊断读取："+_runtimeDiagnosticResult:"");
        }
    }
    internal async Task<string> RefreshDiagnosticSummaryAsync(CancellationToken token) {
        var sample=Volatile.Read(ref _runtimeSample);
        if(sample is not null&&sample.DeviceId==PairedId&&Environment.TickCount64-sample.Tick<(sample.Transport=="USB"?8000:2000))return DiagnosticSummary;
        if(!SupportsRuntimeDiagnostics(DeviceView.Firmware)) {
            _runtimeDiagnosticResult="当前固件仅提供 USB 详细诊断；无线诊断需要 0.2.250-ui 或更新版本";
            return DiagnosticSummary;
        }
        Task pending;
        lock(_runtimeDiagnosticLock) {
            if(_runtimeRefresh is null||_runtimeRefresh.IsCompleted) {
                string transport=WirelessBootTransport;var pair=_store.Current;
                if(pair is null||transport.Length==0||_stopping||BackgroundTransferPaused) {
                    _runtimeDiagnosticResult="当前没有可读取的绑定设备连接，保留上次采样";return DiagnosticSummary;
                }
                var probe=new RuntimeProbe(pair.DeviceId,Tab5HostBinding.Id(pair),Guid.NewGuid().ToString("N"),transport,
                    DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds(),Environment.TickCount64,new(TaskCreationOptions.RunContinuationsAsynchronously));
                _runtimeProbe=probe;_runtimeRefresh=FetchRuntimeDiagnosticsAsync(probe);
            }
            pending=_runtimeRefresh;
        }
        await pending.WaitAsync(token);return DiagnosticSummary;
    }
    private async Task FetchRuntimeDiagnosticsAsync(RuntimeProbe probe) {
        try {
            // Yield before publishing: ConnectionHealth reads the probe under
            // its own lock, and callers may arrive concurrently with Publish.
            await Task.Yield();
            JsonElement snapshot;
            for(int attempt=0;;attempt++) {
                Republish();
                try {snapshot=await probe.Completion.Task.WaitAsync(TimeSpan.FromSeconds(8));break;}
                catch(TimeoutException) when(attempt==0) {
                    // A device forced to BLE can still poll Wi-Fi in standby.
                    // Its UI only consumes commands from the selected link.
                    // Retry the other live link to this same bound host, with
                    // a new challenge; late replies to the old one stay invalid.
                    var current=_store.Current;
                    string other=probe.Transport=="Wi-Fi"?(DeviceView.Bluetooth=="已连接"?"BLE":""):
                        WifiBridgeReady(Environment.TickCount64,Interlocked.Read(ref _wifiRequestAt))?"Wi-Fi":"";
                    if(current is null||current.DeviceId!=probe.DeviceId||Tab5HostBinding.Id(current)!=probe.HostId||
                       other.Length==0||_stopping||BackgroundTransferPaused)throw;
                    lock(_runtimeDiagnosticLock) {
                        probe=new RuntimeProbe(probe.DeviceId,probe.HostId,Guid.NewGuid().ToString("N"),other,
                            DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds(),Environment.TickCount64,
                            new(TaskCreationOptions.RunContinuationsAsynchronously));
                        _runtimeProbe=probe;
                    }
                }
            }
            var pair=_store.Current;
            if(pair is null||pair.DeviceId!=probe.DeviceId||Tab5HostBinding.Id(pair)!=probe.HostId)throw new IOException("设备绑定已变化");
            var existing=Volatile.Read(ref _runtimeSample);
            if(existing is null||existing.DeviceId!=probe.DeviceId||existing.Tick<probe.Started)
                RecordRuntimeHealth(snapshot,probe.Transport);
            ObserveOtaStopped(snapshot,CaptureOtaStopProbe(probe.Id,probe.Started));
            _runtimeDiagnosticResult="";
        }catch(TimeoutException){_runtimeDiagnosticResult="无线读取超时，未取得新采样；上次数据保留";}
        catch(Exception ex) when(ex is IOException or InvalidOperationException or JsonException){_runtimeDiagnosticResult="无线采样未通过校验，保留上次数据";}
        finally {
            lock(_runtimeDiagnosticLock){if(ReferenceEquals(_runtimeProbe,probe))_runtimeProbe=null;}
            if(!_stopping)Republish();
        }
    }
    private (int Status,object Body) ReceiveRuntimeDiagnostics(JsonElement root,string deviceId,string transport) {
        lock(_runtimeDiagnosticLock) {
            var p=_runtimeProbe;var pair=_store.Current;
            if(p is null||pair is null||p.DeviceId!=deviceId||p.HostId!=Tab5HostBinding.Id(pair)||p.Transport!=transport||
               Text(root,"requestId")!=p.Id||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()>p.Expires)
                return(409,new{error="runtime_probe_expired"});
            if(!root.TryGetProperty("snapshot",out var snapshot)||!ValidRuntimeSnapshot(snapshot,p.DeviceId,p.HostId))
                return(400,new{error="invalid_runtime_snapshot"});
            return p.Completion.TrySetResult(snapshot.Clone())?(200,new{accepted=true}):(409,new{error="runtime_probe_consumed"});
        }
    }
    internal static bool ValidRuntimeSnapshot(JsonElement value,string deviceId,string hostId) {
        if(value.ValueKind!=JsonValueKind.Object||value.GetRawText().Length>8192||Text(value,"deviceId")!=deviceId||Text(value,"hostId")!=hostId||
           !SupportsRuntimeDiagnostics(Text(value,"firmware")))return false;
        bool Number(string key,long min,long max)=>value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out long n)&&n>=min&&n<=max;
        if(!Number("diagnosticVersion",1,1)||!Number("sampleAgeMs",0,8000)||!Number("uptimeMs",0,long.MaxValue)||!Number("uiAgeMs",0,uint.MaxValue)||
           !Number("flushCount",0,uint.MaxValue)||!Number("flushErrors",0,uint.MaxValue)||!Number("lcdUnderruns",0,uint.MaxValue))return false;
        foreach(string key in new[]{"displayDiag","cameraDiag","photoDiag","photoUploadDiag"}) {
            string text=Text(value,key);
            if(text.Length is 0 or >1024||text.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not (',' or ';' or '-' or '_' or '.' or ':' or '/' or ' ')))return false;
        }
        return true;
    }
    private void RecordRuntimeHealth(JsonElement root,string transport,int? frameBytes=null,bool? hasResource=null) {
        string Number(string field) => root.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt64(out var n)?n.ToString():"--";
        string ages=root.TryGetProperty("ageMs",out var age)&&age.ValueKind==JsonValueKind.Array
            ?string.Join(",",age.EnumerateArray().Take(3).Select(v=>v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var n)?n.ToString():"--")):"--";
        string wifi=root.TryGetProperty("wifiConnected",out var wifiValue)&&wifiValue.ValueKind==JsonValueKind.True?"已连接":"未连接";
        string body=$"固件：{DiagnosticText(root,"firmware")}；确认时间：{DateTimeOffset.Now:HH:mm:ss}；帧字节：{frameBytes?.ToString()??"--"}；含资源：{hasResource?.ToString()??"--"}；USB资源分片：{_usbResourceChunks}；Wi-Fi：{wifi}；运行毫秒：{Number("uptimeMs")}；重启原因：{Number("resetReason")}；屏保：{Number("saverActive")}；键鼠唤醒：{Number("pcWakeCount")}；界面心跳年龄：{Number("uiAgeMs")}；界面数据年龄：{Number("uiDataAgeMs")}；Codex未读控件：{Number("codexNotice")}；已选任务：{Number("taskSelected")}；输入会话就绪：{Number("inputSessionReady")}；语音检查：{Number("voiceCheck")}；点击时数据年龄：{Number("voiceAgeMs")}；语音阶段：{Number("voiceStage")}；HTTP：{Number("voiceHttp")}；语音往返毫秒：{Number("voiceRequestMs")}；音频序号：{Number("voiceSeq")}；背光设置次数：{Number("backlightChanges")}；亮度：{Number("backlightPercent")}；显示欠载：{Number("lcdUnderruns")}；页面：{Number("uiPage")}；资源位图：{Number("assetsReady")}；已持久化：{Number("assetsCached")}；三通道数据年龄：{ages}；BLE连接次数：{Number("bleConnectCount")}；BLE断开原因：{Number("bleDisconnectReason")}；显示诊断：{DiagnosticText(root,"displayDiag")}；升级准备诊断：{DiagnosticText(root,"otaDiag")}；显示错误：{Number("flushErrors")}；相机诊断：{DiagnosticText(root,"cameraDiag")}；照片诊断：{DiagnosticText(root,"photoDiag")}；上传诊断：{DiagnosticText(root,"photoUploadDiag")}";
        if(root.TryGetProperty("artOrientation",out _))body+="\n艺术屏保方向："+DiagnosticText(root,"artOrientation");
        if(root.TryGetProperty("rpcDiag",out _))body+="\n设备 RPC 失败诊断："+DiagnosticText(root,"rpcDiag");
        if(root.TryGetProperty("usbDiag",out _))body+="\n设备 USB 诊断："+DiagnosticText(root,"usbDiag");
        if(root.TryGetProperty("galleryDiag",out _))body+="\n艺术图片接收："+DiagnosticText(root,"galleryDiag");
        if(root.TryGetProperty("galleryTransferDiag",out _))body+="\n图库接收与写卡："+Tab5GalleryStream.TransferDiagnostic(root);
        if(root.TryGetProperty("wifiDiag",out _))body+="\n设备 Wi-Fi 诊断："+NumericProbeDiagnostic(root,"wifiDiag",16);
        if(root.TryGetProperty("wifiRecoveryDiag",out _))body+="\n设备 Wi-Fi 恢复："+NumericProbeDiagnostic(root,"wifiRecoveryDiag",8);
        if(root.TryGetProperty("wifiHttpDiag",out _))body+="\n设备 Wi-Fi HTTP："+NumericProbeDiagnostic(root,"wifiHttpDiag",9);
        if(root.TryGetProperty("wifiPathDiag",out _))body+="\n设备 Wi-Fi 路径："+NumericProbeDiagnostic(root,"wifiPathDiag",17);
        if(root.TryGetProperty("wirelessBootDiag",out _))body+="\n设备无线启动："+NumericProbeDiagnostic(root,"wirelessBootDiag",4);
        body+=$"\n相机驱动记录：{DiagnosticText(root,"cameraLogHex")}；内存：{Number("internalFree")},{Number("internalLargest")},{Number("psramFree")}";
        long sampleAge=root.TryGetProperty("sampleAgeMs",out var ageValue)&&ageValue.ValueKind==JsonValueKind.Number&&ageValue.TryGetInt64(out long n)?Math.Clamp(n,0,8000):0;
        Volatile.Write(ref _runtimeSample,new RuntimeSample(Text(root,"deviceId"),transport,Environment.TickCount64-sampleAge,DateTimeOffset.Now,body));
    }
    // USB and authenticated wireless reports are formatted by the same method.
    private static string DiagnosticText(JsonElement root,string key,int max=1024) {
        if(!root.TryGetProperty(key,out var v)||v.ValueKind!=JsonValueKind.String)return "--";
        string text=v.GetString()??"";
        return text.Length<=max&&text.All(c=>char.IsAsciiLetterOrDigit(c)||c is ',' or ';' or '-' or '_' or '.' or ':' or '/' or ' ')?text:"[无效诊断]";
    }
}
