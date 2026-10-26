import AppKit

enum MacDeviceCenterMenuValidation {
    static func run() throws -> Int {
        _ = NSApplication.shared
        var checked = 0
        func check(_ name: String, _ condition: @autoclosure () -> Bool) throws {
            guard condition() else { throw MacTab5LocalError.invalid("Tray menu check failed: " + name) }
            checked += 1
        }
        let tab = MacRegisteredDevice(kind: "tab5", name: "书桌平板")
        let screen = MacRegisteredDevice(kind: "esp8266", name: "办公室小屏")
        var disabled = tab; disabled.enabled = false
        var long = screen; long.name = String(repeating: "长", count: 30); long.enabled = false
        var offScreen = screen; offScreen.enabled = false
        let cases: [[MacRegisteredDevice]] = [[], [tab], [screen], [tab, screen], [disabled, long], [tab, offScreen], [disabled, screen]]
        for devices in cases {
            var received: (String, String)?, common: String?
            let menu = MacDeviceCenterMenu.build(devices: devices, deviceAction: { received = ($0.id, $1) }, common: { common = $0 })
            let preview = devices.contains { $0.enabled }
            let roots = menu.items.filter { !$0.isSeparatorItem }
            try check("minimal roots", roots.count == devices.count + 2 + (preview ? 1 : 0))
            try check("common labels", roots.first?.title == "设备中心" && roots.last?.title == "退出")
            try check("preview availability", roots.contains { $0.identifier?.rawValue == "mirror" } == preview)
            try check("generic preview label", roots.first { $0.identifier?.rawValue == "mirror" }?.title == (preview ? "设备预览" : nil))
            try check("separators", menu.items.filter(\.isSeparatorItem).count == (devices.isEmpty ? 1 : 2))
            let groups = roots.filter { $0.identifier?.rawValue.hasPrefix("device:") == true }
            let sorted = devices.sorted { $0.kind < $1.kind }
            try check("Windows device order", groups.map { $0.identifier?.rawValue } == sorted.map { "device:" + $0.id })
            for (group, device) in zip(groups, sorted) {
                let expected = device.kind == "esp8266"
                    ? ["显示设置", "外观设置", "数据设置", "连接设置"]
                    : ["屏保图库", "语音设置", "日历生日", "电脑控制", "数据设置", "连接升级"]
                let entries = group.submenu!.items
                try check("Windows shortcut labels and order", entries.map(\.title) == expected)
                try check("disabled group and children", group.isEnabled == device.enabled && entries.allSatisfy { $0.isEnabled == device.enabled })
                try check("two menu levels", entries.allSatisfy { $0.submenu == nil })
                try check("bounded device label", group.title.count <= 30 && (device.enabled || group.title.hasSuffix("（已停用）")))
                if device.enabled {
                    for (index, entry) in entries.enumerated() {
                        received = nil
                        group.submenu!.performActionForItem(at: index)
                        try check("dispatch after tracking", received == nil)
                        RunLoop.current.run(until: Date().addingTimeInterval(0.02))
                        try check("shortcut routes to original device", received?.0 == device.id && received?.1 == entry.identifier?.rawValue)
                    }
                }
            }
            for key in ["devices", "exit"] {
                common = nil
                menu.performActionForItem(at: menu.items.firstIndex { $0.identifier?.rawValue == key }!)
                RunLoop.current.run(until: Date().addingTimeInterval(0.02))
                try check("common command routing", common == key)
            }
            if preview {
                let entry = menu.items.first { $0.identifier?.rawValue == "mirror" }!
                let targets = devices.filter(\.enabled).sorted { $0.kind < $1.kind }
                try check("selector only with multiple enabled devices", (entry.submenu != nil) == (targets.count > 1))
                for (index, device) in targets.enumerated() {
                    received = nil
                    if let choices = entry.submenu {
                        try check("selector device name", choices.items[index].title == device.name)
                        choices.performActionForItem(at:index)
                    } else { menu.performActionForItem(at:menu.items.firstIndex(of:entry)!) }
                    RunLoop.current.run(until:Date().addingTimeInterval(0.02))
                    try check("preview targets selected hardware",received?.0 == device.id && received?.1 == "mirror")
                }
            }
        }
        return checked
    }
}
