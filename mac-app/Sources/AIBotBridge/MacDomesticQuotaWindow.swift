import AppKit

final class MacDomesticQuotaWindow: NSWindowController, NSWindowDelegate {
    private let service: MacDomesticQuotaService, store: MacDataStore
    private let defaults: UserDefaults, loadPages: Bool, refreshLocal: () -> Void
    private let zoom = NSPopUpButton()
    private let zoomPercentages = [60,75,90,100,125]
    private var apiSettingsButton: NSButton?
    private let source = NSPopUpButton(), key = NSSecureTextField(), secret = NSSecureTextField()
    private let package = NSTextField(), port = NSTextField(string:"5494")
    private let heading = MacBridgeSettingsStyle.label("",size:22,weight:.semibold)
    private let product = MacBridgeSettingsStyle.note(""), account = MacBridgeSettingsStyle.label("")
    private let webAccount = MacBridgeSettingsStyle.note("")
    private let local = MacBridgeSettingsStyle.note("")
    private let status = MacBridgeSettingsStyle.note("")
    private let webHost = NSView(), credentialPage = MacBridgeSettingsPage()
    private var credentialRows: [NSView] = []
    private var buttons: [String:NSButton] = [:], notes: [String:String] = [:]
    private var sessions: [String:MacDomesticBrowser] = [:]
    private var timer: Timer?, selected = "qwen", settingsVisible = false
    private var drafts: [String:MacDomesticCredential] = [:]
    private var apiNote = ""
    private var selectionGeneration = 0

    init(service: MacDomesticQuotaService, store: MacDataStore, refreshLocal: @escaping () -> Void = {},
         defaults: UserDefaults = .standard, loadPages: Bool = true) {
        self.service = service; self.store = store; self.refreshLocal = refreshLocal
        self.defaults = defaults; self.loadPages = loadPages
        super.init(window:MacBridgeSettingsStyle.window("模型账号",width:1160,height:800,minimumHeight:620))
        window?.contentMinSize = NSSize(width:940,height:620); window?.delegate = self
        let navigation = MacBridgeSettingsPage(); navigation.heading("模型厂商")
        navigation.add(local)
        for item in MacModelAccountCatalog.all {
            let button = MacSettingsButton(item.name) { [weak self] in self?.selectProvider(item.id,userAction:true) }
            button.identifier = .init("model-provider-"+item.id); button.toolTip = item.product
            button.setButtonType(.pushOnPushOff); button.bezelStyle = .rounded
            navigation.add(button); button.widthAnchor.constraint(equalTo:navigation.stack.widthAnchor).isActive = true
            buttons[item.id] = button
        }
        navigation.add(MacBridgeSettingsStyle.note("Claude／Codex 沿用本机官方客户端授权。国产模型按各自官方来源获取数据。"))
        let right = NSStackView(); right.orientation = .vertical; right.alignment = .leading; right.spacing = 10
        let container = NSView(); right.translatesAutoresizingMaskIntoConstraints = false; container.addSubview(right)
        NSLayoutConstraint.activate([right.leadingAnchor.constraint(equalTo:container.leadingAnchor,constant:16),right.trailingAnchor.constraint(equalTo:container.trailingAnchor,constant:-16),right.topAnchor.constraint(equalTo:container.topAnchor,constant:20),right.bottomAnchor.constraint(equalTo:container.bottomAnchor,constant:-16)])
        func row(_ view: NSView) { right.addArrangedSubview(view); view.widthAnchor.constraint(equalTo:right.widthAnchor).isActive = true }
        row(heading); row(product); row(account); row(webAccount)
        source.target = self; source.action = #selector(sourceChanged)
        row(MacBridgeSettingsStyle.row([MacBridgeSettingsStyle.label("额度来源"),source]))
        zoom.addItems(withTitles:zoomPercentages.map { "网页 \($0)%" }); zoom.identifier = .init("model-web-zoom")
        zoom.target = self; zoom.action = #selector(zoomChanged); zoom.toolTip = "缩小网页可显示被遮挡的弹窗关闭按钮。"
        let apiSettings = MacSettingsButton("接口设置") { [weak self] in guard let self else { return }; self.settingsVisible.toggle(); self.updateCredentialVisibility() }
        apiSettingsButton = apiSettings
        row(MacBridgeSettingsStyle.row([
            MacSettingsButton("刷新额度") { [weak self] in self?.refreshQuota() },
            MacSettingsButton("刷新网页") { [weak self] in self?.session()?.reload() },
            apiSettings,
            MacSettingsButton("在浏览器打开") { [weak self] in guard let self, let item = MacModelAccountCatalog.provider(self.selected) else { return }; self.notes[self.selected] = NSWorkspace.shared.open(item.url) ? "已在默认浏览器打开官方控制台。":"未能打开默认浏览器。"; self.refreshStatus() }, zoom
        ]))
        key.placeholderString = "API Key、Access Key 或本地服务令牌"
        secret.placeholderString = "Secret Key"; package.placeholderString = "千帆模型资源包 ID"
        credentialPage.field("凭据（保存在钥匙串）",key); credentialPage.field("Secret Key",secret)
        credentialPage.field("资源包 ID",package); credentialPage.field("Kimi 本地端口",port)
        credentialRows = Array(credentialPage.stack.arrangedSubviews)
        credentialPage.add(MacSettingsButton("保存并测试") { [weak self] in self?.saveCredential() })
        credentialPage.heightAnchor.constraint(equalToConstant:210).isActive = true; row(credentialPage)
        row(webHost); webHost.heightAnchor.constraint(greaterThanOrEqualToConstant:160).isActive = true
        status.identifier = .init("settings-footer"); row(status)
        row(MacBridgeSettingsStyle.note("登录状态由系统网页存储管理，到期需重新登录。关闭窗口后已授权额度继续后台刷新；桥接不导出 Cookie，API 凭据保存在钥匙串。"))
        window?.contentView = MacSettingsControls.columns(navigation,container,leftWidth:232)
        if let saved = defaults.string(forKey:"model_account_selected"), MacModelAccountCatalog.provider(saved) != nil { selected = saved }
        selectProvider(selected,userAction:false)
        if loadPages { timer = Timer.scheduledTimer(withTimeInterval:2,repeats:true) { [weak self] _ in self?.tick() } }
    }
    required init?(coder:NSCoder) { fatalError() }
    deinit { stop() }
    func stop() { timer?.invalidate(); timer = nil; sessions.values.forEach { $0.stop() }; sessions.removeAll() }
    func open() { showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps:true); activateSelected(); refreshStatus() }
    func windowWillClose(_ notification: Notification) { rememberDraft() } // Keep authorized sessions refreshing in memory.
    var selectedProviderID: String { selected }
    var providerCount: Int { buttons.count }
    private var mode: String {
        let modes = MacDomesticSources.modes(selected)
        return modes.indices.contains(source.indexOfSelectedItem) ? modes[source.indexOfSelectedItem].0:""
    }
    private func rememberDraft() {
        guard !key.stringValue.isEmpty || !secret.stringValue.isEmpty || !package.stringValue.isEmpty else { return }
        drafts[selected] = .init(mode:mode,key:key.stringValue,secret:secret.stringValue,package:package.stringValue,port:Int(port.stringValue) ?? 5494)
    }
    func selectProvider(_ id: String, userAction: Bool) {
        guard let item = MacModelAccountCatalog.provider(id) else { return }
        rememberDraft(); selected = id; selectionGeneration += 1; apiNote = ""
        heading.stringValue = item.name; product.stringValue = item.product
        for (key,button) in buttons { button.state = key == id ? .on:.off }
        let modes = MacDomesticSources.modes(id); source.removeAllItems(); source.addItems(withTitles:modes.isEmpty ? ["仅官方网页（额度尚未接入）"]:modes.map(\.1)); source.isEnabled = !modes.isEmpty
        let saved = drafts[id]?.mode ?? defaults.string(forKey:"domestic_source_"+id)
        if let index = modes.firstIndex(where: { $0.0 == saved }) { source.selectItem(at:index) }
        else if !(id == "deepseek" && defaults.bool(forKey:"deepseek_quota_enabled")), item.supportsWebQuota, let index = modes.firstIndex(where: { $0.0 == "console" }) { source.selectItem(at:index) }
        key.stringValue = drafts[id]?.key ?? ""; secret.stringValue = drafts[id]?.secret ?? ""
        package.stringValue = drafts[id]?.package ?? ""; port.stringValue = String(drafts[id]?.port ?? 5494)
        let percent = defaults.integer(forKey:"model_account_zoom_"+id)
        zoom.selectItem(at:zoomPercentages.firstIndex(of:percent) ?? 3)
        updateCredentialVisibility(); refreshStatus()
        webHost.subviews.forEach { $0.removeFromSuperview() }
        if let view = sessions[id]?.webView { embed(view) }
        if userAction, loadPages { defaults.set(id,forKey:"model_account_selected"); activateSelected() }
    }
    private func embed(_ view: NSView) {
        webHost.subviews.forEach { $0.removeFromSuperview() }; view.translatesAutoresizingMaskIntoConstraints = false; webHost.addSubview(view)
        NSLayoutConstraint.activate([view.leadingAnchor.constraint(equalTo:webHost.leadingAnchor),view.trailingAnchor.constraint(equalTo:webHost.trailingAnchor),view.topAnchor.constraint(equalTo:webHost.topAnchor),view.bottomAnchor.constraint(equalTo:webHost.bottomAnchor)])
    }
    private func session(_ id: String? = nil) -> MacDomesticBrowser? {
        guard loadPages else { return nil }; let id = id ?? selected
        if let value = sessions[id] { return value }
        guard MacModelAccountCatalog.provider(id) != nil else { return nil }
        let session = MacDomesticBrowser(provider:id,store:store,captureEnabled:{ [weak self] in
            let mode = self?.defaults.string(forKey:"domestic_source_"+id)
            return mode == "console" || (id == "deepseek" && mode == "separate")
        },separateDeepSeek:{ [weak self] in self?.defaults.string(forKey:"domestic_source_deepseek") == "separate" },note:{ [weak self] value in self?.notes[id] = value; self?.refreshStatus() })
        let percent = defaults.integer(forKey:"model_account_zoom_"+id)
        session.webView.pageZoom = Double(zoomPercentages.contains(percent) ? percent:100)/100
        sessions[id] = session; return session
    }
    private func activateSelected() {
        guard let session = session() else { return }; embed(session.webView)
        let id = selected
        if mode == "console" {
            Task { [weak self] in
                guard let self else { return }; await service.useConsole(id)
                await MainActor.run { session.loadIfNeeded() }
            }
        } else { session.loadIfNeeded() }
    }
    @objc private func zoomChanged() {
        guard zoomPercentages.indices.contains(zoom.indexOfSelectedItem) else { return }
        let percent = zoomPercentages[zoom.indexOfSelectedItem]
        defaults.set(percent,forKey:"model_account_zoom_"+selected)
        sessions[selected]?.webView.pageZoom = Double(percent)/100
    }
    @objc private func sourceChanged() {
        updateCredentialVisibility()
        if selected == "deepseek", mode == "separate" {
            Task { [weak self] in
                guard let self else { return }
                let enabled = await service.useSeparateDeepSeek()
                if !enabled { await service.useConsole("deepseek") }
                await MainActor.run {
                    if !enabled, self.selected == "deepseek" {
                        self.source.selectItem(at:MacDomesticSources.modes("deepseek").firstIndex(where:{$0.0 == "console"})!)
                        self.notes["deepseek"] = "尚未配置 API Key，使用网页合计余额和消费金额。"
                    }
                    self.settingsVisible = false; self.updateCredentialVisibility()
                    if let browser = self.session("deepseek") { if self.selected == "deepseek" { self.embed(browser.webView) }; browser.reload() }
                    self.refreshStatus()
                }
                if enabled { await service.refresh() }
            }
        }
        else if mode == "console" { activateSelected() }
        else { settingsVisible = true; updateCredentialVisibility(); notes[selected] = "填写并保存接口配置后生效；现有成功数据会保留。"; refreshStatus() }
    }
    private func updateCredentialVisibility() {
        apiSettingsButton?.isEnabled = !mode.isEmpty && mode != "console"
        credentialPage.isHidden = !settingsVisible || mode.isEmpty || mode == "console"
        let minimum: CGFloat = credentialPage.isHidden ? 620 : 800
        window?.contentMinSize.height = minimum
        if let window, let content = window.contentView, content.frame.height < minimum {
            window.setContentSize(NSSize(width:content.frame.width,height:minimum))
        }
        let visible = [true,["qwen","baidu"].contains(selected),mode == "package",mode == "local"]
        for (row,show) in zip(credentialRows,visible) { row.isHidden = !show }
    }
    private func saveCredential() {
        let id = selected
        let value = MacDomesticCredential(mode:mode,key:key.stringValue.trimmingCharacters(in:.whitespacesAndNewlines),secret:secret.stringValue.trimmingCharacters(in:.whitespacesAndNewlines),package:package.stringValue.trimmingCharacters(in:.whitespacesAndNewlines),port:Int(port.stringValue) ?? 0)
        do { try value.validate(provider:id) } catch { notes[id] = error.localizedDescription; refreshStatus(); return }
        Task { [weak self] in
            guard let self else { return }
            do {
                try await service.saveCredential(value,provider:id)
                await MainActor.run { self.drafts[id] = nil; if self.selected == id { self.key.stringValue = ""; self.secret.stringValue = "" }; self.notes[id] = "凭据已保存在钥匙串，正在查询…"; self.refreshStatus() }
                await service.refresh()
            } catch { await MainActor.run { self.notes[id] = "凭据未保存，请检查钥匙串授权；原配置未替换。"; self.refreshStatus() } }
        }
    }
    private func refreshQuota() {
        refreshLocal()
        if selected == "deepseek", mode == "separate" {
            session()?.reload(); Task { await service.refresh() }; refreshStatus(); return
        }
        if mode == "console", let browser = session() {
            let id = selected
            Task { await service.useConsole(id); await MainActor.run { browser.reload() } }
        }
        else { Task { await service.refresh() } }
        refreshStatus()
    }
    func startBackgroundRefresh() {
        guard loadPages else { return }
        for item in MacModelAccountCatalog.all where item.supportsWebQuota {
            let mode = defaults.string(forKey:"domestic_source_"+item.id)
            if mode == "console" || (item.id == "deepseek" && mode == "separate") { session(item.id)?.loadIfNeeded() }
        }
    }
    private func tick() {
        for (id,session) in sessions { session.tick(interactive:window?.isVisible == true && selected == id) }
        let id = selected, generation = selectionGeneration
        Task { [weak self] in
            guard let self else { return }; let diagnostic = await service.diagnostic(provider:id)
            await MainActor.run { guard self.selectionGeneration == generation else { return }; self.apiNote = diagnostic; self.refreshStatus() }
        }
        refreshStatus()
    }
    private func refreshStatus() {
        let snapshot = store.snapshot()
        let separate = selected == "deepseek" && defaults.string(forKey:"domestic_source_deepseek") == "separate"
        webAccount.isHidden = !separate
        if separate {
            var text = "网页登录消费：等待官方数据"
            if let web = store.deepSeekWebSnapshot(), let used = web.usedCost {
                let date = DateFormatter(); date.dateFormat = "MM-dd HH:mm:ss"
                text = String(format:"网页登录消费：%@ %.2f",web.currency ?? "",used) + " · 网页最近成功：" + date.string(from:web.updatedAt) + (web.stale ? " · 旧缓存":"")
            }
            webAccount.stringValue = text + "\n两项来源独立，未核实同一账号；设备仅显示 API 总余额。"
        }
        local.stringValue = "Codex：\(snapshot.quotas?.codex?.plan ?? "等待本机授权")\nClaude：\(snapshot.quotas?.claude?.plan ?? "等待本机授权")"
        if let value = snapshot.domesticQuotas?[selected == "qwen" ? "alibaba":selected] {
            var values = [separate ? "API 来源":value.plan ?? "官方账户数据"]
            if let balance = value.balance { values.append(String(format:separate ? "总余额（含赠送） %@ %.2f":"余额 %@ %.2f",value.currency ?? "",balance)) }
            if let used = value.usedCost {
                var text = String(format:"已用 %@ %.2f",value.currency ?? "",used)
                if selected == "deepseek", let updated = store.deepSeekUsageDate() {
                    let date = DateFormatter(); date.dateFormat = "MM-dd HH:mm"
                    text += "（网页 " + date.string(from:updated) + "）"
                }
                values.append(text)
            } else if selected == "deepseek" && !separate { values.append("已用金额：等待官方控制台数据") }
            if let used = value.primaryPercent { values.append(String(format:"短窗口已用 %.1f%%",used)) }
            if let used = value.weeklyPercent { values.append(String(format:"周窗口已用 %.1f%%",used)) }
            if let used = value.planPercent { values.append(String(format:"订阅／资源包已用 %.1f%%",used)) }
            let date = DateFormatter(); date.dateFormat = "MM-dd HH:mm:ss"
            values.append("最近成功："+date.string(from:value.updatedAt)+(value.stale ? " · 旧缓存":""))
            account.stringValue = values.joined(separator:" · ")
        } else { account.stringValue = MacModelAccountCatalog.provider(selected)?.supportsQuota == true ? "尚无成功账户数据":"此厂商目前提供官方网页入口，尚未接入额度读取。" }
        status.stringValue = [notes[selected],mode == "console" ? nil:apiNote].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator:"\n")
        if status.stringValue.isEmpty { status.stringValue = mode == "console" ? "请在右侧完成官方登录；成功后将自动读取并后台刷新。":"选择厂商，配置接口或登录官方控制台。" }
    }
    nonisolated static func financeEndpoint(_ value: String) -> Bool { URL(string:value).map { MacDomesticBrowser.endpoint(provider:"zhipu",url:$0) } ?? false }
    nonisolated static func navigationAllowed(_ url: URL, isMainFrame: Bool) -> Bool { MacModelAccountCatalog.navigationAllowed(url,provider:"zhipu",mainFrame:isMainFrame) }
}
