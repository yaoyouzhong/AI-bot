using System.Text.Json;

namespace AIBotBridge;

// Keeps a single recording request alive while the desktop initializes. Existing
// firmware renders recording/ready=false as preparation and can poll or cancel it.
internal sealed class Tab5VoicePreparation : IDisposable
{
    // Never cancel the worker task itself: an OS permission dialog may still
    // own the microphone open. Wait for it to finish before touching the audio
    // object again, then honor cancellation without starting the recognizer.
    internal static async Task<T> RunAudioAsync<T>(Func<T> prepare,CancellationToken token){
        T result=await Task.Run(prepare);
        token.ThrowIfCancellationRequested();return result;
    }
    private readonly Func<long> _clock;
    private readonly long _started;
    private long _seen;
    private bool _published;
    private readonly CancellationTokenSource _cancel;
    internal string VoiceId {get;}=Guid.NewGuid().ToString("N");
    internal string TaskId {get;}
    internal string Client {get;}
    internal CancellationToken Token=>_cancel.Token;
    internal bool PermissionWaiting {get;set;}
    internal bool Finished {get;private set;}
    private Tab5VoiceReply? _result;
    internal Tab5VoicePreparation(JsonElement request,CancellationToken token,Func<long>? clock=null)
    {
        TaskId=Text(request,"taskId");Client=Text(request,"client");if(Client.Length==0)Client="codex";
        if(!Guid.TryParseExact(TaskId,"D",out _)||Client is not ("codex" or "claude"))throw new Tab5VoiceRequestException("请选择有效会话");
        _clock=clock??(()=>Environment.TickCount64);_started=_seen=_clock();
        _cancel=CancellationTokenSource.CreateLinkedTokenSource(token);
    }
    private static string Text(JsonElement request,string name)=>request.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
    internal bool Matches(JsonElement request)=>Text(request,"voiceId")==VoiceId&&
        (Text(request,"client") is ""?"codex":Text(request,"client"))==Client;
    internal void Published(){_published=true;_seen=_clock();}
    internal Tab5VoiceReply Snapshot()=>_result??new(VoiceId,TaskId,"","recording",
        PermissionWaiting?"请处理电脑上的权限提示，完成后继续录音":"正在准备语音","");
    internal Tab5VoiceReply Handle(JsonElement request)
    {
        if(!Matches(request))throw new Tab5VoiceRequestException("语音会话已失效");
        string op=Text(request,"op");
        if(op is not ("poll" or "cancel" or "stop"))throw new Tab5VoiceRequestException("语音正在准备，请稍候");
        _seen=_clock();
        if(op is "cancel" or "stop")Cancel("已取消");
        Tick();return Snapshot();
    }
    internal void Tick()
    {
        if(Finished)return;
        if(_clock()-_started>=60000)Cancel("语音准备超时，已停止");
        else if(_published&&_clock()-_seen>=5000)Cancel("语音连接已中断，准备已停止");
    }
    internal void Cancel(string message)
    {
        if(Finished)return;
        _result=new(VoiceId,TaskId,"","cancelled",message,"");Finished=true;_cancel.Cancel();
    }
    internal void Complete(Tab5VoiceReply result)
    {
        if(Finished||Token.IsCancellationRequested)return;
        _result=result with {VoiceId=VoiceId,TaskId=TaskId};Finished=true;
    }
    public void Dispose()=>_cancel.Dispose();
}
