import AppKit
import WebKit

final class MacDomesticQuotaWindow: NSWindowController, WKScriptMessageHandler, WKNavigationDelegate, NSWindowDelegate {
    private let service: MacDomesticQuotaService
    private let store: MacDataStore
    private let key = NSSecureTextField()
    private let status = NSTextField(wrappingLabelWithString: "")
    private var browser: WKWebView?
    private var browserWindow: NSWindow?
    private var timer: Timer?
    private var zhipuNote = "智谱：请打开官方财务页授权读取"

    init(service: MacDomesticQuotaService, store: MacDataStore) {
        self.service = service; self.store = store
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 580, height: 330),
                              styleMask: [.titled, .closable], backing: .buffered, defer: false)
        super.init(window: window)
        window.title = "TAB5 · 国产模型额度"; window.center()
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.frame = NSRect(x: 24, y: 20, width: 532, height: 285)
        let description = NSTextField(wrappingLabelWithString: "DeepSeek 仅查询官方余额接口，API Key 保存到本机钥匙串。智谱通过官方财务页读取可用余额；登录信息只留在本次网页会话，桥接不读取密码或 Cookie。余额会显示到已配对 TAB5。")
        description.widthAnchor.constraint(equalToConstant: 532).isActive = true
        key.placeholderString = "DeepSeek API Key（不回显已保存的值）"
        key.widthAnchor.constraint(equalToConstant: 532).isActive = true
        stack.addArrangedSubview(description); stack.addArrangedSubview(key)
        stack.addArrangedSubview(NSButton(title: "保存 DeepSeek 授权并查询", target: self, action: #selector(saveDeepSeek)))
        stack.addArrangedSubview(NSButton(title: "打开智谱官方财务页…", target: self, action: #selector(openZhipu)))
        stack.addArrangedSubview(NSButton(title: "刷新已授权额度", target: self, action: #selector(refresh)))
        status.widthAnchor.constraint(equalToConstant: 532).isActive = true
        stack.addArrangedSubview(status); window.contentView?.addSubview(stack)
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refreshStatus() }
        refreshStatus()
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate() }
    func open() { showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    private func refreshStatus() {
        Task { [weak self] in
            guard let self else { return }
            let note = await service.diagnostic()
            await MainActor.run { self.status.stringValue = note + "\n" + self.zhipuNote }
        }
    }
    @objc private func saveDeepSeek() {
        let value = key.stringValue; key.stringValue = ""
        Task {
            do { try await service.saveKey(value); await service.refresh() }
            catch { await MainActor.run { self.status.stringValue = "未保存：请检查 API Key 或钥匙串授权。" } }
        }
    }
    @objc private func refresh() {
        Task { await service.refresh() }
        browser?.reload()
    }
    nonisolated static func financeEndpoint(_ value: String) -> Bool {
        guard let url = URL(string: value) else { return false }
        return url.scheme == "https" && url.host == "bigmodel.cn" && (url.port == nil || url.port == 443)
            && url.path == "/api/biz/account/query-customer-account-report"
    }
    @objc private func openZhipu() {
        if let browserWindow { browserWindow.makeKeyAndOrderFront(nil); browser?.reload(); return }
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .nonPersistent()
        let controller = WKUserContentController()
        // Observe only the finance response already requested by the official
        // page. Do not inspect DOM balances, storage, request headers or cookies.
        let script = """
        (() => {
          const allowed = value => { try {
            const u = new URL(value, location.href);
            return u.origin === 'https://bigmodel.cn' && u.pathname === '/api/biz/account/query-customer-account-report';
          } catch { return false; } };
          const emit = (url, text) => {
            if (allowed(url) && typeof text === 'string' && text.length <= 65536)
              window.webkit.messageHandlers.balance.postMessage({url, text});
          };
          const originalFetch = window.fetch;
          window.fetch = async function(...args) {
            const response = await originalFetch.apply(this, args);
            if (response.ok && allowed(response.url))
              response.clone().text().then(text => emit(response.url, text)).catch(() => {});
            return response;
          };
          const send = XMLHttpRequest.prototype.send;
          XMLHttpRequest.prototype.send = function(...args) {
            this.addEventListener('load', () => {
              if (this.status === 200 && allowed(this.responseURL)) {
                try { emit(this.responseURL, this.responseType === 'json' ? JSON.stringify(this.response) : this.responseText); } catch {}
              }
            }, {once:true});
            return send.apply(this, args);
          };
        })();
        """
        controller.addUserScript(WKUserScript(source: script, injectionTime: .atDocumentStart, forMainFrameOnly: true))
        controller.add(self, name: "balance"); config.userContentController = controller
        let web = WKWebView(frame: NSRect(x: 0, y: 0, width: 1050, height: 740), configuration: config)
        web.navigationDelegate = self
        let window = NSWindow(contentRect: web.bounds, styleMask: [.titled, .closable, .resizable],
                              backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false; window.delegate = self; window.title = "智谱官方财务页 · 仅同步余额"
        window.contentView = web; window.center()
        browser = web; browserWindow = window
        web.load(URLRequest(url: URL(string: "https://bigmodel.cn/finance-center/finance/overview")!))
        window.makeKeyAndOrderFront(nil)
    }
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        let origin = message.frameInfo.securityOrigin
        guard message.frameInfo.isMainFrame, origin.protocol == "https", origin.host == "bigmodel.cn",
              let body = message.body as? [String: Any], let url = body["url"] as? String,
              Self.financeEndpoint(url), let text = body["text"] as? String, text.utf8.count <= 65_536 else { return }
        do {
            store.updateDomestic(try MacDomesticQuotaParser.zhipu(Data(text.utf8)))
            zhipuNote = "智谱：官方可用余额已更新（关闭财务页后保留最后结果）"
        } catch {
            store.markDomesticStale("zhipu"); zhipuNote = "智谱：官方数据暂不可用，保留已有余额"
        }
    }
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction,
                 decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        guard let url = navigationAction.request.url, url.scheme == "https",
              let host = url.host, host == "bigmodel.cn" || host.hasSuffix(".bigmodel.cn") else {
            decisionHandler(.cancel); return
        }
        decisionHandler(.allow)
    }
    func windowWillClose(_ notification: Notification) {
        guard notification.object as? NSWindow === browserWindow else { return }
        browser?.stopLoading()
        browser?.configuration.userContentController.removeScriptMessageHandler(forName: "balance")
        browser = nil; browserWindow = nil
        store.markDomesticStale("zhipu")
        zhipuNote = "智谱：财务页已关闭，显示上次读取结果；再次打开可刷新"
    }
}
