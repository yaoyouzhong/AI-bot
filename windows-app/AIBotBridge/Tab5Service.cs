using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// One collector (BridgeRuntime), independent device sessions. Never writes old display settings.
internal sealed class Tab5Service : IDisposable
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
    private volatile string _hidDiagnostic="未触发";
    private readonly string _session=Guid.NewGuid().ToString("N");
    private byte[]? _frame;
    private long _sequence;
    private readonly Tab5Assets _assets=new();
    private readonly Tab5CodexTasks _codexTasks;
    private Tab5OtaPackage? _ota;
    private readonly Tab5FirmwareObservation _firmware=new();
    private int _otaTransfers;
    private volatile string _otaTransferDiagnostic="尚无升级传输";
    internal async Task TransferOtaAsync(Stream stream,byte[] image,string id,string capability,CancellationToken token) {
        bool fast=Tab5OtaFlow.Fast(capability);long started=Environment.TickCount64;
        OtaTransferActive(true);
        try {
            await Tab5OtaFlow.WriteAsync(stream,image,fast,token,sent=>
                _otaTransferDiagnostic=$"{(fast?"TCP流控":"旧版兼容")}；已发送：{sent}/{image.Length}；耗时毫秒：{Environment.TickCount64-started}；等待设备校验");
        }catch { _otaTransferDiagnostic=$"传输中断；耗时毫秒：{Environment.TickCount64-started}";throw; }
        finally {OtaTransferActive(false);}
    }
    internal void OtaTransferActive(bool active) {
        lock(_lifecycle){if(active){if(_stopping)throw new OperationCanceledException("TAB5 服务已停止。");Interlocked.Increment(ref _otaTransfers);}else Interlocked.Decrement(ref _otaTransfers);}
    }
    internal string OtaSummary=>_firmware.Summary(_store.Current?.DeviceId,Volatile.Read(ref _ota)?.Version,Volatile.Read(ref _otaTransfers)>0);
    internal string OtaNotes=>Volatile.Read(ref _ota)?.Notes??"";
    internal bool HasOta=>Volatile.Read(ref _ota) is not null;
    internal void OfferOta(string path) {var image=Tab5OtaPackage.Load(path);Volatile.Write(ref _ota,image);}
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
        if(pairing is null||string.IsNullOrEmpty(name)||!_usbGate.Wait(500))return false;
        try {
            {
                var port=GetOrOpenUsbPort(name);
                port.ReadTimeout=400;
                port.DiscardInBuffer();
                Send(port,new {version=1,type="tab5_ping"});
                if(!VoiceHidReply(port,"tab5_hello",pairing.DeviceId))return false;
                Send(port,new {version=1,type="tab5_hid_toggle",shortcut});
                if(!VoiceHidReply(port,"tab5_hid_probe_start",pairing.DeviceId))return false;
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
    private volatile string _deviceHealth="设备运行：等待诊断";
    private long _wifiRequestAt,_wifiReportAt,_usbAckAt,_bleAckAt;
    private int _installBusy;
    internal bool Busy=>Volatile.Read(ref _voiceRequests)>0||Volatile.Read(ref _otaTransfers)>0||Volatile.Read(ref _installBusy)>0||(_voice as Tab5VoiceHost)?.Busy==true;
    internal string? PairedId=>_store.Current?.DeviceId;
    internal DeviceView DeviceView { get {long now=Environment.TickCount64;bool Recent(long at)=>at>0&&now-at<15000;bool usb=Recent(Interlocked.Read(ref _usbAckAt)),wifi=Recent(Interlocked.Read(ref _wifiRequestAt)),ble=Recent(Interlocked.Read(ref _bleAckAt));return new(usb||wifi||ble,Busy?"正在处理":usb||wifi||ble?"在线":"离线",$"USB：{(usb?"已连接":"未连接")}  Wi-Fi：{(wifi?"已连接":"未连接")}  蓝牙：{(ble?"已连接":"未连接")}\n当前通道：{(usb?"USB":wifi?"Wi-Fi":ble?"蓝牙":"无")}",_firmware.LastVersion(PairedId));}}
    private volatile bool _wifiReportedConnected;
    private volatile string _voiceAuthStatus="尚无语音请求";
    internal string DiagnosticSummary => Summary+"\n"+_deviceHealth+"\n语音鉴权："+_voiceAuthStatus+"；键盘诊断："+_hidDiagnostic+"\nCodex 发送："+_codexTasks.SubmitDiagnostic+"\n蓝牙传输："+_bleDiagnostic+"\n固件传输："+_otaTransferDiagnostic;
    private string? _reservedPort;
    // All callers hold _usbGate. Keeping DTR and the CDC handle stable avoids
    // a device open/close cycle every two seconds during audio capture.
    private SerialPort GetOrOpenUsbPort(string name) {
        if(_usbPort is { IsOpen:true } existing && string.Equals(existing.PortName,name,StringComparison.OrdinalIgnoreCase))return existing;
        CloseUsbPort();
        return _usbPort=Open(name);
    }
    private void CloseUsbPort() {var port=_usbPort;_usbPort=null;port?.Dispose();}
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
    private object ConnectionHealth => new { voiceEnabled=(_voice as Tab5VoiceHost)?.SettingsEnabled };
    internal void Publish(StatusSnapshot snapshot)
    {
        var pairing=_store.Current; if(pairing is null) return;
        var resource=_assets.Next(snapshot);var sequence=Interlocked.Increment(ref _sequence);
        var frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,resource,_assets.Ids,_codexTasks.Snapshot(),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        if(frame.Length > Tab5Protocol.MaximumFrame-28 && resource is not null)
            frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,null,_assets.Ids,_codexTasks.Snapshot(),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        for(int limit=64;frame.Length>Tab5Protocol.MaximumFrame-28&&limit>=24;limit-=8)
            frame=Tab5Protocol.Snapshot(snapshot,pairing.DeviceId,_session,sequence,null,_assets.Ids,_codexTasks.SnapshotLimited(limit),Volatile.Read(ref _ota)?.Offer,ConnectionHealth);
        if(frame.Length > Tab5Protocol.MaximumFrame-28) { _usbStatus="状态数据超出协议上限"; return; }
        Volatile.Write(ref _frame,frame); Interlocked.Exchange(ref _publishedAt,Environment.TickCount64);
    }
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
        if(firmware is not null&&firmware.Length<=31&&Tab5Protocol.Verify(key,"FIRMWARE|"+nonce+"|"+firmware,firmwareProof))_firmware.Observe(id,firmware);
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
    internal async Task<(int Status, object Body)> SubmitCodexAsync(string id,string nonce,string proof,byte[] packet,CancellationToken token,bool readOnly=false,bool imageOnly=false)
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
        try {doc=JsonDocument.Parse(clear);} catch(JsonException) {return (400,new {error="invalid_request"});}
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
                byte[] data=JsonSerializer.SerializeToUtf8Bytes(result.Body,JsonDefaults.Options);
                if(data.Length>16000) {
                    var compact=System.Text.Json.Nodes.JsonNode.Parse(data)!.AsObject();compact.Remove("neighbors");
                    data=JsonSerializer.SerializeToUtf8Bytes(compact,JsonDefaults.Options);
                }
                return(200,new {status="loaded",encrypted=Convert.ToBase64String(Tab5Protocol.Encrypt(key,nonce,data))});
            }
            return result;
        }
        return await _codexTasks.SubmitAsync(taskId.GetString()!,message.GetString()!,token,requestId.Length>0?requestId:null,operation,expectedTurn,images,id);
        }
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


    internal async Task RunUsbAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested) {
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
                await _usbGate.WaitAsync(token);
                try {
                    var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pairing.UsbIdentity);
                    if(device is null) { _usbStatus="未连接"; Volatile.Write(ref _reservedPort,null); CloseUsbPort(); }
                    else {
                        Volatile.Write(ref _reservedPort,device.Port);
                        var port=GetOrOpenUsbPort(device.Port);
                        if(await ReadIdentityAsync(port,token)!=pairing.DeviceId) throw new IOException("USB 设备身份不匹配");
                        if(_host is not null) Send(port,new {version=1,type="tab5_host",host=_host,port=_port});
                        if(CurrentFrame is { } frame) {
                            port.WriteLine(Tab5Protocol.Prefix+Encoding.UTF8.GetString(frame));
                            using var sent=JsonDocument.Parse(frame);
                            using var ack=await ReadReplyAsync(port,"tab5_ack",token);
                            if(ack.RootElement.GetProperty("deviceId").GetString()!=pairing.DeviceId ||
                                ack.RootElement.GetProperty("sequence").GetInt64()!=sent.RootElement.GetProperty("sequence").GetInt64()) throw new IOException("TAB5 数据确认不匹配。");
                            _usbStatus="已连接 · "+device.Port;Interlocked.Exchange(ref _usbAckAt,Environment.TickCount64);
                            if(ack.RootElement.TryGetProperty("firmware",out var installedVersion)&&installedVersion.ValueKind==JsonValueKind.String)
                                _firmware.Observe(pairing.DeviceId,installedVersion.GetString()!);
                            string? reportedAssets=ack.RootElement.TryGetProperty("assetIds",out var idsValue)&&idsValue.ValueKind==JsonValueKind.String?idsValue.GetString():null;
                            _assets.Acknowledge(reportedAssets);
                            var receivedIds=Tab5Assets.ValidIds(reportedAssets)?reportedAssets!.Split(','):[];
                            string Number(string field) => ack.RootElement.TryGetProperty(field,out var value)&&value.TryGetInt64(out var n)?n.ToString():"--";
                            string ages=ack.RootElement.TryGetProperty("ageMs",out var age)&&age.ValueKind==JsonValueKind.Array
                                ?string.Join(",",age.EnumerateArray().Take(3).Select(v=>v.TryGetInt64(out var n)?n.ToString():"--")):"--";
                            bool hasResource=sent.RootElement.GetProperty("data").GetProperty("resource").ValueKind==JsonValueKind.Object;
                            string wifi=ack.RootElement.TryGetProperty("wifiConnected",out var wifiValue)&&wifiValue.ValueKind==JsonValueKind.True?"已连接":"未连接";
                            _wifiReportedConnected=wifi=="已连接";Interlocked.Exchange(ref _wifiReportAt,Environment.TickCount64);
                            _deviceHealth=$"固件：{(ack.RootElement.TryGetProperty("firmware",out var fw)?fw.GetString():"--")}；确认时间：{DateTimeOffset.Now:HH:mm:ss}；帧字节：{frame.Length}；含资源：{hasResource}；USB资源分片：{_usbResourceChunks}；Wi-Fi：{wifi}；运行毫秒：{Number("uptimeMs")}；重启原因：{Number("resetReason")}；屏保：{Number("saverActive")}；键鼠唤醒：{Number("pcWakeCount")}；界面心跳年龄：{Number("uiAgeMs")}；界面数据年龄：{Number("uiDataAgeMs")}；已选任务：{Number("taskSelected")}；输入会话就绪：{Number("inputSessionReady")}；语音检查：{Number("voiceCheck")}；点击时数据年龄：{Number("voiceAgeMs")}；语音阶段：{Number("voiceStage")}；HTTP：{Number("voiceHttp")}；语音往返毫秒：{Number("voiceRequestMs")}；音频序号：{Number("voiceSeq")}；背光设置次数：{Number("backlightChanges")}；亮度：{Number("backlightPercent")}；显示欠载：{Number("lcdUnderruns")}；页面：{Number("uiPage")}；资源位图：{Number("assetsReady")}；已持久化：{Number("assetsCached")}；三通道数据年龄：{ages}；BLE连接次数：{Number("bleConnectCount")}；BLE断开原因：{Number("bleDisconnectReason")}；显示诊断：{(ack.RootElement.TryGetProperty("displayDiag",out var displayDiag)?displayDiag.GetString():"--")}；显示错误：{Number("flushErrors")}";
                            // Bound each USB burst to preserve regular status heartbeats.
                            if(ack.RootElement.TryGetProperty("assetsReady",out var readyValue)&&readyValue.TryGetInt32(out var ready)) {
                                int budget=16;
                                foreach(var asset in _assets.Assets) {
                                    if(receivedIds.Length==3&&receivedIds[asset.Slot]==asset.Id)continue;
                                    _usbAssets.TryGetValue(asset.Slot,out var transfer);
                                    if(transfer.Id!=asset.Id)transfer=(asset.Id,0);
                                    if(transfer.Offset>=asset.Packed.Length) {
                                        if((ready&(1<<asset.Slot))!=0)continue;
                                        transfer=(asset.Id,0);
                                    }
                                    while(budget>0&&transfer.Offset<asset.Packed.Length) {
                                        Send(port,new {version=1,type="tab5_resource",deviceId=pairing.DeviceId,resource=Tab5Assets.Chunk(asset,transfer.Offset)});
                                        using var resourceAck=await ReadReplyAsync(port,"tab5_resource_ack",token);
                                        if(resourceAck.RootElement.GetProperty("deviceId").GetString()!=pairing.DeviceId)throw new IOException("TAB5 资源确认不匹配");
                                        transfer.Offset+=Math.Min(1024,asset.Packed.Length-transfer.Offset);budget--;
                                        _usbResourceChunks++;
                                        _usbAssets[asset.Slot]=transfer;
                                    }
                                    if(budget==0)break;
                                }
                            }
                        }
                    }
                } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception or JsonException or KeyNotFoundException) { CloseUsbPort();_usbStatus="等待连接（"+ex.GetType().Name+"）"; }
                finally { _usbGate.Release(); }
            }
            await Task.Delay(2000,token);
        }
    }
    internal Task RunBleAsync(CancellationToken token) => new Tab5BleClient(_store,()=>Volatile.Read(ref _otaTransfers)>0?null:CurrentFrame,s=>{_bleStatus=s;if(s=="已连接 · 数据已确认")Interlocked.Exchange(ref _bleAckAt,Environment.TickCount64);},_assets.Acknowledge,s=>_bleDiagnostic=s,
        (nonce,proof,packet,ct)=>VoiceAsync(_store.Current?.DeviceId??"",nonce,proof,packet,ct,compact:true)).RunAsync(token);
    private static SerialPort Open(string name) {
        var port=new SerialPort(name,460800) {NewLine="\n",ReadTimeout=500,WriteTimeout=2500,DtrEnable=true,RtsEnable=false,Encoding=Encoding.UTF8};
        try {port.Open();return port;} catch {port.Dispose();throw;}
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
    private static async Task<JsonDocument> ReadReplyAsync(SerialPort port,string type,CancellationToken token)
    {
        var until=Environment.TickCount64+4000;
        var line=new StringBuilder();
        while(Environment.TickCount64<until) {
            token.ThrowIfCancellationRequested();
            // ReadExisting is nonblocking; ReadChar may wait again after checking BytesToRead.
            var available=port.ReadExisting();
            foreach(char ch in available) {
                if(ch=='\n') {
                    var value=line.ToString().Trim();line.Clear();
                    if(!value.StartsWith(Tab5Protocol.Prefix,StringComparison.Ordinal))continue;
                    try {
                        var doc=JsonDocument.Parse(value[Tab5Protocol.Prefix.Length..]);
                        if(doc.RootElement.TryGetProperty("type",out var t)) {
                            if(t.GetString()==type)return doc;
                            if(type=="tab5_wifi_forgotten"&&t.GetString()=="tab5_wifi_rejected"){doc.Dispose();throw new InvalidOperationException("删除未完成：设备忙、网络已不存在或保存失败。");}
                        }
                        doc.Dispose();
                    } catch(JsonException) { }
                } else if(line.Length<4096)line.Append(ch);
                else throw new IOException("TAB5 响应过长。");
            }
            await Task.Delay(10,token);
        }
        throw new TimeoutException("未收到 TAB5 确认："+type);
    }
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
