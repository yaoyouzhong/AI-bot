using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private volatile string _upgradeDiagnostic="尚未核验";
    private sealed record VerifiedBoot(string DeviceId,string Version,string ElfSha256,string Partition);
    private volatile VerifiedBoot? _verifiedBoot;
    internal bool CanVerifyUpgrade=>HasOta&&!Busy&&DeviceView.Online&&(DeviceView.Usb=="已连接"||SupportsWirelessBoot(DeviceView.Firmware))&&DeviceView.Firmware==Volatile.Read(ref _ota)?.Version;
    internal bool AutoBootPending {get{lock(_lifecycle)return _otaDelivery is {} delivery&&AutoBootCurrent(delivery)&&
        !delivery.VerificationFinished&&!BootMatches(delivery.Image,delivery.Device);}}
    private bool BootMatches(Tab5OtaPackage image,string? deviceId)=>_verifiedBoot is {} verified&&verified.DeviceId==deviceId&&
        verified.Version==image.Version&&verified.ElfSha256==image.ElfSha256;
    private bool AutoBootCurrent(OtaDelivery delivery)=>!_stopping&&delivery is {FinalReleased:true,StopRequested:false}&&
        ReferenceEquals(_otaDelivery,delivery)&&ReferenceEquals(_ota,delivery.Image)&&_store.Current is {} pair&&
        pair.DeviceId==delivery.Device&&Tab5HostBinding.Id(pair)==delivery.Host;
    private bool CanProbeWirelessUpgrade=>!BusyExceptOtaLease&&DeviceView.Usb!="已连接"&&
        SupportsWirelessBoot(DeviceView.Firmware)&&WirelessBootTransport.Length>0;
    internal string UpgradeBootSummary {
        get {
            lock(_lifecycle) {
                var image=Volatile.Read(ref _ota);var view=DeviceView;
                if(image is null)return "选择固件后可核验启动";
                if(view.Online&&view.Firmware==image.Version&&BootMatches(image,PairedId))return "启动核验通过";
                if(_otaDelivery is {} delivery&&AutoBootCurrent(delivery)) {
                    if(delivery.Verifying)return "正在自动核验启动…";
                    if(delivery.VerificationFailure.Length>0)return "自动核验未通过";
                    if(delivery.BootConfirmationPending)return "启动核验 · 等待设备自检完成";
                    if(!delivery.VerificationFinished)return "启动核验 · 等待设备重启并连接";
                }
                if(!view.Online)return "启动核验 · 等待设备连接";
                if(view.Firmware!=image.Version)return "启动核验 · 升级完成后可核验";
                return view.Usb=="已连接"?"启动核验 · 尚未核验":SupportsWirelessBoot(view.Firmware)?"启动核验 · 可通过无线核验":"启动核验 · 此固件需连接 USB";
            }
        }
    }
    internal void RecordVerifiedBoot(JsonElement root,Tab5OtaPackage image,string deviceId) {
        VerifyUpgradeDiagnostic(root,image);
        _verifiedBoot=new(deviceId,image.Version,image.ElfSha256,root.GetProperty("partition").GetString()!);
    }
    private sealed class AwaitingOtaBootException():IOException("设备尚未报告本次升级后的启动。") { }
    private sealed class PendingBootConfirmationException():IOException("设备正在完成启动自检。") { }
    private sealed class BootImageMismatchException():IOException("运行镜像、分区或启动确认状态与所选固件不符，不能确认升级成功。") { }

    private static void RequirePostTransferBoot(OtaDelivery? delivery,long requestAt,long uptime) {
        if(delivery is not null&&uptime>=requestAt-delivery.FinalAt)throw new AwaitingOtaBootException();
    }

    private async Task AutoVerifyUpgradeAsync(OtaDelivery delivery,CancellationToken lifetime) {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        deadline.CancelAfter(TimeSpan.FromMinutes(2.5));
        string lastReadError="";
        try {
            while(true) {
                await Task.Delay(TimeSpan.FromSeconds(delivery.BootConfirmationPending?4:2),deadline.Token);
                lock(_lifecycle) {
                    if(!AutoBootCurrent(delivery)||BootMatches(delivery.Image,delivery.Device))return;
                    var view=DeviceView;
                    if(!view.Online||view.Firmware!=delivery.Image.Version||!CanVerifyUpgrade&&!CanProbeWirelessUpgrade)continue;
                    delivery.Verifying=true;
                }
                try {
                    await VerifyUpgradeAsync(deadline.Token,delivery);
                    lock(_lifecycle)if(AutoBootCurrent(delivery))Interlocked.Exchange(ref _rpcOtaUntil,0);
                    return;
                }catch(AwaitingOtaBootException) {
                    lock(_lifecycle)if(AutoBootCurrent(delivery))_upgradeDiagnostic="等待本次升级后重启";
                }
                catch(PendingBootConfirmationException) {
                    lock(_lifecycle)if(AutoBootCurrent(delivery)) {
                        delivery.BootConfirmationPending=true;
                        _upgradeDiagnostic="等待设备完成启动自检";
                    }
                }
                catch(BootImageMismatchException ex) {
                    lock(_lifecycle)if(AutoBootCurrent(delivery))delivery.VerificationFailure=ex.Message;
                    return;
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or JsonException) {
                    lastReadError=ex.GetType().Name;
                }finally{lock(_lifecycle)delivery.Verifying=false;}
            }
        }catch(OperationCanceledException) when(deadline.IsCancellationRequested) {
            lock(_lifecycle)if(!lifetime.IsCancellationRequested&&AutoBootCurrent(delivery)) {
                delivery.VerificationFailure=delivery.BootConfirmationPending?"等待设备完成启动自检超时。":"未在等待时间内读取到有效启动核验结果。";
                _upgradeDiagnostic="failed; automatic_boot_timeout"+(lastReadError.Length>0?"; lastRead="+lastReadError:"");
            }
        }finally{lock(_lifecycle)delivery.VerificationFinished=true;}
    }

    internal Task VerifyUpgradeAsync(CancellationToken token)=>VerifyUpgradeAsync(token,null);
    private async Task VerifyUpgradeAsync(CancellationToken token,OtaDelivery? automatic) {
        var image=automatic?.Image??Volatile.Read(ref _ota)??throw new InvalidOperationException("请先选择本次升级的固件，用于核对版本和指纹。");
        var pair=_store.Current??throw new IOException("TAB5 尚未配对。");
        var device=automatic is not null&&CanProbeWirelessUpgrade?null:UsbDevices().FirstOrDefault(d=>d.Identity==pair.UsbIdentity);
        if(device is null){await VerifyWirelessBootAsync(image,pair.DeviceId,token,automatic);return;}
        OtaDelivery? completedDelivery;
        lock(_lifecycle){
            if(_stopping||Busy||!ReferenceEquals(_ota,image)||_store.Current!=pair||automatic is not null&&!AutoBootCurrent(automatic))
                throw new InvalidOperationException("设备仍在处理操作或升级目标已变化，请稍后核验。");
            completedDelivery=_otaDelivery is {FinalReleased:true} delivery?delivery:null;
            _installBusy++;
        }
        _verifiedBoot=null;_upgradeDiagnostic="正在核验";string observed="";
        try {
            await _usbGate.WaitAsync(token);
            try {
                CloseUsbPort();using var port=Open(device.Port);long previous=-1;
                for(int i=0;i<3;i++) {
                    port.DiscardInBuffer();long requestAt=Environment.TickCount64;Send(port,new{version=1,type="tab5_ping"});
                    using var hello=await ReadReplyAsync(port,"tab5_hello",token);
                    previous=Tab5InstallBootCheck.Hello(hello.RootElement,pair.DeviceId,image.Version,previous);
                    RequirePostTransferBoot(completedDelivery,requestAt,previous);
                    if(i<2)await Task.Delay(1200,token);
                }
                Send(port,new{version=1,type="tab5_ota_status"});using var reply=await ReadReplyAsync(port,"tab5_ota_diagnostic",token);
                var root=reply.RootElement;
                string ReadText(string key)=>root.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()??"":"";
                int ReadNumber(string key)=>root.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.Number&&x.TryGetInt32(out int n)?n:-1;
                observed=$"versionMatch={ReadText("firmware")==image.Version}; elfMatch={ReadText("elfSha256")==image.ElfSha256}; partition={ReadText("partition")}; address={ReadNumber("address")}; state={ReadNumber("state")}; stateError={ReadNumber("stateError")}";
                lock(_lifecycle) {
                    if(_store.Current!=pair||!ReferenceEquals(_ota,image))throw new IOException("设备或所选固件已变化，请重新核验。");
                    RecordVerifiedBoot(root,image,pair.DeviceId);
                }
                string verified=$"verified; firmware={image.Version}; elfSha256={image.ElfSha256}; partition={reply.RootElement.GetProperty("partition").GetString()}; state=VALID";
                _upgradeDiagnostic=verified+"; timing=unavailable";
                // Retained numeric timing survives the OTA reboot. Collect it
                // only after identity, firmware, partition and hash checks pass.
                using var timingStop=CancellationTokenSource.CreateLinkedTokenSource(token);timingStop.CancelAfter(3000);
                try {
                    Send(port,new{version=1,type="tab5_crash_status"});
                    using var trace=await ReadReplyAsync(port,"tab5_crash_diagnostic",timingStop.Token);
                    _upgradeDiagnostic=verified+"; "+FormatUpgradeTiming(trace.RootElement);
                }catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException or JsonException) {
                    if(token.IsCancellationRequested)throw;
                    _upgradeDiagnostic+=$"; timingRead={ex.GetType().Name}";
                }
            }finally{CloseUsbPort();_usbGate.Release();}
        }catch(Exception ex){if(_verifiedBoot is null)_upgradeDiagnostic="failed; "+ex.GetType().Name+(observed.Length>0?"; "+observed:"");throw;}
        finally{lock(_lifecycle)_installBusy--;}
    }
    internal static string FormatUpgradeTiming(JsonElement root) {
        if(!root.TryGetProperty("trace",out var trace)||trace.ValueKind!=JsonValueKind.Object)return "timing=unavailable";
        var fields=new List<string>();
        foreach(string key in new[]{"previousStage","previousOffset","previousTotal","previousError","previousStackFree"})
            if(trace.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetUInt32(out uint number))fields.Add($"{key}={number}");
        foreach(string key in new[]{"otaMs","previousOtaMs"}) {
            if(!trace.TryGetProperty(key,out var values)||values.ValueKind!=JsonValueKind.Array||values.GetArrayLength()!=3)continue;
            var numbers=values.EnumerateArray().ToArray();
            if(numbers.All(x=>x.ValueKind==JsonValueKind.Number&&x.TryGetUInt32(out _)))fields.Add(key+"=["+string.Join(',',numbers.Select(x=>x.GetUInt32()))+"]");
        }
        foreach(string key in new[]{"otaPhases","previousOtaPhases"}) {
            if(!trace.TryGetProperty(key,out var phases)||phases.ValueKind!=JsonValueKind.Object||
               !phases.TryGetProperty("transport",out var link)||link.ValueKind!=JsonValueKind.Number||!link.TryGetInt32(out int transport)||transport is < -1 or > 2)continue;
            var phaseFields=new List<string>{$"transport={transport}"};
            foreach(string name in new[]{"prepareMs","writeMs","freeWaitMs","readyWaitMs","verifyMs","installMs"})
                if(phases.TryGetProperty(name,out var number)&&number.ValueKind==JsonValueKind.Number&&number.TryGetUInt32(out uint ms))phaseFields.Add($"{name}={ms}");
            if(phaseFields.Count==7)fields.Add(key+"={"+string.Join(',',phaseFields)+"}");
        }
        string flash=FormatFlashBenchmark(root);if(flash.Length>0)fields.Add(flash);
        return fields.Count==0?"timing=unavailable":string.Join("; ",fields);
    }
    internal static string FormatFlashBenchmark(JsonElement root) {
        if(!root.TryGetProperty("flashBenchmark",out var result)||result.ValueKind!=JsonValueKind.Object)return "";
        var values=new List<string>();
        foreach(string key in new[]{"state","sampleBytes","partitionAddress","offset","jedecId","pageBytes","restored","modified"}) {
            if(!result.TryGetProperty(key,out var field)||field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value))return "";
            if((key=="state"&&value>4)||((key=="restored"||key=="modified")&&value>1))return "";
            values.Add($"{key}={value}");
        }
        if(!result.TryGetProperty("error",out var error)||error.ValueKind!=JsonValueKind.Number||!error.TryGetInt32(out int code))return "";
        values.Add($"error={code}");
        foreach(string key in new[]{"phase","scanned"}) {
            if(!result.TryGetProperty(key,out var field))continue;
            if(field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value)||(key=="phase"&&value>3))return "";
            values.Add($"{key}={value}");
        }
        if(!result.TryGetProperty("rows",out var rows)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()!=4)return "";
        uint[] chunks=[8192,16384,49152,65536];int i=0;
        foreach(var row in rows.EnumerateArray()) {
            if(row.ValueKind!=JsonValueKind.Object)return "";
            var data=new List<string>();
            foreach(string key in new[]{"chunkBytes","completed","skipped"}) {
                if(!row.TryGetProperty(key,out var field)||field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value)||
                    key=="chunkBytes"&&value!=chunks[i]||key=="completed"&&value>3||key=="skipped"&&value>1)return "";
                data.Add($"{key}={value}");
            }
            foreach(string key in new[]{"eraseUs","writeUs"}) {
                if(!row.TryGetProperty(key,out var array)||array.ValueKind!=JsonValueKind.Array||array.GetArrayLength()!=3)return "";
                var numbers=array.EnumerateArray().ToArray();
                if(numbers.Any(x=>x.ValueKind!=JsonValueKind.Number||!x.TryGetUInt32(out _)))return "";
                data.Add(key+"=["+string.Join(',',numbers.Select(x=>x.GetUInt32()))+"]");
            }
            values.Add($"row{i}={{"+string.Join(',',data)+"}");i++;
        }
        return "flashBenchmark={"+string.Join(',',values)+"}";
    }
    internal static void VerifyUpgradeDiagnostic(JsonElement root,Tab5OtaPackage image) {
        string Text(string key)=>root.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()!:"";
        int Number(string key)=>root.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.Number&&x.TryGetInt32(out int n)?n:-1;
        string partition=Text("partition");int address=Number("address");
        if(Text("type")!="tab5_ota_diagnostic"||Text("firmware")!=image.Version||Text("elfSha256")!=image.ElfSha256||
            !(partition=="ota_0"&&address==0x20000||partition=="ota_1"&&address==0x700000)||Number("stateError")!=0)
            throw new BootImageMismatchException();
        int state=Number("state");
        if(state==1)throw new PendingBootConfirmationException();
        if(state!=2)throw new BootImageMismatchException();
    }
}
