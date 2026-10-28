import AppKit
import UniformTypeIdentifiers

/// The same five modules as the Windows connection dialog, sharing native spacing.
final class MacTab5ToolsWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service, usb: MacTab5USB
    private let canBegin: () -> Bool, install: () -> Void, voice: () -> Void
    private let changeBluetooth: (Bool) -> Void
    private let status = MacBridgeSettingsStyle.note(""), result = MacBridgeSettingsStyle.note("")
    private let connection = MacBridgeSettingsStyle.note(""), notes = MacBridgeSettingsStyle.label("")
    private var actions: [NSButton] = [], timer: Timer?
    private var pages: [MacBridgeSettingsPage] = []
    private var tabs: MacBridgeSettingsTabs!
    private let saved = MacSettingsTable(["Wi-Fi 名称","备注","状态"],minimumHeight:120), ssid = NSTextField(), label = NSTextField(), password = NSSecureTextField(), ports = NSPopUpButton()
    private var networks: [MacSavedWiFi] = []
    private let benchmarkMode = MacSettingsControls.popup(["图库写卡","通信传输"])
    private let benchmarkResult = MacBridgeSettingsStyle.label("选择测试类型，然后开始测速。"), benchmarkHint = MacBridgeSettingsStyle.note("")
    private var stopButton: NSButton!, benchmarkTask: Task<Void,Never>?, benchmarkValue = "尚未测速", expanded = false
    private(set) var busy = false
    init(service:MacTab5Service,usb:MacTab5USB,canBegin:@escaping ()->Bool = { true },install:@escaping ()->Void = {},voice:@escaping ()->Void = {},changeBluetooth:@escaping (Bool)->Void = { _ in }) {
        self.service = service; self.usb = usb; self.canBegin = canBegin; self.install = install; self.voice = voice; self.changeBluetooth = changeBluetooth
        let window = MacBridgeSettingsStyle.window("TAB5 连接与升级",width:640,height:650,minimumHeight:520)
        super.init(window:window); window.delegate = self
        pages = (0..<5).map { _ in MacBridgeSettingsPage() }
        tabs = MacBridgeSettingsTabs(Array(zip(["Wi-Fi","USB 配对","固件升级","语音","传输测速"],pages)))
        let body = NSView(); connection.translatesAutoresizingMaskIntoConstraints = false; tabs.translatesAutoresizingMaskIntoConstraints = false; body.addSubview(connection); body.addSubview(tabs)
        NSLayoutConstraint.activate([connection.leadingAnchor.constraint(equalTo:body.leadingAnchor,constant:24),connection.trailingAnchor.constraint(equalTo:body.trailingAnchor,constant:-24),connection.topAnchor.constraint(equalTo:body.topAnchor,constant:20),tabs.leadingAnchor.constraint(equalTo:body.leadingAnchor),tabs.trailingAnchor.constraint(equalTo:body.trailingAnchor),tabs.topAnchor.constraint(equalTo:connection.bottomAnchor,constant:12),tabs.bottomAnchor.constraint(equalTo:body.bottomAnchor)])
        window.contentView = MacBridgeSettingsShell(body:body,status:result)
        let wifi = pages[0], pairing = pages[1], firmware = pages[2], audio = pages[3], benchmark = pages[4]
        wifi.section("已保存的 Wi-Fi"); wifi.add(saved); saved.heightAnchor.constraint(equalToConstant:120).isActive = true
        wifi.add(row([("刷新",#selector(refreshNetworks)),("新增",#selector(addNetwork)),("删除所选",#selector(forgetNetwork))]))
        let fields = NSGridView(views:[[NSTextField(labelWithString:"Wi-Fi 名称"),ssid],[NSTextField(labelWithString:"备注"),label],[NSTextField(labelWithString:"新密码"),password]])
        fields.columnSpacing = 12; fields.rowSpacing = 12; ssid.placeholderString = "2.4 GHz 网络"; label.placeholderString = "可选"; password.placeholderString = "修改密码时填写"
        wifi.add(fields); wifi.add(row([("保存并连接",#selector(saveNetwork)),("保存备注",#selector(saveLabel))]))
        saved.selected = { [weak self] i in guard let self, self.networks.indices.contains(i) else { return }; let network = self.networks[i]; self.ssid.stringValue = network.ssid; self.ssid.isEditable = false; self.label.stringValue = network.label; self.password.stringValue = "" }
        pairing.section("USB 配对"); pairing.add(ports); pairing.add(row([("刷新设备",#selector(refreshPorts)),("配对",#selector(pair))]))
        pairing.add(MacBridgeSettingsStyle.note("已有 AI-bot：选择 USB 端口后配对。")); pairing.add(button("出厂设备首次安装…",#selector(firstInstall)))
        pairing.section("蓝牙连接")
        let bluetooth = NSButton(checkboxWithTitle:"启用 TAB5 蓝牙连接",target:self,action:#selector(toggleBluetooth(_:))); bluetooth.state = UserDefaults.standard.bool(forKey:MacTab5BLE.enabledKey) ? .on:.off; pairing.add(bluetooth)
        firmware.section("固件升级"); firmware.add(status)
        firmware.add(row([("开始升级",#selector(startUpgrade)),("选择固件…",#selector(selectFirmware)),("停止提供",#selector(clearFirmware))]))
        firmware.add(button("核验启动",#selector(verifyBoot))); firmware.section("更新说明"); firmware.add(notes)
        audio.section("语音输入"); audio.add(MacBridgeSettingsStyle.note("识别后在 TAB5 检查，再手动发送。")); audio.add(button("语音设置…",#selector(openVoice)))
        benchmark.add(benchmarkMode); benchmarkMode.target = self; benchmarkMode.action = #selector(updateBenchmarkHint)
        stopButton = NSButton(title:"停止",target:self,action:#selector(stopBenchmark)); stopButton.isEnabled = false
        benchmark.add(MacBridgeSettingsStyle.row([button("开始测速",#selector(startBenchmark)),stopButton])); benchmark.add(benchmarkHint)
        benchmark.add(MacSettingsButton("查看 / 收起详细数据") { [weak self] in guard let self else { return }; self.expanded.toggle(); self.renderBenchmark() }); benchmark.add(benchmarkResult)
        refreshPorts(); updateBenchmarkHint(); refresh()
        timer = Timer.scheduledTimer(withTimeInterval:1,repeats:true) { [weak self] _ in self?.refresh() }
    }
    required init?(coder:NSCoder) { fatalError() }
    deinit { timer?.invalidate(); benchmarkTask?.cancel() }
    func open(upgrade:Bool = false) { if upgrade { tabs.select(2) }; refresh(); MacSettingsControls.open(self); if service.usbActive, !busy { refreshNetworks() } }
    func windowShouldClose(_ sender:NSWindow) -> Bool { if busy || service.firmwareControl.busy { result.stringValue = "请先完成升级或停止测速，再关闭窗口。"; return false }; return true }
    private func button(_ title:String,_ action:Selector) -> NSButton { let b = NSButton(title:title,target:self,action:action); actions.append(b); return b }
    private func row(_ items:[(String,Selector)]) -> NSView { MacBridgeSettingsStyle.row(items.map { button($0.0,$0.1) }) }
    private func refresh() {
        let value = service.connectionStatus
        connection.stringValue = "\(value.device) · 固件 \(value.firmware)\nUSB：\(value.usb) · Wi-Fi：\(value.wifi) · 蓝牙：\(value.bluetooth)"
        connection.toolTip = service.connectionSummary; status.stringValue = service.firmwareControl.summary
        if let image = service.firmwareControl.selected { notes.stringValue = "版本：\(image.version) · \(image.bytes.count) 字节\nSHA-256：\(image.sha256)\n\n\(image.notes)" } else { notes.stringValue = "选择固件后显示版本、校验和与更新说明。" }
        window?.contentView?.layoutSubtreeIfNeeded(); pages.forEach { $0.fitDocument() }
    }
    private func begin(_ message:String) -> Bool {
        guard !busy, canBegin(), !service.configurationBusy else { result.stringValue = "请先完成当前设备操作。"; return false }
        busy = true; actions.forEach { $0.isEnabled = false }; saved.table.isEnabled = false; result.stringValue = message; return true
    }
    private func finish(_ outcome:Result<Void,Error>,_ message:String) {
        busy = false; actions.forEach { $0.isEnabled = true }; saved.table.isEnabled = true
        switch outcome { case .success: result.stringValue = message; case .failure(let error): result.stringValue = error.localizedDescription }; refresh()
    }
    @objc private func refreshPorts() { let old = ports.titleOfSelectedItem; ports.removeAllItems(); ports.addItems(withTitles:MacTab5USB.ports()); if let old, ports.itemTitles.contains(old) { ports.selectItem(withTitle:old) } }
    @objc private func refreshNetworks() {
        guard begin("正在读取设备保存的网络…") else { return }; let selected = ssid.stringValue
        usb.readWiFi { [weak self] outcome in DispatchQueue.main.async { guard let self else { return }
            switch outcome {
            case .success(let rows): self.networks = rows; self.saved.rows = rows.map { [$0.ssid,$0.label,$0.connected ? "已连接":$0.selected ? "连接中":""] }; if let i = rows.firstIndex(where:{$0.ssid == selected}) { self.saved.select(i) }; self.finish(.success(()),rows.isEmpty ? "暂无保存的网络。":"已读取设备网络。")
            case .failure(let error): self.finish(.failure(error),"")
            }
        } }
    }
    @objc private func addNetwork() { guard !busy else { return }; saved.select(-1); ssid.isEditable = true; ssid.stringValue = ""; label.stringValue = ""; password.stringValue = ""; window?.makeFirstResponder(ssid) }
    @objc private func saveNetwork() {
        if password.stringValue.isEmpty { let alert = NSAlert(); alert.messageText = "将保存为空密码网络，是否继续？"; alert.informativeText = "仅改备注请点击“保存备注”。"; alert.addButton(withTitle:"保存并连接"); alert.addButton(withTitle:"取消"); guard alert.runModal() == .alertFirstButtonReturn else { return } }
        guard begin("正在保存并连接…") else { return }; let secret = password.stringValue; password.stringValue = ""
        usb.configureWiFi(ssid:ssid.stringValue,password:secret,label:label.stringValue) { [weak self] outcome in DispatchQueue.main.async { self?.finish(outcome,"已保存，正在连接。"); if case .success = outcome { self?.refreshNetworks() } } }
    }
    @objc private func saveLabel() { guard begin("正在保存备注…") else { return }; usb.labelWiFi(ssid:ssid.stringValue,label:label.stringValue) { [weak self] outcome in DispatchQueue.main.async { self?.finish(outcome,"备注已保存。"); if case .success = outcome { self?.refreshNetworks() } } } }
    @objc private func forgetNetwork() {
        guard networks.indices.contains(saved.selectedRow), !busy else { return }; let network = networks[saved.selectedRow]
        let alert = NSAlert(); alert.messageText = "删除“\(network.ssid)”？"; alert.informativeText = network.connected ? "此网络正在连接，删除后会切换或断开 Wi-Fi。":"从设备删除此网络。"; alert.addButton(withTitle:"删除"); alert.addButton(withTitle:"取消"); guard alert.runModal() == .alertFirstButtonReturn, begin("正在删除网络…") else { return }
        usb.forgetWiFi(ssid:network.ssid) { [weak self] outcome in DispatchQueue.main.async { self?.finish(outcome,"网络已删除。"); if case .success = outcome { self?.addNetwork(); self?.refreshNetworks() } } }
    }
    @objc private func pair() { guard let path = ports.titleOfSelectedItem else { result.stringValue = "请连接 USB 后刷新。"; return }; guard begin("正在 USB 配对…") else { return }; usb.pair(path:path) { [weak self] outcome in DispatchQueue.main.async { self?.finish(outcome,"配对已完成。"); if case .success = outcome { self?.refreshNetworks() } } } }
    @objc private func firstInstall() { guard !busy, canBegin() else { return }; install() }
    @objc private func openVoice() { voice() }
    @objc private func toggleBluetooth(_ sender:NSButton) { guard !busy, canBegin() else { sender.state = sender.state == .on ? .off:.on; return }; UserDefaults.standard.set(sender.state == .on,forKey:MacTab5BLE.enabledKey); changeBluetooth(sender.state == .on) }
    @objc private func updateBenchmarkHint() { benchmarkHint.stringValue = benchmarkMode.indexOfSelectedItem == 0 ? "8 个独立样本，包含传输、校验和写卡；原图库保留。请先安装名画和书法图库，并连接 USB 读取结果。":"比较各可用通道的上传和下载速度，不写入 TF 卡。USB 用于控制和读取结果。" }
    private func renderBenchmark() { benchmarkResult.stringValue = MacTab5Benchmark.format(benchmarkValue,details:expanded) }
    @objc private func stopBenchmark() { stopButton.isEnabled = false; benchmarkTask?.cancel() }
    private func benchmarkCommand(_ action:String) async throws -> [String:Any] { try await withCheckedThrowingContinuation { continuation in usb.benchmark(action) { continuation.resume(with:$0) } } }
    private func benchmarkStatus(_ action:String) async throws -> [String:Any] {
        var last: Error = MacTab5Error.timeout
        for attempt in 0..<4 {
            try Task.checkCancellation()
            do { return try await benchmarkCommand(action) } catch { last = error; if attempt < 3 { try await Task.sleep(nanoseconds:300_000_000) } }
        }
        throw last
    }
    @objc private func startBenchmark() {
        guard service.usbActive else { result.stringValue = "请连接已配对 TAB5 的 USB。"; return }
        guard begin("正在测速…") else { return }; let gallery = benchmarkMode.indexOfSelectedItem == 0
        benchmarkMode.isEnabled = false; stopButton.isEnabled = true
        benchmarkTask = Task { @MainActor [weak self] in guard let self else { return }; var started = false, cancelled = false
            do {
                try self.service.beginBenchmark(gallery:gallery)
                if gallery { let reply = try await self.benchmarkCommand("gallery_status"); guard reply["gallerySpeed"] as? Bool == true else { throw MacTab5LocalError.invalid("图库写卡测速需要 TAB5 0.2.182-ui 或更新固件。") } }
                try Task.checkCancellation(); started = true
                var reply = try await self.benchmarkCommand(gallery ? "gallery_start":"start")
                let deadline = Date().addingTimeInterval(gallery ? 240:960)
                while true {
                    guard let value = reply["result"] as? String, value.utf8.count <= 8192 else { throw MacTab5Error.invalidPacket }
                    self.benchmarkValue = value; self.renderBenchmark()
                    if !value.hasPrefix("running;") { break }
                    guard Date() < deadline else { throw MacTab5Error.timeout }
                    try await Task.sleep(nanoseconds:1_000_000_000)
                    reply = try await self.benchmarkStatus(gallery ? "gallery_status":"status")
                }
                self.result.stringValue = "测速结束。"
            } catch { cancelled = error is CancellationError; self.result.stringValue = cancelled ? "正在停止测速…":error.localizedDescription }
            if started {
                do {
                    // Cleanup must keep running after the user's task is cancelled.
                    let final = try await Task { @MainActor in
                        try await MacTab5BenchmarkStop.run(gallery:gallery,request:{ try await self.benchmarkCommand($0) })
                    }.value
                    self.benchmarkValue = final; self.renderBenchmark()
                    if cancelled { self.result.stringValue = final.hasPrefix("cancelled") ? "测速已停止。":"测速已结束。" }
                } catch { self.result.stringValue = cancelled ? "停止请求未确认，设备将在测速时限内结束。":self.result.stringValue + " 停止请求未确认，设备将在测速时限内结束。" }
            }
            self.service.endBenchmark(); self.busy = false; self.actions.forEach { $0.isEnabled = true }; self.saved.table.isEnabled = true; self.benchmarkMode.isEnabled = true; self.stopButton.isEnabled = false; self.benchmarkTask = nil
        }
    }
    private func pick(_ types: [UTType]) -> URL? {
        guard !busy, canBegin() else { result.stringValue = "请先完成当前设备操作。"; return nil }
        let panel = NSOpenPanel(); panel.allowedContentTypes = types; panel.allowsMultipleSelection = false
        return panel.runModal() == .OK ? panel.url : nil
    }
    private func work(_ operation: @escaping () throws -> String) {
        guard !busy, canBegin() else { result.stringValue = "请先完成当前设备操作。"; return }
        busy = true; actions.forEach { $0.isEnabled = false }; result.stringValue = "正在处理…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            let result = Result { try operation() }
            DispatchQueue.main.async {
                guard let self else { return }; self.busy = false; self.actions.forEach { $0.isEnabled = true }
                switch result { case .success(let text): self.result.stringValue = text; case .failure(let error): self.result.stringValue = error.localizedDescription }
                self.refresh()
            }
        }
    }
    @objc private func selectFirmware() {
        guard let url = pick([UTType(filenameExtension: "bin") ?? .data, .zip]) else { return }
        work { [service] in let image = try MacTab5FirmwareImage.load(url); try service.firmwareControl.select(image); return "固件与说明已校验。设备上可查看升级；尚未开始安装。" }
    }
    @objc private func clearFirmware() { work { [service] in try service.firmwareControl.select(nil); return "已停止提供固件。" } }
    @objc private func startUpgrade() {
        guard !busy, canBegin(), let pair = service.currentPairing, let image = service.firmwareControl.selected else { result.stringValue = "请先配对、连接 TAB5 并选择固件。"; return }
        let alert = NSAlert(); alert.messageText = "开始升级 TAB5？"
        alert.informativeText = "设备：\(pair.deviceId)\n版本：\(image.version)\nSHA-256：\(image.sha256)\n\n\(image.notes)\n\n请保持供电，设备会先保存草稿。"
        alert.addButton(withTitle: "开始升级"); alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        work { [service] in try service.requestUpgrade(); return "请求已准备，等待设备确认；结果会持续显示在上方。" }
    }
    @objc private func verifyBoot() {
        guard !busy, canBegin(), let pair = service.currentPairing else { return }
        if service.usbActive {
            busy = true; actions.forEach { $0.isEnabled = false }
            usb.verifyBoot { [weak self] outcome in DispatchQueue.main.async {
                guard let self else { return }; self.busy = false; self.actions.forEach { $0.isEnabled = true }
                if case .failure(let error) = outcome { self.result.stringValue = error.localizedDescription }; self.refresh()
            } }
        } else if let transport = service.bootTransport {
            work { [service] in try service.firmwareControl.beginBootCheck(device: pair.deviceId, transport: transport); return "正在等待三个独立实时回报；不能把连接成功视为启动核验通过。" }
        } else { result.stringValue = "请先连接 TAB5；较旧固件需使用 USB 核验。" }
    }
}
