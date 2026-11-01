import Foundation

// API and console capture share one contract; a video allowance is never the
// text plan. Absolute reset fields take precedence even when explicitly invalid.
enum MacMiniMaxQuota {
    static func parse(root: [String:Any], rows: [[String:Any]], now: Date) throws -> DomesticProviderQuotaSnapshot {
        if let raw = root["base_resp"] {
            guard let response = raw as? [String:Any], MacDomesticQuotaParser.number(response["status_code"]) == 0 else { throw MacDomesticQuotaParser.Failure.invalid }
        }
        if let code = root["code"], ![0,200].contains(MacDomesticQuotaParser.number(code) ?? -1) { throw MacDomesticQuotaParser.Failure.invalid }
        if let success = root["success"], !MacTab5Protocol.isBoolean(success) || success as? Bool != true { throw MacDomesticQuotaParser.Failure.invalid }
        var candidates: [(Int,DomesticProviderQuotaSnapshot)] = []
        for raw in rows {
            var row: [String:Any] = [:]
            for (key,value) in raw {
                let key = key.lowercased()
                guard row[key] == nil else { throw MacDomesticQuotaParser.Failure.invalid }
                row[key] = value
            }
            let model = (row["model_name"] as? String ?? "").lowercased()
            if model.contains("video") || model.contains("hailuo") { continue }
            guard let weekly = try window(row,["current_weekly","weekly"]) else { continue }
            let primary = try window(row,["current_interval","interval","five_hour","fivehour"])
            let name = ["combo_name","plan_name","plan_title","current_subscribe_title","subscribe_title","subscription_name","title"].compactMap { row[$0] as? String }.first { !$0.isEmpty && $0.utf8.count <= 192 }
            var value = DomesticProviderQuotaSnapshot(provider:"minimax",plan:name ?? "MiniMax Token Plan",primaryPercent:primary,
                primaryResetsAt:reset(row,weekly:false,now:now),weeklyPercent:weekly,weeklyResetsAt:reset(row,weekly:true,now:now),
                balance:nil,usedCost:nil,currency:nil,updatedAt:now,stale:false)
            let shortWindow = ((row["current_interval"] ?? row["interval"]) as? [String:Any])?["window"]
            let weekWindow = ((row["current_weekly"] ?? row["weekly"]) as? [String:Any])?["window"]
            value.primaryWindowSeconds = primary == nil ? nil : shortWindow == nil ? 18000 : MacDomesticSources.windowSeconds(shortWindow)
            value.weeklyWindowSeconds = weekWindow == nil ? 604800 : MacDomesticSources.windowSeconds(weekWindow)
            let score = (model == "general" ? 30:0) + (model.isEmpty ? 0:15) + (primary == nil ? 0:10) + (name == nil ? 0:5)
            candidates.append((score,value))
        }
        guard let score = candidates.map(\.0).max() else { throw MacDomesticQuotaParser.Failure.invalid }
        let best = candidates.filter { $0.0 == score }.map(\.1)
        guard let value = best.first, best.allSatisfy({ $0 == value }) else { throw MacDomesticQuotaParser.Failure.invalid }
        return value
    }
    private static func number(_ value: Any?) throws -> Double {
        var raw = value
        if var text = value as? String {
            text = text.trimmingCharacters(in:.whitespacesAndNewlines)
            while text.last == "%" { text.removeLast() }
            raw = text
        }
        guard let n = MacDomesticQuotaParser.number(raw) else { throw MacDomesticQuotaParser.Failure.invalid }; return n
    }
    private static func window(_ row: [String:Any], _ prefixes: [String]) throws -> Double? {
        for prefix in prefixes {
            if let total = row[prefix+"_total_percent"] {
                let limit = try number(total), used = try number(row[prefix+"_used_percent"])
                guard limit > 0, used >= 0 else { throw MacDomesticQuotaParser.Failure.invalid }
                return min(1,used/limit)*100
            }
            for suffix in ["_used_percent","_usage_percent","_usagepercent","_usedpercent"] where row[prefix+suffix] != nil {
                let n = try number(row[prefix+suffix]); guard (0...100).contains(n) else { throw MacDomesticQuotaParser.Failure.invalid }; return n
            }
            for suffix in ["_remaining_percent","_remains_percent","_remainingpercent","_remainspercent"] where row[prefix+suffix] != nil {
                let n = try number(row[prefix+suffix]); guard (0...100).contains(n) else { throw MacDomesticQuotaParser.Failure.invalid }; return 100-n
            }
            if let raw = row[prefix+"_total_count"] ?? row[prefix+"_total"] {
                let total = try number(raw)
                let used = row[prefix+"_usage_count"] ?? row[prefix+"_used_count"] ?? row[prefix+"_used"]
                let left = row[prefix+"_remains_count"] ?? row[prefix+"_remaining_count"] ?? row[prefix+"_remaining"]
                guard total > 0 else { throw MacDomesticQuotaParser.Failure.invalid }
                if let used { let n = try number(used); guard n >= 0 else { throw MacDomesticQuotaParser.Failure.invalid }; return min(100,n/total*100) }
                if let left { let n = try number(left); guard (0...total).contains(n) else { throw MacDomesticQuotaParser.Failure.invalid }; return (total-n)/total*100 }
            }
        }; return nil
    }
    private static func reset(_ row: [String:Any], weekly: Bool, now: Date) -> Date? {
        if let value = row[weekly ? "weekly_end_time":"end_time"] {
            guard let ms = MacDomesticQuotaParser.number(value), ms.rounded() == ms, ms > 1e12, ms < 253402300800000 else { return nil }
            return Date(timeIntervalSince1970:ms/1000)
        }
        let keys = weekly ? ["weekly_remains_time","current_weekly_remains_time"] : ["remains_time","current_interval_remains_time","interval_remains_time"]
        for key in keys {
            if let raw = row[key], let ms = MacDomesticQuotaParser.number(raw), (0...691200000).contains(ms) { return now.addingTimeInterval(ms/1000) }
        }
        return nil
    }
}
