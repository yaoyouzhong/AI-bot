import Foundation
import CryptoKit

struct MacDomesticCredential: Codable {
    var mode: String, key: String = "", secret: String = "", package: String = "", port: Int = 5494
    func validate(provider: String) throws {
        guard MacDomesticSources.modes(provider).contains(where: { $0.0 == mode }) else { throw MacTab5LocalError.invalid("不支持此账户读取方式。") }
        if mode == "console" { return }
        guard (1...2048).contains(key.utf8.count), key.utf8.allSatisfy({ (33...126).contains($0) }), secret.utf8.count <= 2048, secret.utf8.allSatisfy({ (33...126).contains($0) }) else { throw MacTab5LocalError.invalid("请填写有效凭据，不包含空白或控制字符。") }
        if provider == "qwen" || provider == "baidu" { guard !secret.isEmpty else { throw MacTab5LocalError.invalid("请填写 Access Key 和 Secret Key。") } }
        if mode == "package" { guard MacProfileValidation.text(package,limit:128) else { throw MacTab5LocalError.invalid("请填写模型资源包 ID。") } }
        if mode == "local" { guard (1024...65535).contains(port) else { throw MacTab5LocalError.invalid("本地 Kimi 服务端口必须在 1024–65535。") } }
    }
}
enum MacDomesticSources {
    // These providers use API credentials for quota reads. Opening their
    // account site must not switch or discard that configured data source.
    static func externalConsoleURL(_ provider: String) -> URL? {
        let urls = ["deepseek":"https://platform.deepseek.com/usage",
                    "stepfun":"https://platform.stepfun.com/",
                    "baidu":"https://console.bce.baidu.com/qianfan/overview"]
        return urls[provider].flatMap(URL.init(string:))
    }
    static func modes(_ provider: String) -> [(String,String)] {
        switch provider {
        case "qwen": return [("console","百炼订阅控制台"),("balance","阿里云现金余额（AK/SK）")]
        case "kimi": return [("local","Kimi Code 本地额度服务"),("balance","Moonshot API 余额"),("console","Kimi Code 控制台")]
        case "minimax": return [("api","Token Plan API"),("console","MiniMax 控制台")]
        case "deepseek": return [("api","DeepSeek 官方余额 API"),("separate","分别显示：API 余额／网页消费"),("console","DeepSeek 官方控制台")]
        case "zhipu": return [("plan","GLM Coding Plan API"),("console","智谱财务控制台")]
        case "stepfun": return [("api","开放平台 API 余额")]
        case "baidu": return [("package","模型资源包（AK/SK）"),("balance","百度云现金余额（AK/SK）")]
        case "xiaomi": return [("console","MiMo Token Plan 控制台")]
        default: return []
        }
    }
    static func escape(_ text: String) -> String { text.addingPercentEncoding(withAllowedCharacters:CharacterSet(charactersIn:"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~"))! }
    private static func hmac(_ key: String, _ text: String) -> String { HMAC<SHA256>.authenticationCode(for:Data(text.utf8),using:SymmetricKey(data:Data(key.utf8))).map { String(format:"%02x",$0) }.joined() }
    static func request(_ provider: String, _ c: MacDomesticCredential, now: Date = Date(), nonce: String = UUID().uuidString) throws -> URLRequest {
        try c.validate(provider:provider)
        let endpoints = ["deepseek":"https://api.deepseek.com/user/balance","minimax":"https://www.minimaxi.com/v1/token_plan/remains","stepfun":"https://api.stepfun.com/v1/accounts","zhipu":"https://open.bigmodel.cn/api/monitor/usage/quota/limit","kimi":c.mode == "local" ? "http://127.0.0.1:\(c.port)/api/v1/oauth/usage":"https://api.moonshot.cn/v1/users/me/balance"]
        if let endpoint = endpoints[provider], c.mode != "console" {
            var r = URLRequest(url:URL(string:endpoint)!); r.setValue(provider == "zhipu" ? c.key:"Bearer " + c.key,forHTTPHeaderField:"Authorization"); return r
        }
        let timestamp = ISO8601DateFormatter().string(from:now)
        if provider == "qwen", c.mode == "balance" {
            let values = ["Action":"QueryAccountBalance","Version":"2017-12-14","Format":"JSON","AccessKeyId":c.key,"SignatureMethod":"HMAC-SHA1","SignatureVersion":"1.0","SignatureNonce":nonce,"Timestamp":timestamp]
            let query = values.keys.sorted().map { escape($0) + "=" + escape(values[$0]!) }.joined(separator:"&")
            let signature = Data(HMAC<Insecure.SHA1>.authenticationCode(for:Data(("GET&%2F&" + escape(query)).utf8),using:SymmetricKey(data:Data((c.secret + "&").utf8)))).base64EncodedString()
            return URLRequest(url:URL(string:"https://business.aliyuncs.com/?" + query + "&Signature=" + escape(signature))!)
        }
        guard provider == "baidu", ["package","balance"].contains(c.mode) else { throw MacTab5LocalError.invalid("此来源使用网页登录授权。") }
        let isPackage = c.mode == "package", host = isPackage ? "qianfan.baidubce.com":"billing.baidubce.com", path = isPackage ? "/v2/charge":"/v1/finance/cash/balance", query = isPackage ? "Action=DescribePackageResource":""
        let prefix = "bce-auth-v1/\(c.key)/\(timestamp)/1800", canonical = "POST\n\(path)\n\(query)\nhost:\(host)\nx-bce-date:" + escape(timestamp)
        var r = URLRequest(url:URL(string:"https://" + host + path + (query.isEmpty ? "":"?" + query))!); r.httpMethod = "POST"
        r.httpBody = try JSONSerialization.data(withJSONObject:isPackage ? ["packageId":c.package]:[:]); r.setValue("application/json",forHTTPHeaderField:"Content-Type"); r.setValue(host,forHTTPHeaderField:"Host"); r.setValue(timestamp,forHTTPHeaderField:"x-bce-date")
        r.setValue(prefix + "/host;x-bce-date/" + hmac(hmac(c.secret,prefix),canonical),forHTTPHeaderField:"Authorization"); return r
    }
    private static func objects(_ root: Any, depth: Int = 0) -> [[String:Any]] {
        guard depth < 8 else { return [] }
        if let dictionary = root as? [String:Any] { return [dictionary] + dictionary.values.flatMap { objects($0,depth:depth+1) }.prefix(10000) }
        if let array = root as? [Any] { return Array(array.prefix(1000).flatMap { objects($0,depth:depth+1) }.prefix(10000)) }; return []
    }
    static func date(_ value: Any?) -> Date? {
        if let number = MacDomesticQuotaParser.number(value), number > 946684800 { let seconds = number > 100000000000 ? number/1000:number; return seconds <= 4105123200 ? Date(timeIntervalSince1970:seconds):nil }
        if let text = value as? String { let f = ISO8601DateFormatter(); if let d = f.date(from:text) { return d }; f.formatOptions.insert(.withFractionalSeconds); return f.date(from:text) }; return nil
    }
    static func windowSeconds(_ value: Any?) -> Int64? {
        guard let window = value as? [String:Any], let count = MacDomesticQuotaParser.number(window["duration"]), let unit = window["unit"] as? String else { return nil }
        let scale: Double = ["second":1,"seconds":1,"minute":60,"minutes":60,"hour":3600,"hours":3600,"day":86400,"days":86400,"week":604800,"weeks":604800][unit.lowercased()] ?? 0
        let seconds = count * scale
        guard seconds.isFinite, seconds > 0, seconds <= 2147483647, seconds.rounded() == seconds else { return nil }; return Int64(seconds)
    }
    static func bounds(_ row: [String:Any]) -> (Date?,Int64?) {
        for (start,end) in [("cycleStartAt","cycleEndAt"),("cycleStartTime","cycleEndTime"),("periodStartTime","periodEndTime"),("startAt","endAt"),("startTime","endTime"),("effectiveTime","expiredTime")] {
            if let a = date(row[start]), let b = date(row[end]) { let seconds = b.timeIntervalSince(a); if seconds > 0, seconds <= 2147483647, seconds.rounded() == seconds { return (b,Int64(seconds)) } }
        }; return (nil,nil)
    }
    static func parse(_ provider: String, mode: String, bytes: Data, package: String = "", now: Date = Date()) throws -> DomesticProviderQuotaSnapshot {
        guard bytes.count <= 1048576, let root = try JSONSerialization.jsonObject(with:bytes) as? [String:Any] else { throw MacDomesticQuotaParser.Failure.invalid }
        let data = root["data"] as? [String:Any] ?? [:], nodes = objects(root)
        let num = MacDomesticQuotaParser.number
        func number(_ v: Any?) throws -> Double { guard let n = num(v) else { throw MacDomesticQuotaParser.Failure.invalid }; return n }
        func percent(_ v: Any?) throws -> Double { let n = try number(v); guard (0...100).contains(n) else { throw MacDomesticQuotaParser.Failure.invalid }; return n }
        func quota(_ plan: String, balance: Double? = nil, currency: String? = nil, primary: Double? = nil, primaryReset: Date? = nil, weekly: Double? = nil, weeklyReset: Date? = nil, planPercent: Double? = nil, planReset: Date? = nil) -> DomesticProviderQuotaSnapshot {
            .init(provider:provider == "qwen" ? "alibaba":provider,plan:plan,primaryPercent:primary,primaryResetsAt:primaryReset,weeklyPercent:weekly,weeklyResetsAt:weeklyReset,balance:balance,usedCost:nil,currency:currency,updatedAt:now,stale:false,planPercent:planPercent,planResetsAt:planReset)
        }
        switch provider {
        case "deepseek":
            if mode != "console" { return try MacDomesticQuotaParser.deepSeek(bytes,now:now) }
            for node in nodes {
                if let code = node["code"], num(code) != 0 && num(code) != 200 { throw MacDomesticQuotaParser.Failure.invalid }
            }
            var wallets: [String:[String:Decimal]] = [:]
            for node in nodes {
                for key in ["normal_wallets","bonus_wallets","balance_infos"] {
                    guard let raw = node[key] else { continue }
                    guard let rows = raw as? [[String:Any]] else { throw MacDomesticQuotaParser.Failure.invalid }
                    if wallets[key] == nil { wallets[key] = [:] }
                    for row in rows {
                        let rawAmount = key == "balance_infos" ? row["total_balance"] : row["available_balance"] ?? row["balance"]
                        guard let currency = row["currency"] as? String, ["CNY","USD"].contains(currency),
                              let number = num(rawAmount), abs(number) < 1e15,
                              let amount = Decimal(string:(rawAmount as? String) ?? String(number),locale:Locale(identifier:"en_US_POSIX")), !amount.isNaN else { throw MacDomesticQuotaParser.Failure.invalid }
                        if let previous = wallets[key]?[currency], previous != amount { throw MacDomesticQuotaParser.Failure.invalid }
                        wallets[key]?[currency] = amount
                    }
                }
            }
            var balances = wallets["balance_infos"] ?? [:]
            // total_balance already includes grants. Console wallet arrays are
            // separate aggregates; only combine a complete same-currency pair.
            if let normal = wallets["normal_wallets"], let bonus = wallets["bonus_wallets"] {
                for currency in Set(normal.keys).union(bonus.keys) where balances[currency] == nil {
                    balances[currency] = (normal[currency] ?? 0) + (bonus[currency] ?? 0)
                }
            }
            let currency = balances["CNY"] == nil ? "USD":"CNY"
            guard let decimal = balances[currency] else { throw MacDomesticQuotaParser.Failure.invalid }
            let amount = NSDecimalNumber(decimal:decimal).doubleValue
            guard amount.isFinite, abs(amount) < 1e15 else { throw MacDomesticQuotaParser.Failure.invalid }
            var costs = Set<Double>()
            for node in nodes {
                for row in node["total_costs"] as? [[String:Any]] ?? [] {
                    guard row["currency"] == nil || (row["currency"] as? String)?.uppercased() == currency,
                          let cost = num(row["amount"]), cost >= 0, cost < 1e15 else { continue }
                    costs.insert(cost)
                }
            }
            var value = quota("开放平台余额",balance:amount,currency:currency)
            // Ambiguous or malformed usage must not replace a successful sample.
            if costs.count == 1 { value.usedCost = costs.first }
            return value
        case "stepfun":
            guard root["object"] as? String == "account", ["prepaid","postpaid"].contains(root["type"] as? String ?? "") else { throw MacDomesticQuotaParser.Failure.invalid }
            return quota("API 余额（非 Step Plan）",balance:try number(root["balance"]),currency:"CNY")
        case "baidu":
            guard root["code"] == nil, root["error"] == nil else { throw MacDomesticQuotaParser.Failure.invalid }
            if mode == "balance" { return quota("百度云现金余额",balance:try number(root["cashBalance"]),currency:"CNY") }
            guard let result = root["result"] as? [String:Any], let row = result["instance"] as? [String:Any], row["packageId"] as? String == package, let name = row["serviceName"] as? String, MacProfileValidation.text(name,limit:100), let expiry = date(row["expiredTime"]) else { throw MacDomesticQuotaParser.Failure.invalid }
            let total = try number(row["specification"]), used = try number(row["used"]), state = row["status"] as? String ?? ""
            guard total > 0, used >= 0, used <= total, ["Pending","Active","Exhausted","Expired"].contains(state) else { throw MacDomesticQuotaParser.Failure.invalid }
            var value = quota(name,planPercent:["Expired","Exhausted"].contains(state) || expiry <= now ? 100:used/total*100,planReset:expiry); value.planExpiresAt = expiry; return value
        case "qwen":
            if mode == "balance" {
                guard root["Success"] as? Bool == true, String(describing:root["Code"] ?? "") == "200", let data = root["Data"] as? [String:Any], let currency = data["Currency"] as? String, ["CNY","USD","JPY"].contains(currency) else { throw MacDomesticQuotaParser.Failure.invalid }
                let cash = (data["AvailableCashAmount"] as? String)?.replacingOccurrences(of:",",with:"") as Any? ?? data["AvailableCashAmount"]
                return quota("阿里云现金余额",balance:try number(cash),currency:currency)
            }
            for row in nodes { if let total = num(row["TotalValue"]), total > 0, let remaining = num(row["TotalSurplusValue"] ?? row["SurplusValue"]), remaining >= 0, remaining <= total {
                let name = nodes.compactMap { $0["SubscriptionName"] as? String }.first ?? "百炼 Token Plan"
                let period = bounds(row)
                var value = quota(name,planPercent:(total-remaining)/total*100,planReset:date(row["resetTime"] ?? row["ResetTime"]) ?? period.0); value.planWindowSeconds = period.1; return value
            } }; throw MacDomesticQuotaParser.Failure.invalid
        case "xiaomi":
            if let code = root["code"], !["0","200"].contains(String(describing:code)) { throw MacDomesticQuotaParser.Failure.invalid }
            if let success = root["success"], !MacTab5Protocol.isBoolean(success) || success as? Bool != true { throw MacDomesticQuotaParser.Failure.invalid }
            let row = root["data"] as? [String:Any] ?? root; var candidates: [Double] = []
            for key in ["usagePercent","usedPercent"] { if row[key] != nil { candidates.append(try percent(row[key])) } }
            for key in ["usageRate","usedRate"] { if row[key] != nil { candidates.append(try number(row[key])*100) } }
            if row["totalCredits"] != nil || row["usedCredits"] != nil || row["remainingCredits"] != nil {
                let total = try number(row["totalCredits"]); guard total > 0 else { throw MacDomesticQuotaParser.Failure.invalid }
                if row["usedCredits"] != nil { candidates.append(try number(row["usedCredits"])/total*100) }
                if row["remainingCredits"] != nil { candidates.append((total - (try number(row["remainingCredits"])))/total*100) }
            }
            guard let first = candidates.first, candidates.allSatisfy({ $0.isFinite && (0...100).contains($0) && abs($0-first) <= 0.1 }) else { throw MacDomesticQuotaParser.Failure.invalid }
            let period = bounds(row); var value = quota("MiMo Token Plan",planPercent:first,planReset:date(row["resetAt"] ?? row["resetTime"]) ?? period.0); value.planWindowSeconds = period.1; return value
        case "zhipu":
            if mode == "console" { return try MacDomesticQuotaParser.zhipu(bytes,now:now) }
            guard root["success"] as? Bool == true, root["code"] == nil || String(describing:root["code"]!) == "200", let limits = data["limits"] as? [[String:Any]] else { throw MacDomesticQuotaParser.Failure.invalid }
            var primary: Double?, weekly: Double?, pReset: Date?, wReset: Date?, seconds: Int64?
            for row in limits where row["type"] as? String == "TOKENS_LIMIT" {
                let pct = try percent(row["percentage"]), unit = num(row["unit"]) ?? 3, count = num(row["number"]) ?? 5
                if unit == 3, (1...24).contains(count) { guard primary == nil else { throw MacDomesticQuotaParser.Failure.invalid }; primary = pct; pReset = date(row["nextResetTime"]); seconds = Int64(count*3600) }
                if unit == 6, count == 1 { guard weekly == nil else { throw MacDomesticQuotaParser.Failure.invalid }; weekly = pct; wReset = date(row["nextResetTime"]) }
            }
            guard primary != nil || weekly != nil else { throw MacDomesticQuotaParser.Failure.invalid }
            var value = quota("GLM Coding Plan",primary:primary,primaryReset:pReset,weekly:weekly,weeklyReset:wReset); value.primaryWindowSeconds = seconds; value.weeklyWindowSeconds = weekly == nil ? nil:604800; return value
        case "minimax":
            func window(_ row: [String:Any], _ prefixes: [String]) throws -> Double? {
                for prefix in prefixes {
                    for suffix in ["_used_percent","_usage_percent","_usedPercent"] { if row[prefix+suffix] != nil { return try percent(row[prefix+suffix]) } }
                    for suffix in ["_remaining_percent","_remains_percent","_remainingPercent"] { if row[prefix+suffix] != nil { return 100 - (try percent(row[prefix+suffix])) } }
                    if let total = num(row[prefix+"_total"] ?? row[prefix+"_total_count"]), total > 0, let left = num(row[prefix+"_remaining"] ?? row[prefix+"_remains_count"]), left >= 0, left <= total { return (total-left)/total*100 }
                }; return nil
            }
            func reset(_ v: Any?) -> Date? { guard let ms = num(v), (0...691200000).contains(ms) else { return nil }; return now.addingTimeInterval(ms/1000) }
            for row in nodes { if let weekly = try window(row,["current_weekly","weekly"]) {
                var value = quota(row["combo_name"] as? String ?? row["plan_name"] as? String ?? "MiniMax Token Plan",primary:try window(row,["current_interval","interval","five_hour"]),primaryReset:reset(row["remains_time"] ?? row["interval_remains_time"]),weekly:weekly,weeklyReset:reset(row["weekly_remains_time"]))
                let shortWindow = ((row["current_interval"] ?? row["interval"]) as? [String:Any])?["window"], weekWindow = ((row["current_weekly"] ?? row["weekly"]) as? [String:Any])?["window"]
                value.primaryWindowSeconds = value.primaryPercent == nil ? nil : shortWindow == nil ? 18000:windowSeconds(shortWindow); value.weeklyWindowSeconds = weekWindow == nil ? 604800:windowSeconds(weekWindow); return value
            } }; throw MacDomesticQuotaParser.Failure.invalid
        case "kimi":
            if mode == "balance" { guard num(root["code"]) == 0, root["status"] as? Bool == true else { throw MacDomesticQuotaParser.Failure.invalid }; return quota("Moonshot API 余额",balance:try number(data["available_balance"]),currency:"CNY") }
            if mode == "local", data["kind"] as? String != "ok" { throw MacDomesticQuotaParser.Failure.invalid }
            for node in nodes { if let usages = node["usages"] as? [String:Any] {
                func read(_ name: String) throws -> (Double?,Date?) { guard let row = usages[name] as? [String:Any] else { return (nil,nil) }; return (try percent(number(row["usedRatio"])*100),date(row["resetAt"])) }
                let p = try read("limit5h"), w = try read("limit7d"), m = try read(usages["monthTotal"] == nil ? "monthCode":"monthTotal")
                guard p.0 != nil || w.0 != nil || m.0 != nil else { continue }
                var result = quota("Kimi Coding Plan",primary:p.0,primaryReset:p.1,weekly:w.0,weeklyReset:w.1,planPercent:m.0,planReset:m.1); result.primaryWindowSeconds = p.0 == nil ? nil:18000; result.weeklyWindowSeconds = w.0 == nil ? nil:604800; return result
            } }
            var primary: Double?, weekly: Double?, pReset: Date?, wReset: Date?, seconds: Int64?
            for row in nodes { guard let duration = windowSeconds(row["window"]), let limit = num(row["limit"]), limit > 0, let used = num(row["used"]), used >= 0 else { continue }
                let pct = min(100,used/limit*100)
                if duration == 604800 { weekly = pct; wReset = date(row["reset_at"]) } else if duration > 0 && duration <= 86400 { primary = pct; pReset = date(row["reset_at"]); seconds = Int64(duration) }
            }
            guard primary != nil || weekly != nil else { throw MacDomesticQuotaParser.Failure.invalid }; var result = quota("Kimi Coding Plan",primary:primary,primaryReset:pReset,weekly:weekly,weeklyReset:wReset); result.primaryWindowSeconds = seconds; result.weeklyWindowSeconds = weekly == nil ? nil:604800; return result
        default: throw MacDomesticQuotaParser.Failure.invalid
        }
    }
}
