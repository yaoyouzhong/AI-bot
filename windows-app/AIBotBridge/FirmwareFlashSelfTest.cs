using System.IO.Compression;

namespace AIBotBridge;

internal static class FirmwareFlashSelfTest
{
    internal static async Task RunAsync()
    {
        CheckDeviceSelection();
        var resident = new BridgeResumeTarget(123, DateTime.UtcNow, @"C:\Installed Bridge\AIBotBridge.exe", 18765);
        Check(BridgeResumeTarget.Select([resident, resident]) == resident, "duplicate listener evidence resolves to resident");
        var restore = resident.StartInfo();
        Check(restore.FileName == resident.Executable && restore.Arguments == "" && restore.Environment["AIBOT_HTTP_PORT"] == "18765", "restore original executable and port without flasher arguments");
        try { BridgeResumeTarget.Select([]); throw new Exception("unknown resident guessed"); } catch (IOException) { }
        try { BridgeResumeTarget.Select([resident, resident with { ProcessId = 124 }]); throw new Exception("ambiguous resident guessed"); } catch (IOException) { }
        Console.WriteLine("FLASH_RESUME_TARGET_OK original-path/port/no-flash-arguments/ambiguity");
        var output = new List<string>();
        var parser = new FlashOutputParser(output.Add);
        parser.Feed("Configuring flash size...\r\n4096 (0 %)");
        Check(output.Count == 1, "partial progress waits for a control delimiter");
        parser.Feed("\b\b\b8192 (1 %)\b");
        Check(output.SequenceEqual(new[] { "Configuring flash size...", "4096 (0 %)", "8192 (1 %)" }), "backspace progress delivered before EOF");
        parser.Feed("Done\r\nlast"); parser.Finish();
        Check(output.TakeLast(2).SequenceEqual(new[] { "Done", "last" }), "CRLF and EOF drain once");
        Console.WriteLine("FLASH_PROGRESS_STREAM_OK backspace/split-chunks/CRLF/EOF");
        var directory = Path.Combine(Path.GetTempPath(), "AI-bot-flash-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var ack = Path.Combine(directory, "progress-delivered");
            var host = Environment.ProcessPath!;
            string[] childArgs = Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? [typeof(FirmwareFlashSelfTest).Assembly.Location, "--self-test-flash-progress-child", ack]
                : ["--self-test-flash-progress-child", ack];
            await FirmwareFlasher.RunProcessAsync(host, childArgs, line => {
                if (line.Contains("25 %")) File.WriteAllText(ack, "delivered before child exit");
            }, CancellationToken.None);
            Check(File.Exists(ack), "real child pipe reports progress without waiting for newline or EOF");
            Console.WriteLine("FLASH_PROGRESS_PIPE_OK child waits for delivered progress before exit");
            var idleAck = Path.Combine(directory, "idle-child-no-ack");
            var idleArgs = childArgs.ToArray(); idleArgs[^1] = idleAck;
            try {
                await FirmwareFlasher.RunProcessAsync(host, idleArgs, _ => { }, CancellationToken.None, TimeSpan.FromMilliseconds(500));
                throw new Exception("stalled read child was not stopped");
            } catch (IOException ex) { Check(ex.Message.Contains("未写入固件"), "read inactivity is reported for safe fallback"); }
            Console.WriteLine("FLASH_READ_IDLE_OK silent child stopped without a write operation");
            var firmware = Path.Combine(directory, "firmware.bin");
            var bytes = new byte[2048]; bytes[0] = 0xE9; await File.WriteAllBytesAsync(firmware, bytes);
            var zipPath = Path.Combine(directory, "materials.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) zip.CreateEntryFromFile(firmware, "firmware.bin");
            var prepared = FirmwareFlasher.PrepareFirmware(zipPath, Path.Combine(directory, "prepared"));
            Check(File.ReadAllBytes(prepared).SequenceEqual(bytes), "ZIP selects exactly the root firmware");
            var invalid = Path.Combine(directory, "bad.bin"); await File.WriteAllTextAsync(invalid, "not firmware");
            try { FirmwareFlasher.PrepareFirmware(invalid, Path.Combine(directory, "bad")); throw new Exception("Invalid firmware accepted"); } catch (IOException) { }
            Check(FirmwareFlasher.ParseFlashSize("Detected flash size: 4MB") == 4194304, "flash capacity");
            try { FirmwareFlasher.ParseFlashSize("connection failed"); throw new Exception("Unknown capacity accepted"); } catch (IOException) { }
            foreach (var scenario in new[] { "success", "backup-only", "short-backup", "backup-error", "write-error", "verify-error", "current-mismatch", "cancel", "fast-read-error", "fast-id-error" })
            {
                var commands = new List<string>();
                var rates = new List<string>();
                using var cancellation = new CancellationTokenSource();
                var flasher = new FirmwareFlasher((args, token) => {
                    token.ThrowIfCancellationRequested();
                    var command = args.FirstOrDefault(x => new[] { "image_info", "flash_id", "read_flash", "write_flash", "verify_flash" }.Contains(x))!;
                    commands.Add(command);
                    var rate = args.Contains("--baud") ? args[Array.IndexOf(args, "--baud") + 1] : "";
                    if (rate.Length > 0) rates.Add(rate);
                    if (rate == "460800" && ((scenario == "fast-read-error" && command == "read_flash") || (scenario == "fast-id-error" && command == "flash_id")))
                        throw new IOException("simulated fast connection failure");
                    if (command == "flash_id") return Task.FromResult("Detected flash size: 1MB");
                    if (command == "read_flash")
                    {
                        if (scenario == "backup-error") throw new IOException("simulated backup error");
                        File.WriteAllBytes(args[^1], new byte[scenario == "short-backup" ? 32 : 1048576]);
                        if (scenario == "cancel") cancellation.Cancel();
                    }
                    if (command == "write_flash" && scenario == "write-error") throw new IOException("simulated write error");
                    if (command == "verify_flash" && scenario is "verify-error" or "current-mismatch") throw new IOException("simulated verify error");
                    return Task.FromResult("OK");
                }, _ => { });
                var succeeded = false;
                try { await flasher.ExecuteAsync("COM5", firmware, Path.Combine(directory, scenario), scenario == "backup-only", () => { }, cancellation.Token, scenario == "current-mismatch"); succeeded = true; }
                catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
                Check(succeeded == (scenario is "success" or "backup-only" or "fast-read-error" or "fast-id-error"), scenario + " outcome");
                if (scenario is "short-backup" or "backup-error" or "cancel" or "backup-only" or "current-mismatch") Check(!commands.Contains("write_flash"), scenario + " must not write");
                if (scenario == "write-error") Check(!commands.Contains("verify_flash"), "failed write must not advance");
                if (scenario == "success") Check(commands.SequenceEqual(new[] { "image_info", "flash_id", "read_flash", "write_flash", "verify_flash" }), "full sequence");
                if (scenario == "success") Check(rates.All(rate => rate == "460800"), "normal flash uses fast rate");
                if (scenario is "fast-read-error" or "fast-id-error") Check(rates[0] == "460800" && rates.TakeLast(2).All(rate => rate == "115200"), "read fallback retains working rate for write and verify");
            }
            Console.WriteLine("FIRMWARE_FLASH_SELF_TEST_OK zip validation; capacity; success; backup-only; backup failures; write/verify failures; cancellation");
        }
        finally
        {
            var full = Path.GetFullPath(directory);
            if (Path.GetDirectoryName(full) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) && Path.GetFileName(full).StartsWith("AI-bot-flash-test-")) Directory.Delete(full, true);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static void CheckDeviceSelection()
    {
        var screen = new FlashUsbDevice("COM7", "USB\\screen", "USB serial");
        var other = new FlashUsbDevice("COM9", "USB\\other", "USB serial");
        Check(!FlashDeviceDiscovery.IsUsbIdentity("BTHENUM\\COM5"), "Bluetooth excluded");
        var selection = new FlashDeviceSelection("COM7");
        selection.Update([screen, other]); Check(selection.Selected == screen, "connected bridge wins");
        selection.ScanFailed(); Check(selection.Selected is null, "failed scan disables flashing");
        selection.Update([screen, other]); Check(selection.Selected == screen, "scan error recovers without replug");
        selection.Update([other]); Check(selection.Selected is null, "unplug must not select another device");
        selection.Update([other, screen with { Identity = "USB\\replacement" }]); Check(selection.Selected is null, "COM reuse is not identity");
        selection.Update([other, screen with { Port = "COM8" }]); Check(selection.Selected?.Port == "COM8", "same device re-enumerates");
        var first = new FlashDeviceSelection();
        first.Update([screen, other]); Check(first.Selected is null, "ambiguous startup must ask for replug");
        first.Update([other]); Check(first.Selected is null, "do not select the device left behind");
        first.Update([other, screen]); Check(first.Selected == screen, "replug selects new device");
        first.Reset([screen, other]); first.Update([screen, other]); Check(first.Selected is null, "reset does not guess");
        var empty = new FlashDeviceSelection(); empty.Update([]); empty.Update([screen, other]); Check(empty.Selected is null, "simultaneous arrivals remain ambiguous");
        var single = new FlashDeviceSelection(); single.Update([screen]); Check(single.Selected == screen, "already plugged single USB device selected");
        single.Update([]); Check(single.Selected is null && single.Message == "正在确认连接…", "transient absence disables write without false disconnect");
        single.Update([screen]); Check(single.Selected == screen, "transient absence recovers automatically");
        single.ScanFailed(); single.Update([other]); Check(single.Selected is null, "scan error must not forget target identity");
        try { FlashDeviceSelection.RequireSame(screen, [screen with { Identity = "USB\\replacement" }]); throw new Exception("replacement accepted"); } catch (IOException) { }
        Console.WriteLine("FLASH_DEVICE_SELECTION_OK bridge/USB-only/replug/ambiguity/disconnect/identity/COM-reuse");
    }

    internal static async Task RunDeviceBackupAsync(string port)
    {
        var device = FlashDeviceDiscovery.Read().Single(d => d.Port == port);
        var total = System.Diagnostics.Stopwatch.StartNew();
        double readSeconds = 0;
        string? readBaud = null;
        var lastPercent = -10;
        using var tool = await FirmwareFlasher.PrepareToolAsync(Console.WriteLine, CancellationToken.None);
        var flasher = new FirmwareFlasher(async (args, cancellation) => {
            if (args.Contains("write_flash") || args.Contains("erase_flash")) throw new Exception("Backup test cannot write or erase");
            FlashDeviceSelection.RequireSame(device, FlashDeviceDiscovery.Read());
            var reading = args.Contains("read_flash");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = await FirmwareFlasher.RunProcessAsync(tool.Executable, args, line => {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s*%");
                if (reading && match.Success && int.TryParse(match.Groups[1].Value, out var percent) && percent >= lastPercent + 10)
                { lastPercent = percent; Console.WriteLine($"BACKUP_PROGRESS {percent}% elapsed={timer.Elapsed.TotalSeconds:0.0}s"); }
            }, cancellation);
            if (reading) { readSeconds = timer.Elapsed.TotalSeconds; readBaud = args[Array.IndexOf(args, "--baud") + 1]; }
            return result;
        }, Console.WriteLine);
        var backup = await flasher.ExecuteAsync(port, "", FirmwareFlasher.BackupDirectory, true, () => throw new Exception("Backup test must not write"), CancellationToken.None);
        Check(File.Exists(backup + ".sha256"), "Backup checksum missing");
        Console.WriteLine($"BACKUP_SPEED baud={readBaud} readSeconds={readSeconds:0.00} totalSeconds={total.Elapsed.TotalSeconds:0.00}");
        Console.WriteLine("FIRMWARE_DEVICE_BACKUP_OK bytes=" + new FileInfo(backup).Length + "; no write_flash issued");
    }

    internal static void Capture(string path, bool liveDevice = false, bool completedPreview = false)
    {
        using var form = new FirmwareFlashForm(preview: !liveDevice);
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
        form.Show(); Application.DoEvents();
        if (completedPreview) {
            form.ShowCompletedPreview();
            var repaintUntil = DateTime.UtcNow.AddMilliseconds(700);
            while (DateTime.UtcNow < repaintUntil) { Application.DoEvents(); Thread.Sleep(20); }
        }
        if (liveDevice)
        {
            // Read-only observation: allow the normal async inventory and timer
            // to populate the actual form. Never select firmware or press Start.
            var until = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
        }
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); bitmap.Save(path);
        form.Close();
        Console.WriteLine("FIRMWARE_FLASH_PREVIEW_OK " + Path.GetFullPath(path));
    }
}
