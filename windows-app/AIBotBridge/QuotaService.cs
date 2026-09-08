using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class QuotaService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly object _sync = new();
    private QuotaSnapshot? _snapshot;

    internal QuotaService()
    {
        var cached = SnapshotCache.Load<QuotaSnapshot>("usage-cache.json");
        if (cached is not null)
            _snapshot = new QuotaSnapshot(Stale(cached.Claude), Stale(cached.Codex));
    }

    internal QuotaSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var claudeTask = FetchClaudeAsync(cancellationToken);
        var codexTask = FetchCodexAsync(cancellationToken);
        await Task.WhenAll(claudeTask, codexTask);

        lock (_sync)
        {
            var claude = claudeTask.Result ?? Stale(_snapshot?.Claude);
            var codex = MergeCodex(codexTask.Result, _snapshot?.Codex);
            if (claude is null && codex is null) return;
            _snapshot = new QuotaSnapshot(claude, codex);
            if (claude?.Stale == false || codex?.Stale == false)
                SnapshotCache.Save("usage-cache.json", _snapshot);
        }
    }

    internal static ProviderQuotaSnapshot? MergeCodex(ProviderQuotaSnapshot? fresh, ProviderQuotaSnapshot? previous)
    {
        if (fresh is null) return Stale(previous);
        if (fresh.ResetCreditsAvailable is > 0 && fresh.ResetCreditsAvailable == previous?.ResetCreditsAvailable &&
            fresh.ResetCreditExpiresAt.Count == 0 && previous.ResetCreditExpiresAt.Count > 0)
            return fresh with { ResetCreditExpiresAt = previous.ResetCreditExpiresAt.ToArray(), Stale = true };
        return fresh;
    }

    internal static ProviderQuotaSnapshot ParseClaude(string json, string? credentialPlan = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var fiveHour = root.TryGetProperty("five_hour", out var primary) ? primary : default;
        var sevenDay = root.TryGetProperty("seven_day", out var weekly) ? weekly : default;
        return new ProviderQuotaSnapshot(
            "claude",
            String(root, "plan_type") ?? String(root, "subscription_type") ?? credentialPlan,
            Number(fiveHour, "utilization"), Date(fiveHour, "resets_at"),
            Number(sevenDay, "utilization"), Date(sevenDay, "resets_at"),
            null, Array.Empty<long>(), DateTimeOffset.UtcNow, false);
    }

    internal static ProviderQuotaSnapshot ParseCodex(string usageJson, string? creditsJson = null,
        string? credentialPlan = null)
    {
        using var document = JsonDocument.Parse(usageJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("rate_limit", out var rateLimit))
            throw new JsonException("rate_limit is missing.");

        double? primaryPercent = null, weeklyPercent = null;
        DateTimeOffset? primaryReset = null, weeklyReset = null;
        foreach (var name in new[] { "primary_window", "secondary_window" })
        {
            if (!rateLimit.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
                continue;
            var seconds = Number(window, "limit_window_seconds");
            var isWeekly = seconds is >= 172800;
            if (isWeekly)
            {
                weeklyPercent = Number(window, "used_percent");
                weeklyReset = UnixDate(window, "reset_at");
            }
            else
            {
                primaryPercent = Number(window, "used_percent");
                primaryReset = UnixDate(window, "reset_at");
            }
        }

        int? available = null;
        var expirations = new List<long>();
        if (root.TryGetProperty("rate_limit_reset_credits", out var embedded))
            available = Integer(embedded, "available_count");
        if (!string.IsNullOrWhiteSpace(creditsJson))
        {
            using var creditsDocument = JsonDocument.Parse(creditsJson);
            var credits = creditsDocument.RootElement;
            available = Integer(credits, "available_count") ?? available;
            if (credits.TryGetProperty("credits", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (!string.Equals(String(row, "status"), "available", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var expires = Date(row, "expires_at");
                    if (expires.HasValue) expirations.Add(expires.Value.ToUnixTimeSeconds());
                }
                expirations.Sort();
            }
        }

        return new ProviderQuotaSnapshot(
            "codex", String(root, "plan_type") ?? credentialPlan,
            primaryPercent, primaryReset, weeklyPercent, weeklyReset,
            available, expirations, DateTimeOffset.UtcNow, false);
    }

    private static async Task<ProviderQuotaSnapshot?> FetchClaudeAsync(CancellationToken cancellationToken)
    {
        var credential = ReadClaudeCredential();
        if (credential is null) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential.Value.Token);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("User-Agent", "claude-code/2.1.0");
        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return ParseClaude(await response.Content.ReadAsStringAsync(cancellationToken), credential.Value.Plan);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static async Task<ProviderQuotaSnapshot?> FetchCodexAsync(CancellationToken cancellationToken)
    {
        var credential = ReadCodexCredential();
        if (credential is null) return null;
        try
        {
            var usage = await SendCodexAsync(
                "https://chatgpt.com/backend-api/wham/usage", credential.Value, cancellationToken);
            if (usage is null) return null;
            var credits = await SendCodexAsync(
                "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits",
                credential.Value, cancellationToken);
            return ParseCodex(usage, credits, credential.Value.Plan);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> SendCodexAsync(
        string url, (string Token, string? AccountId, string? Plan) credential,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential.Token);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "AI-bot/0.1");
        if (!string.IsNullOrWhiteSpace(credential.AccountId))
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", credential.AccountId);
        using var response = await Http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
    }

    private static (string Token, string? Plan)? ReadClaudeCredential()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var oauth = document.RootElement.GetProperty("claudeAiOauth");
            var token = String(oauth, "accessToken");
            return string.IsNullOrWhiteSpace(token) ? null
                : (token, String(oauth, "subscriptionType") ?? String(oauth, "subscription_type"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static (string Token, string? AccountId, string? Plan)? ReadCodexCredential()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "auth.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var tokens = document.RootElement.GetProperty("tokens");
            var access = String(tokens, "access_token");
            if (string.IsNullOrWhiteSpace(access) || JwtExpired(access)) return null;
            var accountId = String(tokens, "account_id");
            string? plan = null;
            var idToken = String(tokens, "id_token");
            if (!string.IsNullOrWhiteSpace(idToken) && TryReadJwt(idToken, out var claims))
            {
                if (claims.TryGetProperty("https://api.openai.com/auth", out var auth))
                {
                    accountId ??= String(auth, "chatgpt_account_id");
                    plan = String(auth, "chatgpt_plan_type");
                }
            }
            return (access, accountId, plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool JwtExpired(string token)
    {
        if (!TryReadJwt(token, out var claims)) return false;
        var expires = Number(claims, "exp");
        return expires.HasValue && expires.Value <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
    }

    private static bool TryReadJwt(string token, out JsonElement claims)
    {
        claims = default;
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return false;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            claims = document.RootElement.Clone();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    private static ProviderQuotaSnapshot? Stale(ProviderQuotaSnapshot? value) =>
        value is null ? null : value with { Stale = true };

    private static string? String(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static int? Integer(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.TryGetInt32(out var number) ? number : null;

    private static DateTimeOffset? Date(JsonElement parent, string name) =>
        DateTimeOffset.TryParse(String(parent, name), out var value) ? value : null;

    private static DateTimeOffset? UnixDate(JsonElement parent, string name)
    {
        var value = Number(parent, name);
        return value.HasValue ? DateTimeOffset.FromUnixTimeSeconds((long)value.Value) : null;
    }
}
