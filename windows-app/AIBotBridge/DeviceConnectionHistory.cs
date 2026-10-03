using System.IO.Compression;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record ConnectionObservation(DateTimeOffset? LastCommunication,DateTimeOffset? LastDisconnected,string Reason,int Recoveries,int ManualRestarts);

// Sampled by the bridge, independently of whether a diagnostics window is open.
internal sealed class DeviceConnectionHistory
{
    private sealed class Entry {
        internal bool Online,SeenOnline;
        internal DateTimeOffset? LastCommunication,LastDisconnected;
        internal string Reason="尚无中断记录";
        internal int Recoveries,ManualRestarts;
    }
    private readonly Dictionary<string,Entry> _entries=[];
    internal ConnectionObservation Observe(string id,bool enabled,bool online,long lastCommunicationTicks,string reason,DateTimeOffset now,long ticks) {
        if(!_entries.TryGetValue(id,out var entry))_entries[id]=entry=new();
        if(enabled&&lastCommunicationTicks>0) {
            var at=now.AddMilliseconds(-Math.Max(0,ticks-lastCommunicationTicks));
            if(entry.LastCommunication is null||at>entry.LastCommunication)entry.LastCommunication=at;
        }
        if(enabled&&entry.Online&&!online){entry.LastDisconnected=now;entry.Reason=reason;}
        if(enabled&&online&&!entry.Online){if(entry.SeenOnline)entry.Recoveries++;entry.SeenOnline=true;}
        entry.Online=enabled&&online;
        return new(entry.LastCommunication,entry.LastDisconnected,entry.Reason,entry.Recoveries,entry.ManualRestarts);
    }
    internal void Restart(string id) {
        if(!_entries.TryGetValue(id,out var entry))_entries[id]=entry=new();
        entry.ManualRestarts++;entry.Online=false;
    }
    // Explicit allowlist: never serialize DeviceView, pairing, exceptions or raw logs.
    internal static void Export(string path,IEnumerable<(HardwareKind Kind,ConnectionObservation Health,bool Enabled,bool Online)> devices) {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        using var zip=new ZipArchive(stream,ZipArchiveMode.Create);
        using var writer=new StreamWriter(zip.CreateEntry("connection-diagnostics.json").Open());
        writer.Write(JsonSerializer.Serialize(new {schemaVersion=1,bridgeVersion=Application.ProductVersion,
            capturedAt=DateTimeOffset.UtcNow,scope="Current bridge session; ESP8266 USB confirms host write, not device rendering",
            devices=devices.Select(d=>new {model=d.Kind.ToString(),d.Enabled,d.Online,
                d.Health.LastCommunication,d.Health.LastDisconnected,d.Health.Reason,d.Health.Recoveries,d.Health.ManualRestarts})},new JsonSerializerOptions{WriteIndented=true}));
    }
}
