import AppKit
import Foundation
import UniformTypeIdentifiers

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let reader = SessionActivityReader()
    private let dataStore = MacDataStore()
    private lazy var dataService = MacDataService(store: dataStore)
    private lazy var quotaService = MacQuotaService(store: dataStore)
    private lazy var musicService = MacMusicService(store: dataStore)
    private let localizedTextResources = MacLocalizedTextResources()
    private let systemMetrics = MacSystemMetricsService()
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private var server: HTTPStatusServer?
    private var timer: Timer?
    private var dataTimer: Timer?
    private var quotaTimer: Timer?
    private var serial: SerialBridge?
    private var screenSaverState = AutomaticScreenSaverState()
    private var port: UInt16 = 8765

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem.button?.title = "AI-bot"
        statusItem.menu = buildMenu()
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        port = UInt16((1...65_535).contains(storedPort) ? storedPort : 8765)
        do { _ = try PairingTokenStore.loadOrCreate() }
        catch { show("配对令牌创建失败", "无法使用 Keychain 保存设备配对令牌。") }
        server = HTTPStatusServer(port: port, token: PairingTokenStore.read) { [weak self] in
            guard let self else { return Data("{\"version\":1}".utf8) }
            return self.reader.json(extras: self.dataStore.snapshot())
        }
        do {
            try server?.start()
        } catch {
            show("LAN 服务启动失败", error.localizedDescription)
            server = nil
        }
        let canProvisionLan = server != nil
        serial = SerialBridge(status: { [weak self] in
            guard let self else { return SessionActivityReader().capture() }
            return self.reader.capture(extras: self.dataStore.snapshot())
        }, lanConfiguration: { [weak self] in
            guard let self, canProvisionLan, let host = LocalNetworkIdentity.privateIPv4(),
                  let token = PairingTokenStore.read(), token.utf8.count >= 32 else { return nil }
            return (host, self.port, token)
        }, resources: { [weak self] in
            guard let self else { return [] }
            let extras = self.dataStore.snapshot()
            return self.localizedTextResources.capture(
                weather: extras.weather, stocks: extras.stocks, music: extras.music,
                musicCover: extras.musicCover)
        })
        serial?.start()
        updateTitle()
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.updateTitle() }
        Task { await self.dataService.refreshDue(force: true) }
        Task { await self.quotaService.refresh() }
        dataTimer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            guard let self else { return }
            Task { await self.dataService.refreshDue() }
        }
        quotaTimer = Timer.scheduledTimer(withTimeInterval: 120, repeats: true) { [weak self] _ in
            guard let self else { return }
            Task { await self.quotaService.refresh() }
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
        dataTimer?.invalidate()
        quotaTimer?.invalidate()
        serial?.stop()
        server?.stop()
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("查看本机状态", #selector(showStatus)))
        let displayItem = NSMenuItem(title: "显示页面", action: nil, keyEquivalent: "")
        let displayMenu = NSMenu()
        for (title, value) in [
            ("自动轮播", "auto"), ("Claude + Codex", "dual"), ("天气", "weather"),
            ("股票", "stocks"), ("账户额度", "quotas"), ("国产额度", "domestic"),
            ("系统监控", "system"), ("音乐", "music"), ("桌宠", "pet"), ("屏保", "screensaver")
        ] {
            let mode = item(title, #selector(selectDisplayMode))
            mode.representedObject = value
            displayMenu.addItem(mode)
        }
        displayItem.submenu = displayMenu
        menu.addItem(displayItem)

        let brightnessItem = NSMenuItem(title: "屏幕亮度", action: nil, keyEquivalent: "")
        let brightnessMenu = NSMenu()
        for level in [25, 50, 75, 100] {
            let choice = item("\(level)%", #selector(selectBrightness))
            choice.representedObject = level
            brightnessMenu.addItem(choice)
        }
        brightnessItem.submenu = brightnessMenu
        menu.addItem(brightnessItem)
        menu.addItem(item("自动屏保设置…", #selector(configureScreenSaver)))
        menu.addItem(item("重新下发 Wi-Fi 回退配置", #selector(reprovisionLan)))
        menu.addItem(item("导入外部桌宠…", #selector(importPet)))
        let musicAutomation = item("读取音乐状态（需自动化权限）", #selector(toggleMusicAutomation))
        musicAutomation.state = UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) ? .on : .off
        menu.addItem(musicAutomation)
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
        if let metrics = systemMetrics.capture() { dataStore.update(systemMetrics: metrics) }
        Task { await self.musicService.refreshIfEnabled() }
        let snapshot = reader.capture(extras: dataStore.snapshot())
        statusItem.button?.title = "C:\(short(snapshot.codex.state)) A:\(short(snapshot.claude.state))"
        updateAutomaticScreenSaver(snapshot)
    }

    private func short(_ state: String) -> String {
        state == "working" ? "W" : state == "idle" ? "I" : "-"
    }

    @objc private func selectDisplayMode(_ sender: NSMenuItem) {
        guard let mode = sender.representedObject as? String else { return }
        screenSaverState.select(mode)
        guard serial?.sendDisplayMode(mode) == true else {
            show("未发送", "设备尚未通过 USB 握手连接。")
            return
        }
    }

    @objc private func selectBrightness(_ sender: NSMenuItem) {
        guard let level = sender.representedObject as? Int,
              serial?.sendBrightness(level) == true else {
            show("未发送", "设备尚未通过 USB 握手连接。")
            return
        }
    }

    @objc private func toggleMusicAutomation(_ sender: NSMenuItem) {
        let enabled = !UserDefaults.standard.bool(forKey: MacMusicService.enabledKey)
        UserDefaults.standard.set(enabled, forKey: MacMusicService.enabledKey)
        sender.state = enabled ? .on : .off
        if enabled {
            Task { await self.musicService.refreshIfEnabled() }
        } else {
            dataStore.update(music: .empty())
        }
    }

    @objc private func reprovisionLan() {
        guard serial?.provisionLan() == true else {
            show("未下发", "需要已启动的 LAN 服务、私有 IPv4 地址和已握手 USB 设备。")
            return
        }
        show("已下发", "设备已收到当前 LAN 地址和 Keychain 配对令牌。")
    }

    @objc private func configureScreenSaver() {
        let choices = [0, 1, 5, 10, 15, 30, 60]
        let current = Self.screenSaverMinutes()
        let popup = NSPopUpButton(frame: NSRect(x: 0, y: 0, width: 220, height: 26))
        popup.addItems(withTitles: choices.map { $0 == 0 ? "关闭" : "\($0) 分钟" })
        popup.selectItem(at: choices.firstIndex(of: current) ?? 0)
        let alert = NSAlert()
        alert.messageText = "自动屏保"
        alert.informativeText = "达到空闲时间后进入设备屏保；检测到本机输入后恢复先前页面。"
        alert.accessoryView = popup
        alert.addButton(withTitle: "保存")
        alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        let index = popup.indexOfSelectedItem
        guard choices.indices.contains(index) else { return }
        UserDefaults.standard.set(choices[index], forKey: "screensaver_timeout_minutes")
    }

    @objc private func importPet() {
        guard serial?.portName != nil else {
            show("无法导入", "设备尚未通过 USB 握手连接。")
            return
        }
        let panel = NSOpenPanel()
        panel.title = "选择有明确许可说明的桌宠图片"
        panel.allowedContentTypes = [.png, .jpeg, .bmp, .gif]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let asset: MacPetAsset
        do {
            asset = try MacPetAssetImporter.load(url)
        } catch {
            show("无法导入", error.localizedDescription)
            return
        }
        let licenseName = asset.licenseURL.lastPathComponent
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self else { return }
            let sent = self.serial?.sendResource(kind: .petAsset, data: asset.data) == true
            let selected = sent && self.serial?.sendDisplayMode("pet") == true
            DispatchQueue.main.async { [weak self] in
                guard let self else { return }
                if !sent {
                    self.show("发送失败", "桌宠资源未被设备完整确认；请检查 USB 连接后重试。")
                    return
                }
                self.screenSaverState.select("pet")
                let detail = selected ? "桌宠已发送并切换显示。" : "桌宠已发送，但页面切换失败。"
                self.show("桌宠导入完成", detail + "\n许可说明：" + licenseName)
            }
        }
    }

    private func updateAutomaticScreenSaver(_ snapshot: MacStatusSnapshot) {
        let aiWorking = snapshot.codex.state == "working" || snapshot.claude.state == "working"
        guard let mode = screenSaverState.desiredMode(
            idleSeconds: MacIdleTime.seconds(), timeoutMinutes: Self.screenSaverMinutes(),
            aiWorking: aiWorking, musicPlaying: snapshot.music?.playing == true) else { return }
        let sent = serial?.sendDisplayMode(mode) == true
        screenSaverState.confirm(mode, sent: sent)
    }

    private static func screenSaverMinutes(_ defaults: UserDefaults = .standard) -> Int {
        let value = defaults.integer(forKey: "screensaver_timeout_minutes")
        return [0, 1, 5, 10, 15, 30, 60].contains(value) ? value : 0
    }

    @objc private func showStatus() {
        let snapshot = reader.capture(extras: dataStore.snapshot())
        let weather = snapshot.weather.map { "\($0.city) \(Int($0.temperature.rounded()))°" } ?? "未配置"
        let system = snapshot.systemMetrics.map {
            "CPU \(Int($0.cpuPercent.rounded()))% / MEM \(Int($0.memoryPercent.rounded()))%"
        } ?? "等待采样"
        let quotaCount = [snapshot.quotas?.claude, snapshot.quotas?.codex].compactMap { $0 }.count
        let music: String
        if !UserDefaults.standard.bool(forKey: MacMusicService.enabledKey) {
            music = "关闭"
        } else if let title = snapshot.music?.title, !title.isEmpty {
            music = title
        } else {
            music = "无活动会话"
        }
        let usb = serial?.portName ?? "未连接"
        show("AI-bot 状态", "Codex: \(snapshot.codex.state)\nClaude: \(snapshot.claude.state)\n天气: \(weather)\n股票: \(snapshot.stocks?.quotes.count ?? 0)\n额度: \(quotaCount)/2\n音乐: \(music)\n系统: \(system)\nUSB: \(usb)\nLAN 端口: \(port)")
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
        do {
            try PairingTokenStore.save(token)
            let provisioned = serial?.provisionLan() == true
            show("已保存", provisioned ? "配对令牌已写入 Keychain，并已通过 USB 下发。" :
                    "配对令牌已写入 Keychain；设备下次 USB 连接时自动下发。")
        }
        catch { show("保存失败", "Keychain 返回错误。") }
    }

    @objc private func copyAddress() {
        let address = LocalNetworkIdentity.privateIPv4() ?? "127.0.0.1"
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
