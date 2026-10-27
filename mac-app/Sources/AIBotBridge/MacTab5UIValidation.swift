import AppKit

enum MacTab5UIValidation {
    // Fixture-only native rendering: no AppDelegate, listeners, device connection,
    // Keychain, Apple Events, microphone, Screen Recording or Codex connection.
    static func run(_ output: URL) throws {
        _ = NSApplication.shared
        NSApp.setActivationPolicy(.accessory)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: false)
        let defaults = UserDefaults(suiteName: "AI-bot-ui-" + UUID().uuidString)!
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-ui-" + UUID().uuidString)
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { false }, call: { _, _ in [:] })
        let computer = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in false }))
        let voice = MacTab5Voice(enabled: { false }, backend: .init(start: { _, _ in }, feed: { _ in }, finish: {}, cancel: {}))
        let service = MacTab5Service(port: 8765, host: { "192.168.1.10" }, gallery: .init(directory: root), computer: computer, codex: codex, voice: voice, birthdays: MacBirthdays(directory: root), agenda: MacAgenda(directory: root), load: { nil }, save: { _ in })
        let usb = MacTab5USB(service: service)
        service.usbState("USB 未确认，等待重连（设备发现：串口不可用）")
        let registry = MacDeviceRegistry(directory:root); try registry.add("tab5"); try registry.add("esp8266")
        var invoked: [String] = []
        let center = MacTab5Window(service: service, usb: usb,
            configureWeather: { invoked.append("weather") }, configureStocks: { invoked.append("stocks") }, configureQuotas: { invoked.append("quotas") },
            dataStatus: { "天气：等待更新\n系统监控：已启用\n网络：等待采样" },
            bridgeAction: { invoked.append(String(describing: $0)) },registry:registry)
        let tabMenu = center.managementMenu(for:"tab5"), espMenu = center.managementMenu(for:"esp8266")
        guard tabMenu.items.map({ $0.isSeparatorItem ? "|":$0.title }) == ["修改名称","停用设备","|","移除设备…"],
              espMenu.items.map({ $0.isSeparatorItem ? "|":$0.title }) == ["修改名称","停用设备","|","固件升级","设备信息","连接诊断","重置网络","|","移除设备…"],
              espMenu.items[3].isEnabled,espMenu.items[4...6].allSatisfy({ !$0.isEnabled }) else { throw MacTab5LocalError.invalid("Management menu does not match Windows") }
        tabMenu.performActionForItem(at:1)
        guard !registry.enabled("tab5"),center.managementMenu(for:"tab5").items[1].title == "启用设备",
              let galleryCard = descendants(center.window!.contentView!).compactMap({ $0 as? NSButton }).first(where:{ $0.identifier?.rawValue == "gallery-browser" }),!galleryCard.isEnabled else { throw MacTab5LocalError.invalid("Disabled device actions remained enabled") }
        center.managementMenu(for:"tab5").performActionForItem(at:1)
        guard registry.enabled("tab5"),galleryCard.isEnabled else { throw MacTab5Error.invalidPacket }
        try validateDeviceCenter(center, output: output)
        guard invoked == ["weather", "stocks", "quotas", "quotaHistory", "status", "updates", "configuration", "notifications", "runtime", "about", "cycle", "appearance", "espData", "espConnection"] else { throw MacTab5Error.invalidPacket }
        let store = MacDataStore(defaults: defaults)
        try MacModelAccountValidation.runUI(store:store,defaults:defaults)
        let data = MacDataSettingsForm(.init(city: "示例城市", latitude: nil, longitude: nil, symbols: ["sh000001", "usAAPL"]))
        let deviceForm = MacBridgeSettingsForm("数据设置",subtitle:"隔离测试设备")
        deviceForm.window?.contentMinSize.height = 520
        let dataControls = MacDeviceDataControls(page:deviceForm.page,device:.init(kind:"tab5",name:"测试设备"))
        try validateEditorBehaviors(root:root)
        let windows: [(String, NSWindowController)] = [
            ("device-data",deviceForm),
            ("esp-display",MacESPDisplayWindow(policy:.init(selectedMode:"auto",cycleEnabled:true,intervalSeconds:15,pages:["codex","domestic_kimi","weather"]),apply:{ _ in },brightness:{ _ in true },read:{ $0(.success(70)) })),
            ("service-status",MacBridgeStatusWindow(capture:{ .init(devices:[.init(kind:"tab5",name:"测试 TAB5",enabled:true,state:"未连接",transport:"—",firmware:"未知",detail:"等待测试设备")],data:[["天气","旧缓存","10-22 12:30"],["Codex 额度","数据可用","10-22 12:31"]],listener:"LAN 测试") },reconnect:{ _,done in done(.success(())) })),
            ("connection", MacTab5ToolsWindow(service:service,usb:usb)), ("birthdays", MacBirthdayWindow(store: service.birthdays)), ("agenda", MacAgendaWindow(store: service.agenda)),
            ("task-pins", MacTaskPinsWindow(codex: codex, store: MacTaskPins(directory: root), permissions: {})),
            ("computer-control", MacComputerWindow(service: service, music: {})), ("unlock-settings",MacUnlockSettingsWindow(service:service)), ("unlock-test",MacUnlockConnectionWindow()), ("voice", MacTab5FeaturesWindow(service: service, focusSection: .voice)),
            ("stocks", MacStocksWindow(didSave: {})), ("quota-history", MacQuotaHistoryWindow(store: MacDataStore(defaults: defaults, quotaHistory: MacQuotaHistory(file: root.appendingPathComponent("history.json"))))),
            ("gallery-browser", MacGalleryBrowser(gallery: service.gallery, importPack: {})), ("configuration", MacConfigurationWindow(didRestore: {})),
            ("notifications", MacNotificationWindow(service: MacNotifications(directory: root))), ("runtime", MacRuntimeWindow(file:.init("runtime-health.json",directory:root,initial:{ .init() },validate:{ try $0.validate() }),capture:{ "CPU：采样正常\n内存：采样正常\nVPN / PAC：等待网络采样" },reload:{})), ("updates", MacUpdateWindow(tab5: service, upgradeTools: {}, espTools: {})), ("features", MacTab5FeaturesWindow(service: service)),
            ("gallery-manager", MacGalleryManager(gallery:service.gallery,browse:{})), ("first-install", MacTab5InstallWindow()),
            ("esp8266-firmware", MacFirmwareFlashWindow(begin: { false }, suspend: {}, finish: { _ in })),
            ("pet-gallery", MacPetGalleryWindow(selectPage: { _ in })),
            ("domestic-quotas", MacDomesticQuotaWindow(service: .init(store: store), store: store, defaults:defaults, loadPages:false)), ("data-settings", data.form)]
        for (name, controller) in windows {
            guard let window = controller.window, let content = window.contentView else { throw MacTab5Error.unavailable }
            window.appearance = NSAppearance(named: .aqua); window.displayIfNeeded(); content.layoutSubtreeIfNeeded()
            try render(content, to: output.appendingPathComponent(name + ".png"))
            if name != "gallery-browser" { try validateSettings(window, name: name, output: output) }
            window.close()
        }
        withExtendedLifetime(dataControls) {}
        let settings = MacTab5SettingsForm.fixture()
        try render(settings.1, to: output.appendingPathComponent("display-settings.png"))
        if let window = settings.1.window { try validateSettings(window, name: "display-settings", output: output) }
        withExtendedLifetime(settings.0) {}
        // Switching tabs must preserve the unsaved draft; QWeather adds two fields.
        data.city.stringValue = "未保存的城市"
        data.provider.selectItem(at: 1); data.provider.sendAction(data.provider.action!, to: data.provider.target)
        if let window = data.form.window {
            try validateSettings(window, name: "data-qweather", output: output)
            guard data.city.stringValue == "未保存的城市" else { throw MacTab5Error.invalidPacket }
        }
        try validateGallery(output:output, root:root.appendingPathComponent("gallery-fixture"))
        try validateModal()
        print("MAC_TAB5_NATIVE_UI_RENDER_OK device center and all settings fixtures; tabs, draft, modal validation/cancel, minimum size and both appearances; no hardware operation or permission requested")
    }

    private static func validateEditorBehaviors(root:URL) throws {
        let store = MacBirthdays(directory:root.appendingPathComponent("birthday-editor")), window = MacBirthdayWindow(store:store)
        guard let content = window.window?.contentView else { throw MacTab5Error.invalidPacket }
        let all = descendants(content)
        let name = all.first { $0.identifier?.rawValue == "birthday-name" } as! NSTextField
        let calendar = all.first { $0.identifier?.rawValue == "birthday-calendar" } as! NSPopUpButton
        let remind = all.first { $0.identifier?.rawValue == "birthday-remind" } as! NSPopUpButton
        let leap = all.compactMap { $0 as? NSButton }.first { $0.title.hasPrefix("闰月") }!
        guard remind.indexOfSelectedItem == 3,!leap.isEnabled else { throw MacTab5Error.invalidPacket }
        calendar.selectItem(at:1); calendar.sendAction(calendar.action!,to:calendar.target); leap.state = .on
        calendar.selectItem(at:0); calendar.sendAction(calendar.action!,to:calendar.target)
        guard !leap.isEnabled,leap.state == .off else { throw MacTab5Error.invalidPacket }
        name.stringValue = "重复应用验证"
        let apply = all.compactMap { $0 as? NSButton }.first { $0.title == "添加到列表" }!
        apply.performClick(nil); apply.performClick(nil)
        let table = all.compactMap { $0 as? MacSettingsTable }.first!
        guard table.rows.count == 1,try store.file.read().isEmpty else { throw MacTab5Error.invalidPacket }
        window.close()
        let page = MacBridgeSettingsPage(), controls = MacDeviceDataControls(page:page,device:.init(kind:"tab5",name:"测试"))
        let quota = controls.sources.first { $0.0 == "quotas" }!.1, before = controls.providers.map { $0.1.state }
        quota.performClick(nil)
        guard controls.providers.allSatisfy({ !$0.1.isEnabled }),before == controls.providers.map({ $0.1.state }) else { throw MacTab5Error.invalidPacket }
        quota.performClick(nil); guard controls.providers.allSatisfy({ $0.1.isEnabled }) else { throw MacTab5Error.invalidPacket }
    }

    private static func validateGallery(output:URL,root:URL) throws {
        let gallery = try MacGalleryConnectionValidation.fixture(root)
        let browser = MacGalleryBrowser(gallery:gallery,importPack:{})
        guard let window = browser.window else { throw MacTab5Error.invalidPacket }
        window.contentView?.layoutSubtreeIfNeeded()
        let deadline = Date().addingTimeInterval(5)
        while browser.stage.comparison.canvas.image == nil && Date() < deadline { RunLoop.main.run(until:Date().addingTimeInterval(0.02)) }
        guard browser.stage.comparison.canvas.image != nil else { throw MacTab5LocalError.invalid("Gallery image load fixture") }
        let work = try gallery.browserWorks("painting")[0], data = try gallery.browserPage(work,frame:0)
        let page = MacGalleryPage(work:work,frame:0,image:NSImage(data:data.0)!,hd:false,guide:data.1)
        let fullscreen = MacGalleryFullscreen(page:page,screen:nil,navigate:{ _ in })
        guard let full = fullscreen.window else { throw MacTab5Error.invalidPacket }
        for (label,w,size) in [("gallery-loaded",window,NSSize(width:1020,height:720)),("gallery-minimum",window,NSSize(width:960,height:580)),("gallery-fullscreen",full,NSSize(width:1440,height:900)),("gallery-fullscreen-narrow",full,NSSize(width:860,height:650))] {
            w.setContentSize(size)
            let stage = w === window ? browser.stage:fullscreen.stage
            for reading in [false,true] {
                stage.comparison.reading = reading
                for (name,appearance) in [("light",NSAppearance.Name.aqua),("dark",.darkAqua)] {
                    w.appearance = NSAppearance(named:appearance); w.contentView?.layoutSubtreeIfNeeded()
                    guard stage.comparison.canvas.bounds.height > 100,stage.comparison.canvas.bounds.width > 100 else { throw MacTab5LocalError.invalid("Gallery image area collapsed") }
                    if reading { guard !stage.comparison.reader.isHidden, !stage.comparison.canvas.frame.intersects(stage.comparison.reader.frame) else { throw MacTab5Error.invalidPacket } }
                    try render(w.contentView!,to:output.appendingPathComponent("\(label)-\(reading ? "reading":"image")-\(name).png"))
                }
            }
        }
        let canvas = fullscreen.stage.comparison.canvas
        canvas.zoom(to:4); canvas.move(by:NSPoint(x:100000,y:-100000))
        guard canvas.zoom == 4,canvas.imageBounds.minX <= 1,canvas.imageBounds.maxX >= canvas.bounds.maxX-1 else { throw MacTab5Error.invalidPacket }
        canvas.resetView(); guard canvas.zoom == 1,canvas.pan == .zero else { throw MacTab5Error.invalidPacket }
        func key(_ code:UInt16,_ characters:String) -> NSEvent { NSEvent.keyEvent(with:.keyDown,location:.zero,modifierFlags:[],timestamp:0,windowNumber:full.windowNumber,context:nil,characters:characters,charactersIgnoringModifiers:characters,isARepeat:false,keyCode:code)! }
        fullscreen.stage.comparison.reading = false
        guard fullscreen.stage.key(key(34,"i")),fullscreen.stage.comparison.reading else { throw MacTab5Error.invalidPacket }
        guard fullscreen.stage.key(key(124,"")),fullscreen.stage.comparison.reader.selectedIndex == 1 else { throw MacTab5Error.invalidPacket }
        guard fullscreen.stage.key(key(53,"")),!fullscreen.stage.comparison.reading else { throw MacTab5Error.invalidPacket }
        window.close(); full.close()
    }

    private static func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }

    private static func validateSettings(_ window: NSWindow, name: String, output: URL) throws {
        guard let content = window.contentView,
              let footer = descendants(content).first(where: { $0.identifier?.rawValue == "settings-footer" }) else { throw MacTab5Error.invalidPacket }
        let tabs = descendants(content).compactMap { $0 as? MacBridgeSettingsTabs }.first
        let original = content.frame.size
        for index in 0..<(tabs?.pages.count ?? 1) {
            tabs?.select(index)
            for (sizeName, size) in [("default", original), ("minimum", window.contentMinSize)] {
                window.setContentSize(size); content.layoutSubtreeIfNeeded()
                guard footer.enclosingScrollView == nil,
                      content.bounds.contains(footer.convert(footer.bounds, to: content)) else { throw MacTab5Error.invalidPacket }
                let pages = descendants(content).compactMap { $0 as? MacBridgeSettingsPage }.filter { !$0.isHiddenOrHasHiddenAncestor }
                for page in pages {
                    page.fitDocument()
                    guard let document = page.documentView, abs(document.frame.width - page.contentSize.width) < 1,
                          page.stack.frame.minX >= 23, page.stack.frame.maxX <= document.bounds.maxX - 23 else { throw MacTab5Error.invalidPacket }
                }
                for (appearance, suffix) in [(NSAppearance.Name.aqua, "light"), (.darkAqua, "dark")] {
                    window.appearance = NSAppearance(named: appearance)
                    try render(content, to: output.appendingPathComponent("\(name)-tab\(index)-\(sizeName)-\(suffix).png"))
                }
                if sizeName == "default", let document = pages.first?.documentView {
                    window.appearance = NSAppearance(named: .aqua)
                    try render(document, to: output.appendingPathComponent(index == 0 ? name + "-full.png" : "\(name)-tab\(index)-full.png"))
                }
            }
        }
        window.setContentSize(original); tabs?.select(0)
    }

    private static func validateModal() throws {
        let form = MacBridgeSettingsForm("布局验证", subtitle: "仅使用测试数据。", height: 380)
        guard let content = form.window?.contentView,
              let save = descendants(content).compactMap({ $0 as? NSButton }).first(where: { $0.title == "保存" }),
              let cancel = descendants(content).compactMap({ $0 as? NSButton }).first(where: { $0.title == "取消" }) else { throw MacTab5Error.invalidPacket }
        var rejected = false
        DispatchQueue.main.async {
            save.performClick(nil)
            rejected = form.status.stringValue == "请修正输入" && NSApp.modalWindow === form.window
            cancel.performClick(nil)
        }
        guard !form.run(validating: { "请修正输入" }), rejected else { throw MacTab5Error.invalidPacket }
        DispatchQueue.main.async { save.performClick(nil) }
        guard form.run() else { throw MacTab5Error.invalidPacket }
        form.window?.close()
    }

    private static func validateDeviceCenter(_ controller: MacTab5Window, output: URL) throws {
        guard let window = controller.window, let content = window.contentView,
              let tabs = content.subviews.compactMap({ $0 as? NSTabView }).first,
              tabs.tabViewItems.map(\.label) == ["我的设备", "账号数据", "桥接设置"] else { throw MacTab5Error.invalidPacket }
        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }
        func click(_ id: String) throws {
            guard let button = descendants(content).compactMap({ $0 as? NSButton }).first(where: { $0.identifier?.rawValue == id }) else { throw MacTab5Error.invalidPacket }
            button.performClick(nil); content.layoutSubtreeIfNeeded()
        }
        func checkPages() throws {
            content.layoutSubtreeIfNeeded()
            for page in descendants(content).compactMap({ $0 as? MacBridgeSettingsPage }) where !page.isHiddenOrHasHiddenAncestor {
                page.fitDocument()
                guard let document = page.documentView, abs(document.frame.width - page.contentSize.width) < 1 else { throw MacTab5Error.invalidPacket }
                let cards = descendants(document).compactMap { $0 as? MacBridgeActionCard }
                for card in cards {
                    let frame = card.convert(card.bounds, to: document)
                    guard frame.width >= 200, frame.width <= 240.5, document.bounds.insetBy(dx: -1, dy: -1).contains(frame) else {
                        throw MacTab5LocalError.invalid("Card bounds: \(card.identifier?.rawValue ?? "") \(frame), document \(document.bounds)")
                    }
                }
                for (index, card) in cards.enumerated() {
                    for other in cards.dropFirst(index + 1) where card.convert(card.bounds, to: document).intersects(other.convert(other.bounds, to: document)) { throw MacTab5Error.invalidPacket }
                }
            }
        }
        for (name, size) in [("default", NSSize(width: 920, height: 620)), ("minimum", NSSize(width: 820, height: 500)), ("large", NSSize(width: 1320, height: 800))] {
            window.setContentSize(size); try checkPages()
            let grids = descendants(content).compactMap { $0 as? MacBridgeCardGrid }.filter { !$0.isHiddenOrHasHiddenAncestor }
            guard grids.first?.columns == (name == "minimum" ? 2:3) else { throw MacTab5LocalError.invalid("Card columns: \(name) = \(grids.first?.columns ?? 0), content \(content.frame.size)") }
            for (appearance, suffix) in [(NSAppearance.Name.aqua, "light"), (.darkAqua, "dark")] {
                window.appearance = NSAppearance(named: appearance)
                try render(content, to: output.appendingPathComponent("device-center-\(name)-\(suffix).png"))
            }
        }
        window.appearance = NSAppearance(named: .aqua); window.setContentSize(NSSize(width: 920, height: 620))
        for (index, name, actions) in [(1, "accounts", ["account-weather-stocks", "account-stocks", "account-quotas", "quota-trend"]),
                                       (2, "bridge", ["bridge-status", "updates", "configuration-backup", "notifications", "runtime-health", "about"])] {
            tabs.selectTabViewItem(at: index); try checkPages()
            for id in actions { try click(id) }
            try render(content, to: output.appendingPathComponent("device-center-\(name).png"))
        }
        tabs.selectTabViewItem(at: 0); try click("device-esp8266"); try checkPages()
        for id in ["cycle", "appearance", "data", "connect"] { try click("esp8266-" + id) }
        try render(content, to: output.appendingPathComponent("device-center-esp8266.png"))
        try click("device-tab5"); try checkPages()
    }
    private static func render(_ view: NSView, to url: URL) throws {
        view.layoutSubtreeIfNeeded()
        guard view.bounds.width >= 100, view.bounds.height >= 100, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw MacTab5Error.unavailable }
        let appearance = view.effectiveAppearance
        appearance.performAsCurrentDrawingAppearance { view.cacheDisplay(in: view.bounds, to: bitmap) }
        // AppKit view caches retain transparency outside controls. Composite the
        // native drawing over the window's semantic background for inspection.
        let drawing = NSImage(size: view.bounds.size); drawing.addRepresentation(bitmap)
        let opaque = NSImage(size: view.bounds.size)
        opaque.lockFocus()
        appearance.performAsCurrentDrawingAppearance { NSColor.windowBackgroundColor.setFill(); NSRect(origin: .zero, size: view.bounds.size).fill(); drawing.draw(in: NSRect(origin: .zero, size: view.bounds.size)) }
        opaque.unlockFocus()
        guard let tiff = opaque.tiffRepresentation, let composed = NSBitmapImageRep(data: tiff), let bytes = composed.representation(using: .png, properties: [:]), bytes.count > 1000 else { throw MacTab5Error.unavailable }
        try bytes.write(to: url, options: .withoutOverwriting)
    }
}
