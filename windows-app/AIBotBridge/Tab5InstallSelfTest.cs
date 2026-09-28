using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5InstallSelfTest
{
    private static int _checks;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private static void Reject(Action action, string label) {
        try { action(); } catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { _checks++; return; }
        throw new Exception("Accepted invalid input: " + label);
    }
    private sealed class Device(byte[] initial)
    {
        internal byte[] Flash = initial.ToArray();
        internal int Writes, Reads, MacReads, Verifies;
        internal string Chip = "ESP32-P4", Size = "16MB", Revision = "0.1", Security = "Disabled", Mac = "aa:bb:cc:dd:ee:ff";
        internal string? Fault;
        internal Action? AfterBackup;
        internal Task<string> Run(string[] args, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            string[] commands = ["image_info", "flash_id", "get_security_info", "read_mac", "read_flash", "verify_flash", "write_flash"];
            string command = args.First(a => commands.Contains(a));
            if (command != "image_info") Require(args.Contains("no_reset") && !args.Contains("--force") && !args.Contains("erase_flash"), "Unsafe device command");
            switch (command) {
                case "image_info": return Task.FromResult("image valid");
                case "flash_id": return Task.FromResult($"Chip is {Chip} (revision v{Revision})\nDetected flash size: {Size}\n");
                case "get_security_info": return Task.FromResult($"Secure Boot: {Security}\nFlash Encryption: Disabled\n");
                case "read_mac": MacReads++; return Task.FromResult("MAC: " + (Fault == "swap" && MacReads > 1 ? "00:11:22:33:44:55" : Mac) + "\n");
                case "read_flash": Reads++; File.WriteAllBytes(args[^1], Fault == "short-backup" ? Flash[..1024] : Flash); break;
                case "verify_flash":
                    Verifies++;
                    if (Fault == "backup-verify" && Writes == 0 || Fault == "write-verify" && Writes > 0) throw new IOException("simulated verify failure");
                    Require(File.ReadAllBytes(args[^1]).AsSpan().SequenceEqual(Flash), "Readback mismatch");
                    if (Writes == 0) AfterBackup?.Invoke();
                    break;
                case "write_flash":
                    Require(token == CancellationToken.None, "Write must not be cancelled mid-flash");
                    Writes++; Flash = File.ReadAllBytes(args[^1]); break;
            }
            return Task.FromResult("OK");
        }
    }
    internal static async Task RunAsync(string zipPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "aibot-tab5-install-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var package = Tab5InstallPackage.Load(zipPath, Path.Combine(root, "package"));
            byte[] image = File.ReadAllBytes(package.ImagePath);
            var manifest = package.Manifest;
            Require(image.Length == 0x1000000 && manifest.Version.Length > 0, "Production package load");
            package.VerifyUnchanged();
            Reject(() => Tab5InstallPackage.Validate(manifest with { Chip = "esp8266" }, image), "wrong chip");
            Reject(() => Tab5InstallPackage.Validate(manifest with { Version = "wrong" }, image), "wrong application version");
            Reject(() => Tab5InstallPackage.Validate(manifest with { ImageSha256 = new string('0', 64) }, image), "wrong hash");
            var parts = manifest.Segments.ToArray(); parts[3] = parts[3] with { Offset = 0x10000 };
            Reject(() => Tab5InstallPackage.Validate(manifest with { Segments = parts }, image), "old layout");
            foreach (int address in new[] { 0, 0x9000, 0xf000, 0x700000, 0xe00000, 0xffffff }) {
                byte[] bad = image.ToArray(); bad[address] = 0x42;
                Reject(() => Tab5InstallPackage.Validate(manifest with { ImageSha256 = Tab5InstallPackage.Hash(bad) }, bad), "private bytes in erased region");
            }
            byte[] badImage = image.ToArray(); badImage[0x20000 + 1000] ^= 1;
            var badParts = manifest.Segments.ToArray(); var app = badParts[3]; badParts[3] = app with { Sha256 = Tab5InstallPackage.Hash(badImage.AsSpan(app.Offset, app.Size)) };
            Reject(() => Tab5InstallPackage.Validate(manifest with { ImageSha256 = Tab5InstallPackage.Hash(badImage), Segments = badParts }, badImage), "invalid ESP image with recomputed package hash");
            Reject(() => Tab5InstallPackage.ValidateEspImage(image.AsSpan(0x20000, app.Size - 1)), "truncated ESP image");
            foreach (string badName in new[] { "../outside.bin", "C:/outside.bin", "factory.bin" }) {
                string badZip = Path.Combine(root, Guid.NewGuid() + ".zip");
                using (var zip = ZipFile.Open(badZip, ZipArchiveMode.Create)) { zip.CreateEntry("factory.bin"); zip.CreateEntry(badName); }
                Reject(() => Tab5InstallPackage.Load(badZip, Path.Combine(root, "bad-stage")), "unsafe ZIP");
            }
            byte[] stock = Enumerable.Repeat((byte)0xff, Tab5InstallPackage.FlashSize).ToArray();
            Encoding.ASCII.GetBytes("synthetic stock firmware").CopyTo(stock, 0x2000);
            async Task MustNotWrite(string label, Action<Device> configure, CancellationToken token = default) {
                var device = new Device(stock); configure(device);
                var installer = new Tab5Installer(device.Run, _ => { }); bool failed = false;
                try { await installer.InstallAsync("COM7", package, Path.Combine(root, label), () => { }, token); }
                catch (Exception ex) when (ex is IOException or OperationCanceledException) { failed = true; }
                Require(failed && device.Writes == 0 && device.Flash.AsSpan().SequenceEqual(stock), label + " must not modify flash");
            }
            await MustNotWrite("wrong-chip", d => d.Chip = "ESP8266");
            await MustNotWrite("wrong-size", d => d.Size = "8MB");
            await MustNotWrite("security", d => d.Security = "Enabled");
            await MustNotWrite("unknown-revision", d => d.Revision = "unknown");
            foreach (string fault in new[] { "short-backup", "backup-verify", "swap" }) await MustNotWrite(fault, d => d.Fault = fault);
            using (var cancel = new CancellationTokenSource()) await MustNotWrite("cancel-before-write", d => d.AfterBackup = cancel.Cancel, cancel.Token);
            foreach (int offset in new[] { 0x10000, 0x20000, 0x700000 }) {
                var existing = new Device(stock); "aibot_tab5\0"u8.CopyTo(existing.Flash.AsSpan(offset + 80));
                bool failed = false;
                try { await new Tab5Installer(existing.Run, _ => { }).InstallAsync("COM7", package, Path.Combine(root, "existing-" + offset), () => { }, default); }
                catch (IOException) { failed = true; }
                Require(failed && existing.Writes == 0, "Existing AI-bot must use OTA or migration");
            }
            await MustNotWrite("tampered-package", d => d.AfterBackup = () => { using var file = File.OpenWrite(package.ImagePath); file.WriteByte(0); });
            File.WriteAllBytes(package.ImagePath, image);
            var success = new Device(stock); var flow = new Tab5Installer(success.Run, _ => { }); bool writing = false;
            await flow.InstallAsync("COM7", package, Path.Combine(root, "success"), () => writing = true, default);
            Require(writing && success.Writes == 1 && success.Verifies == 2 && success.Flash.AsSpan().SequenceEqual(image), "Full install and readback");
            var backup = Tab5Installer.ReadBackup(flow.BackupPath! + ".json");
            Require(backup.Image.AsSpan().SequenceEqual(stock), "Original backup retained exactly");
            var restore = new Tab5Installer(success.Run, _ => { });
            await restore.RestoreAsync("COM7", flow.BackupPath! + ".json", Path.Combine(root, "restore-stage"), Path.Combine(root, "restore-backup"), () => { }, default);
            Require(success.Writes == 2 && success.Flash.AsSpan().SequenceEqual(stock), "Recovery restores original exact bytes");
            var wrongDevice = new Device(stock) { Mac = "11:22:33:44:55:66" };
            bool wrongRejected = false;
            try { await new Tab5Installer(wrongDevice.Run, _ => { }).RestoreAsync("COM7", flow.BackupPath! + ".json", Path.Combine(root, "wrong-restore"), root, () => { }, default); }
            catch (IOException) { wrongRejected = true; }
            Require(wrongRejected && wrongDevice.Writes == 0, "Cannot restore another device backup");
            var damaged = new Device(stock) { Fault = "write-verify" }; var failedFlow = new Tab5Installer(damaged.Run, _ => { }); bool reportedFailure = false;
            try { await failedFlow.InstallAsync("COM7", package, Path.Combine(root, "write-failed"), () => { }, default); } catch (IOException) { reportedFailure = true; }
            Require(reportedFailure && damaged.Writes == 1 && File.Exists(failedFlow.BackupPath! + ".json"), "Failure after write preserves recoverable backup");
            File.WriteAllBytes(flow.BackupPath!, new byte[16]); Reject(() => Tab5Installer.ReadBackup(flow.BackupPath! + ".json"), "damaged backup");
            BootTests(manifest.Version);
            Console.WriteLine($"TAB5_INSTALL_TEST_OK assertions={_checks} version={manifest.Version}; simulated flash only, no hardware accessed");
        } finally {
            // This test owns this GUID directory and all data in it is synthetic.
            string path = Path.GetFullPath(root), expected = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (path.StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("aibot-tab5-install-test-")) Directory.Delete(path, true);
        }
    }
    private static void BootTests(string version) {
        object Hello(long uptime = 10000, long age = 20, string device = "aabbccddeeff", long errors = 0) =>
            new { type = "tab5_hello", version = 1, deviceId = device, firmware = version, uptimeMs = uptime, uiAgeMs = age, flushErrors = errors, flushCount = 100 };
        JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
        Require(Tab5InstallBootCheck.Hello(Json(Hello()), "aa:bb:cc:dd:ee:ff", version, 9000) == 10000, "Healthy hello");
        Reject(() => Tab5InstallBootCheck.Hello(Json(Hello(age: 5000)), "aa:bb:cc:dd:ee:ff", version, 9000), "frozen UI");
        Reject(() => Tab5InstallBootCheck.Hello(Json(Hello()), "aa:bb:cc:dd:ee:ff", version, 11000), "reboot loop");
        Reject(() => Tab5InstallBootCheck.Hello(Json(Hello(device: "112233445566")), "aa:bb:cc:dd:ee:ff", version, 9000), "wrong boot device");
        Reject(() => Tab5InstallBootCheck.Hello(Json(Hello(errors: 1)), "aa:bb:cc:dd:ee:ff", version, 9000), "display failure");
        var diagnostic = Json(new { type = "tab5_ota_diagnostic", firmware = version, partition = "ota_0", address = 0x20000, elfSha256 = "test-hash" });
        Tab5InstallBootCheck.Diagnostic(diagnostic, version, "test-hash");
        Reject(() => Tab5InstallBootCheck.Diagnostic(diagnostic, version, "other-hash"), "wrong running image");
    }
    internal static void Capture(string directory) {
        AppPaths.BeginPublicSelfTest(); Directory.CreateDirectory(directory);
        using var service = new Tab5Service(new Tab5PairingStore(Path.Combine(directory, "unused-pairing.dat")));
        using var form = new Tab5InstallForm(service, preview: true);
        form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
        IEnumerable<Control> Children(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (var inner in Children(child)) yield return inner; } }
        foreach (string size in new[] { "normal", "minimum" }) {
            if (size == "minimum") form.Size = form.MinimumSize;
            Application.DoEvents();
            foreach (var flow in Children(form).OfType<FlowLayoutPanel>().Where(p => p.AutoScroll))
                Require(!flow.HorizontalScroll.Visible, "First-install page must not require horizontal scrolling");
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(directory, "tab5-first-install-" + size + ".png"));
            var content = Children(form).OfType<FlowLayoutPanel>().Single(p => p.AutoScroll);
            content.ScrollControlIntoView(content.Controls[^1]); Application.DoEvents();
            using var bottom = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bottom, new Rectangle(Point.Empty, form.Size));
            bottom.Save(Path.Combine(directory, "tab5-first-install-" + size + "-bottom.png"));
            content.AutoScrollPosition = Point.Empty;
        }
        form.Close(); Console.WriteLine("TAB5_INSTALL_LAYOUT_CAPTURED preview only; normal/minimum size");
    }
    internal static async Task CheckToolAsync(string zipPath) {
        AppPaths.BeginPublicSelfTest();
        using var tool = await FirmwareFlasher.PrepareToolAsync(_ => { }, default);
        var package = Tab5InstallPackage.Load(zipPath, Path.Combine(tool.Directory, "package"));
        foreach (string name in new[] { "bootloader.bin", "application.bin" }) {
            string output = await FirmwareFlasher.RunProcessAsync(tool.Executable, ["--chip", "esp32p4", "image_info", "--version", "2", Path.Combine(package.Directory, name)], _ => { }, default);
            Require(output.Contains("Chip ID: 18 (ESP32-P4)") && output.Contains("(valid)"), "Pinned tool must recognize and validate P4 image: " + output);
            Console.WriteLine("TAB5_PINNED_TOOL_IMAGE_OK " + name);
        }
        string help = await FirmwareFlasher.RunProcessAsync(tool.Executable, ["--chip", "esp32p4", "write_flash", "--help"], _ => { }, default);
        Require(help.Contains("keep"), "Pinned tool must support preserving flash parameters");
        Console.WriteLine("TAB5_PINNED_TOOL_OK offline image inspection only; no device commands");
    }
}
