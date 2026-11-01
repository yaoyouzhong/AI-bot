import AppKit

/// Compare editable controls, not live readouts, before closing a draft editor.
final class MacSettingsDraft: NSObject, NSWindowDelegate {
    private weak var window:NSWindow?
    private let fingerprint: () -> String
    private var original:String
    var confirmDiscard: (() -> Bool)?
    var hasChanges: Bool { original != fingerprint() }
    init(_ window:NSWindow?, fingerprint:@escaping () -> String) {
        self.window = window; self.fingerprint = fingerprint; original = fingerprint()
        super.init(); window?.delegate = self
    }
    func markSaved() { original = fingerprint() }
    func windowShouldClose(_ sender:NSWindow) -> Bool {
        sender.makeFirstResponder(nil)
        guard hasChanges else { return true }
        if let confirmDiscard { return confirmDiscard() }
        let alert = NSAlert(); alert.messageText = "放弃未保存的修改？"; alert.informativeText = "关闭后，本次修改不会保存。"; alert.addButton(withTitle:"继续编辑"); alert.addButton(withTitle:"放弃修改")
        return alert.runModal() == .alertSecondButtonReturn
    }
    static func editableValues(in view: NSView) -> String {
        func controls(_ view: NSView) -> [NSControl] {
            let own: [NSControl]
            if let field = view as? NSTextField, field.isEditable { own = [field] }
            else if let popup = view as? NSPopUpButton { own = [popup] }
            else if let date = view as? NSDatePicker { own = [date] }
            else if let button = view as? NSButton,
                    [NSAccessibility.Role.checkBox, .radioButton].contains(button.accessibilityRole() ?? .unknown) { own = [button] }
            else { own = [] }
            return own + view.subviews.flatMap(controls)
        }
        return values(controls(view))
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
