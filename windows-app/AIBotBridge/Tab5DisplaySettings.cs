using System.Text.Json;

namespace AIBotBridge;
internal sealed record Tab5DisplaySettings(string Revision,int Brightness,int Volume,bool Muted,int Selected,bool Cycle,int Interval,int SaverMinutes,bool Alerts,int[] Enabled,int[] Order)
{
    internal static readonly string[] Pages=["总览","模型额度","天气","行情","电脑状态","音乐","桌宠","时钟"];
    internal void Validate() {
        if(Revision is null||Revision.Length!=8||Revision.Any(c=>!char.IsAsciiHexDigit(c))||Brightness is <10 or >100||Volume is <0 or >100||Selected is <-1 or >7||
            Interval is not (10 or 15 or 30 or 60)||SaverMinutes is not (0 or 1 or 5 or 10 or 30 or 60)||Enabled is null||Enabled.Length!=8||Enabled.Any(n=>n is not (0 or 1))||!Enabled.Contains(1)||
            Order is null||Order.Length!=8||!Order.Order().SequenceEqual(Enumerable.Range(0,8))||Cycle&&Selected!=-1)
            throw new InvalidDataException("设备显示设置无效。");
    }
}

internal sealed partial class Tab5Service
{
    private readonly object _displayLock=new();
    private object? _displayCommand;
    private string? _displayRequestId;
    private TaskCompletionSource<Tab5DisplaySettings>? _displayCompletion;
    private object? DisplayCommand {get{lock(_displayLock)return _displayCommand;}}
    internal async Task<Tab5DisplaySettings> DisplaySettingsAsync(Tab5DisplaySettings? desired,CancellationToken token) {
        desired?.Validate();
        TaskCompletionSource<Tab5DisplaySettings> completion;
        lock(_lifecycle) {
            if(_stopping||Busy)throw new InvalidOperationException("设备正在处理操作，请稍后再试。");
            lock(_displayLock) {
                if(_displayCompletion is not null)throw new InvalidOperationException("正在等待设备设置回执。");
                if(!DeviceView.Online)throw new IOException("TAB5 离线，请连接后重试。");
                _displayCompletion=completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
                _displayRequestId=Guid.NewGuid().ToString("N");
                _displayCommand=new {version=1,requestId=_displayRequestId,expiresAt=DateTimeOffset.UtcNow.AddSeconds(20).ToUnixTimeMilliseconds(),settings=desired};
                _voiceRequests++; // Share the existing lifecycle busy guard.
            }
        }
        try{Republish();return await completion.Task.WaitAsync(TimeSpan.FromSeconds(25),token);}
        catch(TimeoutException){throw new IOException(desired is null?"未收到设备设置，请确认连接，并安装支持显示设置协议的 TAB5 固件。":"保存结果未确认，请重新读取设备设置后再修改。");}
        finally {lock(_lifecycle){lock(_displayLock){_displayCompletion=null;_displayCommand=null;_displayRequestId=null;}_voiceRequests--;}Republish();}
    }
    private (int Status,object Body) ReceiveDisplaySettings(JsonElement root) {
        lock(_displayLock) {
            if(Text(root,"requestId")!=_displayRequestId||_displayCompletion is null)return(409,new{error="request_expired"});
            try {
                var settings=root.GetProperty("settings").Deserialize<Tab5DisplaySettings>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException();settings.Validate();
                if(root.TryGetProperty("ok",out var ok)&&ok.ValueKind==JsonValueKind.True)_displayCompletion.TrySetResult(settings);
                else _displayCompletion.TrySetException(new IOException(Text(root,"error") switch {"conflict"=>"设备设置已在其他位置改变，请重新读取后再保存。","device_busy"=>"TAB5 正在编辑本机设置，请退出设备设置页面后重试。",_=>"设备未能完整保存设置，请重新读取确认实际值。"}));
                return(200,new{accepted=true});
            }catch(Exception ex) when(ex is JsonException or InvalidDataException or KeyNotFoundException){return(400,new{error="invalid_settings"});}
        }
    }
}
