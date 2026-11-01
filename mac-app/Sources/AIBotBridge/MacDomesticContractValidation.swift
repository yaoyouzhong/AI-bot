import Foundation

// Provider payloads and preferences are synthetic. No account or network access.
enum MacDomesticContractValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ label: String, _ ok: Bool) throws { guard ok else { throw MacTab5LocalError.invalid("Domestic contract: " + label) }; count += 1 }
        func rejects(_ work: () throws -> Void) throws {
            do { try work() } catch { count += 1; return }; throw MacTab5LocalError.invalid("Domestic contract: invalid payload accepted")
        }
        func json(_ value: [String:Any]) throws -> Data { try JSONSerialization.data(withJSONObject:value) }
        let now = Date(timeIntervalSince1970:1_793_491_200)
        func glm(_ data: [String:Any], overview: Bool) throws -> DomesticProviderQuotaSnapshot {
            try MacDomesticSources.parse("zhipu",mode:"console",bytes:json(["code":200,"data":data]),now:now,
                capturePath:overview ? MacDomesticQuotaParser.zhipuOverviewPath:MacDomesticQuotaParser.zhipuLegacyPath)
        }
        for (raw,legacy,overview) in [("12.345",12.34,12.35),("-12.345",-12.34,-12.35),("1.15",1.15,1.15),("0",0,0)] {
            try check("legacy amount truncates",try glm(["availableBalance":raw],overview:false).balance == legacy)
            try check("overview amount rounds",try glm(["availableTotalBalance":raw],overview:true).balance == overview)
        }
        let mixed: [String:Any] = ["availableBalance":"2.999","availableTotalBalance":"3.455","totalSpendAmount":"1.239","balance":999]
        let legacy = try glm(mixed,overview:false), overview = try glm(mixed,overview:true)
        try check("captured endpoint binds schema",legacy.balance == 2.99 && legacy.usedCost == 1.23 && overview.balance == 3.46 && overview.usedCost == nil)
        try rejects { _ = try glm(["availableBalance":10],overview:true) }
        try rejects { _ = try glm(["availableTotalBalance":10],overview:false) }
        for bad: Any in [true,"NaN","Infinity","1oops",NSNull(),1e20] { try rejects { _ = try glm(["availableTotalBalance":bad],overview:true) } }
        try rejects { _ = try MacDomesticSources.parse("zhipu",mode:"console",bytes:json(["code":403,"data":mixed]),capturePath:MacDomesticQuotaParser.zhipuOverviewPath) }
        for path in MacDomesticQuotaParser.zhipuPaths {
            try check("official endpoint accepted",MacDomesticBrowser.endpoint(provider:"zhipu",url:URL(string:"https://bigmodel.cn"+path)!))
            for address in ["http://bigmodel.cn"+path,"https://bigmodel.cn:444"+path,"https://user@bigmodel.cn"+path,"https://bigmodel.cn.invalid"+path,"https://bigmodel.cn"+path+"/","https://bigmodel.cn"+path.uppercased()] {
                try check("endpoint boundary",!MacDomesticBrowser.endpoint(provider:"zhipu",url:URL(string:address)!))
            }
        }
        let suite = "aibot-contract-"+UUID().uuidString
        let isolated = UserDefaults(suiteName:suite)!; defer { isolated.removePersistentDomain(forName:suite) }
        let store = MacDataStore(defaults:isolated)
        store.updateDomestic(legacy); store.updateDomestic(overview)
        try check("new overview clears old USED",store.snapshot(now:now).domesticQuotas?.zhipu?.usedCost == nil)
        store.markDomesticStale("zhipu")
        try check("failure retains last balance",store.snapshot(now:now).domesticQuotas?.zhipu?.balance == 3.46)

        func mimo(_ body: [String:Any]) throws -> DomesticProviderQuotaSnapshot { try MacDomesticSources.parse("xiaomi",mode:"console",bytes:json(body),now:now) }
        let primary: [String:Any] = ["name":"total_token","percent":0.25,"used":25,"limit":100,"cycleStartAt":"2026-10-01T00:00:00Z","cycleEndAt":"2026-11-01T00:00:00Z"]
        let compensation: [String:Any] = ["name":"compensation_total_token","percent":0.9]
        func items(_ rows: [[String:Any]]) -> [String:Any] { ["code":0,"data":["usage":["items":rows],"usedPercent":99]] }
        let selected = try mimo(items([compensation,primary]))
        try check("MiMo selects primary only",selected.planPercent == 25 && selected.balance == nil && selected.planWindowSeconds == 31*86400)
        for percent in [0.0,1.0] {
            let value = try mimo(items([["name":"total_token","percent":percent]]))
            try check("MiMo zero and exhausted",value.planPercent == percent*100 && value.planResetsAt == nil && value.planWindowSeconds == nil)
        }
        try check("MiMo legacy retained",try mimo(["data":["usagePercent":20,"totalCredits":100,"usedCredits":20]]).planPercent == 20)
        for bad in [items([]),items([compensation]),items([primary,primary]),items([["name":"total_token"]]),items([["name":"total_token","percent":1.1]]),items([["name":"total_token","percent":true]]),items([["name":"total_token","percent":0.25,"used":60,"limit":100]]),["data":NSNull()],["success":false,"data":["usedPercent":20]]] {
            try rejects { _ = try mimo(bad) }
        }

        let end = Int64(now.addingTimeInterval(3600).timeIntervalSince1970*1000), weeklyEnd = end+604800000
        let general: [String:Any] = ["model_name":"general","current_weekly_used_percent":120,"current_weekly_total_percent":200,
            "current_interval_used_percent":50,"current_interval_total_percent":200,"end_time":end,"weekly_end_time":weeklyEnd,"remains_time":10000,"weekly_remains_time":10000]
        func mini(_ rows: [[String:Any]], mode: String = "api") throws -> DomesticProviderQuotaSnapshot {
            try MacDomesticSources.parse("minimax",mode:mode,bytes:json(["base_resp":["status_code":0],"data":["items":rows]]),now:now)
        }
        let video: [String:Any] = ["model_name":"hailuo-video","current_weekly_used_percent":1,"current_weekly_total_percent":0]
        let parsedMini = try mini([video,general])
        try check("MiniMax expanded allowance",parsedMini.weeklyPercent == 60 && parsedMini.primaryPercent == 25)
        try check("MiniMax absolute milliseconds win",parsedMini.primaryResetsAt == Date(timeIntervalSince1970:Double(end)/1000) && parsedMini.weeklyResetsAt == Date(timeIntervalSince1970:Double(weeklyEnd)/1000))
        try check("MiniMax paths agree",try mini([video,general],mode:"console") == parsedMini)
        var invalidTime = general; invalidTime["end_time"] = 10000; invalidTime["weekly_end_time"] = NSNull()
        let unknown = try mini([invalidTime]); try check("invalid explicit time does not use countdown",unknown.primaryResetsAt == nil && unknown.weeklyResetsAt == nil)
        let old = try mini([["current_weekly_used_percent":32,"current_interval_remaining_percent":80,"weekly_remains_time":10000]])
        try check("MiniMax legacy countdown",old.weeklyPercent == 32 && old.primaryPercent == 20 && old.weeklyResetsAt == now.addingTimeInterval(10))
        var exhausted = general; exhausted["current_weekly_used_percent"] = 300
        try check("expanded exhaustion caps",try mini([exhausted]).weeklyPercent == 100)
        for total: Any in [0,-1,true,"NaN",NSNull()] {
            var bad = general; bad["current_weekly_total_percent"] = total
            try rejects { _ = try mini([bad]) }
        }
        var other = general; other["current_weekly_used_percent"] = 80
        try rejects { _ = try mini([general,other]) }
        try rejects { _ = try mini([video]) }
        try rejects { _ = try MacDomesticSources.parse("minimax",mode:"api",bytes:json(["base_resp":["status_code":401],"data":general]),now:now) }
        return count
    }
}
