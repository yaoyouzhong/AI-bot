using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test-settings", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            ApplicationConfiguration.Initialize();
            var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "settings-self-test.png");
            SettingsSelfTest.Run(output);
            return;
        }

        if (args.Contains("--self-test-mirror", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "mirror-self-test.png");
            MirrorSelfTest.Run(output);
            return;
        }

        if (args.Contains("--self-test-music", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var music = new NowPlayingService();
            music.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var snapshot = music.Snapshot;
            Console.WriteLine($"MUSIC_SELF_TEST_OK session={(string.IsNullOrEmpty(snapshot?.Title) ? "none" : "present")} " +
                              $"playing={snapshot?.Playing ?? false} duration={snapshot?.DurationSeconds ?? 0:0}");
            return;
        }

        if (args.Contains("--self-test-system", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            var metrics = new SystemMetricsService().CaptureForSelfTestAsync().GetAwaiter().GetResult();
            Console.WriteLine($"SYSTEM_SELF_TEST_OK cpu={metrics.CpuPercent:0.0}% memory={metrics.MemoryPercent:0.0}% " +
                              $"up={metrics.UploadBytesPerSecond} down={metrics.DownloadBytesPerSecond}");
            return;
        }

        if (args.Contains("--self-test-live-data", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            LiveDataSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--self-test-data", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            DataSourceSelfTest.Run();
            return;
        }

        if (args.Contains("--self-test-lan", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            LanServerSelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }

        if (args.Contains("--status-once", StringComparer.OrdinalIgnoreCase))
        {
            Console.OutputEncoding = Encoding.UTF8;
            using var runtime = new BridgeRuntime(startRefresh: false);
            Console.WriteLine(JsonSerializer.Serialize(
                runtime.Capture(), JsonDefaults.Options));
            return;
        }

        FreeConsole();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
