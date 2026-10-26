import AppKit

struct MacRegisteredDevice: Codable, Equatable {
    var id = MacProfileValidation.id()
    var kind: String, name: String
    var enabled = true
    var hardwareID: String?
    var sources = MacDeviceRegistry.sources.map(\.0)
    var providers: [String] = ["deepseek", "zhipu"]
    var connection = "auto"
}
final class MacDeviceRegistry {
    static let shared = MacDeviceRegistry()
    static let sources = [("activity", "AI 状态"), ("quotas", "模型额度"), ("weather", "天气"), ("stocks", "股票"), ("music", "音乐"), ("system", "系统监控")]
    static let providers = [("qwen", "阿里云百炼"), ("kimi", "Kimi"), ("minimax", "MiniMax"), ("deepseek", "DeepSeek"), ("zhipu", "智谱 GLM"), ("stepfun", "阶跃星辰"), ("baidu", "百度千帆"), ("xiaomi", "小米 MiMo")]
    let file: MacProfileFile<[MacRegisteredDevice]>
    init(directory: URL? = nil) {
        file = .init("devices.json", directory: directory, initial: { [] }, validate: { values in
            guard values.count <= 2, Set(values.map(\.kind)).count == values.count, Set(values.map(\.id)).count == values.count else { throw MacTab5LocalError.invalid("每种设备型号最多添加一台。") }
            for value in values {
                guard ["tab5", "esp8266"].contains(value.kind), MacTab5Protocol.isHex(value.id, count: 32), MacProfileValidation.text(value.name, limit: 40), Set(value.sources).count == value.sources.count, Set(value.providers).count == value.providers.count,
                      value.sources.allSatisfy({ Self.sources.map(\.0).contains($0) }), value.providers.allSatisfy({ Self.providers.map(\.0).contains($0) }), ["auto", "usb", "wifi"].contains(value.connection), value.kind != "tab5" || value.connection == "auto" else { throw MacTab5LocalError.invalid("设备名称、数据源或连接模式无效。") }
            }
        })
    }
    func migrate(tab5: String?) throws {
        guard !FileManager.default.fileExists(atPath: file.url.path) else { _ = try file.read(); return }
        // Preserve the legacy bridge's enabled ESP transport and existing TAB5 pairing.
        var values = [MacRegisteredDevice(kind: "esp8266", name: "ESP8266 小屏", providers: [])]
        if let tab5 { values.insert(MacRegisteredDevice(kind: "tab5", name: "M5Stack TAB5", hardwareID: tab5), at: 0) }
        try file.write(values)
    }
    func device(_ kind: String) -> MacRegisteredDevice? { (try? file.read())?.first { $0.kind == kind } }
    func enabled(_ kind: String) -> Bool { device(kind)?.enabled == true }
    func source(_ source: String, kind: String) -> Bool { guard let d = device(kind), d.enabled else { return false }; return d.sources.contains(source) }
    func demanded(_ source: String) -> Bool { source == "quotas" || (try? file.read())?.contains { $0.enabled && $0.sources.contains(source) } == true }
    func add(_ kind: String, hardwareID: String? = nil) throws {
        try file.update { values in guard !values.contains(where: { $0.kind == kind }) else { throw MacTab5LocalError.invalid("此型号已在设备列表中。") }; values.append(.init(kind: kind, name: kind == "tab5" ? "M5Stack TAB5" : "ESP8266 小屏", hardwareID: hardwareID, providers: kind == "tab5" ? ["deepseek", "zhipu"] : [])) }
    }
    func update(_ proposed: MacRegisteredDevice) throws {
        try file.update { values in
            guard let i = values.firstIndex(where: { $0.id == proposed.id && $0.kind == proposed.kind }) else { throw MacTab5LocalError.invalid("设备已移除，请重新打开设备列表。") }
            var value = proposed; value.hardwareID = values[i].hardwareID; values[i] = value
        }
    }
    func remove(_ kind: String) throws { try file.update { $0.removeAll { $0.kind == kind } } }
    func filter(_ value: MacStatusSnapshot, kind: String) -> MacStatusSnapshot {
        guard let device = device(kind) else { return filtered(value, sources: [], providers: []) }
        var providers = device.providers
        var sources = device.enabled ? device.sources : []
        if kind == "esp8266", device.enabled {
            let policy = value.displayPolicy ?? MacDisplayPolicy.load()
            for mode in policy.pages + [policy.selectedMode] where mode.hasPrefix("domestic_") {
                let id = String(mode.dropFirst(9)), key = id == "alibaba" ? "qwen" : id
                if !providers.contains(key) { providers.append(key) }
                if !sources.contains("quotas") { sources.append("quotas") }
            }
        }
        return filtered(value, sources:sources, providers:providers)
    }
    private func filtered(_ value: MacStatusSnapshot, sources: [String], providers: [String]) -> MacStatusSnapshot {
        let activity = sources.contains("activity"), quotas = sources.contains("quotas"), music = sources.contains("music")
        var domestic = value.domesticQuotas
        let originalDomestic = domestic
        if domestic != nil {
            domestic?.alibaba = providers.contains("qwen") ? originalDomestic?.alibaba : nil
            domestic?.kimi = providers.contains("kimi") ? originalDomestic?.kimi : nil
            domestic?.miniMax = providers.contains("minimax") ? originalDomestic?.miniMax : nil
            domestic?.deepSeek = providers.contains("deepseek") ? originalDomestic?.deepSeek : nil
            domestic?.zhipu = providers.contains("zhipu") ? originalDomestic?.zhipu : nil
            domestic?.stepFun = providers.contains("stepfun") ? originalDomestic?.stepFun : nil
            domestic?.baidu = providers.contains("baidu") ? originalDomestic?.baidu : nil
            domestic?.xiaomi = providers.contains("xiaomi") ? originalDomestic?.xiaomi : nil
        }
        var weather = value.weather
        let animation = UserDefaults.standard.string(forKey:"weather_animation") ?? "robot"
        weather?.animation = ["robot","house","plant","pet","off"].contains(animation) ? animation:"robot"
        return MacStatusSnapshot(version: value.version, time: value.time, epochUtc: value.epochUtc, utcOffsetSeconds: value.utcOffsetSeconds, capturedAt: value.capturedAt,
            codex: activity ? value.codex : ToolState(state: "idle", ageSeconds: nil), claude: activity ? value.claude : ToolState(state: "idle", ageSeconds: nil), musicPlaying: music ? value.musicPlaying : false,
            weather: sources.contains("weather") ? weather : nil, stocks: sources.contains("stocks") ? value.stocks : nil, systemMetrics: sources.contains("system") ? value.systemMetrics : nil,
            quotas: quotas ? value.quotas : nil, music: music ? value.music : nil, displayPolicy: value.displayPolicy, domesticActivity: activity ? value.domesticActivity : nil, domesticQuotas: quotas ? domestic : nil, followApp: activity ? value.followApp : nil, quotaTrend: quotas ? value.quotaTrend : nil)
    }
    func edit(_ kind: String, dataOnly: Bool = false) throws -> Bool {
        guard var value = device(kind) else { throw MacTab5LocalError.invalid("请先添加此型号设备。") }
        let form = MacBridgeSettingsForm(dataOnly ? "数据设置" : "设备管理", subtitle: value.name)
        let name = NSTextField(string: value.name), enabled = NSButton(checkboxWithTitle: "启用此设备", target: nil, action: nil)
        let connection = MacSettingsControls.popup(["USB 优先 / Wi-Fi 回退", "仅 USB", "仅 Wi-Fi"])
        enabled.state = value.enabled ? .on : .off; connection.selectItem(at: ["auto", "usb", "wifi"].firstIndex(of: value.connection) ?? 0)
        if !dataOnly { form.page.field("设备名称", name); form.page.add(enabled); if kind == "esp8266" { form.page.field("连接方式", connection) } }
        form.window?.contentMinSize.height = 520
        let controls = MacDeviceDataControls(page: form.page, device: value)
        let sources = controls.sources, providers = controls.providers
        return withExtendedLifetime(controls) { form.run { [self] in
            value.name = name.stringValue.trimmingCharacters(in: .whitespacesAndNewlines); value.enabled = enabled.state == .on; value.sources = sources.filter { $0.1.state == .on }.map(\.0); value.providers = providers.filter { $0.1.state == .on }.map(\.0)
            value.connection = kind == "tab5" ? "auto" : ["auto", "usb", "wifi"][connection.indexOfSelectedItem]
            do { try update(value); return nil } catch { return error.localizedDescription }
        } }
    }
}
