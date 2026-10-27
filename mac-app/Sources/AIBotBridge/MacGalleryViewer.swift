import AppKit

/// Shared image surface for the browser and the separate full-screen viewer.
final class MacGalleryCanvas: NSView {
    private(set) var image: NSImage?
    private(set) var zoom: CGFloat = 1
    private(set) var pan = NSPoint.zero
    private var drag = NSPoint.zero
    var doubleClick: (() -> Void)?
    var viewChanged: (() -> Void)?
    override var acceptsFirstResponder: Bool { true }
    override var isFlipped: Bool { true }
    override init(frame: NSRect) {
        super.init(frame: frame)
        clipsToBounds = true
        setAccessibilityLabel("作品图片：滚轮或双指缩放，拖动查看，双击切换全屏")
        let menu = NSMenu(); let fit = NSMenuItem(title: "适合窗口", action: #selector(resetView), keyEquivalent: "")
        fit.target = self; menu.addItem(fit); self.menu = menu
    }
    required init?(coder: NSCoder) { fatalError() }
    var imageBounds: NSRect {
        guard let image, image.size.width > 0, image.size.height > 0 else { return .zero }
        let scale = min(bounds.width / image.size.width, bounds.height / image.size.height) * zoom
        let size = NSSize(width: image.size.width * scale, height: image.size.height * scale)
        return NSRect(x: (bounds.width-size.width)/2+pan.x, y: (bounds.height-size.height)/2+pan.y, width: size.width, height: size.height)
    }
    func setImage(_ value: NSImage?) { image = value; resetView() }
    @objc func resetView() { zoom = 1; pan = .zero; needsDisplay = true; viewChanged?() }
    func zoom(to value: CGFloat, at anchor: NSPoint? = nil) {
        let before = imageBounds; guard before.width > 0, before.height > 0 else { return }
        let point = anchor ?? NSPoint(x: bounds.midX, y: bounds.midY)
        let fx = (point.x-before.minX)/before.width, fy = (point.y-before.minY)/before.height
        zoom = min(16, max(1, value)); pan = .zero
        let after = imageBounds
        pan = NSPoint(x: point.x-after.minX-fx*after.width, y: point.y-after.minY-fy*after.height)
        clampPan(); needsDisplay = true; viewChanged?()
    }
    func move(by delta: NSPoint) { pan.x += delta.x; pan.y += delta.y; clampPan(); needsDisplay = true }
    private func clampPan() {
        let rect = imageBounds, x = max(0,(rect.width-bounds.width)/2), y = max(0,(rect.height-bounds.height)/2)
        pan.x = min(x,max(-x,pan.x)); pan.y = min(y,max(-y,pan.y))
    }
    override func setFrameSize(_ size: NSSize) { super.setFrameSize(size); clampPan(); needsDisplay = true }
    override func draw(_ dirtyRect: NSRect) {
        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }
        bounds.clip()
        NSColor(calibratedWhite: 0.07, alpha: 1).setFill(); bounds.fill()
        NSGraphicsContext.current?.imageInterpolation = .high
        image?.draw(in: imageBounds, from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: nil)
    }
    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
        if event.clickCount == 2 { doubleClick?(); return }
        drag = convert(event.locationInWindow, from: nil)
    }
    override func mouseDragged(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil); move(by: NSPoint(x: point.x-drag.x, y: point.y-drag.y)); drag = point
    }
    func scrollZoom(deltaY: CGFloat, precise: Bool, at anchor: NSPoint? = nil) {
        guard deltaY.isFinite, deltaY != 0 else { return }
        // Trackpads report pixels; mouse wheels report lines (usually three per notch).
        // Bound accelerated/coalesced events so a single scroll never jumps to 16×.
        let steps = min(1, max(-1, deltaY / (precise ? 40 : 3)))
        zoom(to: zoom * pow(1.12, steps), at: anchor)
    }
    override func scrollWheel(with event: NSEvent) {
        guard event.momentumPhase.isEmpty else { return }
        scrollZoom(deltaY: event.scrollingDeltaY, precise: event.hasPreciseScrollingDeltas,
                   at: convert(event.locationInWindow, from: nil))
    }
    override func magnify(with event: NSEvent) { zoom(to: zoom * (1+event.magnification), at: convert(event.locationInWindow,from:nil)) }
    override func resetCursorRects() { addCursorRect(bounds,cursor:.openHand) }
}

final class MacGalleryReader: NSView {
    private var texts: [NSTextView] = [], scrolls: [NSScrollView] = [], buttons: [NSButton] = []
    private(set) var selectedIndex = 0
    override init(frame: NSRect) {
        super.init(frame: frame)
        let tabs = NSStackView(); tabs.spacing = 6; tabs.distribution = .fillEqually; tabs.translatesAutoresizingMaskIntoConstraints = false; addSubview(tabs)
        NSLayoutConstraint.activate([tabs.leadingAnchor.constraint(equalTo:leadingAnchor),tabs.trailingAnchor.constraint(equalTo:trailingAnchor),tabs.topAnchor.constraint(equalTo:topAnchor),tabs.heightAnchor.constraint(equalToConstant:36)])
        for (index,name) in ["简介", "欣赏提示", "原文"].enumerated() {
            let button = MacBridgeNavigationButton(title:name,target:self,action:#selector(selectTab(_:))); button.tag = index; button.setButtonType(.pushOnPushOff); button.isBordered = false; buttons.append(button); tabs.addArrangedSubview(button)
            button.heightAnchor.constraint(equalToConstant:36).isActive = true
            let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.autohidesScrollers = true; scroll.borderType = .bezelBorder
            let text = NSTextView(); text.isEditable = false; text.isSelectable = true; text.isRichText = false
            text.font = .systemFont(ofSize: 15); text.textColor = .labelColor; text.backgroundColor = .textBackgroundColor
            text.textContainerInset = NSSize(width: 20,height: 20); text.isVerticallyResizable = true; text.isHorizontallyResizable = false
            text.autoresizingMask = [.width]; text.textContainer?.widthTracksTextView = true
            scroll.documentView = text; texts.append(text); scrolls.append(scroll); scroll.translatesAutoresizingMaskIntoConstraints = false; addSubview(scroll)
            NSLayoutConstraint.activate([scroll.leadingAnchor.constraint(equalTo:leadingAnchor),scroll.trailingAnchor.constraint(equalTo:trailingAnchor),scroll.topAnchor.constraint(equalTo:tabs.bottomAnchor,constant:8),scroll.bottomAnchor.constraint(equalTo:bottomAnchor)])
        }
        select(0)
    }
    required init?(coder: NSCoder) { fatalError() }
    @objc private func selectTab(_ sender:NSButton) { select(sender.tag) }
    private func select(_ index:Int) { selectedIndex = index; for i in scrolls.indices { scrolls[i].isHidden = i != index; buttons[i].state = i == index ? .on:.off } }
    func update(_ chapters: [String]) { for (text, chapter) in zip(texts,chapters) where text.string != chapter { text.string = chapter; text.scrollToBeginningOfDocument(nil) } }
    func key(_ event: NSEvent) -> Bool {
        let text = texts[selectedIndex]
        let modifiers = event.modifierFlags.intersection([.command,.option,.control,.shift])
        if [123,124].contains(event.keyCode), modifiers.isEmpty || modifiers == .option {
            if modifiers.isEmpty, window?.firstResponder === text, text.selectedRange().length > 0 { return false }
            select((selectedIndex + (event.keyCode == 123 ? 2:1)) % 3); return true
        }
        guard modifiers.isEmpty else { return false }
        switch event.keyCode {
        case 125: text.scrollLineDown(nil)
        case 126: text.scrollLineUp(nil)
        case 121: text.scrollPageDown(nil)
        case 116: text.scrollPageUp(nil)
        default: return false
        }; return true
    }
}

final class MacGalleryComparison: NSView {
    let canvas = MacGalleryCanvas(frame: .zero), reader = MacGalleryReader(frame: .zero)
    var reading = false { didSet { needsLayout = true } }
    override var isFlipped: Bool { true }
    override init(frame: NSRect) { super.init(frame: frame); addSubview(canvas); addSubview(reader); reader.isHidden = true }
    required init?(coder: NSCoder) { fatalError() }
    override func layout() {
        super.layout(); reader.isHidden = !reading
        if !reading { canvas.frame = bounds }
        else if bounds.width >= 900 {
            let width = min(520,bounds.width*0.38)
            canvas.frame = NSRect(x:0,y:0,width:bounds.width-width-16,height:bounds.height)
            reader.frame = NSRect(x:bounds.width-width,y:0,width:width,height:bounds.height)
        } else {
            let height = max(1,(bounds.height-12)*0.52)
            canvas.frame = NSRect(x:0,y:0,width:bounds.width,height:height)
            reader.frame = NSRect(x:0,y:height+12,width:bounds.width,height:max(1,bounds.height-height-12))
        }
    }
}

struct MacGalleryPage {
    let work: MacTab5Artwork, frame: Int, image: NSImage, hd: Bool, guide: [String:Any]
    var title: String { (guide["title"] as? String).flatMap { $0.isEmpty ? nil:$0 } ?? work.title }
    var metadata: String { [guide["author"] as? String ?? work.author,guide["period"] as? String ?? "",guide["museum"] as? String ?? "",hd ? "高清原图 \(Int(image.size.width))×\(Int(image.size.height))":"标准预览"].filter { !$0.isEmpty }.joined(separator:" · ") }
    var chapters: [String] {
        func joined(_ keys: [String], _ fallback: String) -> String { let parts = keys.compactMap { guide[$0] as? String }.filter { !$0.isEmpty }; return parts.isEmpty ? fallback:parts.joined(separator:"\n\n") }
        return [joined(["brief","overview"],"此图库尚未提供作品简介。") + "\n\n许可：" + work.license,
                joined(["looking"],"此作品暂无已核实的欣赏提示。"), joined(["transcription","explanation"],"此作品暂无已核实原文。")]
    }
}

/// Native window-level shortcuts also work when a read-only chapter has focus.
final class MacGalleryWindow: NSWindow {
    var keyHandler: ((NSEvent) -> Bool)?
    override func sendEvent(_ event: NSEvent) {
        if event.type == .keyDown, !((firstResponder as? NSTextView)?.isEditable == true), keyHandler?(event) == true { return }
        super.sendEvent(event)
    }
}

final class MacGalleryStage: NSView {
    let comparison = MacGalleryComparison(frame:.zero)
    let title = MacBridgeSettingsStyle.label("选择作品",size:18,weight:.semibold)
    let metadata = MacBridgeSettingsStyle.note(""), pageLabel = MacBridgeSettingsStyle.note(""), message = MacBridgeSettingsStyle.note("")
    private let zoomLabel = MacBridgeSettingsStyle.note("100%")
    private var details: NSButton!, previous: NSButton!, next: NSButton!, fullscreen: NSButton!
    var navigate: ((Int) -> Void)?, toggleFullscreen: (() -> Void)?, close: (() -> Void)?
    private var page: MacGalleryPage?
    init(fullscreen: Bool) {
        super.init(frame:.zero)
        previous = MacSettingsButton("‹") { [weak self] in self?.navigate?(-1) }; previous.setAccessibilityLabel("上一页")
        next = MacSettingsButton("›") { [weak self] in self?.navigate?(1) }; next.setAccessibilityLabel("下一页")
        details = MacSettingsButton("作品详情 (I)") { [weak self] in self?.toggleReading() }
        self.fullscreen = MacSettingsButton(fullscreen ? "退出全屏 (Esc)":"全屏观看") { [weak self] in self?.toggleFullscreen?() }
        let first = MacSettingsButton("|‹") { [weak self] in guard let self, let page = self.page else { return }; self.navigate?(-page.frame) }; first.setAccessibilityLabel("第一页")
        let tools = MacBridgeSettingsStyle.row([first,previous,next,pageLabel,MacSettingsButton("适合窗口") { [weak self] in self?.comparison.canvas.resetView() },zoomLabel,NSView(),details,self.fullscreen])
        tools.spacing = 8
        let heading = NSStackView(views:[title,metadata]); heading.orientation = .vertical; heading.alignment = .leading; heading.spacing = 6
        for label in [title,metadata] { label.widthAnchor.constraint(equalTo:heading.widthAnchor).isActive = true }
        for view in [heading,comparison,tools,message] { view.translatesAutoresizingMaskIntoConstraints = false; addSubview(view) }
        NSLayoutConstraint.activate([
            heading.leadingAnchor.constraint(equalTo:leadingAnchor,constant:16),heading.trailingAnchor.constraint(equalTo:trailingAnchor,constant:-16),heading.topAnchor.constraint(equalTo:topAnchor,constant:16),
            comparison.leadingAnchor.constraint(equalTo:leadingAnchor,constant:16),comparison.trailingAnchor.constraint(equalTo:trailingAnchor,constant:-16),comparison.topAnchor.constraint(equalTo:heading.bottomAnchor,constant:12),comparison.bottomAnchor.constraint(equalTo:tools.topAnchor,constant:-12),
            tools.leadingAnchor.constraint(equalTo:leadingAnchor,constant:16),tools.trailingAnchor.constraint(equalTo:trailingAnchor,constant:-16),tools.bottomAnchor.constraint(equalTo:message.topAnchor,constant:-8),
            message.leadingAnchor.constraint(equalTo:leadingAnchor,constant:16),message.trailingAnchor.constraint(equalTo:trailingAnchor,constant:-16),message.bottomAnchor.constraint(equalTo:bottomAnchor,constant:-12), message.heightAnchor.constraint(greaterThanOrEqualToConstant:16)
        ])
        comparison.canvas.doubleClick = { [weak self] in self?.toggleFullscreen?() }
        comparison.canvas.viewChanged = { [weak self] in guard let self else { return }; self.zoomLabel.stringValue = "\(Int(self.comparison.canvas.zoom*100))%" }
        update(nil)
    }
    required init?(coder:NSCoder) { fatalError() }
    func update(_ page: MacGalleryPage?) {
        self.page = page; comparison.canvas.setImage(page?.image); comparison.reader.update(page?.chapters ?? ["","",""])
        title.stringValue = page?.title ?? "选择作品"; metadata.stringValue = page?.metadata ?? ""
        pageLabel.stringValue = page.map { "\($0.frame+1)/\($0.work.frames.count)" } ?? ""
        previous.isEnabled = (page?.work.frames.count ?? 0) > 1; next.isEnabled = previous.isEnabled
        details.isEnabled = page != nil; fullscreen.isEnabled = page != nil
        message.stringValue = page == nil ? "请选择已安装的作品。":"滚轮 / 双指缩放 · 拖动查看 · ← → 翻页 · I 图文对照 · ⌘0 适合窗口"
    }
    func toggleReading() { guard page != nil else { return }; comparison.reading.toggle(); details.title = comparison.reading ? "返回作品 (I)":"作品详情 (I)"; window?.makeFirstResponder(comparison.reading ? comparison.reader:comparison.canvas) }
    func key(_ event:NSEvent) -> Bool {
        let modifiers = event.modifierFlags.intersection([.command,.option,.control,.shift])
        if event.keyCode == 53 { if comparison.reading { toggleReading() } else { close?() }; return true }
        if modifiers == .command, event.charactersIgnoringModifiers == "0" { comparison.canvas.resetView(); return true }
        if modifiers.isEmpty, event.charactersIgnoringModifiers?.lowercased() == "i" { toggleReading(); return true }
        if comparison.reading { return comparison.reader.key(event) }
        if modifiers.isEmpty, [123,124].contains(event.keyCode) { navigate?(event.keyCode == 123 ? -1:1); return true }
        return false
    }
}

final class MacGalleryFullscreen: NSWindowController, NSWindowDelegate {
    let stage = MacGalleryStage(fullscreen:true)
    var didClose: (() -> Void)?
    init(page:MacGalleryPage, screen:NSScreen?, navigate:@escaping (Int)->Void) {
        let window = MacGalleryWindow(contentRect:NSRect(x:0,y:0,width:1040,height:740),styleMask:[.titled,.closable,.resizable],backing:.buffered,defer:false)
        super.init(window:window); window.title = page.title; window.isReleasedWhenClosed = false; window.collectionBehavior = [.fullScreenPrimary]; window.delegate = self
        window.contentView = stage; stage.navigate = navigate; stage.update(page)
        stage.toggleFullscreen = { [weak self] in self?.close() }; stage.close = stage.toggleFullscreen
        window.keyHandler = { [weak self] in self?.stage.key($0) ?? false }
        if let screen { window.setFrame(screen.visibleFrame.insetBy(dx:40,dy:40),display:false) }
    }
    required init?(coder:NSCoder) { fatalError() }
    func open() { showWindow(nil); window?.makeKeyAndOrderFront(nil); window?.makeFirstResponder(stage.comparison.canvas); window?.toggleFullScreen(nil) }
    func windowWillClose(_ notification:Notification) { didClose?() }
    func windowDidExitFullScreen(_ notification:Notification) { close() }
}
