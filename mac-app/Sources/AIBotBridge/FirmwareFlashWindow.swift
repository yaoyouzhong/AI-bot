import AppKit
import UniformTypeIdentifiers

final class MacFirmwareFlashWindow: NSWindowController, NSWindowDelegate {
    private let deviceLabel = NSTextField(labelWithString: "正在识别小屏…")
    private let deviceDetails = NSTextField(labelWithString: "未选择设备")
    private var selection: MacFlashDeviceSelection
    private var devices: [String] = []
    private var deviceTimer: Timer?
    private let file = NSTextField(labelWithString: "选择下载的固件包")
    private let status = NSTextField(wrappingLabelWithString: "")
    private let progress = NSProgressIndicator()
    private let log = NSTextView()
    private let details = NSStackView()
    private let page = MacBridgeSettingsPage()
    private var moreButton: NSButton!
    private var startButton: NSButton!
    private var backupButton: NSButton!
    private var refreshButton: NSButton!
    private var browseButton: NSButton!
    private var firmware: URL?
    private var stageText = ""
    private var lastProgressUpdate = ProcessInfo.processInfo.systemUptime
    private var lastProgressPercent = -1
    private let begin: () -> Bool
    private let suspend: () -> Void
    private let finish: (Bool) -> Void
    private(set) var busy = false

    init(bridgePort: String? = nil, begin: @escaping () -> Bool, suspend: @escaping () -> Void, finish: @escaping (Bool) -> Void) {
        selection = MacFlashDeviceSelection(bridgePort: bridgePort)
        self.begin = begin; self.suspend = suspend; self.finish = finish
        let window = MacBridgeSettingsStyle.window("ESP8266 固件升级", height: 560)
        super.init(window: window); window.delegate = self
        startButton = button("开始刷机", #selector(start)); backupButton = button("只备份设备", #selector(backup))
        window.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [startButton, backupButton])
        let stack = page.stack
        page.heading("ESP8266 固件升级", subtitle: "连接 USB，选择固件；写入前自动备份。")
        page.section("设备与固件")
        refreshButton = button("刷新", #selector(locateAgain))
        let deviceRow = NSStackView(views: [NSTextField(labelWithString: "设备"), deviceLabel, NSView(), refreshButton])
        deviceRow.spacing = 12; deviceRow.alignment = .centerY
        deviceRow.edgeInsets = NSEdgeInsets(top: 12, left: 12, bottom: 12, right: 12)
        stack.addArrangedSubview(deviceRow)
        deviceRow.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        browseButton = button("浏览…", #selector(browse))
        file.lineBreakMode = .byTruncatingMiddle; file.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        file.setContentHuggingPriority(.defaultLow, for: .horizontal)
        browseButton.setContentHuggingPriority(.required, for: .horizontal)
        let files = NSStackView(views: [NSTextField(labelWithString: "固件"), file, browseButton]); files.distribution = .fill
        files.spacing = 12; files.alignment = .centerY
        stack.addArrangedSubview(files); files.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        progress.style = .bar; progress.isIndeterminate = false; progress.maxValue = 100; progress.isHidden = true
        stack.addArrangedSubview(progress); progress.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        moreButton = button("更多 ▾", #selector(toggleDetails)); moreButton.isBordered = false; stack.addArrangedSubview(moreButton)
        details.orientation = .vertical; details.alignment = .leading; details.spacing = 16; details.isHidden = true
        details.addArrangedSubview(deviceDetails)
        details.addArrangedSubview(button("查看备份", #selector(openBackups)))
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.borderType = .bezelBorder
        log.isEditable = false; log.font = .monospacedSystemFont(ofSize: 11, weight: .regular); log.autoresizingMask = [.width]
        scroll.documentView = log; details.addArrangedSubview(scroll); stack.addArrangedSubview(details)
        details.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        scroll.widthAnchor.constraint(equalTo: details.widthAnchor).isActive = true
        scroll.heightAnchor.constraint(equalToConstant: 180).isActive = true
        refreshPorts(); window.center()
        deviceTimer = Timer.scheduledTimer(withTimeInterval: 0.75, repeats: true) { [weak self] _ in self?.refreshPorts() }
    }
    deinit { deviceTimer?.invalidate() }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    private func button(_ title: String, _ action: Selector) -> NSButton { NSButton(title: title, target: self, action: action) }
    func open() { showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if busy { status.stringValue = "操作正在进行，请保持连接，完成后再关闭。" }
        return !busy
    }
    @objc private func refreshPorts() {
        guard !busy else { return }
        do { devices = try FileManager.default.contentsOfDirectory(atPath: "/dev")
            .filter(SerialBridge.isCandidateDeviceName).sorted().map { "/dev/" + $0 } }
        catch { selection.scanFailed(); showSelection(); deviceDetails.stringValue = error.localizedDescription; return }
        selection.update(devices); showSelection()
    }
    @objc private func locateAgain() {
        refreshPorts()
    }
    private func showSelection() {
        deviceLabel.stringValue = selection.message; deviceDetails.stringValue = selection.selected ?? "未选择设备"
        deviceLabel.textColor = selection.selected == nil ? .secondaryLabelColor : .systemGreen
        updateButtons()
    }
    @objc private func toggleDetails() {
        details.isHidden.toggle(); moreButton.title = details.isHidden ? "更多 ▾" : "收起 ▴"
        window?.contentView?.layoutSubtreeIfNeeded(); page.fitDocument()
    }
    @objc private func updateButtons() {
        refreshButton.isEnabled = !busy; browseButton.isEnabled = !busy
        backupButton.isEnabled = !busy && selection.selected != nil
        startButton.isEnabled = backupButton.isEnabled && firmware != nil
    }
    @objc private func browse() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.allowedContentTypes = [.zip, UTType(filenameExtension: "bin") ?? .data]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        firmware = url; file.stringValue = url.lastPathComponent; updateButtons()
    }
    @objc private func openBackups() {
        do { try FileManager.default.createDirectory(at: MacFirmwareFlasher.backupDirectory, withIntermediateDirectories: true); NSWorkspace.shared.open(MacFirmwareFlasher.backupDirectory) }
        catch { status.stringValue = error.localizedDescription }
    }
    private func append(_ text: String) {
        let readable = text.replacingOccurrences(of: "\u{08}", with: "")
        if let regex = try? NSRegularExpression(pattern: "(\\d+)\\s*%"),
           let match = regex.matches(in: text, range: NSRange(location: 0, length: (text as NSString).length)).last,
           let percent = Double((text as NSString).substring(with: match.range(at: 1))) {
            let now = ProcessInfo.processInfo.systemUptime
            let value = Int(min(100, percent))
            let show = value != lastProgressPercent && (lastProgressPercent < 0 || value == 100 || now - lastProgressUpdate >= 1)
            if !show && readable.range(of: "^[0-9\\s()%]+$", options: .regularExpression) != nil { return }
            if show {
            lastProgressPercent = value; lastProgressUpdate = now
            progress.stopAnimation(nil); progress.isIndeterminate = false; progress.doubleValue = min(100, percent)
            status.stringValue = stageText.replacingOccurrences(of: "…", with: "") + " · \(Int(progress.doubleValue))%"
            }
        }
        if log.string.count > 120_000 { log.string = "" }
        log.textStorage?.append(NSAttributedString(string: readable + (readable.hasSuffix("\n") ? "" : "\n")))
        log.scrollToEndOfDocument(nil)
    }
    @objc private func start() { run(backupOnly: false) }
    @objc private func backup() { run(backupOnly: true) }
    private func run(backupOnly: Bool) {
        guard !busy, let port = selection.selected, begin() else { return }
        busy = true; updateButtons(); log.string = ""; progress.isHidden = false; progress.isIndeterminate = true; progress.startAnimation(nil)
        let selected = firmware
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            let work = MacFirmwareFlasher.root.appendingPathComponent("flash-tools/firmware-" + UUID().uuidString)
            defer { try? FileManager.default.removeItem(at: work) }
            var success = false
            var message = ""
            func stage(_ text: String) { DispatchQueue.main.async { self.stageText = text; self.lastProgressPercent = -1; self.status.stringValue = text; self.append(text); self.progress.isIndeterminate = true; self.progress.startAnimation(nil) } }
            do {
                let prepared: URL?
                if backupOnly { prepared = nil }
                else if let selected { prepared = try MacFirmwareFlasher.prepareFirmware(selected, at: work) }
                else { throw FlashError.failed("请先选择固件。") }
                let tool = try MacFirmwareFlasher.prepareTool(stage: stage)
                defer { tool.cleanup() }
                stage("正在连接设备…")
                suspend()
                let flasher = MacFirmwareFlasher(run: { args in try MacFirmwareFlasher.process(tool.executable.path, args, log: { text in DispatchQueue.main.async { self.append(text) } }) }, stage: stage)
                let backup = try flasher.execute(port: port, firmware: prepared)
                DispatchQueue.main.async { self.append("备份保存到：" + backup.path) }
                success = true
                message = backupOnly ? "备份完成。" : "刷机完成，小屏正在重新连接。"
            } catch { message = "操作未完成：" + error.localizedDescription }
            DispatchQueue.main.async { [success, message] in
                self.finish(success && !backupOnly)
                self.busy = false; self.updateButtons(); self.progress.stopAnimation(nil); self.progress.isIndeterminate = false
                self.progress.doubleValue = success ? 100 : 0; self.status.stringValue = message; self.append(message)
            }
        }
    }
}
