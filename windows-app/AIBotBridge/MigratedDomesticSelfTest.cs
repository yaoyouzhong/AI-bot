using System.Text.Json;

namespace AIBotBridge;

internal static class MigratedDomesticSelfTest
{
    internal static void Run()
    {
        using var mini = JsonDocument.Parse("""
          {"models":[{"model_name":"video","weekly_total":100,"weekly_used":99},
                     {"model_name":"general","weekly_total":100,"weekly_used":35,"interval_total":100,"interval_used":20,"weekly_remains_time":3600000,"remains_time":"1800000"}]}
          """);
        var parsed = MigratedDomestic.DomesticQuotaAuthForm.FindMiniMaxUsage(mini.RootElement);
        if (parsed?.WeeklyPct != 35 || parsed?.FiveHourPct != 20 || parsed?.FiveHourResetAt is null ||
            Math.Abs((parsed.Value.FiveHourResetAt.Value - DateTimeOffset.Now).TotalMinutes - 30) > 0.1)
            throw new InvalidOperationException("Migrated MiniMax model selection/millisecond countdown failed.");
        using var deepseek = JsonDocument.Parse("""
          {"balance_infos":[{"currency":"USD","total_balance":"99.00"},{"currency":"CNY","total_balance":"28.50"}],
           "total_costs":[{"currency":"USD","amount":"5"},{"currency":"CNY","amount":"9.75"}]}
          """);
        var balance = MigratedDomestic.DomesticQuotaAuthForm.FindDeepSeekBalance(deepseek.RootElement);
        if (balance?.Currency != "CNY" || balance?.Total != 28.5 || MigratedDomestic.DomesticQuotaAuthForm.FindDeepSeekUsedCost(deepseek.RootElement, "CNY") != 9.75)
            throw new InvalidOperationException("Migrated DeepSeek currency selection failed.");
        using var form = new MigratedDomestic.DomesticQuotaAuthForm(new MigratedDomestic.DomesticQuotaService(), hideOnUserClose: false, initializeBrowser: false);
        form.WindowState = FormWindowState.Normal;
        form.Size = new Size(1180, 800);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.Show();
        Application.DoEvents();
        form.PerformLayout();
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "domestic-auth-layout.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); image.Save(output);
        form.Hide();
        Console.WriteLine("MIGRATED_DOMESTIC_SELF_TEST_OK model-selection/countdown/currency/layout; browser not shown or logged in");
    }
}
