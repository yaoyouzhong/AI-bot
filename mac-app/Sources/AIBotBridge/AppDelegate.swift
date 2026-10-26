import AppKit
import Foundation
import UniformTypeIdentifiers

final class MacWeatherSourceFields: NSObject {
    private let provider: NSPopUpButton
    private let fields: [NSView]
    init(provider: NSPopUpButton, fields: [NSView]) {
        self.provider = provider; self.fields = fields
        super.init()
        provider.target = self; provider.action = #selector(update)
        update()
    }
    @objc func update() {
        fields.forEach { $0.isHidden = provider.indexOfSelectedItem != 1 }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private let reader = SessionActivityReader()
    private let devices = MacDeviceRegistry.shared
    private var stocksWindow: MacStocksWindow?
    private var quotaHistoryWindow: MacQuotaHistoryWindow?
    private var runtimeWindow: MacRuntimeWindow?
    private var informationWindows: [String: MacInformationWindow] = [:]
    private let notifications = MacNotifications(), runtimeHealth = MacRuntimeHealth()
    private var notificationWindow: MacNotificationWindow?, configurationWindow: MacConfigurationWindow?, updatesWindow: MacUpdateWindow?
    private var appearanceWindow: NSWindowController?
    private var espDisplayWindow: MacESPDisplayWindow?
    private var bridgeStatusWindow: MacBridgeStatusWindow?
    private let dataStore = MacDataStore(quotaHistory: MacQuotaHistory())
    private lazy var dataService = MacDataService(store: dataStore, demanded: { MacDeviceRegistry.shared.demanded($0) })
    private lazy var quotaService = MacQuotaService(store: dataStore)
    private lazy var domesticQuotaService = MacDomesticQuotaService(store: dataStore)
    private var domesticQuotaWindow: MacDomesticQuotaWindow?
    private lazy var musicService = MacMusicService(store: dataStore)
    private let localizedTextResources = MacLocalizedTextResources()
    private let systemMetrics = MacSystemMetricsService()
    private let networkDiagnostics = MacNetworkDiagnostics()
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private var server: HTTPStatusServer?
    private var timer: Timer?
    private var dataTimer: Timer?
    private var quotaTimer: Timer?
    private var metricsTimer: Timer?
    private var musicTimer: Timer?
    private var serial: SerialBridge?
    private var espDiscovery: MacESPDiscovery?
    private var tab5: MacTab5Service?
    private var tab5USB: MacTab5USB?
    private var tab5Discovery: MacTab5Discovery?
    private var tab5BLE: MacTab5BLE?
    private var tab5Window: MacTab5Window?
    private var screenSaverState = AutomaticScreenSaverState()
    private var port: UInt16 = 8765
    private var deviceOperationBusy = false
    private var flasher: MacFirmwareFlashWindow?
    private var tab5Installer: MacTab5InstallWindow?
    private var flashSerialStopped = false
    private var gallery: MacPetGalleryWindow?
    private var mirror: MacMirrorWindow?
    private var tab5Preview: MacTab5ScreenPreviewWindow?
    private var tab5PreviewDeviceID: String?
    private var windowPreviewDevices: [MacRegisteredDevice]?
    private var lastAttention = false
    private var lastCompletion: Int64 = 0
    private var wakeUntil = Date.distantPast

    func applicationDidFinishLaunching(_ notification: Notification) {
        configureApplicationMenu()
        statusItem.button?.title = "AI-bot"
        if let url = Bundle.main.url(forResource: "AppIcon", withExtension: "icns"),
           let image = NSImage(contentsOf: url) {
            image.size = NSSize(width: 18, height: 18)
            statusItem.button?.image = image
            statusItem.button?.title = ""
            statusItem.button?.imagePosition = .imageOnly
        }
        statusItem.button?.toolTip = "AI-bot Bridge"
        statusItem.button?.target=self
        statusItem.button?.action=#selector(statusClicked)
        statusItem.button?.sendAction(on:[.leftMouseUp,.rightMouseUp])
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        port = UInt16((1...65_535).contains(storedPort) ? storedPort : 8765)
        do { _ = try PairingTokenStore.loadOrCreate() }
        catch { show("无法访问设备配对令牌", "请检查钥匙串授权后重新启动。读取失败不会替换现有令牌，也不会反复请求密码。") }
        screenSaverState.select(MacDisplayPolicy.load().selectedMode)
        let music = { [dataStore] in dataStore.snapshot().music }
        let tab5 = MacTab5Service(port: port, computer: MacTab5Computer(music: music, musicUpdated: { [dataStore] in dataStore.update(music: $0) }), networkDiagnostics: { [networkDiagnostics] in networkDiagnostics.snapshot() }, music: { [dataStore] in
            let media = dataStore.snapshot(); return (media.music, media.musicTab5Cover ?? media.musicCover)
        }, enabled: { MacDeviceRegistry.shared.enabled("tab5") }, sourceEnabled: { MacDeviceRegistry.shared.source($0, kind: "tab5") })
        self.tab5 = tab5
        DispatchQueue.global(qos:.utility).async { tab5.unlock.load() }
        do { try devices.migrate(tab5: tab5.currentPairing?.deviceId) }
        catch { show("设备配置未载入", "原文件已保留，设备连接暂停。" + error.localizedDescription) }
        tab5.codex.start()
        networkDiagnostics.start()
        tab5.publish(devices.filter(reader.capture(extras: dataStore.snapshot()), kind: "tab5"))
        server = HTTPStatusServer(port: port, token: PairingTokenStore.read, resources: { [weak self] in self?.resourceSnapshot() ?? [] }, event: { [weak self] agent, event, message in
            self?.reader.signals.record(agent:agent,event:event,message:message) ?? false
        }, acknowledge: { [weak self] in self?.reader.signals.acknowledge() }, tab5: tab5, espEnabled: { MacDeviceRegistry.shared.enabled("esp8266") && MacDeviceRegistry.shared.device("esp8266")?.connection != "usb" }) { [weak self] in
            guard let self else { return Data("{\"version\":1}".utf8) }
            let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601
            return (try? encoder.encode(self.devices.filter(self.reader.capture(extras: self.dataStore.snapshot()), kind: "esp8266"))) ?? Data()
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
            return self.devices.filter(self.reader.capture(extras: self.dataStore.snapshot()), kind: "esp8266")
        }, lanConfiguration: { [weak self] in
            guard let self, canProvisionLan, let host = LocalNetworkIdentity.privateIPv4(),
                  let token = PairingTokenStore.read(), token.utf8.count >= 32 else { return nil }
            return (host, self.port, token)
        }, resources: { [weak self] in
            guard let self else { return [] }
            return self.resourceSnapshot()
        }, enabled: { MacDeviceRegistry.shared.enabled("esp8266") && MacDeviceRegistry.shared.device("esp8266")?.connection != "wifi" })
        serial?.start()
        tab5USB = MacTab5USB(service: tab5)
        tab5USB?.start()
        tab5BLE = MacTab5BLE(service: tab5)
        tab5BLE?.setEnabled(UserDefaults.standard.bool(forKey: MacTab5BLE.enabledKey))
        let espDiscovery = MacESPDiscovery(token:PairingTokenStore.read,
            enabled:{ MacDeviceRegistry.shared.enabled("esp8266") && MacDeviceRegistry.shared.device("esp8266")?.connection != "usb" },
            port:{ [weak self] in self?.server?.boundPort })
        self.espDiscovery = espDiscovery
        tab5Discovery = MacTab5Discovery(service:tab5,esp:espDiscovery)
        do { try tab5Discovery?.start() }
        catch {
            tab5.setDiscoveryState("未启动；可通过 USB 写入电脑地址")
            show("TAB5 自动发现未启动", "USB 连接仍可使用；Wi-Fi 请先通过 USB 写入电脑地址。")
        }
        metricsTimer=Timer.scheduledTimer(withTimeInterval:0.25,repeats:true){[weak self] _ in
            guard let self,self.devices.demanded("system"),let metrics=self.systemMetrics.capture() else{return}
            self.dataStore.update(systemMetrics:metrics)
            if self.devices.source("system", kind: "esp8266") { self.serial?.sendMetrics(metrics) }
        }
        prepareUpdateWindow(); updatesWindow?.startAutomaticChecks()
        updateTitle()
        Task { await self.musicService.refreshIfEnabled() }
        musicTimer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            guard let self, self.devices.demanded("music") else { return }
            Task { await self.musicService.refreshIfEnabled() }
        }
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.updateTitle() }
        Task { await self.dataService.refreshDue(force: true) }
        Task { await self.quotaService.refresh() }
        Task { await self.domesticQuotaService.refresh() }
        prepareDomesticQuotas(); domesticQuotaWindow?.startBackgroundRefresh()
        MacCalendarStore.shared.schedule()
        dataTimer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            guard let self else { return }
            MacCalendarStore.shared.schedule()
            Task { await self.dataService.refreshDue() }
        }
        quotaTimer = Timer.scheduledTimer(withTimeInterval: 120, repeats: true) { [weak self] _ in
            guard let self else { return }
            Task { await self.domesticQuotaService.refresh() }
            Task { await self.quotaService.refresh() }
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
        dataTimer?.invalidate()
        quotaTimer?.invalidate()
        domesticQuotaWindow?.stop()
        metricsTimer?.invalidate()
        networkDiagnostics.stop()
        musicTimer?.invalidate()
        serial?.stop()
        tab5USB?.stopAndWait()
        tab5Discovery?.stop()
        tab5BLE?.stop()
        tab5?.stop()
        server?.stop()
    }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if updatesWindow?.busy == true { updatesWindow?.open(); return .terminateCancel }
        if flasher?.busy == true { flasher?.open(); return .terminateCancel }
        if tab5Installer?.busy == true { tab5Installer?.open(); return .terminateCancel }
        if tab5Window?.busy == true { tab5Window?.open(); return .terminateCancel }
        if tab5Window?.toolsBusy == true || tab5?.configurationBusy == true { tab5Window?.open(); return .terminateCancel }
        return .terminateNow
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        openTab5()
        return false
    }

    private func configureApplicationMenu() {
        let menu = NSMenu()
        let applicationItem = NSMenuItem()
        let applicationMenu = NSMenu()
        applicationMenu.addItem(item("退出 AI-bot Bridge", #selector(quit), key: "q"))
        applicationItem.submenu = applicationMenu
        menu.addItem(applicationItem)

        let editItem = NSMenuItem()
        let editMenu = NSMenu(title: "编辑")
        for (title, action, key) in [("剪切", #selector(NSText.cut(_:)), "x"), ("复制", #selector(NSText.copy(_:)), "c"), ("粘贴", #selector(NSText.paste(_:)), "v"), ("全选", #selector(NSText.selectAll(_:)), "a")] {
            editMenu.addItem(NSMenuItem(title: title, action: action, keyEquivalent: key))
        }
        editItem.submenu = editMenu; menu.addItem(editItem)

        let windowItem = NSMenuItem()
        let windowMenu = NSMenu(title: "窗口")
        windowMenu.identifier = .init("bridge-window-menu")
        windowMenu.delegate = self
        windowMenu.addItem(NSMenuItem(title: "关闭窗口", action: #selector(NSWindow.performClose(_:)), keyEquivalent: "w"))
        windowMenu.addItem(item("设备中心…", #selector(openTab5)))
        windowItem.submenu = windowMenu
        menu.addItem(windowItem)
        NSApp.mainMenu = menu
        NSApp.windowsMenu = windowMenu
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        guard menu.identifier?.rawValue == "bridge-window-menu" else { return }
        let registered = (try? devices.file.read()) ?? []
        guard windowPreviewDevices != registered else { return }
        windowPreviewDevices = registered
        if let old = menu.items.first(where: { $0.identifier?.rawValue == "mirror" }) { menu.removeItem(old) }
        // Reuse the tray's live device selection and guarded actions so keyboard
        // and accessibility users can open the same preview from a standard menu.
        let current = buildMenu()
        guard let preview = current.items.first(where: { $0.identifier?.rawValue == "mirror" }) else { return }
        current.removeItem(preview)
        let index = menu.items.firstIndex(where: { $0.action == #selector(openTab5) }).map { $0 + 1 } ?? menu.items.count
        menu.insertItem(preview, at: index)
    }

    @objc private func openFlasher() {
        if flasher == nil {
            flasher = MacFirmwareFlashWindow(bridgePort: serial?.portName, begin: { [weak self] in
                guard let self, !self.deviceOperationBusy, self.tab5Window?.busy != true, self.tab5Window?.toolsBusy != true, self.tab5?.configurationBusy != true else { return false }
                self.deviceOperationBusy = true
                return true
            }, suspend: { [weak self] in
                self?.serial?.stopAndWait()
                self?.tab5USB?.stopAndWait()
                // The worker's completion is dispatched to main after this assignment.
                self?.flashSerialStopped = true
            }, finish: { [weak self] success in
                guard let self else { return }
                if success {
                    UserDefaults.standard.set("auto", forKey: "display_mode")
                    UserDefaults.standard.set(true, forKey: "display_cycle_enabled")
                    self.screenSaverState.select("auto"); self.reader.select("auto")
                }
                if self.flashSerialStopped { self.serial?.start(); self.tab5USB?.start(); self.flashSerialStopped = false }
                self.deviceOperationBusy = false
            })
        }
        flasher?.open()
    }

    private func openTab5Installer() {
        if tab5Installer == nil {
            tab5Installer = MacTab5InstallWindow(begin: { [weak self] in
                guard let self, !self.deviceOperationBusy, self.tab5Window?.busy != true, self.tab5Window?.toolsBusy != true, self.tab5?.configurationBusy != true, self.tab5?.setMaintenance(true) == true else { return false }
                self.deviceOperationBusy = true; return true
            }, suspend: { [weak self] in
                self?.serial?.stopAndWait(); self?.tab5USB?.stopAndWait(); self?.tab5BLE?.stopAndWait()
            }, finish: { [weak self] suspended in
                guard let self else { return }; _ = self.tab5?.setMaintenance(false)
                if suspended { self.serial?.start(); self.tab5USB?.start(); self.tab5BLE?.setEnabled(UserDefaults.standard.bool(forKey: MacTab5BLE.enabledKey)) }
                self.deviceOperationBusy = false
            })
        }
        tab5Installer?.open()
    }

    private func buildMenu() -> NSMenu {
        MacDeviceCenterMenu.build(devices: (try? devices.file.read()) ?? [], deviceAction: { [weak self] device, action in
            guard let self, let current = self.devices.device(device.kind), current.id == device.id, current.enabled else { return }
            if action == "mirror" {
                if device.kind == "esp8266" { self.openMirror() }
                else if let service = self.tab5 {
                    if self.tab5Preview == nil || self.tab5PreviewDeviceID != device.id {
                        self.tab5Preview?.close(); self.tab5PreviewDeviceID = device.id
                        self.tab5Preview = MacTab5ScreenPreviewWindow(service:service,name:current.name,valid:{ [weak self] in
                            self?.devices.device("tab5").map { $0.id == device.id && $0.enabled } == true
                        })
                    }
                    self.tab5Preview?.updateName(current.name); self.tab5Preview?.open()
                }
                return
            }
            self.openTab5()
            self.tab5Window?.openDeviceShortcut(kind: device.kind, action: action)
        }, common: { [weak self] action in
            if action == "devices" { self?.openTab5() }
            else if action == "exit" { self?.quit() }
        })
    }

    private func item(_ title: String, _ action: Selector, key: String = "") -> NSMenuItem {
        let value = NSMenuItem(title: title, action: action, keyEquivalent: key)
        value.target = self
        return value
    }

    private func resourceSnapshot() -> [MacResourcePayload] {
        let extras = dataStore.snapshot()
        return localizedTextResources.capture(weather: devices.source("weather",kind:"esp8266") ? extras.weather:nil, stocks: devices.source("stocks",kind:"esp8266") ? extras.stocks:nil,
            music: devices.source("music",kind:"esp8266") ? extras.music:nil, musicCover: devices.source("music",kind:"esp8266") ? extras.musicCover:nil) + MacPetCache.shared.resources()
    }

    private func updateTitle() {
        let snapshot = reader.capture(extras: dataStore.snapshot())
        tab5?.assets.updatePets([MacPetCache.shared.selection("claude"), MacPetCache.shared.selection("codex")])
        tab5?.publish(devices.filter(snapshot, kind: "tab5"))
        statusItem.button?.toolTip = "AI-bot Bridge\nCodex：\(statusLabel(snapshot.codex.state))\nClaude：\(statusLabel(snapshot.claude.state))"
        updateAutomaticScreenSaver(snapshot)
        let health = runtimeHealth.snapshot(metrics:snapshot.systemMetrics,now:Date(),logicalProcessors:ProcessInfo.processInfo.activeProcessorCount,codexEnabled:tab5?.codex.isEnabled ?? false,desktopObserved:tab5?.codex.canMutate ?? false,network:networkDiagnostics.snapshot())
        let keys = (health?["alerts"] as? [[String:Any]] ?? []).compactMap { $0["key"] as? String }
        notifications.observe(snapshot,deviceOnline:tab5?.usbActive == true || tab5?.bootTransport != nil,healthKeys:keys)
    }

    private func statusLabel(_ state: String) -> String {
        state == "working" ? "工作中" : state == "idle" ? "空闲" : "离线"
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
    @objc private func openTab5() {
        guard let service = tab5, let usb = tab5USB else { return }
        if tab5Window == nil {
            tab5Window = MacTab5Window(service: service, usb: usb, canBegin: { [weak self] in self?.deviceOperationBusy == false }, changeBluetooth: { [weak self] enabled in self?.tab5BLE?.setEnabled(enabled) }, install: { [weak self] in self?.openTab5Installer() },
                configureWeather: { [weak self] in self?.configureDataSources() },
                configureStocks: { [weak self] in self?.configureStocks() },
                configureQuotas: { [weak self] in self?.openDomesticQuotas() },
                dataStatus: { [weak self] in
                    guard let self else { return "" }
                    return self.dataStore.weatherDiagnostic + "\n" + self.dataStore.systemDiagnostic + "\n" + self.networkDiagnostics.summary
                }, bridgeAction: { [weak self] action in self?.performBridgeAction(action) },
                smallScreenStatus: { [weak self] in
                    guard let self else { return "USB：未连接" }
                    return "USB：\(self.serial?.portName ?? "未连接")\n\(self.serial?.connectionDetail ?? "等待扫描")\n设备地址：\(self.serial?.deviceHost ?? "未发现")"
                }, registry: devices, smallScreenOnline:{ [weak self] in
                    guard let self else { return false }
                    return self.serial?.portName != nil || self.server?.lastESPRequest.map { Date().timeIntervalSince($0) < 8 } == true
                })
        }
        tab5Window?.open()
    }
    private func performBridgeAction(_ action: MacBridgeAction) {
        switch action {
        case .status: showStatus()
        case .cycle: configureCycle()
        case .screenSaver: configureScreenSaver()
        case .mirror: openMirror()
        case .petGallery: openGallery()
        case .deviceInfo: showDeviceInfo()
        case .flash: openFlasher()
        case .reprovisionLAN: reprovisionLan()
        case .pairingToken: setPairingToken()
        case .copyAddress: copyAddress()
        case .appearance: openAppearance()
        case .espConnection: openESPConnection()
        case .espFallback: testWiFiFallback()
        case .espReset: resetDeviceWiFi()
        case .espData, .tab5Data:
            do { _ = try devices.edit(action == .espData ? "esp8266":"tab5",dataOnly:true); updateTitle() } catch { show("无法保存数据设置",error.localizedDescription) }
        case .quotaHistory:
            if quotaHistoryWindow == nil { quotaHistoryWindow = MacQuotaHistoryWindow(store:dataStore) }; quotaHistoryWindow?.open()
        case .configuration:
            configurationWindow = MacConfigurationWindow { [weak self] in guard let self else { return }; self.reader.select(MacDisplayPolicy.load().selectedMode); self.updateTitle(); Task { await self.dataService.refreshDue(force:true) } }; configurationWindow?.open()
        case .notifications:
            notificationWindow = MacNotificationWindow(service:notifications); notificationWindow?.open()
        case .runtime: openRuntime()
        case .about: openAbout()
        case .updates:
            prepareUpdateWindow(); updatesWindow?.open()
        }
    }
    private func prepareUpdateWindow() {
        if updatesWindow == nil { updatesWindow = MacUpdateWindow(tab5:tab5,upgradeTools:{ [weak self] in self?.openTab5(); self?.tab5Window?.showUpgradeTools() },espTools:{ [weak self] in self?.openFlasher() }) }
    }
    private func openRuntime() {
        let window = MacRuntimeWindow(capture:{ [weak self] in
            guard let self else { return "" }; let sample = self.dataStore.snapshot()
            var lines = [MacRuntimePreferences.current.enabled ? "监控已启用":"监控已停用",self.networkDiagnostics.summary,self.dataStore.systemDiagnostic]
            if let health = self.runtimeHealth.snapshot(metrics:sample.systemMetrics,now:Date(),logicalProcessors:ProcessInfo.processInfo.activeProcessorCount,codexEnabled:self.tab5?.codex.isEnabled ?? false,desktopObserved:self.tab5?.codex.canMutate ?? false,network:self.networkDiagnostics.snapshot()), let checks = health["checks"] as? [[String:Any]] {
                lines += checks.map { "\($0["label"] as? String ?? "")：\($0["summary"] as? String ?? "")\n\($0["detail"] as? String ?? "")" }
            }
            lines.append(self.tab5?.connectionSummary ?? "TAB5 尚未启动"); return lines.joined(separator:"\n\n")
        },reload:{ [weak self] in self?.networkDiagnostics.stop(); self?.networkDiagnostics.start() },
        overviewCapture:{ [weak self] in
            guard let self else { return (nil,nil) }; let metrics = self.dataStore.snapshot().systemMetrics
            return (metrics,self.runtimeHealth.snapshot(metrics:metrics,now:Date(),logicalProcessors:ProcessInfo.processInfo.activeProcessorCount,codexEnabled:self.tab5?.codex.isEnabled ?? false,desktopObserved:self.tab5?.codex.canMutate ?? false,network:self.networkDiagnostics.snapshot()))
        },deviceCapture:{ [weak self] in
            [self?.server?.statusDiagnosticSummary,self?.tab5?.usbDiagnosticSummary,self?.tab5?.screenPreview.diagnostic,self?.tab5?.computer.mediaDiagnostic,self?.dataStore.musicArtworkDiagnostic,self?.tab5?.assets.coverDiagnostic,self?.tab5?.deviceDiagnosticSummary].compactMap { $0 }.joined(separator:"\n\n")
        },
        deviceImage:{ [weak self] in
            guard let media = self?.dataStore.snapshot() else { return nil }
            return media.musicTab5Cover.flatMap(NSImage.init(data:)) ?? media.musicCover.flatMap { MacPetAnimation.image($0,width:112,height:112) }
        },
        deviceImageCaption:{ [weak self] in
            guard let track = self?.dataStore.snapshot().music, !track.title.isEmpty else { return "Mac 当前采样封面：没有活动曲目" }
            return "Mac 当前采样（仅内存显示）\n\(track.title)\n\(track.artist) · \(track.album)"
        },
        deviceRefresh:{ [weak self] done in guard let tab5 = self?.tab5 else { done(); return }; tab5.refreshDeviceDiagnostics(completion:done) }); runtimeWindow = window; window.open()
    }
    private func openAbout() {
        let version = Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "本地开发版"
        let window = MacInformationWindow("关于应用",width:540,height:460,actions:[MacSettingsButton("项目主页") { NSWorkspace.shared.open(URL(string:"https://github.com/yaoyouzhong/AI-bot")!) },MacSettingsButton("使用说明") { if let url = Bundle.main.url(forResource:"MAC-TAB5-CANDIDATE",withExtension:"md") { NSWorkspace.shared.open(url) } },MacSettingsButton("许可文件") { if let url = Bundle.main.url(forResource:"LICENSE",withExtension:nil) { NSWorkspace.shared.open(url) } }],capture:{ "AI-bot macOS 桥接程序\n版本：\(version)\n作者：yaoyouzhong & Codex\n系统：\(ProcessInfo.processInfo.operatingSystemVersionString)\n\n独立实现的 AI 状态桌面时钟桥接。支持 ESP8266 和 M5Stack TAB5，桥接与固件独立编号。\n\n此构建包含本机桌面权限与 Keychain 凭据保护。功能随平台能力开放，Windows PIN 解锁及处理器队列计数不适用于 macOS。" }); informationWindows["about"] = window; window.open()
    }
    private func openESPConnection() {
        let window = MacInformationWindow("连接设置",subtitle:"ESP8266 使用 USB 优先、Wi-Fi 回退。仅 USB／仅 Wi-Fi 可在设备管理中设置。",live:true,actions:[MacSettingsButton("连接方式…") { [weak self] in do { _ = try self?.devices.edit("esp8266") } catch { self?.show("连接设置",error.localizedDescription) } },MacSettingsButton("屏保等待…") { [weak self] in self?.configureScreenSaver() },MacSettingsButton("复制 LAN 地址") { [weak self] in self?.copyAddress() },MacSettingsButton("配对令牌…") { [weak self] in self?.setPairingToken() },MacSettingsButton("下发 LAN") { [weak self] in self?.reprovisionLan() },MacSettingsButton("回退测试") { [weak self] in self?.testWiFiFallback() },MacSettingsButton("自动发现测试") { [weak self] in self?.testESPDiscovery() },MacSettingsButton("重置网络…") { [weak self] in self?.resetDeviceWiFi() }],capture:{ [weak self] in guard let self else { return "" }; return "USB：\(self.serial?.portName ?? "未连接")\n\(self.serial?.connectionDetail ?? "等待扫描")\n设备地址：\(self.serial?.deviceHost ?? "未发现")\nLAN 端口：\(self.port)\n\n\(self.networkDiagnostics.summary)" }); informationWindows["esp-connection"] = window; window.open()
    }
    private func openAppearance() {
        let page = MacBridgeSettingsPage(), status = MacBridgeSettingsStyle.note("")
        let window = MacBridgeSettingsStyle.window("外观设置",height:650); appearanceWindow = NSWindowController(window:window)
        page.heading("外观设置",subtitle:"ESP8266 天气动画和桌宠；修改天气动画后随下一次状态发送。")
        let animation = MacSettingsControls.popup(["机器人","房屋","植物","桌宠","关闭"]), ids = ["robot","house","plant","pet","off"]
        animation.selectItem(at:ids.firstIndex(of:UserDefaults.standard.string(forKey:"weather_animation") ?? "robot") ?? 0); page.field("天气动画",animation)
        animation.target = self; animation.action = #selector(changeWeatherAnimation(_:))
        status.stringValue = "天气动画自动保存，设备连接后更新。"
        for owner in ["claude","codex"] {
            page.section(owner == "claude" ? "Claude 动画":"Codex 动画")
            page.add(MacBridgeSettingsStyle.row([MacSettingsButton("导入动画…") {
                let panel = NSOpenPanel(); panel.allowedContentTypes = [.png,.jpeg,.bmp,.gif]; guard panel.runModal() == .OK,let url = panel.url else { return }
                do { let asset = try MacPetAssetImporter.loadAnimation(url); try MacPetCache.shared.select(owner,data:asset.data); status.stringValue = "已保存，连接设备后自动同步。" } catch { status.stringValue = error.localizedDescription }
            },MacSettingsButton("恢复默认") { do { try MacPetCache.shared.restore(owner); status.stringValue = "已恢复默认动画。" } catch { status.stringValue = error.localizedDescription } }]))
        }
        page.section("预览与屏保"); page.add(MacBridgeSettingsStyle.row([MacSettingsButton("桌宠图库…") { [weak self] in self?.openGallery() },MacSettingsButton("预览…") { [weak self] in self?.openMirror() },MacSettingsButton("自动屏保…") { [weak self] in self?.configureScreenSaver() }]))
        page.section("资源导入"); page.add(MacBridgeSettingsStyle.row([MacSettingsButton("导入外部桌宠…") { [weak self] in self?.importPet() },MacSettingsButton("导入本机旧版桌宠…") { [weak self] in self?.importDefaultPets() },MacSettingsButton("导入本机旧版图标…") { [weak self] in self?.importPageLogos() }]))
        window.contentView = MacBridgeSettingsShell(body:page,status:status); appearanceWindow.map(MacSettingsControls.open)
    }
    @objc private func changeWeatherAnimation(_ sender:NSPopUpButton) {
        let ids = ["robot","house","plant","pet","off"]
        guard ids.indices.contains(sender.indexOfSelectedItem) else { return }
        UserDefaults.standard.set(ids[sender.indexOfSelectedItem],forKey:"weather_animation"); updateTitle()
    }
    @objc private func statusClicked(){guard let button=statusItem.button else{return};if NSApp.currentEvent?.type == .rightMouseUp {buildMenu().popUp(positioning:nil,at:NSPoint(x:0,y:button.bounds.minY),in:button)}else{openTab5()}}
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
        espDisplayWindow = MacESPDisplayWindow(policy:MacDisplayPolicy.load(),apply:{ [weak self] policy in
            UserDefaults.standard.set(policy.selectedMode,forKey:"display_mode"); UserDefaults.standard.set(policy.cycleEnabled,forKey:"display_cycle_enabled")
            UserDefaults.standard.set(policy.intervalSeconds,forKey:"display_cycle_interval_seconds"); UserDefaults.standard.set(policy.pages,forKey:"display_cycle_pages")
            if policy.cycleEnabled { self?.reader.signals.acknowledge() }
            MacDisplayPolicy.cycleAnchor = Int64(Date().timeIntervalSince1970)
            self?.screenSaverState.select(policy.selectedMode); self?.reader.select(policy.selectedMode); _ = self?.serial?.sendDisplayMode(policy.selectedMode)
        },brightness:{ [weak self] in self?.serial?.sendBrightness($0) ?? false },read:{ [weak self] done in
            guard let self, !self.deviceOperationBusy, self.serial?.portName != nil else { done(.failure(MacTab5LocalError.invalid("USB 未连接，亮度尚未读取。"))); return }
            self.deviceOperationBusy = true
            Task { @MainActor in defer { self.deviceOperationBusy = false }; do { done(.success(try await self.deviceAdminService().fetchInfo().brightness)) } catch { done(.failure(error)) } }
        }); espDisplayWindow?.open()
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

    private func testESPDiscovery() {
        guard !deviceOperationBusy, let serial, let espDiscovery else { return }
        deviceOperationBusy = true
        let service = deviceAdminService()
        Task { @MainActor [weak self] in
            defer { self?.deviceOperationBusy = false }
            do { let message = try await MacESPDiscoveryDeviceTest.run(serial:serial,service:service,discovery:espDiscovery); self?.show("自动发现测试通过",message) }
            catch { self?.show("自动发现测试未通过", "已恢复常规 USB 发送并尝试重新下发原 LAN 配置。\n" + error.localizedDescription) }
        }
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
        let minutes = NSTextField(string:String(Self.screenSaverMinutes()))
        let form = MacBridgeSettingsForm("ESP8266 自动屏保", subtitle:"达到空闲时间后进入屏保；检测到输入后恢复先前页面。",height:380)
        form.page.field("等待分钟（0–1440，0 为关闭）",minutes)
        guard form.run(validating:{ Int(minutes.stringValue).map { (0...1440).contains($0) } == true ? nil:"请输入 0–1440 分钟。" }) else { return }
        UserDefaults.standard.set(Int(minutes.stringValue)!,forKey:"screensaver_timeout_minutes")
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
        return (0...1440).contains(value) ? value : 0
    }

    @objc private func showStatus() {
        bridgeStatusWindow = MacBridgeStatusWindow(capture:{ [weak self] in self?.bridgeStatus() ?? .init(devices:[],data:[],listener:"服务已停止") },reconnect:{ [weak self] kind,done in
            guard let self, !self.deviceOperationBusy, self.flasher?.busy != true, self.tab5Installer?.busy != true, self.tab5Window?.busy != true, self.tab5Window?.toolsBusy != true, self.tab5?.configurationBusy != true else { done(.failure(MacTab5Error.busy)); return }
            self.deviceOperationBusy = true
            DispatchQueue.global(qos:.userInitiated).async {
                if kind == "tab5" { self.tab5USB?.stopAndWait() } else { self.serial?.stopAndWait() }
                DispatchQueue.main.async {
                    defer { self.deviceOperationBusy = false }
                    do {
                        guard let server = self.server else { throw MacTab5LocalError.invalid("LAN 服务未初始化。") }; server.stop(); try server.start()
                        if kind == "tab5" { self.tab5USB?.start(); self.tab5BLE?.setEnabled(false); self.tab5BLE?.setEnabled(UserDefaults.standard.bool(forKey:MacTab5BLE.enabledKey)) } else { self.serial?.start() }
                        done(.success(()))
                    } catch { if kind == "tab5" { self.tab5USB?.start() } else { self.serial?.start() }; done(.failure(error)) }
                }
            }
        }); bridgeStatusWindow?.open()
    }
    private func bridgeStatus() -> MacBridgeStatusView {
        let snapshot = dataStore.snapshot(), registered = (try? devices.file.read()) ?? []
        let rows = registered.map { device -> MacBridgeStatusView.Device in
            if device.kind == "tab5" {
                let state = tab5?.connectionStatus, transport = tab5?.usbActive == true ? "USB" : tab5?.bootTransport ?? "—"
                return .init(kind:device.kind,name:device.name,enabled:device.enabled,state:!device.enabled ? "已停用" : transport == "—" ? "未连接":"在线",transport:transport,firmware:state?.firmware ?? "未知",detail:tab5?.connectionSummary ?? "TAB5 服务未启动")
            }
            let wifi = server?.lastESPRequest.map { Date().timeIntervalSince($0) < 8 } ?? false
            let usb = serial?.portName != nil, transport = usb ? "USB" : wifi ? "Wi-Fi":"—"
            return .init(kind:device.kind,name:device.name,enabled:device.enabled,state:!device.enabled ? "已停用" : transport == "—" ? "未连接":usb ? "USB 已连接":"收到认证请求",transport:transport,firmware:"待设备读取",detail:"USB：\(serial?.portName ?? "未连接")\nWi-Fi：\(wifi ? "最近 8 秒收到认证状态请求":"等待设备请求")\n重新连接会重启所选 USB / 蓝牙及共用 LAN 监听，保留配对与配置。")
        }
        var data:[[String]] = []
        func add(_ label:String,_ source:String,_ date:Date?,_ stale:Bool,provider:String? = nil) {
            let enabled = registered.contains { $0.enabled && $0.sources.contains(source) && (provider == nil || $0.providers.contains(provider!)) }
            data.append([label,!enabled ? "未启用":date == nil ? "暂无数据":stale ? "旧缓存":"数据可用",date.map { DateFormatter.localizedString(from:$0,dateStyle:.short,timeStyle:.medium) } ?? "—"])
        }
        add("天气","weather",snapshot.weather?.updatedAt,snapshot.weather?.stale ?? true); add("股票","stocks",snapshot.stocks?.updatedAt,snapshot.stocks?.stale ?? true)
        add("Codex 额度","quotas",snapshot.quotas?.codex?.updatedAt,snapshot.quotas?.codex?.stale ?? true); add("Claude 额度","quotas",snapshot.quotas?.claude?.updatedAt,snapshot.quotas?.claude?.stale ?? true)
        for (id,name) in MacDeviceRegistry.providers { let value = snapshot.domesticQuotas?[id]; add(name+" 额度","quotas",value?.updatedAt,value?.stale ?? true,provider:id) }
        add("音乐","music",snapshot.music?.updatedAt,snapshot.music.map { Date().timeIntervalSince($0.updatedAt) > 3 } ?? true)
        add("系统状态","system",snapshot.systemMetrics?.updatedAt,snapshot.systemMetrics.map { Date().timeIntervalSince($0.updatedAt) > 8 } ?? true)
        return .init(devices:rows,data:data,listener:server?.boundPort.map { "LAN 已监听：\($0)" } ?? "LAN 未就绪")
    }

    @objc private func configureDataSources() { editDataSources(selectedTab: 0) }
    @objc private func configureStocks() {
        stocksWindow = MacStocksWindow(quotes:dataStore.snapshot().stocks?.quotes ?? []) { [weak self] in guard let self else { return }; Task { await self.dataService.refreshDue(force: true) } }
        stocksWindow?.open()
    }
    private func editDataSources(selectedTab: Int) {
        let editor = MacDataSettingsForm(MacDataPreferences.load(), selectedTab: selectedTab, standalone: true, preview:{ [dataService] preferences,key in try await dataService.previewWeather(preferences,draftKey:key) })
        var preferences: MacDataPreferences?
        guard editor.form.run(validating: {
            do {
                let value = try editor.preferences()
                if value.weatherProvider == "qweather" {
                    if !editor.weatherKey.stringValue.isEmpty { try MacWeatherCredentialStore.shared.save(editor.weatherKey.stringValue,host:value.qweatherHost) }
                    _ = try MacWeatherCredentialStore.shared.read(host:value.qweatherHost)
                }
                editor.weatherKey.stringValue = ""; preferences = value; return nil
            } catch { return error.localizedDescription }
        }), let preferences else { return }
        preferences.save()
        Task { await self.dataService.refreshDue(force: true) }
        show("已保存", "天气将立即刷新；网络失败时保留最近一次成功数据。")
    }
    private func prepareDomesticQuotas() {
        if domesticQuotaWindow == nil { domesticQuotaWindow = MacDomesticQuotaWindow(service: domesticQuotaService, store: dataStore, refreshLocal:{ [weak self] in guard let self else { return }; Task { await self.quotaService.refresh() } }) }
    }
    @objc private func openDomesticQuotas() {
        prepareDomesticQuotas()
        domesticQuotaWindow?.open()
    }

    @objc private func setPairingToken() {
        let form = MacBridgeSettingsForm("小屏配对令牌", subtitle: "令牌至少 32 个字符，仅保存到当前用户钥匙串。保存后通过 USB 下发 LAN 配置。", height: 380)
        let field = NSSecureTextField(); field.placeholderString = "输入新的配对令牌"
        form.page.field("设备配对令牌", field)
        guard form.run(validating: { field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines).utf8.count >= 32 ? nil : "令牌必须至少 32 个字符。" }) else { return }
        let token = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
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
