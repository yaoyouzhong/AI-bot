import AppKit
import UniformTypeIdentifiers

final class MacTab5InstallWindow: NSWindowController, NSWindowDelegate {
    private let ports = NSPopUpButton(), status = NSTextField(wrappingLabelWithString: "")
    private var actions: [NSButton] = []
    private let begin: () -> Bool, suspend: () -> Void, finish: (Bool) -> Void
    private(set) var busy = false
    init(begin: @escaping () -> Bool = { false }, suspend: @escaping () -> Void = {}, finish: @escaping (Bool) -> Void = { _ in }) {
        self.begin = begin; self.suspend = suspend; self.finish = finish
        let window = MacBridgeSettingsStyle.window("首次安装与备份恢复")
        super.init(window: window); window.delegate = self
        let page = MacBridgeSettingsPage()
        window.contentView = MacBridgeSettingsShell(body: page, status: status)
        func button(_ title: String, _ selector: Selector) { let button = NSButton(title: title, target: self, action: selector); actions.append(button); page.add(button) }
        page.heading("首次安装与备份恢复", subtitle: "仅适用于 TAB5（16 MiB）。已有 AI-bot 请用“固件升级”。")
        page.section("下载端口")
        page.add(MacBridgeSettingsStyle.note("连接 USB-C 数据线，长按 RESET 约 2 秒，绿灯快闪后松开，再刷新端口。"))
        page.field("目标 TAB5 端口", ports); button("刷新 USB 端口", #selector(refresh))
        page.section("首次安装")
        button("选择首刷 ZIP，备份并安装…", #selector(install))
        page.add(MacBridgeSettingsStyle.note("首刷后重启检查屏幕，再回到“USB 配对”。"))
        page.section("备份与恢复")
        button("只备份原机完整 Flash…", #selector(backup))
        button("选择本机备份记录并恢复…", #selector(restore))
        button("打开备份文件夹", #selector(openBackups))
        page.add(MacBridgeSettingsStyle.note("写入前自动备份，仅可恢复到原机。备份含私人设置，请勿分享。"))
        refresh()
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func open() { showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    func windowShouldClose(_ sender: NSWindow) -> Bool { !busy }
    @objc private func refresh() {
        guard !busy else { return }; ports.removeAllItems()
        let values = (try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? []
        ports.addItems(withTitles: values.filter { $0.hasPrefix("cu.usbmodem") || $0.hasPrefix("cu.usbserial") }.sorted().map { "/dev/" + $0 })
        ports.selectItem(at: -1); status.stringValue = ports.numberOfItems == 0 ? "未发现 USB 设备，请检查数据线和下载模式后刷新。" : "请选择 TAB5 下载端口。"
    }
    @objc private func openBackups() { _ = NSWorkspace.shared.open(MacFirmwareFlasher.backupDirectory) }
    @objc private func install() { let panel = NSOpenPanel(); panel.allowedContentTypes = [.zip]; guard panel.runModal() == .OK, let url = panel.url else { return }; execute(packageURL: url) }
    @objc private func backup() { execute() }
    @objc private func restore() { let panel = NSOpenPanel(); panel.allowedContentTypes = [.json]; guard panel.runModal() == .OK, let url = panel.url else { return }; execute(restoring: url) }
    private func execute(packageURL: URL? = nil, restoring: URL? = nil) {
        guard !busy, let port = ports.titleOfSelectedItem else { status.stringValue = "请先选择 TAB5 下载端口。"; return }
        let alert = NSAlert(); alert.messageText = packageURL != nil ? "备份并替换 TAB5 原固件及设置？" : restoring != nil ? "备份当前设备，再恢复所选完整备份？" : "读取并保存原机完整备份？"
        alert.informativeText = "端口：\(port)\n请确认是目标 TAB5，保持供电和数据线连接。"; alert.addButton(withTitle: "确认并开始"); alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn, begin() else { return }
        busy = true; actions.forEach { $0.isEnabled = false }; ports.isEnabled = false; status.stringValue = "正在校验文件与准备工具…"
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            let work = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-tab5-" + UUID().uuidString)
            var tool: MacFirmwareFlasher.ToolInstall?
            var didSuspend = false
            let stage: (String) -> Void = { value in DispatchQueue.main.async { self.status.stringValue = value } }
            var message = ""
            do {
                let package = try packageURL.map(MacTab5InstallPackage.load)
                if let restoring { _ = try MacTab5FlashBackup.read(restoring) }
                tool = try MacFirmwareFlasher.prepareTool(stage: stage)
                suspend()
                didSuspend = true
                let installer = MacTab5Installer(run: { try MacFirmwareFlasher.process(tool!.executable.path, $0) }, stage: stage)
                let backup = try installer.execute(port: port, package: package, restoring: restoring, directory: MacFirmwareFlasher.backupDirectory, work: work)
                message = packageURL == nil && restoring == nil ? "备份已校验：\(backup.lastPathComponent)" : "写入已校验，备份已保留。请重启并检查屏幕与连接。"
            } catch { message = "操作未完成：\(error.localizedDescription) 已有备份保留，请检查后重试。" }
            tool?.cleanup(); if FileManager.default.fileExists(atPath: work.path) { try? FileManager.default.removeItem(at: work) }
            let result = message, suspended = didSuspend
            DispatchQueue.main.async { self.finish(suspended); self.busy = false; self.actions.forEach { $0.isEnabled = true }; self.ports.isEnabled = true; self.status.stringValue = result }
        }
    }
}
