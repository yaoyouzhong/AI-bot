import AppKit
import ApplicationServices
import UniformTypeIdentifiers

final class MacTab5FeaturesWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service
    private let computer = NSButton(checkboxWithTitle: "允许 TAB5 调节音量、静音、锁屏和打开常用入口", target: nil, action: nil)
    private let music = NSButton(checkboxWithTitle: "采集 Apple Music／Spotify 播放信息", target: nil, action: nil)
    private let musicControl = NSButton(checkboxWithTitle: "允许 TAB5 控制音乐播放、暂停和切歌", target: nil, action: nil)
    private let codex = NSButton(checkboxWithTitle: "允许 TAB5 查看 Codex 会话与公开回复、同步加密草稿", target: nil, action: nil)
    private let send = NSButton(checkboxWithTitle: "允许在 TAB5 明确点击后发送或中断 Codex 任务", target: nil, action: nil)
    private let desktop = NSButton(checkboxWithTitle: "允许快捷控制台打开并核对 Codex 桌面输入框", target: nil, action: nil)
    private let launchers = NSPopUpButton()
    private let result = NSTextField(wrappingLabelWithString: "")
    private let codexState = NSTextField(wrappingLabelWithString: "")
    private let musicState = NSTextField(wrappingLabelWithString: "")
    private let desktopState = NSTextField(wrappingLabelWithString: "")
    private let accessibilityState = NSTextField(wrappingLabelWithString: "")
    private let accessibilityTrusted: () -> Bool
    private var timer: Timer?
    private var deliveryRecoveryBusy = false
    enum Section: Int { case computer, music, codex }
    private var sections: MacBridgeSettingsTabs!
    init(service: MacTab5Service, focusSection: Section? = nil, accessibilityTrusted: @escaping () -> Bool = { AXIsProcessTrusted() }) {
        self.service = service
        self.accessibilityTrusted = accessibilityTrusted
        let window = MacBridgeSettingsStyle.window("功能设置", height: 640)
        super.init(window: window); window.delegate = self
        let computerPage = MacBridgeSettingsPage(), musicPage = MacBridgeSettingsPage()
        let codexPage = MacBridgeSettingsPage()
        sections = MacBridgeSettingsTabs([("电脑控制", computerPage), ("音乐播放", musicPage), ("Codex 会话", codexPage)])
        result.stringValue = "普通功能默认启用；可在此停用，更改立即生效并保留。"
        let sectionNames = ["电脑控制", "音乐播放", "Codex 会话"]
        if let focusSection { window.title = "AI-bot · " + sectionNames[focusSection.rawValue]; sections.select(focusSection.rawValue) }
        window.contentView = MacBridgeSettingsShell(body: focusSection.map { sections.pages[$0.rawValue] } ?? sections, status: result,
            actions: [NSButton(title: "打开系统设置…", target: self, action: #selector(openSettings))])

        computerPage.heading("电脑控制", subtitle: "设置 TAB5 可操作的音量、静音、锁屏和常用入口。")
        computer.title = "启用 TAB5 电脑控制"
        computer.state = service.computer.isEnabled ? .on : .off; computer.target = self; computer.action = #selector(toggleComputer)
        computerPage.add(computer)
        computerPage.add(MacBridgeSettingsStyle.note("锁屏需辅助功能权限；锁定状态未知时不提供解锁。"))
        computerPage.section("常用入口")
        computerPage.field("已添加的程序或文件夹（最多四个）", launchers)
        computerPage.add(MacBridgeSettingsStyle.row([
            NSButton(title: "添加入口…", target: self, action: #selector(addLauncher)),
            NSButton(title: "移除所选", target: self, action: #selector(removeLauncher))]))
        computerPage.add(MacBridgeSettingsStyle.note("仅允许打开已选入口，设备不能指定路径或命令。"))
        computerPage.section("系统权限")
        computerPage.add(MacBridgeSettingsStyle.note("权限位置：系统设置 → 隐私与安全性 → 自动化／辅助功能。"))

        musicPage.heading("音乐播放", subtitle: "默认启用播放信息与控制；系统自动化权限按 macOS 提示确认。")
        musicControl.title = "启用 TAB5 音乐播放、暂停和切歌"
        music.state = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? .on : .off; music.target = self; music.action = #selector(toggleMusic)
        musicControl.state = MacFeaturePreferences.musicControlEnabled() ? .on : .off; musicControl.target = self; musicControl.action = #selector(toggleMusicControl)
        musicPage.add(music); musicPage.add(musicControl)
        musicPage.add(MacBridgeSettingsStyle.note("支持 Apple Music、Spotify；操作后核对状态，不自动重复切歌。"))
        musicPage.add(MacBridgeSettingsStyle.note("缺封面时用歌名、歌手和专辑查询 Apple 公开目录，不涉及账户。"))
        musicPage.section("播放状态"); musicPage.add(musicState)

        codexPage.heading("Codex 会话", subtitle: "读取本机任务与公开回复；支持手动发送和中断。")
        codex.title = "启用 TAB5 Codex 会话与公开回复"
        send.title = "启用 TAB5 手动发送与中断 Codex 任务"
        desktop.title = "启用 Codex 桌面直达与输入框核对"
        codex.state = service.codex.isEnabled ? .on : .off; codex.target = self; codex.action = #selector(toggleCodex)
        send.state = UserDefaults.standard.bool(forKey: MacTab5Codex.sendKey) ? .on : .off; send.target = self; send.action = #selector(toggleSend)
        codexPage.add(codex); codexPage.add(send)
        codexPage.add(MacBridgeSettingsStyle.note("仅操作已确认任务，沿用原模型与审批设置。追加遵循 Codex 排队／引导设置；有电脑草稿、附件或题卡时暂停，不支持带照片追加。草稿本机加密保存，停用或退出后断开。"))
        codexPage.add(MacBridgeSettingsStyle.row([
            NSButton(title: "选择 Codex CLI…", target: self, action: #selector(chooseCLI)),
            NSButton(title: "处理待确认发送…", target: self, action: #selector(recoverDelivery))]))
        codexPage.section("Claude 会话")
        codexPage.add(MacBridgeSettingsStyle.note("在 TAB5 可查看 Claude 原会话和回复。发送时需要核对电脑上的原会话与输入框；macOS 不允许控制时仍可查看。工具审批在原客户端完成。"))
        codexPage.section("桌面直达")
        desktop.state = UserDefaults.standard.bool(forKey: MacTab5Desktop.enabledKey) ? .on : .off; desktop.target = self; desktop.action = #selector(toggleDesktop)
        codexPage.add(desktop)
        codexPage.add(MacBridgeSettingsStyle.note("直达需要辅助功能权限（部分 macOS 显示为“设备控制与数据访问”），以核对 Codex 会话和输入框。无法确认时不会操作，草稿保留。"))
        codexPage.add(accessibilityState)
        codexPage.add(MacBridgeSettingsStyle.row([NSButton(title: "打开系统辅助功能设置…", target: self, action: #selector(openAccessibilitySettings))]))
        codexPage.section("连接与诊断"); codexPage.add(codexState); codexPage.add(desktopState)

        for label in [codexState, desktopState, musicState, accessibilityState] { label.font = .systemFont(ofSize: 12); label.textColor = .secondaryLabelColor }
        reload()
        resizeDocument()
        refreshCodexState()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refreshCodexState() }
    }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func windowDidResize(_ notification: Notification) { resizeDocument() }
    private func resizeDocument() { window?.contentView?.layoutSubtreeIfNeeded(); sections.pages.forEach { $0.fitDocument() } }
    private func refreshCodexState() {
        let requests = service.interactionDiagnostics.summary
        let text = "Codex：" + service.codex.summary + (requests.isEmpty ? "" : "\n最近会话请求（仅本机诊断，不含传输耗时）：\n" + requests)
        let playback = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? service.musicDiagnostics : "音乐：尚未启用"
        let direct = "直达：" + service.desktop.summary
        let access = accessibilityTrusted() ? "系统辅助功能：可用" : "系统辅助功能：未启用。仍可查看任务和回复。"
        guard codexState.stringValue != text || musicState.stringValue != playback || desktopState.stringValue != direct || accessibilityState.stringValue != access else { return }
        codexState.stringValue = text; musicState.stringValue = playback; desktopState.stringValue = direct; accessibilityState.stringValue = access; resizeDocument()
    }
    func open(section: Section? = nil) { if let section { sections.select(section.rawValue) }; reload(); showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc private func toggleComputer() { UserDefaults.standard.set(computer.state == .on, forKey: MacTab5Computer.enabledKey) }
    @objc private func toggleMusic() {
        UserDefaults.standard.set(music.state == .on, forKey: MacMusicService.enabledKey)
        musicControl.state = MacFeaturePreferences.musicControlEnabled() ? .on : .off
    }
    @objc private func toggleMusicControl() {
        if music.state != .on { musicControl.state = .off; result.stringValue = "请先开启音乐采集。" }
        UserDefaults.standard.set(musicControl.state == .on, forKey: MacMusicService.controlKey)
    }
    @objc private func toggleCodex() { UserDefaults.standard.set(codex.state == .on, forKey: MacTab5Codex.enabledKey); if codex.state == .off { UserDefaults.standard.set(false, forKey: MacTab5Codex.sendKey); send.state = .off } }
    @objc private func toggleSend() {
        if codex.state != .on || !service.codex.canMutate {
            send.state = .off
            result.stringValue = "当前连接只读，发送、追加和中断不可用。"
        }
        UserDefaults.standard.set(send.state == .on, forKey: MacTab5Codex.sendKey)
    }
    @objc private func toggleDesktop() {
        if codex.state != .on { desktop.state = .off; result.stringValue = "请先启用 Codex 会话读取。" }
        let enabled = desktop.state == .on
        UserDefaults.standard.set(enabled, forKey: MacTab5Desktop.enabledKey)
        if enabled && !accessibilityTrusted() {
            result.stringValue = "桌面直达已开启；macOS 目前不允许控制输入框。"
        }
        refreshCodexState()
    }
    @objc private func recoverDelivery() {
        guard !deliveryRecoveryBusy else { return }
        deliveryRecoveryBusy = true; result.stringValue = "正在读取待确认发送…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self else { return }
            let pending = Result { try self.service.codex.pendingDeliveries() }
            DispatchQueue.main.async {
                switch pending {
                case .failure:
                    self.deliveryRecoveryBusy = false
                    self.result.stringValue = "待确认记录暂不可读，未修改任何内容。"
                case .success(let items):
                    guard !items.isEmpty else {
                        self.deliveryRecoveryBusy = false
                        self.result.stringValue = "没有可人工确认的文字追加记录；记录或草稿变化时保持原状态。"
                        return
                    }
                    let dialog = MacTab5DeliveryRecoveryAlert(items: items)
                    guard dialog.alert.runModal() == .alertSecondButtonReturn,
                          let selected = dialog.selected else {
                        self.deliveryRecoveryBusy = false
                        self.result.stringValue = "已取消，待确认记录和草稿保持不变。"; return
                    }
                    DispatchQueue.global(qos: .userInitiated).async {
                        let result = Result { try self.service.codex.confirmDelivered(selected) }
                        DispatchQueue.main.async {
                            self.deliveryRecoveryBusy = false
                            switch result {
                            case .success: self.result.stringValue = "已登记人工确认。请在 TAB5 点一次“查询送达”解除锁定；不会重新发送。"
                            case .failure(let error): self.result.stringValue = error.localizedDescription
                            }
                        }
                    }
                }
            }
        }
    }
    @objc private func chooseCLI() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url, FileManager.default.isExecutableFile(atPath: url.path) else { return }
        UserDefaults.standard.set(url.resolvingSymlinksInPath().path, forKey: MacTab5CodexRPC.pathKey); result.stringValue = "CLI 路径已保存，将连接本机 Codex。"
    }
    @objc private func openSettings() { _ = NSWorkspace.shared.open(URL(fileURLWithPath: "/System/Applications/System Settings.app")) }
    @objc private func openAccessibilitySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            _ = NSWorkspace.shared.open(url)
        }
    }
    private func reload() {
        do { launchers.removeAllItems(); launchers.addItems(withTitles: try service.computer.launchers.load().map(\.name)) }
        catch { result.stringValue = error.localizedDescription }
    }
    @objc private func addLauncher() {
        do {
            var values = try service.computer.launchers.load()
            guard values.count < 4 else { result.stringValue = "最多设置四个常用入口。"; return }
            let panel = NSOpenPanel(); panel.canChooseDirectories = true; panel.canChooseFiles = true; panel.allowsMultipleSelection = false
            panel.allowedContentTypes = [.application, .folder]
            guard panel.runModal() == .OK, let url = panel.url else { return }
            let name = String(FileManager.default.displayName(atPath: url.path).prefix(16))
            values.append(.init(id: UUID().uuidString.lowercased(), name: name, path: url.resolvingSymlinksInPath().path))
            try service.computer.launchers.save(values); reload(); result.stringValue = "入口已保存。"
        } catch { result.stringValue = error.localizedDescription }
    }
    @objc private func removeLauncher() {
        do {
            var values = try service.computer.launchers.load(); let index = launchers.indexOfSelectedItem
            guard values.indices.contains(index) else { return }; values.remove(at: index)
            try service.computer.launchers.save(values); reload(); result.stringValue = "入口已移除，原程序或文件夹保持不变。"
        } catch { result.stringValue = error.localizedDescription }
    }
}
