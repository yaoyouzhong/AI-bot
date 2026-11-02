import AppKit

enum MacKeychainMigrationAlert {
    static func make() -> NSAlert {
        let alert = NSAlert()
        alert.icon = NSImage(systemSymbolName: "lock.shield", accessibilityDescription: nil)
        alert.messageText = "迁移旧版钥匙串资料"
        alert.informativeText = "检测到旧版资料。macOS 可能要求分别授权原有钥匙串项目；完成迁移后，桥接将通过一个资料入口读取。请在系统提示中选择“始终允许”。\n\n暂不迁移或授权失败时，原资料会保留，设备连接可能暂不可用。"
        alert.addButton(withTitle: "暂不迁移")
        alert.addButton(withTitle: "继续迁移")
        alert.buttons[0].keyEquivalent = "\r"
        alert.buttons[1].keyEquivalent = ""
        return alert
    }
}
