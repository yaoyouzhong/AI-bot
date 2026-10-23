import AppKit

final class MacTab5Window: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service
    private let usb: MacTab5USB
    private let canBegin: () -> Bool
    private let connection = NSTextField(wrappingLabelWithString: "")
    private let result = NSTextField(wrappingLabelWithString: "")
    private let ports = NSPopUpButton()
    private let ssid = NSTextField()
    private let password = NSSecureTextField()
    private var actions: [NSButton] = []
    private var timer: Timer?
    private(set) var busy = false

    init(service: MacTab5Service, usb: MacTab5USB, canBegin: @escaping () -> Bool = { true }) {
        self.service = service; self.usb = usb; self.canBegin = canBegin
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 610, height: 700),
                              styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        super.init(window: window)
        window.title = "AI-bot · TAB5"; window.delegate = self; window.minSize = NSSize(width: 580, height: 540); window.center()
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        let content = window.contentView!
        let scroll = NSScrollView(frame: content.bounds); scroll.autoresizingMask = [.width, .height]
        scroll.hasVerticalScroller = true; scroll.drawsBackground = false
        let document = MacTab5DocumentView(frame: NSRect(x: 0, y: 0, width: content.bounds.width, height: 760))
        document.autoresizingMask = [.width]; scroll.documentView = document
        content.addSubview(scroll); document.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: document.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(equalTo: document.trailingAnchor, constant: -24),
            stack.topAnchor.constraint(equalTo: document.topAnchor, constant: 24),
            stack.bottomAnchor.constraint(lessThanOrEqualTo: document.bottomAnchor, constant: -24)
        ])
        let title = NSTextField(labelWithString: "TAB5"); title.font = .systemFont(ofSize: 23, weight: .semibold)
        stack.addArrangedSubview(title)
        stack.addArrangedSubview(connection)
        connection.font = .systemFont(ofSize: 13)
        connection.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        addSeparator(to: stack)
        stack.addArrangedSubview(heading("USB 配对"))
        let refresh = action("刷新串口", #selector(refreshPorts))
        let portRow = NSStackView(views: [ports, refresh]); portRow.spacing = 10
        stack.addArrangedSubview(portRow)
        portRow.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        ports.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        stack.addArrangedSubview(action("配对所选 TAB5", #selector(pair)))
        stack.addArrangedSubview(note("连接已安装 AI-bot 固件的 TAB5 数据线。配对会让设备连接到这台 Mac；从其他电脑迁移后，原电脑需要重新配对。"))
        addSeparator(to: stack)
        stack.addArrangedSubview(heading("Wi-Fi 配置"))
        ssid.placeholderString = "Wi-Fi 名称"; password.placeholderString = "Wi-Fi 密码（开放网络留空）"
        let wifi = NSGridView(views: [[NSTextField(labelWithString: "网络名称"), ssid],
                                      [NSTextField(labelWithString: "密码"), password]])
        wifi.columnSpacing = 12; wifi.rowSpacing = 8
        wifi.column(at: 0).xPlacement = .trailing
        stack.addArrangedSubview(wifi); wifi.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        stack.addArrangedSubview(action("通过 USB 保存 Wi-Fi", #selector(saveWiFi)))
        stack.addArrangedSubview(note("设备确认保存后，可拔掉 USB 数据线，通过同一局域网显示 Mac 数据。密码只通过 USB 发往设备。"))
        addSeparator(to: stack)
        stack.addArrangedSubview(heading("显示设置"))
        stack.addArrangedSubview(action("读取并修改显示设置…", #selector(editDisplay)))
        stack.addArrangedSubview(note("亮度、声音、页面和轮播设置以 TAB5 实际值为准；保存需要设备确认。"))
        result.textColor = .secondaryLabelColor
        stack.addArrangedSubview(result); result.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        refreshPorts(); refreshStatus()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refreshStatus() }
    }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func open() { refreshStatus(); showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if busy { result.stringValue = "正在等待设备确认，请完成后再关闭。" }
        return !busy
    }
    private func heading(_ text: String) -> NSTextField {
        let label = NSTextField(labelWithString: text); label.font = .systemFont(ofSize: 15, weight: .semibold); return label
    }
    private func note(_ text: String) -> NSTextField {
        let label = NSTextField(wrappingLabelWithString: text); label.font = .systemFont(ofSize: 12); label.textColor = .secondaryLabelColor
        label.preferredMaxLayoutWidth = 562; return label
    }
    private func action(_ title: String, _ selector: Selector) -> NSButton {
        let button = NSButton(title: title, target: self, action: selector); actions.append(button); return button
    }
    private func addSeparator(to stack: NSStackView) {
        let line = NSBox(); line.boxType = .separator; stack.addArrangedSubview(line)
        line.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
    }
    private func setBusy(_ value: Bool, message: String) {
        busy = value; actions.forEach { $0.isEnabled = !value }; ports.isEnabled = !value
        ssid.isEnabled = !value; password.isEnabled = !value
        result.stringValue = message
    }
    private func refreshStatus() { connection.stringValue = service.connectionSummary }
    private func allowOperation() -> Bool {
        guard !busy else { return false }
        guard canBegin() else { result.stringValue = "请先完成小屏刷机操作，再操作 TAB5。"; return false }
        return true
    }
    @objc private func refreshPorts() {
        let selected = ports.titleOfSelectedItem
        ports.removeAllItems(); ports.addItems(withTitles: MacTab5USB.ports())
        if let selected, ports.itemTitles.contains(selected) { ports.selectItem(withTitle: selected) }
        else if let saved = service.currentPairing?.usbPath, ports.itemTitles.contains(saved) { ports.selectItem(withTitle: saved) }
    }
    @objc private func pair() {
        guard allowOperation() else { return }
        guard let path = ports.titleOfSelectedItem else { result.stringValue = "请先连接 USB 数据线，再刷新串口。"; return }
        setBusy(true, message: "正在识别并配对 TAB5…")
        usb.pair(path: path) { [weak self] outcome in
            DispatchQueue.main.async { self?.finish(outcome, success: "USB 配对已确认，等待日常数据。") }
        }
    }
    @objc private func saveWiFi() {
        guard allowOperation() else { return }
        let name = ssid.stringValue, secret = password.stringValue
        password.stringValue = ""
        setBusy(true, message: "正在通过 USB 保存 Wi-Fi…")
        usb.configureWiFi(ssid: name, password: secret) { [weak self] outcome in
            DispatchQueue.main.async { self?.finish(outcome, success: "设备已确认保存 Wi-Fi；连接状态将在上方更新。") }
        }
    }
    private func finish(_ outcome: Result<Void, Error>, success: String) {
        switch outcome {
        case .success: setBusy(false, message: success)
        case .failure(let error): setBusy(false, message: error.localizedDescription)
        }
        refreshStatus()
    }
    @objc private func editDisplay() {
        guard allowOperation() else { return }
        setBusy(true, message: "正在读取 TAB5 显示设置…")
        service.displaySettings(nil) { [weak self] outcome in
            DispatchQueue.main.async {
                guard let self else { return }
                switch outcome {
                case .failure(let error): self.setBusy(false, message: error.localizedDescription)
                case .success(let current):
                    guard let desired = MacTab5SettingsForm.edit(current) else { self.setBusy(false, message: "已读取设备设置，未保存修改。"); return }
                    self.setBusy(true, message: "正在等待设备确认保存…")
                    self.service.displaySettings(desired) { [weak self] saved in
                        DispatchQueue.main.async {
                            guard let self else { return }
                            switch saved {
                            case .success: self.setBusy(false, message: "TAB5 已确认保存显示设置。")
                            case .failure(let error): self.setBusy(false, message: error.localizedDescription)
                            }
                        }
                    }
                }
            }
        }
    }
}

private final class MacTab5SettingsForm: NSObject {
    private let brightness = NSTextField()
    private let volume = NSTextField()
    private let muted = NSButton(checkboxWithTitle: "静音", target: nil, action: nil)
    private let cycle = NSButton(checkboxWithTitle: "启用轮播", target: nil, action: nil)
    private let alerts = NSButton(checkboxWithTitle: "显示提醒", target: nil, action: nil)
    private let selected = NSPopUpButton()
    private let interval = NSPopUpButton()
    private let saver = NSPopUpButton()
    private let pageRows = NSStackView()
    private var pageOrder: [Int]
    private var pageChecks: [NSButton] = []
    private let current: MacTab5DisplaySettings

    private init(_ current: MacTab5DisplaySettings) {
        self.current = current; pageOrder = current.order
        super.init()
        brightness.stringValue = String(current.brightness); volume.stringValue = String(current.volume)
        muted.state = current.muted ? .on : .off; cycle.state = current.cycle ? .on : .off; alerts.state = current.alerts ? .on : .off
        selected.addItems(withTitles: ["自动"] + MacTab5DisplaySettings.pages); selected.selectItem(at: current.selected + 1)
        interval.addItems(withTitles: ["10 秒", "15 秒", "30 秒", "60 秒"]); interval.selectItem(at: [10, 15, 30, 60].firstIndex(of: current.interval) ?? 1)
        saver.addItems(withTitles: ["关闭", "1 分钟", "5 分钟", "10 分钟", "30 分钟", "60 分钟"])
        saver.selectItem(at: [0, 1, 5, 10, 30, 60].firstIndex(of: current.saverMinutes) ?? 0)
        cycle.target = self; cycle.action = #selector(cycleChanged)
        pageRows.orientation = .vertical; pageRows.alignment = .leading; pageRows.spacing = 3
        for index in 0..<8 {
            let check = NSButton(checkboxWithTitle: MacTab5DisplaySettings.pages[index], target: nil, action: nil)
            check.state = current.enabled[index] == 1 ? .on : .off
            check.widthAnchor.constraint(equalToConstant: 280).isActive = true; pageChecks.append(check)
        }
        cycleChanged(); rebuildPages()
    }
    static func edit(_ current: MacTab5DisplaySettings) -> MacTab5DisplaySettings? {
        let form = MacTab5SettingsForm(current)
        let alert = NSAlert(); alert.messageText = "TAB5 显示设置"
        alert.informativeText = "以下为设备实际设置。勾选参与轮播的页面，使用上下按钮调整顺序。"
        alert.addButton(withTitle: "保存到 TAB5"); alert.addButton(withTitle: "取消")
        let grid = NSGridView(views: [
            [NSTextField(labelWithString: "亮度（10–100）"), form.brightness],
            [NSTextField(labelWithString: "音量（0–100）"), form.volume],
            [NSTextField(labelWithString: "固定页面"), form.selected],
            [NSTextField(labelWithString: "轮播间隔"), form.interval],
            [NSTextField(labelWithString: "屏保等待"), form.saver]
        ])
        grid.rowSpacing = 8; grid.columnSpacing = 14; grid.column(at: 0).xPlacement = .trailing
        let flags = NSStackView(views: [form.muted, form.cycle, form.alerts]); flags.spacing = 18
        let body = NSStackView(views: [grid, flags, form.pageRows]); body.orientation = .vertical; body.alignment = .leading; body.spacing = 14
        body.frame = NSRect(x: 0, y: 0, width: 430, height: 460)
        grid.widthAnchor.constraint(equalToConstant: 430).isActive = true
        alert.accessoryView = body
        while alert.runModal() == .alertFirstButtonReturn {
            do { return try form.value() }
            catch { alert.informativeText = error.localizedDescription }
        }
        return nil
    }
    @objc private func cycleChanged() {
        selected.isEnabled = cycle.state != .on
        if cycle.state == .on { selected.selectItem(at: 0) }
    }
    private func rebuildPages() {
        pageRows.arrangedSubviews.forEach { pageRows.removeArrangedSubview($0); $0.removeFromSuperview() }
        for (position, id) in pageOrder.enumerated() {
            let check = pageChecks[id]; check.removeFromSuperview()
            let up = NSButton(title: "↑", target: self, action: #selector(movePage)); up.tag = id * 2; up.isEnabled = position > 0
            let down = NSButton(title: "↓", target: self, action: #selector(movePage)); down.tag = id * 2 + 1; down.isEnabled = position < 7
            up.setAccessibilityLabel("上移" + check.title); down.setAccessibilityLabel("下移" + check.title)
            let row = NSStackView(views: [check, up, down]); row.spacing = 8
            pageRows.addArrangedSubview(row)
        }
    }
    @objc private func movePage(_ sender: NSButton) {
        guard let position = pageOrder.firstIndex(of: sender.tag / 2) else { return }
        let target = position + (sender.tag % 2 == 0 ? -1 : 1)
        guard (0..<8).contains(target) else { return }
        pageOrder.swapAt(position, target); rebuildPages()
    }
    private func value() throws -> MacTab5DisplaySettings {
        guard let light = Int(brightness.stringValue), let sound = Int(volume.stringValue) else { throw MacTab5Error.invalidSettings }
        var value = current
        value.brightness = light; value.volume = sound; value.muted = muted.state == .on
        value.cycle = cycle.state == .on; value.alerts = alerts.state == .on
        value.selected = selected.indexOfSelectedItem - 1
        value.interval = [10, 15, 30, 60][interval.indexOfSelectedItem]
        value.saverMinutes = [0, 1, 5, 10, 30, 60][saver.indexOfSelectedItem]
        value.enabled = pageChecks.map { $0.state == .on ? 1 : 0 }; value.order = pageOrder
        try value.validate(); return value
    }
}

private final class MacTab5DocumentView: NSView {
    override var isFlipped: Bool { true }
}
