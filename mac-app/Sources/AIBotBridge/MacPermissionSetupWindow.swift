import AppKit
import ApplicationServices
import CoreLocation

enum MacPermissionSetup {
    static let shownKey = "first_system_permissions_shown"

    enum State {
        case unused, request, settings, allowed

        var needsPermission: Bool { self == .request || self == .settings }
        var caption: String {
            switch self {
            case .unused: return "当前未使用"
            case .request, .settings: return "待授权"
            case .allowed: return "已授权"
            }
        }
    }

    static func location(enabled: Bool, status: CLAuthorizationStatus) -> State {
        guard enabled else { return .unused }
        switch status {
        case .authorizedAlways, .authorizedWhenInUse: return .allowed
        case .notDetermined: return .request
        default: return .settings
        }
    }

    static func accessibility(enabled: Bool, trusted: Bool) -> State {
        guard enabled else { return .unused }
        return trusted ? .allowed : .settings
    }

    static func shouldOffer(_ states: [State], defaults: UserDefaults = .standard) -> Bool {
        !defaults.bool(forKey: shownKey) && states.contains(where: \.needsPermission)
    }

    static func openLocationSettings() {
        openPrivacy("Privacy_LocationServices")
    }

    static func requestAccessibility() {
        guard !AXIsProcessTrusted() else { return }
        _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
        openPrivacy("Privacy_Accessibility")
    }

    private static func openPrivacy(_ section: String) {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?" + section) {
            NSWorkspace.shared.open(url)
        }
    }
}

final class MacPermissionSetupWindow: NSWindowController, NSWindowDelegate {
    private let location: () -> MacPermissionSetup.State
    private let accessibility: () -> MacPermissionSetup.State
    private let requestLocation: (MacPermissionSetup.State) -> Void
    private let requestAccessibility: () -> Void
    private let page = MacBridgeSettingsPage()
    private let locationState = MacBridgeSettingsStyle.note("")
    private let accessibilityState = MacBridgeSettingsStyle.note("")
    private let locationButton = NSButton(title: "允许天气定位…", target: nil, action: nil)
    private let accessibilityButton = NSButton(title: "打开辅助功能设置…", target: nil, action: nil)
    private let closeButton = NSButton(title: "稍后完成", target: nil, action: nil)
    private var timer: Timer?

    init(location: @escaping () -> MacPermissionSetup.State, accessibility: @escaping () -> MacPermissionSetup.State,
         requestLocation: @escaping (MacPermissionSetup.State) -> Void, requestAccessibility: @escaping () -> Void) {
        self.location = location; self.accessibility = accessibility
        self.requestLocation = requestLocation; self.requestAccessibility = requestAccessibility
        let window = MacBridgeSettingsStyle.window("系统授权", height: 510, minimumHeight: 470)
        super.init(window: window)
        window.delegate = self
        window.contentView = MacBridgeSettingsShell(body: page, actions: [closeButton])
        page.heading("系统授权", subtitle: "请在 macOS 中确认。授权后对应功能立即可用，无需再打开应用内的开关。")
        addPermission("天气定位", detail: "使用大致位置更新本地天气。也可以在天气设置中使用手动地点。",
                      state: locationState, button: locationButton)
        addPermission("Codex 直达与电脑控制", detail: "允许 AI-bot Bridge 使用辅助功能，以便从 TAB5 定位和切换 Codex 会话，或锁定 Mac。",
                      state: accessibilityState, button: accessibilityButton)
        locationButton.target = self; locationButton.action = #selector(allowLocation)
        accessibilityButton.target = self; accessibilityButton.action = #selector(allowAccessibility)
        for button in [locationButton, accessibilityButton, closeButton] {
            button.controlSize = .large; button.bezelStyle = .rounded
            button.heightAnchor.constraint(greaterThanOrEqualToConstant: 32).isActive = true
        }
        closeButton.target = self; closeButton.action = #selector(dismiss)
        closeButton.keyEquivalent = "\u{1b}"
        refresh()
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate() }

    private func addPermission(_ title: String, detail: String, state: NSTextField, button: NSButton) {
        page.section(title)
        let description = MacBridgeSettingsStyle.note(detail)
        page.add(description); page.stack.setCustomSpacing(6, after: description)
        page.add(state); page.stack.setCustomSpacing(8, after: state)
        page.add(button)
    }

    func open() {
        refresh()
        showWindow(nil); window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        if timer == nil {
            timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.refresh() }
        }
    }

    func windowWillClose(_ notification: Notification) { timer?.invalidate(); timer = nil }

    func refresh() {
        let weather = location(), codex = accessibility()
        locationState.stringValue = weather.caption
        accessibilityState.stringValue = codex.caption
        locationButton.title = weather == .settings ? "打开定位服务设置…" : "允许天气定位…"
        locationButton.isEnabled = weather.needsPermission
        accessibilityButton.isEnabled = codex.needsPermission
        locationButton.isHidden = !locationButton.isEnabled
        accessibilityButton.isHidden = !accessibilityButton.isEnabled
        closeButton.title = weather.needsPermission || codex.needsPermission ? "稍后完成" : "完成"
    }

    @objc private func allowLocation() {
        let current = location()
        if current.needsPermission { requestLocation(current) }
    }
    @objc private func allowAccessibility() {
        if accessibility().needsPermission { requestAccessibility() }
    }
    @objc private func dismiss() { close() }
}
