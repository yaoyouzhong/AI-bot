using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;

internal static class DeviceStatusFrame
{
    internal const int MaximumLineBytes = 6144;

    internal static JsonObject Create(StatusSnapshot snapshot)
    {
        var data = JsonSerializer.SerializeToNode(snapshot, JsonDefaults.Options)!.AsObject();
        // Keep LAN/mirror snapshots complete. USB omits only non-rendered metadata;
        // full Chinese stock/music labels are carried in binary resources.
        data.Remove("capturedAt"); data.Remove("time"); data.Remove("musicPlaying");
        // Fast metrics frames carry the incremental tail; the heartbeat remains small.
        if(data["systemMetrics"] is JsonObject metrics) metrics.Remove("samples");
        if (data["domesticActivity"] is JsonObject activity) activity.Remove("providers");
        Prune(data);
        if (data["stocks"]?["quotes"] is JsonArray quotes)
        {
            while (quotes.Count > 20) quotes.RemoveAt(quotes.Count - 1);
            foreach (var quote in quotes.OfType<JsonObject>()) { quote.Remove("name"); quote.Remove("symbol"); }
        }
        if (data["music"] is JsonObject music) music.Remove("album");
        var frame = new JsonObject { ["version"] = 1, ["type"] = "status", ["data"] = data };
        if (Encoding.UTF8.GetByteCount("@AIBOT " + frame.ToJsonString(JsonDefaults.Options)) > MaximumLineBytes)
            throw new InvalidOperationException("Device status exceeds the 6144-byte wire limit; no truncated JSON was sent.");
        return frame;
    }

    private static void Prune(JsonNode? node)
    {
        if (node is JsonArray array) { foreach (var item in array) Prune(item); return; }
        if (node is not JsonObject obj) return;
        obj.Remove("source"); obj.Remove("provider");
        // System updatedAt is deliberately retained for duplicate graph-sample suppression.
        if (!obj.ContainsKey("cpuPercent")) obj.Remove("updatedAt");
        foreach (var key in obj.Select(pair => pair.Key).ToArray())
        {
            int limit = key switch { "plan" => 24, "city" => 32, "condition" => 16,
                "title" or "artist" => 48, "code" or "price" or "changePercent" => 20, _ => 0 };
            if (limit > 0 && obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                obj[key] = string.Concat(text.EnumerateRunes().Take(limit).Select(rune => rune.ToString()));
            else Prune(obj[key]);
        }
    }
}
