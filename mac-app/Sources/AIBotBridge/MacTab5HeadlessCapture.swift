import AppKit

// Explicit, local USB capture using the same authenticated preview receiver as
// the mirror window. No AppDelegate, visible window, touch or firmware command.
enum MacTab5HeadlessCapture {
    static func run(output: URL) throws {
        guard output.pathExtension.lowercased() == "png",
              !FileManager.default.fileExists(atPath: output.path) else { throw MacTab5Error.invalidPacket }
        let registry = MacDeviceRegistry()
        guard registry.enabled("tab5") else { throw MacTab5Error.unavailable }
        let stored = UserDefaults.standard.integer(forKey: "status_port")
        let port = UInt16((1...65535).contains(stored) ? stored : 8765)
        guard let address = LocalNetworkIdentity.privateIPv4() else { throw MacTab5Error.unavailable }
        let service = MacTab5Service(port: port, enabled: { registry.enabled("tab5") },
            sourceEnabled: { registry.source($0, kind: "tab5") }, claudeSessions: MacClaudeSessionSampler())
        guard service.currentPairing?.confirmed == true else { throw MacTab5Error.pairingRequired }
        let usb = MacTab5USB(service: service, authenticationEndpoint: { (address, port) })
        let reader = SessionActivityReader()
        var lastPublish = -Double.infinity
        func pump() {
            let now = ProcessInfo.processInfo.systemUptime
            if now-lastPublish >= 0.5 {
                service.publish(registry.filter(reader.capture(), kind: "tab5")); lastPublish = now
            }
            RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.05))
        }
        usb.start()
        defer { service.screenPreview.stop(); usb.stopAndWait(); service.stop() }
        let deadline = ProcessInfo.processInfo.systemUptime+60
        while !service.usbActive, ProcessInfo.processInfo.systemUptime < deadline { pump() }
        guard service.usbActive, service.screenPreviewContext?.allowsCapture == true else { throw MacTab5Error.unavailable }
        let token = service.screenPreview.start(service.screenPreviewContext)
        while ProcessInfo.processInfo.systemUptime < deadline {
            service.screenPreview.renew(token, context: service.screenPreviewContext)
            pump()
            guard let frame = service.screenPreview.snapshot(token).0 else { continue }
            guard let image = MacTab5ScreenPreviewWindow.image(frame),
                  let bitmap = image.representations.first as? NSBitmapImageRep,
                  let png = bitmap.representation(using: .png, properties: [:]) else { throw MacTab5Error.unconfirmed }
            try png.write(to: output, options: .withoutOverwriting)
            print("TAB5_SCREEN_CAPTURE_OK \(frame.width)x\(frame.height); authenticated USB; no touch or settings change")
            return
        }
        throw MacTab5Error.timeout
    }
}
