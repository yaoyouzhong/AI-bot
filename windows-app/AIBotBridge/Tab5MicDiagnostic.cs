using System.Text.Json;
using System.Diagnostics;

namespace AIBotBridge;

// Temporary local diagnostic endpoint: receives no stored audio and never starts the desktop IME.
internal sealed class Tab5MicDiagnostic : ITab5VoiceEndpoint
{
    private readonly object _gate=new();
    private string _id="",_task="";
    private long _lastAudioTick;
    private double _maxAudioGapMs;
    public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token)
    {
        lock(_gate) {
            string Value(string name)=>request.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
            var op=Value("op");
            if(op=="start") { _id=Guid.NewGuid().ToString("N");_task=Value("taskId");_lastAudioTick=0;_maxAudioGapMs=0; }
            if(op=="audio") {
                long tick=Stopwatch.GetTimestamp();
                if(_lastAudioTick!=0)_maxAudioGapMs=Math.Max(_maxAudioGapMs,
                    (tick-_lastAudioTick)*1000.0/Stopwatch.Frequency);
                _lastAudioTick=tick;
                if(request.TryGetProperty("seq",out var seq)&&seq.TryGetInt32(out var number)&&number%10==9)
                    File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"tab5-mic-timing.log"),
                        $"{DateTimeOffset.Now:O} seq={number} max_gap_ms={_maxAudioGapMs:F0}{Environment.NewLine}");
            }
            var state=op is "stop" or "cancel"?op=="stop"?"review":"cancelled":"recording";
            return Task.FromResult(new Tab5VoiceReply(_id,_task,"tab5",state,"TAB5 麦克风诊断中",""));
        }
    }
    public void ShowSettings(IWin32Window owner) { }
    public void Dispose() { }
}
