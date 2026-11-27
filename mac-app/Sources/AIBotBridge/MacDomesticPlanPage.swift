import Foundation

// Local normalized observations, not private vendor APIs.
enum MacDomesticPlanPage {
    static func supports(_ provider: String, mode: String) -> Bool {
        ["volcengine","tencent","baidu","stepfun","huawei"].contains(provider) && ["coding","token"].contains(mode) && MacDomesticSources.modes(provider).contains(where:{$0.0 == mode})
    }
    static func allows(_ provider: String, mode: String, url: URL) -> Bool {
        guard supports(provider,mode:mode), url.scheme == "https", url.port == nil || url.port == 443,
              url.user == nil, url.password == nil, let entry = MacModelAccountCatalog.entry(provider,mode:mode),
              url.host == entry.host else { return false }
        let path = url.path.lowercased() + (url.fragment ?? "").lowercased()
        if path.contains("login") || path.contains("signin") { return false }
        switch provider {
        case "volcengine": return path == "/ark/region:ark+cn-beijing/codingplan"
        case "tencent": return path == "/tokenhub/" + (mode == "coding" ? "codingplan":"tokenplan")
        case "baidu": return path == "/qianfan/resource/token-plan"
        case "stepfun": return path == "/step-plan" || path == "/plan/usage"
        case "huawei": return path.hasPrefix("/maas/") && ["tokenplan","token-plan","subscription","subscribe"].contains(where:{ path.split(separator:"/").contains(Substring($0)) })
        default: return false
        }
    }
    static let script = #"""
    (() => {
      const visible = element => {
        for (let node = element; node; node = node.parentElement) {
          const style = getComputedStyle(node);
          if (node.hidden || node.getAttribute('aria-hidden') === 'true' || style.display === 'none' || style.visibility !== 'visible' || Number(style.opacity) === 0) return false;
        }
        return element.getClientRects().length > 0;
      };
      const body = document.body;
      if (!body || !/Coding\s*Plan|Token\s*Plan|Step\s*Plan|套餐用量|套餐额度|订阅用量/i.test(body.innerText)) return [];
      const period = /5\s*(?:小时|h)|five\s*hour|每周|本周|周额度|周用量|weekly|每月|本月|订阅月|月额度|月用量|monthly|套餐额度|套餐用量|本周期/i;
      const used = /已用|已使用|已消耗|消耗比例|使用率|used|utilization/i, remaining = /剩余|remaining/i;
      const excluded = /余额|人民币|balance|CNY|￥|¥|已到期|已过期|未订阅|未开通|expired/i;
      const selector = '[role="progressbar"],progress,[class*="progress"],[class*="Progress"]';
      const rows = [];
      for (const bar of document.querySelectorAll(selector)) {
        if (!visible(bar) || bar.closest('nav,aside,header,table')) continue;
        for (let box = bar, n = 0; box && box !== body && n < 4; box = box.parentElement, n++) {
          if (box.matches('nav,aside,header,table')) break;
          const label = box.innerText || '';
          if (label.length > 500 || excluded.test(label)) break;
          if ([...box.querySelectorAll('[role="progressbar"],progress')].filter(visible).length > 1) break;
          if (used.test(label) && remaining.test(label)) break;
          if (!period.test(label) || (!used.test(label) && !remaining.test(label))) continue;
          const percentages = [...new Set([...label.matchAll(/([-−]?\d+(?:\.\d+)?)\s*%/g)].map(m => Number(m[1])))];
          if (percentages.length !== 1) continue;
          const percent = percentages[0];
          if (!Number.isFinite(percent) || percent < 0 || percent > 100) break;
          rows.push({label: (label.match(new RegExp(period.source,'ig')) || []).join(' '), percent: remaining.test(label) ? 100-percent : percent});
          break;
        }
      }
      return rows.slice(0,32);
    })()
    """#
    static func parse(_ provider: String, mode: String, rows: [[String:Any]], now: Date = Date()) throws -> DomesticProviderQuotaSnapshot {
        guard supports(provider,mode:mode), !rows.isEmpty, rows.count <= 32 else { throw MacDomesticQuotaParser.Failure.invalid }
        var values: [Int:Double] = [:]
        func has(_ text: String, _ pattern: String) -> Bool { text.range(of:pattern,options:[.regularExpression,.caseInsensitive]) != nil }
        for row in rows {
            guard let label = row["label"] as? String, label.count <= 500,
                  !has(label,"余额|人民币|balance|CNY|￥|¥|过期|到期|expired"),
                  let percent = MacDomesticQuotaParser.number(row["percent"]), (0...100).contains(percent) else { throw MacDomesticQuotaParser.Failure.invalid }
            let matches = [has(label,#"5\s*(小时|h)|five\s*hour"#), has(label,"每周|本周|周额度|周用量|weekly"), has(label,"每月|本月|订阅月|月额度|月用量|monthly")]
            let indexes = matches.indices.filter { matches[$0] }
            guard indexes.count <= 1 else { throw MacDomesticQuotaParser.Failure.invalid }
            guard let index = indexes.first ?? (has(label,"套餐额度|套餐用量|本周期") ? 2:nil) else { throw MacDomesticQuotaParser.Failure.invalid }
            if let old = values[index], abs(old-percent) > 0.1 { throw MacDomesticQuotaParser.Failure.invalid }
            values[index] = percent
        }
        var value = DomesticProviderQuotaSnapshot(provider:provider,plan:mode == "coding" ? "Coding Plan":"Token Plan",primaryPercent:values[0],primaryResetsAt:nil,weeklyPercent:values[1],weeklyResetsAt:nil,balance:nil,usedCost:nil,currency:nil,updatedAt:now,stale:false,planPercent:values[2])
        value.primaryWindowSeconds = values[0] == nil ? nil:18000
        value.weeklyWindowSeconds = values[1] == nil ? nil:604800
        return value
    }
}
