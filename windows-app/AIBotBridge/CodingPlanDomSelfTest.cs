using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AIBotBridge;

internal static class CodingPlanDomSelfTest
{
    // Exercise the actual extraction JavaScript in Chromium against synthetic
    // console markup. This never navigates to a vendor or opens a visible window.
    internal static void Run()
    {
        using var form = new Form { Size = new Size(1060, 720), ShowInTaskbar = false };
        using var web = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(web);
        _ = form.Handle; _ = web.Handle;
        var task = RunAsync(web);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (!task.IsCompleted && DateTimeOffset.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        if (!task.IsCompleted) throw new TimeoutException("Offline plan DOM test did not finish.");
        task.GetAwaiter().GetResult();
        Console.WriteLine("CODING_PLAN_DOM_SELF_TEST_OK actual WebView2 extraction; synthetic quotas/remaining/zero/hidden/wallet/ambiguity; no vendor navigation");
    }

    private static async Task RunAsync(WebView2 web)
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "coding-plan-dom-test"));
        await web.EnsureCoreWebView2Async(environment);
        async Task<JsonElement> Extract(string html)
        {
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnNavigation(object? sender, CoreWebView2NavigationCompletedEventArgs e) => loaded.TrySetResult(e.IsSuccess);
            web.NavigationCompleted += OnNavigation;
            try
            {
                web.NavigateToString("<!doctype html><html><body><h1>腾讯 Coding Plan</h1>" + html + "</body></html>");
                if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("Offline fixture navigation failed.");
                using var doc = JsonDocument.Parse(await web.CoreWebView2.ExecuteScriptAsync(CodingPlanPage.Script));
                return doc.RootElement.Clone();
            }
            finally { web.NavigationCompleted -= OnNavigation; }
        }
        const string valid = """
            <section>每5小时 已用 <div role="progressbar">0%</div></section>
            <section>每周 已使用 <div role="progressbar">20%</div></section>
            <section>套餐额度 剩余 <div role="progressbar">40%</div></section>
            <section style="display:none">每周 已用 <div role="progressbar">95%</div></section>
            <section>云余额 <div role="progressbar">99%</div></section>
            """;
        var rows = await Extract(valid);
        var q = CodingPlanPage.Parse("tencent", "coding", rows);
        if (q.PrimaryPercent != 0 || q.WeeklyPercent != 20 || q.PlanPercent != 60) throw new InvalidOperationException("DOM usage semantics changed.");
        if (rows.ToString().Contains("云余额") || rows.ToString().Contains("95")) throw new InvalidOperationException("Hidden or wallet data leaked into quota capture.");
        var ambiguous = await Extract(valid + "<section>每周 已用 <div role='progressbar'>30%</div></section>");
        try { CodingPlanPage.Parse("tencent", "coding", ambiguous); throw new InvalidOperationException("Conflicting progress accepted."); }
        catch (JsonException) { }
        var unrelated = await Extract("<section>账户余额 CNY 5 <div role='progressbar'>50%</div></section><section>每周 已过期 已用 <div role='progressbar'>60%</div></section>");
        if (unrelated.GetArrayLength() != 0) throw new InvalidOperationException("Wallet or expired subscription accepted.");
    }
}
