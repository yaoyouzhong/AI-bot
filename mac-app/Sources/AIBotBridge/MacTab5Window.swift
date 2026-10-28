import AppKit

final class MacTab5Window: NSWindowController, NSWindowDelegate, NSTabViewDelegate {
    private let service: MacTab5Service
    private let usb: MacTab5USB
    private let canBegin: () -> Bool
    private let changeBluetooth: (Bool) -> Void
    private let install: () -> Void
    private let configureWeather: () -> Void
    private let configureStocks: () -> Void
    private let configureQuotas: () -> Void
    private let dataStatus: () -> String
    private let bridgeAction: (MacBridgeAction) -> Void
    private let smallScreenStatus: () -> String
    private let smallScreenOnline: () -> Bool
    private let dataNote = NSTextField(wrappingLabelWithString: "")
    private let deviceState = NSTextField(wrappingLabelWithString: "")
    private let smallScreenState = NSTextField(wrappingLabelWithString: "")
    private let transports = (0..<3).map { _ in NSTextField(labelWithString: "") }
    private let pages = NSTabView()
    private let deviceContent = NSView()
    private let overview = MacBridgeSettingsPage()
    private let smallScreen = MacBridgeSettingsPage()
    private let emptyDevices = MacBridgeSettingsPage()
    private let tab5Title = MacBridgeSettingsStyle.label("M5Stack TAB5",size:23,weight:.semibold)
    private let espTitle = MacBridgeSettingsStyle.label("ESP8266 小屏",size:23,weight:.semibold)
    private let accounts = MacBridgeSettingsPage()
    private let bridge = MacBridgeSettingsPage()
    private var deviceButtons: [NSButton] = []
    private var tabButtons: [NSButton] = []
    private var selectedDevice = 0
    private let result = NSTextField(wrappingLabelWithString: "")
    private var actions: [NSButton] = []
    private var timer: Timer?
    private var tools: MacTab5ToolsWindow?
    private var features: MacTab5FeaturesWindow?
    private var birthdayWindow: MacBirthdayWindow?, agendaWindow: MacAgendaWindow?, pinsWindow: MacTaskPinsWindow?
    private var galleryBrowser: MacGalleryBrowser?, galleryManager: MacGalleryManager?, computerWindow: MacComputerWindow?
    private let registry: MacDeviceRegistry?
    private let startup = NSButton(checkboxWithTitle: "开机启动", target: nil, action: nil)
    private(set) var busy = false

    init(service: MacTab5Service, usb: MacTab5USB, canBegin: @escaping () -> Bool = { true }, changeBluetooth: @escaping (Bool) -> Void = { _ in }, install: @escaping () -> Void = {}, configureWeather: @escaping () -> Void = {}, configureStocks: @escaping () -> Void = {}, configureQuotas: @escaping () -> Void = {}, dataStatus: @escaping () -> String = { "" }, bridgeAction: @escaping (MacBridgeAction) -> Void = { _ in }, smallScreenStatus: @escaping () -> String = { "USB：未连接" }, registry: MacDeviceRegistry? = nil, smallScreenOnline: @escaping () -> Bool = { false }) {
        self.registry = registry; self.smallScreenOnline = smallScreenOnline
        self.service = service; self.usb = usb; self.canBegin = canBegin; self.changeBluetooth = changeBluetooth; self.install = install
        self.configureWeather = configureWeather; self.configureStocks = configureStocks; self.configureQuotas = configureQuotas; self.dataStatus = dataStatus
        self.bridgeAction = bridgeAction; self.smallScreenStatus = smallScreenStatus
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 920, height: 620),
                              styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        super.init(window: window)
        window.title = "AI-bot · 设备中心"; window.delegate = self
        window.contentMinSize = NSSize(width: 820, height: 500); window.center()
        let content = window.contentView!
        let navigation = NSStackView(); navigation.spacing = 8
        navigation.translatesAutoresizingMaskIntoConstraints = false; content.addSubview(navigation)
        for (index, title) in ["我的设备", "账号数据", "桥接设置"].enumerated() {
            let button = MacBridgeNavigationButton(title: title, target: self, action: #selector(selectTab(_:)))
            button.tag = index; button.setButtonType(.pushOnPushOff); button.isBordered = false
            button.state = index == 0 ? .on : .off
            button.widthAnchor.constraint(equalToConstant: 110).isActive = true
            button.heightAnchor.constraint(equalToConstant: 36).isActive = true
            navigation.addArrangedSubview(button); tabButtons.append(button)
        }
        pages.identifier = NSUserInterfaceItemIdentifier("bridge-pages")
        pages.tabViewType = .noTabsNoBorder; pages.delegate = self
        pages.translatesAutoresizingMaskIntoConstraints = false; content.addSubview(pages)
        result.identifier = NSUserInterfaceItemIdentifier("tab5-operation-result")
        result.textColor = .secondaryLabelColor; result.font = .systemFont(ofSize: 12)
        result.translatesAutoresizingMaskIntoConstraints = false; content.addSubview(result)
        NSLayoutConstraint.activate([
            pages.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 12),
            pages.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -12),
            navigation.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 16),
            navigation.topAnchor.constraint(equalTo: content.topAnchor, constant: 12),
            pages.topAnchor.constraint(equalTo: navigation.bottomAnchor, constant: 12),
            pages.bottomAnchor.constraint(equalTo: result.topAnchor, constant: -12),
            result.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 24),
            result.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -24),
            result.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -14),
            result.heightAnchor.constraint(greaterThanOrEqualToConstant: 18)
        ])
        buildDevicesPage()
        buildOverview(); buildSmallScreenPage()
        buildSharedPages()
        if let registry, registry.device("tab5") == nil, registry.device("esp8266") != nil { selectedDevice = 1 }
        showDevicePage(selectedDevice == 0 ? overview:smallScreen)
        refreshStatus()
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refreshStatus() }
    }

    private func addTab(_ title: String, id: String, view: NSView) {
        let tab = NSTabViewItem(identifier: id); tab.label = title; tab.view = view; pages.addTabViewItem(tab)
    }

    private func buildDevicesPage() {
        let body = NSView(); addTab("我的设备", id: "devices", view: body)
        let sidebar = NSVisualEffectView(); sidebar.material = .sidebar; sidebar.blendingMode = .withinWindow
        sidebar.translatesAutoresizingMaskIntoConstraints = false; body.addSubview(sidebar)
        deviceContent.translatesAutoresizingMaskIntoConstraints = false; body.addSubview(deviceContent)
        let list = NSStackView(); list.orientation = .vertical; list.alignment = .leading; list.spacing = 8
        list.translatesAutoresizingMaskIntoConstraints = false; sidebar.addSubview(list)
        let title = note("设备"); list.addArrangedSubview(title)
        for (index, title) in ["M5Stack TAB5", "ESP8266 小屏"].enumerated() {
            let button = MacBridgeNavigationButton(title: title, target: self, action: #selector(selectDevice(_:)))
            button.tag = index; button.identifier = NSUserInterfaceItemIdentifier(index == 0 ? "device-tab5" : "device-esp8266")
            button.setButtonType(.pushOnPushOff); button.isBordered = false
            button.alignment = .left; button.font = .systemFont(ofSize: 13, weight: .medium)
            button.state = index == 0 ? .on : .off
            list.addArrangedSubview(button); button.widthAnchor.constraint(equalTo: list.widthAnchor).isActive = true
            button.heightAnchor.constraint(equalToConstant: 40).isActive = true; deviceButtons.append(button)
        }
        let connect = NSButton(title: "＋ 添加设备", target: self, action: #selector(addDevice))
        connect.identifier = NSUserInterfaceItemIdentifier("connect-device")
        connect.translatesAutoresizingMaskIntoConstraints = false; sidebar.addSubview(connect)
        NSLayoutConstraint.activate([
            sidebar.leadingAnchor.constraint(equalTo: body.leadingAnchor), sidebar.topAnchor.constraint(equalTo: body.topAnchor),
            sidebar.bottomAnchor.constraint(equalTo: body.bottomAnchor), sidebar.widthAnchor.constraint(equalToConstant: 164),
            deviceContent.leadingAnchor.constraint(equalTo: sidebar.trailingAnchor), deviceContent.trailingAnchor.constraint(equalTo: body.trailingAnchor),
            deviceContent.topAnchor.constraint(equalTo: body.topAnchor), deviceContent.bottomAnchor.constraint(equalTo: body.bottomAnchor),
            list.leadingAnchor.constraint(equalTo: sidebar.leadingAnchor, constant: 12), list.trailingAnchor.constraint(equalTo: sidebar.trailingAnchor, constant: -12),
            list.topAnchor.constraint(equalTo: sidebar.topAnchor, constant: 20),
            connect.leadingAnchor.constraint(equalTo: sidebar.leadingAnchor, constant: 12), connect.trailingAnchor.constraint(equalTo: sidebar.trailingAnchor, constant: -12),
            connect.bottomAnchor.constraint(equalTo: sidebar.bottomAnchor, constant: -16)
        ])
        emptyDevices.heading("连接你的第一台设备",subtitle:"用 USB 数据线连接 TAB5 或 ESP8266 小屏。")
        emptyDevices.add(NSButton(title:"＋ 添加设备",target:self,action:#selector(addDevice)))
        for (id, page) in [("tab5-overview", overview), ("esp8266-overview", smallScreen), ("empty-devices",emptyDevices)] {
            page.identifier = NSUserInterfaceItemIdentifier(id); page.translatesAutoresizingMaskIntoConstraints = false
            deviceContent.addSubview(page)
            NSLayoutConstraint.activate([page.leadingAnchor.constraint(equalTo: deviceContent.leadingAnchor), page.trailingAnchor.constraint(equalTo: deviceContent.trailingAnchor), page.topAnchor.constraint(equalTo: deviceContent.topAnchor), page.bottomAnchor.constraint(equalTo: deviceContent.bottomAnchor)])
        }
    }

    private func buildOverview() {
        overview.add(deviceHeading(tab5Title))
        deviceState.font = .systemFont(ofSize: 12); deviceState.textColor = .secondaryLabelColor; overview.add(deviceState)
        let row = NSStackView(); row.distribution = .fillEqually; row.spacing = 12
        for (index, title) in ["USB", "Wi-Fi", "蓝牙"].enumerated() {
            let column = NSStackView(); column.orientation = .vertical; column.alignment = .leading; column.spacing = 6
            column.addArrangedSubview(note(title)); column.addArrangedSubview(transports[index])
            transports[index].font = .systemFont(ofSize: 12)
            transports[index].maximumNumberOfLines = 1
            transports[index].lineBreakMode = .byTruncatingTail
            transports[index].setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
            transports[index].widthAnchor.constraint(equalTo: column.widthAnchor).isActive = true
            row.addArrangedSubview(column)
        }
        overview.add(row)
        overview.addCards([
            card("显示设置", "调节亮度与声音", id: "tab5-display", #selector(editDisplay)),
            card("语音设置", "设置语音与麦克风", id: "tab5-voice", #selector(openVoice)),
            card("日历生日", "设置日历与提醒", id: "birthday-settings", #selector(openBirthdays)),
            card("日程待办", "管理事项与提醒", id: "agenda", #selector(openAgenda)),
            card("电脑控制", "常用软件与文件夹", id: "computer-control", #selector(openFeatures)),
            card("屏保图库", "浏览名画与书法", id: "gallery-browser", #selector(openGalleryManager)),
            card("常用任务", "优先显示常用任务", id: "task-pins", #selector(openPins)),
            commonCard("数据设置", "选择需要的数据", id: "tab5-data", .tab5Data),
            card("连接升级", "管理连接与升级", id: "tab5-connect", #selector(showConnection))
        ])
        overview.add(note("亮度、声音、页面和轮播以设备实际值为准，保存后显示设备确认结果。"))
    }

    private func buildSmallScreenPage() {
        smallScreen.add(deviceHeading(espTitle))
        smallScreenState.font = .systemFont(ofSize: 12); smallScreenState.textColor = .secondaryLabelColor; smallScreen.add(smallScreenState)
        smallScreen.addCards([
            commonCard("显示设置", "选择循环页面、顺序与间隔", id: "esp8266-cycle", .cycle),
            commonCard("外观设置", "天气动画、桌宠与角色动画", id: "esp8266-appearance", .appearance),
            commonCard("数据设置", "选择需要的数据", id: "esp8266-data", .espData),
            commonCard("连接设置", "USB、Wi-Fi 回退和配对令牌", id: "esp8266-connect", .espConnection)
        ])
    }

    private func buildSharedPages() {
        addTab("账号数据", id: "accounts", view: accounts)
        accounts.add(heading("账号数据", size: 23))
        accounts.add(note("管理这台 Mac 提供的天气、股票与模型额度。"))
        accounts.addCards([
            card("模型账号", "Claude、Codex 与国产模型授权", id: "account-quotas", #selector(openQuotas)),
            card("天气定位", "城市、定位与天气来源", id: "account-weather-stocks", #selector(openWeather)),
            card("自选股票", "管理关注的股票与指数", id: "account-stocks", #selector(openStocks)),
            commonCard("额度历史", "7／30 天额度图表与明细", id: "quota-trend", .quotaHistory)
        ])
        accounts.add(heading("数据更新")); dataNote.font = .systemFont(ofSize: 12); dataNote.textColor = .secondaryLabelColor; accounts.add(dataNote)
        addTab("桥接设置", id: "bridge-settings", view: bridge)
        bridge.add(heading("桥接设置", size: 23))
        bridge.add(note("查看本机运行状态，管理局域网连接与使用说明。"))
        startup.state = MacLoginItem.enabled ? .on : .off; startup.target = self; startup.action = #selector(toggleStartup); bridge.add(startup)
        bridge.addCards([
            commonCard("服务状态", "查看状态、数据更新与连接", id: "bridge-status", .status),
            commonCard("软件固件", "检查版本、下载校验与升级", id: "updates", .updates),
            commonCard("配置迁移", "备份、预览与选择恢复", id: "configuration-backup", .configuration),
            commonCard("提醒管理", "任务、额度、运行提醒与勿扰", id: "notifications", .notifications),
            commonCard("运行监控", "系统采样、连接与网络诊断", id: "runtime-health", .runtime),
            commonCard("关于应用", "版本、许可与使用说明", id: "about", .about)
        ])
    }

    private func card(_ title: String, _ detail: String, id: String, _ selector: Selector) -> MacBridgeActionCard {
        let button = MacBridgeActionCard(title, detail, id: id) { [weak self] in
            guard let self else { return }; _ = self.perform(selector)
        }
        actions.append(button); return button
    }
    private func commonCard(_ title: String, _ detail: String, id: String, _ action: MacBridgeAction) -> MacBridgeActionCard {
        let button = MacBridgeActionCard(title, detail, id: id) { [weak self] in self?.bridgeAction(action) }
        actions.append(button); return button
    }
    @objc private func selectDevice(_ sender: NSButton) {
        selectedDevice = sender.tag
        showDevicePage(selectedDevice == 0 ? overview : smallScreen)
    }
    @objc private func selectTab(_ sender: NSButton) {
        pages.selectTabViewItem(at: sender.tag)
        tabView(pages, didSelect: pages.selectedTabViewItem)
    }
    func tabView(_ tabView: NSTabView, didSelect tabViewItem: NSTabViewItem?) {
        let selected = tabViewItem.map { tabView.indexOfTabViewItem($0) } ?? 0
        for button in tabButtons { button.state = button.tag == selected ? .on : .off }
    }
    private func showDevicePage(_ page: MacBridgeSettingsPage) {
        let effective = registry != nil && registry?.device(selectedDevice == 0 ? "tab5":"esp8266") == nil ? emptyDevices:page
        for candidate in [overview, smallScreen, emptyDevices] { candidate.isHidden = candidate !== effective }
        for button in deviceButtons { button.state = button.tag == selectedDevice ? .on : .off }
        refreshStatus()
    }
    @objc private func showOverview() { selectedDevice = 0; showDevicePage(overview) }
    @objc private func showConnection() { openTools() }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc private func openWeather() { configureWeather() }
    @objc private func openStocks() { configureStocks() }
    @objc private func openQuotas() { configureQuotas() }
    func open() { refreshStatus(); showWindow(nil); window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    func openDeviceShortcut(kind: String, action: String) {
        guard registry?.enabled(kind) == true, !busy, canBegin() else { return }
        pages.selectTabViewItem(at: 0)
        selectedDevice = kind == "tab5" ? 0 : 1
        showDevicePage(selectedDevice == 0 ? overview : smallScreen)
        if kind == "esp8266" {
            switch action {
            case "cycle": bridgeAction(.cycle)
            case "appearance": bridgeAction(.appearance)
            case "data": bridgeAction(.espData)
            case "legacy-settings": bridgeAction(.espConnection)
            default: break
            }
        } else if kind == "tab5" {
            switch action {
            case "gallery-browser": openGalleryManager()
            case "voice": openVoice()
            case "birthday-settings": openBirthdays()
            case "computer-control": openFeatures()
            case "data": bridgeAction(.tab5Data)
            case "tab5": showConnection()
            default: break
            }
        }
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if busy { result.stringValue = "正在等待设备确认，请完成后再关闭。" }
        return !busy
    }
    private func heading(_ text: String, size: CGFloat = 15) -> NSTextField {
        let label = NSTextField(labelWithString: text); label.font = .systemFont(ofSize: size, weight: .semibold); return label
    }
    private func note(_ text: String) -> NSTextField {
        let label = NSTextField(wrappingLabelWithString: text); label.font = .systemFont(ofSize: 12); label.textColor = .secondaryLabelColor
        return label
    }
    private func setBusy(_ value: Bool, message: String) {
        busy = value; refreshStatus()
        result.stringValue = message
    }
    private func refreshStatus() {
        if let registry {
            tab5Title.stringValue = registry.device("tab5")?.name ?? "M5Stack TAB5"; espTitle.stringValue = registry.device("esp8266")?.name ?? "ESP8266 小屏"
            for button in deviceButtons {
                let kind = button.tag == 0 ? "tab5" : "esp8266", device = registry.device(button.tag == 0 ? "tab5" : "esp8266")
                button.isHidden = device == nil
                button.title = device.map { $0.name + ($0.enabled ? "" : " · 已停用") } ?? kind
            }
        }
        for button in actions {
            let id = button.identifier?.rawValue ?? ""
            let kind = id.hasPrefix("esp8266-") ? "esp8266" : ["tab5-display","tab5-voice","birthday-settings","agenda","computer-control","gallery-browser","task-pins","tab5-data","tab5-connect"].contains(id) ? "tab5":nil
            button.isEnabled = !busy && (kind.map { registry?.enabled($0) ?? true } ?? true)
        }
        let status = service.connectionStatus
        deviceState.stringValue = (registry?.device("tab5")?.enabled == false ? "已停用" : status.device) + " · 固件 " + status.firmware + status.confirmation
        let usbSummary = status.usb.hasPrefix("USB 未确认") || status.usb.hasPrefix("USB 操作未确认")
            ? "未连接，等待重连" : status.usb.replacingOccurrences(of: "USB ", with: "")
        for (index, text) in [usbSummary, status.wifi, status.bluetooth].enumerated() {
            transports[index].stringValue = text
            transports[index].toolTip = [status.usb, status.wifi, status.bluetooth][index]
        }
        smallScreenState.stringValue = registry?.device("esp8266")?.enabled == false ? "已停用" : smallScreenStatus()
        dataNote.stringValue = dataStatus()
        result.preferredMaxLayoutWidth = max(1, (window?.contentView?.bounds.width ?? 900) - 48)
        window?.contentView?.layoutSubtreeIfNeeded()
        for page in [overview, smallScreen, accounts, bridge] where !page.isHidden { page.fitDocument() }
    }
    func windowDidResize(_ notification: Notification) { refreshStatus() }
    @objc private func openGuide() { if let url = Bundle.main.url(forResource: "MAC-TAB5-CANDIDATE", withExtension: "md") { _ = NSWorkspace.shared.open(url) } }
    var toolsBusy: Bool { tools?.busy == true || galleryManager?.busy == true }
    @objc private func openFeatures() {
        computerWindow = MacComputerWindow(service: service, music: { [weak self] in self?.openFeature(.music) }); computerWindow?.open()
    }
    @objc private func openVoice() { openFeature(.voice) }
    @objc private func openCodex() { openFeature(.codex) }
    @objc private func openMusic() { openFeature(.music) }
    private func openFeature(_ section: MacTab5FeaturesWindow.Section) {
        features = MacTab5FeaturesWindow(service: service, focusSection: section); features?.open(section: section)
    }
    @objc private func openBirthdays() { if birthdayWindow == nil { birthdayWindow = MacBirthdayWindow(store: service.birthdays) }; birthdayWindow?.open() }
    @objc private func openAgenda() { if agendaWindow == nil { agendaWindow = MacAgendaWindow(store: service.agenda) }; agendaWindow?.open() }
    @objc private func openPins() { pinsWindow = MacTaskPinsWindow(codex: service.codex, permissions: { [weak self] in self?.openCodex() }); pinsWindow?.open() }
    @objc private func openGalleryBrowser() { if galleryBrowser == nil { galleryBrowser = MacGalleryBrowser(gallery: service.gallery, importPack: { [weak self] in self?.openGalleryManager() }) }; galleryBrowser?.open() }
    @objc private func openGalleryManager() { if galleryManager == nil { galleryManager = MacGalleryManager(gallery:service.gallery,browse:{ [weak self] in self?.openGalleryBrowser() }) }; galleryManager?.open() }
    @objc private func toggleStartup() { do { try MacLoginItem.setEnabled(startup.state == .on); result.stringValue = MacLoginItem.description } catch { result.stringValue = "登录项设置失败：" + error.localizedDescription }; startup.state = MacLoginItem.enabled ? .on : .off }
    @objc private func addDevice() {
        guard let registry else { return }
        let kinds = ["tab5","esp8266"].filter { registry.device($0) == nil }
        guard !kinds.isEmpty else { result.stringValue = "每种设备型号最多添加一台，当前列表已齐全。"; return }
        let form = MacBridgeSettingsForm("添加设备", subtitle: "每种型号一台，添加后设置 USB 连接。", height: 380)
        let kind = MacSettingsControls.popup(kinds.map { $0 == "tab5" ? "M5Stack TAB5" : "ESP8266 小屏" }); form.page.field("设备型号", kind)
        form.page.add(note("已有 AI-bot：添加后连接。\n出厂设备：添加后先安装固件。"))
        guard form.run(validating: { do { let value = kinds[kind.indexOfSelectedItem]; try registry.add(value, hardwareID: value == "tab5" ? self.service.currentPairing?.deviceId : nil); return nil } catch { return error.localizedDescription } }) else { return }
        selectedDevice = kinds[kind.indexOfSelectedItem] == "tab5" ? 0 : 1; showDevicePage(selectedDevice == 0 ? overview : smallScreen); if selectedDevice == 0 { openTools(page:.usbPairing) }
    }
    @objc private func manageDevice(_ sender: NSButton) {
        let menu = managementMenu(for:selectedDevice == 0 ? "tab5":"esp8266")
        menu.popUp(positioning:nil,at:NSPoint(x:0,y:sender.bounds.height),in:sender)
    }
    func managementMenu(for kind:String) -> NSMenu {
        let menu = NSMenu(); menu.autoenablesItems = false
        let enabled = registry?.device(kind)?.enabled ?? true
        func add(_ title:String,_ action:Selector,allowed:Bool = true) {
            let item = NSMenuItem(title:title,action:action,keyEquivalent:""); item.target = self; item.isEnabled = allowed && !busy && canBegin(); menu.addItem(item)
        }
        add("修改名称",#selector(renameDevice)); add(enabled ? "停用设备":"启用设备",#selector(toggleDevice))
        if kind == "esp8266" {
            menu.addItem(.separator())
            add("固件升级",#selector(deviceUpgrade),allowed:enabled)
            for (title,action) in [("设备信息",#selector(deviceInformation)),("连接诊断",#selector(deviceDiagnostics)),("重置网络",#selector(resetDeviceNetwork))] { add(title,action,allowed:enabled && smallScreenOnline()) }
        }
        menu.addItem(.separator()); add("移除设备…",#selector(removeDevice)); return menu
    }
    @objc private func renameDevice() {
        guard let registry,var device = registry.device(selectedDevice == 0 ? "tab5":"esp8266") else { return }
        let form = MacBridgeSettingsForm("修改设备名称",subtitle:"",width:420,height:240)
        form.window?.contentMinSize = NSSize(width:420,height:240)
        let name = NSTextField(string:device.name); form.page.field("设备名称",name)
        if form.run(validating:{
            device.name = name.stringValue.trimmingCharacters(in:.whitespacesAndNewlines)
            do { try registry.update(device); return nil } catch { return error.localizedDescription }
        }) { refreshStatus() }
    }
    @objc private func toggleDevice() {
        guard !busy,canBegin(),let registry,var device = registry.device(selectedDevice == 0 ? "tab5":"esp8266") else { return }
        device.enabled.toggle()
        do { try registry.update(device); refreshStatus(); result.stringValue = device.enabled ? "设备已启用。":"设备已停用，配置与配对保留。" } catch { result.stringValue = error.localizedDescription }
    }
    @objc private func deviceInformation() { bridgeAction(.deviceInfo) }
    @objc private func deviceDiagnostics() { bridgeAction(.espFallback) }
    @objc private func deviceUpgrade() { bridgeAction(.flash) }
    @objc private func resetDeviceNetwork() { bridgeAction(.espReset) }
    @objc private func removeDevice() {
        guard let registry else { return }; let kind = selectedDevice == 0 ? "tab5":"esp8266"
        let alert = NSAlert(); alert.messageText = "从列表移除此设备？"; alert.informativeText = "会停止此设备的数据服务，保留配对、生日、图库和备份，可重新添加。"; alert.addButton(withTitle:"移除"); alert.addButton(withTitle:"取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }; do { try registry.remove(kind); selectedDevice = registry.device("tab5") != nil ? 0:1; showDevicePage(selectedDevice == 0 ? overview:smallScreen); result.stringValue = "设备已移除。" } catch { result.stringValue = error.localizedDescription }
    }
    func showUpgradeTools() { openTools(page:.firmware) }
    private func deviceHeading(_ title: NSTextField) -> NSView {
        let spacer = NSView(); spacer.setContentHuggingPriority(.defaultLow,for:.horizontal)
        let button = NSButton(title:"管理 ▾",target:self,action:#selector(manageDevice(_:)))
        return MacBridgeSettingsStyle.row([title,spacer,button])
    }
    private func openTools(page:MacTab5ToolsWindow.Page? = nil) {
        if tools == nil { tools = MacTab5ToolsWindow(service: service, usb: usb, canBegin: { [weak self] in self?.busy == false && self?.canBegin() == true }, install: install, voice: { [weak self] in self?.openVoice() }, changeBluetooth: changeBluetooth) }
        tools?.open(page:page)
    }
    private func allowOperation() -> Bool {
        guard !busy else { return false }
        guard canBegin() else { result.stringValue = "请先完成小屏刷机操作，再操作 TAB5。"; return false }
        return true
    }
    @objc private func editDisplay() {
        guard allowOperation() else { return }
        setBusy(true, message: "正在读取 TAB5 显示设置…")
        service.displaySettings(nil) { [weak self] outcome in
            DispatchQueue.main.async {
                guard let self else { return }
                switch outcome {
                case .failure(let error): self.setBusy(false, message: error.localizedDescription)
                case .success(let current):
                    guard let desired = MacTab5SettingsForm.edit(current) else { self.setBusy(false, message: "已读取设备设置，未保存修改。"); return }
                    self.setBusy(true, message: "正在等待设备确认保存…")
                    self.service.displaySettings(desired) { [weak self] saved in
                        DispatchQueue.main.async {
                            guard let self else { return }
                            switch saved {
                            case .success: self.setBusy(false, message: "TAB5 已确认保存显示设置。")
                            case .failure(let error): self.setBusy(false, message: error.localizedDescription)
                            }
                        }
                    }
                }
            }
        }
    }
}

final class MacTab5SettingsForm: NSObject {
    private let brightness = NSTextField()
    private let volume = NSTextField()
    private let muted = NSButton(checkboxWithTitle: "静音", target: nil, action: nil)
    private let cycle = NSButton(checkboxWithTitle: "启用轮播", target: nil, action: nil)
    private let alerts = NSButton(checkboxWithTitle: "显示提醒", target: nil, action: nil)
    private let selected = NSPopUpButton()
    private let interval = NSPopUpButton()
    private let saver = NSPopUpButton()
    private let pageRows = NSStackView()
    private var pageOrder: [Int]
    private var pageChecks: [NSButton] = []
    private let current: MacTab5DisplaySettings
    private var editor: MacBridgeSettingsForm?

    private init(_ current: MacTab5DisplaySettings) {
        self.current = current; pageOrder = current.order
        super.init()
        brightness.stringValue = String(current.brightness); volume.stringValue = String(current.volume)
        muted.state = current.muted ? .on : .off; cycle.state = current.cycle ? .on : .off; alerts.state = current.alerts ? .on : .off
        selected.addItems(withTitles: ["自动"] + MacTab5DisplaySettings.pages); selected.selectItem(at: current.selected + 1)
        interval.addItems(withTitles: ["10 秒", "15 秒", "30 秒", "60 秒"]); interval.selectItem(at: [10, 15, 30, 60].firstIndex(of: current.interval) ?? 1)
        saver.addItems(withTitles: ["关闭", "1 分钟", "5 分钟", "10 分钟", "30 分钟", "60 分钟"])
        saver.selectItem(at: [0, 1, 5, 10, 30, 60].firstIndex(of: current.saverMinutes) ?? 0)
        cycle.target = self; cycle.action = #selector(cycleChanged)
        selected.target = self; selected.action = #selector(modeChanged)
        pageRows.orientation = .vertical; pageRows.alignment = .leading; pageRows.spacing = 3
        for index in 0..<8 {
            let check = NSButton(checkboxWithTitle: MacTab5DisplaySettings.pages[index], target: nil, action: nil)
            check.state = current.enabled[index] == 1 ? .on : .off
            check.setContentHuggingPriority(.defaultLow, for: .horizontal); pageChecks.append(check)
        }
        cycleChanged(); rebuildPages()
    }
    static func edit(_ current: MacTab5DisplaySettings) -> MacTab5DisplaySettings? {
        let form = MacTab5SettingsForm(current)
        let editor = form.makeEditor()
        guard editor.run(validating: {
            do { _ = try form.value(); return nil }
            catch { return error.localizedDescription }
        }) else { return nil }
        return try? form.value()
    }
    static func fixture() -> (NSObject, NSView) {
        let form = MacTab5SettingsForm(.init(revision: "00000000", brightness: 80, volume: 50, muted: false, selected: -1, cycle: true, interval: 15, saverMinutes: 5, alerts: true, enabled: Array(repeating: 1, count: 8), order: Array(0..<8)))
        return (form, form.makeEditor().window!.contentView!)
    }
    private func makeEditor() -> MacBridgeSettingsForm {
        if let editor { return editor }
        let editor = MacBridgeSettingsForm("TAB5 显示设置", subtitle: "以下为设备实际设置；保存后等待设备确认。", height: 700, confirm: "保存到 TAB5")
        self.editor = editor
        let page = editor.page
        page.section("画面与声音")
        let media = NSGridView(views: [[MacBridgeSettingsStyle.label("亮度（10–100）"), brightness], [MacBridgeSettingsStyle.label("音量（0–100）"), volume]])
        media.rowSpacing = 12; media.columnSpacing = 16; page.add(media)
        page.add(MacBridgeSettingsStyle.row([muted, alerts]))
        page.section("页面与轮播")
        page.add(cycle)
        let display = NSGridView(views: [[MacBridgeSettingsStyle.label("固定页面"), selected], [MacBridgeSettingsStyle.label("轮播间隔"), interval], [MacBridgeSettingsStyle.label("屏保等待"), saver]])
        display.rowSpacing = 12; display.columnSpacing = 16; page.add(display)
        page.add(MacBridgeSettingsStyle.note("勾选参与轮播的页面，使用上下按钮调整顺序。"))
        page.add(pageRows)
        return editor
    }
    @objc private func cycleChanged() {
        if cycle.state == .on { selected.selectItem(at: 0) }
    }
    @objc private func modeChanged() { if selected.indexOfSelectedItem > 0 { cycle.state = .off } }
    private func rebuildPages() {
        pageRows.arrangedSubviews.forEach { pageRows.removeArrangedSubview($0); $0.removeFromSuperview() }
        for (position, id) in pageOrder.enumerated() {
            let check = pageChecks[id]; check.removeFromSuperview()
            let up = NSButton(title: "↑", target: self, action: #selector(movePage)); up.tag = id * 2; up.isEnabled = position > 0
            let down = NSButton(title: "↓", target: self, action: #selector(movePage)); down.tag = id * 2 + 1; down.isEnabled = position < 7
            up.setAccessibilityLabel("上移" + check.title); down.setAccessibilityLabel("下移" + check.title)
            let row = NSStackView(views: [check, up, down]); row.spacing = 12; row.distribution = .fill
            pageRows.addArrangedSubview(row); row.widthAnchor.constraint(equalTo: pageRows.widthAnchor).isActive = true
        }
    }
    @objc private func movePage(_ sender: NSButton) {
        guard let position = pageOrder.firstIndex(of: sender.tag / 2) else { return }
        let target = position + (sender.tag % 2 == 0 ? -1 : 1)
        guard (0..<8).contains(target) else { return }
        pageOrder.swapAt(position, target); rebuildPages()
    }
    private func value() throws -> MacTab5DisplaySettings {
        guard let light = Int(brightness.stringValue), let sound = Int(volume.stringValue) else { throw MacTab5Error.invalidSettings }
        var value = current
        value.brightness = light; value.volume = sound; value.muted = muted.state == .on
        value.cycle = cycle.state == .on; value.alerts = alerts.state == .on
        value.selected = selected.indexOfSelectedItem - 1
        value.interval = [10, 15, 30, 60][interval.indexOfSelectedItem]
        value.saverMinutes = [0, 1, 5, 10, 30, 60][saver.indexOfSelectedItem]
        value.enabled = pageChecks.map { $0.state == .on ? 1 : 0 }; value.order = pageOrder
        try value.validate(); return value
    }
}
