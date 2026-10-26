import AppKit

final class MacSettingsButton: NSButton {
    private let handler: () -> Void
    init(_ title: String, _ handler: @escaping () -> Void) {
        self.handler = handler; super.init(frame: .zero); self.title = title
        bezelStyle = .rounded; target = self; action = #selector(invoke)
    }
    required init?(coder: NSCoder) { fatalError() }
    @objc private func invoke() { handler() }
}

final class MacSettingsTable: NSScrollView, NSTableViewDataSource, NSTableViewDelegate {
    let table = NSTableView()
    var rows: [[String]] = [] { didSet { table.reloadData() } }
    var selected: ((Int) -> Void)?
    init(_ titles: [String], minimumHeight: CGFloat = 180) {
        super.init(frame: .zero)
        hasVerticalScroller = true; autohidesScrollers = true; borderType = .bezelBorder
        table.usesAlternatingRowBackgroundColors = true; table.rowHeight = 36
        table.columnAutoresizingStyle = .uniformColumnAutoresizingStyle
        for (i, title) in titles.enumerated() {
            let c = NSTableColumn(identifier: .init(String(i))); c.title = title; c.width = 160; c.minWidth = 60
            table.addTableColumn(c)
        }
        table.delegate = self; table.dataSource = self; documentView = table
        heightAnchor.constraint(greaterThanOrEqualToConstant: minimumHeight).isActive = true
    }
    var selectedRow: Int { table.selectedRow }
    func select(_ index: Int) { table.selectRowIndexes(index >= 0 ? IndexSet(integer: index) : [], byExtendingSelection: false) }
    func numberOfRows(in tableView: NSTableView) -> Int { rows.count }
    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        guard rows.indices.contains(row), let column = Int(tableColumn?.identifier.rawValue ?? ""), rows[row].indices.contains(column) else { return nil }
        let text = NSTextField(labelWithString: rows[row][column]); text.lineBreakMode = .byTruncatingTail; text.toolTip = text.stringValue
        return text
    }
    func tableViewSelectionDidChange(_ notification: Notification) { selected?(table.selectedRow) }
    required init?(coder: NSCoder) { fatalError() }
}

enum MacSettingsControls {
    static func columns(_ left: NSView, _ right: NSView, leftWidth: CGFloat = 300) -> NSView {
        let view = NSView(); left.translatesAutoresizingMaskIntoConstraints = false; right.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(left); view.addSubview(right)
        NSLayoutConstraint.activate([
            left.leadingAnchor.constraint(equalTo: view.leadingAnchor), left.topAnchor.constraint(equalTo: view.topAnchor), left.bottomAnchor.constraint(equalTo: view.bottomAnchor), left.widthAnchor.constraint(equalToConstant: leftWidth),
            right.leadingAnchor.constraint(equalTo: left.trailingAnchor), right.trailingAnchor.constraint(equalTo: view.trailingAnchor), right.topAnchor.constraint(equalTo: view.topAnchor), right.bottomAnchor.constraint(equalTo: view.bottomAnchor)
        ]); return view
    }
    static func popup(_ titles: [String]) -> NSPopUpButton { let p = NSPopUpButton(); p.addItems(withTitles: titles); return p }
    static func datePicker() -> NSDatePicker {
        let p = NSDatePicker(); p.datePickerStyle = .textFieldAndStepper; p.datePickerElements = [.yearMonthDay, .hourMinute]; p.dateValue = Date().addingTimeInterval(3600)
        return p
    }
    static func open(_ controller: NSWindowController) { controller.showWindow(nil); controller.window?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
}
