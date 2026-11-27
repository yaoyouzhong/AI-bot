using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// A deliberately separate browser adapter. These are our normalized display
// rows, not invented vendor API response fields. No credentials or DOM text are saved.
internal static class CodingPlanPage
{
    internal static bool Allows(string provider, string product, string url)
    {
        if (!CodingPlanSources.IsPlan(product) || !Uri.TryCreate(url, UriKind.Absolute, out var u)
            || u.Scheme != "https" || !u.IsDefaultPort) return false;
        var route = u.AbsolutePath + u.Fragment;
        if (Regex.IsMatch(route, "login|signin", RegexOptions.IgnoreCase)) return false;
        return provider switch
        {
            "zhipu" => u.Host is "bigmodel.cn" or "www.bigmodel.cn" && route.Contains("coding-plan", StringComparison.OrdinalIgnoreCase),
            "qwen" => u.Host == "bailian.console.aliyun.com" && route.Contains(product == "coding" ? "coding-plan" : "token-plan", StringComparison.OrdinalIgnoreCase),
            "minimax" => u.Host is "platform.minimax.cn" or "platform.minimaxi.com" && Regex.IsMatch(route, @"usage|subscribe", RegexOptions.IgnoreCase),
            "xiaomi" => u.Host == "platform.xiaomimimo.com" && route.Contains("console/plan-manage", StringComparison.OrdinalIgnoreCase),
            "kimi" => u.Host == "www.kimi.com" && route.StartsWith("/code/console", StringComparison.Ordinal),
            "volcengine" => u.Host == "console.volcengine.com" && route.StartsWith("/ark/", StringComparison.Ordinal) && Regex.IsMatch(route, "coding.?plan", RegexOptions.IgnoreCase),
            "tencent" => u.Host == "console.cloud.tencent.com" && Regex.IsMatch(route, @"^/(tokenhub|hunyuan|lkeap)/.*" + (product == "coding" ? "coding" : "token") + @".?plan", RegexOptions.IgnoreCase),
            "baidu" => u.Host == "console.bce.baidu.com" && route.StartsWith("/qianfan/", StringComparison.Ordinal) && Regex.IsMatch(route, "coding.?plan|token.?plan|subscription", RegexOptions.IgnoreCase),
            "stepfun" => u.Host == "platform.stepfun.com" && Regex.IsMatch(route, "step-plan|plan/usage|plan/overview", RegexOptions.IgnoreCase),
            "huawei" => u.Host == "console.huaweicloud.com" && route.StartsWith("/maas/", StringComparison.Ordinal) && Regex.IsMatch(route, "token.?plan|subscription|subscribe", RegexOptions.IgnoreCase),
            _ => false
        };
    }

    // Only visible progress blocks with an explicit usage/remaining caption and
    // a single percentage or a semantic progress bar. Tables/charts, sidebars,
    // wallet figures and unrelated unlabelled percentages cannot supply a quota.
    internal const string Script = """
        (() => {
          const visible = e => e.getClientRects().length && getComputedStyle(e).visibility !== 'hidden';
          const body = document.body;
          if (!body || !/(?:Coding\s*Plan|Token\s*Plan|Step\s*Plan|M\s*Plan|套餐用量|套餐额度|订阅用量)/i.test(body.innerText)) return [];
          const rows = [];
          const period = /(?:每\s*5\s*小时|5\s*(?:小时|[hH])|five\s*hour|每周|本周|周额度|周用量|weekly|每月|本月|订阅月|月额度|月用量|monthly|套餐额度|套餐用量|本周期)/i;
          const usage = /(?:已用|已使用|已消耗|消耗比例|使用率|used|utilization|剩余|remaining)/i;
          const candidates = [...document.querySelectorAll('[role="progressbar"], progress, [class*="progress"], [class*="Progress"]')].filter(visible);
          for (const bar of candidates) {
            let block = bar;
            for (let depth = 0; block && depth < 5; depth++, block = block.parentElement) {
              if (block === body || block.matches('nav, aside, header, table')) break;
              const label = ((bar.getAttribute('aria-label') || '') + ' ' + (block.innerText || '')).trim();
              if (label.length > 500) break;
              if (!period.test(label) || !usage.test(label) || /余额|人民币|balance|CNY|￥|¥|已到期|已过期|未订阅|未开通|expired/i.test(label)) continue;
              // Do not climb into a container that combines distinct quota bars.
              if ([...block.querySelectorAll('[role="progressbar"], progress')].filter(visible).length > 1) break;
              const percentages = [...label.matchAll(/(\d+(?:\.\d+)?)\s*%/g)].map(m => Number(m[1]));
              const unique = [...new Set(percentages)];
              if (unique.length !== 1) continue;
              const left = /剩余|remaining/i.test(label), used = /已用|已使用|已消耗|消耗比例|使用率|used|utilization/i.test(label);
              if (left && used) continue;
              const labels = label.match(new RegExp(period.source, 'ig')) || [];
              rows.push({label: [...new Set(labels)].join(' '), percent: left ? 100 - unique[0] : unique[0]});
              break;
            }
          }
          return rows.slice(0, 32);
        })()
        """;

    internal static DomesticProviderQuotaSnapshot Parse(string provider, string product, JsonElement rows)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 32) throw new JsonException("Invalid page quota rows.");
        double? primary = null, weekly = null, plan = null;
        void Assign(ref double? field, double pct)
        {
            if (field.HasValue && Math.Abs(field.Value - pct) > .1) throw new JsonException("Ambiguous page quota.");
            field = pct;
        }
        foreach (var row in rows.EnumerateArray())
        {
            var label = row.GetProperty("label").GetString() ?? "";
            if (label.Length > 500 || Regex.IsMatch(label, "余额|人民币|balance|CNY|￥|¥", RegexOptions.IgnoreCase)) throw new JsonException("Not a plan quota.");
            var pct = CodingPlanSources.Number(row.GetProperty("percent"));
            if (pct < 0 || pct > 100) throw new JsonException("Invalid page percent.");
            bool h = Regex.IsMatch(label, @"每\s*5\s*小时|5\s*(?:小时|h)|five\s*hour", RegexOptions.IgnoreCase);
            bool w = Regex.IsMatch(label, "每周|本周|周额度|周用量|weekly", RegexOptions.IgnoreCase);
            bool m = Regex.IsMatch(label, "每月|本月|订阅月|月额度|月用量|monthly", RegexOptions.IgnoreCase);
            if ((h ? 1 : 0) + (w ? 1 : 0) + (m ? 1 : 0) > 1) throw new JsonException("Combined page periods.");
            if (h) Assign(ref primary, pct);
            else if (w) Assign(ref weekly, pct);
            else if (m || Regex.IsMatch(label, "套餐额度|套餐用量|本周期")) Assign(ref plan, pct);
        }
        if (primary is null && weekly is null && plan is null) throw new JsonException("No labelled plan quota.");
        var definition = CodingPlanSources.Products(provider).Single(p => p.Id == product);
        return new(provider == "qwen" ? "alibaba" : provider, definition.Label, primary, null, weekly, null, null, null, null, DateTimeOffset.UtcNow, false)
        { PlanPercent = plan, PrimaryWindowSeconds = primary.HasValue ? 18000 : null, WeeklyWindowSeconds = weekly.HasValue ? 604800 : null };
    }

    internal static string Summary(DomesticProviderQuotaSnapshot? q)
    {
        if (q is null) return "尚未取得套餐额度；请登录并打开套餐用量页。";
        var rows = new List<string>();
        if (q.PrimaryPercent is double h) rows.Add($"短周期已用 {h:0.##}%");
        if (q.WeeklyPercent is double w) rows.Add($"周已用 {w:0.##}%");
        if (q.PlanPercent is double m) rows.Add($"套餐已用 {m:0.##}%");
        return string.Join(" · ", rows) + $"；更新 {q.UpdatedAt.ToLocalTime():MM-dd HH:mm}。";
    }
}
