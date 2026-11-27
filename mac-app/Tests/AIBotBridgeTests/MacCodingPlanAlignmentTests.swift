import XCTest
import AppKit
import WebKit
@testable import AIBotBridge

final class MacCodingPlanAlignmentTests: XCTestCase {
    private func defaults() -> UserDefaults {
        let name = "coding-alignment-"+UUID().uuidString
        let value = UserDefaults(suiteName:name)!
        addTeardownBlock { value.removePersistentDomain(forName:name) }
        return value
    }
    private func quota(_ provider:String, _ name:String, balance:Double? = nil, percent:Double? = nil) -> DomesticProviderQuotaSnapshot {
        .init(provider:provider,plan:name,primaryPercent:nil,primaryResetsAt:nil,weeklyPercent:nil,weeklyResetsAt:nil,balance:balance,usedCost:nil,currency:balance == nil ? nil:"CNY",updatedAt:Date(),stale:false,planPercent:percent)
    }
    private func qwen(_ statuses:[String] = ["VALID"], used:Double = 0, total:Double = 6000, reset:String = "2026-12-03 00:00:00", start:String? = nil) throws -> DomesticProviderQuotaSnapshot {
        let q: [String:Any] = ["per5HourUsedQuota":used,"per5HourTotalQuota":total,"per5HourQuotaNextRefreshTime":"2026-11-27 18:00:00","perWeekUsedQuota":9000,"perWeekTotalQuota":45000,"perWeekQuotaNextRefreshTime":"2026-11-30 00:00:00","perBillMonthUsedQuota":22500,"perBillMonthTotalQuota":90000,"perBillMonthQuotaNextRefreshTime":reset]
        let rows: [[String:Any]] = statuses.map { status in
            var row: [String:Any] = ["status":status,"codingPlanQuotaInfo":q]
            if let start { row["startTime"] = start; row["endTime"] = reset }
            return row
        }
        let data = try JSONSerialization.data(withJSONObject:["success":true,"data":["codingPlanInstanceInfos":rows]])
        return try MacDomesticSources.parse("qwen",mode:"coding",bytes:data)
    }
    func testLegacySelectionAndNewSubscriptionDefault() throws {
        let prefs = defaults(), old = MacDataStore(defaults:prefs)
        prefs.set("balance",forKey:"domestic_source_qwen")
        old.updateDomestic(quota("alibaba","阿里云现金余额",balance:12))
        let restored = MacDataStore(defaults:prefs)
        XCTAssertEqual(restored.selectedDomesticMode("qwen"),"balance")
        XCTAssertEqual(restored.domesticContentSnapshot("qwen",mode:"balance")?.balance,12)
        XCTAssertNil(restored.domesticContentSnapshot("qwen",mode:"coding"))
        let fresh = MacDataStore(defaults:defaults())
        XCTAssertEqual(fresh.selectedDomesticMode("qwen"),"coding")
        XCTAssertEqual(fresh.selectedDomesticMode("baidu"),"token")
        XCTAssertEqual(fresh.selectedDomesticMode("zhipu"),"plan")
        XCTAssertEqual(fresh.selectedDomesticMode("stepfun"),"coding")
    }
    func testIndependentStatisticsAndFailureRetention() throws {
        let store = MacDataStore(defaults:defaults())
        for (mode,value) in [("token",quota("baidu","Token Plan",percent:20)),("package",quota("baidu","模型资源包",percent:80)),("balance",quota("baidu","百度云现金余额",balance:12))] {
            let revision = store.selectDomesticContent("baidu",mode:mode)
            XCTAssertNil(store.snapshot().domesticQuotas?.baidu)
            XCTAssertTrue(store.updateDomesticContent(value,provider:"baidu",mode:mode,generation:revision))
        }
        for (mode,expected) in [("token",20.0),("package",80.0),("balance",12.0)] {
            let revision = store.selectDomesticContent("baidu",mode:mode)
            store.markDomesticContentStale("baidu",mode:mode,generation:revision)
            let value = try XCTUnwrap(store.snapshot().domesticQuotas?.baidu)
            XCTAssertEqual(value.balance ?? value.planPercent,expected); XCTAssertTrue(value.stale)
        }
    }
    func testLateResponseCannotReplaceNewSelection() throws {
        let store = MacDataStore(defaults:defaults())
        let initial = store.selectDomesticContent("qwen",mode:"coding")
        XCTAssertTrue(store.updateDomesticContent(try qwen(),provider:"qwen",mode:"coding",generation:initial))
        let wallet = store.selectDomesticContent("qwen",mode:"balance")
        XCTAssertNil(store.snapshot().domesticQuotas?.alibaba)
        XCTAssertFalse(store.updateDomesticContent(try qwen(used:1000),provider:"qwen",mode:"coding",generation:initial))
        XCTAssertTrue(store.updateDomesticContent(quota("alibaba","现金余额",balance:15),provider:"qwen",mode:"balance",generation:wallet))
        XCTAssertEqual(store.snapshot().domesticQuotas?.alibaba?.balance,15)
        _ = store.selectDomesticContent("qwen",mode:"coding")
        XCTAssertFalse(store.updateDomesticContent(try qwen(used:1000),provider:"qwen",mode:"coding",generation:initial))
        XCTAssertEqual(store.snapshot().domesticQuotas?.alibaba?.primaryPercent,0)
    }
    func testIndependentStatisticsRestoreAfterRestart() throws {
        let prefs = defaults(), store = MacDataStore(defaults:prefs)
        let coding = store.selectDomesticContent("qwen",mode:"coding")
        XCTAssertTrue(store.updateDomesticContent(try qwen(),provider:"qwen",mode:"coding",generation:coding))
        let wallet = store.selectDomesticContent("qwen",mode:"balance")
        XCTAssertTrue(store.updateDomesticContent(quota("alibaba","现金余额",balance:15),provider:"qwen",mode:"balance",generation:wallet))
        let restored = MacDataStore(defaults:prefs)
        XCTAssertEqual(restored.selectedDomesticMode("qwen"),"balance")
        XCTAssertEqual(restored.snapshot().domesticQuotas?.alibaba?.balance,15)
        restored.selectDomesticContent("qwen",mode:"coding")
        XCTAssertEqual(restored.snapshot().domesticQuotas?.alibaba?.weeklyPercent,20)
    }
    func testNamesAndAccountWindowOffscreen() throws {
        _ = NSApplication.shared
        let prefs = defaults(), store = MacDataStore(defaults:prefs)
        let revision = store.selectDomesticContent("zhipu",mode:"plan")
        XCTAssertTrue(store.updateDomesticContent(quota("zhipu","GLM Coding Plan",percent:25),provider:"zhipu",mode:"plan",generation:revision))
        let window = MacDomesticQuotaWindow(service:.init(store:store),store:store,defaults:prefs,loadPages:false)
        defer { window.stop(); window.window?.close() }
        window.selectProvider("zhipu",userAction:false)
        XCTAssertTrue(window.accountSummary.hasPrefix("Coding Plan"))
        XCTAssertEqual(window.selectedContentLabels,["Coding Plan","API 账户余额"])
        XCTAssertFalse(window.accountSummary.contains("GLM Coding Plan"))
        XCTAssertEqual(store.snapshot().domesticQuotas?.zhipu?.plan,"Coding Plan")
        window.selectContentMode("console")
        XCTAssertEqual(window.accountSummary,"尚无成功账户数据")
        let wallet = store.domesticGeneration("zhipu")
        XCTAssertTrue(store.updateDomesticContent(quota("zhipu","开放平台余额",balance:10),provider:"zhipu",mode:"console",generation:wallet))
        XCTAssertTrue(window.accountSummary.contains("10.00"))
        window.selectContentMode("plan")
        XCTAssertTrue(window.accountSummary.hasPrefix("Coding Plan"))
        for (raw,expected) in [("百炼 Token Plan","Token Plan"),("Kimi Coding Plan","Coding Plan"),("MiMo Token Plan","Token Plan"),("MiniMax M Plan","Token Plan"),("M Plan","Token Plan"),("Step Plan","Coding Plan"),("百度云现金余额","百度云现金余额")] {
            XCTAssertEqual(MacDomesticSources.displayName(raw),expected)
        }
        let view = try XCTUnwrap(window.window?.contentView)
        view.layoutSubtreeIfNeeded()
        let bitmap = try XCTUnwrap(view.bitmapImageRepForCachingDisplay(in:view.bounds))
        view.cacheDisplay(in:view.bounds,to:bitmap)
        XCTAssertGreaterThan(bitmap.pixelsWide,0)
        let png = try XCTUnwrap(bitmap.representation(using:.png,properties:[:]))
        try png.write(to:URL(fileURLWithPath:NSTemporaryDirectory()).appendingPathComponent("aibot-mac-model-account-preview.png"))
    }
    func testZhipuPersonalPlanAndFailureRetention() throws {
        let request = try MacDomesticSources.request("zhipu",.init(mode:"plan",key:"synthetic-key"))
        XCTAssertEqual(request.url?.absoluteString,"https://open.bigmodel.cn/api/monitor/usage/quota/limit")
        XCTAssertEqual(request.value(forHTTPHeaderField:"Authorization"),"synthetic-key")
        let good = Data(#"{"success":true,"code":200,"data":{"limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":0,"nextResetTime":1793491200000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":60,"nextResetTime":1794096000000},{"type":"TIME_LIMIT","unit":3,"number":5,"percentage":90}]}}"#.utf8)
        let value = try MacDomesticSources.parse("zhipu",mode:"plan",bytes:good)
        XCTAssertEqual(value.primaryPercent,0); XCTAssertEqual(value.weeklyPercent,60)
        XCTAssertEqual(value.primaryWindowSeconds,18000); XCTAssertEqual(value.weeklyWindowSeconds,604800)
        XCTAssertEqual(value.primaryResetsAt,Date(timeIntervalSince1970:1793491200))
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("zhipu",mode:"plan")
        XCTAssertTrue(store.updateDomesticContent(value,provider:"zhipu",mode:"plan",generation:generation))
        XCTAssertThrowsError(try MacDomesticSources.parse("zhipu",mode:"plan",bytes:Data(#"{"success":false,"code":401}"#.utf8)))
        store.markDomesticContentStale("zhipu",mode:"plan",generation:generation)
        XCTAssertEqual(store.snapshot().domesticQuotas?.zhipu?.weeklyPercent,60)
    }
    func testGLMMirrorPlanOffscreen() throws {
        let store = MacDataStore(defaults:defaults())
        let value = try MacDomesticSources.parse("zhipu",mode:"plan",bytes:Data(#"{"success":true,"data":{"limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":20},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":60}]}}"#.utf8))
        let generation = store.selectDomesticContent("zhipu",mode:"plan")
        XCTAssertTrue(store.updateDomesticContent(value,provider:"zhipu",mode:"plan",generation:generation))
        let reader = SessionActivityReader(metadata:MacActivitySampler(scan:{ _ in MacActivityTotals() }))
        var snapshot = reader.capture(extras:store.snapshot())
        snapshot.displayPolicy = MacDisplayPolicy(selectedMode:"domestic_zhipu",cycleEnabled:false,intervalSeconds:15,pages:["domestic_zhipu"])
        XCTAssertEqual(snapshot.domesticQuotas?.zhipu?.primaryPercent,20)
        XCTAssertEqual(snapshot.domesticQuotas?.zhipu?.weeklyPercent,60)
        XCTAssertNil(snapshot.domesticQuotas?.zhipu?.balance)
        let view = MacMirrorView(frame:NSRect(x:0,y:0,width:240,height:240))
        view.capture = { snapshot }
        let bitmap = try XCTUnwrap(view.bitmapImageRepForCachingDisplay(in:view.bounds))
        view.cacheDisplay(in:view.bounds,to:bitmap)
        let png = try XCTUnwrap(bitmap.representation(using:.png,properties:[:]))
        try png.write(to:URL(fileURLWithPath:NSTemporaryDirectory()).appendingPathComponent("aibot-mac-glm-plan-preview.png"))
    }
    func testQwenThreeWindowsAndZero() throws {
        let value = try qwen()
        XCTAssertEqual(value.primaryPercent,0)
        XCTAssertEqual(value.weeklyPercent,20)
        XCTAssertEqual(value.planPercent,25)
        XCTAssertEqual(value.primaryWindowSeconds,18000)
        XCTAssertEqual(value.weeklyWindowSeconds,604800)
        XCTAssertEqual(value.primaryResetsAt,ISO8601DateFormatter().date(from:"2026-11-27T10:00:00Z"))
        XCTAssertEqual(value.weeklyResetsAt,ISO8601DateFormatter().date(from:"2026-11-29T16:00:00Z"))
        XCTAssertEqual(value.planResetsAt,ISO8601DateFormatter().date(from:"2026-12-02T16:00:00Z"))
    }
    func testQwenInvalidResponsesRetainCache() throws {
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("qwen",mode:"coding")
        let before = try qwen()
        XCTAssertTrue(store.updateDomesticContent(before,provider:"qwen",mode:"coding",generation:generation))
        XCTAssertThrowsError(try qwen(["INVALID"]))
        XCTAssertThrowsError(try qwen(["VALID","VALID"]))
        XCTAssertThrowsError(try qwen(used:-1))
        XCTAssertThrowsError(try qwen(total:0))
        XCTAssertEqual(store.domesticContentSnapshot("qwen",mode:"coding"),before)
    }
    func testQwenMonthlyResetWithoutInventedDuration() throws {
        for (start,reset,days) in [("2026-02-01 00:00:00","2026-03-01 00:00:00",28),("2026-01-01 00:00:00","2026-02-01 00:00:00",31),("","",0),("","2026-04-01 00:00:00",0)] {
            let prefs = defaults(), store = MacDataStore(defaults:prefs)
            let generation = store.selectDomesticContent("qwen",mode:"coding")
            let value = try qwen(reset:reset,start:start.isEmpty ? nil:start)
            let duration: Int64? = days == 0 ? nil:Int64(days*86400)
            XCTAssertEqual(value.planWindowSeconds,duration)
            XCTAssertTrue(store.updateDomesticContent(value,provider:"qwen",mode:"coding",generation:generation))
            let restored = MacDataStore(defaults:prefs).snapshot().domesticQuotas?.alibaba
            XCTAssertEqual(restored?.planResetsAt,value.planResetsAt)
            XCTAssertEqual(restored?.planWindowSeconds,duration)
            if reset.isEmpty { XCTAssertNil(value.planResetsAt) } else { XCTAssertNotNil(value.planResetsAt) }
        }
    }
    func testMiniMaxCurrentChinaEntryAndCredential() throws {
        let saved = try JSONEncoder().encode(MacDomesticCredential(mode:"api",key:"synthetic-key"))
        let credential = try JSONDecoder().decode(MacDomesticCredential.self,from:saved)
        let request = try MacDomesticSources.request("minimax",credential)
        XCTAssertEqual(request.url?.absoluteString,"https://www.minimax.cn/v1/token_plan/remains")
        XCTAssertEqual(request.value(forHTTPHeaderField:"Authorization"),"Bearer synthetic-key")
        XCTAssertEqual(MacModelAccountCatalog.entry("minimax",mode:"api")?.host,"platform.minimax.cn")
    }
    func testMiniMaxExactWebCapture() throws {
        let paths = ["/v1/token_plan/remains","/v1/api/openplatform/coding_plan/remains","/backend/account/token_plan/remains_percent"]
        for host in ["www.minimax.cn","platform.minimax.cn","www.minimaxi.com","platform.minimaxi.com"] {
            for path in paths {
                XCTAssertTrue(MacDomesticBrowser.endpoint(provider:"minimax",url:URL(string:"https://"+host+path)!))
                for bad in ["https://"+host+":444"+path,"https://"+host+".invalid"+path,"https://"+host+path+"/","https://"+host+path.uppercased()] {
                    XCTAssertFalse(MacDomesticBrowser.endpoint(provider:"minimax",url:URL(string:bad)!))
                }
            }
        }
        XCTAssertGreaterThan(try MacDomesticCaptureValidation.run(),30)
    }
    func testMiniMaxInvalidStatusPreservesLastSuccess() throws {
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("minimax",mode:"api")
        let good = Data(#"{"base_resp":{"status_code":0},"data":{"items":[{"model_name":"general","current_weekly_used_percent":30}]}}"#.utf8)
        let value = try MacDomesticSources.parse("minimax",mode:"api",bytes:good)
        XCTAssertTrue(store.updateDomesticContent(value,provider:"minimax",mode:"api",generation:generation))
        XCTAssertThrowsError(try MacDomesticSources.parse("minimax",mode:"api",bytes:Data(#"{"base_resp":{"status_code":401},"data":{"items":[{"model_name":"general","current_weekly_used_percent":99}]}}"#.utf8)))
        store.markDomesticContentStale("minimax",mode:"api",generation:generation)
        XCTAssertEqual(store.snapshot().domesticQuotas?.miniMax?.weeklyPercent,30)
    }
    func testMiMoManagementAndCreditsFailureRetention() throws {
        XCTAssertEqual(MacModelAccountCatalog.entry("xiaomi",mode:"console")?.fragment,"/console/plan-manage")
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("xiaomi",mode:"console")
        let value = try MacDomesticSources.parse("xiaomi",mode:"console",bytes:Data(#"{"data":{"totalCredits":100,"usedCredits":20}}"#.utf8))
        XCTAssertTrue(store.updateDomesticContent(value,provider:"xiaomi",mode:"console",generation:generation))
        XCTAssertThrowsError(try MacDomesticSources.parse("xiaomi",mode:"console",bytes:Data(#"{"success":false,"data":{"usedPercent":90}}"#.utf8)))
        XCTAssertEqual(store.snapshot().domesticQuotas?.xiaomi?.planPercent,20)
    }
    private func checkEntry(_ provider:String,_ mode:String, _ path:String) throws {
        let url = try XCTUnwrap(MacModelAccountCatalog.entry(provider,mode:mode))
        XCTAssertEqual(url.path.trimmingCharacters(in:CharacterSet(charactersIn:"/")),path.trimmingCharacters(in:CharacterSet(charactersIn:"/")))
        XCTAssertTrue(MacModelAccountCatalog.navigationAllowed(url,provider:provider,mainFrame:true))
        XCTAssertTrue(MacDomesticPlanPage.supports(provider,mode:mode))
        XCTAssertTrue(MacDomesticSources.modes(provider).contains(where:{$0.0 == mode}))
        if provider != "huawei" { XCTAssertTrue(MacDomesticPlanPage.allows(provider,mode:mode,url:url)) }
        XCTAssertFalse(MacDomesticPlanPage.allows(provider,mode:mode,url:URL(string:"https://"+url.host!+".invalid"+url.path)!))
    }
    func testVolcenginePlan() throws {
        try checkEntry("volcengine","coding","/ark/region:ark+cn-beijing/codingPlan")
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("volcengine",mode:"coding")
        let value = try MacDomesticPlanPage.parse("volcengine",mode:"coding",rows:[["label":"每周","percent":20]])
        XCTAssertTrue(store.updateDomesticContent(value,provider:"volcengine",mode:"coding",generation:generation))
        XCTAssertEqual(store.domesticContentSnapshot("volcengine",mode:"coding")?.weeklyPercent,20)
        XCTAssertNil(store.snapshot().domesticQuotas?["volcengine"])
    }
    func testTencentSeparatePlans() throws {
        try checkEntry("tencent","coding","/tokenhub/codingplan")
        try checkEntry("tencent","token","/tokenhub/tokenplan")
        XCTAssertFalse(MacDomesticPlanPage.allows("tencent",mode:"coding",url:URL(string:"https://console.cloud.tencent.com/tokenhub/tokenplan")!))
        let store = MacDataStore(defaults:defaults())
        for (mode,pct) in [("coding",20.0),("token",80.0)] {
            let generation = store.selectDomesticContent("tencent",mode:mode)
            let value = try MacDomesticPlanPage.parse("tencent",mode:mode,rows:[["label":"每周","percent":pct]])
            XCTAssertTrue(store.updateDomesticContent(value,provider:"tencent",mode:mode,generation:generation))
        }
        XCTAssertEqual(store.domesticContentSnapshot("tencent",mode:"coding")?.weeklyPercent,20)
        XCTAssertEqual(store.domesticContentSnapshot("tencent",mode:"token")?.weeklyPercent,80)
    }
    func testBaiduTokenSeparateFromResourcePackage() throws {
        try checkEntry("baidu","token","/qianfan/resource/token-plan")
        XCTAssertTrue(MacDomesticSources.modes("baidu").contains(where:{$0.0 == "package"}))
        XCTAssertFalse(MacDomesticPlanPage.supports("baidu",mode:"package"))
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("baidu",mode:"token")
        XCTAssertTrue(store.updateDomesticContent(try MacDomesticPlanPage.parse("baidu",mode:"token",rows:[["label":"每周","percent":30]]),provider:"baidu",mode:"token",generation:generation))
        XCTAssertNil(store.domesticContentSnapshot("baidu",mode:"package"))
    }
    func testStepfunPlanSeparateFromWallet() throws {
        try checkEntry("stepfun","coding","/step-plan")
        XCTAssertTrue(MacDomesticSources.modes("stepfun").contains(where:{$0.0 == "api"}))
        XCTAssertFalse(MacDomesticPlanPage.supports("stepfun",mode:"api"))
        let wallet = try MacDomesticSources.parse("stepfun",mode:"api",bytes:Data(#"{"object":"account","type":"prepaid","balance":12}"#.utf8))
        XCTAssertEqual(wallet.balance,12); XCTAssertNil(wallet.planPercent)
    }
    func testHuaweiPlanDesktopOnly() throws {
        try checkEntry("huawei","token","/maas")
        XCTAssertTrue(MacDomesticPlanPage.allows("huawei",mode:"token",url:URL(string:"https://console.huaweicloud.com/maas/token-plan")!))
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("huawei",mode:"token")
        let value = try MacDomesticPlanPage.parse("huawei",mode:"token",rows:[["label":"每周","percent":40]])
        XCTAssertTrue(store.updateDomesticContent(value,provider:"huawei",mode:"token",generation:generation))
        XCTAssertEqual(store.domesticContentSnapshot("huawei",mode:"token")?.weeklyPercent,40)
        XCTAssertNil(store.snapshot().domesticQuotas?["huawei"])
    }
    private final class Navigation: NSObject, WKNavigationDelegate {
        var finished = false
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) { finished = true }
    }
    func testVisibleDOMProgressAndInvalidContent() throws {
        _ = NSApplication.shared
        let configuration = WKWebViewConfiguration(); configuration.websiteDataStore = .nonPersistent()
        let view = WKWebView(frame:NSRect(x:0,y:0,width:800,height:600),configuration:configuration)
        let navigation = Navigation(); view.navigationDelegate = navigation
        defer { view.stopLoading(); view.navigationDelegate = nil }
        func extract(_ html:String) throws -> [[String:Any]] {
            navigation.finished = false
            view.loadHTMLString("<html><body><h1>Coding Plan</h1>"+html+"</body></html>",baseURL:nil)
            let deadline = Date().addingTimeInterval(10)
            while !navigation.finished && Date() < deadline { RunLoop.current.run(until:Date(timeIntervalSinceNow:0.02)) }
            XCTAssertTrue(navigation.finished)
            var result: [[String:Any]]?, finished = false
            view.evaluateJavaScript(MacDomesticPlanPage.script) { value,_ in result = value as? [[String:Any]]; finished = true }
            while !finished && Date() < deadline { RunLoop.current.run(until:Date(timeIntervalSinceNow:0.02)) }
            return try XCTUnwrap(result)
        }
        let html = """
        <section>5小时 已用 <div role="progressbar">0%</div></section>
        <section>每周 剩余 <div role="progressbar">80%</div></section>
        <section>订阅月 已用 <div role="progressbar">60%</div></section>
        <section style="display:none">每周 已用 <div role="progressbar">95%</div></section>
        <section>账户余额 每周 已用 <div role="progressbar">99%</div></section>
        <section>每周 已过期 已用 <div role="progressbar">90%</div></section>
        """
        let value = try MacDomesticPlanPage.parse("tencent",mode:"coding",rows:extract(html))
        XCTAssertEqual(value.primaryPercent,0); XCTAssertEqual(value.weeklyPercent,20); XCTAssertEqual(value.planPercent,60)
        XCTAssertThrowsError(try MacDomesticPlanPage.parse("tencent",mode:"coding",rows:extract(html+"<section>每周 已用 <div role='progressbar'>30%</div></section>")))
        XCTAssertThrowsError(try MacDomesticPlanPage.parse("tencent",mode:"coding",rows:extract("<section>5小时 每周 已用 <div role='progressbar'>30%</div></section>")))
        let store = MacDataStore(defaults:defaults()), generation = store.selectDomesticContent("tencent",mode:"coding")
        XCTAssertTrue(store.updateDomesticContent(value,provider:"tencent",mode:"coding",generation:generation))
        let invalid = try extract("<section hidden>每周 已用 <div role='progressbar'>90%</div></section><section>余额 每周 已用 <div role='progressbar'>70%</div></section>")
        XCTAssertThrowsError(try MacDomesticPlanPage.parse("tencent",mode:"coding",rows:invalid))
        store.markDomesticContentStale("tencent",mode:"coding",generation:generation)
        XCTAssertEqual(store.domesticContentSnapshot("tencent",mode:"coding")?.weeklyPercent,20)
    }
}
