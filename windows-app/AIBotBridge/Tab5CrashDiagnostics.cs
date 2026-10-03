using System.Text.Json;

namespace AIBotBridge;

// Keep bounded numeric panic evidence for successive boots, even when the
// device's RTC record is replaced by another reset. No credentials or media.
internal sealed class Tab5CrashDiagnostics
{
    private long _uptime=-1;
    private int _attempts;
    private bool _captured;
    private readonly Queue<object> _records=[];
    internal bool Observe(long uptime,int reason) {
        if(uptime<0)return false;
        if(_uptime<0||uptime<_uptime){_captured=false;_attempts=0;}
        _uptime=uptime;
        if(reason!=4||_captured||_attempts>=3)return false;
        _attempts++;return true;
    }
    internal void Record(string firmware,JsonElement root) {
        if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("trace",out var trace)||trace.ValueKind!=JsonValueKind.Object||
           !trace.TryGetProperty("crash",out var crash)||crash.ValueKind!=JsonValueKind.Number||!crash.TryGetInt32(out int value)||value is <0 or >1||
           System.Text.Encoding.UTF8.GetByteCount(trace.GetRawText())>3072)throw new IOException("Invalid crash diagnostic");
        _captured=true;
        lock(_records) {
            _records.Enqueue(new{observedAt=DateTimeOffset.Now,firmware,trace=trace.Clone()});
            while(_records.Count>8)_records.Dequeue();
        }
    }
    internal string Snapshot {get{lock(_records)return JsonSerializer.Serialize(_records.ToArray(),JsonDefaults.Options);}}
}
