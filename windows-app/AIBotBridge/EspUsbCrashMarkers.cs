using System.Globalization;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// Recognize the ESP8266 Arduino postmortem headers only. Never retain stack
// memory words, arbitrary serial text, payloads or registers containing data.
internal sealed class EspUsbCrashMarkers
{
    private static readonly Regex ExceptionLine=new(@"^Exception \(([0-9]{1,3})\):$",RegexOptions.CultureInvariant);
    private static readonly Regex ProgramCounter=new(@"^epc1=0x([0-9a-fA-F]{8})(?: |$)",RegexOptions.CultureInvariant);
    private long _watchdog,_smash,_exceptions,_stackDumps;
    private int? _exception;
    private string? _epc1,_context,_reason;
    private DateTimeOffset? _lastCrashAt;
    internal void Observe(string line)
    {
        if(line.Length>256)return;
        if(line=="Soft WDT reset"){_watchdog++;Begin("soft-watchdog");return;}
        if(line=="Stack smashing detected."){_smash++;Begin("stack-smash");return;}
        var match=ExceptionLine.Match(line);
        if(match.Success){_exceptions++;_exception=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);_lastCrashAt=DateTimeOffset.UtcNow;return;}
        match=ProgramCounter.Match(line);
        if(match.Success){
            uint address=uint.Parse(match.Groups[1].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            _epc1=address is >=0x40000000 and <0x40300000?address.ToString("X8",CultureInfo.InvariantCulture):"outside-code";return;
        }
        if(line==">>>stack>>>"){_stackDumps++;_lastCrashAt=DateTimeOffset.UtcNow;return;}
        if(line is "ctx: cont" or "ctx: sys" or "ctx: bearssl")_context=line[5..];
    }
    private void Begin(string reason){_reason=reason;_epc1=null;_context=null;_exception=null;_lastCrashAt=DateTimeOffset.UtcNow;}
    internal object Snapshot()=>new{watchdogMarkers=_watchdog,stackSmashMarkers=_smash,exceptionMarkers=_exceptions,
        stackDumpMarkers=_stackDumps,lastReason=_reason,lastException=_exception,lastEpc1=_epc1,lastContext=_context,lastCrashAt=_lastCrashAt};
}
