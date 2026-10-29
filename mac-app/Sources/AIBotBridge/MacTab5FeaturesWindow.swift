import AppKit
import UniformTypeIdentifiers

final class MacTab5FeaturesWindow: NSWindowController, NSWindowDelegate, NSMenuDelegate {
    private let service: MacTab5Service
    private var voiceInputs:MacVoiceInputSettings?
    private let speechProvider = NSPopUpButton()
    private let speechAvailability = NSTextField(wrappingLabelWithString: "")
    private var installDoubaoButton: NSButton!
    private let doubaoInstalled: () -> Bool
    private var doubaoAvailable = false
    private var lastSpeechAvailabilityCheck: TimeInterval = 0
    private let captureSource = NSPopUpButton()
    private let voiceSourceDefaults: UserDefaults
    private var doubaoTestButton: NSButton!
    private var doubaoTest:MacTab5SpeechBackend?
    private let computer = NSButton(checkboxWithTitle: "允许 TAB5 调节音量、静音、锁屏和打开常用入口", target: nil, action: nil)
    private let music = NSButton(checkboxWithTitle: "采集 Apple Music／Spotify 播放信息", target: nil, action: nil)
    private let musicControl = NSButton(checkboxWithTitle: "允许 TAB5 控制音乐播放、暂停和切歌", target: nil, action: nil)
    private let codex = NSButton(checkboxWithTitle: "允许 TAB5 查看 Codex 会话与公开回复、同步加密草稿", target: nil, action: nil)
    private let send = NSButton(checkboxWithTitle: "允许在 TAB5 明确点击后发送或中断 Codex 任务", target: nil, action: nil)
    private let voice = NSButton(checkboxWithTitle: "允许 TAB5 使用 Mac 本地语音识别", target: nil, action: nil)
    private let desktop = NSButton(checkboxWithTitle: "允许快捷控制台打开并核对 Codex 桌面输入框", target: nil, action: nil)
    private let launchers = NSPopUpButton()
    private let result = NSTextField(wrappingLabelWithString: "")
    private let codexState = NSTextField(wrappingLabelWithString: "")
    private let voiceState = NSTextField(wrappingLabelWithString: "")
    private let musicState = NSTextField(wrappingLabelWithString: "")
    private let desktopState = NSTextField(wrappingLabelWithString: "")
    private var timer: Timer?
    enum Section: Int { case computer, music, codex, voice }
    private var sections: MacBridgeSettingsTabs!
    init(service: MacTab5Service, focusSection: Section? = nil, voiceSourceDefaults: UserDefaults = .standard, doubaoInstalled: @escaping () -> Bool = MacDoubaoSpeech.installed) {
        self.service = service; self.voiceSourceDefaults = voiceSourceDefaults; self.doubaoInstalled = doubaoInstalled
        let window = MacBridgeSettingsStyle.window("交互与权限", height: 640)
        super.init(window: window); window.delegate = self
        let computerPage = MacBridgeSettingsPage(), musicPage = MacBridgeSettingsPage()
        let codexPage = MacBridgeSettingsPage(), voicePage = MacBridgeSettingsPage()
        sections = MacBridgeSettingsTabs([("电脑控制", computerPage), ("音乐播放", musicPage), ("Codex 会话", codexPage), ("语音输入", voicePage)])
        result.stringValue = "开关更改立即生效，重启后保留。"
        let sectionNames = ["电脑控制", "音乐播放", "Codex 会话", "语音设置"]
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
        codexPage.add(MacBridgeSettingsStyle.note("操作仅针对已加载的精确任务，沿用该任务的模型和审批设置。当前桌面版本的追加接口不能绑定指定轮次，暂不开放追加。草稿在本机加密保存，关闭功能或退出桥接后结束连接。"))
        codexPage.add(NSButton(title: "选择 Codex CLI…", target: self, action: #selector(chooseCLI)))
        codexPage.section("桌面直达")
        desktop.state = UserDefaults.standard.bool(forKey: MacTab5Desktop.enabledKey) ? .on : .off; desktop.target = self; desktop.action = #selector(toggleDesktop)
        codexPage.add(desktop)
        codexPage.add(MacBridgeSettingsStyle.note("需要辅助功能权限。打开会话时核对精确地址或在完整目录中唯一的名称，缩略名称也检查重名。无法唯一区分时只确认已唤起 Codex；无法确认输入框时保留文字。提交草稿还需要上方操作权限及可用的桌面任务连接。"))
        codexPage.section("连接与诊断"); codexPage.add(codexState); codexPage.add(desktopState)

        voicePage.heading("语音输入", subtitle: "选择识别方式，将文字交给 TAB5 预览。")
        speechProvider.addItems(withTitles:["Mac 本地识别","豆包输入法"])
        speechProvider.identifier = .init("voice-speech-provider")
        speechProvider.menu?.autoenablesItems = false; speechProvider.menu?.delegate = self
        speechProvider.selectItem(at:voiceSourceDefaults.string(forKey:MacSpeechProvider.key) == "doubao" ? 1:0); speechProvider.target = self; speechProvider.action = #selector(changeSpeechProvider)
        voicePage.field("识别方式",speechProvider)
        speechAvailability.identifier = .init("voice-speech-availability")
        speechAvailability.textColor = .secondaryLabelColor; voicePage.add(speechAvailability)
        installDoubaoButton = MacSettingsButton("安装豆包输入法…") { _ = NSWorkspace.shared.open(URL(string:"https://srf.doubao.com/pc")!) }
        installDoubaoButton.identifier = .init("voice-install-doubao"); voicePage.add(installDoubaoButton)
        voice.title = "允许 TAB5 使用所选语音识别方式"
        voice.state = service.voice.isEnabled ? .on : .off; voice.target = self; voice.action = #selector(toggleVoice)
        voicePage.add(voice)
        voicePage.section("麦克风与权限")
        captureSource.identifier = .init("voice-capture-source")
        captureSource.addItems(withTitles: MacVoiceCaptureSource.allCases.map(\.title))
        captureSource.target = self; captureSource.action = #selector(changeCaptureSource)
        voicePage.field("收音设备", captureSource)
        voicePage.add(MacBridgeSettingsStyle.note("选择后用于下一段录音，当前录音保持原设备。TAB5 内置麦克风请在 TAB5 上发起录音；Mac 麦克风可在下方选择具体设备并测试。豆包使用 TAB5 收音时需要 VB-CABLE Mac。"))
        voiceInputs = MacVoiceInputSettings(page:voicePage)
        voicePage.section("豆包输入法")
        voicePage.add(MacBridgeSettingsStyle.note("豆包使用长按 Fn 模式，识别可能需要联网。未指定实体麦克风时优先 DJI，否则使用 Mac 默认麦克风；TAB5 收音通过 VB-CABLE Mac 转送。桥接核验实际收音，停止后等待完整文字并返回 TAB5 预览，不自动发送任务。临时选择豆包麦克风，结束后恢复原选择。"))
        doubaoTestButton = MacSettingsButton("测试 Mac 豆包") { [weak self] in self?.testDoubao() }
        doubaoTestButton.identifier = .init("voice-test-mac-doubao")
        voicePage.add(doubaoTestButton)
        refreshCaptureSource()
        voicePage.add(MacSettingsButton("恢复豆包麦克风") { [weak self] in
            guard let self else { return }
            guard !self.service.voice.busy,self.doubaoTest == nil else { self.result.stringValue = "请先停止当前语音，再恢复麦克风。"; return }
            self.result.stringValue = "正在检查并恢复豆包麦克风…"
            MacDoubaoMicrophone().release { [weak self] outcome in
                switch outcome {
                case .success: self?.result.stringValue = "豆包麦克风恢复检查完成；保留你随后手动选择的设备。"
                case .failure: self?.result.stringValue = MacDoubaoMicrophone.restorationStatus
                }
            }
        })
        voicePage.section("豆包音频组件")
        voicePage.add(MacBridgeSettingsStyle.note("仅 TAB5 麦克风转送需要 VB-CABLE Mac；Mac／DJI 实体麦克风可直接使用。音频仅送入指定虚拟设备，不改变系统默认输入／输出。组件安装后可能需要重新启动 Mac；实际效果请用 TAB5 录音验收。"))
        voicePage.add(MacBridgeSettingsStyle.row([MacSettingsButton("检查音频组件") { [weak self] in
            do { _ = try MacDoubaoAudio.device(); self?.result.stringValue = "VB-CABLE Mac 签名与音频端点检查通过，尚需实际录音验收。" }
            catch { self?.result.stringValue = error.localizedDescription }
        },MacSettingsButton("VB-CABLE 官网") { _ = NSWorkspace.shared.open(URL(string:"https://vb-audio.com/Cable/")!) }]))
        voicePage.add(MacBridgeSettingsStyle.note("Mac 本地识别支持 TAB5 收音或所选 Mac 麦克风；USB / DJI 麦克风连接后可在上方选择。仅在系统支持该语言的本地识别时启动，不自动上传音频。首次启用会请求语音识别与麦克风权限。"))
        voicePage.add(MacBridgeSettingsStyle.note("识别文字先预览，确认后才能发送或填入设备支持的事项／日期输入框。最长一分钟，取消后丢弃迟到结果。"))
        voicePage.section("识别与传输状态"); voicePage.add(voiceState)
        for label in [codexState, desktopState, musicState, voiceState] { label.font = .systemFont(ofSize: 12); label.textColor = .secondaryLabelColor }
        reload()
        resizeDocument()
        refreshCodexState()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refreshCodexState() }
    }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func windowWillClose(_ notification:Notification) { voiceInputs?.cancelTest(); doubaoTest?.cancel(); doubaoTest = nil }
    func windowDidResize(_ notification: Notification) { resizeDocument() }
    private func resizeDocument() { window?.contentView?.layoutSubtreeIfNeeded(); sections.pages.forEach { $0.fitDocument() } }
    private func refreshCodexState() {
        if window?.isVisible == true, ProcessInfo.processInfo.systemUptime-lastSpeechAvailabilityCheck >= 5 { refreshSpeechAvailability() }
        let requests = service.interactionDiagnostics.summary
        let text = "Codex：" + service.codex.summary + (requests.isEmpty ? "" : "\n最近会话请求（仅本机诊断，不含传输耗时）：\n" + requests)
        let details = service.voice.diagnostics
        let transfer = service.bleDiagnostics.summary
        let speech = "语音：" + service.voice.summary + (details.isEmpty ? "" : "\n" + details) +
            (MacDoubaoMicrophone.restorationStatus.isEmpty ? "" : "\n" + MacDoubaoMicrophone.restorationStatus) +
            (transfer.isEmpty ? "" : "\n蓝牙传输（本机计时；提交字节不表示设备已接收）：\n" + transfer)
        let playback = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? service.musicDiagnostics : "音乐：尚未启用"
        let direct = "直达：" + service.desktop.summary
        guard codexState.stringValue != text || voiceState.stringValue != speech || musicState.stringValue != playback || desktopState.stringValue != direct else { return }
        codexState.stringValue = text; voiceState.stringValue = speech; musicState.stringValue = playback; desktopState.stringValue = direct; resizeDocument()
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
    @objc private func toggleVoice() {
        if voice.state == .off { UserDefaults.standard.set(false, forKey: MacTab5Voice.enabledKey); service.voice.stop(); return }
        if MacSpeechProvider.doubao {
            let installed = MacDoubaoSpeech.installed(); voice.state = installed ? .on:.off
            UserDefaults.standard.set(installed,forKey:MacTab5Voice.enabledKey)
            result.stringValue = installed ? "豆包模式已启用；请先测试调用，并在豆包设置中确认麦克风和按住 Fn 快捷键。":"请先安装并启用豆包输入法。"
            return
        }
        MacTab5Speech.authorize { [weak self] allowed in guard let self else { return }; self.voice.state = allowed ? .on : .off; UserDefaults.standard.set(allowed, forKey: MacTab5Voice.enabledKey); self.result.stringValue = allowed ? "语音权限已授权；开始时仍会检查本地语言模型是否可用。" : "权限未完整授权，请到系统设置检查。" }
    }
    @objc private func changeSpeechProvider() {
        let previous = voiceSourceDefaults.string(forKey:MacSpeechProvider.key) == "doubao" ? 1:0
        guard !service.voice.busy, doubaoTest == nil else { speechProvider.selectItem(at:previous); result.stringValue = "请先结束当前收音。"; return }
        if speechProvider.indexOfSelectedItem == 1 && !doubaoInstalled() {
            speechProvider.selectItem(at:previous); refreshSpeechAvailability()
            result.stringValue = "未检测到官方豆包输入法，无法选择。安装后会自动检测。"; return
        }
        voiceSourceDefaults.set(speechProvider.indexOfSelectedItem == 1 ? "doubao":"local",forKey:MacSpeechProvider.key)
        result.stringValue = speechProvider.indexOfSelectedItem == 1 ? "已选择豆包。音频由豆包处理；请先测试调用。":"已选择 Mac 本地识别。"
    }
    func menuWillOpen(_ menu:NSMenu) { if menu === speechProvider.menu { refreshSpeechAvailability() } }
    private func refreshSpeechAvailability() {
        lastSpeechAvailabilityCheck = ProcessInfo.processInfo.systemUptime
        doubaoAvailable = doubaoInstalled()
        speechProvider.item(at:1)?.isEnabled = doubaoAvailable
        speechProvider.item(at:1)?.title = doubaoAvailable ? "豆包输入法":"豆包输入法（未安装）"
        speechAvailability.stringValue = "未安装官方豆包输入法，暂不可选。安装后自动恢复可选。"
        speechAvailability.isHidden = doubaoAvailable; installDoubaoButton.isHidden = doubaoAvailable
        refreshCaptureSource()
        resizeDocument()
    }
    private func refreshCaptureSource() {
        let source = MacVoiceCaptureSource.saved(in: voiceSourceDefaults) ?? .mac
        captureSource.selectItem(at: MacVoiceCaptureSource.allCases.firstIndex(of: source)!)
        voiceInputs?.setMacInputEnabled(source == .mac)
        doubaoTestButton.isEnabled = source == .mac && doubaoAvailable
    }
    @objc private func changeCaptureSource() {
        guard MacVoiceCaptureSource.allCases.indices.contains(captureSource.indexOfSelectedItem) else { return }
        let source = MacVoiceCaptureSource.allCases[captureSource.indexOfSelectedItem]
        voiceSourceDefaults.set(source.rawValue, forKey: MacVoiceCaptureSource.preferenceKey)
        refreshCaptureSource()
        result.stringValue = "已保存：" + source.title + "。下一段录音生效；请在 TAB5 上发起录音验证。"
    }
    private func testDoubao() {
        guard MacVoiceCaptureSource.saved(in: voiceSourceDefaults) != .tab5 else { result.stringValue = "已选择 TAB5 内置麦克风，请在 TAB5 上发起录音。"; return }
        guard !service.voice.busy, doubaoTest == nil else { result.stringValue = "请先结束当前收音。"; return }
        voiceInputs?.cancelTest()
        let backend = MacDoubaoSpeech(showsEditor:true).backend(); doubaoTest = backend
        do { try backend.start("pc") { [weak self] text, final, failure in DispatchQueue.main.async {
            guard let self else { return }
            self.result.stringValue = failure?.message ?? (final ? "豆包草稿已确认；本次测试未发送文字。":"豆包草稿已收到文字（\(text.utf8.count) 字节）。")
            if final || failure != nil { self.doubaoTest?.cancel(); self.doubaoTest = nil }
        } } } catch { doubaoTest = nil; result.stringValue = error.localizedDescription }
    }
    @objc private func chooseCLI() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url, FileManager.default.isExecutableFile(atPath: url.path) else { return }
        UserDefaults.standard.set(url.resolvingSymlinksInPath().path, forKey: MacTab5CodexRPC.pathKey); result.stringValue = "CLI 路径已保存，将连接本机 Codex。"
    }
    @objc private func openSettings() { _ = NSWorkspace.shared.open(URL(fileURLWithPath: "/System/Applications/System Settings.app")) }
    private func reload() {
        refreshSpeechAvailability()
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
