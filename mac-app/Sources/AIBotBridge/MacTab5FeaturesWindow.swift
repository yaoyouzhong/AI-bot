import AppKit
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
    private var timer: Timer?
    private var deliveryRecoveryBusy = false
    enum Section: Int { case computer, music, codex }
    private var sections: MacBridgeSettingsTabs!
    init(service: MacTab5Service, focusSection: Section? = nil) {
        self.service = service
        let window = MacBridgeSettingsStyle.window("交互与权限", height: 640)
        super.init(window: window); window.delegate = self
        let computerPage = MacBridgeSettingsPage(), musicPage = MacBridgeSettingsPage()
        let codexPage = MacBridgeSettingsPage()
        sections = MacBridgeSettingsTabs([("电脑控制", computerPage), ("音乐播放", musicPage), ("Codex 会话", codexPage)])
        result.stringValue = "开关更改立即生效，重启后保留。"
        let sectionNames = ["电脑控制", "音乐播放", "Codex 会话"]
        if let focusSection { window.title = "AI-bot · " + sectionNames[focusSection.rawValue]; sections.select(focusSection.rawValue) }
        window.contentView = MacBridgeSettingsShell(body: focusSection.map { sections.pages[$0.rawValue] } ?? sections, status: result,
            actions: [NSButton(title: "打开系统设置…", target: self, action: #selector(openSettings))])

        computerPage.heading("电脑控制", subtitle: "设置 TAB5 可操作的音量、静音、锁屏和常用入口。")
        computer.title = "允许 TAB5 控制电脑"
        computer.state = service.computer.isEnabled ? .on : .off; computer.target = self; computer.action = #selector(toggleComputer)
        computerPage.add(computer)
        computerPage.add(MacBridgeSettingsStyle.note("锁屏需要辅助功能权限。Mac 锁定状态未知时保持未知，不提供一键解锁。"))
        computerPage.section("常用入口")
        computerPage.field("已添加的程序或文件夹（最多四个）", launchers)
        computerPage.add(MacBridgeSettingsStyle.row([
            NSButton(title: "添加入口…", target: self, action: #selector(addLauncher)),
            NSButton(title: "移除所选", target: self, action: #selector(removeLauncher))]))
        computerPage.add(MacBridgeSettingsStyle.note("TAB5 仅能打开这里选择的入口；设备请求不能指定文件路径或命令。"))
        computerPage.section("系统权限")
        computerPage.add(MacBridgeSettingsStyle.note("在系统设置“隐私与安全性 → 自动化／辅助功能”中管理权限。拒绝权限会显示未完成，授权后可重新发起新操作。"))

        musicPage.heading("音乐播放", subtitle: "采集播放信息和控制播放分别授权；首次使用会请求自动化权限。")
        music.state = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? .on : .off; music.target = self; music.action = #selector(toggleMusic)
        musicControl.state = MacFeaturePreferences.musicControlEnabled() ? .on : .off; musicControl.target = self; musicControl.action = #selector(toggleMusicControl)
        musicPage.add(music); musicPage.add(musicControl)
        musicPage.add(MacBridgeSettingsStyle.note("支持 Apple Music 和 Spotify。控制只发送一次，并读取实际播放状态；切换曲目未确认时不会自动重复跳曲。"))
        musicPage.add(MacBridgeSettingsStyle.note("封面无法确认时会向 Apple 公开目录查询当前歌曲、歌手和专辑；不使用账户信息。TAB5 使用高清封面，小屏保持原尺寸。"))
        musicPage.section("播放状态"); musicPage.add(musicState)

        codexPage.heading("Codex 会话", subtitle: "查看本机近期任务和公开回复，同步加密草稿。发送与中断需要另行启用。")
        codex.title = "允许 TAB5 查看 Codex 会话与公开回复"
        send.title = "允许在 TAB5 手动发送或中断 Codex 任务"
        desktop.title = "启用 Codex 桌面直达与输入框核对"
        codex.state = service.codex.isEnabled ? .on : .off; codex.target = self; codex.action = #selector(toggleCodex)
        send.state = UserDefaults.standard.bool(forKey: MacTab5Codex.sendKey) ? .on : .off; send.target = self; send.action = #selector(toggleSend)
        codexPage.add(codex); codexPage.add(send)
        codexPage.add(MacBridgeSettingsStyle.note("操作仅针对已加载的精确任务，沿用该任务的模型和审批设置。文字追加遵循 Codex 当前排队／引导设置；电脑已有草稿、附件或问题卡时先保留内容，带照片的追加暂不支持。草稿在本机加密保存，关闭功能或退出桥接后结束连接。"))
        codexPage.add(MacBridgeSettingsStyle.row([
            NSButton(title: "选择 Codex CLI…", target: self, action: #selector(chooseCLI)),
            NSButton(title: "处理待确认发送…", target: self, action: #selector(recoverDelivery))]))
        codexPage.section("桌面直达")
        desktop.state = UserDefaults.standard.bool(forKey: MacTab5Desktop.enabledKey) ? .on : .off; desktop.target = self; desktop.action = #selector(toggleDesktop)
        codexPage.add(desktop)
        codexPage.add(MacBridgeSettingsStyle.note("需要辅助功能权限。打开会话时核对精确地址或在完整目录中唯一的名称，缩略名称也检查重名。无法唯一区分时只确认已唤起 Codex；无法确认输入框时保留文字。提交草稿还需要上方操作权限及可用的桌面任务连接。"))
        codexPage.section("连接与诊断"); codexPage.add(codexState); codexPage.add(desktopState)

        for label in [codexState, desktopState, musicState] { label.font = .systemFont(ofSize: 12); label.textColor = .secondaryLabelColor }
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
        guard codexState.stringValue != text || musicState.stringValue != playback || desktopState.stringValue != direct else { return }
        codexState.stringValue = text; musicState.stringValue = playback; desktopState.stringValue = direct; resizeDocument()
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
    @objc private func toggleDesktop() { if codex.state != .on { desktop.state = .off }; UserDefaults.standard.set(desktop.state == .on, forKey: MacTab5Desktop.enabledKey) }
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
