using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AIBotBridge;

internal sealed record Tab5AudioDevice(string Id,string Name) { public override string ToString()=>Name; }
internal sealed record Tab5VoiceAvailability(bool CableReady,bool DjiAvailable,string Message);
internal sealed class Tab5VoiceAudio(Func<string> djiId) : ITab5VoiceAudio
{
    private WasapiCapture? _capture;
    private WasapiOut? _output;
    private BufferedWaveProvider? _buffer;
    private MMDevice? _inputDevice,_outputDevice;
    private long _lastData;
    private volatile bool _captureFailed;
    private volatile bool _outputFailed;
    private string _source="";
    private bool _hasInputData;
    private string _cableOutputId="";
    private int _inputLevel;
    private long _levelAt;
    private readonly Tab5CaptureActivity _activity=new();
    private int _tab5Samples;
    public bool? CapturedSound=>_activity.Sound;
    public int InputLevel => Environment.TickCount64-Interlocked.Read(ref _levelAt)>600?0:Volatile.Read(ref _inputLevel);
    private void Meter(byte[] bytes,int count,WaveFormat format,bool readyCueWindow=false) {
        bool floating=format.Encoding==WaveFormatEncoding.IeeeFloat || format is WaveFormatExtensible extended&&extended.SubFormat==new Guid("00000003-0000-0010-8000-00aa00389b71");
        int level=Tab5AudioMeter.Measure(bytes.AsSpan(0,count),format.BitsPerSample,floating,out bool valid);
        Volatile.Write(ref _inputLevel,level);
        // Latch every received block, including speech between UI timer ticks
        // and the final tail. Use the existing very low silence threshold;
        // uncertain/noisy input keeps the normal recognition grace period.
        bool cue=readyCueWindow&&valid&&level>=8&&Tab5ReadyCue.IsTone(bytes.AsSpan(0,count));
        _activity.Observe(cue?0:level,valid);
        Interlocked.Exchange(ref _levelAt,Environment.TickCount64);
    }
    internal static bool IsDji(string name)=>name.Contains("DJI",StringComparison.OrdinalIgnoreCase)||name.Contains("Wireless Mic Rx",StringComparison.OrdinalIgnoreCase);
    internal static Tab5AudioDevice[] Devices(DataFlow flow) {
        using var enumerator=new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(flow,DeviceState.Active).Select(d=>{using(d)return new Tab5AudioDevice(d.ID,d.FriendlyName);}).ToArray();
    }
    internal static bool IsCable(string name)=>name.StartsWith("CABLE Input",StringComparison.OrdinalIgnoreCase)&&name.Contains("VB-Audio",StringComparison.OrdinalIgnoreCase);
    internal static bool IsCableCapture(string name)=>name.StartsWith("CABLE Output",StringComparison.OrdinalIgnoreCase)&&name.Contains("VB-Audio",StringComparison.OrdinalIgnoreCase);
    internal static Tab5AudioDevice? SelectInput(Tab5AudioDevice[] inputs,string selectedId)=>
        inputs.FirstOrDefault(d=>!IsCableCapture(d.Name)&&(selectedId.Length>0?d.Id==selectedId:IsDji(d.Name)));
    internal static Tab5VoiceAvailability Inspect(Tab5AudioDevice[] inputs,Tab5AudioDevice[] outputs,string selectedId) {
        bool render=outputs.Any(d=>IsCable(d.Name));
        bool capture=inputs.Any(d=>IsCableCapture(d.Name));
        bool dji=SelectInput(inputs,selectedId) is not null;
        string message=!render||!capture?"VB-CABLE 音频通路未就绪，请安装后检查录音端和播放端是否启用。":
            dji?"已检测到大疆输入设备；开始时尝试大疆，打开失败则改用 TAB5。":"未检测到所选大疆输入设备；开始时使用 TAB5 收音。";
        return new(render&&capture,dji,message);
    }
    public string Start(bool forceTab5) {
        Stop();
        _activity.Reset();
        _tab5Samples=0;
        var inputs=Devices(DataFlow.Capture);var selectedId=djiId();
        var outputs=Devices(DataFlow.Render);
        var availability=Inspect(inputs,outputs,selectedId);
        if(!availability.CableReady)throw new InvalidOperationException(availability.Message);
        _cableOutputId=outputs.First(d=>IsCable(d.Name)).Id;
        var selected=SelectInput(inputs,selectedId);
        if(!forceTab5&&selected is not null) {
            try {
                using var enumerator=new MMDeviceEnumerator();
                _inputDevice=enumerator.GetDevice(selected.Id);_capture=new WasapiCapture(_inputDevice);
                OpenOutput(_capture.WaveFormat);_captureFailed=false;_lastData=Environment.TickCount64;
                var buffer=_buffer!;
                var inputFormat=_capture.WaveFormat;
                _capture.DataAvailable+=(_,e)=> {
                    if(e.BytesRecorded<=0)return;
                    Meter(e.Buffer,e.BytesRecorded,inputFormat);
                    try {buffer.AddSamples(e.Buffer,0,e.BytesRecorded);Interlocked.Exchange(ref _lastData,Environment.TickCount64);Volatile.Write(ref _hasInputData,true);}
                    catch(InvalidOperationException){_captureFailed=true;}
                };
                _capture.RecordingStopped+=(_,_)=>_captureFailed=true;
                _capture.StartRecording();_source="dji";return _source;
            } catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException or NAudio.MmException) {Stop();}
        }
        UseTab5();return _source;
    }
    private void OpenOutput(WaveFormat format) {
        if(_cableOutputId.Length==0)throw new InvalidOperationException("虚拟麦克风未连接");
        using var enumerator=new MMDeviceEnumerator();_outputDevice=enumerator.GetDevice(_cableOutputId);
        _buffer=new BufferedWaveProvider(format){BufferDuration=TimeSpan.FromSeconds(2),DiscardOnBufferOverflow=false};
        _outputFailed=false;
        _output=new WasapiOut(_outputDevice,AudioClientShareMode.Shared,true,80);
        _output.PlaybackStopped+=(_,e)=>{if(e.Exception is not null)_outputFailed=true;};
        _output.Init(_buffer);_output.Play();
    }
    public bool DjiHealthy {
        get {
            if(_outputFailed)throw new InvalidOperationException("虚拟麦克风已断开");
            try{return _source=="dji"&&!_captureFailed&&_inputDevice?.State==DeviceState.Active&&Environment.TickCount64-Interlocked.Read(ref _lastData)<2000;}
            catch(System.Runtime.InteropServices.COMException){return false;}
        }
    }
    public bool InputReady {
        get {
            if(_outputFailed||_output?.PlaybackState!=PlaybackState.Playing)return false;
            return _source=="tab5"||(_source=="dji"&&DjiHealthy&&Volatile.Read(ref _hasInputData));
        }
    }
    public void UseTab5(){if(_source=="tab5")return;Stop();OpenOutput(new WaveFormat(16000,16,1));_source="tab5";}
    public void Feed(byte[] pcm){if(_outputFailed||_source!="tab5"||_buffer is null)throw new InvalidOperationException("TAB5 音频未就绪");Meter(pcm,pcm.Length,_buffer.WaveFormat,_tab5Samples<16000);_tab5Samples+=pcm.Length/2;_buffer.AddSamples(pcm,0,pcm.Length);}
    public void Drain(){var capture=_capture;_capture=null;try{capture?.StopRecording();}finally{capture?.Dispose();}}
    public bool Drained {
        get {
            if(_outputFailed)throw new InvalidOperationException("虚拟麦克风已断开");
            return _buffer is null||_buffer.BufferedBytes==0;
        }
    }
    public void Stop() {
        // Detach first so a failure disposing one endpoint cannot strand the other
        // endpoint or make the next start reuse half-disposed capture state.
        var capture=_capture;var output=_output;var inputDevice=_inputDevice;var outputDevice=_outputDevice;
        _capture=null;_output=null;_buffer=null;_inputDevice=null;_outputDevice=null;_source="";
        _hasInputData=false;
        Volatile.Write(ref _inputLevel,0);Interlocked.Exchange(ref _levelAt,0);
        try {try{capture?.StopRecording();}finally{capture?.Dispose();}}
        finally {
            try {try{output?.Stop();}finally{output?.Dispose();}}
            finally {try{inputDevice?.Dispose();}finally{outputDevice?.Dispose();}}
        }
    }
    public void Dispose()=>Stop();
}
