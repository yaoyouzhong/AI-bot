using System.Speech.AudioFormat;
using System.Speech.Recognition;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AIBotBridge;

// SAPI reads on its own worker. Never return temporary silence as end-of-stream,
// discard audio on overflow, or retain more than two seconds of pending PCM.
internal sealed class WindowsSpeechPcmStream : Stream
{
    private readonly byte[] _bytes=new byte[64000];
    private readonly object _gate=new();
    private int _head,_count;
    private long _read;
    private bool _ended;
    internal int Available {get{lock(_gate)return _bytes.Length-_count;}}
    internal int Pending {get{lock(_gate)return _count;}}
    internal bool Push(byte[] bytes,int count){lock(_gate){
        if(_ended||count>_bytes.Length-_count)return false;
        for(int i=0;i<count;i++)_bytes[(_head+_count+i)%_bytes.Length]=bytes[i];
        _count+=count;Monitor.PulseAll(_gate);return true;
    }}
    internal void Complete(){lock(_gate){_ended=true;Monitor.PulseAll(_gate);}}
    public override int Read(byte[] buffer,int offset,int count){
        ArgumentNullException.ThrowIfNull(buffer);ArgumentOutOfRangeException.ThrowIfNegative(offset);ArgumentOutOfRangeException.ThrowIfNegative(count);
        if(offset>buffer.Length-count)throw new ArgumentException("Invalid buffer range");
        if(count==0)return 0;
        lock(_gate){
            int copied=0;
            // SAPI's COM IStream consumer treats a short read as EOF. Fill its
            // requested block across producer writes, until explicit completion.
            while(copied<count){
                while(_count==0&&!_ended)Monitor.Wait(_gate);
                if(_count==0)break;
                int take=Math.Min(count-copied,_count);
                for(int i=0;i<take;i++)buffer[offset+copied+i]=_bytes[(_head+i)%_bytes.Length];
                _head=(_head+take)%_bytes.Length;_count-=take;_read+=take;copied+=take;
            }
            return copied;
        }
    }
    protected override void Dispose(bool disposing){if(disposing)Complete();base.Dispose(disposing);}
    public override bool CanRead=>true;
    public override bool CanSeek=>false;
    public override bool CanWrite=>false;
    // System.Speech's IStream wrapper snapshots Length even for a live input.
    // EOF is signalled by Complete + Read returning zero, not a finite length.
    public override long Length=>long.MaxValue;
    public override long Position {get{lock(_gate)return _read;}set=>throw new NotSupportedException();}
    public override void Flush(){}
    public override long Seek(long offset,SeekOrigin origin){lock(_gate){
        if(origin==SeekOrigin.Current&&offset==0||origin==SeekOrigin.Begin&&offset==_read)return _read;
        throw new NotSupportedException("Live PCM is forward-only");
    }}
    public override void SetLength(long value)=>throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}

// One backend owns audio and recognition. SAPI receives PCM directly; no desktop
// editor, IME, virtual cable, system-default mutation or cloud credentials.
internal sealed class WindowsSpeechBackend(Func<string> input) : ITab5VoiceAudio,ITab5VoiceEditor
{
    private readonly object _textGate=new();
    private SpeechRecognitionEngine? _engine;
    private WindowsSpeechPcmStream? _pcm;
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private readonly Tab5CaptureActivity _activity=new();
    private string _committed="",_hypothesis="",_source="";
    private volatile string? _failure;
    private volatile bool _running,_completed,_acceptCapture,_acceptPcm,_hasData;
    private bool _prepared,_disposed;
    private volatile int _generation;
    private int _level,_samples;
    private long _lastData;
    internal static RecognizerInfo? Installed()=>SpeechRecognitionEngine.InstalledRecognizers()
        .FirstOrDefault(r=>r.Culture.Name.Equals("zh-CN",StringComparison.OrdinalIgnoreCase));
    internal static string? Availability(){
        try{return Installed()?.Description;}
        catch(Exception ex) when(ex is InvalidOperationException or System.Runtime.InteropServices.COMException or PlatformNotSupportedException){return null;}
    }
    internal async Task PrepareAsync(CancellationToken token){
        Cleanup();token.ThrowIfCancellationRequested();int owner=++_generation;
        SpeechRecognitionEngine? engine=null;WindowsSpeechPcmStream? pcm=null;
        try{
            await Task.Run(()=>{
                var installed=Installed()??throw new InvalidOperationException("未安装微软简体中文识别器，请在 Windows 语言设置中检查语音组件。");
                engine=new SpeechRecognitionEngine(installed);pcm=new();
                engine.LoadGrammar(new DictationGrammar());
                engine.SetInputToAudioStream(pcm,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));
                engine.SpeechHypothesized+=(_,e)=>{lock(_textGate){if(owner==_generation)_hypothesis=e.Result.Text;}};
                engine.SpeechRecognized+=(_,e)=>{lock(_textGate){if(owner==_generation){_committed+=e.Result.Text;_hypothesis="";}}};
                engine.SpeechRecognitionRejected+=(_,_)=>{lock(_textGate){if(owner==_generation)_hypothesis="";}};
                engine.RecognizeCompleted+=(_,e)=>{lock(_textGate){if(owner!=_generation)return;
                    _hypothesis="";if(e.Error is not null)_failure="Windows 语音识别失败，请检查中文语音组件。";_completed=true;_running=false;}};
            },token);
            token.ThrowIfCancellationRequested();ObjectDisposedException.ThrowIf(_disposed,this);
            if(owner!=_generation)throw new OperationCanceledException("语音准备已被取消");
            _engine=engine;_pcm=pcm;engine=null;pcm=null;_prepared=true;_completed=false;_failure=null;
        }catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or NotSupportedException or ArgumentException or IOException){
            throw new InvalidOperationException("无法初始化 Windows 中文识别器，请检查 Windows 语言设置中的语音组件。",ex);
        }finally{pcm?.Complete();engine?.Dispose();pcm?.Dispose();}
    }
    private void Check(){if(_failure is {} failure)throw new InvalidOperationException(failure);}
    public string Text {get{lock(_textGate)return _committed+_hypothesis;}}
    public string RecognizerName=>"Windows 本地识别";
    public string RecognitionHint=>"请检查 Windows 中文语音组件与麦克风";
    public bool SafeFocus=>true;
    public bool? CaptureRunning {get{Check();return _running;}}
    public bool CaptureStopped=>_completed;
    public bool RecognitionComplete=>_completed&&_failure is null;
    bool ITab5VoiceEditor.Start(){
        if(_engine is null||!_prepared)return false;
        _prepared=false;_running=true;_completed=false;
        try{_engine.RecognizeAsync(RecognizeMode.Multiple);_capture?.StartRecording();return true;}
        catch{_running=false;_pcm?.Complete();throw;}
    }
    bool ITab5VoiceEditor.Stop(){_acceptPcm=false;CloseCapture();_pcm?.Complete();return true;}
    public void Clear(){
        if(!_prepared)Cleanup();
        lock(_textGate){_committed=_hypothesis="";}
    }
    public string SourceName {get;private set;}="";
    public bool CanSwitchToTab5=>false;
    public string Start(bool forceTab5){
        if(_engine is null||!_prepared)throw new InvalidOperationException("Windows 识别器尚未准备就绪");
        var inputs=Tab5VoiceAudio.Devices(DataFlow.Capture);string selected=input();
        var device=forceTab5||selected==Tab5VoiceAudio.Tab5Selection?null:Tab5VoiceAudio.SelectInput(inputs,selected);
        if(!forceTab5&&Tab5VoiceAudio.Inspect(inputs,[],selected).SelectedMissing)
            throw new InvalidOperationException("所选麦克风不可用或有多个 DJI，请在语音设置中重新选择。");
        _activity.Reset();_level=0;_samples=0;_hasData=false;_lastData=Environment.TickCount64;_acceptPcm=true;
        if(device is null){_source="tab5";SourceName="TAB5 内置麦克风";return _source;}
        _source="dji";SourceName=device.Name;
        using(var enumerator=new MMDeviceEnumerator())_device=enumerator.GetDevice(device.Id);
        _capture=new WasapiCapture(_device);var buffer=new BufferedWaveProvider(_capture.WaveFormat){ReadFully=false,BufferDuration=TimeSpan.FromSeconds(2)};
        ISampleProvider samples=buffer.ToSampleProvider();
        if(samples.WaveFormat.Channels==2)samples=new StereoToMonoSampleProvider(samples);
        else if(samples.WaveFormat.Channels!=1)throw new InvalidOperationException("此麦克风声道格式暂不支持，请选择单声道或双声道麦克风。");
        var converted=new SampleToWaveProvider16(new WdlResamplingSampleProvider(samples,16000));
        byte[] chunk=new byte[3200];int owner=_generation;_acceptCapture=true;
        _capture.DataAvailable+=(_,e)=>{
            if(!_acceptCapture||owner!=_generation)return;
            try{buffer.AddSamples(e.Buffer,0,e.BytesRecorded);int count;while((count=converted.Read(chunk,0,chunk.Length))>0)Push(chunk,count);}
            catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)||ex is ArgumentException){if(_acceptCapture&&owner==_generation)_failure="Windows 收音缓冲失败，请重新开始录音。";}
        };
        _capture.RecordingStopped+=(_,_)=>{if(_acceptCapture&&owner==_generation)_failure="所选麦克风收音已中断，请检查连接后重试。";};
        return _source;
    }
    private void Push(byte[] pcm,int count){
        if(!_acceptPcm||_pcm is null||!_pcm.Push(pcm,count))throw new InvalidOperationException("Windows 识别音频缓冲已满，本段未完整送达，请重新开始。");
        int level=Tab5AudioMeter.Measure(pcm.AsSpan(0,count),16,false,out bool valid);
        bool cue=_source=="tab5"&&_samples<16000&&valid&&level>=8&&Tab5ReadyCue.IsTone(pcm.AsSpan(0,count));
        _activity.Observe(cue?0:level,valid);_samples+=count/2;Volatile.Write(ref _level,level);
        Interlocked.Exchange(ref _lastData,Environment.TickCount64);_hasData=true;
    }
    public void Feed(byte[] pcm){Check();Push(pcm,pcm.Length);}
    public bool DjiHealthy {get{Check();return _acceptCapture&&_device?.State==DeviceState.Active&&Environment.TickCount64-Interlocked.Read(ref _lastData)<2000;}}
    public bool InputReady {get{Check();return _running&&(_source=="tab5"||_hasData);}}
    public int InputLevel=>Environment.TickCount64-Interlocked.Read(ref _lastData)>600?0:Volatile.Read(ref _level);
    public bool? CapturedSound=>_activity.Sound;
    public bool CanAcceptAudio {get{Check();return _acceptPcm&&_pcm?.Available>=6400;}}
    public bool Drained {get{Check();return _pcm?.Pending==0;}}
    public void Drain(){_acceptPcm=false;CloseCapture();}
    public void Stop(){_acceptPcm=false;CloseCapture();}
    public void UseTab5()=>throw new InvalidOperationException("请重新开始使用 TAB5 麦克风。");
    public string PlaybackDiagnostic=>$"windowsSpeech=true; pendingPcmMs={(_pcm?.Pending??0)/32}";
    private void CloseCapture(){_acceptCapture=false;var capture=_capture;_capture=null;
        try{try{capture?.StopRecording();}finally{capture?.Dispose();}}finally{_device?.Dispose();_device=null;}}
    private void Cleanup(){
        ++_generation;_prepared=false;_acceptPcm=false;CloseCapture();
        var engine=_engine;var pcm=_pcm;_engine=null;_pcm=null;pcm?.Complete();
        try{if(engine is not null){if(_running)engine.RecognizeAsyncCancel();engine.Dispose();}}
        finally{pcm?.Dispose();_running=false;_completed=true;}
    }
    public void Dispose(){if(_disposed)return;_disposed=true;Cleanup();}
}
