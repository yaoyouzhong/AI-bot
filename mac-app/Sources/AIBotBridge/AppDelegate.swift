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
    private var metricsTimer: Timer?
    private var serial: SerialBridge?
    private var screenSaverState = AutomaticScreenSaverState()
    private var port: UInt16 = 8765
    private var deviceOperationBusy = false
    private var gallery: MacPetGalleryWindow?
    private var mirror: MacMirrorWindow?
    private var lastAttention = false
    private var lastCompletion: Int64 = 0
    private var wakeUntil = Date.distantPast

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem.button?.title = "AI-bot"
        statusItem.button?.target=self
        statusItem.button?.action=#selector(statusClicked)
        statusItem.button?.sendAction(on:[.leftMouseUp,.rightMouseUp])
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        port = UInt16((1...65_535).contains(storedPort) ? storedPort : 8765)
        do { _ = try PairingTokenStore.loadOrCreate() }
        catch { show("配对令牌创建失败", "无法使用 Keychain 保存设备配对令牌。") }
        screenSaverState.select(MacDisplayPolicy.load().selectedMode)
        server = HTTPStatusServer(port: port, token: PairingTokenStore.read, resources: { [weak self] in self?.resourceSnapshot() ?? [] }, event: { [weak self] agent, event, message in
            self?.reader.signals.record(agent:agent,event:event,message:message) ?? false
        }, acknowledge: { [weak self] in self?.reader.signals.acknowledge() }) { [weak self] in
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
            return self.resourceSnapshot()
        })
        serial?.start()
        metricsTimer=Timer.scheduledTimer(withTimeInterval:0.25,repeats:true){[weak self] _ in
            guard let self,let metrics=self.systemMetrics.capture() else{return}
            self.dataStore.update(systemMetrics:metrics)
            self.serial?.sendMetrics(metrics)
        }
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
        metricsTimer?.invalidate()
        serial?.stop()
        server?.stop()
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("查看本机状态", #selector(showStatus)))
        menu.addItem(item("设备镜像", #selector(openMirror)))
        menu.addItem(item("确认 Codex 完成提醒", #selector(acknowledgeCompletion)))
        let displayItem = NSMenuItem(title: "显示页面", action: nil, keyEquivalent: "")
        let displayMenu = NSMenu()
        for (title, value) in [
            ("智能跟随", "auto"), ("Claude", "claude"), ("Codex", "codex"), ("Claude + Codex 额度", "dual"), ("天气", "weather"),
            ("股票", "stocks"), ("账户额度", "quotas"), ("国产额度", "domestic"),
            ("系统监控", "system"), ("音乐", "music"), ("桌宠", "pet"), ("屏保", "screensaver")
        ] {
            let mode = item(title, #selector(selectDisplayMode))
            mode.representedObject = value
            displayMenu.addItem(mode)
        }
        displayItem.submenu = displayMenu
        menu.addItem(displayItem)
        menu.addItem(item("轮播页面设置…", #selector(configureCycle)))

        let brightnessItem = NSMenuItem(title: "屏幕亮度", action: nil, keyEquivalent: "")
        let brightnessMenu = NSMenu()
        for level in [25, 50, 75, 100] {
            let choice = item("\(level)%", #selector(selectBrightness))
            choice.representedObject = level
            brightnessMenu.addItem(choice)
        }
        brightnessItem.submenu = brightnessMenu
        menu.addItem(brightnessItem)
        menu.addItem(item("查看设备信息…", #selector(showDeviceInfo)))
        menu.addItem(item("自动屏保设置…", #selector(configureScreenSaver)))
        menu.addItem(item("重新下发 Wi-Fi 回退配置", #selector(reprovisionLan)))
        menu.addItem(item("重置设备 Wi-Fi…", #selector(resetDeviceWiFi)))
        menu.addItem(item("测试 Wi-Fi 回退（保持 USB 供电）…", #selector(testWiFiFallback)))
        menu.addItem(item("导入外部桌宠…", #selector(importPet)))
        menu.addItem(item("更换桌宠动画（图库）…", #selector(openGallery)))
        menu.addItem(item("从本机旧版导入默认桌宠…", #selector(importDefaultPets)))
        menu.addItem(item("从本机旧版导入页面图标…", #selector(importPageLogos)))
        for role in ["claude","codex"] {
            let choice=item("恢复 \(role.capitalized) 默认桌宠",#selector(restorePet));choice.representedObject=role;menu.addItem(choice)
        }
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

    private func resourceSnapshot() -> [MacResourcePayload] {
        let extras = dataStore.snapshot()
        return localizedTextResources.capture(weather: extras.weather, stocks: extras.stocks,
            music: extras.music, musicCover: extras.musicCover) + MacPetCache.shared.resources()
    }

    private func updateTitle() {
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
        selectMode(mode)
    }

    private func selectMode(_ mode: String) {
        screenSaverState.select(mode)
        UserDefaults.standard.set(mode,forKey:"display_mode")
        UserDefaults.standard.set(false,forKey:"display_cycle_enabled")
        reader.select(mode)
        _ = serial?.sendDisplayMode(mode)
    }

    @objc private func acknowledgeCompletion() { reader.signals.acknowledge() }
    @objc private func statusClicked(){guard let button=statusItem.button else{return};if NSApp.currentEvent?.type == .rightMouseUp {buildMenu().popUp(positioning:nil,at:NSPoint(x:0,y:button.bounds.minY),in:button)}else{openMirror()}}
    @objc private func openMirror() {
        guard let button=statusItem.button else{return}
        if mirror == nil { mirror=MacMirrorWindow(anchor:button,capture:{[weak self] in self?.reader.capture(extras:self?.dataStore.snapshot() ?? .empty) ?? SessionActivityReader().capture()},resources:{[weak self] in self?.resourceSnapshot() ?? []},select:{[weak self] mode in self?.selectMode(mode)},brightness:{[weak self] level in self?.serial?.sendBrightness(level) ?? false}) }
        mirror?.open()
    }
    @objc private func openGallery() {
        if gallery == nil {gallery=MacPetGalleryWindow(selectPage:{[weak self] mode in self?.selectMode(mode)})};gallery?.open()
    }
    @objc private func importDefaultPets() {
        let panel=NSOpenPanel();panel.title="选择本机旧版项目目录（仅导入私有缓存，不加入仓库）";panel.canChooseDirectories=true;panel.canChooseFiles=false;panel.allowsMultipleSelection=false
        guard panel.runModal() == .OK,let root=panel.url else{return}
        do {try MacPetCache.shared.importDefaults(from:root);show("已导入默认桌宠","Claude 和 Codex 原始尺寸、6 帧动画已保存到本机缓存，现有自选桌宠不变。")}
        catch {show("导入失败",error.localizedDescription)}
    }
    @objc private func importPageLogos() {
        let panel=NSOpenPanel();panel.title="选择本机旧版目录（图标仅存私有缓存）";panel.canChooseDirectories=true;panel.canChooseFiles=false;panel.allowsMultipleSelection=false
        guard panel.runModal() == .OK,let root=panel.url else{return}
        do {try MacPetCache.shared.importLogos(from:root);show("图标已导入","原 40×40 图标已保存，等待 USB 或 Wi-Fi 同步。")}
        catch {show("图标导入失败",error.localizedDescription)}
    }
    @objc private func restorePet(_ sender:NSMenuItem) {
        guard let role=sender.representedObject as? String else{return}
        do {try MacPetCache.shared.restore(role);show("已恢复","已恢复 \(role) 默认桌宠；USB 或 Wi-Fi 会同步持久化缓存。")}
        catch {show("无法恢复","请先从本机旧版导入默认桌宠。\n"+error.localizedDescription)}
    }
    @objc private func configureCycle() {
        let policy=MacDisplayPolicy.load(),stack=NSStackView();stack.orientation = .vertical;stack.alignment = .leading
        let enabled=NSButton(checkboxWithTitle:"启用循环展示",target:nil,action:nil);enabled.state=policy.cycleEnabled ? .on:.off;stack.addArrangedSubview(enabled)
        let interval=NSPopUpButton();interval.addItems(withTitles:["10 秒","15 秒","30 秒","60 秒"]);interval.selectItem(at:[10,15,30,60].firstIndex(of:policy.intervalSeconds) ?? 1);stack.addArrangedSubview(interval)
        var choices:[NSButton]=[]
        for mode in MacDisplayPolicy.modes {let button=NSButton(checkboxWithTitle:mode,target:nil,action:nil);button.state=policy.pages.contains(mode) ? .on:.off;choices.append(button);stack.addArrangedSubview(button)}
        stack.frame=NSRect(x:0,y:0,width:280,height:300)
        let alert=NSAlert();alert.messageText="轮播设置";alert.informativeText="按所选页面循环；等待输入和完成提醒优先。手动选页将停止轮播。";alert.accessoryView=stack;alert.addButton(withTitle:"保存");alert.addButton(withTitle:"取消")
        guard alert.runModal() == .alertFirstButtonReturn else{return}
        let pages=choices.filter{$0.state == .on}.map{$0.title};guard !pages.isEmpty else{show("未保存","至少选择一个页面。");return}
        UserDefaults.standard.set(enabled.state == .on,forKey:"display_cycle_enabled");UserDefaults.standard.set([10,15,30,60][interval.indexOfSelectedItem],forKey:"display_cycle_interval_seconds");UserDefaults.standard.set(pages,forKey:"display_cycle_pages")
        UserDefaults.standard.set("auto",forKey:"display_mode")
        MacDisplayPolicy.cycleAnchor=Int64(Date().timeIntervalSince1970)
        screenSaverState.select("auto");reader.select("auto");_ = serial?.sendDisplayMode("auto")
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

    @objc private func showDeviceInfo() {
        guard !deviceOperationBusy else { return }
        guard serial?.portName != nil else {
            show("无法读取", "需要通过 USB 连接运行新版固件的设备，无需局域网互访。")
            return
        }
        deviceOperationBusy = true
        let service = deviceAdminService()
        Task { @MainActor [weak self] in
            defer { self?.deviceOperationBusy = false }
            do {
                let info = try await service.fetchInfo()
                await MainActor.run { [weak self] in
                    self?.show("设备信息", "设备: \(info.device)\n协议: v\(info.version)\nIP: \(info.ip)\nUSB: \(info.usbActive ? "活动" : "非活动")\n桥接: \(info.bridgeOnline ? "在线" : "离线")\n页面: \(info.mode)\n亮度: \(info.brightness)%")
                }
            } catch {
                await MainActor.run { [weak self] in
                    self?.show("读取失败", error.localizedDescription)
                }
            }
        }
    }

    @objc private func resetDeviceWiFi() {
        guard !deviceOperationBusy else { return }
        guard serial?.portName != nil else {
            show("无法重置", "需要通过 USB 连接运行新版固件的设备，无需 Wi-Fi 地址或配对令牌。")
            return
        }
        let alert = NSAlert()
        alert.alertStyle = .critical
        alert.messageText = "重置设备 Wi-Fi？"
        alert.informativeText = "设备将删除已保存的 Wi-Fi 配置并立即重启。重启后必须重新配网。"
        alert.addButton(withTitle: "重置并重启")
        alert.addButton(withTitle: "取消")
        alert.buttons.first?.keyEquivalent = ""
        alert.buttons.last?.keyEquivalent = "\r"
        guard alert.runModal() == .alertFirstButtonReturn else { return }

        deviceOperationBusy = true
        let service = deviceAdminService()
        Task { @MainActor [weak self] in
            defer { self?.deviceOperationBusy = false }
            do {
                try await service.resetWiFi()
                await MainActor.run { [weak self] in
                    self?.show("已发送", "设备正在清除 Wi-Fi 配置并重启。")
                }
            } catch {
                await MainActor.run { [weak self] in
                    self?.show("重置失败", error.localizedDescription)
                }
            }
        }
    }

    private func deviceAdminService() -> DeviceAdminService {
        DeviceAdminService(host: { [weak self] in self?.serial?.deviceHost },
                           token: PairingTokenStore.read, serial: serial)
    }

    @objc private func testWiFiFallback() {
        guard !deviceOperationBusy, let serial else { return }
        let alert = NSAlert()
        alert.messageText = "测试 Wi-Fi 回退"
        alert.informativeText = "保持 USB 插着。暂停常规发送约 12 秒，LAN 服务继续运行，然后恢复 USB。网络隔离时预期回退不通过。"
        alert.addButton(withTitle: "开始")
        alert.addButton(withTitle: "取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        deviceOperationBusy = true
        let service = deviceAdminService()
        Task { @MainActor [weak self] in
            defer { serial.resumeTransmission(); self?.deviceOperationBusy = false }
            do {
                try serial.pauseTransmission()
                let before = try await service.fetchInfo()
                guard before.usbActive else { throw DeviceAdminError.unconfirmed }
                try await Task.sleep(nanoseconds: 12_000_000_000)
                let during = try await service.fetchInfo()
                serial.resumeTransmission()
                try await Task.sleep(nanoseconds: 3_000_000_000)
                let after = try await service.fetchInfo()
                let result = DeviceInfo.fallbackPassed(before: before, during: during, after: after)
                self?.show(result ? "测试通过" : "测试未通过", result ?
                    "Wi-Fi 回退与 USB 恢复均已由设备计数确认。" :
                    "已恢复 USB 发送。请检查 LAN 地址、网络隔离、防火墙和设备配网；不自动判定固件故障。")
            } catch {
                serial.resumeTransmission()
                self?.show("测试未通过", error.localizedDescription)
            }
        }
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
        let panel = NSOpenPanel()
        panel.title = "选择有明确许可说明的桌宠图片"
        panel.allowedContentTypes = [.png, .jpeg, .bmp, .gif]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let asset: MacPetAsset
        do {
            asset = try MacPetAssetImporter.loadAnimation(url)
        } catch {
            show("无法导入", error.localizedDescription)
            return
        }
        let licenseName = asset.licenseURL.lastPathComponent
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self else { return }
            do { try MacPetCache.shared.select("claude",data:asset.data);try MacPetCache.shared.select("codex",data:asset.data) }
            catch { DispatchQueue.main.async {self.show("桌宠保存失败",error.localizedDescription)};return }
            let claudeSent = self.serial?.sendResource(kind: .claudePetAnimation, data: asset.data) == true
            let codexSent = self.serial?.sendResource(kind: .codexPetAnimation, data: asset.data) == true
            let sent = claudeSent && codexSent
            let selected = sent && self.serial?.sendDisplayMode("pet") == true
            DispatchQueue.main.async { [weak self] in
                guard let self else { return }
                if !sent {
                    self.show("桌宠已保存", "两种角色的桌宠已持久化；当前 USB 同步未全部确认，设备连接后会继续同步，Wi-Fi 回退也会读取此缓存。")
                    return
                }
                self.selectMode("pet")
                let detail = selected ? "桌宠已发送并切换显示。" : "桌宠已发送，但页面切换失败。"
                self.show("桌宠导入完成", detail + "\n许可说明：" + licenseName)
            }
        }
    }

    private func updateAutomaticScreenSaver(_ snapshot: MacStatusSnapshot) {
        let attention=snapshot.codex.needsInput || snapshot.claude.needsInput || snapshot.domesticActivity?.needsInput == true
        if attention && !lastAttention || snapshot.codex.completionSequence>lastCompletion {wakeUntil=Date().addingTimeInterval(12)}
        lastAttention=attention;lastCompletion=max(lastCompletion,snapshot.codex.completionSequence)
        let aiWorking = snapshot.codex.state == "working" || snapshot.claude.state == "working" || snapshot.domesticActivity?.state == "working" || Date()<wakeUntil
        guard let mode = screenSaverState.desiredMode(
            idleSeconds: MacIdleTime.seconds(), timeoutMinutes: Self.screenSaverMinutes(),
            aiWorking: aiWorking, musicPlaying: snapshot.music?.playing == true,
            wakeMode: MacDisplayPolicy.load(selected:"auto").resolve(snapshot)) else { return }
        reader.select(mode)
        _ = serial?.sendDisplayMode(mode)
        // This confirms publication, not device acceptance: LAN can consume the same policy.
        screenSaverState.confirm(mode, sent: true)
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
        let device = serial?.deviceHost ?? "未发现"
        show("AI-bot 状态", "Codex: \(snapshot.codex.state)\nClaude: \(snapshot.claude.state)\n天气: \(weather)\n股票: \(snapshot.stocks?.quotes.count ?? 0)\n额度: \(quotaCount)/2\n音乐: \(music)\n系统: \(system)\nUSB: \(usb)\n设备 IP: \(device)\nLAN 端口: \(port)")
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
