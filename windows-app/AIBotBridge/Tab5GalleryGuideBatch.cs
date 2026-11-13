using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Gallery
{
    private const int GuideBatchHeader = 140;
    private const int GuideBatchEntry = 40;
    private const int GuideBatchLimit = 3 * 1024 * 1024;
    private sealed record GuideBatch(string Sha, byte[] Bytes, int Decoded, int Changed);
    private sealed record GuideSummary(string Revision, string From, string Sha, byte[] Bytes, int Received);
    private readonly Dictionary<string, GuideBatch> _guideBatches = [];
    private readonly Dictionary<string, GuideSummary> _guideSummaries = [];

    private (int Status, object Body) HandleGuideBatch(JsonElement request, bool binary, bool bulk, bool stream, ReadOnlyMemory<byte> upload)
    {
        string Text(string name) => request.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        int Number(string name) => request.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : -1;
        (int, object) Invalid() => (409, new { error = "gallery_delta_unavailable" });
        string category = Text("category"), revision = Text("revision"), from = Text("from");
        if (!GalleryPack.Category(category) || revision.Length != 64 || revision != ContentDigest(category)) return Invalid();
        if (Text("op") == "guideBatchRead")
        {
            int offset = Number("offset"), count = Number("count");
            if (!_guideBatches.TryGetValue(category, out var batch) || batch.Sha != Text("sha256")) return Invalid();
            if (!binary || offset < 0 || count < 1 || count > (stream ? Tab5GalleryStream.MaximumImage : bulk ? Tab5RpcBinary.BulkData : 8192) ||
                offset > batch.Bytes.Length - count) return (400, new { error = "invalid_gallery_range" });
            return (200, new Tab5RpcDataBody(new { offset, count }, batch.Bytes.AsMemory(offset, count)));
        }
        string directory = CategoryDirectory(category);
        if (from.Length != 64 || from == revision || SyncCatalog(category, directory) is not { } catalog) return Invalid();
        if (Text("op") == "guideSummary")
        {
            string sha = Text("sha256");
            int offset = Number("offset"), total = catalog.Works.Length * 32;
            if (!bulk || Text("imageRevision") != catalog.Images || sha.Length != 64 ||
                upload.IsEmpty || offset < 0 || offset > total - upload.Length) return Invalid();
            if (!_guideSummaries.TryGetValue(category, out var summary) ||
                (summary.Revision, summary.From, summary.Sha) != (revision, from, sha))
            {
                if (offset != 0) return Invalid();
                summary = new(revision, from, sha, new byte[total], 0);
            }
            if (offset > summary.Received) return Invalid();
            if (offset < summary.Received)
            {
                if (offset + upload.Length > summary.Received ||
                    !upload.Span.SequenceEqual(summary.Bytes.AsSpan(offset, upload.Length))) return Invalid();
            }
            else
            {
                upload.Span.CopyTo(summary.Bytes.AsSpan(offset));
                summary = summary with { Received = offset + upload.Length };
            }
            _guideSummaries[category] = summary;
            return (200, new { offset, count = upload.Length });
        }
        try
        {
            byte[]? saved = null;
            string? backup = null;
            if (Text("summarySha").Length != 0)
            {
                if (!_guideSummaries.TryGetValue(category, out var summary) || summary.Revision != revision || summary.From != from ||
                    summary.Received != catalog.Works.Length * 32 || summary.Sha != Text("summarySha") ||
                    !Convert.ToHexString(SHA256.HashData(summary.Bytes)).Equals(summary.Sha, StringComparison.OrdinalIgnoreCase)) return Invalid();
                saved = summary.Bytes;
            }
            else if (!DeltaSources(category, catalog.Images).TryGetValue(from, out backup)) return Invalid();
            var batch = BuildGuideBatch(directory, catalog.Images, catalog.Works, from, backup, saved);
            _guideBatches[category] = batch;
            return (200, new { revision, from, works = catalog.Works.Length, batch.Changed, batch.Sha, bytes = batch.Bytes.Length, batch.Decoded });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Invalid();
        }
    }

    private GuideBatch BuildGuideBatch(string directory, string images, Artwork[] works, string from, string? backup, byte[]? previous)
    {
        byte[] index = new byte[GuideBatchHeader + works.Length * GuideBatchEntry];
        "AIBGB002"u8.CopyTo(index);
        BinaryPrimitives.WriteInt32LittleEndian(index.AsSpan(8), works.Length);
        Encoding.ASCII.GetBytes(from, index.AsSpan(12, 64));
        Encoding.ASCII.GetBytes(images, index.AsSpan(76, 64));
        using var records = new MemoryStream();
        int changed = 0;
        for (int i = 0; i < works.Length; i++)
        {
            var work = works[i];
            var guide = Guide(directory, work, 0);
            var projected = Tab5ArtGuides.ForReader(guide, 3);
            var pages = projected is { TranscriptionPages: > 1 } ? Tab5ArtGuides.TextPages(guide!.Transcription) : null;
            string id = work.WorkId ?? work.Source;
            JsonElement? payload = null;
            if (projected is not null)
            {
                var node = JsonSerializer.SerializeToNode(projected, Tab5ArtGuides.WireOptions)!.AsObject();
                if (pages is not null) node["transcriptionPageTexts"] = JsonSerializer.SerializeToNode(pages, Tab5ArtGuides.WireOptions);
                payload = JsonSerializer.SerializeToElement(node, Tab5ArtGuides.WireOptions);
            }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { workId = id, guide = payload }, Tab5ArtGuides.WireOptions);
            var entry = index.AsSpan(GuideBatchHeader + i * GuideBatchEntry, GuideBatchEntry);
            SHA256.HashData(bytes, entry[..32]);
            bool unchanged = previous is not null ? entry[..32].SequenceEqual(previous.AsSpan(i * 32, 32)) :
                guide == Guide(backup!, work, 0);
            if (unchanged) continue;
            if (bytes.Length > 64 * 1024 || index.Length + records.Length + bytes.Length > GuideBatchLimit)
                throw new InvalidDataException("Guide batch exceeds device capacity.");
            BinaryPrimitives.WriteInt32LittleEndian(entry[32..], checked(index.Length + (int)records.Length));
            BinaryPrimitives.WriteInt32LittleEndian(entry[36..], bytes.Length);
            records.Write(bytes);
            changed++;
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(index);
            records.Position = 0;
            records.CopyTo(zlib);
        }
        byte[] data = compressed.ToArray();
        if (data.Length > Tab5GalleryStream.MaximumImage) throw new InvalidDataException("Compressed guide batch exceeds transport capacity.");
        return new(Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(), data, checked(index.Length + (int)records.Length), changed);
    }
}
