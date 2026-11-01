import AppKit
import AVFoundation
import CoreAudio
import Speech

struct MacVoiceSettings: Equatable {
    var provider: String, source: MacVoiceCaptureSource?, input: String
    var microphone: MacVoiceMicrophone? = nil
    static func read(_ defaults: UserDefaults) -> Self {
        let stored = defaults.string(forKey: MacVoiceMicrophone.preferenceKey)
        let legacy = [MacSpeechProvider.key, MacVoiceCaptureSource.preferenceKey, MacAudioInputs.preference, MacTab5Voice.enabledKey].contains { defaults.object(forKey: $0) != nil }
        let microphone = stored.map(MacVoiceMicrophone.init(stored:)) ?? (legacy ? nil : .automatic)
        return .init(provider: defaults.string(forKey: MacSpeechProvider.key) == "doubao" ? "doubao" : "local",
              source: !legacy && stored == nil ? .tab5 : MacVoiceCaptureSource.saved(in: defaults),
              input: defaults.string(forKey: MacAudioInputs.preference) ?? "", microphone: microphone)
    }
    func save(_ defaults: UserDefaults) {
        defaults.set(provider, forKey: MacSpeechProvider.key)
        defaults.set(source?.rawValue, forKey: MacVoiceCaptureSource.preferenceKey)
        defaults.set(input, forKey: MacAudioInputs.preference)
        defaults.set(microphone?.stored, forKey: MacVoiceMicrophone.preferenceKey)
    }
}

struct MacVoiceEnvironment {
    var inputs: [MacAudioInput], defaultInput: AudioDeviceID?
    var doubaoInstalled: Bool, doubaoEnabled: Bool, doubaoRunning: Bool, cable: Bool
    var speechPermission: Bool, microphonePermission: Bool, localRecognition: Bool
    static func capture() -> Self {
        let inputs = MacAudioInputs.devices()
        let installed = MacDoubaoSpeech.installed()
        let state = MacTab5ComputerPlatform.main {
            (installed && MacDoubaoSpeech.inputSourceEnabled(), NSWorkspace.shared.runningApplications.contains { $0.bundleIdentifier == "com.bytedance.inputmethod.doubaoime" })
        }
        let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "zh-CN"))
        return .init(inputs: inputs, defaultInput: MacAudioInputs.defaultInputID(),
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
        } else {
            guard speechPermission else { return "Mac 本地识别尚未授权，请先授权后保存。" }
            guard localRecognition else { return "中文本地识别暂不可用，请检查系统听写与语音资源后重试。" }
        }
        do {
            let route = try value.route(requested: value.source?.rawValue ?? "tab5", inputs: inputs, defaultInput: defaultInput)
            if route.source == .tab5 {
                return value.provider == "doubao" && !cable ? "TAB5 豆包收音通道未就绪，请安装并启用 VB-CABLE Mac 后重试。" : nil
            }
            return value.provider == "local" && !microphonePermission ? "麦克风尚未授权，请先授权后保存。" : nil
        } catch { return error.localizedDescription }
    }
    func expectedInput(for value: MacVoiceSettings) -> String {
        if value.microphone == nil, value.source == nil { return "沿用原设置，由 TAB5 的录音入口决定" }
        do {
            return try value.route(requested: value.source?.rawValue ?? "tab5", inputs: inputs, defaultInput: defaultInput).name
        } catch {
            return "所选设备暂不可用"
        }
    }
}

/// One transactional voice entry. Opening only enumerates metadata in the
/// background; it never records, selects an input method or changes preferences.
final class MacVoiceSettingsWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service, defaults: UserDefaults
    private let inspect: () throws -> MacVoiceEnvironment
    private let page = MacBridgeSettingsPage(), status = MacBridgeSettingsStyle.note("正在读取麦克风…")
    private let availability = MacBridgeSettingsStyle.note("")
    private let guidance = MacBridgeSettingsStyle.note("")
    private let provider = MacSettingsControls.popup(["Mac 本地识别", "豆包输入法"])
    private let input = NSPopUpButton()
    private var install: NSButton!, authorize: NSButton!, restore: NSButton!, saveButton: NSButton!
    private var choices: [(MacVoiceMicrophone, String)] = [], environment: MacVoiceEnvironment?
    private var baseline: MacVoiceSettings, selectedMicrophone: MacVoiceMicrophone
    private var generation = UUID(), saving = false, closed = false
    private var draftGuard: MacSettingsDraft?, timer: Timer?
    init(service: MacTab5Service, defaults: UserDefaults = .standard,
         inspect: @escaping () throws -> MacVoiceEnvironment = MacVoiceEnvironment.capture, confirmDiscard: (() -> Bool)? = nil) {
        self.service = service; self.defaults = defaults; self.inspect = inspect
        baseline = .read(defaults); selectedMicrophone = baseline.displayedMicrophone
        super.init(window: MacBridgeSettingsStyle.window("语音设置", width: 560, height: 480, minimumHeight: 420))
        window?.contentMinSize = NSSize(width: 520, height: 420)
        page.heading("语音输入", subtitle: "保存后用于下一段录音。")
        provider.identifier = .init("voice-speech-provider"); provider.menu?.autoenablesItems = false
        provider.selectItem(at: baseline.provider == "doubao" ? 1 : 0)
        input.identifier = .init("voice-recording-microphone"); input.addItem(withTitle: "正在读取麦克风…"); input.isEnabled = false
        input.cell?.lineBreakMode = .byTruncatingTail
        input.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        for control in [provider, input] { control.target = self; control.action = #selector(changed(_:)) }
        for (title, control) in [("识别方式", provider), ("录音麦克风", input)] {
            let label = MacBridgeSettingsStyle.label(title), spacer = NSView()
            label.widthAnchor.constraint(equalToConstant: 90).isActive = true
            control.widthAnchor.constraint(equalToConstant: 340).isActive = true
            spacer.setContentHuggingPriority(.defaultLow, for: .horizontal)
            page.add(MacBridgeSettingsStyle.row([label, control, spacer]))
        }
        guidance.isHidden = true; page.add(guidance)
        page.section("收音状态")
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
        var value = baseline.selecting(selectedMicrophone)
        value.provider = provider.indexOfSelectedItem == 1 ? "doubao" : "local"
        return value
    }
    private var fingerprint: String { let d = draft; return d.provider + "|" + (d.source?.rawValue ?? "") + "|" + d.input + "|" + (d.microphone?.stored ?? "legacy") }
    func open() { MacSettingsControls.open(self) }
    func windowShouldClose(_ sender: NSWindow) -> Bool { draftGuard?.windowShouldClose(sender) ?? true }
    func windowWillClose(_ notification: Notification) { closed = true; generation = UUID(); timer?.invalidate() }
    @objc private func changed(_ sender: NSPopUpButton) {
        if sender === input {
            let index = input.indexOfSelectedItem
            if choices.indices.contains(index) { selectedMicrophone = choices[index].0 }
        }
        updateAvailability(); status.stringValue = draft == baseline ? "" : "尚未保存"
    }
    private func updateAvailability() {
        guard let environment else { return }
        let value = draft
        input.isEnabled = !saving
        provider.item(at: 1)?.isEnabled = environment.doubaoInstalled
        provider.item(at: 1)?.title = environment.doubaoInstalled ? "豆包输入法" : "豆包输入法（未安装）"
        install.isHidden = environment.doubaoInstalled
        authorize.isHidden = value.provider != "local" || (environment.speechPermission && environment.microphonePermission)
        let expected = value == baseline ? "下次使用：" : "保存后使用："
        var lines = [expected + environment.expectedInput(for: value), service.voice.microphoneStatus]
        if let issue = environment.issue(for: value) { lines.append(issue) }
        else if value.provider == "doubao", !environment.doubaoRunning { lines.append("豆包将在录音时准备。") }
        availability.stringValue = lines.joined(separator: "\n")
        guidance.stringValue = selectedMicrophone == .legacy ? "保留原设置；重新选择并保存后更换。" : {
            if case .device = selectedMicrophone { return "设备断开时不自动切换。" }; return ""
        }()
        guidance.isHidden = guidance.stringValue.isEmpty
        input.toolTip = input.titleOfSelectedItem
    }
    private func reload() {
        guard !closed, !saving else { return }
        let owner = UUID(); generation = owner; environment = nil
        input.isEnabled = false; saveButton.isEnabled = false; status.stringValue = "读取麦克风…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self, inspect] in
            let result = Result { try inspect() }
            DispatchQueue.main.async {
                guard let self, !self.closed, self.generation == owner else { return }
                switch result {
                case .failure: self.status.stringValue = "无法读取音频设备，原设置保留，请刷新重试。"
                case .success(let env):
                    self.environment = env
                    self.choices = [(.automatic, "自动（优先 DJI，否则 TAB5）"), (.tab5, "TAB5 内置麦克风"), (.systemDefault, "Mac 系统默认麦克风")]
                    self.choices += env.inputs.filter(MacVoiceMicrophone.isPhysical).map { (.device($0.uid), $0.name) }
                    if !self.choices.contains(where: { $0.0 == self.selectedMicrophone }) {
                        let label = self.selectedMicrophone == .legacy ? (self.baseline.source == nil ? "原设置（随 TAB5 录音入口）" : "原设置（DJI／Mac 默认麦克风）") : "原选择（未连接或不可用）"
                        self.choices.append((self.selectedMicrophone, label))
                    }
                    self.input.removeAllItems(); self.input.addItems(withTitles: self.choices.map { $0.1 })
                    self.input.selectItem(at: self.choices.firstIndex(where: { $0.0 == self.selectedMicrophone }) ?? 0)
                    self.saveButton.isEnabled = true; self.status.stringValue = ""; self.updateAvailability()
                }
            }
        }
    }
    private func save() {
        guard !saving, environment != nil, !service.voice.busy else { status.stringValue = "请等待设备加载或当前录音结束后保存。"; return }
        let value = draft, owner = UUID(); generation = owner; saving = true
        provider.isEnabled = false; input.isEnabled = false; saveButton.isEnabled = false
        status.stringValue = "检查设备…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self, inspect] in
            let result = Result { try inspect() }
            DispatchQueue.main.async {
                guard let self, !self.closed, self.generation == owner else { return }
                self.saving = false; self.provider.isEnabled = true; self.saveButton.isEnabled = true
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
