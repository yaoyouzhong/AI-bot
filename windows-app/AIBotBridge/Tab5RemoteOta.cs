using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    private readonly object _remoteOtaLock=new();
    private sealed record RemoteOta(Tab5OtaPackage Image,string Id,long Expires,TaskCompletionSource<bool> Completion);
    private RemoteOta? _remoteOta;
    private volatile string _remoteOtaStatus="";
    private object? RemoteOtaCommand {get{lock(_remoteOtaLock)return _remoteOta is {} r?
        new{version=1,requestId=r.Id,offerId=r.Image.OfferId,sha256=r.Image.Sha256,expiresAt=r.Expires}:null;}}
    internal bool RemoteOtaPending {get{lock(_remoteOtaLock)return _remoteOta is not null;}}
    internal static bool SupportsRemoteOta(string value)=>value.EndsWith("-ui",StringComparison.Ordinal)&&
        Version.TryParse(value[..^3],out var version)&&version>=new Version(0,2,190);
    internal bool CanStartOta=>!_usbAuthenticationRejected&&HasOta&&!Busy&&!RemoteOtaPending&&DeviceView.Online&&
        SupportsRemoteOta(_firmware.LastVersion(_store.Current?.DeviceId))&&
        _firmware.LastVersion(_store.Current?.DeviceId)!=Volatile.Read(ref _ota)?.Version;
    internal string RemoteOtaStatus=>_remoteOtaStatus;
    internal string OtaStartHint=>_usbAuthenticationRejected?"USB 已连接，但当前电脑未通过设备配对认证；请先验证配对，固件尚未传输。":HasOta&&!SupportsRemoteOta(_firmware.LastVersion(_store.Current?.DeviceId))?
        "此设备需先在 Tab5 完成一次升级，之后可从电脑开始升级。":"";
    internal async Task StartOtaAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();RemoteOta request;
        lock(_lifecycle) {
            if(_stopping||!CanStartOta)throw new InvalidOperationException(OtaStartHint.Length>0?OtaStartHint:"请先连接设备并选择固件，或等待当前操作结束。");
            lock(_remoteOtaLock) {
                request=new(Volatile.Read(ref _ota)!,Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds(),new(TaskCreationOptions.RunContinuationsAsynchronously));
                _remoteOta=request;_remoteOtaStatus="正在请求设备保存草稿并准备升级…";_otaTransferDiagnostic="尚无升级传输";_voiceRequests++;
                BeginOtaDelivery(_otaDelivery!);
            }
        }
        try {Republish();await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(35),token);}
        catch(TimeoutException){_remoteOtaStatus="升级请求未确认，请核对设备状态后重试。";throw new IOException(_remoteOtaStatus);}
        catch(OperationCanceledException){if(_otaDelivery?.StopRequested!=true)_remoteOtaStatus="已停止等待；若设备已开始升级，请保持供电并核验启动。";throw;}
        finally {
            lock(_lifecycle){lock(_remoteOtaLock){if(ReferenceEquals(_remoteOta,request))_remoteOta=null;}_voiceRequests--;}
            Republish();
        }
    }
    private (int Status,object Body) ReceiveRemoteOta(JsonElement root)
    {
        lock(_lifecycle) {
        lock(_remoteOtaLock) {
            var r=_remoteOta;
            if(r is null||r.Completion.Task.IsCompleted||Text(root,"requestId")!=r.Id||Text(root,"offerId")!=r.Image.OfferId||
                Text(root,"sha256")!=r.Image.Sha256||!ReferenceEquals(Volatile.Read(ref _ota),r.Image)||
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()>r.Expires)return(409,new{error="request_expired",commit=false});
            switch(Text(root,"stage")) {
                case "ready":
                    _otaDelivery!.Committed=true;
                    _otaDelivery.CommitUntil=Environment.TickCount64+Math.Max(0,r.Expires-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())+6000;
                    _remoteOtaStatus="设备已保存草稿，正在启动升级…";
                    Interlocked.Exchange(ref _rpcOtaUntil,Environment.TickCount64+40000);
                    return(200,new{commit=true});
                case "started":
                    _remoteOtaStatus="设备已开始升级，请保持供电。";
                    Interlocked.Exchange(ref _rpcOtaUntil,Environment.TickCount64+30000);
                    r.Completion.TrySetResult(true);return(200,new{accepted=true});
                case "rejected":
                    if(_otaDelivery is {DataStarted:false} rejected){rejected.Started=false;rejected.Committed=false;rejected.CommitUntil=0;}
                    _remoteOtaStatus=Text(root,"error") switch {
                        "device_busy"=>"设备正在录音、拍照或处理消息，请结束后重试。",
                        "draft_pending"=>"设备还有未确认消息或正在同步的草稿，请处理后重试。",
                        "draft_save_failed"=>"设备草稿保存失败，升级未开始。",
                        "offer_changed"=>"设备上的固件与本次请求不一致，请重新提供固件。",
                        "no_link"=>"设备没有可用的升级通道。",
                        _=>"设备未能开始升级，请查看设备状态。"};
                    r.Completion.TrySetException(new IOException(_remoteOtaStatus));return(200,new{accepted=true});
                default:return(400,new{error="invalid_stage",commit=false});
            }
        }
        }
    }
}
