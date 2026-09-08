using System.Text.Json;

namespace AIBotBridge;

internal static class UsbManagementSelfTest
{
    internal static async Task RunAsync()
    {
        const string line = "@AIBOT {\"version\":1,\"type\":\"device_info\",\"request_id\":7,\"ok\":true," +
            "\"data\":{\"device\":\"AI-bot\",\"version\":1,\"ip\":\"\",\"mode\":\"weather\",\"brightness\":75," +
            "\"usb_active\":true,\"bridge_online\":true,\"uptime_ms\":10000,\"usb_status_count\":4,\"lan_status_count\":0}}";
        var reply = UsbDeviceProtocol.ParseReply(line, "device_info", 7) ?? throw new Exception("Valid reply rejected.");
        var info = UsbDeviceProtocol.ReadInfo(reply);
        Require(info.Ip == "" && info.UsbActive && info.UsbStatusCount == 4, "Offline Wi-Fi must not block USB information.");
        foreach (var invalid in new[] { "garbage", "@AIBOT []", line.Replace("\"request_id\":7", "\"request_id\":8"),
                     line.Replace("\"version\":1", "\"version\":2"), line.Replace("device_info", "reset_wifi_ack") })
            Require(UsbDeviceProtocol.ParseReply(invalid, "device_info", 7) is null, "Wrong/stale replies must not satisfy requests.");
        var rejected = UsbDeviceProtocol.ParseReply(
            "@AIBOT {\"version\":1,\"type\":\"reset_wifi_ack\",\"request_id\":9,\"ok\":false}", "reset_wifi_ack", 9);
        Require(rejected is not null && !rejected.Value.GetProperty("ok").GetBoolean(), "Negative reset ACK must remain negative.");

        var during = info with { UsbActive = false, LanStatusCount = 2, UptimeMs = 22000 };
        var restored = during with { UsbActive = true, UsbStatusCount = 5, UptimeMs = 25000 };
        var good = new FakeDevice(info, during, restored);
        await WifiFallbackTest.RunAsync(good, CancellationToken.None, NoDelay);
        Require(good.Resumed && !good.Paused, "Successful test must restore USB.");

        foreach (var bad in new[] { during with { LanStatusCount = 0 }, during with { UsbActive = true },
                     during with { UsbStatusCount = 5 }, during with { UptimeMs = 1 }, during with { BridgeOnline = false } })
        {
            var fake = new FakeDevice(info, bad, restored);
            await ExpectFailure(() => WifiFallbackTest.RunAsync(fake, CancellationToken.None, NoDelay));
            Require(fake.Resumed && !fake.Paused, "Failed fallback must restore USB.");
        }
        var cancelled = new FakeDevice(info, during, restored);
        await ExpectFailure(() => WifiFallbackTest.RunAsync(cancelled, CancellationToken.None,
            (_, _) => Task.FromCanceled(new CancellationToken(true))));
        Require(cancelled.Resumed && !cancelled.Paused, "Cancellation must restore USB.");
        var stale = new FakeDevice(info, during, during);
        await ExpectFailure(() => WifiFallbackTest.RunAsync(stale, CancellationToken.None, NoDelay));
        var disconnected = new FakeDevice(info, during, restored) { ThrowAt = 1 };
        await ExpectFailure(() => WifiFallbackTest.RunAsync(disconnected, CancellationToken.None, NoDelay));
        Require(disconnected.Resumed && !disconnected.Paused, "Diagnostic failure must restore USB.");
        var noBaseline = new FakeDevice(info with { UsbActive = false }, during, restored);
        await ExpectFailure(() => WifiFallbackTest.RunAsync(noBaseline, CancellationToken.None, NoDelay));
        Require(noBaseline.Resumed && !noBaseline.Paused, "Invalid baseline must restore USB.");
        Console.WriteLine("USB_MANAGEMENT_SELF_TEST_OK (synthetic; no hardware/reset)");
    }

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;
    private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static async Task ExpectFailure(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { return; }
        throw new Exception("Expected an explicit test failure.");
    }

    private sealed class FakeDevice(params UsbDeviceInfo[] snapshots) : IUsbFallbackDevice
    {
        private int _index;
        internal bool Paused;
        internal bool Resumed;
        internal int ThrowAt = -1;
        public void PauseTransmission() => Paused = true;
        public void ResumeTransmission() { Paused = false; Resumed = true; }
        public UsbDeviceInfo ReadDeviceInfo()
        {
            if (_index == ThrowAt) throw new IOException("Simulated diagnostic disconnect.");
            Require(_index >= 2 || Paused, "Baseline must be sampled after USB traffic is paused.");
            return snapshots[_index++];
        }
    }
}
