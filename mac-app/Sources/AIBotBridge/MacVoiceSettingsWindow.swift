import AppKit
import AVFoundation
import CoreAudio
import Speech

struct MacVoiceSettings: Equatable {
    var provider: String, source: MacVoiceCaptureSource?, input: String
    static func read(_ defaults: UserDefaults) -> Self {
        .init(provider: defaults.string(forKey: MacSpeechProvider.key) == "doubao" ? "doubao" : "local",
              source: MacVoiceCaptureSource.saved(in: defaults), input: defaults.string(forKey: MacAudioInputs.preference) ?? "")
    }
    func save(_ defaults: UserDefaults) {
        defaults.set(provider, forKey: MacSpeechProvider.key)
        defaults.set(source?.rawValue, forKey: MacVoiceCaptureSource.preferenceKey)
        defaults.set(input, forKey: MacAudioInputs.preference)
    }
}

struct MacVoiceEnvironment {
    var inputs: [MacAudioInput], defaultInput: AudioDeviceID?
    var doubaoInstalled: Bool, doubaoEnabled: Bool, doubaoRunning: Bool, cable: Bool
    var speechPermission: Bool, microphonePermission: Bool, localRecognition: Bool
    static func capture() -> Self {
        let inputs = MacAudioInputs.devices()
        var address = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyDefaultInputDevice, mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
        var device = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        let hasDefault = AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size, &device) == noErr
        let installed = MacDoubaoSpeech.installed()
        let state = MacTab5ComputerPlatform.main {
            (installed && MacDoubaoSpeech.inputSourceEnabled(), NSWorkspace.shared.runningApplications.contains { $0.bundleIdentifier == "com.bytedance.inputmethod.doubaoime" })
        }
        let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "zh-CN"))
        return .init(inputs: inputs, defaultInput: hasDefault ? device : nil,
                     doubaoInstalled: installed, doubaoEnabled: state.0, doubaoRunning: state.1,
                     cable: (try? MacDoubaoAudio.device()) != nil,
                     speechPermission: SFSpeechRecognizer.authorizationStatus() == .authorized,
                     microphonePermission: AVCaptureDevice.authorizationStatus(for: .audio) == .authorized,
                     localRecognition: recognizer?.isAvailable == true && recognizer?.supportsOnDeviceRecognition == true)
    }
    var doubaoStatus: String {
        if !doubaoInstalled { return "未安装官方豆包输入法，暂不可选。" }
        if !doubaoEnabled { return "豆包已安装，尚未在系统键盘设置中启用。" }
        if !doubaoRunning { return "豆包已启用，输入服务尚未运行；录音时会准备，失败时保留预览。" }
        return "豆包输入服务正在运行；实际收音在开始录音时确认。"
    }
    func issue(for value: MacVoiceSettings) -> String? {
        guard ["local", "doubao"].contains(value.provider), value.input.utf8.count <= 512,
              !value.input.unicodeScalars.contains(where: CharacterSet.controlCharacters.contains) else { return "设置内容无效，请重新选择。" }
        if value.provider == "doubao" {
            guard doubaoInstalled, doubaoEnabled else { return doubaoStatus }
            if value.source == .tab5 { return cable ? nil : "TAB5 豆包收音需要可用的 VB-CABLE Mac，设置未保存。" }
        } else {
            guard speechPermission else { return "Mac 本地识别尚未授权，请先授权后保存。" }
            guard localRecognition else { return "中文本地识别暂不可用，请检查系统听写与语音资源后重试。" }
            if value.source == .tab5 { return nil }
            guard microphonePermission else { return "麦克风尚未授权，请先授权后保存。" }
        }
        let physical = inputs.filter { value.provider != "doubao" || (!$0.uid.hasPrefix("com.vbaudio.vbcable:") && $0.uid != "BlackHole2ch_UID") }
        if !value.input.isEmpty {
            return physical.contains(where: { $0.uid == value.input }) ? nil : "所选麦克风已断开或不适用，设置未保存；请重新选择。"
        }
        let available = physical.contains { $0.id == defaultInput } || (value.provider == "doubao" && physical.contains { $0.name.localizedCaseInsensitiveContains("DJI") || $0.name.contains("大疆") })
        return available ? nil : "没有可用的默认麦克风，设置未保存。"
    }
}

/// One transactional voice entry. Opening only enumerates metadata in the
/// background; it never records, selects an input method or changes preferences.
final class MacVoiceSettingsWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service, defaults: UserDefaults
    private let inspect: () throws -> MacVoiceEnvironment
    private let page = MacBridgeSettingsPage(), status = MacBridgeSettingsStyle.note("正在读取麦克风…")
    private let availability = MacBridgeSettingsStyle.note("")
    private let provider = MacSettingsControls.popup(["Mac 本地识别", "豆包输入法"])
    private let source = MacSettingsControls.popup(["跟随录音入口", "TAB5 内置麦克风", "Mac 麦克风"])
    private let input = NSPopUpButton()
    private var install: NSButton!, authorize: NSButton!, restore: NSButton!, saveButton: NSButton!
    private var entries: [MacAudioInput] = [], environment: MacVoiceEnvironment?
    private var baseline: MacVoiceSettings, selectedInput: String
    private var generation = UUID(), saving = false, closed = false
    private var draftGuard: MacSettingsDraft?, timer: Timer?
    init(service: MacTab5Service, defaults: UserDefaults = .standard,
         inspect: @escaping () throws -> MacVoiceEnvironment = MacVoiceEnvironment.capture, confirmDiscard: (() -> Bool)? = nil) {
        self.service = service; self.defaults = defaults; self.inspect = inspect
        baseline = .read(defaults); selectedInput = baseline.input
        super.init(window: MacBridgeSettingsStyle.window("语音设置", width: 640, height: 600, minimumHeight: 500))
        page.heading("语音输入", subtitle: "在 TAB5 发起录音后自动填入；发送仍需明确点击。设置保存后用于下一段录音。")
        provider.identifier = .init("voice-speech-provider"); provider.menu?.autoenablesItems = false
        provider.selectItem(at: baseline.provider == "doubao" ? 1 : 0)
        source.identifier = .init("voice-capture-source"); source.selectItem(at: baseline.source == nil ? 0 : baseline.source == .tab5 ? 1 : 2)
        input.identifier = .init("voice-mac-input"); input.addItem(withTitle: "正在读取麦克风…"); input.isEnabled = false
        for control in [provider, source, input] { control.target = self; control.action = #selector(changed(_:)) }
        page.field("识别方式", provider); page.field("收音设备", source); page.field("Mac 优先麦克风", input)
        page.add(MacBridgeSettingsStyle.note("自动选择时，豆包优先使用已连接的 DJI，否则使用系统默认麦克风。指定设备断开时会提示，不会改用其他设备。TAB5 豆包收音需要 VB-CABLE Mac。"))
        page.section("当前状态")
        availability.identifier = .init("voice-speech-availability"); page.add(availability)
        install = MacSettingsButton("安装豆包输入法…") { _ = NSWorkspace.shared.open(URL(string: "https://srf.doubao.com/pc")!) }
        install.identifier = .init("voice-install-doubao"); install.isHidden = true; page.add(install)
        authorize = MacSettingsButton("授权本地语音与麦克风…") { [weak self] in
            guard let self, !self.saving else { return }
            MacTab5Speech.authorize { [weak self] _ in self?.reload() }
        }
        authorize.isHidden = true; page.add(authorize)
        restore = MacSettingsButton("恢复豆包麦克风") { [weak self] in
            guard let self, !self.service.voice.busy, !self.saving else { return }
            MacDoubaoMicrophone().release { [weak self] result in
                self?.status.stringValue = (try? result.get()) != nil ? "豆包麦克风恢复检查完成。" : MacDoubaoMicrophone.restorationStatus
            }
        }
        restore.isHidden = MacDoubaoMicrophone.restorationStatus.isEmpty; page.add(restore)
        saveButton = MacSettingsButton("保存") { [weak self] in self?.save() }; saveButton.isEnabled = false
        window?.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [
            MacSettingsButton("刷新设备") { [weak self] in self?.reload() },
            MacSettingsButton("取消") { [weak self] in self?.window?.performClose(nil) }, saveButton])
        draftGuard = MacSettingsDraft(window) { [weak self] in self?.fingerprint ?? "" }; draftGuard?.confirmDiscard = confirmDiscard; window?.delegate = self
        reload()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            guard let self, self.window?.isVisible == true else { return }; self.updateAvailability()
        }
    }
    required init?(coder: NSCoder) { fatalError() }
    deinit { timer?.invalidate() }
    private var draft: MacVoiceSettings {
        .init(provider: provider.indexOfSelectedItem == 1 ? "doubao" : "local",
              source: source.indexOfSelectedItem == 0 ? nil : source.indexOfSelectedItem == 1 ? .tab5 : .mac, input: selectedInput)
    }
    private var fingerprint: String { let d = draft; return d.provider + "|" + (d.source?.rawValue ?? "") + "|" + d.input }
    func open() { MacSettingsControls.open(self) }
    func windowShouldClose(_ sender: NSWindow) -> Bool { draftGuard?.windowShouldClose(sender) ?? true }
    func windowWillClose(_ notification: Notification) { closed = true; generation = UUID(); timer?.invalidate() }
    @objc private func changed(_ sender: NSPopUpButton) {
        if sender === input {
            let index = input.indexOfSelectedItem
            if index == 0 { selectedInput = "" }
            else if entries.indices.contains(index - 1) { selectedInput = entries[index - 1].uid }
        }
        updateAvailability(); status.stringValue = "修改尚未保存。"
    }
    private func updateAvailability() {
        guard let environment else { return }
        let value = draft
        input.isEnabled = !saving && value.source != .tab5
        provider.item(at: 1)?.isEnabled = environment.doubaoInstalled
        provider.item(at: 1)?.title = environment.doubaoInstalled ? "豆包输入法" : "豆包输入法（未安装）"
        install.isHidden = environment.doubaoInstalled
        authorize.isHidden = value.provider != "local" || (environment.speechPermission && environment.microphonePermission)
        availability.stringValue = (value.provider == "doubao" ? environment.doubaoStatus : environment.localRecognition ? "中文本地识别可用。" : "中文本地识别待就绪。") + "\n" + (environment.issue(for: value) ?? "设备检查通过；实际收音将在录音开始时确认。") + "\n语音：" + service.voice.summary
    }
    private func reload() {
        guard !closed, !saving else { return }
        let owner = UUID(); generation = owner; environment = nil
        input.isEnabled = false; saveButton.isEnabled = false; status.stringValue = "正在读取麦克风…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self, inspect] in
            let result = Result { try inspect() }
            DispatchQueue.main.async {
                guard let self, !self.closed, self.generation == owner else { return }
                switch result {
                case .failure: self.status.stringValue = "无法读取音频设备，原设置保留，请刷新重试。"
                case .success(let env):
                    self.environment = env; self.entries = env.inputs
                    self.input.removeAllItems(); self.input.addItems(withTitles: ["自动选择麦克风"] + env.inputs.map(\.name))
                    if let index = env.inputs.firstIndex(where: { $0.uid == self.selectedInput }) { self.input.selectItem(at: index + 1) }
                    else if !self.selectedInput.isEmpty { self.input.addItem(withTitle: "之前选择的麦克风（未连接）"); self.input.selectItem(at: self.input.numberOfItems - 1) }
                    self.saveButton.isEnabled = true; self.status.stringValue = "已读取 \(env.inputs.count) 个输入设备。"; self.updateAvailability()
                }
            }
        }
    }
    private func save() {
        guard !saving, environment != nil, !service.voice.busy else { status.stringValue = "请等待设备加载或当前录音结束后保存。"; return }
        let value = draft, owner = UUID(); generation = owner; saving = true
        provider.isEnabled = false; source.isEnabled = false; input.isEnabled = false; saveButton.isEnabled = false
        status.stringValue = "正在重新检查音频设备…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self, inspect] in
            let result = Result { try inspect() }
            DispatchQueue.main.async {
                guard let self, !self.closed, self.generation == owner else { return }
                self.saving = false; self.provider.isEnabled = true; self.source.isEnabled = true; self.saveButton.isEnabled = true
                defer { self.updateAvailability() }
                guard !self.service.voice.busy, self.draft == value, MacVoiceSettings.read(self.defaults) == self.baseline else {
                    self.status.stringValue = "录音或设置已发生变化，本次未保存，请检查后重试。"; return
                }
                switch result {
                case .failure: self.status.stringValue = "设备检查失败，原设置保留。"
                case .success(let env):
                    self.environment = env
                    if let issue = env.issue(for: value) { self.status.stringValue = issue; return }
                    value.save(self.defaults); self.baseline = value; self.draftGuard?.markSaved(); self.close()
                }
            }
        }
    }
}
