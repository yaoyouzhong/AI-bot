import AppKit
import ServiceManagement
import UserNotifications
import UniformTypeIdentifiers

enum MacLoginItem {
    static var enabled: Bool { SMAppService.mainApp.status == .enabled }
    static func setEnabled(_ value: Bool) throws {
        if value { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
    }
    static var description: String {
        switch SMAppService.mainApp.status {
        case .enabled: return "已设为登录后启动"
        case .requiresApproval: return "等待系统批准，可在系统设置 → 通用 → 登录项中启用"
        case .notFound: return "请从完整的 AI-bot.app 应用包启动后设置"
        default: return "未启用登录后启动"
        }
    }
}

final class MacInformationWindow: NSWindowController {
    private let text = MacBridgeSettingsStyle.label(""), capture: () -> String
    private var timer: Timer?
    init(_ title: String, subtitle: String = "", live: Bool = false, width: CGFloat = 760, height: CGFloat = 620, actions: [NSButton] = [], capture: @escaping () -> String) {
        self.capture = capture; super.init(window: MacBridgeSettingsStyle.window(title, width: width, height: height, minimumHeight: min(height,440)))
        let page = MacBridgeSettingsPage(); page.heading(title, subtitle: subtitle); text.isSelectable = true; page.add(text)
        window?.contentView = MacBridgeSettingsShell(body: page, actions: actions)
        refresh(); if live { timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.refresh() } }
    }
    required init?(coder: NSCoder) { fatalError() }
    deinit { timer?.invalidate() }
    private func refresh() { let value = capture(); if text.stringValue != value { text.stringValue = value } }
    func open() { refresh(); MacSettingsControls.open(self) }
}

struct MacNotificationOptions: Codable {
    var completion = true, attention = true, quota = true, health = true, sound = true, quiet = false
    var quietStart = 1320, quietEnd = 480
    func validate() throws { guard (0..<1440).contains(quietStart), (0..<1440).contains(quietEnd), !quiet || quietStart != quietEnd else { throw MacTab5LocalError.invalid("勿扰开始和结束时间须不同。") } }
    func isQuiet(_ date: Date) -> Bool { let c = Calendar.current, m = c.component(.hour, from: date) * 60 + c.component(.minute, from: date); return quiet && (quietStart < quietEnd ? m >= quietStart && m < quietEnd : m >= quietStart || m < quietEnd) }
}
final class MacNotifications {
    let file: MacProfileFile<MacNotificationOptions>
    init(directory: URL? = nil) { file = .init("notifications.json",directory:directory,initial:{.init()},validate:{ try $0.validate() }) }
    private(set) var history: [[String]] = []
    private var completions: [String: Int64] = [:], attention = Set<String>(), quota = Set<String>(), health = Set<String>()
    private var lastShown: [String: Date] = [:], initialized = false
    func observe(_ snapshot: MacStatusSnapshot, deviceOnline: Bool, healthKeys: [String]) {
        guard let options = try? file.read() else { return }
        let active = FileManager.default.fileExists(atPath: file.url.path)
        defer { initialized = true }
        for (id, state) in [("Codex", snapshot.codex), ("Claude", snapshot.claude)] {
            if initialized, state.completionAt > (completions[id] ?? 0), state.completionActive { record("completion", "\(id) 任务已完成", options: options, active: active, onDevice: deviceOnline) }
            completions[id] = max(state.completionAt, completions[id] ?? 0)
            if state.needsInput { if attention.insert(id).inserted, initialized { record("attention", "\(id) 等待操作", options: options, active: active) } } else { attention.remove(id) }
        }
        for value in [snapshot.quotas?.codex, snapshot.quotas?.claude].compactMap({ $0 }) {
            let abnormal = value.stale || max(value.primaryPercent ?? 0, value.weeklyPercent ?? 0) >= 90
            if abnormal { if quota.insert(value.provider).inserted, initialized { record("quota", "\(value.provider) 额度需要关注", options: options, active: active) } } else { quota.remove(value.provider) }
        }
        let current = Set(healthKeys)
        if initialized, !current.subtracting(health).isEmpty { record("health", "运行监控发现持续异常", options: options, active: active) }; health = current
    }
    static func outcome(kind:String, options:MacNotificationOptions, at:Date, last:Date?, active:Bool, onDevice:Bool) -> String {
        let enabled = kind == "completion" ? options.completion : kind == "attention" ? options.attention : kind == "health" ? options.health : options.quota
        if kind == "completion" && onDevice { return "由 TAB5 提醒" }
        if !enabled { return "已关闭" }
        if !active { return "尚未启用系统提醒" }
        if options.isQuiet(at) { return "勿扰时段" }
        if let last, at.timeIntervalSince(last) < (kind == "quota" ? 300:10) { return "合并频繁提醒" }
        return kind == "completion" ? (options.sound ? "已请求备用音":"提醒音已关闭") : "已请求通知"
    }
    private func record(_ kind: String, _ title: String, options: MacNotificationOptions, active: Bool, onDevice:Bool = false) {
        let now = Date(), stamp = DateFormatter.localizedString(from: now, dateStyle: .short, timeStyle: .medium)
        let outcome = Self.outcome(kind:kind,options:options,at:now,last:lastShown[kind],active:active,onDevice:onDevice)
        history.insert([stamp,title,outcome],at:0); history = Array(history.prefix(100))
        guard ["已请求通知","已请求备用音","提醒音已关闭"].contains(outcome) else { return }; lastShown[kind] = now
        if kind == "completion" { if options.sound { NSSound.beep() }; return }
        let content = UNMutableNotificationContent(); content.title = "AI-bot"; content.body = title
        if options.sound && kind == "health" { content.sound = .default }
        let request = UNNotificationRequest(identifier:UUID().uuidString,content:content,trigger:nil)
        UNUserNotificationCenter.current().add(request) { [weak self] error in
            if error != nil { DispatchQueue.main.async { self?.history.insert([stamp,title,"系统未接受，请检查通知权限"],at:0); self?.history = Array(self?.history.prefix(100) ?? []) } }
        }
    }

}
final class MacNotificationWindow: NSWindowController {
    private let service: MacNotifications, status = MacBridgeSettingsStyle.note("")
    private var boxes: [NSButton] = [], timer: Timer?
    private let start = NSTextField(), end = NSTextField(), quietRow = NSStackView(), table = MacSettingsTable(["时间", "提醒", "处理"])
    init(service: MacNotifications) {
        self.service = service; super.init(window: MacBridgeSettingsStyle.window("提醒管理", width: 680, height: 560))
        let page = MacBridgeSettingsPage(); page.heading("提醒管理", subtitle: "任务完成时，设备不在线才使用电脑备用提醒；勿扰时段仅记录，不弹出或播放声音。")
        do {
            let value = try service.file.read()
            for (name, selected) in zip(["完成备用音", "等待操作", "额度异常", "运行异常", "提醒音", "勿扰时段"], [value.completion,value.attention,value.quota,value.health,value.sound,value.quiet]) {
                let box = NSButton(checkboxWithTitle: name, target: nil, action: nil); box.state = selected ? .on : .off; boxes.append(box)
            }
            let toggles = NSGridView(views:[Array(boxes.prefix(3)),Array(boxes[3..<5]) + [NSView()]])
            toggles.columnSpacing = 24; toggles.rowSpacing = 12; page.add(toggles)
            page.add(boxes[5]); boxes[5].target = self; boxes[5].action = #selector(quietChanged)
            start.stringValue = String(format: "%02d:%02d", value.quietStart / 60, value.quietStart % 60); end.stringValue = String(format: "%02d:%02d", value.quietEnd / 60, value.quietEnd % 60)
            quietRow.spacing = 12
            for view in [MacBridgeSettingsStyle.label("从"),start,MacBridgeSettingsStyle.label("至"),end] { quietRow.addArrangedSubview(view) }
            start.widthAnchor.constraint(equalToConstant:90).isActive = true; end.widthAnchor.constraint(equalToConstant:90).isActive = true
            page.add(quietRow); quietChanged()
        } catch { status.stringValue = error.localizedDescription }
        page.section("提醒记录"); page.add(table); table.heightAnchor.constraint(equalToConstant: 220).isActive = true; table.rows = service.history
        window?.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [MacSettingsButton("保存") { [weak self] in self?.save() }, MacSettingsButton("取消") { [weak self] in self?.close() }])
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in guard let self else { return }; self.table.rows = self.service.history }
    }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError() }
    func open() { MacSettingsControls.open(self) }
    @objc private func quietChanged() { quietRow.isHidden = boxes.count != 6 || boxes[5].state != .on }
    private func save() {
        func minutes(_ text: String) -> Int? { let fields = text.split(separator: ":",omittingEmptySubsequences:false); let p = fields.compactMap { Int($0) }; guard fields.count == 2, p.count == 2, (0...23).contains(p[0]), (0...59).contains(p[1]) else { return nil }; return p[0] * 60 + p[1] }
        guard boxes.count == 6, let from = minutes(start.stringValue), let to = minutes(end.stringValue) else { status.stringValue = "请输入 HH:mm 格式的有效时间。"; return }
        do {
            let value = MacNotificationOptions(completion: boxes[0].state == .on, attention: boxes[1].state == .on, quota: boxes[2].state == .on, health: boxes[3].state == .on, sound: boxes[4].state == .on, quiet: boxes[5].state == .on, quietStart: from, quietEnd: to)
            try service.file.write(value)
            UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { [weak self] granted, _ in DispatchQueue.main.async { self?.status.stringValue = granted ? "已保存，系统提醒已启用。" : "已保存；系统未授权通知，记录仍可查看。" } }
        } catch { status.stringValue = error.localizedDescription }
    }
}

struct MacConfigurationArchive {
    static let fields = [("display_mode","显示页面"), ("display_cycle_enabled","启用轮播"), ("display_cycle_interval_seconds","轮播间隔"), ("display_cycle_pages","轮播页面与顺序"), ("screensaver_timeout_minutes","屏保等待"), ("weather_animation","天气动画"), ("stock_symbols","自选股票")]
    var settings: [String: String], birthdays: [MacBirthday]
    var createdAt: Date? = nil
    func validate() throws {
        guard settings.count <= Self.fields.count else { throw MacTab5LocalError.invalid("备份字段不支持。") }
        for (key, text) in settings {
            let valid: Bool
            switch key {
            case "display_mode": valid = (["auto", "screensaver", "activity", "quotas", "domestic"] + MacDisplayPolicy.modes).contains(text)
            case "display_cycle_enabled": valid = ["0", "1"].contains(text)
            case "display_cycle_interval_seconds": valid = ["10", "15", "30", "60"].contains(text)
            case "display_cycle_pages": let p = text.split(separator: ",").map(String.init); valid = !p.isEmpty && Set(p).count == p.count && p.allSatisfy(MacDisplayPolicy.modes.contains)
            case "screensaver_timeout_minutes": valid = Int(text).map { (0...1440).contains($0) } == true
            case "weather_animation": valid = ["robot", "house", "plant", "pet", "off"].contains(text)
            case "stock_symbols": let s = text.split(separator: ",").map(String.init); valid = s.count <= 20 && s.allSatisfy { MacDataService.normalizeStock($0) != nil }
            default: valid = false
            }
            guard valid else { throw MacTab5LocalError.invalid("备份设置无效：\(key)") }
        }
        try MacBirthdays.validate(birthdays)
    }
    static func capture() throws -> Self {
        let p = MacDisplayPolicy.load(), d = UserDefaults.standard
        return .init(settings: ["display_mode":p.selectedMode, "display_cycle_enabled":p.cycleEnabled ? "1":"0", "display_cycle_interval_seconds":String(p.intervalSeconds), "display_cycle_pages":p.pages.joined(separator: ","), "screensaver_timeout_minutes":String(d.integer(forKey: "screensaver_timeout_minutes")), "weather_animation":d.string(forKey: "weather_animation") ?? "robot", "stock_symbols":MacDataPreferences.load().symbols.joined(separator: ",")], birthdays: try MacBirthdays.shared.file.read())
    }
    func data() throws -> Data {
        try validate()
        let rows = birthdays.map { ["Name":$0.name, "Month":$0.month, "Day":$0.day, "Lunar":$0.lunar, "Leap":$0.leap, "RemindDays":$0.remind] as [String: Any] }
        return try JSONSerialization.data(withJSONObject: ["Version":1,"CreatedAt":ISO8601DateFormatter().string(from: Date()),"Settings":settings,"Birthdays":rows], options: [.prettyPrinted, .sortedKeys])
    }
    static func read(_ url: URL) throws -> Self {
        guard (try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? Int.max) <= 65536 else { throw MacTab5LocalError.invalid("备份超过 64 KB。") }
        let root = try MacTab5Protocol.object(Data(contentsOf: url))
        guard Set(root.keys).isSubset(of: ["Version","CreatedAt","Settings","Birthdays"]), MacTab5Protocol.integer(root["Version"]) == 1, let settings = root["Settings"] as? [String:String], let rows = root["Birthdays"] as? [[String:Any]] else { throw MacTab5LocalError.invalid("不支持的配置备份。") }
        let values = try rows.map { row -> MacBirthday in
            guard let name = row["Name"] as? String, let month = MacTab5Protocol.integer(row["Month"]), let day = MacTab5Protocol.integer(row["Day"]), let remind = MacTab5Protocol.integer(row["RemindDays"]), MacTab5Protocol.isBoolean(row["Lunar"]), MacTab5Protocol.isBoolean(row["Leap"]) else { throw MacTab5LocalError.invalid("备份生日字段无效。") }
            return .init(name:name,month:Int(month),day:Int(day),lunar:row["Lunar"] as! Bool,leap:row["Leap"] as! Bool,remind:Int(remind))
        }
        let result = Self(settings: settings, birthdays: values, createdAt:(root["CreatedAt"] as? String).flatMap { ISO8601DateFormatter().date(from:$0) }); try result.validate(); return result
    }
    func restore(keys: Set<String>, includeBirthdays: Bool) throws -> URL {
        try validate(); guard !keys.isEmpty || includeBirthdays, keys.isSubset(of: Set(settings.keys)) else { throw MacTab5LocalError.invalid("请至少选择一个有效恢复项目。") }
        let before = try Self.capture(), root = MacProfileFile<[MacBirthday]>.directory.appendingPathComponent("configuration-backups", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true, attributes: [.posixPermissions:0o700])
        let backup = root.appendingPathComponent("before-restore-\(UUID().uuidString).json"); try before.data().write(to: backup, options: .withoutOverwriting)
        if includeBirthdays { try MacBirthdays.shared.file.write(birthdays) }
        for key in keys {
            let value = settings[key]!, defaults = UserDefaults.standard
            switch key {
            case "display_cycle_pages", "stock_symbols": defaults.set(value.split(separator: ",").map(String.init), forKey:key)
            case "display_cycle_enabled": defaults.set(value == "1", forKey:key)
            case "display_cycle_interval_seconds", "screensaver_timeout_minutes": defaults.set(Int(value)!, forKey:key)
            default: defaults.set(value, forKey:key)
            }
        }
        return backup
    }
}
final class MacConfigurationWindow: NSWindowController {
    private let status = MacBridgeSettingsStyle.note(""), page = MacBridgeSettingsPage(), preview = MacBridgeSettingsPage()
    private var archive: MacConfigurationArchive?, choices: [(String, NSButton)] = []
    private let didRestore: () -> Void
    init(didRestore: @escaping () -> Void) {
        self.didRestore = didRestore; super.init(window: MacBridgeSettingsStyle.window("配置迁移", width: 800, height: 620))
        page.heading("配置迁移", subtitle: "与 Windows 共用的显示设置、自选股票和生日备份。恢复前自动备份原配置，凭据及设备配对不包含在文件中。")
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("导出备份") { [weak self] in self?.export() }, MacSettingsButton("打开备份") { [weak self] in self?.read() }]))
        page.add(preview); preview.heightAnchor.constraint(equalToConstant: 300).isActive = true
        window?.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [MacSettingsButton("恢复所选") { [weak self] in self?.restore() }])
    }
    required init?(coder: NSCoder) { fatalError() }
    func open() { MacSettingsControls.open(self) }
    private func export() { let panel = NSSavePanel(); panel.allowedContentTypes = [.json]; panel.nameFieldStringValue = "AI-bot-settings.json"; guard panel.runModal() == .OK, let url = panel.url else { return }; do { try MacConfigurationArchive.capture().data().write(to:url,options:.atomic); status.stringValue = "备份已导出。" } catch { status.stringValue = error.localizedDescription } }
    private func read() {
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.json]; guard panel.runModal() == .OK, let url = panel.url else { return }
        do { let value = try MacConfigurationArchive.read(url), current = try MacConfigurationArchive.capture(); archive = value; choices = []; for view in preview.stack.arrangedSubviews { preview.stack.removeArrangedSubview(view); view.removeFromSuperview() }
            for (key, name) in MacConfigurationArchive.fields where value.settings[key] != nil {
                let box = NSButton(checkboxWithTitle:name,target:nil,action:nil); preview.add(box); choices.append((key,box))
                preview.add(MacBridgeSettingsStyle.note("当前：\(current.settings[key] ?? "—")\n备份：\(value.settings[key]!)"))
            }
            let birthdays = NSButton(checkboxWithTitle:"生日列表",target:nil,action:nil); preview.add(birthdays); choices.append(("birthdays",birthdays))
            func describe(_ values:[MacBirthday]) -> String { values.isEmpty ? "无" : values.map { "\($0.name) · \($0.lunar ? "农历":"公历")\($0.leap ? "闰":"")\($0.month)/\($0.day) · 提前 \($0.remind) 天" }.joined(separator:"；") }
            preview.add(MacBridgeSettingsStyle.note("当前："+describe(current.birthdays)+"\n备份："+describe(value.birthdays)))
            status.stringValue = (value.createdAt.map { "备份时间："+DateFormatter.localizedString(from:$0,dateStyle:.short,timeStyle:.short)+"\n" } ?? "") + "勾选需要恢复的项目。"

        } catch { archive = nil; status.stringValue = error.localizedDescription }
    }
    private func restore() { guard let archive else { status.stringValue = "请先打开备份。"; return }; do { let keys = Set(choices.filter { $0.1.state == .on }.map(\.0)); let backup = try archive.restore(keys:keys.subtracting(["birthdays"]),includeBirthdays:keys.contains("birthdays")); didRestore(); status.stringValue = "已恢复，原配置备份：" + backup.path } catch { status.stringValue = error.localizedDescription } }
}
