using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    // All delivery decisions share _lifecycle with offer replacement, RPC ranges
    // and the final HTTP write. Never accept a stop after releasing the last byte:
    // the device may already be selecting its next boot partition.
    private sealed class OtaDelivery(Tab5OtaPackage image)
    {
        internal readonly Tab5OtaPackage Image=image;
        internal readonly CancellationTokenSource Stop=new();
        internal bool Started,Committed,DataStarted,FinalReleased,StopRequested,Signalled,Confirmed;
        internal string Device="",Host="",OriginalVersion="";
        internal long StopAt,CommitUntil,ConfirmAfter;
    }
    private sealed record OtaStopProbe(OtaDelivery Delivery,string Challenge,long Started,long? UsbSequence=null);
    private OtaDelivery? _otaDelivery;
    private bool OtaStopPending=>_otaDelivery is {StopRequested:true} d&&(!d.Confirmed||!d.Signalled);
    internal bool OtaStarted {get{lock(_lifecycle)return _otaDelivery?.Started==true;}}
    internal bool CanSelectOta {get{lock(_lifecycle)return !Busy&&!OtaStopPending;}}
    internal bool CanStopOta {get{lock(_lifecycle)return _otaDelivery is {Started:true,FinalReleased:false,StopRequested:false} d&&_ota is not null&&(d.OriginalVersion==d.Image.Version||_firmware.LastVersion(PairedId)!=d.Image.Version);}}
    internal string OtaStopHint {get{lock(_lifecycle)return _otaDelivery is {StopRequested:true} d?
        d.Confirmed?"升级已停止，继续使用原固件。":Environment.TickCount64-d.StopAt>30000?
        "传输已停止，尚未收到设备确认；请保持供电并重新连接。":"正在停止升级，等待设备确认；请保持供电和连接。":"";}}
    private void BeginOtaDelivery(OtaDelivery delivery)
    {
        delivery.Started=true;
        if(delivery.Device.Length>0)return;
        var pair=_store.Current;
        delivery.Device=pair?.DeviceId??"";delivery.Host=pair is null?"":Tab5HostBinding.Id(pair);
        delivery.OriginalVersion=_firmware.LastVersion(pair?.DeviceId);
    }
    internal void StopOta()
    {
        OtaDelivery delivery;
        lock(_lifecycle) {
            if(!CanStopOta)throw new InvalidOperationException(_otaDelivery?.FinalReleased==true?
                "固件已传完，设备可能正在校验或切换，不能停止；请保持供电。":"当前没有可停止的升级。");
            delivery=_otaDelivery!;delivery.StopRequested=true;delivery.StopAt=Environment.TickCount64;
            // A ready response may still be in flight. Before any data request,
            // do not mistake its pre-install idle interval for an abort. The
            // firmware rejects that commit after the original command expiry.
            delivery.ConfirmAfter=delivery.Committed&&!delivery.DataStarted?Math.Max(delivery.StopAt,delivery.CommitUntil):delivery.StopAt;
            // With no issued commit or data there is no installation to unwind.
            delivery.Confirmed=!delivery.Committed;
            Volatile.Write(ref _ota,null);
            lock(_remoteOtaLock) {
                _remoteOta?.Completion.TrySetException(new IOException("升级已停止。"));
                _remoteOta=null;
            }
            Interlocked.Exchange(ref _rpcOtaUntil,0);
            _remoteOtaStatus=delivery.Confirmed?"升级已停止。":"正在停止升级，等待设备确认。";
        }
        // Cancelling outside the lock lets an in-flight stream unwind its own
        // finally block. No process kill, port reset or device reboot is used.
        delivery.Stop.Cancel();lock(_lifecycle)delivery.Signalled=true;Republish();
        if(!delivery.Confirmed)_=ConfirmOtaStopAsync(delivery);
    }
    private async Task ConfirmOtaStopAsync(OtaDelivery delivery)
    {
        try {
            while(!_lifetime.IsCancellationRequested) {
                await Task.Delay(Environment.TickCount64-delivery.StopAt>30000?5000:750,_lifetime.Token);
                lock(_lifecycle)if(!ReferenceEquals(_otaDelivery,delivery)||delivery.Confirmed||_stopping)return;
                await RefreshDiagnosticSummaryAsync(_lifetime.Token);
            }
        }catch(Exception ex) when(ex is OperationCanceledException or IOException or InvalidOperationException) {
            // Missing confirmation remains visible. It never becomes success on
            // timeout, nor does it rearm the withdrawn offer.
        }
    }
    private OtaStopProbe? CaptureOtaStopProbe(string challenge,long started,long? usbSequence=null)
    {
        lock(_lifecycle) {
            var d=_otaDelivery;
            return d is {StopRequested:true,Confirmed:false,Signalled:true}&&started>d.StopAt&&started>=d.ConfirmAfter?
                new(d,challenge,started,usbSequence):null;
        }
    }
    private void ObserveOtaStopped(JsonElement snapshot,OtaStopProbe? probe,bool usb=false)
    {
        // Only the result of a distinct request started after Stop is evidence.
        // Ordinary USB ACKs, cached diagnostics and pre-stop wireless probes
        // can arrive late and must never unlock selection of another image.
        if(probe is null)return;
        lock(_lifecycle) {
            var d=_otaDelivery;var pair=_store.Current;
            if(d is not {StopRequested:true,Confirmed:false,Signalled:true}||!ReferenceEquals(d,probe.Delivery)||
               probe.Challenge.Length==0||probe.Started<=d.StopAt||probe.Started<d.ConfirmAfter||_otaTransfers!=0||
               Text(snapshot,"deviceId")!=d.Device||PairedId!=d.Device||
               pair is null||Tab5HostBinding.Id(pair)!=d.Host||Text(snapshot,"firmware")!=d.OriginalVersion)return;
            if(usb) {
                if(probe.UsbSequence is not long expected||!snapshot.TryGetProperty("sequence",out var sequence)||
                   !sequence.TryGetInt64(out long actual)||actual!=expected||Text(snapshot,"currentHostId")!=d.Host||
                   !snapshot.TryGetProperty("usbHostAuthenticated",out var owned)||owned.ValueKind!=JsonValueKind.True)return;
            }else if(probe.UsbSequence is not null||Text(snapshot,"hostId")!=d.Host)return;
            string[] parts=Text(snapshot,"otaDiag").Split(',');
            if(parts.Length!=4||!parts.All(p=>uint.TryParse(p,out _))||parts[3]!="0")return;
            d.Confirmed=true;_remoteOtaStatus="升级已停止，继续使用原固件。";
        }
    }
    private OtaDelivery StartOtaStream(byte[] image,string device)
    {
        lock(_lifecycle) {
            var d=_otaDelivery;
            if(_stopping||d is null||d.StopRequested||!ReferenceEquals(_ota,d.Image)||
               !ReferenceEquals(image,d.Image.Image)||device!=PairedId)
                throw new OperationCanceledException("固件已停止提供或设备已变化。");
            BeginOtaDelivery(d);d.Committed=d.DataStarted=true;_otaTransfers++;
            Interlocked.Exchange(ref _rpcOtaUntil,Environment.TickCount64+90000);
            return d;
        }
    }
    private void BeforeOtaStreamWrite(OtaDelivery delivery,int end,int length)
    {
        lock(_lifecycle) {
            delivery.Stop.Token.ThrowIfCancellationRequested();
            if(!ReferenceEquals(_otaDelivery,delivery)||!ReferenceEquals(_ota,delivery.Image))throw new OperationCanceledException();
            Interlocked.Exchange(ref _rpcOtaUntil,Environment.TickCount64+90000);
            if(end==length)delivery.FinalReleased=true;
        }
    }
}
