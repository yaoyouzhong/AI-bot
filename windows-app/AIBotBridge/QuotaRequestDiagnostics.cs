using System.Text.Json;

namespace AIBotBridge;

// Fixed endpoint, categories and numeric metrics only. Never persist headers, bodies or exception messages.
internal static class QuotaRequestDiagnostics
{
    private static readonly object Gate = new();
    internal static string FilePath => Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot", "deepseek-requests.jsonl");
    internal static void Record(string category, int? status, long elapsedMs)
    {
        lock (Gate) {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath).TakeLast(199).ToList() : new List<string>();
                lines.Add(JsonSerializer.Serialize(new { at = DateTimeOffset.Now, category, status, elapsedMs }));
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
    internal static async Task ProbeAsync()
    {
        var key = MigratedWeather.CredentialStore.Read("AI-bot/DeepSeekApiKey");
        if (string.IsNullOrWhiteSpace(key)) { Console.WriteLine("DEEPSEEK_NOT_CONFIGURED"); return; }
        for (var i = 0; i < 3; i++) {
            var result = await DeepSeekBalanceApi.FetchAsync(key);
            Console.WriteLine(JsonSerializer.Serialize(new { result.Success, result.Category, result.HttpStatus }));
            if (result.RateLimited || result.HttpStatus is 401 or 403) break;
            if (i < 2) await Task.Delay(2000);
        }
    }

    internal static void RecordLocalServerFailure(string socketError)
    {
        try {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "local-server-error.json"),
                JsonSerializer.Serialize(new { at = DateTimeOffset.Now, socketError }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
