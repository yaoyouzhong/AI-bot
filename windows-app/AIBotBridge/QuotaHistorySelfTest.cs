namespace AIBotBridge;

internal static class QuotaHistorySelfTest
{
    internal static void Run()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, "artifacts", "quota-history-test-" + Guid.NewGuid().ToString("N"));
        var history = new QuotaHistory(folder);
        var at = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);
        var reset = at.AddDays(1);
        ProviderQuotaSnapshot Q(DateTimeOffset time, double value, DateTimeOffset? expiry = null, bool stale = false, string plan = "PRO") =>
            new("codex", plan, 30, time.AddHours(5), value, expiry ?? reset, null, [], time, stale);
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        history.Record(Q(at, 20)); history.Record(Q(at.AddMinutes(2), 28)); history.Record(Q(at.AddMinutes(2), 35));
        history.Record(Q(at.AddMinutes(3), 90, stale: true));
        var day = QuotaHistory.Daily(history.Read(), new(2026,9,10), 7, TimeZoneInfo.Utc)[^1];
        Require(day.Growth == 8 && day.Samples == 2 && day.Partial, "Growth/duplicate/stale/baseline failed.");
        Require(QuotaHistory.Daily(history.Read(), new(2026,9,10), 7, TimeZoneInfo.Utc)[0].Growth is null, "Missing day became zero.");
        var restart = new QuotaHistory(folder); Require(restart.Read().Length == 2, "History persistence failed.");
        restart.Record(Q(at.AddMinutes(20), 60)); restart.Record(Q(at.AddMinutes(22), 62));
        Require(QuotaHistory.Daily(restart.Read(), new(2026,9,10), 1, TimeZoneInfo.Utc)[0].Growth == 10, "Offline gap counted as exact daily growth.");
        var verifiedGap = QuotaHistory.Daily(restart.Read().Select(x => x with { AccountFingerprint = "same-account" }).ToArray(),
            new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(verifiedGap.Growth == 42 && verifiedGap.Partial && verifiedGap.Gaps == 1,
            "Same-account same-window cumulative use lost across a pause.");
        var delayedPoll = QuotaHistory.Daily([
            new(at,"PRO",25,reset,null,null,"same-account"),
            new(at.AddSeconds(309),"PRO",26,reset,null,null,"same-account")],new(2026,9,10),1,TimeZoneInfo.Utc)[0];
        Require(delayedPoll.Growth == 1 && delayedPoll.Partial, "Five-minute polling jitter discarded observable use.");
        restart.Record(Q(at.AddMinutes(24), 5)); restart.Record(Q(at.AddMinutes(26), 7));
        Require(QuotaHistory.Daily(restart.Read(), new(2026,9,10), 1, TimeZoneInfo.Utc)[0].Growth == 12, "Decline/new baseline failed.");
        var before = new QuotaObservation(at, "PRO", 95, at.AddMinutes(1), 0, null);
        var after = new QuotaObservation(at.AddMinutes(2), "PRO", 3, at.AddDays(7), 3, null);
        var resetDay = QuotaHistory.Daily([before, after], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(resetDay.Growth == 3 && resetDay.Resets == 1, "Reset segmentation failed.");
        Require(resetDay.Partial, "Unobserved pre-reset use marked complete.");
        var jitter = QuotaHistory.Daily([
            before with { Weekly = 20, WeeklyReset = at.AddDays(7) },
            after with { Weekly = 23, WeeklyReset = at.AddDays(7).AddSeconds(1) }
        ], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(jitter.Growth == 3 && jitter.UncertainResets == 0 && jitter.Resets == 0, "Reset timestamp jitter discarded valid use.");
        foreach (var seconds in new[] { -60, -3, 3, 60 })
        foreach (var usage in new[] { 68.0, 70.0, 5.0 })
        {
            var baseline = before with { Weekly = 68, WeeklyReset = at.AddDays(7) };
            var sample = after with { Weekly = usage, WeeklyReset = baseline.WeeklyReset!.Value.AddSeconds(seconds) };
            var observed = QuotaHistory.Daily([baseline, sample], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
            Require(observed.Resets == 0 && observed.UncertainResets == (usage < 68 ? 1 : 0) &&
                observed.Growth == (usage < 68 ? (double?)null : usage - 68), "Jitter tolerance hid a decline or discarded stable/increasing usage.");
        }
        var roundTrip = QuotaHistory.Daily([
            before with { Weekly = 68, WeeklyReset = at.AddDays(7) },
            after with { Weekly = 68, WeeklyReset = at.AddDays(7).AddSeconds(3) },
            after with { At = at.AddMinutes(4), Weekly = 68, WeeklyReset = at.AddDays(7) }
        ], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(roundTrip.Growth == 0 && roundTrip.UncertainResets == 0 && roundTrip.Resets == 0, "Today's three-second roundtrip counted as resets.");
        var shifted = QuotaHistory.Daily([
            before with { Weekly = 68, WeeklyReset = at.AddDays(7) },
            after with { Weekly = 70, WeeklyReset = at.AddDays(7).AddSeconds(61) }
        ], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(shifted.Growth is null && shifted.UncertainResets == 1, "Substantial deadline change silently counted.");
        var offlineReset = QuotaHistory.Daily([before, after with { At = at.AddMinutes(20) }], new(2026,9,10), 1, TimeZoneInfo.Utc)[0];
        Require(offlineReset.Gaps == 1 && offlineReset.Resets == 0 && offlineReset.UncertainResets == 0 && offlineReset.Growth is null, "Offline gap reported as an observed reset.");
        var manual = after with { At=at.AddMinutes(2), WeeklyReset=before.WeeklyReset };
        var uncertain = QuotaHistory.Daily([before,manual],new(2026,9,10),1,TimeZoneInfo.Utc)[0];
        Require(uncertain.UncertainResets==1 && uncertain.Growth is null && uncertain.Partial, "Unconfirmed reset fabricated consumption.");
        var start = new DateTimeOffset(2026,9,8,0,0,0,TimeSpan.Zero);
        var complete = Enumerable.Range(0,721).Select(i=>new QuotaObservation(start.AddMinutes(i*2),"PRO",20+Math.Min(i,719)/71.9,start.AddDays(7),null,null,"test-account")).ToArray();
        var full = QuotaHistory.Daily(complete,new(2026,9,10),3,TimeZoneInfo.Utc);
        Require(full[0].Growth==10 && !full[0].Partial && full[1].Growth==0,"Midnight sample assigned to wrong day or counted twice.");
        Require(QuotaHistory.AverageRecorded(full,new(2026,9,10))==(10.0,1),"Missing dates entered daily average.");
        Require(QuotaHistory.Daily(complete.Skip(1).ToArray(),new(2026,9,10),3,TimeZoneInfo.Utc)[0].Partial,"Missing midnight baseline accepted.");
        Require(QuotaHistory.Daily(complete.Take(720).ToArray(),new(2026,9,10),3,TimeZoneInfo.Utc)[0].Partial,"Missing closing midnight accepted.");
        var jittered = Enumerable.Range(-1, 723).Select(i => new QuotaObservation(start.AddMinutes(i*2).AddSeconds(7),
            "PRO",20+Math.Clamp(i,0,719)/71.9,start.AddDays(7),null,null,"test-account")).ToArray();
        var jitteredDay = QuotaHistory.Daily(jittered,new(2026,9,10),3,TimeZoneInfo.Utc)[0];
        Require(jitteredDay.Growth == 10 && !jitteredDay.Partial, "Flat midnight boundaries with normal polling jitter never become complete.");
        var midnightIncrease = jittered.Select((x,i) => i==0 ? x with {Weekly=19} : x).ToArray();
        var crossingDay=QuotaHistory.Daily(midnightIncrease,new(2026,9,10),3,TimeZoneInfo.Utc)[0];
        Require(!crossingDay.Partial && crossingDay.Growth==11,
            "Short cross-midnight increment not assigned once to the later sample's date.");
        var switched=complete.Select((x,i)=>i>360?x with{AccountFingerprint="another-account"}:x).ToArray();
        Require(QuotaHistory.Daily(switched,new(2026,9,10),3,TimeZoneInfo.Utc)[0].Partial,"Account switch treated as complete use.");
        Require(QuotaHistory.Daily([before, after with {Plan="PLUS"}], new(2026,9,10), 1, TimeZoneInfo.Utc)[0].Growth is null, "Plan change compared.");
        var midnight = at.Date.AddDays(1);
        Require(QuotaHistory.Daily([before with {At=new DateTimeOffset(midnight.AddMinutes(-1), TimeSpan.Zero)}, after with {At=new DateTimeOffset(midnight.AddMinutes(1), TimeSpan.Zero)}], new(2026,9,11), 1, TimeZoneInfo.Utc)[0].Growth is null, "Cross-day usage falsely assigned.");
        var broken = Path.Combine(folder,"broken"); Directory.CreateDirectory(broken); var path=Path.Combine(broken,"codex-quota-history.json");File.WriteAllText(path,"[null]");
        var damaged = new QuotaHistory(broken); damaged.Record(Q(at,20)); Require(damaged.Error is not null && File.ReadAllText(path)=="[null]", "Damaged evidence overwritten.");
        var retryFolder = Path.Combine(folder, "retry");
        var retry = new QuotaHistory(retryFolder);
        var blockedTemporary = Path.Combine(retryFolder, "codex-quota-history.json.tmp");
        Directory.CreateDirectory(blockedTemporary);
        retry.Record(Q(at, 20));
        Require(retry.Error is not null, "Save failure not surfaced.");
        Directory.Delete(blockedTemporary);
        retry.Record(Q(at.AddMinutes(2), 22));
        Require(retry.Error is null && new QuotaHistory(retryFolder).Read().Length == 2, "Transient save failure permanently stopped history persistence.");
        var ui = new QuotaHistory(Path.Combine(folder,"ui"));
        var today = new DateTimeOffset(DateTime.Today.AddHours(10));
        for (int d = 6; d >= 0; d--) { ui.Record(Q(today.AddDays(-d), 20, today.AddDays(7))); ui.Record(Q(today.AddDays(-d).AddMinutes(2), 22 + d, today.AddDays(7))); }
        using var form = new QuotaTrendForm(ui);
        form.Show(); Application.DoEvents();
        var table = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single().TabPages[1].Controls.OfType<DataGridView>().Single();
        Require(table.Rows.Count == 7 && table.Rows.Cast<DataGridViewRow>().All(row => row.Cells[1].Value?.ToString() != "--"), "Known partial-day usage hidden.");
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(folder,"trend.png")); form.Hide();
        foreach (var size in new[] { new Size(680, 520), new Size(1100, 760) })
        foreach (var metric in new[] { 0, 1 })
        foreach (var details in new[] { false, true })
        {
            form.Size = size; form.SetTestView(metric, 1, details); form.Show(); Application.DoEvents();
            using var preview = new Bitmap(form.Width, form.Height); form.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
            preview.Save(Path.Combine(folder, $"trend-{size.Width}-{metric}-{details}.png")); form.Hide();
        }
        Console.WriteLine("QUOTA_HISTORY_SELF_TEST_OK baseline/growth/reset/gap/midnight/stale/duplicate/plan/restart/corrupt/UI; synthetic only");
    }

    internal static async Task CollectionAsync()
    {
        const string usage = """{"plan_type":"pro","rate_limit":{"primary_window":{"limit_window_seconds":604800,"used_percent":42,"reset_at":1791000000}}}""";
        var directory = Path.Combine(Path.GetTempPath(), "aibot-collection-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var failure in new[] { "timeout", "network", "json", "http" })
            {
                var history = new QuotaHistory(Path.Combine(directory, failure));
                Task<string?> Send(string endpoint, CancellationToken ct)
                {
                    if (endpoint == "usage") return Task.FromResult<string?>(usage);
                    if (history.Read().Length != 1) throw new InvalidOperationException("Usage not saved before optional credit request.");
                    return failure switch
                    {
                        "timeout" => Task.FromException<string?>(new TaskCanceledException()),
                        "network" => Task.FromException<string?>(new HttpRequestException()),
                        "json" => Task.FromResult<string?>("broken json"),
                        _ => Task.FromResult<string?>(null)
                    };
                }
                var result = await QuotaService.CollectCodexAsync(Send,"PRO","synthetic-account",history,CancellationToken.None);
                if (result?.WeeklyPercent != 42 || result.Stale || new QuotaHistory(Path.Combine(directory,failure)).Read().Length != 1)
                    throw new InvalidOperationException("Optional credit failure lost valid usage: " + failure);
            }
            Console.WriteLine("QUOTA_COLLECTION_SELF_TEST_OK optional credit timeout/network/malformed/non-success preserve usage immediately");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
}
