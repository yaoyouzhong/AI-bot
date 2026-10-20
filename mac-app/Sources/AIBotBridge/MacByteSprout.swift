import AppKit

/// The project's original geometric fallback, also used by the Mac preview.
/// No imported artwork, logo, cache file or external download is required.
enum MacByteSprout {
    static func draw(x: CGFloat, y: CGFloat, working: Bool, tick: Int) {
        let shell: NSColor = working ? .cyan : .darkGray
        func box(_ dx: CGFloat, _ dy: CGFloat, _ w: CGFloat, _ h: CGFloat, _ color: NSColor) {
            color.setFill(); NSRect(x: x + dx, y: y + dy, width: w, height: h).fill()
        }
        box(27, 0, 4, 10, shell); box(23, 0, 12, 4, shell)
        shell.setFill(); NSBezierPath(roundedRect: NSRect(x: x + 8, y: y + 10, width: 42, height: 35), xRadius: 5, yRadius: 5).fill()
        box(14, 17, 30, 19, .black)
        box(20, 23, 5, 6, working ? .green : .lightGray); box(34, 23, 5, 6, working ? .green : .lightGray)
        shell.setFill(); NSBezierPath(roundedRect: NSRect(x: x + 12, y: y + 48, width: 34, height: 42), xRadius: 5, yRadius: 5).fill()
        box(20, 59, 18, 5, .black); box(5, 54, 7, 27, shell); box(46, 54, 7, 27, shell)
        let step = working && (tick / 240) % 2 == 1
        box(step ? 6 : 15, 90, 17, 8, shell); box(step ? 32 : 41, 90, 17, 8, shell)
    }
    static let animation: MacPetAnimation? = {
        var frames: [Data] = []
        for step in 0..<2 {
            guard let context = CGContext(data: nil, width: 112, height: 112, bitsPerComponent: 8,
                                          bytesPerRow: 112 * 4, space: CGColorSpaceCreateDeviceRGB(),
                                          bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }
            context.setShouldAntialias(false)
            // The existing RGB565 encoder performs the output-row inversion.
            // Applying another bitmap transform here would flip the device pet.
            NSGraphicsContext.saveGraphicsState()
            NSGraphicsContext.current = NSGraphicsContext(cgContext: context, flipped: true)
            NSColor.black.setFill(); NSRect(x: 0, y: 0, width: 112, height: 112).fill()
            draw(x: 28, y: 6, working: true, tick: step * 240)
            NSGraphicsContext.restoreGraphicsState()
            guard let image = context.makeImage(),
                  let bytes = MacRgb565Renderer.renderImage(image, width: 112, height: 112) else { return nil }
            frames.append(bytes)
        }
        return MacPetAnimation(width: 112, height: 112, delays: [240, 240], frames: frames)
    }()
}
