using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// Uses Espressif's unmodified standalone tool; users do not need Python.
internal sealed class FirmwareFlasher
{
    internal const string ToolVersion = "4.9.1";
    internal const string ToolUrl = "https://github.com/espressif/esptool/releases/download/v4.9.1/esptool-v4.9.1-windows-amd64.zip";
    internal const string ToolHash = "6d3d08187e21af57c6f2ba4ebbed9f1b53240d271a506ab888ddd0f5bfa8eaf7";
    internal static string CacheDirectory => Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot", "flash-tools");
    internal static string BackupDirectory => Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot", "device-backups");
    internal delegate Task<string> Runner(string[] arguments, CancellationToken cancellation);
    private readonly Runner _run;
    private readonly Action<string> _stage;

    internal FirmwareFlasher(Runner run, Action<string> stage) { _run = run; _stage = stage; }

    internal sealed record ToolInstall(string Executable, string Directory) : IDisposable
    {
        public void Dispose() => CleanupTemporaryDirectory(Directory, "run-");
    }

    internal static void CleanupTemporaryDirectory(string directory, string prefix)
    {
        var full = Path.GetFullPath(directory);
        var root = Path.GetFullPath(CacheDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal))
            throw new IOException("临时目录超出清理范围。");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    internal static async Task<ToolInstall> PrepareToolAsync(Action<string> report, CancellationToken cancellation)
    {
        Directory.CreateDirectory(CacheDirectory);
        var archive = Path.Combine(AppContext.BaseDirectory, "flash-tools", "esptool-" + ToolVersion + ".zip");
        report("正在准备…");
        if (!File.Exists(archive)) throw new IOException("安装文件不完整，请重新安装完整版刷机工具。");
        if (!await Task.Run(() => MatchesHash(archive, ToolHash), cancellation))
            throw new IOException("安装文件校验失败，请重新安装完整版刷机工具。");
        // Fresh extraction from the verified archive also prevents reuse of a modified executable.
        var stage = Path.Combine(CacheDirectory, "run-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, stage), cancellation);
            return new ToolInstall(Directory.GetFiles(stage, "esptool.exe", SearchOption.AllDirectories).Single(), stage);
        }
        catch { CleanupTemporaryDirectory(stage, "run-"); throw; }
    }

    internal static bool MatchesHash(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    internal static string PrepareFirmware(string selected, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        var target = Path.Combine(workDirectory, "firmware.bin");
        if (Path.GetExtension(selected).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(selected);
            var entry = zip.GetEntry("firmware.bin") ?? throw new IOException("请选择 AI-bot 固件材料包，里面应有顶层 firmware.bin。");
            if (entry.Length is < 1024 or > 4 * 1024 * 1024) throw new IOException("固件大小异常。");
            entry.ExtractToFile(target);
        }
        else File.Copy(selected, target);
        using var input = File.OpenRead(target);
        if (input.Length is < 1024 or > 4 * 1024 * 1024 || input.ReadByte() != 0xE9)
            throw new IOException("不是有效的 ESP8266 固件文件。请选择本项目的固件包或 firmware.bin。");
        return target;
    }

    internal async Task<string> ExecuteAsync(string port, string firmware, string backupDirectory,
        bool backupOnly, Action writing, CancellationToken cancellation, bool requireCurrentFirmware = false)
    {
        if (!Regex.IsMatch(port, @"^COM[1-9][0-9]*$", RegexOptions.IgnoreCase)) throw new IOException("请选择有效的 COM 端口。");
        var baud = "460800";
        string[] Command(params string[] args) => ["--chip", "esp8266", "--port", port, "--baud", baud, .. args];
        async Task<string> ReadWithFallback(string stage, params string[] args)
        {
            _stage(stage);
            try { return await _run(Command(args), cancellation); }
            catch (IOException) when (baud == "460800")
            {
                // Retry only read-only operations, before any write. Retain the
                // working rate for the later write and verification commands.
                baud = "115200";
                _stage("正在以兼容速度重试，" + stage);
                return await _run(Command(args), cancellation);
            }
        }
        if (!backupOnly)
        {
            _stage("正在检查固件…");
            await _run(["--chip", "esp8266", "image_info", firmware], cancellation);
        }
        var info = await ReadWithFallback("正在检查设备…", "flash_id");
        var capacity = ParseFlashSize(info);
        if (!backupOnly && new FileInfo(firmware).Length > capacity) throw new IOException("固件超过设备存储容量，已停止。");
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"backup-{port}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bin");
        await ReadWithFallback($"正在备份（{capacity / 1048576.0:0.#} MB），请保持连接…",
            "read_flash", "0", capacity.ToString(System.Globalization.CultureInfo.InvariantCulture), backup);
        if (!File.Exists(backup) || new FileInfo(backup).Length != capacity)
            throw new IOException("备份不完整，已停止，未写入新固件。请重试备份。");
        using (var input = File.OpenRead(backup))
            await File.WriteAllTextAsync(backup + ".sha256", Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant() + "  " + Path.GetFileName(backup) + "\n", cancellation);
        if (backupOnly) return backup;
        cancellation.ThrowIfCancellationRequested();
        if (requireCurrentFirmware)
        {
            _stage("正在确认与设备当前固件一致…");
            await _run(Command("verify_flash", "0x0", firmware), cancellation);
        }
        writing();
        _stage("正在刷机，请勿拔线…");
        await _run(Command("write_flash", "0x0", firmware), CancellationToken.None);
        _stage("即将完成，请保持连接…");
        await _run(Command("verify_flash", "0x0", firmware), CancellationToken.None);
        return backup;
    }

    internal static long ParseFlashSize(string output)
    {
        var match = Regex.Match(output, @"Detected flash size:\s*(\d+)\s*(KB|MB)", RegexOptions.IgnoreCase);
        if (!match.Success) throw new IOException("未能确认 Flash 容量，已停止；请检查设备连接和型号。");
        var size = long.Parse(match.Groups[1].Value) * (match.Groups[2].Value.Equals("MB", StringComparison.OrdinalIgnoreCase) ? 1024 * 1024 : 1024);
        if (size is < 256 * 1024 or > 16 * 1024 * 1024) throw new IOException("不支持的 Flash 容量。");
        return size;
    }

    internal static async Task<string> RunProcessAsync(string executable, string[] arguments, Action<string> log, CancellationToken cancellation, TimeSpan? readIdleTimeout = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var idleLimit = arguments.Contains("read_flash") ? readIdleTimeout ?? TimeSpan.FromSeconds(30) : readIdleTimeout;
        timeout.CancelAfter(idleLimit ?? TimeSpan.FromMinutes(15));
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        }};
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
        var result = new StringBuilder();
        var sync = new object();
        void Line(string line) {
            if (idleLimit is { } idle) timeout.CancelAfter(idle);
            lock (sync) { if (result.Length > 2_000_000) result.Clear(); result.AppendLine(line); }
            log(line);
        }
        process.Start();
        // esptool read_flash flushes progress followed by backspaces, not newlines.
        // Read chunks so progress reaches the UI before the full backup completes.
        var stdout = ReadOutputAsync(process.StandardOutput, Line);
        var stderr = ReadOutputAsync(process.StandardError, Line);
        try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(stdout, stderr); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            if (!cancellation.IsCancellationRequested) throw new IOException(idleLimit is not null
                ? "备份读取长时间没有新进度，已停止本次读取，未写入固件。"
                : "操作超时。请检查连接；如已开始写入，请重新刷写，不要全片擦除。");
            throw;
        }
        if (process.ExitCode != 0) throw new IOException("刷机工具执行失败。请查看下方详细记录；检查数据线、端口占用与设备型号后重试。");
        return result.ToString();
    }

    private static async Task ReadOutputAsync(StreamReader reader, Action<string> line)
    {
        var parser = new FlashOutputParser(line);
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory())) > 0)
            parser.Feed(buffer.AsSpan(0, count));
        parser.Finish();
    }
}

internal sealed class FlashOutputParser(Action<string> line)
{
    private readonly StringBuilder _pending = new();
    internal void Feed(ReadOnlySpan<char> text)
    {
        foreach (var value in text)
        {
            if (value is '\b' or '\r' or '\n') Finish();
            else { _pending.Append(value); if (_pending.Length >= 8192) Finish(); }
        }
    }
    internal void Finish()
    {
        if (_pending.Length == 0) return;
        line(_pending.ToString()); _pending.Clear();
    }
}
