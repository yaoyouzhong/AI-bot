import AppKit

/// Compare editable controls, not live readouts, before closing a draft editor.
final class MacSettingsDraft: NSObject, NSWindowDelegate {
    private weak var window:NSWindow?
    private let fingerprint: () -> String
    private var original:String
    init(_ window:NSWindow?, fingerprint:@escaping () -> String) {
        self.window = window; self.fingerprint = fingerprint; original = fingerprint()
        super.init(); window?.delegate = self
    }
    func markSaved() { original = fingerprint() }
    func windowShouldClose(_ sender:NSWindow) -> Bool {
        sender.makeFirstResponder(nil)
        guard original != fingerprint() else { return true }
        let alert = NSAlert(); alert.messageText = "放弃未保存的修改？"; alert.informativeText = "关闭后，本次修改不会保存。"; alert.addButton(withTitle:"继续编辑"); alert.addButton(withTitle:"放弃修改")
        return alert.runModal() == .alertSecondButtonReturn
    }
    static func values(_ controls:[NSControl]) -> String {
        controls.map { control in
            if let field = control as? NSTextField { return field.stringValue }
            if let popup = control as? NSPopUpButton { return String(popup.indexOfSelectedItem) }
            if let button = control as? NSButton { return String(button.state.rawValue) }
            if let date = control as? NSDatePicker { return String(date.dateValue.timeIntervalSince1970) }
            return ""
        }.map { "\($0.utf8.count):\($0)" }.joined(separator:"|")
    }
}
