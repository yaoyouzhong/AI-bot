using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;

// Deltas refer only to an authenticated, ACKed full-state sequence. Arrays are
// atomic; null is an ordinary value, and removal is explicit. A lost baseline
// causes a reconnect/full frame, never a heartbeat over stale task contents.
internal static class Tab5TelemetryDelta
{
    internal static byte[]? Create(byte[] baseline,byte[] current)
    {
        var before=JsonNode.Parse(baseline)!.AsObject();var after=JsonNode.Parse(current)!.AsObject();
        if(!JsonNode.DeepEquals(before["deviceId"],after["deviceId"])||!JsonNode.DeepEquals(before["session"],after["session"]))return null;
        // Firmware offers have a separate consumer: deliver their transitions
        // through the existing full-frame path, including offer removal.
        if(!JsonNode.DeepEquals(before["data"]?["ota"],after["data"]?["ota"]))return null;
        var changes=new JsonArray();
        if(!Diff(before["data"],after["data"],[],changes))return null;
        var frame=new JsonObject { ["version"]=1,["type"]="tab5_delta",["deviceId"]=after["deviceId"]!.DeepClone(),
            ["session"]=after["session"]!.DeepClone(),["sequence"]=after["sequence"]!.DeepClone(),
            ["baseSequence"]=before["sequence"]!.DeepClone(),["changes"]=changes };
        return JsonSerializer.SerializeToUtf8Bytes(frame,JsonDefaults.Options);
    }
    private static bool Diff(JsonNode? before,JsonNode? after,string[] path,JsonArray changes)
    {
        if(JsonNode.DeepEquals(before,after))return true;
        if(path.Length>16||changes.Count>=256)return false;
        if(before is JsonObject a&&after is JsonObject b) {
            foreach(var (key,value) in b) {
                var next=path.Append(key).ToArray();
                if(a.TryGetPropertyValue(key,out var old)) {if(!Diff(old,value,next,changes))return false;}
                else if(!Add(next,value,false,changes))return false;
            }
            foreach(var (key,_) in a)if(!b.ContainsKey(key)&&!Add(path.Append(key).ToArray(),null,true,changes))return false;
            return true;
        }
        return Add(path,after,false,changes);
    }
    private static bool Add(string[] path,JsonNode? value,bool remove,JsonArray changes)
    {
        if(path.Length is <1 or >16||changes.Count>=256)return false;
        var operation=new JsonObject { ["path"]=new JsonArray(path.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()) };
        if(remove)operation["remove"]=true;else operation["value"]=value?.DeepClone();
        changes.Add(operation);return true;
    }
}
