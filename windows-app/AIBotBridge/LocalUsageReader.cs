using System.Text.Json;

namespace AIBotBridge;

internal sealed record LocalProviderUsage(long TokensToday, long LastActivity, string Model);
internal sealed class LocalUsageReader
{
    private sealed record Cache(long Length, DateTime Modified, DateTime Day, Dictionary<string, LocalProviderUsage> Values);
    private readonly Dictionary<string, Cache> _cache = new(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyDictionary<string, LocalProviderUsage> Capture(string claudeRoot, string codexRoot)
    {
        var totals = new Dictionary<string, LocalProviderUsage>();
        foreach (var (root, codex) in new[] { (claudeRoot, false), (codexRoot, true) })
        {
            if (!Directory.Exists(root)) continue;
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var path in Directory.EnumerateFiles(root, "*.jsonl", options))
            {
                try
                {
                    var file = new FileInfo(path);
                    if (file.LastWriteTime < DateTime.Today) continue;
                    if (!_cache.TryGetValue(path, out var cached) || cached.Length != file.Length || cached.Modified != file.LastWriteTimeUtc || cached.Day != DateTime.Today)
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(stream);
                        var lines = new List<string>(); string? line;
                        // Only metadata-bearing lines are retained for this scan; never log conversation text.
                        while ((line = reader.ReadLine()) is not null)
                            if (line.Length <= 2_000_000 && line.Contains(codex ? "token_count" : "usage", StringComparison.Ordinal)) lines.Add(line);
                        var values = Parse(lines, codex, DateTimeOffset.Now);
                        cached = new(file.Length, file.LastWriteTimeUtc, DateTime.Today, values); _cache[path] = cached;
                    }
                    foreach (var (provider, value) in cached.Values)
                    {
                        var old = totals.GetValueOrDefault(provider, new(0,0,""));
                        totals[provider] = new(old.TokensToday + value.TokensToday, Math.Max(old.LastActivity,value.LastActivity), value.LastActivity >= old.LastActivity ? value.Model : old.Model);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        return totals;
    }
    internal static Dictionary<string, LocalProviderUsage> Parse(IEnumerable<string> lines, bool codex, DateTimeOffset now)
    {
        var result = new Dictionary<string, LocalProviderUsage>();
        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line); var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!DateTimeOffset.TryParse(Text(root,"timestamp"), out var time) || time.LocalDateTime.Date != now.LocalDateTime.Date || time > now.AddMinutes(1)) continue;
                if (codex)
                {
                    if (!root.TryGetProperty("payload",out var payload) || Text(payload,"type") != "token_count" || !payload.TryGetProperty("info",out var info) || info.ValueKind != JsonValueKind.Object || !info.TryGetProperty("total_token_usage",out var usage)) continue;
                    var old=result.GetValueOrDefault("codex",new(0,0,""));
                    result["codex"] = new(Math.Max(old.TokensToday,Number(usage,"total_tokens")),Math.Max(old.LastActivity,time.ToUnixTimeSeconds()),"");
                    continue;
                }
                if (!root.TryGetProperty("message",out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("usage",out var data)) continue;
                var model=Text(message,"model") ?? ""; var provider=Provider(model); if(provider.Length==0)continue;
                long tokens=Number(data,"input_tokens")+Number(data,"output_tokens")+Number(data,"cache_creation_input_tokens")+Number(data,"cache_read_input_tokens");
                var previous=result.GetValueOrDefault(provider,new(0,0,""));
                result[provider]=new(previous.TokensToday+tokens,Math.Max(previous.LastActivity,time.ToUnixTimeSeconds()),time.ToUnixTimeSeconds()>=previous.LastActivity?model:previous.Model);
            }
            catch (JsonException) { }
        }
        return result;
    }
    internal static string Provider(string model)
    {
        var m=model.ToLowerInvariant().Split('/').Last();
        if(m.StartsWith("claude-"))return "claude";
        if(m.StartsWith("qwen"))return "alibaba";
        if(m.StartsWith("mimo")||m.Contains("xiaomi"))return "xiaomi";
        if(m.StartsWith("kimi")||m.StartsWith("moonshot")||m=="k3")return "kimi";
        if(m.StartsWith("minimax")||m.StartsWith("abab"))return "minimax";
        if(m.StartsWith("deepseek"))return "deepseek";
        return "";
    }
    private static string? Text(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static long Number(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var number)?Math.Max(0,number):0;
}
