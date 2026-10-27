import AppKit

/// Shared native layout for the device center. Content follows the viewport;
/// only the document height grows, so a small window never needs horizontal scrolling.
final class MacBridgeSettingsPage: NSScrollView {
    let stack = NSStackView()
    private let document = MacBridgeSettingsDocument()
    private var cardGrids: [MacBridgeCardGrid] = []

    init() {
        super.init(frame: .zero)
        hasVerticalScroller = true; autohidesScrollers = true; drawsBackground = false
        document.autoresizingMask = [.width]; documentView = document
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 16
        stack.translatesAutoresizingMaskIntoConstraints = false
        document.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: document.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(equalTo: document.trailingAnchor, constant: -24),
            stack.topAnchor.constraint(equalTo: document.topAnchor, constant: 24)
        ])
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    func add(_ view: NSView) {
        stack.addArrangedSubview(view)
        if let button = view as? NSButton { button.lineBreakMode = .byTruncatingTail; button.widthAnchor.constraint(lessThanOrEqualTo: stack.widthAnchor).isActive = true }
        else { view.widthAnchor.constraint(equalTo: stack.widthAnchor).isActive = true }
    }

    func heading(_ title: String, subtitle: String = "") {
        add(MacBridgeSettingsStyle.label(title, size: 23, weight: .semibold))
        if !subtitle.isEmpty { add(MacBridgeSettingsStyle.note(subtitle)) }
    }

    func section(_ title: String) {
        if !stack.arrangedSubviews.isEmpty {
            let line = NSBox(); line.boxType = .separator; add(line)
        }
        add(MacBridgeSettingsStyle.label(title, size: 15, weight: .semibold))
    }

    func field(_ title: String, _ control: NSView) {
        let column = NSStackView(); column.orientation = .vertical; column.alignment = .leading; column.spacing = 8
        column.addArrangedSubview(MacBridgeSettingsStyle.label(title))
        column.addArrangedSubview(control); control.widthAnchor.constraint(equalTo: column.widthAnchor).isActive = true
        add(column)
    }

    func fitDocument() {
        let width = max(1, contentSize.width)
        if abs(document.frame.width - width) > 0.5 { document.setFrameSize(NSSize(width: width, height: max(1, document.frame.height))) }
        cardGrids.forEach { $0.reflow(width: max(1, width - 48)) }
        stack.layoutSubtreeIfNeeded()
        let height = max(contentSize.height, stack.fittingSize.height + 48)
        if abs(document.frame.height - height) > 0.5 { document.setFrameSize(NSSize(width: width, height: height)) }
    }

    override func layout() { super.layout(); fitDocument() }

    func addCards(_ cards: [MacBridgeActionCard]) {
        let grid = MacBridgeCardGrid(cards); cardGrids.append(grid); add(grid)
    }
}

/// Explicit card positions keep a capped grid from contributing a horizontal
/// fitting size to NSWindow. Resizing changes placement without replacing cards.
final class MacBridgeCardGrid: NSView {
    private let cards: [MacBridgeActionCard]
    private var placements: [(left: NSLayoutConstraint, top: NSLayoutConstraint, width: NSLayoutConstraint)] = []
    private var contentHeight: NSLayoutConstraint?
    private(set) var columns = 0
    override var isFlipped: Bool { true }
    init(_ cards: [MacBridgeActionCard]) {
        self.cards = cards; super.init(frame: .zero)
        contentHeight = heightAnchor.constraint(equalToConstant: 68); contentHeight?.isActive = true
        for card in cards {
            card.translatesAutoresizingMaskIntoConstraints = false; addSubview(card)
            let left = card.leadingAnchor.constraint(equalTo: leadingAnchor)
            let top = card.topAnchor.constraint(equalTo: topAnchor)
            let width = card.widthAnchor.constraint(equalToConstant: 200)
            NSLayoutConstraint.activate([left,top,width]); placements.append((left,top,width))
        }
        reflow(width: 1)
    }
    required init?(coder: NSCoder) { fatalError() }
    static func columnCount(count: Int, width: CGFloat, preferred: CGFloat) -> Int {
        count >= 6 && width >= preferred * 3 + 24 ? 3 : 2
    }
    func reflow(width: CGFloat) {
        columns = Self.columnCount(count: cards.count, width: width, preferred: cards.map(\.preferredWidth).max() ?? 200)
        let cardWidth = max(1, min(240, (width - CGFloat(columns - 1) * 12) / CGFloat(columns)))
        let rows = (cards.count + columns - 1) / columns
        contentHeight?.constant = CGFloat(rows * 68 + max(0, rows - 1) * 12)
        for (index, placement) in placements.enumerated() {
            placement.left.constant = CGFloat(index % columns) * (cardWidth + 12)
            placement.top.constant = CGFloat(index / columns) * 80
            placement.width.constant = cardWidth
        }
    }
}

private final class MacBridgeSettingsDocument: NSView { override var isFlipped: Bool { true } }

/// Keep NSButton's keyboard and accessibility behavior while drawing a compact
/// title/detail card like the Windows device center, using macOS semantic colors.
final class MacBridgeActionCard: NSButton {
    private let caption: String
    private let detail: String
    private let handler: () -> Void

    init(_ title: String, _ detail: String, id: String, action: @escaping () -> Void) {
        caption = title; self.detail = detail; handler = action
        super.init(frame: .zero)
        self.title = title; identifier = NSUserInterfaceItemIdentifier(id)
        setButtonType(.momentaryPushIn); isBordered = false
        target = self; self.action = #selector(invoke)
        alignment = .left; toolTip = detail
        setAccessibilityLabel(title); setAccessibilityHelp(detail)
        heightAnchor.constraint(equalToConstant: 68).isActive = true
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    @objc private func invoke() { handler() }
    override var isFlipped: Bool { true }
    var preferredWidth: CGFloat {
        max(200, max((caption as NSString).size(withAttributes: [.font: NSFont.systemFont(ofSize: 14, weight: .semibold)]).width,
                     (detail as NSString).size(withAttributes: [.font: NSFont.systemFont(ofSize: 12)]).width) + 32)
    }

    override func draw(_ dirtyRect: NSRect) {
        let shape = NSBezierPath(roundedRect: bounds.insetBy(dx: 1, dy: 1), xRadius: 8, yRadius: 8)
        (isHighlighted ? NSColor.selectedContentBackgroundColor.withAlphaComponent(0.12) : .controlBackgroundColor).setFill()
        shape.fill(); NSColor.separatorColor.setStroke(); shape.stroke()
        let paragraph = NSMutableParagraphStyle(); paragraph.lineBreakMode = .byTruncatingTail
        (caption as NSString).draw(in: NSRect(x: 16, y: 12, width: bounds.width - 32, height: 22), withAttributes: [
            .font: NSFont.systemFont(ofSize: 14, weight: .semibold),
            .foregroundColor: isEnabled ? NSColor.labelColor : .disabledControlTextColor,
            .paragraphStyle: paragraph
        ])
        (detail as NSString).draw(in: NSRect(x: 16, y: 38, width: bounds.width - 32, height: 20), withAttributes: [
            .font: NSFont.systemFont(ofSize: 12), .foregroundColor: NSColor.secondaryLabelColor,
            .paragraphStyle: paragraph
        ])
    }
    override var focusRingMaskBounds: NSRect { bounds }
    override func drawFocusRingMask() { NSBezierPath(roundedRect: bounds, xRadius: 8, yRadius: 8).fill() }
}

final class MacBridgeNavigationButton: NSButton {
    override var isFlipped: Bool { true }
    override func draw(_ dirtyRect: NSRect) {
        if state == .on || isHighlighted {
            NSColor.controlAccentColor.withAlphaComponent(0.14).setFill()
            NSBezierPath(roundedRect: bounds.insetBy(dx: 1, dy: 1), xRadius: 6, yRadius: 6).fill()
        }
        let paragraph = NSMutableParagraphStyle(); paragraph.lineBreakMode = .byTruncatingTail
        (title as NSString).draw(in: NSRect(x: 12, y: (bounds.height - 20) / 2, width: bounds.width - 24, height: 20), withAttributes: [
            .font: NSFont.systemFont(ofSize: 13, weight: state == .on ? .semibold : .regular),
            .foregroundColor: state == .on ? NSColor.controlAccentColor : .labelColor,
            .paragraphStyle: paragraph
        ])
    }
    override var focusRingMaskBounds: NSRect { bounds }
    override func drawFocusRingMask() { NSBezierPath(roundedRect: bounds, xRadius: 6, yRadius: 6).fill() }
}

enum MacBridgeAction {
    case status, cycle, screenSaver, mirror, petGallery, deviceInfo, flash, reprovisionLAN, pairingToken, copyAddress
    case appearance, espConnection, espFallback, espReset, espData, tab5Data, quotaHistory, updates, configuration, notifications, runtime, about
}

enum MacBridgeSettingsStyle {
    static func window(_ title: String, width: CGFloat = 680, height: CGFloat = 620, minimumHeight: CGFloat = 440) -> NSWindow {
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: width, height: height),
                              styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = "AI-bot · " + title; window.contentMinSize = NSSize(width: min(width, 600), height: minimumHeight)
        window.isReleasedWhenClosed = false; window.center(); return window
    }
    static func label(_ text: String, size: CGFloat = 13, weight: NSFont.Weight = .regular) -> NSTextField {
        let label = NSTextField(wrappingLabelWithString: text); label.font = .systemFont(ofSize: size, weight: weight); return label
    }
    static func note(_ text: String) -> NSTextField {
        let label = self.label(text, size: 12); label.textColor = .secondaryLabelColor; return label
    }
    static func row(_ views: [NSView]) -> NSStackView {
        let row = NSStackView(views: views); row.spacing = 12; row.alignment = .centerY; return row
    }
}

/// One scrollable body, with a fixed receipt and action area shared by all editors.
final class MacBridgeSettingsShell: NSView {
    init(body: NSView, status: NSTextField? = nil, actions: [NSButton] = []) {
        super.init(frame: .zero)
        body.translatesAutoresizingMaskIntoConstraints = false; addSubview(body)
        let footer = NSStackView(); footer.orientation = .vertical; footer.alignment = .leading; footer.spacing = 12
        footer.identifier = NSUserInterfaceItemIdentifier("settings-footer")
        footer.translatesAutoresizingMaskIntoConstraints = false; addSubview(footer)
        if let status {
            status.font = .systemFont(ofSize: 12); status.textColor = .secondaryLabelColor
            footer.addArrangedSubview(status); status.widthAnchor.constraint(equalTo: footer.widthAnchor).isActive = true
            status.heightAnchor.constraint(greaterThanOrEqualToConstant: 16).isActive = true
        }
        for start in stride(from:0,to:actions.count,by:3) { footer.addArrangedSubview(MacBridgeSettingsStyle.row(Array(actions[start..<min(start+3,actions.count)]))) }
        let line = NSBox(); line.boxType = .separator; line.translatesAutoresizingMaskIntoConstraints = false; addSubview(line)
        NSLayoutConstraint.activate([
            body.leadingAnchor.constraint(equalTo: leadingAnchor), body.trailingAnchor.constraint(equalTo: trailingAnchor),
            body.topAnchor.constraint(equalTo: topAnchor), body.bottomAnchor.constraint(equalTo: line.topAnchor, constant: -4),
            line.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24), line.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -24),
            line.bottomAnchor.constraint(equalTo: footer.topAnchor, constant: -12),
            footer.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24), footer.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -24),
            footer.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -20)
        ])
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
}

final class MacBridgeSettingsTabs: NSView {
    let pages: [MacBridgeSettingsPage]
    private var buttons: [NSButton] = []
    private(set) var selectedIndex = 0
    init(_ sections: [(String, MacBridgeSettingsPage)]) {
        pages = sections.map(\.1)
        super.init(frame: .zero)
        let navigation = NSStackView(); navigation.spacing = 8; navigation.distribution = .fillEqually
        navigation.translatesAutoresizingMaskIntoConstraints = false; addSubview(navigation)
        NSLayoutConstraint.activate([navigation.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24), navigation.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -24), navigation.topAnchor.constraint(equalTo: topAnchor, constant: 12)])
        for (index, section) in sections.enumerated() {
            let button = MacBridgeNavigationButton(title: section.0, target: self, action: #selector(selectPage(_:)))
            button.tag = index; button.identifier = NSUserInterfaceItemIdentifier("settings-tab-\(index)")
            button.setButtonType(.pushOnPushOff); button.isBordered = false
            button.heightAnchor.constraint(equalToConstant: 36).isActive = true
            navigation.addArrangedSubview(button); buttons.append(button)
            let page = section.1; page.translatesAutoresizingMaskIntoConstraints = false; addSubview(page)
            NSLayoutConstraint.activate([page.leadingAnchor.constraint(equalTo: leadingAnchor), page.trailingAnchor.constraint(equalTo: trailingAnchor), page.topAnchor.constraint(equalTo: navigation.bottomAnchor, constant: 4), page.bottomAnchor.constraint(equalTo: bottomAnchor)])
        }
        select(0)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func select(_ index: Int) {
        guard pages.indices.contains(index) else { return }; selectedIndex = index
        for i in pages.indices { pages[i].isHidden = i != index; buttons[i].state = i == index ? .on : .off }
        needsLayout = true
    }
    @objc private func selectPage(_ sender: NSButton) { select(sender.tag) }
}

final class MacBridgeSettingsForm: NSWindowController, NSWindowDelegate {
    let page = MacBridgeSettingsPage()
    let status = MacBridgeSettingsStyle.note("")
    private var validate: (() -> String?)?

    init(_ title: String, subtitle: String, width: CGFloat = 640, height: CGFloat = 600, confirm: String = "保存", body: NSView? = nil) {
        let window = MacBridgeSettingsStyle.window(title, width: width, height: height, minimumHeight: min(height, 380))
        super.init(window: window); window.delegate = self
        page.heading(title, subtitle: subtitle)
        let save = NSButton(title: confirm, target: self, action: #selector(accept)); save.keyEquivalent = "\r"
        let cancel = NSButton(title: "取消", target: self, action: #selector(cancel)); cancel.keyEquivalent = "\u{1b}"
        window.contentView = MacBridgeSettingsShell(body: body ?? page, status: status, actions: [save, cancel])
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func run(validating: (() -> String?)? = nil) -> Bool {
        guard let window else { return false }; validate = validating; status.stringValue = ""
        window.center(); window.makeKeyAndOrderFront(nil)
        let result = NSApp.runModal(for: window); window.orderOut(nil); validate = nil
        return result == .OK
    }
    @objc private func accept() {
        guard window?.makeFirstResponder(nil) == true else { return }
        if let error = validate?() { status.stringValue = error; return }
        NSApp.stopModal(withCode: .OK)
    }
    @objc private func cancel() { NSApp.stopModal(withCode: .cancel) }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if NSApp.modalWindow === sender { NSApp.stopModal(withCode: .cancel) }; return true
    }
}
