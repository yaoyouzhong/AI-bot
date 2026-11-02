import AppKit
import ApplicationServices

enum MacDirectAuthorizationAlert {
    static func make(previouslyRequested: Bool) -> NSAlert {
        let alert = NSAlert()
        alert.icon = NSImage(systemSymbolName: "hand.raised.fill", accessibilityDescription: nil)
        alert.messageText = "完成 Codex 直达授权"
        alert.informativeText = "请在系统设置的“隐私与安全性 → 辅助功能”中允许 AI-bot Bridge。部分系统名称为“设备控制与数据访问”。\n\n未列出时，点“+”添加“应用程序”中的 AIBotBridge.app。"
            + (previouslyRequested ? "已勾选仍无效时，先移除旧条目再重新添加。" : "")
            + "\n\n完成后回 TAB5 重新定位。"
        alert.addButton(withTitle: "打开系统设置…")
        alert.addButton(withTitle: "稍后")
        alert.buttons[0].keyEquivalent = "\r"
        alert.buttons[1].keyEquivalent = "\u{1b}"
        return alert
    }

    static func openSettings() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            _ = NSWorkspace.shared.open(url)
        }
    }
}
