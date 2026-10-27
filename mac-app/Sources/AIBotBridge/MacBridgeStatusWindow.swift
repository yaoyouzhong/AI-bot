import AppKit
import UniformTypeIdentifiers

struct MacBridgeStatusView: Codable {
    struct Device: Codable { let kind:String, name:String, enabled:Bool, state:String, transport:String, firmware:String, detail:String }
    var devices:[Device], data:[[String]], listener:String
    var updatedAt = Date()
}
final class MacBridgeStatusWindow: NSWindowController {
    private let capture: () -> MacBridgeStatusView, reconnect: (String,@escaping (Result<Void,Error>) -> Void) -> Void
    private let devices = MacSettingsTable(["设备","状态","当前通道","固件"],minimumHeight:90), data = MacSettingsTable(["数据来源","状态","最近成功更新"])
    private let summary = MacBridgeSettingsStyle.note(""), detail = MacBridgeSettingsStyle.note(""), receipt = MacBridgeSettingsStyle.note("")
    private var current:MacBridgeStatusView?, timer:Timer?, busy = false
    private var reconnectButton:MacSettingsButton!
    init(capture:@escaping () -> MacBridgeStatusView, reconnect:@escaping (String,@escaping (Result<Void,Error>) -> Void) -> Void) {
        self.capture = capture; self.reconnect = reconnect
        super.init(window:MacBridgeSettingsStyle.window("服务状态",width:760,height:660))
        let page = MacBridgeSettingsPage(); page.heading("服务状态"); page.add(summary)
        page.section("设备连接"); page.add(devices); devices.heightAnchor.constraint(equalToConstant:110).isActive = true
        page.section("数据更新"); page.add(data); data.heightAnchor.constraint(equalToConstant:240).isActive = true; page.add(detail)
        reconnectButton = MacSettingsButton("重新连接") { [weak self] in self?.restart() }
        window?.contentView = MacBridgeSettingsShell(body:page,status:receipt,actions:[reconnectButton,MacSettingsButton("导出诊断包…") { [weak self] in self?.export() }])
        devices.selected = { [weak self] _ in self?.selection() }; refresh()
        timer = Timer.scheduledTimer(withTimeInterval:2,repeats:true) { [weak self] _ in if self?.window?.isVisible == true { self?.refresh() } }
    }
    required init?(coder:NSCoder) { fatalError() }
    deinit { timer?.invalidate() }
    func open() { refresh(); MacSettingsControls.open(self) }
    private func refresh() {
        let kind = selected()?.kind, value = capture(); current = value
        let handler = devices.selected; devices.selected = nil
        devices.rows = value.devices.map { [$0.name,$0.state,$0.transport,$0.firmware] }
        devices.select(value.devices.firstIndex { $0.kind == kind } ?? (value.devices.isEmpty ? -1:0)); devices.selected = handler
        data.rows = value.data
        summary.stringValue = "已启用 \(value.devices.filter(\.enabled).count) / \(value.devices.count) 台 · \(value.listener)\n更新于 " + DateFormatter.localizedString(from:value.updatedAt,dateStyle:.none,timeStyle:.medium)
        selection()
    }
    private func selected() -> MacBridgeStatusView.Device? { guard let current,current.devices.indices.contains(devices.selectedRow) else { return nil }; return current.devices[devices.selectedRow] }
    private func selection() { detail.stringValue = selected()?.detail ?? "尚未添加设备。"; reconnectButton?.isEnabled = !busy && selected()?.enabled == true }
    private func restart() {
        guard !busy,let row = selected(),row.enabled else { return }; busy = true; selection(); receipt.stringValue = "正在重新连接…"
        reconnect(row.kind) { [weak self] result in DispatchQueue.main.async { guard let self else { return }; self.busy = false
            switch result { case .success: self.receipt.stringValue = "已重启连接服务，等待设备重新确认。"; case .failure(let error): self.receipt.stringValue = error.localizedDescription }; self.refresh()
        } }
    }
    private func export() {
        let panel = NSSavePanel(); panel.allowedContentTypes = [.zip]; panel.nameFieldStringValue = "AI-bot-diagnostics-" + ISO8601DateFormatter().string(from:Date()).replacingOccurrences(of:":",with:"-") + ".zip"
        guard panel.runModal() == .OK,let target = panel.url else { return }
        do {
            let root = FileManager.default.temporaryDirectory.appendingPathComponent("AI-bot-diagnostics-"+UUID().uuidString)
            try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700]); defer { try? FileManager.default.removeItem(at:root) }
            let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601; encoder.outputFormatting = [.prettyPrinted,.sortedKeys]
            try encoder.encode(capture()).write(to:root.appendingPathComponent("status.json"))
            let text = "AI-bot macOS diagnostics\nBridge: \(Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "development")\nOS: \(ProcessInfo.processInfo.operatingSystemVersionString)\nOnly displayed connection/data statuses are included. No credentials, task content, raw logs or pairing records.\n"
            try Data(text.utf8).write(to:root.appendingPathComponent("README.txt"))
            let zip = root.appendingPathComponent("diagnostics.zip")
            _ = try MacTab5LocalProcess.run("/usr/bin/zip",["-q",zip.path,"status.json","README.txt"],directory:root)
            try Data(contentsOf:zip).write(to:target,options:.atomic); receipt.stringValue = "诊断包已保存。"
        } catch { receipt.stringValue = "导出失败："+error.localizedDescription }
    }
}
