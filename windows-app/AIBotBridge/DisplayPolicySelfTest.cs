namespace AIBotBridge;

internal static class DisplayPolicySelfTest
{
    internal static void Run()
    {
        AutoFollowSelfTest.Run();
        var stocksOnly = new DisplayPolicy("stocks",false,15,new[]{"domestic_deepseek","domestic_zhipu"});
        bool Configured(string provider) => provider is "deepseek" or "zhipu";
        Require(QuotaMonitoringPolicy.Monitored(stocksOnly,"alibaba",Configured).Count==0,
            "ESP8266 alone preserves fixed-page quota monitoring.");
        Require(QuotaMonitoringPolicy.Monitored(stocksOnly,"alibaba",Configured,tab5Paired:true).SetEquals(new[]{"deepseek","zhipu"}),
            "Paired TAB5 refreshes configured quotas while ESP8266 stays on stocks with cycling disabled.");
        Require(QuotaMonitoringPolicy.Monitored(stocksOnly,"alibaba",_=>true,tab5Paired:true).SetEquals(new[]{"deepseek","zhipu"}),
            "Previously authorized but unselected providers must not be queried or emit quota warnings.");
        Require(QuotaMonitoringPolicy.Monitored(stocksOnly with {Pages=new[]{"domestic_kimi"}},"alibaba",_=>true,tab5Paired:true).SetEquals(new[]{"kimi"}),
            "Changing the selected pages immediately replaces TAB5 background provider demand.");
        Require(QuotaMonitoringPolicy.Monitored(stocksOnly,"alibaba",_=>false,tab5Paired:true).Count==0,
            "TAB5 must not query accounts that were never configured.");
        var quotaCycle=stocksOnly with {SelectedMode="auto",CycleEnabled=true};
        Require(QuotaMonitoringPolicy.Monitored(quotaCycle,"alibaba",Configured,tab5Paired:true).Count==2,
            "TAB5 and ESP8266 demand must not duplicate provider requests.");
        Require(!QuotaMonitoringPolicy.Monitored(stocksOnly,"alibaba",_=>true,tab5Paired:true).Contains("volcengine"),
            "Unsupported catalog entries must not be scheduled.");
        var webProviders=new[]{"qwen","kimi","deepseek","zhipu"};
        Require(QuotaMonitoringPolicy.OrderWeb(webProviders,true).SequenceEqual(new[]{"zhipu","deepseek","qwen","kimi"}),
            "TAB5 overview wallets refresh before older configured accounts at startup, without dropping providers.");
        Require(QuotaMonitoringPolicy.OrderWeb(webProviders,false).SequenceEqual(webProviders),
            "ESP8266-only web scheduling order stays unchanged.");
        Require(QuotaMonitoringPolicy.OrderWeb(new[]{"kimi"},true).SequenceEqual(new[]{"kimi"}),
            "Web priority must not add an unconfigured overview provider.");
        var folder = Path.Combine(Environment.CurrentDirectory, "artifacts", "policy-test-" + Guid.NewGuid().ToString("N"));
        var first = BridgeSettings.CreatePublicSelfTestSettings();
        var second = BridgeSettings.CreatePublicSelfTestSettings();
        void Save(BridgeSettings settings, Dictionary<string, string> values)
        {
            if (!settings.SaveEditable(values, out var error, folder)) throw new InvalidOperationException(error);
        }
        Save(first, new() { ["display_mode"] = "weather", ["display_cycle_pages"] = "stock,net,domestic:kimi,invalid,stock", ["display_cycle_interval_seconds"] = "30" });
        Save(second, new() { ["weather_animation"] = "pet" });
        var loaded = BridgeSettings.LoadCurrentFromDirectory(folder);
        var policy = DisplayModes.Load(loaded);
        Require(policy.SelectedMode == "weather" && policy.IntervalSeconds == 30, "Stale settings overwrote display selection.");
        Require(policy.Pages.SequenceEqual(new[] { "stocks", "system", "domestic_kimi" }), "Legacy aliases/order/deduplication.");
        Require(loaded.Get("weather_animation") == "pet", "Independent settings change lost.");
        var status = new StatusSnapshot(1, "00:00:00", 0, 0, DateTimeOffset.UnixEpoch,
            new("idle", 0), new("idle", 0), DisplayPolicy: policy);
        for (var index = 0; index < policy.Pages.Length; index++)
            Require(DisplayModes.Resolve(status with { EpochUtc = index * 30 }, "auto") == policy.Pages[index], "Cycle order/interval.");
        Require(DisplayModes.Resolve(status with { Codex = new("working", 0) }, "auto") == "stocks", "Explicit cycle must continue while Codex works.");
        Require(DisplayModes.Resolve(status with { Claude = new("working", 0) }, "auto") == "stocks", "Explicit cycle must continue while Claude works.");
        var follow = status with { DisplayPolicy = policy with { CycleEnabled = false } };
        Require(DisplayModes.Resolve(follow with { Codex = new("working", 0) }, "auto") == "codex", "Smart follow Codex priority.");
        Require(DisplayModes.Resolve(follow with { Claude = new("working", 0) }, "auto") == "claude", "Smart follow Claude priority.");
        foreach(var bothWorking in new[]{true,false}) {
            var sample=follow with {Codex=new(bothWorking?"working":"idle",0),Claude=new(bothWorking?"working":"idle",0),DisplayPolicy=policy with{CycleEnabled=false,CycleStartedAt=100}};
            int seconds=bothWorking?2:6;
            Require(DisplayModes.Resolve(sample with{EpochUtc=100},"auto")=="claude"&&DisplayModes.Resolve(sample with{EpochUtc=100+seconds},"auto")=="codex"&&DisplayModes.Resolve(sample with{EpochUtc=100+2*seconds},"auto")=="claude","Both-active/idle alternation");
        }
        Require(DisplayModes.Resolve(follow with{Codex=new("working",0),DomesticActivity=new("kimi","working",false,new Dictionary<string,LocalProviderUsage>())},"auto")=="domestic_kimi","Domestic activity precedes single tool follow.");
        Require(DisplayModes.Resolve(status with { EpochUtc = 173, DisplayPolicy = policy with { CycleStartedAt = 173 } }, "auto") == "stocks", "Cycle begins at its first page, not epoch modulo.");
        Require(DisplayModes.Resolve(status with { EpochUtc = 203, DisplayPolicy = policy with { CycleStartedAt = 173 } }, "auto") == "system", "Cycle advances relative to enable time.");
        foreach (var page in DisplayModes.Pages.Append(("screen", "screensaver")))
            Require(DisplayModes.Resolve(status with { Codex = new("working", 0) }, page.Item2) == page.Item2, "Manual mode must win.");
        var attention=status with {Codex=new("working",0,NeedsInput:true)};
        Require(DisplayModes.Resolve(attention,"auto")=="codex","Input request interrupts automatic cycling.");
        foreach (var alert in new[] {
            attention, status with { Claude = new("idle",0,NeedsInput:true) },
            status with { Codex = new("idle",0,CompletionActive:true) },
            status with { DomesticActivity = new("kimi","idle",true,new Dictionary<string,LocalProviderUsage>()) }
        })
            foreach (var page in DisplayModes.Pages.Append(("screen", "screensaver")))
                Require(DisplayModes.Resolve(alert,page.Item2)==page.Item2,"Alerts must not override manual selection.");
        Require(DisplayModes.Resolve(status with {Codex=new("idle",0,CompletionActive:true)},"auto")=="codex","Completion still navigates in auto mode.");
        Require(DisplayModes.Resolve(attention,"screensaver")=="screensaver","Explicit screensaver preview is not interrupted.");
        var now = DateTimeOffset.UnixEpoch;
        Require(UsagePageRenderer.Reset(status, now.AddSeconds(61)) == "2m", "Reset rounds up.");
        Require(UsagePageRenderer.Reset(status, now.AddMinutes(61)) == "1h 1m", "Hour countdown.");
        Require(UsagePageRenderer.Reset(status, now.AddHours(25)) == "1d 1h", "Day countdown.");
        Require(UsagePageRenderer.Reset(status, now.AddSeconds(-1)) == "0m", "Expired reset clamps.");
        Require(UsagePageRenderer.Reset(status, null) == "", "Unknown reset must not invent time.");
        using var usageJson = System.Text.Json.JsonDocument.Parse("""
            {"Claude":{"PrimaryPct":25,"FetchedAt":"2026-09-08T10:00:00Z","PrimaryResetMin":120},
             "Codex":{"WeeklyPct":40,"ResetCreditsAvailable":3,"ResetCreditExpiresAtList":[1800000000,1800000000,"invalid"],"token":"do-not-copy"}}
            """);
        var usage = LegacyDisplayCache.ParseUsage(usageJson.RootElement);
        Require(usage.Claude?.PrimaryResetsAt == DateTimeOffset.Parse("2026-09-08T12:00:00Z") && usage.Claude.Stale, "Legacy relative reset uses fetched time, not migration time.");
        Require(usage.Codex?.ResetCreditExpiresAt.Count == 2 && usage.Codex.ResetCreditsAvailable == 3, "Duplicate and unknown credit dates.");
        using var domesticJson = System.Text.Json.JsonDocument.Parse("""
            {"QwenPlanPct":12.5,"KimiWeeklyPct":42,"MiniMaxWeeklyPct":17,"DeepSeekBalance":28.5,"DeepSeekCurrency":"CNY","Cookie":"do-not-copy"}
            """);
        var domestic = LegacyDisplayCache.ParseDomestic(domesticJson.RootElement);
        Require(domestic.Alibaba?.PlanPercent == 12.5 && domestic.Alibaba.WeeklyPercent is null && domestic.Kimi?.WeeklyPercent == 42 && domestic.MiniMax?.WeeklyPercent == 17 && domestic.DeepSeek?.Balance == 28.5, "Provider and window isolation in cache migration.");
        Require(!System.Text.Json.JsonSerializer.Serialize(new { usage, domestic }).Contains("do-not-copy"), "Non-display fields leaked from legacy cache.");
        Console.WriteLine("LEGACY_DISPLAY_CACHE_SELF_TEST_OK provider-isolation/reset-time/duplicate-dates/no-extra-fields");
        Console.WriteLine("DISPLAY_POLICY_SELF_TEST_OK modes=13 aliases/order/interval/manual-priority/settings-merge/countdown");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
