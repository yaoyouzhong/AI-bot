import Foundation
import AppKit

enum MacModelAccountValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ label: String, _ value: Bool) throws { guard value else { throw MacTab5LocalError.invalid("Model accounts: " + label) }; checked += 1 }
        let all = MacModelAccountCatalog.all
        try check("14 distinct providers",all.count == 14 && Set(all.map(\.id)).count == 14)
        for item in all {
            try check("official entry " + item.id,MacModelAccountCatalog.navigationAllowed(item.url,provider:item.id,mainFrame:true))
            try check("lookalike excluded " + item.id,!MacModelAccountCatalog.navigationAllowed(URL(string:"https://"+item.url.host!+".invalid/")!,provider:item.id,mainFrame:true))
        }
        for address in ["http://bigmodel.cn/","https://bigmodel.cn:444/","https://bigmodel.cn@evil.invalid/","https://user@bigmodel.cn/"] {
            try check("untrusted navigation rejected",!MacModelAccountCatalog.navigationAllowed(URL(string:address)!,provider:"zhipu",mainFrame:true))
        }
        let endpoint = "https://bigmodel.cn/api/biz/account/query-customer-account-report"
        try check("finance endpoint",MacDomesticBrowser.endpoint(provider:"zhipu",url:URL(string:endpoint)!))
        try check("unrelated finance endpoint",!MacDomesticBrowser.endpoint(provider:"zhipu",url:URL(string:endpoint+"-other")!))
        try check("finance host spoof",!MacDomesticBrowser.endpoint(provider:"zhipu",url:URL(string:endpoint.replacingOccurrences(of:"bigmodel.cn",with:"bigmodel.cn.invalid"))!))
        var state = MacConsoleRefreshState(); state.start(100)
        try check("no refresh while awaiting",!state.due(250,interactive:false))
        try check("bounded timeout",!state.timeout(159) && state.timeout(160))
        try check("failure backoff",!state.due(459,interactive:false) && state.due(460,interactive:false))
        state.success(500)
        try check("background cadence",!state.due(619,interactive:false) && state.due(620,interactive:false))
        try check("never reload active login",!state.due(1000,interactive:true))
        state.fail(1000,login:true)
        try check("expired login identified",state.needsLogin && !state.due(1299,interactive:false))
        func parse(_ value: Any) throws -> DomesticProviderQuotaSnapshot {
            try MacDomesticSources.parse("deepseek",mode:"console",bytes:JSONSerialization.data(withJSONObject:value))
        }
        let value = try parse(["code":0,"data":["biz_data":["bonus_wallets":[],"normal_wallets":[["currency":"CNY","balance":"12.34"],["currency":"USD","balance":90]]]]])
        try check("explicit CNY wallet preferred",value.balance == 12.34 && value.currency == "CNY")
        for invalid: [String:Any] in [
            ["code":401,"data":["normal_wallets":[["currency":"CNY","balance":10]]]],
            ["normal_wallets":[["balance":10]]],
            ["normal_wallets":[["currency":"CNY","balance":true]]],
            ["normal_wallets":[["currency":"CNY","balance":10],["currency":"CNY","balance":20]]]
        ] {
            var rejected = false; do { _ = try parse(invalid) } catch { rejected = true }
            try check("bad or ambiguous wallet rejected",rejected)
        }
        func summary(_ costs: [[String:Any]]) throws -> DomesticProviderQuotaSnapshot {
            try parse(["data":["biz_data":["bonus_wallets":[],"normal_wallets":[["currency":"USD","balance":99],["currency":"CNY","balance":75.37]],"total_costs":costs]]])
        }
        let combined = try parse(["data":["biz_data":["normal_wallets":[["currency":"CNY","balance":"10.10"],["currency":"USD","balance":"99"]],"bonus_wallets":[["currency":"CNY","balance":"0.20"],["currency":"USD","balance":"5"]]]]])
        try check("console total includes same-currency bonus",combined.balance == 10.30 && combined.currency == "CNY")
        try check("bonus-only wallet",try parse(["normal_wallets":[],"bonus_wallets":[["currency":"CNY","balance":"3.5"]]]).balance == 3.5)
        try check("scientific zero bonus",try parse(["normal_wallets":[["currency":"CNY","balance":"2"]],"bonus_wallets":[["currency":"CNY","balance":"0E-16"]]]).balance == 2)
        try check("API total is not added twice",try parse(["balance_infos":[["currency":"CNY","total_balance":"12","granted_balance":"2"]],"normal_wallets":[["currency":"CNY","balance":"10"]],"bonus_wallets":[["currency":"CNY","balance":"2"]]]).balance == 12)
        try check("other-currency bonus never added",try parse(["normal_wallets":[["currency":"CNY","balance":"10"]],"bonus_wallets":[["currency":"USD","balance":"2"]]]).balance == 10)
        for invalid: [String:Any] in [
            ["normal_wallets":[["currency":"CNY","balance":10]]],
            ["normal_wallets":[["currency":"CNY","balance":10]],"bonus_wallets":[["currency":"CNY","balance":true]]],
            ["normal_wallets":[["currency":"CNY","balance":10]],"bonus_wallets":[["currency":"CNY","balance":"NaN"]]],
            ["normal_wallets":[["currency":"CNY","balance":10]],"bonus_wallets":[["currency":"CNY","balance":1],["currency":"CNY","balance":2]]]
        ] {
            var rejected = false; do { _ = try parse(invalid) } catch { rejected = true }
            try check("partial or invalid wallet response rejected",rejected)
        }
        let cost = try summary([["currency":"USD","amount":5],["currency":"CNY","amount":"22.23"]])
        try check("console usage follows selected currency",cost.usedCost == 22.23 && cost.currency == "CNY")
        try check("zero is an observed cost",try summary([["currency":"CNY","amount":0]]).usedCost == 0)
        try check("Windows summary without currency",try summary([["amount":9.75]]).usedCost == 9.75)
        for costs: [[String:Any]] in [[],[["currency":"USD","amount":5]],[["amount":true]],[["amount":-1]],[["amount":"NaN"]],[["amount":2],["amount":3]]] {
            try check("missing invalid or ambiguous usage stays unknown",try summary(costs).usedCost == nil)
        }
        let suite = "aibot-deepseek-validation-" + UUID().uuidString
        let defaults = UserDefaults(suiteName:suite)!; defer { defaults.removePersistentDomain(forName:suite) }
        let store = MacDataStore(defaults:defaults), now = cost.updatedAt
        store.updateDomestic(cost)
        let api = try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":"74.00"}]}"#.utf8),now:now.addingTimeInterval(60))
        try check("API does not invent spending",api.usedCost == nil)
        store.updateDomestic(api)
        try check("same-console balance-only update preserves cost and original age",store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == 22.23 && store.deepSeekUsageDate(now:now.addingTimeInterval(60)) == now)
        store.markDomesticStale("deepseek")
        try check("failure retains cost",store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == 22.23)
        let restored = MacDataStore(defaults:defaults)
        try check("usage survives restart with original age",restored.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == 22.23 && restored.deepSeekUsageDate(now:now.addingTimeInterval(60)) == now)
        try check("24-hour usage expiry independent of balance",restored.snapshot(now:now.addingTimeInterval(86400)).domesticQuotas?.deepSeek?.usedCost == nil && restored.snapshot(now:now.addingTimeInterval(86400)).domesticQuotas?.deepSeek?.balance == 74)
        store.updateDomestic(try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"USD","total_balance":"9"}]}"#.utf8),now:now.addingTimeInterval(120)))
        try check("currency change clears usage",store.snapshot(now:now.addingTimeInterval(120)).domesticQuotas?.deepSeek?.usedCost == nil && store.deepSeekUsageDate(now:now.addingTimeInterval(120)) == nil)
        for clear in [store.clearDeepSeekForNewAccount, {store.clearDomestic("deepseek")}] {
            store.updateDomestic(cost); clear(); store.updateDomestic(api)
            try check("account change clears usage and age",store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == nil && store.deepSeekUsageDate(now:now.addingTimeInterval(60)) == nil)
        }
        store.updateDomestic(cost); store.updateDeepSeekWeb(cost)
        store.updateDomestic(api,deepSeekAPI:true)
        try check("API never inherits web account spending",store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.balance == 74 && store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == nil)
        try check("web account retains its separate observation",store.deepSeekWebSnapshot(now:now.addingTimeInterval(60))?.usedCost == 22.23)
        store.markDeepSeekWebStale()
        try check("web failure does not stale API balance",store.deepSeekWebSnapshot(now:now.addingTimeInterval(60))?.stale == true && store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.stale == false)
        let independent = MacDataStore(defaults:defaults)
        try check("restart never merges independent accounts",independent.snapshot(now:now.addingTimeInterval(60)).domesticQuotas?.deepSeek?.usedCost == nil && independent.deepSeekWebSnapshot(now:now.addingTimeInterval(60))?.usedCost == 22.23)
        try check("separate web spending expires",independent.deepSeekWebSnapshot(now:now.addingTimeInterval(86400))?.usedCost == nil)
        let wire = try JSONEncoder().encode(store.snapshot(now:now.addingTimeInterval(60)).domesticQuotas)
        try check("device quota contains no separate web spend",!String(decoding:wire,as:UTF8.self).contains("usedCost"))
        return checked
    }
    static func runUI(store: MacDataStore, defaults: UserDefaults) throws {
        var enabled = true
        var note = ""
        let browser = MacDomesticBrowser(provider:"zhipu",store:store,persistent:false,captureEnabled:{enabled},note:{note = $0})
        defer { browser.stop() }
        let good = Data(#"{"code":200,"data":{"availableBalance":12.34,"totalSpendAmount":4}}"#.utf8)
        let date = Date(timeIntervalSince1970:1_800_000_000)
        browser.receive(status:200,bytes:good,now:date)
        guard store.snapshot().domesticQuotas?["zhipu"]?.balance == 12.34 else { throw MacTab5Error.invalidPacket }
        browser.receive(status:401,bytes:Data(),now:date.addingTimeInterval(1))
        guard let stale = store.snapshot().domesticQuotas?["zhipu"], stale.balance == 12.34, stale.updatedAt == date, stale.stale,
              browser.refreshState.needsLogin, note.contains("登录") else { throw MacTab5Error.invalidPacket }
        browser.receive(status:200,bytes:Data(#"{"code":401,"data":{}}"#.utf8))
        guard store.snapshot().domesticQuotas?["zhipu"]?.balance == 12.34 else { throw MacTab5Error.invalidPacket }
        enabled = false; browser.receive(status:200,bytes:good,now:date.addingTimeInterval(2))
        guard store.snapshot().domesticQuotas?["zhipu"]?.updatedAt == date else { throw MacTab5Error.invalidPacket }
        enabled = true; browser.receive(status:200,bytes:good,now:date.addingTimeInterval(3))
        guard store.snapshot().domesticQuotas?["zhipu"]?.stale == false else { throw MacTab5Error.invalidPacket }
        let deep = MacDomesticBrowser(provider:"deepseek",store:store,persistent:false,captureEnabled:{true},separateDeepSeek:{true},note:{_ in})
        defer { deep.stop() }
        let current = Date()
        store.updateDomestic(try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":"90"}]}"#.utf8),now:current),deepSeekAPI:true)
        deep.receive(status:200,bytes:Data(#"{"data":{"biz_data":{"normal_wallets":[{"currency":"CNY","balance":"10"}],"bonus_wallets":[{"currency":"CNY","balance":"2"}],"total_costs":[{"currency":"CNY","amount":"5"}]}}}"#.utf8),now:current)
        guard store.snapshot().domesticQuotas?.deepSeek?.balance == 90, store.snapshot().domesticQuotas?.deepSeek?.usedCost == nil, store.deepSeekWebSnapshot()?.usedCost == 5 else { throw MacTab5Error.invalidPacket }
        deep.receive(status:401,bytes:Data(),now:current)
        guard store.deepSeekWebSnapshot()?.stale == true, store.snapshot().domesticQuotas?.deepSeek?.stale == false else { throw MacTab5Error.invalidPacket }
        let window = MacDomesticQuotaWindow(service:.init(store:store),store:store,defaults:defaults,loadPages:false)
        defer { window.stop(); window.window?.close() }
        guard window.providerCount == 14 else { throw MacTab5Error.invalidPacket }
        for item in MacModelAccountCatalog.all {
            window.selectProvider(item.id,userAction:true)
            guard window.selectedProviderID == item.id, defaults.string(forKey:"domestic_source_"+item.id) == nil else { throw MacTab5Error.invalidPacket }
        }
        func children(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(children) }
        window.selectProvider("zhipu",userAction:false)
        guard let zoom = children(window.window!.contentView!).first(where: { $0.identifier?.rawValue == "model-web-zoom" }) as? NSPopUpButton else { throw MacTab5Error.invalidPacket }
        zoom.selectItem(at:1); zoom.sendAction(zoom.action,to:zoom.target)
        guard defaults.integer(forKey:"model_account_zoom_zhipu") == 75 else { throw MacTab5Error.invalidPacket }
        window.selectProvider("kimi",userAction:false); window.selectProvider("zhipu",userAction:false)
        guard zoom.indexOfSelectedItem == 1 else { throw MacTab5Error.invalidPacket }
        print("MAC_MODEL_ACCOUNT_UI_OK 14 providers; source isolation, expired-login cache retention and recovery; no network/credentials")
    }

}
