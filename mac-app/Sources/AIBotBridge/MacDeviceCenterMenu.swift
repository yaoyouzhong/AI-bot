import AppKit

/// Keep the tray contract aligned with Windows DeviceCenterMenu.
enum MacDeviceCenterMenu {
    static func build(devices: [MacRegisteredDevice], deviceAction: @escaping (MacRegisteredDevice, String) -> Void, common: @escaping (String) -> Void) -> NSMenu {
        let menu = NSMenu(); menu.autoenablesItems = false
        menu.addItem(MacDeviceMenuItem("设备中心", key: "devices") { common("devices") })
        let previews = devices.filter(\.enabled).sorted { $0.kind < $1.kind }
        if previews.count == 1, let device = previews.first {
            menu.addItem(MacDeviceMenuItem("设备预览", key: "mirror") { deviceAction(device, "mirror") })
        } else if previews.count > 1 {
            let group = NSMenuItem(title:"设备预览",action:nil,keyEquivalent:"")
            group.identifier = .init("mirror")
            let choices = NSMenu(); choices.autoenablesItems = false
            for device in previews {
                choices.addItem(MacDeviceMenuItem(device.name,key:"preview:"+device.id) { deviceAction(device,"mirror") })
            }
            group.submenu = choices; menu.addItem(group)
        }
        if !devices.isEmpty { menu.addItem(.separator()) }
        for device in devices.sorted(by: { $0.kind < $1.kind }) {
            let name = device.name.count > 24 ? String(device.name.prefix(23)) + "…" : device.name
            let group = NSMenuItem(title: name + (device.enabled ? "" : "（已停用）"), action: nil, keyEquivalent: "")
            group.identifier = .init("device:" + device.id); group.isEnabled = device.enabled
            let submenu = NSMenu(); submenu.autoenablesItems = false
            let actions = device.kind == "esp8266"
                ? [("cycle", "显示设置"), ("appearance", "外观设置"), ("data", "数据设置"), ("legacy-settings", "连接设置")]
                : [("gallery-browser", "屏保图库"), ("voice", "语音设置"), ("birthday-settings", "日历生日"), ("computer-control", "电脑控制"), ("data", "数据设置"), ("tab5", "连接升级")]
            for (key, label) in actions {
                let entry = MacDeviceMenuItem(label, key: key) { deviceAction(device, key) }
                entry.isEnabled = device.enabled; submenu.addItem(entry)
            }
            group.submenu = submenu; menu.addItem(group)
        }
        menu.addItem(.separator())
        menu.addItem(MacDeviceMenuItem("退出", key: "exit", shortcut: "q") { common("exit") })
        return menu
    }
}

private final class MacDeviceMenuItem: NSMenuItem {
    private let handler: () -> Void
    init(_ title: String, key: String, shortcut: String = "", action: @escaping () -> Void) {
        handler = action
        super.init(title: title, action: #selector(invoke), keyEquivalent: shortcut)
        identifier = .init(key); target = self
    }
    required init(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc private func invoke() {
        guard isEnabled else { return }
        menu?.cancelTrackingWithoutAnimation()
        DispatchQueue.main.async(execute: handler)
    }
}
