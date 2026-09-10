using System.Text.Json;

namespace AIBotBridge;

internal static class LanResourceCatalog
{
    internal static (string Status, byte[] Body) Respond(string path, IReadOnlyList<ResourcePayload> resources)
    {
        if (path == "/resources")
            return ("200 OK", JsonSerializer.SerializeToUtf8Bytes(new { version = 1, resources = resources.Select(r => new
            { kind = (int)r.Kind, crc = BinaryResourceProtocol.Crc32(r.Data), length = r.Data.Length }) }, JsonDefaults.Options));
        var parts = path.Split('/');
        if (parts.Length != 4 || parts[1] != "resources" || !byte.TryParse(parts[2], out var kind) || !uint.TryParse(parts[3], out var crc))
            return ("404 Not Found", "{}"u8.ToArray());
        var resource = resources.FirstOrDefault(r => (byte)r.Kind == kind);
        if (resource is null) return ("404 Not Found", "{}"u8.ToArray());
        if (BinaryResourceProtocol.Crc32(resource.Data) != crc) return ("409 Conflict", "{}"u8.ToArray());
        return ("200 OK", resource.Data);
    }
}
