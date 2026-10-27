import AppKit
import WebKit

// WebKit owns its login store; the bridge never reads cookies, passwords,
// storage or request headers. Only known quota response bodies cross this bridge.
final class MacDomesticBrowser: NSObject, WKScriptMessageHandler, WKNavigationDelegate, WKUIDelegate {
    let provider: String
    let webView: WKWebView
    private let store: MacDataStore
    private let captureEnabled: () -> Bool
    private let separateDeepSeek: () -> Bool
    private let note: (String) -> Void
    private let definition: MacModelAccountProvider
    private(set) var refreshState = MacConsoleRefreshState()
    private var loaded = false
    nonisolated static func endpoint(provider: String, url: URL) -> Bool {
        guard url.scheme == "https", url.port == nil || url.port == 443, url.user == nil, url.password == nil else { return false }
        let host = url.host?.lowercased() ?? "", path = url.path.lowercased()
        switch provider {
        case "qwen": return host == "bailian.console.aliyun.com" && (path == "/data/api.json" || path.contains("quota") || path.contains("subscription") || path.contains("token-plan"))
        case "kimi": return ["www.kimi.com","kimi.com","api.kimi.com"].contains(host) && (path.contains("usage") || path.contains("quota") || path.hasSuffix("billingservice/getusage"))
        case "minimax": return ["platform.minimaxi.com","www.minimaxi.com"].contains(host) && ["usage","token_plan","token-plan","coding_plan","model_remains","remains"].contains { path.contains($0) }
        case "zhipu": return host == "bigmodel.cn" && path == "/api/biz/account/query-customer-account-report"
        case "xiaomi": return host == "platform.xiaomimimo.com" && path == "/api/v1/tokenplan/usage"
        case "deepseek": return host == "platform.deepseek.com" && (path.hasSuffix("/get_user_summary") || path.hasSuffix("/balance"))
        default: return false
        }
    }
    init(provider: String, store: MacDataStore, persistent: Bool = true,
         captureEnabled: @escaping () -> Bool, separateDeepSeek: @escaping () -> Bool = {false}, note: @escaping (String) -> Void) {
        self.provider = provider; self.store = store; self.captureEnabled = captureEnabled; self.note = note
        self.separateDeepSeek = separateDeepSeek
        definition = MacModelAccountCatalog.provider(provider)!
        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = persistent ? .default():.nonPersistent()
        webView = WKWebView(frame:.zero,configuration:configuration)
        super.init()
        let providerJSON = String(data:try! JSONEncoder().encode(provider),encoding:.utf8)!
        let script = """
        (() => {
          const provider=\(providerJSON);
          const allowed = value => { try { const u=new URL(value,location.href), p=u.pathname.toLowerCase(), h=u.hostname;
            if(u.protocol!=='https:' || (u.port && u.port!=='443') || u.username || u.password) return false;
            if(provider==='zhipu') return h==='bigmodel.cn' && p==='/api/biz/account/query-customer-account-report';
            if(provider==='xiaomi') return h==='platform.xiaomimimo.com' && p==='/api/v1/tokenplan/usage';
            if(provider==='qwen') return h==='bailian.console.aliyun.com' && (p==='/data/api.json'||p.includes('quota')||p.includes('subscription')||p.includes('token-plan'));
            if(provider==='kimi') return ['www.kimi.com','kimi.com','api.kimi.com'].includes(h) && (p.includes('usage')||p.includes('quota')||p.endsWith('billingservice/getusage'));
            if(provider==='minimax') return ['platform.minimaxi.com','www.minimaxi.com'].includes(h) && ['usage','token_plan','token-plan','coding_plan','model_remains','remains'].some(v=>p.includes(v));
            if(provider==='deepseek') return h==='platform.deepseek.com' && (p.endsWith('/get_user_summary')||p.endsWith('/balance'));
            return false;
          } catch {return false;} };
          const emit=(url,status,text)=>{if(allowed(url)&&typeof text==='string'&&text.length<=1048576)window.webkit.messageHandlers.quota.postMessage({url,status,text});};
          const originalFetch=window.fetch;window.fetch=async function(...args){const response=await originalFetch.apply(this,args);if(allowed(response.url))response.clone().text().then(text=>emit(response.url,response.status,text)).catch(()=>{});return response;};
          const send=XMLHttpRequest.prototype.send;XMLHttpRequest.prototype.send=function(...args){this.addEventListener('load',()=>{if(allowed(this.responseURL)){try{emit(this.responseURL,this.status,this.responseType==='json'?JSON.stringify(this.response):this.responseText);}catch{}}},{once:true});return send.apply(this,args);};
        })();
        """
        webView.configuration.userContentController.addUserScript(.init(source:script,injectionTime:.atDocumentStart,forMainFrameOnly:true))
        webView.configuration.userContentController.add(self,name:"quota")
        webView.navigationDelegate = self; webView.uiDelegate = self
    }
    func loadIfNeeded() { if !loaded { reload() } }
    func reload() {
        loaded = true; refreshState.start(Date().timeIntervalSince1970)
        note(captureEnabled() ? "正在加载官方控制台；等待登录或额度响应…":"官方控制台；额度仍使用已选择的数据来源。")
        webView.load(URLRequest(url:definition.url))
    }
    func tick(now: Date = Date(), interactive: Bool) {
        guard captureEnabled() else { return }
        let time = now.timeIntervalSince1970
        if refreshState.timeout(time) { markStale(); note("尚未取得新的额度响应；保留上次数据。请检查右侧登录状态，或刷新网页重试。") }
        if refreshState.due(time,interactive:interactive) { reload() }
    }
    func stop() {
        webView.stopLoading(); webView.configuration.userContentController.removeScriptMessageHandler(forName:"quota")
        webView.navigationDelegate = nil; webView.uiDelegate = nil
    }
    func userContentController(_ controller: WKUserContentController, didReceive message: WKScriptMessage) {
        let origin = message.frameInfo.securityOrigin
        guard captureEnabled(), message.frameInfo.isMainFrame, origin.protocol == "https",
              let originURL = URL(string:"https://"+origin.host),
              MacModelAccountCatalog.navigationAllowed(originURL,provider:provider,mainFrame:true),
              let body = message.body as? [String:Any], let raw = body["url"] as? String,
              let url = URL(string:raw), Self.endpoint(provider:provider,url:url),
              let status = body["status"] as? Int, let text = body["text"] as? String, text.utf8.count <= 1_048_576 else { return }
        receive(status:status,bytes:Data(text.utf8))
    }
    func receive(status: Int, bytes: Data, now: Date = Date()) {
        guard captureEnabled() else { return }
        guard status == 200 else {
            refreshState.fail(now.timeIntervalSince1970,login:status == 401 || status == 403)
            markStale()
            note(status == 401 || status == 403 ? "登录已失效或未获授权，请在右侧重新登录；保留上次数据。":status == 429 ? "官方接口限流，5 分钟后重试；保留上次数据。":"官方额度请求失败（HTTP \(status)），保留上次数据。")
            return
        }
        do {
            let value = try MacDomesticSources.parse(provider,mode:"console",bytes:bytes,now:now)
            if provider == "deepseek" { store.updateDeepSeekWeb(value) }
            if provider != "deepseek" || !separateDeepSeek() { store.updateDomestic(value) }
            refreshState.success(now.timeIntervalSince1970)
            note("已取得官方账户数据；关闭此窗口后仍会后台刷新。")
        } catch { /* Unrelated JSON never replaces a successful quota sample. */ }
    }
    func webView(_ webView: WKWebView, decidePolicyFor action: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        guard let url = action.request.url, MacModelAccountCatalog.navigationAllowed(url,provider:provider,mainFrame:action.targetFrame?.isMainFrame ?? true) else {
            if action.targetFrame?.isMainFrame ?? true { note("此页面跳转不在已知官方登录地址内；可使用“在浏览器打开”完成查看。") }
            decisionHandler(.cancel); return
        }
        decisionHandler(.allow)
    }
    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration, for action: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        if action.targetFrame == nil, let url = action.request.url, MacModelAccountCatalog.navigationAllowed(url,provider:provider,mainFrame:true) { webView.load(action.request) }
        return nil
    }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) { failed(error) }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) { failed(error) }
    private func failed(_ error: Error) {
        guard (error as NSError).code != NSURLErrorCancelled else { return }
        refreshState.fail(Date().timeIntervalSince1970); if captureEnabled() { markStale() }
        note("官方网页加载失败，请检查网络或稍后刷新；保留上次数据。")
    }
    func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
        refreshState.fail(Date().timeIntervalSince1970); note("网页进程已停止，将在后台重试；也可手动刷新。")
        if captureEnabled() { markStale() }
    }
    private func markStale() {
        if provider == "deepseek" { store.markDeepSeekWebStale() }
        if provider != "deepseek" || !separateDeepSeek() { store.markDomesticStale(provider) }
    }
}
