import AppKit

final class MacTab5ScreenPreviewContent: NSView {
    let image = NSImageView(), status = MacBridgeSettingsStyle.note("")
    let name = NSTextField(labelWithString: "")
    init(name: String, close: @escaping () -> Void) {
        super.init(frame: .zero)
        self.name.stringValue = name; self.name.font = .systemFont(ofSize: 20, weight: .semibold)
        let subtitle = MacBridgeSettingsStyle.note("TAB5 画面镜像 · 仅查看")
        let button = MacSettingsButton("关闭", close)
        image.imageScaling = .scaleProportionallyUpOrDown
        image.setContentCompressionResistancePriority(.defaultLow,for:.horizontal)
        image.setContentCompressionResistancePriority(.defaultLow,for:.vertical)
        image.wantsLayer = true; image.layer?.backgroundColor = NSColor.black.cgColor
        image.identifier = .init("tab5-screen-preview"); status.identifier = .init("tab5-preview-status")
        for view in [self.name, subtitle, image, status, button] {
            view.translatesAutoresizingMaskIntoConstraints = false; addSubview(view)
        }
        NSLayoutConstraint.activate([
            self.name.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24),
            self.name.trailingAnchor.constraint(lessThanOrEqualTo: trailingAnchor, constant: -24),
            self.name.topAnchor.constraint(equalTo: topAnchor, constant: 20),
            subtitle.leadingAnchor.constraint(equalTo: self.name.leadingAnchor),
            subtitle.topAnchor.constraint(equalTo: self.name.bottomAnchor, constant: 4),
            subtitle.trailingAnchor.constraint(lessThanOrEqualTo: trailingAnchor, constant: -24),
            image.topAnchor.constraint(equalTo: subtitle.bottomAnchor, constant: 16),
            image.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24),
            image.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -24),
            image.bottomAnchor.constraint(equalTo: button.topAnchor, constant: -16),
            button.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -24),
            button.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -20),
            button.widthAnchor.constraint(equalToConstant: 72), button.heightAnchor.constraint(equalToConstant: 32),
            status.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 24),
            status.trailingAnchor.constraint(lessThanOrEqualTo: button.leadingAnchor, constant: -12),
            status.centerYAnchor.constraint(equalTo: button.centerYAnchor)
        ])
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
}

final class MacTab5ScreenPreviewWindow: NSWindowController, NSWindowDelegate {
    private let service: MacTab5Service, valid: () -> Bool
    private var content: MacTab5ScreenPreviewContent!
    private var image: NSImageView { content.image }
    private var status: NSTextField { content.status }
    private var token = "", timer: Timer?, shown: TimeInterval?, shownPixels: Data?
    private var shownWidth = 0, shownHeight = 0
    init(service: MacTab5Service, name: String, valid: @escaping () -> Bool) {
        self.service = service; self.valid = valid
        let window = MacBridgeSettingsStyle.window("设备预览 · " + name,width:840,height:580,minimumHeight:420)
        window.contentMinSize = NSSize(width:640,height:420); window.styleMask.insert(.miniaturizable)
        super.init(window:window); window.delegate = self
        content = MacTab5ScreenPreviewContent(name:name) { [weak self] in self?.close() }
        window.contentView = content
    }
    required init?(coder:NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate(); service.screenPreview.stop(token) }
    func updateName(_ name: String) { window?.title = "设备预览 · " + name; content.name.stringValue = name }
    func open() {
        if window?.isVisible == true, window?.isMiniaturized != true, !token.isEmpty {
            MacSettingsControls.open(self); refresh(); return
        }
        MacSettingsControls.open(self)
        window?.deminiaturize(nil)
        token = service.screenPreview.start(service.screenPreviewContext); shown = nil; shownPixels = nil; image.image = nil
        timer?.invalidate()
        timer = Timer(timeInterval:0.1,repeats:true) { [weak self] _ in self?.refresh() }
        RunLoop.main.add(timer!,forMode:.common)
        refresh()
    }
    private func refresh() {
        guard window?.isVisible == true, window?.isMiniaturized != true else {
            service.screenPreview.stop(token); token = ""; image.image = nil; shown = nil; shownPixels = nil; return
        }
        guard valid() else {
            service.screenPreview.stop(token); token = ""; image.image = nil; status.stringValue = "设备已移除或停用。"; return
        }
        if token.isEmpty { token = service.screenPreview.start(service.screenPreviewContext) }
        service.screenPreview.renew(token,context:service.screenPreviewContext)
        let snapshot = service.screenPreview.snapshot(token)
        if status.stringValue != snapshot.1 { status.stringValue = snapshot.1 }
        if let frame = snapshot.0, shown != frame.received {
            if shownPixels != frame.pixels || shownWidth != frame.width || shownHeight != frame.height {
                image.image = Self.image(frame); shownPixels = frame.pixels; shownWidth = frame.width; shownHeight = frame.height
            }
            shown = frame.received
            service.screenPreview.observedInWindow(token,received:frame.received)
        } else if snapshot.0 == nil { image.image = nil; shown = nil; shownPixels = nil }
    }
    func windowWillClose(_ notification:Notification) {
        timer?.invalidate(); timer = nil; service.screenPreview.stop(token); image.image = nil; shown = nil; shownPixels = nil
    }
    static func image(_ frame: MacTab5ScreenPreview.Frame) -> NSImage? {
        guard frame.pixels.count == frame.width*frame.height*2,
              let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:frame.width,pixelsHigh:frame.height,
                bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,
                bytesPerRow:frame.width*4,bitsPerPixel:32), let out = bitmap.bitmapData else { return nil }
        frame.pixels.withUnsafeBytes { (bytes:UnsafeRawBufferPointer) in
            for i in 0..<frame.width*frame.height {
                let value = UInt16(bytes[i*2]) | UInt16(bytes[i*2+1])<<8
                let r = UInt8((value>>11)&31), g = UInt8((value>>5)&63), b = UInt8(value&31)
                out[i*4]=(r<<3)|(r>>2); out[i*4+1]=(g<<2)|(g>>4); out[i*4+2]=(b<<3)|(b>>2); out[i*4+3]=255
            }
        }
        let result = NSImage(size:NSSize(width:frame.width,height:frame.height)); result.addRepresentation(bitmap); return result
    }
}
