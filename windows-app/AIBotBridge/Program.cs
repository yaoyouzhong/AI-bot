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
