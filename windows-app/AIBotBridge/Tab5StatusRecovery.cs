using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;

internal static class Tab5StatusRecovery
{
    // Reserve space for clocks, connection health and stale flags even when
    // an earlier valid frame was already close to the transport ceiling.
    internal const int Budget=Tab5Protocol.MaximumFrame-28-512;
    private static readonly string[] LiveFields=["time","epochUtc","epochMilliseconds","utcOffsetSeconds","capturedAt","pcInput","codex","claude"];
    internal static byte[] Build(byte[] incoming,byte[]? previous,out string[] deferred,int maximum=Tab5Protocol.MaximumFrame)
    {
        var current=JsonNode.Parse(incoming)!.AsObject();
        var data=current["data"]!.AsObject();
        var result=current.DeepClone().AsObject();
        var cached=previous is null?null:JsonNode.Parse(previous)!["data"]!.AsObject();
        var kept=cached?.DeepClone().AsObject()??new JsonObject();result["data"]=kept;
        // Cached business values retain their original sample timestamps. Mark
        // supported cache fields stale; never replay an old pending command.
        foreach(string field in new[]{"weather","stocks","music"})
            if(kept[field] is JsonObject value)value["stale"]=true;
        if(kept["quotas"] is JsonObject quotas)
            foreach(var quota in quotas)if(quota.Value is JsonObject value)value["stale"]=true;
        if(kept["quotaDetails"] is JsonArray details)
            foreach(var detail in details)if(detail is JsonObject value)value["stale"]=true;
        kept["resource"]=null;kept["ota"]=null;kept["connectionHealth"]=null;
        foreach(string field in LiveFields)kept[field]=data[field]?.DeepClone();
        byte[] Encode()=>JsonSerializer.SerializeToUtf8Bytes(result,Tab5Protocol.StatusJson);
        // An old frame from a previous software version might lack the reserve.
        // Bootstrap core status instead of allowing it to wedge recovery too.
        if(Encode().Length>maximum-28) {
            kept=new JsonObject();result["data"]=kept;
            foreach(string field in LiveFields)kept[field]=data[field]?.DeepClone();
        }
        var skipped=new List<string>();
        // Current commands, task selection and quota values take precedence;
        // every section is retried on the next normal publication automatically.
        foreach(string field in new[]{"connectionHealth","ota","codexTasks"}.Concat(data.Select(p=>p.Key)).Distinct()) {
            if(LiveFields.Contains(field)||field=="resource")continue;
            bool existed=kept.ContainsKey(field);
            var old=kept[field]?.DeepClone();kept[field]=data[field]?.DeepClone();
            if(Encode().Length>Tab5StatusCapacity.Budget(maximum)){if(existed)kept[field]=old;else kept.Remove(field);skipped.Add(field);}
        }
        deferred=skipped.ToArray();return Encode();
    }
}
