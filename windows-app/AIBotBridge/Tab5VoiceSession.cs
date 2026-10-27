using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal interface ITab5VoiceAudio : IDisposable
{
    string Start(bool forceTab5);
    string SourceName => "";
    bool DjiHealthy { get; }
    bool InputReady { get; }
    int InputLevel { get; }
    // null means the capture has not supplied enough evidence. A UI meter is
    // only the latest block; implementations must retain activity for the take.
    bool? CapturedSound => null;
    void UseTab5();
    void Feed(byte[] pcm);
    void Drain();
    bool Drained { get; }
    bool CanAcceptAudio => true;
    void PauseForFocus(bool paused) {}
    bool CanSwitchToTab5 => true;
    void ValidateTab5() {}
    string PlaybackDiagnostic => "";
    void Stop();
}
internal interface ITab5VoiceEditor
{
    string Text { get; }
    bool SafeFocus { get; }
    bool TryRestoreFocus()=>false;
    bool FocusRecoveryPending=>false;
    bool CanResumeCapture=>true;
    // null means the backend cannot confirm a native state, never "stopped".
    bool? CaptureRunning=>null;
    void ObserveSafeFocus() {}
    bool Start();
    bool Stop();
    bool CaptureStopped=>true;
    // Only an acknowledged idle recognizer proves that an empty result is final.
    // A successful keyboard stop is not this evidence.
    bool RecognitionComplete=>false;
    void Clear();
}
internal sealed record Tab5VoiceReply(string VoiceId, string TaskId, string Source, string State, string Message, string Text, bool Ready=false,int Level=0,bool Silent=false,bool CanInsert=false,string SourceName="");
internal sealed class Tab5VoiceRequestException(string message) : ArgumentException(message);

// Only the UI owner calls this state machine. Tests use fake audio/editor and a monotonic clock.
internal sealed class Tab5VoiceSession(ITab5VoiceAudio audio, ITab5VoiceEditor editor, Func<long>? clock = null) : IDisposable
{
    internal const string AgendaOwner="a1b07a65-0000-4000-8000-00000000a5ed";
    internal static bool ForceTab5(JsonElement root)=>root.TryGetProperty("source",out var source)&&source.ValueKind==JsonValueKind.String&&source.GetString()=="tab5"&&
        (!root.TryGetProperty("taskId",out var owner)||owner.ValueKind!=JsonValueKind.String||owner.GetString()!=AgendaOwner);
    private readonly Func<long> _clock=clock??(()=>Environment.TickCount64);
    private string _id="",_task="",_source="",_sourceName="",_state="idle",_message="";
    private long _started,_lastSeen,_stopped,_drainedSince=-1,_waitingSince;
    private string _drainMessage="";
    private string _settlingText="";
    private long _textChangedAt;
    private int _nextChunk;
    private long _pcmBytes;
    private bool _ready;
    private long _lastSignal;
    private bool _silent;
    private bool _quietTake,_receivedText;
    private bool _explicitStop;
    private bool _switching;
    private long _finalWaitMs;
    private long _takeMs;
    private string _endReason="";
    private long _audioWaitMs,_audioWaitPeakMs;
    private int _focusRestores;
    private bool _focusPaused;
    private bool _captureEndedEarly;
    private bool HasVoiceFocus() {
        if(editor.SafeFocus) {
            if(_focusPaused&&!editor.CanResumeCapture) {
                End("焦点切换后豆包识别已结束，请检查草稿");return false;
            }
            editor.ObserveSafeFocus();
            if(_focusPaused){audio.PauseForFocus(false);_focusPaused=false;_focusRestores++;}
            return true;
        }
        if(!_focusPaused){audio.PauseForFocus(true);_focusPaused=true;}
        if(editor.TryRestoreFocus()&&editor.SafeFocus) {
            audio.PauseForFocus(false);_focusPaused=false;_focusRestores++;return true;
        }
        if(editor.FocusRecoveryPending)_message="正在恢复语音输入焦点";
        else End("语音输入焦点已变化，收音已停止，请检查草稿");
        return false;
    }
    internal bool Active => _state is "recording" or "draining" or "recognizing";
    internal string Diagnostic=>$"state={_state}; source={_source}; sound={audio.CapturedSound?.ToString()??"unknown"}; quietEnd={_quietTake}; finalWaitMs={(_state=="recognizing"?Math.Max(0,_clock()-_stopped):_finalWaitMs)}; endReason={_endReason}; audioWaitMs={_audioWaitMs}; audioWaitPeakMs={_audioWaitPeakMs}; focusRestores={_focusRestores}; focusPaused={_focusPaused}; pcmChunks={_nextChunk}; pcmMs={_pcmBytes/32}; takeMs={(_state is "recording" or "draining"?Math.Max(0,_clock()-_started):_takeMs)}; textSeen={_receivedText}; {audio.PlaybackDiagnostic}";
    internal bool NeedsSourcePreparation(JsonElement root)=>!_switching&&_state=="recording"&&_source=="dji"&&!audio.CanSwitchToTab5&&
        root.TryGetProperty("op",out var op)&&op.ValueKind==JsonValueKind.String&&op.GetString()=="tab5"&&
        root.TryGetProperty("voiceId",out var id)&&id.ValueKind==JsonValueKind.String&&id.GetString()==_id;
    internal async Task<Tab5VoiceReply> HandleAsync(JsonElement root,CancellationToken token,Func<CancellationToken,Task>? wait=null,
        Func<CancellationToken,Task>? prepareTab5=null) {
        token.ThrowIfCancellationRequested();
        if(NeedsSourcePreparation(root)) {
            await SwitchSharedDjiAsync(token,wait,prepareTab5);
            return Snapshot();
        }
        bool PendingAudio()=>_state=="recording"&&_source=="tab5"&&
            root.TryGetProperty("op",out var op)&&op.ValueKind==JsonValueKind.String&&op.GetString()=="audio"&&
            root.TryGetProperty("voiceId",out var id)&&id.ValueKind==JsonValueKind.String&&id.GetString()==_id&&
            root.TryGetProperty("seq",out var seq)&&seq.TryGetInt32(out int number)&&number==_nextChunk;
        long started=_clock();bool waited=false;
        if(PendingAudio())Tick();
        while(PendingAudio()&&(_focusPaused||!audio.CanAcceptAudio)) {
            token.ThrowIfCancellationRequested();
            _lastSeen=_clock();Tick();
            if(!PendingAudio())break;
            if(!_focusPaused&&audio.CanAcceptAudio)break;
            _audioWaitMs=_clock()-started;_audioWaitPeakMs=Math.Max(_audioWaitPeakMs,_audioWaitMs);
            if(_audioWaitMs>=1500) {
                Fail("电脑音频播放积压，请检查虚拟麦克风后重试");return Snapshot();
            }
            waited=true;
            // Yield the UI thread so playback, stop/cancel and TSF can progress.
            // The transport reply supplies backpressure; never drop audio.
            await (wait?.Invoke(token)??Task.Delay(20,token));
        }
        if(waited){_audioWaitMs=_clock()-started;_audioWaitPeakMs=Math.Max(_audioWaitPeakMs,_audioWaitMs);}
        token.ThrowIfCancellationRequested();
        return Handle(root);
    }
    internal Tab5VoiceReply Snapshot() => new(_id,_task,_source,_state,_message,BoundText(editor.Text),_state=="recording"&&_ready&&!_focusPaused,
        _state=="recording"&&_ready?Math.Clamp(audio.InputLevel,0,100):0,_state=="recording"&&_ready&&_silent,
        _state=="review"&&_explicitStop&&_endReason=="正在等待豆包返回文字"&&editor.RecognitionComplete&&Encoding.UTF8.GetByteCount(editor.Text)<=2000,_sourceName);
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
            _quietTake=false;_receivedText=false;
            _explicitStop=false;
            _stopped=_finalWaitMs=0;
            _audioWaitMs=_audioWaitPeakMs=0;_focusRestores=0;_focusPaused=false;
            _takeMs=0;_endReason="";_captureEndedEarly=false;
            editor.Clear();
            try {
                long stageStarted=Environment.TickCount64;
                _source=audio.Start(ForceTab5(root));
                _sourceName=audio.SourceName;
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
                if(_state!="recording")break;
                SwitchToTab5();break;
            case "audio":
                // In-flight audio may arrive after focus loss, stop or completion.
                // Return the original state/cause without playing it or replacing
                // the error. The voiceId check above still isolates other takes.
                if(_state!="recording")break;
                if(_focusPaused)throw new Tab5VoiceRequestException("语音焦点正在恢复，请稍候");
                if(_source!="tab5")throw new Tab5VoiceRequestException("当前使用电脑麦克风");
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
                if(_state=="recording"){_explicitStop=true;BeginDrain("正在等待豆包返回文字");}
                break;
            case "cancel":
                if(_state is "recording" or "draining")End("已取消");
                _state="cancelled";editor.Clear();_message="已取消";break;
            default: throw new ArgumentException("未知语音操作");
        }
        return Snapshot();
    }
    private void SwitchToTab5() {
        if(_switching||_source=="tab5")return;
        if(!audio.CanSwitchToTab5)throw new InvalidOperationException("请先结束大疆录音，再重新开始使用 TAB5 内置麦克风");
        audio.UseTab5();_source="tab5";_sourceName=audio.SourceName;_ready=false;_waitingSince=_clock();_message="正在准备 TAB5 收音";
    }
    private async Task SwitchSharedDjiAsync(CancellationToken token,Func<CancellationToken,Task>? wait,Func<CancellationToken,Task>? prepareTab5) {
        if(prepareTab5 is null)throw new InvalidOperationException("内置麦克风切换尚未就绪");
        audio.ValidateTab5();
        string owner=_id;
        _switching=true;_ready=false;_message="正在结束大疆收音，保留文字…";
        void CheckOwner() {
            token.ThrowIfCancellationRequested();
            if(_id!=owner||_state!="recording")throw new OperationCanceledException("语音切换已取消");
            if(!editor.SafeFocus)throw new InvalidOperationException("输入焦点已变化，切换已停止，文字保留在电脑草稿");
            _lastSeen=_clock();
        }
        try {
            // Direct DJI uses the desktop IME endpoint. Finish that owned take
            // before borrowing CABLE Output; never change an active IME input.
            audio.Stop();
            if(!editor.Stop())throw new InvalidOperationException("大疆录音未确认结束，请在电脑豆包中检查");
            long stopped=_clock(),changed=stopped;string text=editor.Text;
            while(true) {
                CheckOwner();long now=_clock();
                if(editor.Text!=text){text=editor.Text;changed=now;}
                if(editor.RecognitionComplete&&editor.CaptureStopped&&now-stopped>=1200&&now-changed>=800)break;
                if(now-stopped>=8000)throw new InvalidOperationException("大疆文字尚未确认完成，已保留电脑草稿，请稍后重试");
                await (wait?.Invoke(token)??Task.Delay(50,token));
            }
            _message="正在切换 TAB5 内置麦克风…";
            await prepareTab5(token);CheckOwner();
            _source=audio.Start(true);_sourceName=audio.SourceName;
            if(_source!="tab5"||!editor.Start())throw new InvalidOperationException("内置麦克风未启动，已保留电脑草稿");
            // Preserve voiceId, owner, committed text and total take duration.
            // DJI sent no PCM, so the device still begins with sequence zero.
            _pcmBytes=0;_quietTake=false;
            _waitingSince=_lastSeen=_lastSignal=_clock();_silent=false;
            _message="正在准备 TAB5 收音";
        } catch {
            if(_id==owner&&_state=="recording")Fail("音源切换未完成，文字已保留在电脑草稿");
            throw;
        } finally {_switching=false;}
    }
    private void BeginDrain(string message) {
        audio.Drain();_state="draining";_stopped=_clock();_drainedSince=-1;
        _drainMessage=message;_message="正在送完最后一段声音";
    }
    private void End(string message) {
        _endReason=message;
        _takeMs=Math.Max(0,_clock()-_started);
        // Always try to end IME capture even if the unplugged audio endpoint fails to close.
        bool stopped=false, audioFailed=false;
        try{audio.Stop();}
        catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)){audioFailed=true;}
        finally{_focusPaused=false;_state="error";stopped=editor.Stop();_stopped=_clock();}
        _quietTake=_ready&&!audioFailed&&audio.CapturedSound==false;
        _settlingText=editor.Text;_textChangedAt=_stopped;
        _receivedText|=!string.IsNullOrWhiteSpace(_settlingText);
        _state=stopped&&!audioFailed?"recognizing":"error";
        _message=!stopped?(message.Contains("焦点")?message+"；请在电脑豆包中结束识别":"收音已停止；请在电脑豆包中结束识别"):audioFailed?"音频设备已断开，识别已结束，请检查草稿":message;
    }
    internal void Tick() {
        // The asynchronous source handoff owns stop/finalize/prepare/start.
        // Keep the existing wire state while waiting so old firmware can poll
        // and start its microphone after the final source=tab5 response.
        if(_switching)return;
        if(Active&&!string.IsNullOrWhiteSpace(editor.Text))_receivedText=true;
        if(_state=="draining") {
            if(!HasVoiceFocus()){_drainedSince=-1;return;}
            if(editor.CaptureRunning==false){CaptureEndedEarly();return;}
            if(audio.Drained) {if(_drainedSince<0)_drainedSince=_clock();}
            else _drainedSince=-1;
            // BufferedBytes excludes samples already submitted to WASAPI. Leave playback
            // latency AFTER the queue empties, rather than measuring from the stop command.
            if(_drainedSince>=0&&_clock()-_drainedSince>=200)End(_drainMessage);
            else if(_clock()-_stopped>3000)Fail("最后一段声音未能送完，请检查电脑音频通路后重试");
        }
        if(_state=="recording") {
            if(!HasVoiceFocus())return;
            if(editor.CaptureRunning==false){CaptureEndedEarly();return;}
            if(_clock()-_lastSeen>4000){BeginDrain("连接已断开，收音已停止，等待取回识别文字");return;}
            if(_clock()-_started>=60000){BeginDrain("已达到 60 秒，等待识别文字");return;}
            if(_source=="dji"&&!audio.DjiHealthy) {
                if(!audio.CanSwitchToTab5){Fail("大疆已断开，录音已停止；重新开始可使用 TAB5 内置麦克风");return;}
                SwitchToTab5();
            }
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
            _receivedText|=!empty;
            // Wait for committed text to settle, not a fixed six-second pause.
            // Every revision restarts the quiet period; late/empty results retain
            // the draft window and are never treated as a successful completion.
            if(!empty&&now-_stopped>=1200&&now-_textChangedAt>=800&&editor.CaptureStopped) {
                _finalWaitMs=now-_stopped;
                _state="review";
                _message=_captureEndedEarly?"豆包提前结束收音，已保留文字，请补录未完成部分":
                    _endReason.Contains("焦点")?"语音因焦点变化中断，已保留文字，请检查是否完整":
                    Encoding.UTF8.GetByteCount(text)>2000?"文字过长，TAB5 仅显示前段；完整文字请到电脑草稿查看":"文字已返回，请检查后发送";
            // Native idle only acknowledges capture completion. It cannot prove
            // that the TSF text result has arrived. Only measured silence may
            // finish early; received/unknown sound retains the full delivery wait.
            } else if(empty&&!_receivedText&&_quietTake&&now-_stopped>=1200&&editor.CaptureStopped) {
                _finalWaitMs=now-_stopped;
                _state="cancelled";
                _message="未检测到语音，已结束";
            } else if(now-_stopped>=8000) {
                _finalWaitMs=now-_stopped;
                _state="error";
                _message=empty?(_captureEndedEarly?"豆包提前结束收音，未收到文字，请检查电脑豆包状态":audio.CapturedSound==true?"已收到声音，但未收到识别文字，请检查电脑豆包状态":"未收到豆包文字，请检查语音快捷键和麦克风选择"):"豆包文字仍在变化，请在电脑草稿中确认完整内容";
            }
        }
        // The device may spend up to 75 s detecting a lost reply, then 120 s
        // waiting for its original link. Keep this take beyond that window.
        if(!Active&&_id.Length>0&&_clock()-_lastSeen>300000) {
            editor.Clear();_id=_task=_source=_sourceName="";_state="idle";
        }
    }
    private void CaptureEndedEarly() {
        _captureEndedEarly=true;
        End("豆包已提前结束收音，正在取回已识别文字");
    }
    internal void Fail(string message){if(_state is "recording" or "draining")End(message);_state="error";_message=message;}
    public void Dispose(){
        try{if(_state is "recording" or "draining")End("已停止");}
        finally{try{audio.Dispose();}finally{editor.Clear();}}
    }
}
