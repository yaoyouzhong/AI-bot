using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIBotBridge;

internal sealed record UsbDeviceInfo(
    string Device, int Version, string Ip, string Mode, int Brightness,
    [property: JsonPropertyName("usb_active")] bool UsbActive,
    [property: JsonPropertyName("bridge_online")] bool BridgeOnline,
    [property: JsonPropertyName("uptime_ms")] uint UptimeMs,
    [property: JsonPropertyName("usb_status_count")] uint UsbStatusCount,
    [property: JsonPropertyName("lan_status_count")] uint LanStatusCount,
    [property: JsonPropertyName("page_data")] JsonElement? PageData = null);

internal static class UsbDeviceProtocol
{
    internal static JsonElement? ParseReply(string line, string type, uint requestId)
    {
        const string prefix = "@AIBOT ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal)) return null;
        try
        {
            using var document = JsonDocument.Parse(line[prefix.Length..]);
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != 1 ||
                root.GetProperty("type").GetString() != type ||
                root.GetProperty("request_id").GetUInt32() != requestId || requestId == 0)
                return null;
            return root.Clone();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or
                                   KeyNotFoundException or FormatException) { return null; }
    }

    internal static UsbDeviceInfo ReadInfo(JsonElement reply)
    {
        try { return ReadInfoCore(reply); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or
                                   KeyNotFoundException or FormatException)
        { throw new IOException("Invalid USB device information; check firmware version.", ex); }
    }

    private static UsbDeviceInfo ReadInfoCore(JsonElement reply)
    {
        var data = reply.GetProperty("data");
        if (!reply.GetProperty("ok").GetBoolean()) throw new IOException("Device rejected info request.");
        // Counters are required: old firmware must not falsely pass the fallback test.
        _ = data.GetProperty("uptime_ms").GetUInt32();
        _ = data.GetProperty("usb_status_count").GetUInt32();
        _ = data.GetProperty("lan_status_count").GetUInt32();
        _ = data.GetProperty("usb_active").GetBoolean();
        _ = data.GetProperty("bridge_online").GetBoolean();
        _ = data.GetProperty("brightness").GetInt32();
        var info = data.Deserialize<UsbDeviceInfo>(JsonDefaults.Options);
        if (info is null || info.Device != "AI-bot" || info.Version != 1 ||
            info.Brightness is < 0 or > 100 || info.Ip is null ||
            !DisplayModes.IsValid(info.Mode))
            throw new IOException("Invalid device information.");
        return info;
    }
}
