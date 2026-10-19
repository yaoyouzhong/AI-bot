import AppKit
import UniformTypeIdentifiers

final class MacTab5FeaturesWindow: NSWindowController {
    private let service: MacTab5Service
    private let computer = NSButton(checkboxWithTitle: "允许 TAB5 调节音量、静音、锁屏和打开常用入口", target: nil, action: nil)
    private let music = NSButton(checkboxWithTitle: "允许读取并控制 Apple Music／Spotify", target: nil, action: nil)
    private let codex = NSButton(checkboxWithTitle: "允许 TAB5 查看 Codex 会话与公开回复、同步加密草稿", target: nil, action: nil)
    private let send = NSButton(checkboxWithTitle: "允许在 TAB5 明确点击后发送、追加或中断 Codex 任务", target: nil, action: nil)
    private let voice = NSButton(checkboxWithTitle: "允许 TAB5 使用 Mac 本地语音识别", target: nil, action: nil)
    private let launchers = NSPopUpButton()
    private let result = NSTextField(wrappingLabelWithString: "")
    init(service: MacTab5Service) {
        self.service = service
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 650, height: 560), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        super.init(window: window); window.title = "TAB5 · 交互与权限"; window.minSize = NSSize(width: 620, height: 560); window.center()
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14; stack.translatesAutoresizingMaskIntoConstraints = false
        let content = window.contentView!, scroll = NSScrollView(); scroll.translatesAutoresizingMaskIntoConstraints = false; scroll.hasVerticalScroller = true
        let document = NSView(); document.translatesAutoresizingMaskIntoConstraints = false; scroll.documentView = document; content.addSubview(scroll); document.addSubview(stack)
        NSLayoutConstraint.activate([scroll.leadingAnchor.constraint(equalTo: content.leadingAnchor), scroll.trailingAnchor.constraint(equalTo: content.trailingAnchor), scroll.topAnchor.constraint(equalTo: content.topAnchor), scroll.bottomAnchor.constraint(equalTo: content.bottomAnchor), document.widthAnchor.constraint(equalTo: scroll.contentView.widthAnchor), stack.leadingAnchor.constraint(equalTo: document.leadingAnchor, constant: 24), stack.trailingAnchor.constraint(equalTo: document.trailingAnchor, constant: -24), stack.topAnchor.constraint(equalTo: document.topAnchor, constant: 24), stack.bottomAnchor.constraint(equalTo: document.bottomAnchor, constant: -24)])
        func label(_ text: String, size: CGFloat = 13) { let label = NSTextField(wrappingLabelWithString: text); label.font = .systemFont(ofSize: size); stack.addArrangedSubview(label); label.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true }
        label("交互与权限", size: 23)
        computer.state = service.computer.isEnabled ? .on : .off; computer.target = self; computer.action = #selector(toggleComputer); stack.addArrangedSubview(computer)
        music.state = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? .on : .off; music.target = self; music.action = #selector(toggleMusic); stack.addArrangedSubview(music)
        label("音乐首次使用会请求自动化权限。控制只发送一次，并读取实际播放状态；切换曲目未确认时不会自动重复跳曲。锁屏需要辅助功能权限。Mac 锁定状态未知时保持未知，不提供一键解锁。")
        label("Codex 会话", size: 15)
        codex.state = service.codex.isEnabled ? .on : .off; codex.target = self; codex.action = #selector(toggleCodex); stack.addArrangedSubview(codex)
        send.state = UserDefaults.standard.bool(forKey: MacTab5Codex.sendKey) ? .on : .off; send.target = self; send.action = #selector(toggleSend); stack.addArrangedSubview(send)
        label("连接已运行的 Codex 本地 App Server，不修改登录、模型或审批策略。后台统计始终只读文件元数据；启用此项后才读取公开回复。草稿与送达记录在本机加密保存，发送结果未知时请在电脑核查，不会自动重发。")
        let cli = NSButton(title: "选择 Codex CLI…", target: self, action: #selector(chooseCLI)); stack.addArrangedSubview(cli)
        label("语音输入", size: 15)
        voice.state = service.voice.isEnabled ? .on : .off; voice.target = self; voice.action = #selector(toggleVoice); stack.addArrangedSubview(voice)
        label("支持 TAB5 收音或 Mac 默认麦克风（可在系统设置选择 USB 麦克风）。仅在系统支持该语言的本地识别时启动；不自动上传音频。识别文字先预览，确认后才能发送或填入事项／日期。首次启用会请求系统权限。")
        label("常用入口（最多四个）", size: 15)
        stack.addArrangedSubview(launchers)
        let add = NSButton(title: "添加本机程序或文件夹…", target: self, action: #selector(addLauncher))
        let remove = NSButton(title: "移除所选入口", target: self, action: #selector(removeLauncher))
        let row = NSStackView(views: [add, remove]); row.spacing = 12; stack.addArrangedSubview(row)
        label("TAB5 仅能打开这里选择的入口；设备请求不能指定文件路径或命令。")
        let settings = NSButton(title: "打开系统设置", target: self, action: #selector(openSettings)); stack.addArrangedSubview(settings)
        label("在“隐私与安全性 → 自动化／辅助功能”中管理权限。拒绝权限会显示未完成，授权后可重新发起新操作。")
        stack.addArrangedSubview(result); result.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true; result.textColor = .secondaryLabelColor
        reload()
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func open() { reload(); showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc private func toggleComputer() { UserDefaults.standard.set(computer.state == .on, forKey: MacTab5Computer.enabledKey) }
    @objc private func toggleMusic() { UserDefaults.standard.set(music.state == .on, forKey: MacMusicService.enabledKey) }
    @objc private func toggleCodex() { UserDefaults.standard.set(codex.state == .on, forKey: MacTab5Codex.enabledKey); if codex.state == .off { UserDefaults.standard.set(false, forKey: MacTab5Codex.sendKey); send.state = .off } }
    @objc private func toggleSend() { if codex.state != .on { send.state = .off }; UserDefaults.standard.set(send.state == .on, forKey: MacTab5Codex.sendKey) }
    @objc private func toggleVoice() {
        if voice.state == .off { UserDefaults.standard.set(false, forKey: MacTab5Voice.enabledKey); service.voice.stop(); return }
        MacTab5Speech.authorize { [weak self] allowed in guard let self else { return }; self.voice.state = allowed ? .on : .off; UserDefaults.standard.set(allowed, forKey: MacTab5Voice.enabledKey); self.result.stringValue = allowed ? "语音权限已授权；开始时仍会检查本地语言模型是否可用。" : "权限未完整授权，请到系统设置检查。" }
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
