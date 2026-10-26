import AppKit
import UniformTypeIdentifiers

final class MacComputerWindow: NSWindowController {
    private let service: MacTab5Service, status = MacBridgeSettingsStyle.note("")
    private let enabled = NSButton(checkboxWithTitle:"允许 TAB5 调节音量、静音、锁屏和打开常用入口",target:nil,action:nil)
    private var names: [NSTextField] = [], paths: [NSTextField] = [], ids: [String] = []
    private var draftGuard:MacSettingsDraft?
    private var original: [MacTab5Launcher]?
    private var unlockWindow:MacUnlockSettingsWindow?
    init(service: MacTab5Service, music: @escaping () -> Void) {
        self.service = service; super.init(window:MacBridgeSettingsStyle.window("电脑控制",width:720,height:700))
        let page = MacBridgeSettingsPage(); page.heading("电脑控制",subtitle:"四个常用入口按顺序显示在 TAB5；保存后生效。")
        enabled.state = service.computer.isEnabled ? .on:.off; page.add(enabled)
        page.add(MacBridgeSettingsStyle.note("音量与静音使用系统接口，锁屏需要辅助功能权限。一键解锁独立设置，密码仅保存在本机钥匙串。"))
        page.add(MacSettingsButton("一键解锁设置…") { [weak self] in
            guard let self else { return }
            if self.unlockWindow == nil { self.unlockWindow = MacUnlockSettingsWindow(service:service) }
            self.unlockWindow?.open()
        })
        var initial: [MacTab5Launcher]
        do { initial = try service.computer.launchers.load(); original = initial }
        catch { initial = []; status.stringValue = error.localizedDescription }
        for i in 0..<4 {
            let item = initial.indices.contains(i) ? initial[i]:nil
            let name = NSTextField(string:item?.name ?? ""), path = NSTextField(string:item?.path ?? "")
            path.isEditable = false; path.isSelectable = true; path.placeholderString = "尚未选择"
            names.append(name); paths.append(path); ids.append(item?.id ?? UUID().uuidString.lowercased())
            let buttons = MacBridgeSettingsStyle.row([MacSettingsButton("选应用…") { [weak self] in self?.choose(i,folder:false) },MacSettingsButton("选文件夹…") { [weak self] in self?.choose(i,folder:true) },MacSettingsButton("移除") { [weak self] in self?.names[i].stringValue = ""; self?.paths[i].stringValue = ""; self?.ids[i] = UUID().uuidString.lowercased() }])
            let group = NSStackView(); group.orientation = .vertical; group.alignment = .leading; group.spacing = 8
            group.addArrangedSubview(MacBridgeSettingsStyle.label("常用入口 \(i+1)",weight:.semibold))
            let nameRow = MacBridgeSettingsStyle.row([MacBridgeSettingsStyle.label("名称"),name])
            let pathRow = MacBridgeSettingsStyle.row([MacBridgeSettingsStyle.label("位置"),path])
            for row in [nameRow,pathRow] {
                row.arrangedSubviews[0].widthAnchor.constraint(equalToConstant:34).isActive = true
                group.addArrangedSubview(row); row.widthAnchor.constraint(equalTo:group.widthAnchor).isActive = true
            }
            group.addArrangedSubview(buttons)
            for field in [name,path] { field.setContentHuggingPriority(.defaultLow,for:.horizontal); field.setContentCompressionResistancePriority(.defaultLow,for:.horizontal) }
            page.add(group)
        }
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[MacSettingsButton("保存") { [weak self] in self?.save() },MacSettingsButton("音乐与权限…",music),MacSettingsButton("取消") { [weak self] in self?.close() }])
        draftGuard = MacSettingsDraft(window) { [weak self] in guard let self else { return "" }; return MacSettingsDraft.values(self.names + self.paths + [self.enabled]) }
    }
    required init?(coder:NSCoder) { fatalError() }
    func open() { MacSettingsControls.open(self) }
    private func choose(_ index: Int, folder: Bool) {
        let panel = NSOpenPanel(); panel.canChooseDirectories = folder; panel.canChooseFiles = !folder; panel.allowsMultipleSelection = false
        if !folder { panel.allowedContentTypes = [.application] }
        guard panel.runModal() == .OK, let url = panel.url else { return }
        paths[index].stringValue = url.resolvingSymlinksInPath().path
        ids[index] = UUID().uuidString.lowercased()
        if names[index].stringValue.isEmpty { names[index].stringValue = String(FileManager.default.displayName(atPath:url.path).prefix(16)) }
    }
    private func save() {
        window?.makeFirstResponder(nil)
        do {
            guard let original, try service.computer.launchers.load() == original else { throw MacTab5LocalError.invalid("入口配置已变化，请重新打开后编辑。") }
            let values = paths.indices.filter { !paths[$0].stringValue.isEmpty }.map { MacTab5Launcher(id:ids[$0],name:names[$0].stringValue.trimmingCharacters(in:.whitespacesAndNewlines),path:paths[$0].stringValue) }
            try service.computer.launchers.save(values); UserDefaults.standard.set(enabled.state == .on,forKey:MacTab5Computer.enabledKey); close()
        } catch { status.stringValue = error.localizedDescription }
    }
}
