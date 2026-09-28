using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5InstallSegment(string Name, int Offset, int Size, string Sha256);
internal sealed record Tab5InstallManifest(int Format, string Kind, string Chip, string Project,
    string Version, int FlashBytes, string ImageSha256, Tab5InstallSegment[] Segments);

// Only the reviewed ESP32-P4 layout is accepted. A raw OTA BIN is never accepted.
internal sealed record Tab5InstallPackage(Tab5InstallManifest Manifest, string ImagePath, string Directory)
{
    internal const int FlashSize = 0x1000000;
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly (string Name, int Offset, int Maximum)[] Layout = [
        ("bootloader.bin", 0x2000, 0x6000), ("partition-table.bin", 0x8000, 0x1000),
        ("otadata.bin", 0x10000, 0x2000), ("application.bin", 0x20000, 0x6e0000)];

    internal static Tab5InstallPackage Load(string zipPath, string directory)
    {
        if (new FileInfo(zipPath).Length > 32 * 1024 * 1024) throw new IOException("首次安装包过大。");
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > 2048 || zip.Entries.GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1) ||
            zip.Entries.Any(e => e.FullName.Contains('\\') || e.FullName.StartsWith('/') || e.FullName.Contains(':') || e.FullName.Split('/').Contains("..")))
            throw new IOException("安装包文件名无效或重复。");
        var metadata = zip.GetEntry("manifest.json") ?? throw new IOException("请选择 TAB5 首次安装 ZIP 包，不能选择 OTA 固件。");
        if (metadata.Length > 16384) throw new IOException("安装清单过大。");
        using var input = metadata.Open();
        var json = new byte[(int)metadata.Length]; input.ReadExactly(json);
        if (input.ReadByte() != -1) throw new IOException("安装清单长度不符。");
        var manifest = JsonSerializer.Deserialize<Tab5InstallManifest>(json, Json) ?? throw new IOException("安装清单为空。");
        var entry = zip.GetEntry("factory.bin");
        if (entry is null || entry.Length != FlashSize) throw new IOException("完整安装镜像必须为 16 MiB。");
        using var imageStream = entry.Open();
        var image = new byte[FlashSize]; imageStream.ReadExactly(image);
        if (imageStream.ReadByte() != -1) throw new IOException("镜像大小不符。");
        Validate(manifest, image);
        System.IO.Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "factory.bin");
        using (var output = new FileStream(target, FileMode.CreateNew)) output.Write(image);
        foreach (var segment in manifest.Segments)
            using (var output = new FileStream(Path.Combine(directory, segment.Name), FileMode.CreateNew))
                output.Write(image, segment.Offset, segment.Size);
        return new(manifest, target, directory);
    }

    internal void VerifyUnchanged()
    {
        if (!FirmwareFlasher.MatchesHash(ImagePath, Manifest.ImageSha256)) throw new IOException("安装镜像已变化，已停止。");
        foreach (var part in Manifest.Segments)
            if (!FirmwareFlasher.MatchesHash(Path.Combine(Directory, part.Name), part.Sha256)) throw new IOException("安装文件已变化，已停止。");
    }

    internal static string Hash(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    internal static void Validate(Tab5InstallManifest m, byte[] image)
    {
        if (m.Format != 1 || m.Kind != "aibot-tab5-first-install" || m.Chip != "esp32p4" || m.Project != "aibot_tab5" ||
            m.FlashBytes != FlashSize || image.Length != FlashSize || m.ImageSha256 != Hash(image) || m.Segments?.Length != Layout.Length)
            throw new IOException("安装包型号、版本或校验值不匹配。");
        int end = 0;
        for (int i = 0; i < Layout.Length; i++)
        {
            var expected = Layout[i]; var part = m.Segments[i];
            if (part is null || part.Name != expected.Name || part.Offset != expected.Offset || part.Size < 1 || part.Size > expected.Maximum)
                throw new IOException("安装包分区或地址不符合 TAB5 布局。");
            Erased(image.AsSpan(end, part.Offset - end));
            var bytes = image.AsSpan(part.Offset, part.Size);
            if (Hash(bytes) != part.Sha256) throw new IOException("安装组件校验失败。");
            if (i is 0 or 3) ValidateEspImage(bytes);
            if (i == 1) ValidatePartitions(bytes);
            if (i == 2) { if (part.Size != 0x2000) throw new IOException("OTA 初始化长度错误。"); Erased(bytes); }
            if (i == 3 && (bytes.Length < 288 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]) < 256 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[32..]) != 0xabcd5432 ||
                ReadText(bytes.Slice(80, 32)) != "aibot_tab5" || ReadText(bytes.Slice(48, 32)) != m.Version))
                throw new IOException("应用项目名或版本不匹配。");
            end = part.Offset + part.Size;
        }
        Erased(image.AsSpan(end)); // NVS, PHY, spare OTA and assets must contain no device data.
    }

    private static void Erased(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOfAnyExcept((byte)0xff) >= 0) throw new IOException("安装包含未声明的数据或设备配置，拒绝安装。");
    }
    private static string ReadText(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        if (end <= 0 || bytes[..end].ContainsAnyExceptInRange((byte)32, (byte)126)) throw new IOException("镜像标识无效。");
        return Encoding.ASCII.GetString(bytes[..end]);
    }
    internal static void ValidateEspImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 48 || bytes[0] != 0xe9 || bytes[1] is < 1 or > 16 || BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]) != 18)
            throw new IOException("镜像不是 ESP32-P4 固件。");
        int position = 24; byte checksum = 0xef;
        for (int i = 0; i < bytes[1]; i++)
        {
            if (position > bytes.Length - 8) throw new IOException("镜像段不完整。");
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(position + 4)..]); position += 8;
            if (size > bytes.Length - position) throw new IOException("镜像段越界。");
            foreach (byte b in bytes.Slice(position, (int)size)) checksum ^= b;
            position += (int)size;
        }
        int end = (position | 15) + 1;
        if (end >= bytes.Length || bytes[end - 1] != checksum || bytes[23] != 1 || bytes.Length != end + 32 ||
            !SHA256.HashData(bytes[..end]).AsSpan().SequenceEqual(bytes[end..]))
            throw new IOException("镜像校验失败，或不是受支持的未签名固件。");
    }
    private static void ValidatePartitions(ReadOnlySpan<byte> bytes)
    {
        (string, byte, byte, uint, uint)[] expected = [
            ("nvs",1,2,0x9000,0x6000), ("phy_init",1,1,0xf000,0x1000), ("otadata",1,0,0x10000,0x2000),
            ("ota_0",0,0x10,0x20000,0x6e0000), ("ota_1",0,0x11,0x700000,0x6e0000), ("assets",1,0x40,0xe00000,0x200000)];
        if (bytes.Length < 224) throw new IOException("分区表不完整。");
        for (int i = 0; i < expected.Length; i++)
        {
            var row = bytes.Slice(i * 32, 32); var p = expected[i];
            if (BinaryPrimitives.ReadUInt16LittleEndian(row) != 0x50aa || row[2] != p.Item2 || row[3] != p.Item3 ||
                BinaryPrimitives.ReadUInt32LittleEndian(row[4..]) != p.Item4 || BinaryPrimitives.ReadUInt32LittleEndian(row[8..]) != p.Item5 ||
                ReadText(row.Slice(12,16)) != p.Item1 || BinaryPrimitives.ReadUInt32LittleEndian(row[28..]) != 0)
                throw new IOException("分区表不符合 TAB5 首次安装布局。");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[192..]) != 0xebeb || !MD5.HashData(bytes[..192]).AsSpan().SequenceEqual(bytes.Slice(208,16)))
            throw new IOException("分区表校验失败。");
        Erased(bytes.Slice(194,14));
        Erased(bytes[224..]);
    }
}
