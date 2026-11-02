import AppKit
import WebKit

// Execute the real injected filter in an offscreen, nonpersistent WebKit page.
// This never loads an account site, reads a login store or presents a window.
enum MacDomesticCaptureValidation {
    private final class Navigation: NSObject, WKNavigationDelegate {
        var finished = false, failed = false
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) { finished = true }
        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) { failed = true }
        func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) { failed = true }
    }
    static func run() throws -> Int {
        _ = NSApplication.shared; NSApp.setActivationPolicy(.prohibited)
        let suite = "aibot-capture-"+UUID().uuidString
        let isolated = UserDefaults(suiteName:suite)!; defer { isolated.removePersistentDomain(forName:suite) }
        let store = MacDataStore(defaults:isolated)
        var count = 0
        func check(_ label: String, _ ok: Bool) throws { guard ok else { throw MacTab5LocalError.invalid("Capture contract: "+label) }; count += 1 }
        let urls = ["zhipu":"https://bigmodel.cn/api/biz/account/finance-overview",
                    "xiaomi":"https://platform.xiaomimimo.com/api/v1/tokenPlan/usage",
                    "minimax":"https://www.minimaxi.com/v1/token_plan/remains",
                    "kimi":"https://api.kimi.com/BillingService/GetUsage",
                    "qwen":"https://bailian.console.aliyun.com/data/api.json",
                    "deepseek":"https://platform.deepseek.com/api/get_user_summary"]
        for provider in urls.keys.sorted() {
            let good = urls[provider]!, url = URL(string:good)!, host = url.host!, path = url.path
            var samples: [(String,Bool)] = [(good,true),("https://"+host+":443"+path,true),
                ("http://"+host+path,false),("https://"+host+":444"+path,false),
                ("https://user@"+host+path,false),("https://"+host+".invalid"+path,false)]
            if provider == "zhipu" { samples += [("https://bigmodel.cn"+MacDomesticQuotaParser.zhipuLegacyPath,true),
                (good+"/",false),("https://bigmodel.cn"+path.uppercased(),false),
                (good.replacingOccurrences(of:"finance-overview",with:"finance%2Doverview"),false)] }
            let browser = MacDomesticBrowser(provider:provider,store:store,persistent:false,captureEnabled:{false},note:{_ in})
            defer { browser.stop() }
            guard let source = browser.webView.configuration.userContentController.userScripts.first?.source,
                  let marker = source.range(of:"const originalFetch=window.fetch;") else { throw MacTab5Error.unconfirmed }
            let probes = String(data:try JSONSerialization.data(withJSONObject:samples.map(\.0)),encoding:.utf8)!
            let script = source.replacingCharacters(in:marker,with:"window.__aibotProbe="+probes+".map(allowed); "+source[marker])
            let configuration = WKWebViewConfiguration(); configuration.websiteDataStore = .nonPersistent()
            configuration.userContentController.addUserScript(.init(source:script,injectionTime:.atDocumentStart,forMainFrameOnly:true))
            let view = WKWebView(frame:.zero,configuration:configuration), navigation = Navigation()
            view.navigationDelegate = navigation
            defer { view.stopLoading(); view.navigationDelegate = nil }
            view.loadHTMLString("<html><body>synthetic contract</body></html>",baseURL:nil)
            let deadline = ProcessInfo.processInfo.systemUptime+20
            while !navigation.finished && !navigation.failed && ProcessInfo.processInfo.systemUptime < deadline { RunLoop.current.run(until:Date(timeIntervalSinceNow:0.02)) }
            try check("offscreen page loaded",navigation.finished && !navigation.failed)
            var result: [Bool]?, completed = false
            view.evaluateJavaScript("window.__aibotProbe") { value,_ in result = value as? [Bool]; completed = true }
            while !completed && ProcessInfo.processInfo.systemUptime < deadline { RunLoop.current.run(until:Date(timeIntervalSinceNow:0.02)) }
            try check("injected script executed",completed && result?.count == samples.count)
            for (index,sample) in samples.enumerated() {
                try check("Swift exact endpoint",MacDomesticBrowser.endpoint(provider:provider,url:URL(string:sample.0)!) == sample.1)
                try check("WebKit exact endpoint",result?[index] == sample.1)
            }
        }
        let browser = MacDomesticBrowser(provider:"zhipu",store:store,persistent:false,captureEnabled:{true},note:{_ in})
        defer { browser.stop() }
        func json(_ body: [String:Any]) throws -> Data { try JSONSerialization.data(withJSONObject:["code":200,"data":body]) }
        let legacyURL = URL(string:"https://bigmodel.cn"+MacDomesticQuotaParser.zhipuLegacyPath)!, overviewURL = URL(string:urls["zhipu"]!)!
        let now = Date()
        browser.receive(status:200,bytes:try json(["availableBalance":"9.999","totalSpendAmount":"2.239"]),now:now,url:legacyURL)
        try check("standalone legacy browser capture ignored",store.snapshot(now:now).domesticQuotas?.zhipu == nil)
        browser.receive(status:200,bytes:try json(["availableTotalBalance":"7.345","availableBalance":100,"totalSpendAmount":99]),now:now,url:overviewURL)
        let captured = store.snapshot(now:now).domesticQuotas?.zhipu
        try check("overview browser schema",captured?.balance == 7.35 && captured?.usedCost == nil)
        browser.receive(status:200,bytes:try json(["availableBalance":0]),now:now,url:overviewURL)
        try check("wrong schema keeps cache",store.snapshot(now:now).domesticQuotas?.zhipu == captured)
        browser.receive(status:200,bytes:try json(["availableTotalBalance":0]),now:now,url:URL(string:"https://bigmodel.cn.invalid"+overviewURL.path)!)
        try check("wrong origin keeps cache",store.snapshot(now:now).domesticQuotas?.zhipu == captured)
        browser.receive(status:429,bytes:Data(),now:now,url:overviewURL)
        let stale = store.snapshot(now:now).domesticQuotas?.zhipu
        try check("HTTP failure keeps amount",stale?.balance == 7.35 && stale?.usedCost == nil && stale?.stale == true)
        return count
    }
}
