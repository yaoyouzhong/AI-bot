using System.Text.Json;

namespace AIBotBridge;

internal sealed record NeteaseTrackMetadata(string Id, string Name, string Artist, string Album, string[] Aliases)
{
    internal string DisplayTitle => Aliases.Length == 0 ? Name : Name + "\n" + string.Join(" / ", Aliases);
    internal string CompactTitle => Aliases.FirstOrDefault() ?? Name;

    internal bool Matches(string title, string artist, string album) =>
        (Name == title.Trim() || Aliases.Contains(title.Trim(), StringComparer.Ordinal)) &&
        ArtistNames(Artist).SequenceEqual(ArtistNames(artist), StringComparer.OrdinalIgnoreCase) &&
        (string.IsNullOrWhiteSpace(album) || Album == album.Trim());

    private static string[] ArtistNames(string text) => text.Split(['/', ',', ';', '、'],
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static NeteaseTrackMetadata? Read(string id)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetEase", "CloudMusic", "WebData", "file", "playingList");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length is <= 0 or > 8 * 1024 * 1024) return null;
        // Bound concurrent rewrites as well as the initial length; never read account files.
        var bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        return Parse(bytes, id);
    }

    internal static NeteaseTrackMetadata? Parse(ReadOnlyMemory<byte> json, string id)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (!doc.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array) return null;
        foreach (var entry in list.EnumerateArray().Take(8192))
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var value) || value.ToString() != id ||
                !entry.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object) continue;
            var name = Text(track, "name");
            if (name.Length == 0 || !track.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array) return null;
            var artist = string.Join(" / ", artists.EnumerateArray().Take(32).Select(a => Text(a, "name")).Where(s => s.Length > 0));
            var album = track.TryGetProperty("album", out var a) ? Text(a, "name") : "";
            var aliases = new[] { "transNames", "tns", "alias" }.SelectMany(key =>
                track.TryGetProperty(key, out var names) && names.ValueKind == JsonValueKind.Array
                    ? names.EnumerateArray().Take(4).Where(n => n.ValueKind == JsonValueKind.String).Select(n => n.GetString()!.Trim())
                    : Enumerable.Empty<string>()).Where(s => s.Length is > 0 and <= 256 && s != name).Distinct().Take(2).ToArray();
            return artist.Length == 0 ? null : new(id, name, artist, album, aliases);
        }
        return null;
    }

    private static string Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: <= 512 } text ? text.Trim() : "";
}
