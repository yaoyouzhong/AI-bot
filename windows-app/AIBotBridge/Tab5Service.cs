using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// One collector (BridgeRuntime), independent device sessions. Never writes old display settings.
internal sealed partial class Tab5Service : IDisposable
{
    private readonly Tab5PairingStore _store;
    private readonly object _lifecycle=new();
    private readonly CancellationTokenSource _lifetime=new();
    private bool _stopping;
    private int _voiceRequests;
    internal CancellationToken LifetimeToken=>_lifetime.Token;
    internal void BeginStop(){lock(_lifecycle){if(Busy)throw new InvalidOperationException("TAB5 正在录音、升级或安装，请结束后再试。");_stopping=true;_lifetime.Cancel();}}

    private readonly SemaphoreSlim _usbGate = new(1,1);
    private SerialPort? _usbPort;
    internal Func<IReadOnlyList<FlashUsbDevice>> UsbDevices {get;set;}=FlashDeviceDiscovery.Read;
    private volatile string _hidDiagnostic="未触发";
    private readonly string _session=Guid.NewGuid().ToString("N");
    private byte[]? _frame;
    private StatusSnapshot? _lastSnapshot;
    private long _sequence;
    private readonly Tab5Assets _assets=new();
    private readonly object _publishLock=new();
    private readonly Tab5CodexTasks _codexTasks;
    private Tab5OtaPackage? _ota;
    private readonly Tab5FirmwareObservation _firmware=new();
    private int _otaTransfers;
    internal bool BackgroundTransferPaused=>Volatile.Read(ref _firmwareProbeRunning)>0||Volatile.Read(ref _otaTransfers)>0||Environment.TickCount64<Interlocked.Read(ref _rpcOtaUntil);
    private volatile string _otaTransferDiagnostic="尚无升级传输";
    private volatile string _httpReadDiagnostic="尚无 HTTP 回复读取";
    internal void RecordHttpRead(string phase,long started,int status=0,int bytes=0)=>
        _httpReadDiagnostic=$"{DateTime.Now:HH:mm:ss} {phase}; elapsed={Environment.TickCount64-started}ms; status={status}; bytes={bytes}";
    internal async Task TransferOtaAsync(Stream stream,byte[] image,string id,string capability,CancellationToken token,string encoding="") {
        bool fast=Tab5OtaFlow.Fast(capability);long started=Environment.TickCount64;
        bool compressed=fast&&encoding==Tab5OtaCompression.StreamEncoding;
        OtaTransferActive(true);
        try {
            token.ThrowIfCancellationRequested();
            byte[] payload=compressed?Tab5OtaCompression.Stream(image):image;
            await Tab5OtaFlow.WriteAsync(stream,payload,fast,token,sent=>
                _otaTransferDiagnostic=$"{(compressed?"Wi-Fi 分块压缩":fast?"TCP流控":"旧版兼容")}；镜像：{image.Length}；实际传输：{sent}/{payload.Length}；耗时毫秒：{Environment.TickCount64-started}；等待设备校验",compressed);
        }catch { _otaTransferDiagnostic=$"传输中断；耗时毫秒：{Environment.TickCount64-started}";throw; }
        finally {OtaTransferActive(false);}
    }
    internal void OtaTransferActive(bool active) {
        lock(_lifecycle){if(active){if(_stopping)throw new OperationCanceledException("TAB5 服务已停止。");Interlocked.Increment(ref _otaTransfers);}else Interlocked.Decrement(ref _otaTransfers);}
    }
    internal string OtaSummary=>HasOta&&Environment.TickCount64<Interlocked.Read(ref _rpcOtaUntil)?Volatile.Read(ref _ota)?.Version+" · 更新处理中，等待设备校验":_firmware.Summary(_store.Current?.DeviceId,Volatile.Read(ref _ota)?.Version,Volatile.Read(ref _otaTransfers)>0);
    internal string OtaNotes=>Volatile.Read(ref _ota)?.Notes??"";
    internal bool HasOta=>Volatile.Read(ref _ota) is not null;
    internal void OfferOta(string path)=>OfferOta(Tab5OtaPackage.Load(path));
    internal void OfferOta(Tab5OtaPackage image) {lock(_lifecycle){if(_stopping||Busy)throw new InvalidOperationException("设备忙，请稍后提供固件。");Volatile.Write(ref _ota,image);}}
    internal void CancelOta()=>Volatile.Write(ref _ota,null);
    private ITab5VoiceEndpoint? _voice;
    private readonly Dictionary<string,long> _voiceNonces=[];
    internal void AttachVoice(ITab5VoiceEndpoint voice)=>_voice=voice;
    internal bool AllowUnregisteredInstall {get;set;}
    internal string WindowTitle {get;set;}="TAB5";
    internal void ShowVoiceSettings(IWin32Window owner){if(_voice is Tab5VoiceHost host)host.ShowNamedSettings(owner,WindowTitle+" · 语音设置");else _voice?.ShowSettings(owner);}
    // The installed Doubao keyboard hook ignores SendInput. Ask the paired
    // TAB5 to send the shortcut through its composite USB keyboard interface.
    internal bool TryVoiceHidToggle(string shortcut)
    {
        if(Environment.GetEnvironmentVariable("AIBOT_TAB5_USB_PAUSED")=="1")return false;
        _hidDiagnostic="开始键盘切换";
        if(shortcut is not ("LeftAltSpace" or "RightAltSpace" or "CtrlAltSpace"))return false;
        var pairing=_store.Current;
        var name=Volatile.Read(ref _reservedPort);
        if(pairing is null||string.IsNullOrEmpty(name)){_hidDiagnostic="USB 设备不可用";return false;}
        if(!_usbGate.Wait(500)){_hidDiagnostic="USB 正忙，未发送快捷键";return false;}
        string stage="identity";
        try {
            {
                var port=GetOrOpenUsbPort(name);
                port.ReadTimeout=400;
                port.DiscardInBuffer();
                Send(port,new {version=1,type="tab5_ping"});
                if(!VoiceHidReply(port,"tab5_hello",pairing.DeviceId)){_hidDiagnostic="USB 身份确认超时，未发送快捷键";return false;}
                stage="toggle";
                Send(port,new {version=1,type="tab5_hid_toggle",shortcut});
                if(!VoiceHidReply(port,"tab5_hid_probe_start",pairing.DeviceId)){_hidDiagnostic="快捷键已请求，启动确认未返回";return false;}
                stage="confirm";
                // CDC and HID stay enumerated together. Keep DTR and the port
                // stable while the device finishes its keyboard report.
                long until=Environment.TickCount64+10000;
                while(Environment.TickCount64<until) {
                    Send(port,new {version=1,type="tab5_hid_status"});
                    if(VoiceHidStatus(port,pairing.DeviceId)) {
                        _hidDiagnostic="键盘已确认";
                        return true;
                    }
                    Thread.Sleep(100);
                }
            }
            _hidDiagnostic="键盘确认超时";
            return false;
        } catch(Exception ex) when(ex is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception) {
            _hidDiagnostic=$"{stage}: {ex.GetType().Name} / 0x{ex.HResult:X8}";
            RecordUsbFailure("voice_hid_"+stage,ex);
            CloseUsbPort();
            return false;
        } finally { _usbGate.Release(); }
    }
    private static bool VoiceHidReply(SerialPort port,string type,string deviceId)
    {
        long deadline=Environment.TickCount64+1200;
        while(Environment.TickCount64<deadline) {
            string line;
            try{line=port.ReadLine();}catch(TimeoutException){continue;}
            if(!line.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal))continue;
            try {
                using var doc=JsonDocument.Parse(line[Tab5Protocol.Prefix.Length..]);
                var root=doc.RootElement;
                if(root.TryGetProperty("type",out var t)&&t.GetString()==type&&
                    root.TryGetProperty("deviceId",out var id)&&id.GetString()==deviceId)return true;
            }catch(JsonException){ }
        }
        return false;
    }
    private static bool VoiceHidStatus(SerialPort port,string deviceId)
    {
        long until=Environment.TickCount64+500;
        while(Environment.TickCount64<until) {
            string line;
            try{line=port.ReadLine();}catch(TimeoutException){continue;}
            if(!line.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal)){
                try{File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"tab5-voice-panic.log"),$"{DateTimeOffset.Now:O} {line}\n");}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){ }
                continue;
            }
            try {
                using var doc=JsonDocument.Parse(line[Tab5Protocol.Prefix.Length..]);
                var root=doc.RootElement;
                if(root.TryGetProperty("type",out var type)&&type.GetString()=="hid_status"&&
                    root.TryGetProperty("deviceId",out var id)&&id.GetString()==deviceId&&
                    root.TryGetProperty("valid",out var valid)&&valid.ValueKind==JsonValueKind.True&&
                    root.TryGetProperty("stage",out var stage)&&stage.TryGetInt32(out int step)&&step==9&&
                    root.TryGetProperty("error",out var error)&&error.TryGetInt32(out int code)&&code==0)return true;
            }catch(JsonException){ }
        }
        return false;
    }
    private readonly HashSet<string> _actionNonces=[];
    private readonly Queue<string> _actionNonceOrder=new();
    private readonly object _actionLock=new();
    private long _lastActionTick,_lastReadTick,_lastDraftTick;
    private readonly Dictionary<int,(string Id,int Offset)> _usbAssets=[];
    private long _usbResourceChunks;
    private string? _host;
    private int _port;
    private long _publishedAt;
    private volatile string _usbStatus="未配对";
    private volatile string _bleStatus="未配对";
    private volatile string _bleDiagnostic="尚无传输";
    private volatile string _bleRpcDiagnostic="尚无请求";
    private volatile string _bleVoiceDiagnostic="尚无请求";
    private volatile string _readBatchDiagnostic="尚无批次";
    private volatile string _bleLastFailure="无";
    private volatile string _usbRpcLastFailure="无";
    private volatile string _usbRpcFirstFailure="无";
    private volatile string _deviceHealth="设备运行：等待诊断";
    private readonly Tab5CrashDiagnostics _crashes=new();
    private long _wifiRequestAt,_wifiReportAt,_usbAckAt,_bleAckAt;
    internal long LastCommunicationAt=>Math.Max(Interlocked.Read(ref _wifiRequestAt),Math.Max(Interlocked.Read(ref _usbAckAt),Interlocked.Read(ref _bleAckAt)));
    private int _installBusy;
    internal bool Busy=>Volatile.Read(ref _benchmarkRunning)>0||Volatile.Read(ref _rpcRequests)>0||Environment.TickCount64<Interlocked.Read(ref _rpcOtaUntil)||Volatile.Read(ref _voiceRequests)>0||Volatile.Read(ref _otaTransfers)>0||Volatile.Read(ref _installBusy)>0||(_voice as Tab5VoiceHost)?.Busy==true;
    internal string? PairedId=>_store.Current?.DeviceId;
    internal DeviceView DeviceView { get {long now=Environment.TickCount64;bool Recent(long at)=>at>0&&now-at<15000;bool usb=Recent(Interlocked.Read(ref _usbAckAt)),wifi=Recent(Interlocked.Read(ref _wifiRequestAt)),ble=Recent(Interlocked.Read(ref _bleAckAt));return new(usb||wifi||ble,Busy?"正在处理":usb||wifi||ble?"在线":"离线",$"USB：{(usb?"已连接":"未连接")}  Wi-Fi：{(wifi?"已连接":"未连接")}  蓝牙：{(ble?"已连接":"未连接")}\n当前通道：{(usb?"USB":wifi?"Wi-Fi":ble?"蓝牙":"无")}",_firmware.LastVersion(PairedId),usb?"已连接":"未连接",wifi?"已连接":"未连接",ble?"已连接":"未连接",usb?"USB":wifi?"Wi-Fi":ble?"蓝牙":"无");}}
    private volatile bool _wifiReportedConnected;
    private volatile string _voiceAuthStatus="尚无语音请求";
    internal string DiagnosticSummary => ProfileDiagnostic+GalleryDiagnostic+"\n"+Summary+"\n"+_deviceHealth+"\n语音鉴权："+_voiceAuthStatus+"；键盘诊断："+_hidDiagnostic+"\n语音会话："+(_voice as Tab5VoiceHost)?.Diagnostic+"\nCodex 直达："+Tab5QuickConsole.NavigationDiagnostic+"\nCodex 草稿："+Tab5CodexComposer.Diagnostic+"\nCodex 快捷发送："+Tab5CodexComposer.SubmitDiagnostic+"\nCodex 清空："+Tab5CodexComposer.ClearDiagnostic+"\nCodex 发送："+_codexTasks.SubmitDiagnostic+"\n蓝牙传输："+_bleDiagnostic+"\n历史读取："+_codexTasks.ReadDiagnostic+"; "+_readBatchDiagnostic+"\nHTTP 回复读取："+_httpReadDiagnostic+"\n蓝牙语音："+_bleVoiceDiagnostic+"\n蓝牙 RPC："+_bleRpcDiagnostic+"\n蓝牙最近中断："+_bleLastFailure+"\nUSB RPC："+_usbRpcTiming+"\nUSB 调度：otaActive="+BackgroundTransferPaused+"; rpcGateWaitMs="+Interlocked.Read(ref _usbRpcGateWaitMs)+"; rpcGateWaitPeakMs="+Interlocked.Read(ref _usbRpcGateWaitPeakMs)+"\nUSB RPC 首次中断："+_usbRpcFirstFailure+"\nUSB RPC 最近中断："+_usbRpcLastFailure+"\nUSB 控制最近中断："+_usbControlFailure+"\n图片上传："+_imageUploadDiagnostic+"\n固件传输："+_otaTransferDiagnostic+"\n启动核验："+_upgradeDiagnostic+"\nWi-Fi 临时功耗状态："+_wifiPowerDiagnostic+"\n蓝牙升级预检："+_bleOtaProbeDiagnostic+"\n蓝牙预检射频："+_bleRadioDiagnostic+"\n蓝牙预检 Wi-Fi 隔离："+_wifiIsolationDiagnostic+"\n蓝牙预检分段："+_bleProbeTrace.Json+"\n蓝牙参数对照："+_bleQueueComparison.Json+"\n传输测速："+_benchmarkDiagnostic+"\n封面传输："+_assets.Diagnostic;
    private string? _reservedPort;
    internal string CrashDiagnostic=>_crashes.Snapshot;
    // All callers hold _usbGate. Keeping DTR and the CDC handle stable avoids
    // a device open/close cycle every two seconds during audio capture.
    private SerialPort GetOrOpenUsbPort(string name) {
        if(_usbPort is { IsOpen:true } existing && string.Equals(existing.PortName,name,StringComparison.OrdinalIgnoreCase))return existing;
        CloseUsbPort();
        return _usbPort=Open(name);
    }
    private void CloseUsbPort() {
        _usbPacked=false;_usbRpcVersion=_usbTelemetryVersion=0;_usbRpcBinary=false;_usbRpcChunk=2048;var port=_usbPort;_usbPort=null;
        if(!DisposeUsbPort(port))_usbStatus="USB 已断开，等待重新连接";
    }
    internal static bool DisposeUsbPort(IDisposable? port) {
        try {port?.Dispose();return true;}
        // Windows may report ERROR_NO_SUCH_DEVICE while disposing a CDC
        // handle after unplug/reboot. The handle has already been detached;
        // this cleanup failure must not terminate the reconnect worker.
        catch(IOException) {return false;}
    }
    internal string? ReservedPort => Volatile.Read(ref _reservedPort);
    internal bool HasPairedDevice => _store.Current is not null;
    internal string Summary => $"设备：{_store.Current?.DeviceId ?? "未配对"}\nUSB：{_usbStatus}\n蓝牙：{_bleStatus}\nWi-Fi 服务：{_host ?? "等待可用局域网"}:{_port}";
    internal string ConnectionSummary {
        get {
            long now=Environment.TickCount64,request=Interlocked.Read(ref _wifiRequestAt),report=Interlocked.Read(ref _wifiReportAt);
            string wifi=request>0&&now-request<8000?"已连接 · 桥接通信正常":report>0&&now-report<8000?
                _wifiReportedConnected?"已联网 · 等待桥接请求":"未连接":"等待设备回报";
            return $"USB：{_usbStatus}\nWi-Fi：{wifi}\n蓝牙：{_bleStatus}\n电脑服务地址：{_host??"等待可用局域网"}{(_port>0?":"+_port:"")}";
        }
    }
    internal Tab5Service(Tab5PairingStore? store=null,Tab5CodexTasks? codexTasks=null) {
        _store=store??new();
        _codexTasks=codexTasks??new();
        if(_store.Current is { } pair) _reservedPort=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pair.UsbIdentity)?.Port;
    }
    private object ConnectionHealth => new { voiceEnabled=(_voice as Tab5VoiceHost)?.SettingsEnabled,displayCommand=DisplayCommand };
    internal void Publish(StatusSnapshot snapshot)
    {
        lock(_publishLock) {
        _lastSnapshot=snapshot;
        var pairing=_store.Current; if(pairing is null) return;
        var resource=BackgroundTransferPaused?null:_assets.Next(snapshot);var sequence=Interlocked.Increment(ref _sequence);
        var frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,resource,_assets.Ids,_codexTasks.Snapshot(),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        if(frame.Length > Tab5Protocol.MaximumFrame-28 && resource is not null)
            frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,null,_assets.Ids,_codexTasks.Snapshot(),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        for(int limit=64;frame.Length>Tab5Protocol.MaximumFrame-28&&limit>=24;limit-=8)
            frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,null,_assets.Ids,_codexTasks.SnapshotLimited(limit),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        if(frame.Length > Tab5Protocol.MaximumFrame-28) { _usbStatus="状态数据超出协议上限"; return; }
        Volatile.Write(ref _frame,frame); Interlocked.Exchange(ref _publishedAt,Environment.TickCount64);
        }
    }
    private void Republish() {lock(_publishLock){if(_lastSnapshot is {} snapshot)Publish(snapshot);}}
    internal byte[]? CurrentFrame => Environment.TickCount64-Interlocked.Read(ref _publishedAt)<6000 ? Volatile.Read(ref _frame) : null;
    internal byte[]? Discover(byte[] packet,System.Net.IPAddress peer) {
        var pair=_store.Current;var host=_host;var port=_port;
        if(pair is null||host is null||port<=0||
           !System.Net.IPAddress.TryParse(host,out var address)||
           !address.Equals(LanPairingFactory.FindPrivateAddress(peer.ToString(),requireSubnet:true)))return null;
        return Tab5DiscoveryProtocol.Respond(packet,pair.DeviceId,Convert.FromBase64String(pair.Key),host,port);
    }
    internal void SetBinding(LanPairing? binding) { _host=binding?.Address.ToString(); _port=binding?.Port??0; }
    internal byte[]? Respond(string id,string nonce,string proof,string? assets=null,string? assetsProof=null,string? firmware=null,string? firmwareProof=null)
    {
        var pairing=_store.Current; var frame=CurrentFrame;
        if(pairing is null || pairing.DeviceId!=id || !Tab5Protocol.ValidNonce(nonce) || frame is null) return null;
        var key=Convert.FromBase64String(pairing.Key);
        if(!Tab5Protocol.Verify(key,"GET|"+id+"|"+nonce,proof)) return null;
        if(firmware is not null&&firmware.Length<=31&&Tab5Protocol.Verify(key,"FIRMWARE|"+nonce+"|"+firmware,firmwareProof)) {
            ObserveFirmware(id,firmware);
            if(Version.TryParse(firmware.Split('-')[0],out var version)&&version>=new Version(0,2,40))frame=TelemetryFrame(1,true,version>=new Version(0,2,42))??frame;
        }
        Interlocked.Exchange(ref _wifiRequestAt,Environment.TickCount64);
        if(Tab5Assets.ValidIds(assets)&&Tab5Protocol.Verify(key,"ASSETS|"+nonce+"|"+assets,assetsProof))_assets.Acknowledge(assets);
        return Tab5Protocol.Encrypt(key,nonce,frame);
    }
    internal byte[]? OtaImage(string id,string nonce,string proof,string sha) {
        var pairing=_store.Current;var image=Volatile.Read(ref _ota);
        if(pairing is null||pairing.DeviceId!=id||image is null||image.Sha256!=sha||!Tab5Protocol.ValidNonce(nonce))return null;
        var key=Convert.FromBase64String(pairing.Key);
        return Tab5Protocol.Verify(key,$"GET|/tab5/v1/ota/{sha}|{id}|{nonce}",proof)?image.Image:null;
    }
    internal async Task<(int Status, object Body)> SubmitCodexAsync(string id,string nonce,string proof,byte[] packet,CancellationToken token,bool readOnly=false,bool imageOnly=false,bool rpcEnvelope=false)
    {
        var pairing=_store.Current;
        if(pairing is null || pairing.DeviceId!=id || !Tab5Protocol.ValidNonce(nonce) || packet.Length<28 || packet.Length>(imageOnly?Tab5CodexImages.MaxPacket:8192))
            return (401,new {error="unauthorized"});
        var key=Convert.FromBase64String(pairing.Key);
        var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packet)).ToLowerInvariant();
        if(!Tab5Protocol.Verify(key,$"POST|{id}|{nonce}|{digest}",proof))return (401,new {error="unauthorized"});
        byte[] clear;
        try {clear=Tab5Protocol.Decrypt(key,nonce,packet,imageOnly?Tab5CodexImages.MaxPacket:Tab5Protocol.MaximumFrame);} catch(System.Security.Cryptography.CryptographicException) {return (401,new {error="invalid_packet"});}
        JsonDocument doc;
        int imageOffset=0;
        try {
            if(imageOnly&&clear.AsSpan().StartsWith("T5I1"u8)){int metadata=Tab5ImageBinary.MetadataLength(clear);imageOffset=8+metadata;doc=JsonDocument.Parse(clear.AsMemory(8,metadata));}
            else doc=JsonDocument.Parse(clear);
        } catch(Exception ex) when(ex is JsonException or ArgumentException) {return (400,new {error="invalid_request"});}
        using(doc) {
        var root=doc.RootElement;
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("op",out var opKind)&&opKind.ValueKind!=JsonValueKind.String)return (400,new {error="invalid_operation"});
        string operation=root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("op",out var op)&&op.ValueKind==JsonValueKind.String?op.GetString()!:"send";
        if(operation is not ("send" or "read" or "draft-save" or "draft-load" or "receipt" or "steer" or "interrupt" or "image-upload")||imageOnly!=(operation=="image-upload"))return (400,new {error="invalid_operation"});
        bool readOperation=operation is "read" or "draft-load" or "receipt";
        if(readOnly!=readOperation)return (400,new {error="invalid_operation"});
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("session",out var session) || session.ValueKind!=JsonValueKind.String || session.GetString()!=_session ||
           !root.TryGetProperty("taskId",out var taskId) || !root.TryGetProperty("message",out var message) ||
           taskId.ValueKind!=JsonValueKind.String || message.ValueKind!=JsonValueKind.String ||
           !root.TryGetProperty("issuedAt",out var issuedAt) || issuedAt.ValueKind!=JsonValueKind.Number || !issuedAt.TryGetInt64(out var issued) ||
           issued < DateTimeOffset.UtcNow.AddSeconds(-90).ToUnixTimeMilliseconds() || issued > DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds())
            return (400,new {error="invalid_request"});
        lock(_actionLock) {
            if(_actionNonces.Contains(nonce))return (409,new {error="already_submitted"});
            var tick=Environment.TickCount64;
            bool draftOperation=operation is "draft-save" or "draft-load";
            long lastTick=draftOperation?_lastDraftTick:readOnly?_lastReadTick:_lastActionTick;
            if(lastTick!=0 && tick-lastTick<(operation=="read"?100:1000))return (429,new {error="rate_limited"});
            if(draftOperation)_lastDraftTick=tick;else if(readOnly)_lastReadTick=tick;else _lastActionTick=tick;
            _actionNonces.Add(nonce);
            _actionNonceOrder.Enqueue(nonce);
            if(_actionNonceOrder.Count>1024)_actionNonces.Remove(_actionNonceOrder.Dequeue());
        }
        string readTurn=root.TryGetProperty("turnId",out var rt)&&rt.ValueKind==JsonValueKind.String?rt.GetString()!:"";
        int turnOffset=0;
        if(root.TryGetProperty("turnOffset",out var to)&&(to.ValueKind!=JsonValueKind.Number||!to.TryGetInt32(out turnOffset)||turnOffset is < -1 or > 1))return (400,new {error="invalid_turn"});
        int page=-1;
        if(readOnly&&root.TryGetProperty("page",out var pageValue)&&(pageValue.ValueKind!=JsonValueKind.Number||!pageValue.TryGetInt32(out page)||page < -1))return (400,new {error="invalid_page"});
        string requestId=root.TryGetProperty("requestId",out var request)&&request.ValueKind==JsonValueKind.String?request.GetString()!:"";
        string expectedTurn=root.TryGetProperty("expectedTurn",out var expected)&&expected.ValueKind==JsonValueKind.String?expected.GetString()!:"";
        if(imageOnly&&imageOffset>0)return _codexTasks.UploadImageBytes(id,taskId.GetString()!,requestId,clear[imageOffset..]);
        if(imageOnly)return root.TryGetProperty("image",out var image)&&image.ValueKind==JsonValueKind.String
            ?_codexTasks.UploadImage(id,taskId.GetString()!,requestId,image.GetString()!):(400,new{error="invalid_image"});
        string[] images=[];
        if(root.TryGetProperty("images",out var imageIds)) {
            if(imageIds.ValueKind!=JsonValueKind.Array||imageIds.GetArrayLength()>Tab5CodexImages.MaxCount||imageIds.EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String))return(400,new{error="invalid_images"});
            images=imageIds.EnumerateArray().Select(x=>x.GetString()!).ToArray();
        }
        if(operation=="draft-save")return _codexTasks.Draft(taskId.GetString()!,requestId,message.GetString()!,images,id);
        if(operation=="draft-load")return _codexTasks.Draft(taskId.GetString()!,requestId);
        if(operation=="receipt")return await _codexTasks.ReceiptAsync(taskId.GetString()!,requestId,token);
        if(operation=="read") {
            bool inlineReply=root.TryGetProperty("inlineReply",out var inline)&&inline.ValueKind==JsonValueKind.True;
            var result=await _codexTasks.ReadPageAsync(taskId.GetString()!,page,token,readTurn,turnOffset,inlineReply);
            // Public text must never be returned as cleartext over LAN. Bind the
            // encrypted response to this authenticated request's nonce.
            if(inlineReply&&result.Status==200) {
                byte[] data=BoundReadBatch(result.Body);
                using(var batch=JsonDocument.Parse(data))_readBatchDiagnostic=$"bytes={data.Length}; retained={batch.RootElement.GetProperty("neighbors").GetArrayLength()}";
                // RPC already seals the whole response with this request's nonce.
                // New readers avoid a second encrypted/base64 copy of every page.
                // HTTP and legacy RPC retain their existing encrypted envelope.
                if(rpcEnvelope&&root.TryGetProperty("rpcReplyVersion",out var format)&&format.TryGetInt32(out int v)&&v==1) {
                    var body=System.Text.Json.Nodes.JsonNode.Parse(data)!.AsObject();body["rpcReplyVersion"]=1;
                    if(root.TryGetProperty("packedReply",out var pack)&&pack.ValueKind==JsonValueKind.True) {
                        byte[] pageBytes=JsonSerializer.SerializeToUtf8Bytes(body,JsonDefaults.Options);
                        using var compressed=new MemoryStream();
                        using(var zipper=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionLevel.SmallestSize,true))zipper.Write(pageBytes);
                        var packed=new {packedReply=true,size=pageBytes.Length,payload=Convert.ToBase64String(compressed.ToArray())};
                        if(pageBytes.Length<=24576&&JsonSerializer.SerializeToUtf8Bytes(packed,JsonDefaults.Options).Length<pageBytes.Length)return(200,packed);
                    }
                    return(200,body);
                }
                return(200,new {status="loaded",encrypted=Convert.ToBase64String(Tab5Protocol.Encrypt(key,nonce,data))});
            }
            return result;
        }
        return await _codexTasks.SubmitAsync(taskId.GetString()!,message.GetString()!,token,requestId.Length>0?requestId:null,operation,expectedTurn,images,id);
        }
    }
    internal static byte[] BoundReadBatch(object body) {
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(body,JsonDefaults.Options);
        if(data.Length<=16000)return data;
        var compact=System.Text.Json.Nodes.JsonNode.Parse(data)!.AsObject();
        var neighbors=compact["neighbors"]?.AsArray();
        while(data.Length>16000&&neighbors is {Count:>0}) {
            neighbors.RemoveAt(neighbors.Count-1);data=JsonSerializer.SerializeToUtf8Bytes(compact,JsonDefaults.Options);
        }
        return data;
    }
    // Serialize installer/probe access with every TAB5 heartbeat and pairing command.
    internal async Task WithInstallUsbAsync(FlashUsbDevice device, Func<Task> action, CancellationToken token)
    {
        if(!AllowUnregisteredInstall&&_store.Current is {} bound&&bound.UsbIdentity!=device.Identity)throw new InvalidOperationException("安装目标与已配对 TAB5 不一致，未开始刷写。");
        await _usbGate.WaitAsync(token);
        Interlocked.Increment(ref _installBusy);
        string? previous = _reservedPort;
        try {
            Volatile.Write(ref _reservedPort, device.Port);
            CloseUsbPort();
            FlashDeviceSelection.RequireSame(device, FlashDeviceDiscovery.Read());
            await action();
        } finally { CloseUsbPort(); Volatile.Write(ref _reservedPort, previous); Interlocked.Decrement(ref _installBusy);_usbGate.Release(); }
    }
    internal async Task CheckInstalledUsbAsync(FlashUsbDevice device, string mac, string version, string elfSha, CancellationToken token)
    {
        await WithInstallUsbAsync(device, async () => {
            using var port = Open(device.Port);
            long previous = -1;
            for (int i = 0; i < 3; i++) {
                port.DiscardInBuffer(); Send(port, new { version = 1, type = "tab5_ping" });
                using var hello = await ReadReplyAsync(port, "tab5_hello", token);
                previous = Tab5InstallBootCheck.Hello(hello.RootElement, mac, version, previous);
                if (i < 2) await Task.Delay(1200, token);
            }
            Send(port, new { version = 1, type = "tab5_ota_status" });
            using var diagnostic = await ReadReplyAsync(port, "tab5_ota_diagnostic", token);
            Tab5InstallBootCheck.Diagnostic(diagnostic.RootElement, version, elfSha);
        }, token);
    }
    internal async Task PairUsbAsync(FlashUsbDevice device,CancellationToken token,string? replacementId=null)
    {
        Volatile.Write(ref _reservedPort,device.Port);
        await _usbGate.WaitAsync(token);
        try {
            FlashDeviceSelection.RequireSame(device,FlashDeviceDiscovery.Read());
            var port=GetOrOpenUsbPort(device.Port);
            var id=await ReadIdentityAsync(port,token);
            if(_store.Current is { } existing&&existing.DeviceId!=id&&replacementId!=id)throw new Tab5IdentityConflictException(id);
            var pairing=_store.Pair(id,device.Identity);
            Send(port,new {version=1,type="tab5_pair",deviceId=id,key=pairing.Key,host=_host,port=_port});
            await RequireAckAsync(port,"tab5_paired",token);
            _usbStatus="配对完成，等待状态数据";
        } finally { _usbGate.Release(); }
    }
    internal async Task ConfigureWifiAsync(string ssid,string password,CancellationToken token,string? label=null)
    {
        var pairing=_store.Current??throw new InvalidOperationException("请先通过 USB 配对。");
        if(Encoding.UTF8.GetByteCount(ssid) is <1 or >32 || Encoding.UTF8.GetByteCount(password)>63) throw new ArgumentException("Wi-Fi 名称或密码长度无效。");
        if(label is not null)ValidateWifiLabel(ssid,label);
        await _usbGate.WaitAsync(token);
        try {
            var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pairing.UsbIdentity)??throw new IOException("请连接已配对 TAB5 的 USB 数据线。");
            var port=GetOrOpenUsbPort(device.Port);
            if(await ReadIdentityAsync(port,token)!=pairing.DeviceId) throw new IOException("USB 设备身份不匹配。");
            Send(port,new {version=1,type="tab5_wifi",deviceId=pairing.DeviceId,ssid,password,label});
            await RequireAckAsync(port,"tab5_wifi_saved",token);
        } finally { _usbGate.Release(); }
    }
    internal static void ValidateWifiLabel(string ssid,string label) {
        if(Encoding.UTF8.GetByteCount(ssid) is <1 or >32||Encoding.UTF8.GetByteCount(label)>48||label.Any(char.IsControl))
            throw new ArgumentException("请填写原 Wi-Fi 名称；备注最多 16 个汉字或 48 个英文字母，不能包含换行。");
    }
    internal async Task ConfigureWifiLabelAsync(string ssid,string label,CancellationToken token) {
        ValidateWifiLabel(ssid,label);
        var pairing=_store.Current??throw new InvalidOperationException("请先通过 USB 配对。");
        await _usbGate.WaitAsync(token);
        try {
            var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pairing.UsbIdentity)??throw new IOException("请连接已配对 TAB5 的 USB 数据线。");
            var port=GetOrOpenUsbPort(device.Port);
            if(await ReadIdentityAsync(port,token)!=pairing.DeviceId)throw new IOException("USB 设备身份不匹配。");
            Send(port,new {version=1,type="tab5_wifi_label",deviceId=pairing.DeviceId,ssid,label});
            await RequireAckAsync(port,"tab5_wifi_label_saved",token);
        }finally{_usbGate.Release();}
    }
    internal sealed record SavedWifiNetwork(string Ssid,string Label,bool Connected,bool Selected);
    internal static SavedWifiNetwork[] ParseWifiList(JsonElement root,string deviceId) {
        if(!root.TryGetProperty("deviceId",out var id)||id.GetString()!=deviceId||
           !root.TryGetProperty("version",out var version)||!version.TryGetInt32(out var v)||v!=1||
           !root.TryGetProperty("networks",out var networks)||networks.ValueKind!=JsonValueKind.Array||networks.GetArrayLength()>5)
            throw new IOException("Wi-Fi 列表响应无效。");
        var result=new List<SavedWifiNetwork>();
        foreach(var entry in networks.EnumerateArray()) {
            string ssid=entry.GetProperty("ssid").GetString()??"",label=entry.GetProperty("label").GetString()??"";
            ValidateWifiLabel(ssid,label);
            if(result.Any(n=>n.Ssid==ssid))throw new IOException("Wi-Fi 列表包含重复网络。");
            result.Add(new(ssid,label,entry.GetProperty("connected").GetBoolean(),entry.GetProperty("selected").GetBoolean()));
        }
        return result.ToArray();
    }
    internal async Task<SavedWifiNetwork[]> ReadWifiNetworksAsync(CancellationToken token) {
        var pairing=_store.Current??throw new InvalidOperationException("请先通过 USB 配对。");
        await _usbGate.WaitAsync(token);
        try {
            var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pairing.UsbIdentity)??throw new IOException("请连接 TAB5 USB。");
            var port=GetOrOpenUsbPort(device.Port);
            if(await ReadIdentityAsync(port,token)!=pairing.DeviceId)throw new IOException("USB 设备身份不匹配。");
            Send(port,new {version=1,type="tab5_wifi_list",deviceId=pairing.DeviceId});
            using var response=await ReadReplyAsync(port,"tab5_wifi_list",token);
            return ParseWifiList(response.RootElement,pairing.DeviceId);
        } catch(TimeoutException) {throw new TimeoutException("读取失败，请确认固件为 0.2.22 或更高版本。");}
        finally{_usbGate.Release();}
    }
    internal async Task ForgetWifiAsync(string ssid,CancellationToken token) {
        ValidateWifiLabel(ssid,"");
        var pairing=_store.Current??throw new InvalidOperationException("请先通过 USB 配对。");
        await _usbGate.WaitAsync(token);
        try {
            var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pairing.UsbIdentity)??throw new IOException("请连接 TAB5 USB。");
            var port=GetOrOpenUsbPort(device.Port);
            if(await ReadIdentityAsync(port,token)!=pairing.DeviceId)throw new IOException("USB 设备身份不匹配。");
            Send(port,new {version=1,type="tab5_wifi_forget",deviceId=pairing.DeviceId,ssid});
            using var response=await ReadReplyAsync(port,"tab5_wifi_forgotten",token);
            if(response.RootElement.GetProperty("deviceId").GetString()!=pairing.DeviceId)throw new IOException("USB 设备身份不匹配。");
        }finally{_usbGate.Release();}
    }


    internal Task RunUsbAsync(CancellationToken token)=>Task.WhenAll(RunUsbStatusAsync(token),RunUsbRpcAsync(token),RunUsbMetricsAsync(token));
    internal async Task RunUsbStatusAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested) {
            bool pendingResources=false;
            // Local hardware verification can reserve the serial port for a
            // standalone HID checker while Wi-Fi/BLE keep the display current.
            if(Environment.GetEnvironmentVariable("AIBOT_TAB5_USB_PAUSED")=="1") {
                _usbStatus="诊断采集中（临时暂停）";
                await _usbGate.WaitAsync(token);
                try{CloseUsbPort();}finally{_usbGate.Release();}
                await Task.Delay(2000,token);
                continue;
            }
            var pairing=_store.Current;
            if(pairing is not null) {
                bool gateHeld=false;string stage="enumerate";
                try {
                    // SetupAPI enumeration must not monopolize the serial gate
                    // while firmware fragments or interactive RPCs are flowing.
                    var candidates=await Task.Run(UsbDevices,token);
                    await _usbGate.WaitAsync(token);gateHeld=true;
                    stage="discover_identity";
                    var device=await Tab5UsbRecovery.FindAsync(pairing,candidates,async(candidate,ct)=>{
                        try{return await ReadIdentityAsync(GetOrOpenUsbPort(candidate.Port),ct);}finally{CloseUsbPort();}
                    },token);
                    if(device is null) { _usbStatus="未连接"; Volatile.Write(ref _reservedPort,null); CloseUsbPort(); }
                    else {
                        Volatile.Write(ref _reservedPort,device.Port);
                        var port=GetOrOpenUsbPort(device.Port);
                        stage="identity";
                        if(await ReadIdentityAsync(port,token)!=pairing.DeviceId) throw new IOException("USB 设备身份不匹配");
                        if(!device.Identity.Equals(pairing.UsbIdentity,StringComparison.OrdinalIgnoreCase))_store.Pair(pairing.DeviceId,device.Identity);
                        if(_host is not null) Send(port,new {version=1,type="tab5_host",host=_host,port=_port});
                        if(CurrentFrame is { } frame) {
                            stage="status_write";
                            byte[] wire=_usbPacked?PackedFullFrame(frame):frame;
                            port.WriteLine(Tab5Protocol.Prefix+Encoding.UTF8.GetString(wire));
                            stage="status_ack";
                            using var sent=JsonDocument.Parse(frame);
                            using var ack=await ReadReplyAsync(port,"tab5_ack",token);
                            if(ack.RootElement.GetProperty("deviceId").GetString()!=pairing.DeviceId ||
                                ack.RootElement.GetProperty("sequence").GetInt64()!=sent.RootElement.GetProperty("sequence").GetInt64()) throw new IOException("TAB5 数据确认不匹配。");
                            _usbPacked=ack.RootElement.TryGetProperty("usbPacked",out var packed)&&packed.TryGetInt32(out int pv)&&pv==1;
                            _usbRpcVersion=ack.RootElement.TryGetProperty("rpcVersion",out var rpcVersion)&&rpcVersion.TryGetInt32(out var rv)?rv:0;
                            _usbRpcBinary=ack.RootElement.TryGetProperty("rpcUsbBinary",out var binary)&&binary.TryGetInt32(out int bv)&&bv==1;
                            _usbRpcChunk=Tab5UsbBinary.Chunk(ack.RootElement,_usbRpcBinary);
                            _usbTelemetryVersion=ack.RootElement.TryGetProperty("telemetryVersion",out var telemetryVersion)&&telemetryVersion.TryGetInt32(out var tv)?tv:0;
                            _usbStatus="已连接 · "+device.Port;Interlocked.Exchange(ref _usbAckAt,Environment.TickCount64);
                            if(ack.RootElement.TryGetProperty("firmware",out var installedVersion)&&installedVersion.ValueKind==JsonValueKind.String)
                                ObserveFirmware(pairing.DeviceId,installedVersion.GetString()!);
                            string? reportedAssets=ack.RootElement.TryGetProperty("assetIds",out var idsValue)&&idsValue.ValueKind==JsonValueKind.String?idsValue.GetString():null;
                            _assets.Acknowledge(reportedAssets);
                            if(ack.RootElement.TryGetProperty("uptimeMs",out var uptimeValue)&&uptimeValue.TryGetInt64(out long uptime)&&
                               ack.RootElement.TryGetProperty("resetReason",out var resetValue)&&resetValue.TryGetInt32(out int reset)&&_crashes.Observe(uptime,reset)) {
                                stage="crash_trace";
                                Send(port,new{version=1,type="tab5_crash_status"});
                                using var crash=await ReadReplyAsync(port,"tab5_crash_diagnostic",token);
                                _crashes.Record(ack.RootElement.TryGetProperty("firmware",out var crashVersion)?crashVersion.GetString()??"":"",crash.RootElement);
                            }
                            var receivedIds=Tab5Assets.ValidIds(reportedAssets)?reportedAssets!.Split(','):[];
                            string Number(string field) => ack.RootElement.TryGetProperty(field,out var value)&&value.TryGetInt64(out var n)?n.ToString():"--";
                            string ages=ack.RootElement.TryGetProperty("ageMs",out var age)&&age.ValueKind==JsonValueKind.Array
                                ?string.Join(",",age.EnumerateArray().Take(3).Select(v=>v.TryGetInt64(out var n)?n.ToString():"--")):"--";
                            bool hasResource=sent.RootElement.GetProperty("data").GetProperty("resource").ValueKind==JsonValueKind.Object;
                            string wifi=ack.RootElement.TryGetProperty("wifiConnected",out var wifiValue)&&wifiValue.ValueKind==JsonValueKind.True?"已连接":"未连接";
                            _wifiReportedConnected=wifi=="已连接";Interlocked.Exchange(ref _wifiReportAt,Environment.TickCount64);
                            _deviceHealth=$"固件：{(ack.RootElement.TryGetProperty("firmware",out var fw)?fw.GetString():"--")}；确认时间：{DateTimeOffset.Now:HH:mm:ss}；帧字节：{frame.Length}；含资源：{hasResource}；USB资源分片：{_usbResourceChunks}；Wi-Fi：{wifi}；运行毫秒：{Number("uptimeMs")}；重启原因：{Number("resetReason")}；屏保：{Number("saverActive")}；键鼠唤醒：{Number("pcWakeCount")}；界面心跳年龄：{Number("uiAgeMs")}；界面数据年龄：{Number("uiDataAgeMs")}；已选任务：{Number("taskSelected")}；输入会话就绪：{Number("inputSessionReady")}；语音检查：{Number("voiceCheck")}；点击时数据年龄：{Number("voiceAgeMs")}；语音阶段：{Number("voiceStage")}；HTTP：{Number("voiceHttp")}；语音往返毫秒：{Number("voiceRequestMs")}；音频序号：{Number("voiceSeq")}；背光设置次数：{Number("backlightChanges")}；亮度：{Number("backlightPercent")}；显示欠载：{Number("lcdUnderruns")}；页面：{Number("uiPage")}；资源位图：{Number("assetsReady")}；已持久化：{Number("assetsCached")}；三通道数据年龄：{ages}；BLE连接次数：{Number("bleConnectCount")}；BLE断开原因：{Number("bleDisconnectReason")}；显示诊断：{(ack.RootElement.TryGetProperty("displayDiag",out var displayDiag)?displayDiag.GetString():"--")}；升级准备诊断：{(ack.RootElement.TryGetProperty("otaDiag",out var otaDiag)?otaDiag.GetString():"--")}；显示错误：{Number("flushErrors")}；相机诊断：{(ack.RootElement.TryGetProperty("cameraDiag",out var cameraDiag)?cameraDiag.GetString():"--")}；照片诊断：{(ack.RootElement.TryGetProperty("photoDiag",out var photoDiag)?photoDiag.GetString():"--")}；上传诊断：{(ack.RootElement.TryGetProperty("photoUploadDiag",out var uploadDiag)?uploadDiag.GetString():"--")}";
                            if(ack.RootElement.TryGetProperty("artOrientation",out var artOrientation))_deviceHealth+="\n艺术屏保方向："+artOrientation.GetString();
                            // Bound each USB burst to preserve regular status heartbeats.
                            if(!BackgroundTransferPaused&&ack.RootElement.TryGetProperty("assetsReady",out var readyValue)&&readyValue.TryGetInt32(out var ready)) {
                                int budget=16;
                                foreach(var asset in _assets.Assets) {
                                    if(receivedIds.Length==3&&receivedIds[asset.Slot]==asset.Id)continue;
                                    _usbAssets.TryGetValue(asset.Slot,out var transfer);
                                    if(transfer.Id!=asset.Id)transfer=(asset.Id,0);
                                    if(transfer.Offset>=asset.Packed.Length) {
                                        transfer=(asset.Id,0);
                                    }
                                    while(!BackgroundTransferPaused&&budget>0&&transfer.Offset<asset.Packed.Length) {
                                        stage="resource";
                                        Send(port,new {version=1,type="tab5_resource",deviceId=pairing.DeviceId,resource=Tab5Assets.Chunk(asset,transfer.Offset)});
                                        using var resourceAck=await ReadReplyAsync(port,"tab5_resource_ack",token);
                                        if(resourceAck.RootElement.GetProperty("deviceId").GetString()!=pairing.DeviceId)throw new IOException("TAB5 资源确认不匹配");
                                        if(resourceAck.RootElement.TryGetProperty("assetIds",out var received)&&received.ValueKind==JsonValueKind.String)_assets.Acknowledge(received.GetString());
                                        transfer.Offset+=Math.Min(1024,asset.Packed.Length-transfer.Offset);budget--;
                                        _usbResourceChunks++;
                                        _usbAssets[asset.Slot]=transfer;
                                    }
                                    if(budget==0)break;
                                }
                                pendingResources=!BackgroundTransferPaused&&_assets.HasPending;
                            }
                        }
                    }
                } catch(Exception ex) when(Tab5UsbRecovery.IsDisconnect(ex,token)) { RecordUsbFailure(stage,ex);if(gateHeld)CloseUsbPort();_usbStatus="等待连接（"+ex.GetType().Name+"）"; }
                finally { if(gateHeld)_usbGate.Release(); }
            }
            await Task.Delay(pendingResources?40:2000,token);
        }
    }
    internal Task RunBleAsync(CancellationToken token) => new Tab5BleClient(_store,()=>Volatile.Read(ref _otaTransfers)>0?null:CurrentFrame,s=>{_bleStatus=s;if(s=="已连接 · 数据已确认")Interlocked.Exchange(ref _bleAckAt,Environment.TickCount64);},_assets.Acknowledge,s=>{_bleDiagnostic=s;if(s.StartsWith("FAILED "))_bleLastFailure=DateTimeOffset.Now.ToString("HH:mm:ss")+" "+s;},
        (nonce,proof,packet,ct)=>VoiceAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct,compact:true),
        (nonce,proof,packet,ct)=>RpcAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct,transport:"BLE"),
        (capability,interactive)=>Volatile.Read(ref _otaTransfers)>0?null:TelemetryFrame(2,capability>0,capability>=2,interactive,capability>=3),
        s=>_bleRpcDiagnostic=DateTimeOffset.Now.ToString("HH:mm:ss")+" "+s,()=>ResetTelemetryChannel(2),()=>ImageUploadActive,s=>_bleVoiceDiagnostic=DateTimeOffset.Now.ToString("HH:mm:ss")+" "+s,seq=>AcknowledgeTelemetry(2,seq),version=>ObserveFirmware(_store.Current?.DeviceId??"",version),probeTrace:_bleProbeTrace,nativeLimit:()=>_bleQueueComparison.NativeLimit,bulkWriteLimit:()=>_bleQueueComparison.WriteWindow,bulkWriteSupported:n=>Volatile.Write(ref _bleWriteWindowSupported,n)).RunAsync(token);
    private static SerialPort Open(string name) {
        var port=new SerialPort(name,460800) {NewLine="\n",ReadTimeout=500,WriteTimeout=2500,DtrEnable=true,RtsEnable=false,Encoding=Encoding.UTF8};
        try {port.Open();return port;} catch {DisposeUsbPort(port);throw;}
    }
    private static void Send(SerialPort port,object frame)=>port.WriteLine(Tab5Protocol.Prefix+JsonSerializer.Serialize(frame,JsonDefaults.Options));
    private static async Task<string> ReadIdentityAsync(SerialPort port,CancellationToken token) {
        port.DiscardInBuffer(); Send(port,new {version=1,type="tab5_ping"});
        using var reply=await ReadReplyAsync(port,"tab5_hello",token);
        var root=reply.RootElement;
        var id=root.TryGetProperty("deviceId",out var d)&&d.ValueKind==JsonValueKind.String?d.GetString():null;
        if(!Tab5Protocol.ValidId(id) || !root.TryGetProperty("version",out var v)||!v.TryGetInt32(out var version)||version!=1) throw new IOException("不兼容的 TAB5 固件。");
        return id!;
    }
    private static async Task RequireAckAsync(SerialPort port,string type,CancellationToken token) { using var reply=await ReadReplyAsync(port,type,token); }
    private static Task<JsonDocument> ReadReplyAsync(SerialPort port,string type,CancellationToken token)=>Tab5UsbText.ReadAsync(port,type,token);
    internal async Task<(int Status,byte[]? Packet)> VoiceAsync(string id,string nonce,string proof,byte[] packet,CancellationToken token,bool compact=false)
    {
        lock(_lifecycle){if(_stopping)return(503,null);Interlocked.Increment(ref _voiceRequests);}
        try {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        var pair=_store.Current;
        if(pair is null||pair.DeviceId!=id){_voiceAuthStatus="设备配对不匹配";return(401,null);}
        if(!Tab5Protocol.ValidNonce(nonce)||packet.Length is <28 or >16384){_voiceAuthStatus="请求格式无效";return(401,null);}
        var key=Convert.FromBase64String(pair.Key);
        var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packet)).ToLowerInvariant();
        if(!Tab5Protocol.Verify(key,$"POST|/tab5/v1/voice|{id}|{nonce}|{digest}",proof)){_voiceAuthStatus="签名不匹配";return(401,null);}
        byte[] clear;
        try{clear=Tab5Protocol.Decrypt(key,nonce,packet);}catch(System.Security.Cryptography.CryptographicException){_voiceAuthStatus="解密失败";return(401,null);}
        _voiceAuthStatus="已通过";
        object result;
        try {
            using var doc=JsonDocument.Parse(clear);var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("session",out var session)||session.ValueKind!=JsonValueKind.String||session.GetString()!=_session||
                !root.TryGetProperty("issuedAt",out var at)||!at.TryGetInt64(out var issued)||Math.Abs((double)issued-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())>15000)
                throw new ArgumentException("电脑状态已变化，请等待重新连接");
            lock(_voiceNonces) {
                long now=Environment.TickCount64;
                foreach(var expired in _voiceNonces.Where(p=>now-p.Value>35000).Select(p=>p.Key).ToArray())_voiceNonces.Remove(expired);
                if(_voiceNonces.ContainsKey(nonce))throw new ArgumentException("重复语音请求已拒绝");
                if(_voiceNonces.Count>=1024)throw new ArgumentException("语音请求过快");
                _voiceNonces[nonce]=now;
            }
            result=_voice is null?throw new InvalidOperationException("电脑语音服务未就绪"):await _voice.HandleAsync(root,token);
            var diagPath=Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG");
            if(diagPath is not null) {
                string op=root.TryGetProperty("op",out var opValue)&&opValue.ValueKind==JsonValueKind.String?opValue.GetString()??"?":"?";
                int seq=root.TryGetProperty("seq",out var seqValue)&&seqValue.TryGetInt32(out var number)?number:-1;
                double elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if(op!="audio"||seq%10==0||elapsed>180) {
                    var voiceReply=result as Tab5VoiceReply;
                    try{File.AppendAllText(diagPath,$"{DateTimeOffset.Now:O} HOST op={op} seq={seq} ms={elapsed:F0} state={voiceReply?.State} textBytes={Encoding.UTF8.GetByteCount(voiceReply?.Text??"")}{Environment.NewLine}");}
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }catch(Exception ex) when(ex is ArgumentException or JsonException or InvalidOperationException or NAudio.MmException or System.Runtime.InteropServices.COMException) {
            result=new {state="error",message=ex is JsonException?"语音请求格式错误":ex is ArgumentException or InvalidOperationException?ex.Message:"音频设备不可用，请检查电脑",text=""};
        }
        // BLE audio acknowledgements must not resend an ever-growing transcript
        // five times/second. Full draft remains available once recognition starts.
        if(compact&&result is Tab5VoiceReply {State:"recording" or "draining"} current)result=current with {Text=""};
        return(200,Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(result,JsonDefaults.Options)));
        }finally{Interlocked.Decrement(ref _voiceRequests);}
    }
    public void Dispose() {
        lock(_lifecycle){_stopping=true;_lifetime.Cancel();}
        _voice?.Dispose();_codexTasks.Dispose();
        if(_usbGate.Wait(1500))try{CloseUsbPort();}finally{_usbGate.Release();}
    }
}

internal sealed class Tab5IdentityConflictException(string candidateId) : InvalidOperationException("此设备与保留的 TAB5 配对资料不同，尚未替换配对。") {internal string CandidateId {get;}=candidateId;}
