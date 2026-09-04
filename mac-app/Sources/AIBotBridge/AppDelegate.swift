import AppKit
import Foundation

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let reader = SessionActivityReader()
    private let dataStore = MacDataStore()
    private lazy var dataService = MacDataService(store: dataStore)
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private var server: HTTPStatusServer?
    private var timer: Timer?
    private var dataTimer: Timer?
    private var port: UInt16 = 8765

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem.button?.title = "AI-bot"
        statusItem.menu = buildMenu()
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        port = UInt16((1...65_535).contains(storedPort) ? storedPort : 8765)
        server = HTTPStatusServer(port: port, token: PairingTokenStore.read) { [weak self] in
            guard let self else { return Data("{\"version\":1}".utf8) }
            return self.reader.json(extras: self.dataStore.snapshot())
        }
        do { try server?.start() } catch { show("LAN 服务启动失败", error.localizedDescription) }
        updateTitle()
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.updateTitle() }
        Task { await self.dataService.refreshDue(force: true) }
        dataTimer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            guard let self else { return }
            Task { await self.dataService.refreshDue() }
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
        dataTimer?.invalidate()
        server?.stop()
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("查看本机状态", #selector(showStatus)))
        menu.addItem(item("天气和股票设置…", #selector(configureDataSources)))
        menu.addItem(item("设置配对令牌…", #selector(setPairingToken)))
        menu.addItem(item("复制 LAN 服务地址", #selector(copyAddress)))
        menu.addItem(.separator())
        menu.addItem(item("退出", #selector(quit), key: "q"))
        return menu
    }

    private func item(_ title: String, _ action: Selector, key: String = "") -> NSMenuItem {
        let value = NSMenuItem(title: title, action: action, keyEquivalent: key)
        value.target = self
        return value
    }

    private func updateTitle() {
        let snapshot = reader.capture(extras: dataStore.snapshot())
        statusItem.button?.title = "C:\(short(snapshot.codex.state)) A:\(short(snapshot.claude.state))"
    }

    private func short(_ state: String) -> String {
        state == "working" ? "W" : state == "idle" ? "I" : "-"
    }

    @objc private func showStatus() {
        let snapshot = reader.capture(extras: dataStore.snapshot())
        let weather = snapshot.weather.map { "\($0.city) \(Int($0.temperature.rounded()))°" } ?? "未配置"
        show("AI-bot 状态", "Codex: \(snapshot.codex.state)\nClaude: \(snapshot.claude.state)\n天气: \(weather)\n股票: \(snapshot.stocks?.quotes.count ?? 0)\n端口: \(port)")
    }

    @objc private func configureDataSources() {
        let preferences = MacDataPreferences.load()
        let city = NSTextField(string: preferences.city)
        let latitude = NSTextField(string: preferences.latitude.map { String($0) } ?? "")
        let longitude = NSTextField(string: preferences.longitude.map { String($0) } ?? "")
        let stocks = NSTextField(string: preferences.symbols.joined(separator: ","))
        let stack = NSStackView()
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 8
        for (label, field) in [("天气城市", city), ("纬度（可空）", latitude),
                               ("经度（可空）", longitude), ("股票代码（逗号分隔，最多 20 个）", stocks)] {
            let title = NSTextField(labelWithString: label)
            field.frame.size.width = 420
            stack.addArrangedSubview(title)
            stack.addArrangedSubview(field)
        }
        stack.frame = NSRect(x: 0, y: 0, width: 420, height: 220)

        let alert = NSAlert()
        alert.messageText = "天气和股票设置"
        alert.informativeText = "经纬度必须同时填写或同时留空；设置仅保存在 UserDefaults。"
        alert.accessoryView = stack
        alert.addButton(withTitle: "保存")
        alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }

        let latitudeText = latitude.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        let longitudeText = longitude.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        let lat = Double(latitudeText)
        let lon = Double(longitudeText)
        guard (latitudeText.isEmpty && longitudeText.isEmpty) ||
              (lat != nil && lon != nil && (-90...90).contains(lat!) && (-180...180).contains(lon!)) else {
            show("未保存", "经纬度必须同时留空，或填写有效数字：纬度 -90~90、经度 -180~180。")
            return
        }
        let rawSymbols = stocks.stringValue.replacingOccurrences(of: "，", with: ",")
            .split(separator: ",", omittingEmptySubsequences: true).map(String.init)
        let normalized = rawSymbols.compactMap(MacDataService.normalizeStock)
        guard rawSymbols.count <= 20 && normalized.count == rawSymbols.count else {
            show("未保存", "股票代码最多 20 个，仅支持 sh、sz、bj、hk、us 市场前缀。")
            return
        }
        let uniqueSymbols = normalized.reduce(into: [String]()) { result, symbol in
            if !result.contains(symbol) { result.append(symbol) }
        }
        MacDataPreferences(city: city.stringValue.trimmingCharacters(in: .whitespacesAndNewlines),
                           latitude: lat, longitude: lon, symbols: uniqueSymbols).save()
        Task { await self.dataService.refreshDue(force: true) }
        show("已保存", "天气和股票将立即刷新；网络失败时保留最近一次成功数据。")
    }

    @objc private func setPairingToken() {
        let alert = NSAlert()
        alert.messageText = "设置设备配对令牌"
        alert.informativeText = "令牌至少 32 个字符，仅保存到当前用户 Keychain。"
        alert.addButton(withTitle: "保存")
        alert.addButton(withTitle: "取消")
        let field = NSSecureTextField(frame: NSRect(x: 0, y: 0, width: 360, height: 24))
        alert.accessoryView = field
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        let token = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard token.utf8.count >= 32 else { show("未保存", "令牌必须至少 32 个字符。"); return }
        do { try PairingTokenStore.save(token); show("已保存", "配对令牌已写入 Keychain。") }
        catch { show("保存失败", "Keychain 返回错误。") }
    }

    @objc private func copyAddress() {
        let address = Host.current().addresses.first { value in
            let parts = value.split(separator: ".").compactMap { Int($0) }
            return parts.count == 4 && (parts[0] == 10 || parts[0] == 192 && parts[1] == 168 ||
                parts[0] == 172 && parts[1] >= 16 && parts[1] <= 31)
        } ?? "127.0.0.1"
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString("http://\(address):\(port)/status", forType: .string)
    }

    @objc private func quit() { NSApp.terminate(nil) }

    private func show(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.runModal()
    }
}
