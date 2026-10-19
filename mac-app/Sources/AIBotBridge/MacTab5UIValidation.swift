import AppKit

enum MacTab5UIValidation {
    // Fixture-only native rendering: no AppDelegate, listeners, device scanner,
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
        let service = MacTab5Service(port: 8765, host: { "192.168.1.10" }, gallery: .init(directory: root), computer: computer, codex: codex, voice: voice, load: { nil }, save: { _ in })
        let usb = MacTab5USB(service: service)
        let windows: [(String, NSWindowController)] = [("connection", MacTab5Window(service: service, usb: usb)), ("features", MacTab5FeaturesWindow(service: service)), ("resources", MacTab5ToolsWindow(service: service, usb: usb)), ("first-install", MacTab5InstallWindow())]
        for (name, controller) in windows {
            guard let window = controller.window, let content = window.contentView else { throw MacTab5Error.unavailable }
            window.appearance = NSAppearance(named: .aqua); window.displayIfNeeded(); content.layoutSubtreeIfNeeded()
            try render(content, to: output.appendingPathComponent(name + ".png"))
            if let scroll = content.subviews.compactMap({ $0 as? NSScrollView }).first, let document = scroll.documentView { try render(document, to: output.appendingPathComponent(name + "-full.png")) }
            window.close()
        }
        let settings = MacTab5SettingsForm.fixture()
        try render(settings.1, to: output.appendingPathComponent("display-settings.png")); withExtendedLifetime(settings.0) {}
        print("MAC_TAB5_NATIVE_UI_RENDER_OK four fixture windows and display settings; no hardware, live conversation or permission accessed")
    }
    private static func render(_ view: NSView, to url: URL) throws {
        view.layoutSubtreeIfNeeded()
        guard view.bounds.width >= 100, view.bounds.height >= 100, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw MacTab5Error.unavailable }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        // AppKit view caches retain transparency outside controls. Composite the
        // native drawing over the window's semantic background for inspection.
        let drawing = NSImage(size: view.bounds.size); drawing.addRepresentation(bitmap)
        let opaque = NSImage(size: view.bounds.size)
        opaque.lockFocus()
        NSAppearance(named: .aqua)!.performAsCurrentDrawingAppearance { NSColor.windowBackgroundColor.setFill(); NSRect(origin: .zero, size: view.bounds.size).fill(); drawing.draw(in: NSRect(origin: .zero, size: view.bounds.size)) }
        opaque.unlockFocus()
        guard let tiff = opaque.tiffRepresentation, let composed = NSBitmapImageRep(data: tiff), let bytes = composed.representation(using: .png, properties: [:]), bytes.count > 1000 else { throw MacTab5Error.unavailable }
        try bytes.write(to: url, options: .withoutOverwriting)
    }
}
