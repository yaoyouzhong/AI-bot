namespace AIBotBridge;

internal static class PublicSelfTest
{
    internal static void Run()
    {
        if (!AppPaths.IsPublicSelfTest) throw new InvalidOperationException("Fresh profile required.");
        // No refresh workers, serial connection, production HTTP port or browser login.
        using (var runtime = new BridgeRuntime(startRefresh: false))
        {
            var status = runtime.Capture();
            if (status.Version != 1 || status.Codex.State != "offline" || status.Claude.State != "offline"
                || status.Weather is not null || status.Stocks is not null
                || status.Quotas?.Claude is not null || status.Quotas?.Codex is not null
                || status.DomesticQuotas is { } q && new[] { q.Alibaba, q.Kimi, q.MiniMax, q.DeepSeek, q.Zhipu }.Any(x => x is not null)
                || PetAnimationStore.Shared.AllResources().Count != 0)
                throw new InvalidOperationException("Empty profile unexpectedly contains user state.");
            if (MigratedWeather.CredentialStore.Read("AI-bot/QWeatherApiKey") != ""
                || AppPaths.GetProviderEnvironmentVariable("MINIMAX_API_KEY") is not null)
                throw new InvalidOperationException("Provider credentials are not isolated.");
            foreach (var mode in new[] { "claude", "codex", "quotas", "weather", "stocks", "system", "pet", "domestic_zhipu" })
                using (var page = MirrorForm.RenderSnapshot(status, mode))
                    if (page.Width != 240 || page.Height != 240) throw new InvalidOperationException("Empty page size changed.");
            _ = runtime.Resources();
        }
        DataSourceSelfTest.Run();
        QuotaHistorySelfTest.Run();
        CodexLifecycleSelfTest.Run();
        DisplayPolicySelfTest.Run();
        PetAnimationSelfTest.Run();
        PetSelectionSelfTest.Run();
        MigrationRegressionSelfTest.RunAsync().GetAwaiter().GetResult();
        ZhipuSelfTest.Run();
        CycleSettingsForm.VerifyLayout();
        MirrorSelfTest.Run(Path.Combine(Environment.CurrentDirectory, "artifacts", "public-mirror.png"));
        Console.WriteLine("PUBLIC_SELF_TEST_OK fresh profile, empty pages, fixture regressions; no real credentials, USB or production bridge");
    }
}
