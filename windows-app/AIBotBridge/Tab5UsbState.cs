using System.Text.Json;

namespace AIBotBridge;

// One baseline per live CDC connection, advanced only after its exact ACK.
// State freshness and unread/settings updates retain the normal publication
// cadence; unchanged catalogs no longer occupy the gallery's serial lane.
internal sealed class Tab5UsbState
{
    private byte[]? _confirmed;
    private byte[]? _confirmedWire;
    internal void Reset(){_confirmed=null;_confirmedWire=null;}
    internal void Confirm(byte[] frame,byte[]? wire=null){_confirmed=frame;_confirmedWire=wire;}
    internal static bool SupportsDelta(JsonElement hello,string device)=>
        Tab5UsbStatus.SupportsPacked(hello,device)&&hello.TryGetProperty("usbDelta",out var delta)&&
        delta.ValueKind==JsonValueKind.Number&&delta.TryGetInt32(out int v)&&v==1;
    internal byte[] Wire(byte[] frame,bool packed,bool delta,int maximum=Tab5Protocol.MaximumFrame)
    {
        if(!packed)return frame;
        // An unchanged publication replays its exact ACKed delta. Its sequence
        // cannot renew task freshness and need not resend the complete catalog.
        if(delta&&ReferenceEquals(_confirmed,frame)&&_confirmedWire is {} replay)return replay;
        byte[] full=Tab5Service.PackTelemetry(frame,System.IO.Compression.CompressionLevel.Optimal);
        if(delta&&_confirmed is {} baseline&&!ReferenceEquals(baseline,frame)&&Tab5TelemetryDelta.Create(baseline,frame) is {} change&&change.Length<=maximum-28) {
            byte[] wire=Tab5Service.PackTelemetry(change,System.IO.Compression.CompressionLevel.Optimal);
            if(wire.Length<full.Length)return wire;
        }
        return full;
    }
}
