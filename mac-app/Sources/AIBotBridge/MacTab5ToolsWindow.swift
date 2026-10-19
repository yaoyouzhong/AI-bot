import AppKit
import UniformTypeIdentifiers

final class MacTab5ToolsWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service
    private let usb: MacTab5USB
    private let canBegin: () -> Bool
    private let status = NSTextField(wrappingLabelWithString: "")
    private let result = NSTextField(wrappingLabelWithString: "")
    private var actions: [NSButton] = []
    private var timer: Timer?
    private var stackView: NSStackView?
    private var documentView: NSView?
    private(set) var busy = false
    init(service: MacTab5Service, usb: MacTab5USB, canBegin: @escaping () -> Bool = { true }) {
        self.service = service; self.usb = usb; self.canBegin = canBegin
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 650, height: 680), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        super.init(window: window); window.title = "TAB5 · 资源与固件"; window.minSize = NSSize(width: 600, height: 500); window.delegate = self; window.center()
        let content = window.contentView!, scroll = NSScrollView(frame: window.contentView!.bounds)
        scroll.autoresizingMask = [.width, .height]; scroll.hasVerticalScroller = true; scroll.drawsBackground = false
        let document = MacTab5ToolsDocument(frame: NSRect(x: 0, y: 0, width: content.bounds.width, height: 900)); document.autoresizingMask = [.width]
        scroll.documentView = document; content.addSubview(scroll)
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false; document.addSubview(stack)
        stackView = stack; documentView = document
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: document.leadingAnchor, constant: 24), stack.trailingAnchor.constraint(equalTo: document.trailingAnchor, constant: -24), stack.topAnchor.constraint(equalTo: document.topAnchor, constant: 24)])
        func label(_ text: String, size: CGFloat = 13, bold: Bool = false) {
            let label = NSTextField(wrappingLabelWithString: text); label.font = .systemFont(ofSize: size, weight: bold ? .semibold : .regular)
            stack.addArrangedSubview(label); label.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        }
        func button(_ text: String, _ selector: Selector) { let button = NSButton(title: text, target: self, action: selector); actions.append(button); stack.addArrangedSubview(button) }
        label("资源与固件", size: 23, bold: true)
        label("图库", size: 15, bold: true)
        label("导入现有 AI-bot 每日名画／每日书法 ZIP。校验全部文件后切换，原图库保留为备份。桌宠沿用菜单中的动画选择，音乐封面随当前曲目同步。")
        button("导入图库 ZIP…", #selector(importGallery))
        label("TAB5 升级", size: 15, bold: true)
        label("选择升级 BIN 或 ZIP 后，固件才会提供给已配对设备。开始升级需要明确确认；传输完成后还需核验启动。请保持供电。")
        button("选择升级固件…", #selector(selectFirmware))
        button("请求 TAB5 开始升级…", #selector(startUpgrade))
        button("核验所选固件的启动状态", #selector(verifyBoot))
        button("停止提供所选固件", #selector(clearFirmware))
        stack.addArrangedSubview(status); status.font = .systemFont(ofSize: 12); status.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        stack.addArrangedSubview(result); result.textColor = .secondaryLabelColor; result.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refresh() }; refresh()
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate() }
    func open() { refresh(); showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    func windowShouldClose(_ sender: NSWindow) -> Bool { !busy }
    private func refresh() {
        status.stringValue = service.firmwareControl.summary
        window?.contentView?.layoutSubtreeIfNeeded()
        if let documentView, let stackView { documentView.setFrameSize(NSSize(width: documentView.frame.width, height: max(680, stackView.fittingSize.height + 48))) }
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
    @objc private func importGallery() {
        guard let url = pick([.zip]) else { return }
        work { [service] in let manifest = try service.gallery.install(url); return "已校验并导入 \(manifest.category == "painting" ? "每日名画" : "每日书法")：\(manifest.works) 件作品、\(manifest.frames) 张图。" }
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
private final class MacTab5ToolsDocument: NSView { override var isFlipped: Bool { true } }
