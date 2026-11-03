using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class EspUsbResourceCache
{
    private readonly Dictionary<BinaryResourceKind,string> _sent=[];
    internal static string Fingerprint(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    internal bool Contains(BinaryResourceKind kind,string fingerprint)=>_sent.TryGetValue(kind,out var prior)&&prior==fingerprint;
    internal void Confirm(BinaryResourceKind kind,string fingerprint)=>_sent[kind]=fingerprint;
    internal void Forget(BinaryResourceKind kind)=>_sent.Remove(kind);
    internal void Clear()=>_sent.Clear();
}

// Loopback diagnostics only. No payload, text, keys, hardware identity or hashes.
// A separate short lock keeps counters readable during a long serial transfer.
internal sealed class EspUsbDiagnostics
{
    private readonly object _gate=new();
    private readonly Queue<object> _events=[];
    private readonly EspUsbCrashMarkers _crash=new();
    private long _attempts,_connections,_disconnects,_resourceStarts,_resourceOk,_resourceFailed,_resourceSkipped,
        _wireBytes,_retries,_statusWrites,_brightnessRequests,_brightnessWrites,_brightnessRepeated,
        _receivedLines,_receivedCharacters,_nonProtocolLines,_invalidJsonLines,_otherReplyLines,_pongLines,_infoLines,_ackLines,
        _drainedCharacters,_readTimeouts,_pingChecks,_pingOk,_pingTimeouts,_infoRequests,_infoOk,_infoTimeouts,_infoFailures,_infoPending,
        _infoWrongRequest,_infoWrongVersion;
    private int _pendingInputAtTimeout;
    private long _lastInfoQueueMs,_lastInfoDurationMs;
    private DateTimeOffset? _lastReceivedAt,_lastConfirmedReplyAt;
    private bool _connected,_transferring;
    private int? _previousBrightness;
    private string? _lastError;
    private void Event(string stage,string? resource=null,int bytes=0,long durationMs=0){
        _events.Enqueue(new{at=DateTimeOffset.UtcNow,session=_connections,stage,resource,bytes,durationMs});
        while(_events.Count>40)_events.Dequeue();
    }
    internal void Attempt(){lock(_gate){_attempts++;}}
    internal void Connected(){lock(_gate){_connections++;_connected=true;_previousBrightness=null;_lastError=null;Event("connected");}}
    internal void Error(Exception exception){lock(_gate){_lastError=exception.GetType().Name;Event("connection-error");}}
    internal void Disconnected(){lock(_gate){if(_connected){_disconnects++;Event("disconnected");}_connected=false;_previousBrightness=null;}}
    internal void ResourceSkipped(){lock(_gate){_resourceSkipped++;}}
    internal void ResourceStarted(BinaryResourceKind kind,int bytes){lock(_gate){_resourceStarts++;_transferring=true;Event("resource-start",kind.ToString(),bytes);}}
    internal void ChunkWritten(int bytes,bool retry){lock(_gate){_wireBytes+=bytes;if(retry)_retries++;}}
    internal void ResourceFinished(BinaryResourceKind kind,int bytes,bool success,long durationMs){lock(_gate){
        if(success)_resourceOk++;else _resourceFailed++;_transferring=false;
        Event(success?"resource-acknowledged":"resource-failed",kind.ToString(),bytes,durationMs);
    }}
    internal void StatusWritten(){lock(_gate){_statusWrites++;}}
    internal void Received(string line,bool drained=false){lock(_gate){
        _receivedCharacters+=line.Length;if(drained)_drainedCharacters+=line.Length;
        if(line.Length>0)_lastReceivedAt=DateTimeOffset.UtcNow;
        // ReadExisting may return only part of a line. Count its bytes without
        // mistaking that fragment for a malformed device response.
        if(drained){foreach(var part in line.Split('\n'))_crash.Observe(part.Trim());return;}
        _receivedLines++;
        if(!line.StartsWith("@AIBOT ",StringComparison.Ordinal)){_nonProtocolLines++;_crash.Observe(line);return;}
        try {
            using var document=JsonDocument.Parse(line[7..]);var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("type",out var type)||type.ValueKind!=JsonValueKind.String){_invalidJsonLines++;return;}
            switch(type.GetString()){
                case "pong":_pongLines++;break;
                case "device_info":_infoLines++;break;
                case "resource_ack":_ackLines++;break;
                default:_otherReplyLines++;break;
            }
        }catch(JsonException){_invalidJsonLines++;}
    }}
    internal void ReadTimedOut(int pendingInput){lock(_gate){_readTimeouts++;_pendingInputAtTimeout=pendingInput;}}
    internal void ReplyConfirmed(){lock(_gate){_lastConfirmedReplyAt=DateTimeOffset.UtcNow;}}
    internal void PingFinished(bool success){lock(_gate){
        _pingChecks++;if(success){_pingOk++;_lastConfirmedReplyAt=DateTimeOffset.UtcNow;}else{_pingTimeouts++;Event("ping-timeout");}
    }}
    internal void InfoQueued(){lock(_gate){_infoRequests++;_infoPending++;}}
    internal void InfoStarted(long queueMs){lock(_gate){_lastInfoQueueMs=queueMs;}}
    internal void InfoReplyRejected(UsbReplyRejection reason){lock(_gate){
        if(reason==UsbReplyRejection.WrongRequestId)_infoWrongRequest++;
        else if(reason==UsbReplyRejection.WrongVersion)_infoWrongVersion++;
    }}
    internal void InfoFinished(bool success,bool timedOut,long durationMs){lock(_gate){
        _infoPending--;_lastInfoDurationMs=durationMs;
        if(success){_infoOk++;_lastConfirmedReplyAt=DateTimeOffset.UtcNow;}
        else {if(timedOut)_infoTimeouts++;else _infoFailures++;Event(timedOut?"device-info-timeout":"device-info-failed",durationMs:durationMs);}
    }}
    internal void Brightness(int level,bool sent){lock(_gate){
        _brightnessRequests++;if(_previousBrightness==level)_brightnessRepeated++;
        _previousBrightness=level;if(sent)_brightnessWrites++;Event(sent?"brightness-written":"brightness-not-written");
    }}
    internal object Snapshot(bool enabled,bool active,string? firmware){lock(_gate)return new{
        connected=_connected,usbDataEnabled=enabled,usbDataActive=active,firmware,
        connectionAttempts=_attempts,connections=_connections,disconnects=_disconnects,
        resourceTransfers=_resourceStarts,resourceAcknowledged=_resourceOk,resourceFailed=_resourceFailed,
        resourceCachedSkips=_resourceSkipped,resourceWireBytes=_wireBytes,resourceRetries=_retries,
        resourceInProgress=_transferring,statusWrites=_statusWrites,
        brightnessRequests=_brightnessRequests,brightnessWrites=_brightnessWrites,repeatedBrightnessRequests=_brightnessRepeated,
        receivedLines=_receivedLines,receivedCharacters=_receivedCharacters,nonProtocolLines=_nonProtocolLines,invalidJsonLines=_invalidJsonLines,
        pongLines=_pongLines,deviceInfoLines=_infoLines,resourceAckLines=_ackLines,otherReplyLines=_otherReplyLines,
        drainedCharacters=_drainedCharacters,readTimeouts=_readTimeouts,pendingInputAtLastTimeout=_pendingInputAtTimeout,
        pingChecks=_pingChecks,pingAcknowledged=_pingOk,pingTimeouts=_pingTimeouts,
        deviceInfoRequests=_infoRequests,deviceInfoMatchedReplies=_infoOk,deviceInfoTimeouts=_infoTimeouts,deviceInfoFailures=_infoFailures,
        deviceInfoPending=_infoPending,lastDeviceInfoQueueMs=_lastInfoQueueMs,lastDeviceInfoDurationMs=_lastInfoDurationMs,
        deviceInfoWrongRequestIds=_infoWrongRequest,deviceInfoWrongVersions=_infoWrongVersion,
        lastReceivedAt=_lastReceivedAt,lastConfirmedReplyAt=_lastConfirmedReplyAt,
        crash=_crash.Snapshot(),
        lastError=_lastError,events=_events.ToArray()
    };}
}
