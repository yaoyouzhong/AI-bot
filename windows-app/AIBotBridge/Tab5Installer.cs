using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed record Tab5FlashBackup(int Format, string Chip, string Mac, int FlashBytes, string Sha256);

// All device commands retain ROM download mode. No reset, force or whole-chip
// erase command is issued. No mutation is retried automatically.
internal sealed class Tab5Installer(FirmwareFlasher.Runner run, Action<string> stage)
{
    internal string? BackupPath { get; private set; }
    internal bool BackupVerified { get; private set; }
    internal string? DeviceMac { get; private set; }
    private int _revision;
    private static string[] Command(string port, params string[] args) =>
        ["--chip", "esp32p4", "--port", port, "--baud", "460800", "--before", "no_reset", "--after", "no_reset", .. args];
    private async Task<string> IdentifyAsync(string port, CancellationToken token)
    {
        if (!Regex.IsMatch(port, @"^COM[1-9][0-9]*$")) throw new IOException("请选择有效的 TAB5 下载端口。");
        stage("正在核对芯片和容量…");
        string info = await run(Command(port, "flash_id"), token);
        if (!Regex.IsMatch(info, @"Chip is ESP32-P4\b") || FirmwareFlasher.ParseFlashSize(info) != Tab5InstallPackage.FlashSize)
            throw new IOException("只支持 16 MiB Flash 的 ESP32-P4 TAB5，未写入任何数据。");
        var rev = Regex.Match(info, @"Chip is ESP32-P4.*revision v(\d+)\.(\d+)");
        if (!rev.Success) throw new IOException("无法确认芯片修订版本。");
        _revision = checked(int.Parse(rev.Groups[1].Value) * 100 + int.Parse(rev.Groups[2].Value));
        string security = await run(Command(port, "get_security_info"), token);
        if (!Regex.IsMatch(security, @"(?m)^Secure Boot:\s*Disabled\s*$") || !Regex.IsMatch(security, @"(?m)^Flash Encryption:\s*Disabled\s*$"))
            throw new IOException("设备启用了安全启动/加密，或安全状态无法确认，已停止。");
        return DeviceMac = await ReadMacAsync(port, token);
    }
    private async Task<string> ReadMacAsync(string port, CancellationToken token)
    {
        string output = await run(Command(port, "read_mac"), token);
        var mac = Regex.Match(output, @"(?im)^MAC:\s*([0-9a-f]{2}(?::[0-9a-f]{2}){5})\s*$");
        if (!mac.Success) throw new IOException("无法确认设备硬件身份。");
        return mac.Groups[1].Value.ToLowerInvariant();
    }
    private async Task BackupAsync(string port, string mac, string directory, CancellationToken token)
    {
        BackupVerified = false;
        Directory.CreateDirectory(directory);
        BackupPath = Path.Combine(directory, $"tab5-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bin");
        stage("正在备份原固件（16 MiB），请保持连接…");
        await run(Command(port, "read_flash", "0x0", "0x1000000", BackupPath), token);
        if (!File.Exists(BackupPath) || new FileInfo(BackupPath).Length != Tab5InstallPackage.FlashSize)
            throw new IOException("备份不完整，已停止，未写入设备。");
        stage("正在核验原固件备份…");
        await run(Command(port, "verify_flash", "0x0", BackupPath), token);
        string sha = Tab5InstallPackage.Hash(await File.ReadAllBytesAsync(BackupPath, token));
        await File.WriteAllTextAsync(BackupPath + ".json", JsonSerializer.Serialize(new Tab5FlashBackup(1, "esp32p4", mac,
            Tab5InstallPackage.FlashSize, sha), Tab5InstallPackage.Json), token);
        BackupVerified = true;
    }
    private async Task WriteAsync(string port, string mac, string image, string sha, Action writing, CancellationToken token)
    {
        if (await ReadMacAsync(port, token) != mac) throw new IOException("设备已更换，已停止。");
        if (!FirmwareFlasher.MatchesHash(image, sha)) throw new IOException("写入镜像已变化，已停止。");
        token.ThrowIfCancellationRequested(); writing();
        stage("正在写入，请勿拔线或关闭程序…");
        await run(Command(port, "write_flash", "--flash_mode", "keep", "--flash_freq", "keep", "--flash_size", "keep", "0x0", image), CancellationToken.None);
        stage("正在核验写入内容…");
        await run(Command(port, "verify_flash", "0x0", image), CancellationToken.None);
    }
    internal async Task InstallAsync(string port, Tab5InstallPackage package, string backupDirectory, Action writing, CancellationToken token)
    {
        package.VerifyUnchanged();
        foreach (string part in new[] { "bootloader.bin", "application.bin" })
            await run(["--chip", "esp32p4", "image_info", Path.Combine(package.Directory, part)], token);
        string mac = await IdentifyAsync(port, token);
        foreach (string name in new[] { "bootloader.bin", "application.bin" }) {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(package.Directory, name), token);
            int min = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(15));
            int max = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(17));
            if (_revision < Math.Max(min, bytes[14] * 100) || (max != 0 && _revision > max))
                throw new IOException("固件与芯片修订版本不兼容，未写入设备。");
        }
        await BackupAsync(port, mac, backupDirectory, token);
        byte[] backup = await File.ReadAllBytesAsync(BackupPath!, token);
        // Never silently turn an update/migration into destructive first install.
        foreach (int address in new[] { 0x10000, 0x20000, 0x700000 })
            if (backup.AsSpan(address + 80, 11).SequenceEqual("aibot_tab5\0"u8))
                throw new IOException("设备已安装 AI-bot TAB5。请使用无线升级；旧分区请使用迁移流程。备份已保留。");
        package.VerifyUnchanged();
        await WriteAsync(port, mac, package.ImagePath, package.Manifest.ImageSha256, writing, token);
    }
    internal static (Tab5FlashBackup Metadata, byte[] Image) ReadBackup(string metadataPath)
    {
        if (!metadataPath.EndsWith(".bin.json", StringComparison.OrdinalIgnoreCase) || new FileInfo(metadataPath).Length > 4096)
            throw new IOException("请选择本工具生成的 .bin.json 备份记录。");
        var metadata = JsonSerializer.Deserialize<Tab5FlashBackup>(File.ReadAllText(metadataPath), Tab5InstallPackage.Json);
        string imagePath = metadataPath[..^5];
        if (metadata is null || metadata.Format != 1 || metadata.Chip != "esp32p4" || metadata.FlashBytes != Tab5InstallPackage.FlashSize ||
            !Regex.IsMatch(metadata.Mac ?? "", @"^[0-9a-f]{2}(?::[0-9a-f]{2}){5}$") || new FileInfo(imagePath).Length != Tab5InstallPackage.FlashSize)
            throw new IOException("备份身份或大小无效。");
        byte[] bytes = File.ReadAllBytes(imagePath);
        if (Tab5InstallPackage.Hash(bytes) != metadata.Sha256) throw new IOException("备份校验失败，不能恢复。");
        return (metadata, bytes);
    }
    internal async Task RestoreAsync(string port, string metadataPath, string workDirectory, string backupDirectory, Action writing, CancellationToken token)
    {
        var backup = ReadBackup(metadataPath);
        string mac = await IdentifyAsync(port, token);
        if (mac != backup.Metadata.Mac) throw new IOException("备份属于另一台设备，拒绝恢复。");
        Directory.CreateDirectory(workDirectory);
        string frozen = Path.Combine(workDirectory, "restore.bin");
        using (var stream = new FileStream(frozen, FileMode.CreateNew)) await stream.WriteAsync(backup.Image, token);
        await BackupAsync(port, mac, backupDirectory, token);
        if (!FirmwareFlasher.MatchesHash(frozen, backup.Metadata.Sha256)) throw new IOException("恢复镜像已变化。");
        await WriteAsync(port, mac, frozen, backup.Metadata.Sha256, writing, token);
    }
}
