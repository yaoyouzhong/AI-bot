import AppKit

enum MacTab5UIValidation {
    // Fixture-only native rendering: no AppDelegate, listeners, device connection,
    // Keychain, Apple Events, microphone, Screen Recording or Codex connection.
    static func run(_ output: URL) throws {
        _ = NSApplication.shared
        NSApp.setActivationPolicy(.prohibited)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: false)
        let suite = "AI-bot-ui-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-ui-" + UUID().uuidString)
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { false }, call: { _, _ in [:] })
        let computer = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in false }))
        let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { _, _ in }, feed: { _ in }, finish: {}, cancel: {}))
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
        try validateVoiceSource(service: service, defaults: defaults, output: output)
        try validateDoubaoDraft()
        try validatePinsAndCancel(root: root, codex: codex, service: service, output: output)
        let windows: [(String, NSWindowController)] = [
            ("device-data",deviceForm),
            ("esp-display",MacESPDisplayWindow(policy:.init(selectedMode:"auto",cycleEnabled:true,intervalSeconds:15,pages:["codex","domestic_kimi","weather"]),apply:{ _ in },brightness:{ _ in true },read:{ $0(.success(70)) })),
            ("service-status",MacBridgeStatusWindow(capture:{ .init(devices:[.init(kind:"tab5",name:"测试 TAB5",enabled:true,state:"未连接",transport:"—",firmware:"未知",detail:"等待测试设备")],data:[["天气","旧缓存","10-22 12:30"],["Codex 额度","数据可用","10-22 12:31"]],listener:"LAN 测试") },reconnect:{ _,done in done(.success(())) })),
            ("connection", MacTab5ToolsWindow(service:service,usb:usb)), ("birthdays", MacBirthdayWindow(store: service.birthdays)), ("agenda", MacAgendaWindow(store: service.agenda)),
            ("task-pins", MacTaskPinsWindow(codex: codex, store: MacTaskPins(directory: root), permissions: {})),
            ("computer-control", MacComputerWindow(service: service, music: {})), ("unlock-settings",MacUnlockSettingsWindow(service:service)), ("unlock-test",MacUnlockConnectionWindow()), ("voice", MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: { voiceFixture() })),
            ("stocks", MacStocksWindow(didSave: {})), ("quota-history", MacQuotaHistoryWindow(store: MacDataStore(defaults: defaults, quotaHistory: MacQuotaHistory(file: root.appendingPathComponent("history.json"))))),
            ("gallery-browser", MacGalleryBrowser(gallery: service.gallery, importPack: {})), ("configuration", MacConfigurationWindow(didRestore: {})),
            ("notifications", MacNotificationWindow(service: MacNotifications(directory: root))), ("runtime", MacRuntimeWindow(file:.init("runtime-health.json",directory:root,initial:{ .init() },validate:{ try $0.validate() }),capture:{ "CPU：采样正常\n内存：采样正常\nVPN / PAC：等待网络采样" },reload:{},deviceCapture:{ "本段收音设备：TAB5 内置麦克风 · 豆包输入法\n本机分段计时：本地启动 390 ms · 收音就绪应答 610 ms · 首段结果（含说话等待） 3280 ms · 停止至最终识别 780 ms · 最终结果至响应生成 4 ms\nTAB5 音频：已接收 25 块 · 5.0 秒 · 最近电平 36% · 峰值 82%\n最近请求：USB · 查询 · 200\n\nBLE：当前没有语音请求" })), ("updates", MacUpdateWindow(tab5: service, upgradeTools: {}, espTools: {})), ("features", MacTab5FeaturesWindow(service: service)),
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
        print("MAC_TAB5_NATIVE_UI_RENDER_OK device center and all settings fixtures; tabs, draft, offscreen validation/cancel, minimum size and both appearances; no hardware operation or permission requested")
    }

    private static func validateDoubaoDraft() throws {
        let hidden = MacDoubaoDraftWindow.make(showsEditor:false)
        let editor = NSTextView(frame:NSRect(x:0,y:0,width:620,height:460)); hidden.contentView = editor
        defer { hidden.close() }
        guard hidden.styleMask == .borderless, hidden.alphaValue == 0, !hidden.isOpaque,
              !hidden.hasShadow, hidden.ignoresMouseEvents, hidden.isExcludedFromWindowsMenu,
              hidden.canBecomeKey, !hidden.canBecomeMain, hidden.makeFirstResponder(editor),
              hidden.firstResponder === editor else { throw MacTab5LocalError.invalid("Hidden Doubao draft cannot accept native text input") }
        editor.insertText("隔离草稿",replacementRange:NSRange(location:0,length:0))
        guard editor.string == "隔离草稿" else { throw MacTab5Error.invalidPacket }
        let setup = MacDoubaoDraftWindow.make(showsEditor:true)
        defer { setup.close() }
        guard setup.styleMask.contains(.titled), setup.alphaValue == 1, !setup.ignoresMouseEvents else {
            throw MacTab5LocalError.invalid("Manual Doubao test editor must remain visible and interactive")
        }
        print("MAC_DOUBAO_DRAFT_UI_OK transparent borderless native editor; visible manual test; no microphone or shortcut")
    }

    private static func voiceFixture() -> MacVoiceEnvironment {
        .init(inputs: [.init(id: 1, uid: "fixture-mic", name: "Mac 内置麦克风")], defaultInput: 1,
              doubaoInstalled: true, doubaoEnabled: true, doubaoRunning: true, cable: true,
              speechPermission: true, microphonePermission: true, localRecognition: true)
    }
    private static func pump(until done: () -> Bool) throws {
        let deadline = ProcessInfo.processInfo.systemUptime + 5
        while !done(), ProcessInfo.processInfo.systemUptime < deadline { RunLoop.current.run(until: Date().addingTimeInterval(0.01)) }
        guard done() else { throw MacTab5LocalError.invalid("Offscreen asynchronous action timed out") }
    }
    private static func validateVoiceSource(service: MacTab5Service, defaults: UserDefaults, output: URL) throws {
        let original = MacVoiceSettings.read(defaults)
        let controller = MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: { voiceFixture() })
        let controls = descendants(controller.window!.contentView!)
        let source = controls.first { $0.identifier?.rawValue == "voice-capture-source" } as! NSPopUpButton
        let input = controls.first { $0.identifier?.rawValue == "voice-mac-input" } as! NSPopUpButton
        let save = controls.compactMap { $0 as? NSButton }.first { $0.title == "保存" }!
        guard !save.isEnabled else { throw MacTab5LocalError.invalid("Voice window blocked on enumeration") }
        try pump { save.isEnabled }
        for choice in MacVoiceCaptureSource.allCases {
            source.selectItem(at: choice == .tab5 ? 1 : 2); source.sendAction(source.action!, to: source.target)
            guard MacVoiceSettings.read(defaults) == original, input.isEnabled == (choice == .mac) else { throw MacTab5LocalError.invalid("Voice selection saved before explicit Save") }
            try validateSettings(controller.window!, name: "voice-source-" + choice.rawValue, output: output)
        }
        input.selectItem(at: 1); input.sendAction(input.action!, to: input.target)
        save.performClick(nil)
        try pump { MacVoiceSettings.read(defaults).input == "fixture-mic" }
        guard MacVoiceCaptureSource.saved(in: defaults) == .mac else { throw MacTab5Error.invalidPacket }
        let stateLock = NSLock(); var env = voiceFixture()
        func environment() -> MacVoiceEnvironment { stateLock.lock(); defer { stateLock.unlock() }; return env }
        let unavailable = MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: environment)
        let fields = descendants(unavailable.window!.contentView!)
        let provider = fields.first { $0.identifier?.rawValue == "voice-speech-provider" } as! NSPopUpButton
        let pendingSave = fields.compactMap { $0 as? NSButton }.first { $0.title == "保存" }!
        try pump { pendingSave.isEnabled }
        provider.selectItem(at: 1); provider.sendAction(provider.action!, to: provider.target)
        stateLock.lock(); env.doubaoInstalled = false; stateLock.unlock()
        let before = MacVoiceSettings.read(defaults)
        pendingSave.performClick(nil); try pump { pendingSave.isEnabled }
        guard MacVoiceSettings.read(defaults) == before, provider.item(at: 1)?.isEnabled == false else { throw MacTab5LocalError.invalid("Live provider removal overwrote settings") }
        try render(unavailable.window!.contentView!, to: output.appendingPathComponent("voice-doubao-uninstalled.png"))
        unavailable.window?.close()
        stateLock.lock(); env = voiceFixture(); env.inputs = []; env.defaultInput = nil; stateLock.unlock()
        let removed = MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: environment)
        let removedFields = descendants(removed.window!.contentView!)
        let removedSave = removedFields.compactMap { $0 as? NSButton }.first { $0.title == "保存" }!
        try pump { removedSave.isEnabled }; removedSave.performClick(nil); try pump { removedSave.isEnabled }
        guard MacVoiceSettings.read(defaults) == before else { throw MacTab5LocalError.invalid("Unavailable selected microphone overwrote settings") }
        removed.window?.close()
        let cancel = MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: { voiceFixture() })
        let cancelFields = descendants(cancel.window!.contentView!)
        let changed = cancelFields.first { $0.identifier?.rawValue == "voice-speech-provider" } as! NSPopUpButton
        changed.selectItem(at: 1); changed.sendAction(changed.action!, to: changed.target)
        cancel.window?.close(); RunLoop.current.run(until: Date().addingTimeInterval(0.05))
        guard MacVoiceSettings.read(defaults) == before else { throw MacTab5LocalError.invalid("Cancel saved voice settings") }
        let release = DispatchSemaphore(value: 0)
        var inspections = 0, validationFinished = false
        let duringSave = MacVoiceSettingsWindow(service: service, defaults: defaults, inspect: {
            stateLock.lock(); inspections += 1; let current = inspections; stateLock.unlock()
            if current == 2 { _ = release.wait(timeout: .now() + 4); stateLock.lock(); validationFinished = true; stateLock.unlock() }
            return voiceFixture()
        }, confirmDiscard: { true })
        let duringFields = descendants(duringSave.window!.contentView!)
        let duringButton = duringFields.compactMap { $0 as? NSButton }.first { $0.title == "保存" }!
        try pump { duringButton.isEnabled }
        let duringProvider = duringFields.first { $0.identifier?.rawValue == "voice-speech-provider" } as! NSPopUpButton
        duringProvider.selectItem(at: 1); duringProvider.sendAction(duringProvider.action!, to: duringProvider.target)
        duringButton.performClick(nil)
        try pump { stateLock.lock(); defer { stateLock.unlock() }; return inspections >= 2 }
        duringFields.compactMap { $0 as? NSButton }.first { $0.title == "取消" }!.performClick(nil)
        release.signal()
        try pump { stateLock.lock(); defer { stateLock.unlock() }; return validationFinished }
        RunLoop.current.run(until: Date().addingTimeInterval(0.05))
        guard MacVoiceSettings.read(defaults) == before else { throw MacTab5LocalError.invalid("Late save validation wrote after Cancel") }
        print("MAC_VOICE_SETTINGS_UI_OK async load; transactional source/input; live provider/microphone removal; cancellation; fixtures only")
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
        if name == "runtime" {
            guard original.width >= window.contentMinSize.width, original.width == 800 else { throw MacTab5LocalError.invalid("Runtime artwork narrowed the diagnostic viewport") }
        }
        for index in 0..<(tabs?.pages.count ?? 1) {
            window.setContentSize(original); content.layoutSubtreeIfNeeded()
            let before = window.frame, viewport = tabs?.frame.size
            tabs?.select(index); content.layoutSubtreeIfNeeded()
            guard window.frame == before, tabs?.frame.size == viewport else { throw MacTab5LocalError.invalid("Switching settings tabs changed the viewport") }
            for (sizeName, size) in [("default", original), ("minimum", window.contentMinSize)] {
                window.setContentSize(size); content.layoutSubtreeIfNeeded()
                if name == "runtime", abs(content.frame.width - size.width) > 1 { throw MacTab5LocalError.invalid("Runtime diagnostics could not retain requested width") }
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

    private static func validatePinsAndCancel(root: URL, codex: MacTab5Codex, service: MacTab5Service, output: URL) throws {
        let ids = (0..<34).map { _ in UUID().uuidString.lowercased() }, hidden = UUID().uuidString.lowercased()
        let model = MacTab5Codex(store: .init(root: root.appendingPathComponent("pin-catalog"), key: { Data(repeating: 7, count: 32) }), enabled: { true }, call: { method, _ in
            method == "thread/list" ? ["data": ids.enumerated().map { ["id": $0.element, "name": String(repeating: "需要完整阅读的长标题与跨平台验证任务", count: 5) + String($0.offset), "cwd": "/fixture"] }] : [:]
        })
        model.refresh()
        let store = MacTaskPins(directory: root.appendingPathComponent("pin-editor")); try store.save([hidden, ids[1]])
        let pins = MacTaskPinsWindow(codex: model, store: store, permissions: {})
        let content = pins.window!.contentView!, controls = descendants(content)
        let count = controls.first { $0.identifier?.rawValue == "task-pin-count" } as! NSTextField
        let choices = controls.compactMap { $0 as? NSButton }.filter { $0.identifier?.rawValue.hasPrefix("task-pin-") == true }
        guard count.stringValue == "已选 2 / 32", choices.count == 35 else { throw MacTab5LocalError.invalid("Pin count or hidden selections missing") }
        for choice in choices where choice.state == .off { choice.performClick(nil) }
        guard count.stringValue == "已选 32 / 32", try store.read() == [hidden, ids[1]] else { throw MacTab5LocalError.invalid("Pin limit or transactional selection failed") }
        guard controls.compactMap({ $0 as? NSTextField }).contains(where: { $0.identifier?.rawValue == "task-pin-title" && $0.stringValue.count > 100 }),
              model.taskCatalog().allSatisfy({ ($0["title"] as? String ?? "").count <= 80 }) else { throw MacTab5LocalError.invalid("Local full titles changed the wire catalog") }
        let draft = pins.window!.delegate as! MacSettingsDraft
        var confirmations = 0, closed = false
        draft.confirmDiscard = { confirmations += 1; return false }
        let observer = NotificationCenter.default.addObserver(forName: NSWindow.willCloseNotification, object: pins.window, queue: nil) { _ in closed = true }
        let cancel = controls.compactMap { $0 as? NSButton }.first { $0.title == "取消" }!
        cancel.performClick(nil)
        guard confirmations == 1, !closed, draft.hasChanges else { throw MacTab5LocalError.invalid("Cancel bypassed unsaved-change protection") }
        try validateSettings(pins.window!, name: "task-pins-long", output: output)
        for label in controls.compactMap({ $0 as? NSTextField }).filter({ $0.identifier?.rawValue == "task-pin-title" }) {
            guard label.lineBreakMode == .byWordWrapping, label.frame.width > 400, label.frame.height > 20 else { throw MacTab5LocalError.invalid("Pin titles not wrapped to viewport") }
        }
        draft.confirmDiscard = { true }; cancel.performClick(nil)
        NotificationCenter.default.removeObserver(observer)
        guard closed, try store.read() == [hidden, ids[1]] else { throw MacTab5LocalError.invalid("Cancel saved pin changes") }
        let savedPins = MacTaskPinsWindow(codex: model, store: store, permissions: {})
        let savedControls = descendants(savedPins.window!.contentView!)
        savedControls.compactMap { $0 as? NSButton }.first { $0.identifier?.rawValue == "task-pin-" + ids[0] }!.performClick(nil)
        savedControls.compactMap { $0 as? NSButton }.first { $0.title == "保存" }!.performClick(nil)
        guard try store.read() == [hidden, ids[1], ids[0]] else { throw MacTab5LocalError.invalid("Saving dropped hidden pins or original order") }
        // Real computer-control Cancel, with an injected answer to its native guard.
        let computer = MacComputerWindow(service: service, music: {})
        let computerFields = descendants(computer.window!.contentView!)
        let editable = computerFields.compactMap { $0 as? NSTextField }.first { $0.isEditable }!
        let before = try service.computer.launchers.load(); editable.stringValue = "unsaved fixture"
        let computerDraft = computer.window!.delegate as! MacSettingsDraft
        computerDraft.confirmDiscard = { true }
        computerFields.compactMap { $0 as? NSButton }.first { $0.title == "取消" }!.performClick(nil)
        guard try service.computer.launchers.load() == before else { throw MacTab5LocalError.invalid("Computer Cancel changed stored launchers") }
        print("MAC_SETTINGS_TRANSACTION_UI_OK pin wrapping/count/limit/hidden-order/save/cancel; computer Cancel; no foreground dialogs")
    }

    private static func validateModal() throws {
        let form = MacBridgeSettingsForm("布局验证", subtitle: "仅使用测试数据。", height: 380)
        let content = form.window!.contentView!
        let save = descendants(content).compactMap { $0 as? NSButton }.first { $0.title == "保存" }!
        let cancel = descendants(content).compactMap { $0 as? NSButton }.first { $0.title == "取消" }!
        form.prepare(validating: { "请修正输入" }); save.performClick(nil)
        guard form.status.stringValue == "请修正输入", save.keyEquivalent == "\r", cancel.keyEquivalent == "\u{1b}" else { throw MacTab5Error.invalidPacket }
        var closed = false
        let observer = NotificationCenter.default.addObserver(forName: NSWindow.willCloseNotification, object: form.window, queue: nil) { _ in closed = true }
        cancel.performClick(nil); NotificationCenter.default.removeObserver(observer)
        guard closed else { throw MacTab5LocalError.invalid("Non-modal Cancel did not close") }
        let valid = MacBridgeSettingsForm("保存验证", subtitle: "仅使用测试数据。", height: 380)
        valid.prepare(); var accepted = false
        let saved = NotificationCenter.default.addObserver(forName: NSWindow.willCloseNotification, object: valid.window, queue: nil) { _ in accepted = true }
        descendants(valid.window!.contentView!).compactMap { $0 as? NSButton }.first { $0.title == "保存" }!.performClick(nil)
        NotificationCenter.default.removeObserver(saved)
        guard accepted else { throw MacTab5LocalError.invalid("Non-modal Save did not close") }
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
