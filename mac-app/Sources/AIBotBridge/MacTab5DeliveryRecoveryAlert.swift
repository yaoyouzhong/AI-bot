import AppKit

// Opened only by the user's explicit recovery-button action. Rendering fixtures
// never order a window front, activate the app, or read a real delivery journal.
final class MacTab5DeliveryRecoveryAlert: NSObject {
    let alert = NSAlert()
    private let items: [MacTab5Codex.PendingDelivery]
    private let picker = NSPopUpButton(frame: NSRect(x: 0, y: 82, width: 420, height: 28))
    private let preview = NSTextField(wrappingLabelWithString: "")
    var selected: MacTab5Codex.PendingDelivery? {
        items.indices.contains(picker.indexOfSelectedItem) ? items[picker.indexOfSelectedItem] : nil
    }
    init(items: [MacTab5Codex.PendingDelivery]) {
        self.items = items
        super.init()
        alert.messageText = "确认消息已送达"
        alert.icon = NSImage(systemSymbolName: "checkmark.message", accessibilityDescription: nil)
        alert.informativeText = "请先在电脑 Codex 会话中核对所选消息确已送达。确认仅结束这一条待确认记录，不会重新发送，也不会修改电脑输入框。"
        alert.addButton(withTitle: "取消")
        alert.addButton(withTitle: "已在电脑确认送达")
        alert.buttons[0].keyEquivalent = "\r"
        alert.buttons[1].keyEquivalent = ""
        alert.buttons[1].isEnabled = !items.isEmpty
        let accessory = NSView(frame: NSRect(x: 0, y: 0, width: 420, height: 110))
        picker.addItems(withTitles: items.map { "[" + String($0.id.prefix(8)) + "] " + String($0.title.prefix(44)) })
        picker.cell?.lineBreakMode = .byTruncatingMiddle
        picker.target = self; picker.action = #selector(selectionChanged)
        picker.identifier = .init("pending-delivery-selection")
        preview.frame = NSRect(x: 0, y: 8, width: 420, height: 62)
        preview.font = .systemFont(ofSize: 13); preview.textColor = .secondaryLabelColor
        preview.maximumNumberOfLines = 3; preview.lineBreakMode = .byWordWrapping
        preview.identifier = .init("pending-delivery-text")
        accessory.addSubview(picker); accessory.addSubview(preview)
        alert.accessoryView = accessory; selectionChanged()
    }
    @objc private func selectionChanged() {
        preview.stringValue = selected.map { String($0.text.prefix(90)) + ($0.text.count > 90 ? "…" : "") } ?? "无待确认记录"
        preview.toolTip = selected?.text
        picker.toolTip = selected.map { $0.title + "\n请求：" + $0.id }
    }
    static func render(_ output: URL) throws {
        _ = NSApplication.shared
        NSApp.setActivationPolicy(.prohibited)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        for (name, appearance, longText) in [("light", NSAppearance.Name.aqua, false), ("dark", .darkAqua, false), ("long-light", .aqua, true), ("long-dark", .darkAqua, true)] {
            let fixture = MacTab5Codex.PendingDelivery(id: "00000000-0000-4000-8000-000000000001", binding: "fixture",
                fingerprint: "fixture", expectedTurn: "fixture", title: longText ? String(repeating: "较长的测试会话名称", count: 8) : "修复 TAB5 回填未就绪 · 测试会话",
                text: longText ? String(repeating: "这是需要人工核对的长测试文字，不能覆盖另一条草稿。", count: 20) : "草稿保护验收。")
            let dialog = MacTab5DeliveryRecoveryAlert(items: [fixture])
            dialog.alert.window.appearance = NSAppearance(named: appearance)
            dialog.alert.layout()
            guard let view = dialog.alert.window.contentView else { throw MacTab5LocalError.invalid("Missing recovery view") }
            dialog.alert.window.displayIfNeeded()
            view.layoutSubtreeIfNeeded()
            guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw MacTab5LocalError.invalid("Missing recovery bitmap") }
            let appearance = view.effectiveAppearance
            appearance.performAsCurrentDrawingAppearance { view.cacheDisplay(in: view.bounds, to: bitmap) }
            let drawing = NSImage(size: view.bounds.size); drawing.addRepresentation(bitmap)
            let opaque = NSImage(size: view.bounds.size)
            opaque.lockFocus()
            appearance.performAsCurrentDrawingAppearance {
                NSColor.windowBackgroundColor.setFill(); view.bounds.fill(); drawing.draw(in: view.bounds)
            }
            opaque.unlockFocus()
            guard let tiff = opaque.tiffRepresentation, let composed = NSBitmapImageRep(data: tiff),
                  let data = composed.representation(using: .png, properties: [:]) else { throw MacTab5LocalError.invalid("Missing recovery PNG") }
            try data.write(to: output.appendingPathComponent("delivery-recovery-" + name + ".png"))
        }
        print("DELIVERY_RECOVERY_UI_RENDER_OK synthetic only; no windows ordered front")
    }
}
