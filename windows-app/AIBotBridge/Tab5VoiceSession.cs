using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal interface ITab5VoiceAudio : IDisposable
{
    string Start(bool forceTab5);
    bool DjiHealthy { get; }
    bool InputReady { get; }
    int InputLevel { get; }
    void UseTab5();
    void Feed(byte[] pcm);
    void Drain();
    bool Drained { get; }
    void Stop();
}
internal interface ITab5VoiceEditor
{
    string Text { get; }
    bool SafeFocus { get; }
    bool Start();
    bool Stop();
    bool CaptureStopped=>true;
    void Clear();
}
internal sealed record Tab5VoiceReply(string VoiceId, string TaskId, string Source, string State, string Message, string Text, bool Ready=false,int Level=0,bool Silent=false);
internal sealed class Tab5VoiceRequestException(string message) : ArgumentException(message);

// Only the UI owner calls this state machine. Tests use fake audio/editor and a monotonic clock.
internal sealed class Tab5VoiceSession(ITab5VoiceAudio audio, ITab5VoiceEditor editor, Func<long>? clock = null) : IDisposable
{
    private readonly Func<long> _clock=clock??(()=>Environment.TickCount64);
    private string _id="",_task="",_source="",_state="idle",_message="";
    private long _started,_lastSeen,_stopped,_drainedSince=-1,_waitingSince;
    private string _drainMessage="";
    private string _settlingText="";
    private long _textChangedAt;
    private int _nextChunk;
    private long _pcmBytes;
    private bool _ready;
    private long _lastSignal;
    private bool _silent;
    internal bool Active => _state is "recording" or "draining" or "recognizing";
    internal Tab5VoiceReply Snapshot() => new(_id,_task,_source,_state,_message,BoundText(editor.Text),_state=="recording"&&_ready,
        _state=="recording"&&_ready?Math.Clamp(audio.InputLevel,0,100):0,_state=="recording"&&_ready&&_silent);
    internal static string BoundText(string text) {
        if(Encoding.UTF8.GetByteCount(text)<=2000)return text;
        var result=new StringBuilder();int bytes=0;
        foreach(var rune in text.EnumerateRunes()) {if(bytes+rune.Utf8SequenceLength>2000)break;result.Append(rune.ToString());bytes+=rune.Utf8SequenceLength;}
        return result.ToString();
    }
    internal Tab5VoiceReply Handle(JsonElement root) {
        Tick();
        string Str(string key)=>root.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
        var op=Str("op");
        if(op=="start") {
            if(Active)throw new Tab5VoiceRequestException("已有语音输入正在进行");
            if(!Guid.TryParseExact(Str("taskId"),"D",out _))throw new Tab5VoiceRequestException("请选择任务");
            _id=Guid.NewGuid().ToString("N");_task=Str("taskId");_nextChunk=0;_pcmBytes=0;_ready=false;
            _silent=false;_lastSignal=_clock();
            editor.Clear();
            try {
                long stageStarted=Environment.TickCount64;
                _source=audio.Start(Str("source")=="tab5");
                Tab5VoiceTiming.Log("audio-open",stageStarted);stageStarted=Environment.TickCount64;
                if(!editor.Start())throw new InvalidOperationException("请在电脑上打开语音草稿窗口并选择豆包输入法");
                Tab5VoiceTiming.Log("shortcut",stageStarted);
                _state="recording";_message="正在等待电脑收音就绪";
                _started=_lastSeen=_waitingSince=_clock();
            } catch {_state="error";_message="语音未启动，请检查电脑设置";audio.Stop();throw;}
            return Snapshot();
        }
        if(_id.Length==0||Str("voiceId")!=_id)throw new Tab5VoiceRequestException("语音会话已失效");
        _lastSeen=_clock();
        switch(op) {
            case "poll": break;
            case "tab5":
                if(_state!="recording")throw new InvalidOperationException("当前未收音");
                SwitchToTab5();break;
            case "audio":
                if(_state!="recording"||_source!="tab5")throw new InvalidOperationException("当前不接收 TAB5 声音");
                if(!root.TryGetProperty("seq",out var seq)||!seq.TryGetInt32(out var number)||number!=_nextChunk)
                    throw new ArgumentException("音频顺序错误，请重新开始");
                byte[] pcm;
                try {pcm=Convert.FromBase64String(Str("pcm"));}catch(FormatException){throw new ArgumentException("音频格式错误");}
                if(Str("codec")=="ima-adpcm")pcm=Tab5VoiceAdpcm.Decode(pcm);
                else if(Str("codec")=="ima-adpcm8k")pcm=Tab5VoiceAdpcm.Decode8k(pcm);
                else if(Str("codec") is not ("" or "pcm16"))throw new ArgumentException("不支持的音频编码");
                if(pcm.Length is <2 or >6400 || pcm.Length%2!=0 || _pcmBytes+pcm.Length>32000*60)
                    throw new ArgumentException("音频长度超出范围");
                audio.Feed(pcm);_pcmBytes+=pcm.Length;_nextChunk++;Tick();break;
            case "stop":
                if(_state=="recording")BeginDrain("正在等待豆包返回文字");
                break;
            case "cancel":
                if(_state is "recording" or "draining")End("已取消");
                _state="cancelled";editor.Clear();_message="已取消";break;
            default: throw new ArgumentException("未知语音操作");
        }
        return Snapshot();
    }
    private void SwitchToTab5() {audio.UseTab5();_source="tab5";_ready=false;_waitingSince=_clock();_message="正在准备 TAB5 收音";}
    private void BeginDrain(string message) {
        audio.Drain();_state="draining";_stopped=_clock();_drainedSince=-1;
        _drainMessage=message;_message="正在送完最后一段声音";
    }
    private void End(string message) {
        // Always try to end IME capture even if the unplugged audio endpoint fails to close.
        bool stopped=false, audioFailed=false;
        try{audio.Stop();}
        catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)){audioFailed=true;}
        finally{_state="error";stopped=editor.Stop();_stopped=_clock();}
        _settlingText=editor.Text;_textChangedAt=_stopped;
        _state=stopped&&!audioFailed?"recognizing":"error";
        _message=!stopped?"收音已停止；请在电脑豆包中结束识别":audioFailed?"音频设备已断开，识别已结束，请检查草稿":message;
    }
    internal void Tick() {
        if(_state=="draining") {
            if(!editor.SafeFocus){End("输入焦点已变化，收音已停止");return;}
            if(audio.Drained) {if(_drainedSince<0)_drainedSince=_clock();}
            else _drainedSince=-1;
            // BufferedBytes excludes samples already submitted to WASAPI. Leave playback
            // latency AFTER the queue empties, rather than measuring from the stop command.
            if(_drainedSince>=0&&_clock()-_drainedSince>=200)End(_drainMessage);
            else if(_clock()-_stopped>3000)Fail("最后一段声音未能送完，请检查电脑音频通路后重试");
        }
        if(_state=="recording") {
            if(!editor.SafeFocus){End("输入焦点已变化，收音已停止");return;}
            if(_clock()-_lastSeen>4000){End("连接已断开，收音已停止");return;}
            if(_clock()-_started>=60000){BeginDrain("已达到 60 秒，等待识别文字");return;}
            if(_source=="dji"&&!audio.DjiHealthy)SwitchToTab5();
            // The shortcut must be acknowledged and the virtual output running.
            // TAB5 also needs received PCM; DJI needs an actual capture callback.
            // This confirms the audio path, not a private IME recognition API.
            bool ready=(_source=="dji"||_pcmBytes>0)&&audio.InputReady;
            if(ready&&!_ready){Tab5VoiceTiming.Log("capture-ready",_started);_lastSignal=_clock();}
            _ready=ready;
            if(!ready||audio.InputLevel>=8)_lastSignal=_clock();
            _silent=ready&&_clock()-_lastSignal>=6000;
            _message=!ready?"正在等待电脑收音就绪":_silent?"暂未检测到声音，请检查麦克风":"可以说话了";
            if(ready)_waitingSince=_clock();
            else if(_clock()-_waitingSince>=8000)Fail("音频通路尚未就绪，请检查电脑麦克风设置");
        }
        if(_state=="recognizing") {
            long now=_clock();string text=editor.Text;
            if(text!=_settlingText){_settlingText=text;_textChangedAt=now;}
            bool empty=string.IsNullOrWhiteSpace(text);
            // Wait for committed text to settle, not a fixed six-second pause.
            // Every revision restarts the quiet period; late/empty results retain
            // the draft window and are never treated as a successful completion.
            if(!empty&&now-_stopped>=1200&&now-_textChangedAt>=800&&editor.CaptureStopped) {
                _state="review";
                _message=Encoding.UTF8.GetByteCount(text)>2000?"文字过长，TAB5 仅显示前段；完整文字请到电脑草稿查看":"文字已返回，请检查后发送";
            } else if(now-_stopped>=8000) {
                _state="error";
                _message=empty?"未收到豆包文字，请检查语音快捷键和麦克风选择":"豆包文字仍在变化，请在电脑草稿中确认完整内容";
            }
        }
        if(!Active&&_id.Length>0&&_clock()-_lastSeen>30000) {
            editor.Clear();_id=_task=_source="";_state="idle";
        }
    }
    internal void Fail(string message){if(_state is "recording" or "draining")End(message);_state="error";_message=message;}
    public void Dispose(){
        try{if(_state is "recording" or "draining")End("已停止");}
        finally{try{audio.Dispose();}finally{editor.Clear();}}
    }
}
