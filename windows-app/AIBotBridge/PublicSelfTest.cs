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
            // A fresh install must show the built-in pet without creating private resources.
            foreach (var mode in new[] { "claude", "codex", "pet" })
            {
                var sample=status with {Codex=new("working",0),Claude=new("working",0),CapturedAt=DateTimeOffset.FromUnixTimeMilliseconds(0)};
                using var first=MirrorForm.RenderSnapshot(sample,mode);
                using var next=MirrorForm.RenderSnapshot(sample with {CapturedAt=DateTimeOffset.FromUnixTimeMilliseconds(240)},mode);
                int body=0,changed=0;
                for(int y=64;y<170;y++) for(int x=91;x<153;x++)
                {if(first.GetPixel(x,y).ToArgb()==Color.Cyan.ToArgb())body++;if(first.GetPixel(x,y)!=next.GetPixel(x,y))changed++;}
                if(body<500 || changed==0)throw new InvalidOperationException("Fresh-profile default pet missing or not animated: "+mode);
            }
            if(PetAnimationStore.Shared.AllResources().Count!=0)throw new InvalidOperationException("Built-in pet must not write imported resources.");
            Console.WriteLine("BUILTIN_PET_FRESH_PROFILE_OK claude/codex/pet; animated; no imported resource");
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
