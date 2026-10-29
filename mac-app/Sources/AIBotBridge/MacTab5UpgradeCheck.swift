import Foundation

// Explicit local acceptance runner. It uses the production transport and the
// existing pairing; no enrollment, preference changes, or synthetic device data.
enum MacTab5UpgradeCheck {
    static func run(mode: String, imageURL: URL, reportURL: URL) throws {
        guard ["usb", "wifi", "wifi-boot", "ble", "ble-race"].contains(mode) else { throw MacTab5Error.invalidPacket }
        let isBLE = mode.hasPrefix("ble")
        let transport = mode == "usb" ? "USB" : isBLE ? "BLE" : "Wi-Fi"
        let image = try MacTab5FirmwareImage.load(imageURL)
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        let port = UInt16((1...65535).contains(storedPort) ? storedPort : 8765)
        let service = MacTab5Service(port: port)
        guard let pair = service.currentPairing, pair.confirmed else { throw MacTab5Error.pairingRequired }
        try service.firmwareControl.select(image)
        if mode == "ble-race" { service.firmwareControl.diagnosticStartedDelay = 1 }
        // USB isolation stops the listener, not the saved endpoint. The receiver
        // persists the endpoint supplied by host authentication even when empty.
        guard let address = LocalNetworkIdentity.privateIPv4(), PrivateIPv4Address.isValid(address) else { throw MacTab5Error.unavailable }
        let usb = MacTab5USB(service: service, authenticationEndpoint: { (address, port) })
        let server = HTTPStatusServer(port: port, token: { nil }, tab5: service, espEnabled: { false }, snapshot: { Data() })
        let discovery = MacTab5Discovery(service: service)
        let bluetooth = MacTab5BLE(service: service)
        let reader = SessionActivityReader()
        service.publish(reader.capture())
        if mode == "usb" { usb.start() }
        else if isBLE { bluetooth.setEnabled(true) }
        else { try server.start(); try discovery.start() }
        defer { bluetooth.stopAndWait(); usb.stopAndWait(); discovery.stop(); server.stop(); service.stop() }
        var lastPublish = 0.0
        func pump() {
            let now = ProcessInfo.processInfo.systemUptime
            if now - lastPublish >= 1 { service.publish(reader.capture()); lastPublish = now }
            RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.1))
        }
        let connectDeadline = ProcessInfo.processInfo.systemUptime + 60
        var previousConnection = ""
        while ProcessInfo.processInfo.systemUptime < connectDeadline {
            pump()
            let connection = isBLE ? service.connectionStatus.bluetooth : transport
            if connection != previousConnection { previousConnection = connection; print("CONNECT " + connection); fflush(stdout) }
            if (mode == "usb" ? service.usbActive : service.bootTransport == transport), !service.connectionStatus.firmware.isEmpty { break }
        }
        guard mode == "usb" ? service.usbActive : service.bootTransport == transport else { throw MacTab5Error.unavailable }
        // Let the previous transport's eight-second heartbeat lease expire.
        let settle = ProcessInfo.processInfo.systemUptime + 9
        while ProcessInfo.processInfo.systemUptime < settle { pump() }
        func settings() throws -> MacTab5DisplaySettings {
            let lock = NSLock(); var outcome: Result<MacTab5DisplaySettings, Error>?
            service.displaySettings(nil) { result in lock.lock(); outcome = result; lock.unlock() }
            let deadline = ProcessInfo.processInfo.systemUptime + 30
            while ProcessInfo.processInfo.systemUptime < deadline {
                lock.lock(); let result = outcome; lock.unlock()
                if let result { return try result.get() }
                pump()
            }
            throw MacTab5Error.timeout
        }
        print("CONNECTED " + transport + "; reading display settings"); fflush(stdout)
        let before = try settings()
        // Explicit diagnostics: USB only reads a numeric trace; no USB status
        // heartbeat, authentication mutation or image bytes are sent.
        var traceControl = isBLE ? try? MacTab5BLEProbeControl(pair: pair, endpoint: (address, port)) : nil
        let initialTrace = try? traceControl?.otaTrace()
        var deviceTraces: [[String: Any]] = [], nextTrace = 0.0
        let began = ProcessInfo.processInfo.systemUptime
        if mode == "wifi-boot" { try service.firmwareControl.beginBootCheck(device: pair.deviceId, transport: "Wi-Fi") }
        else { try service.requestUpgrade() }
        var previous = "", progress: [[String: Any]] = [], connections: [[String: Any]] = []
        while ProcessInfo.processInfo.systemUptime - began < (mode == "wifi-boot" ? 45 : 1200) {
            pump()
            if isBLE {
                let state = service.connectionStatus.bluetooth
                if state != previousConnection {
                    previousConnection = state
                    let seconds = ProcessInfo.processInfo.systemUptime - began
                    connections.append(["seconds": seconds, "state": state])
                    print(String(format: "%.1fs BLE ", seconds) + state); fflush(stdout)
                }
            }
            if mode != "wifi-boot", service.firmwareControl.beginAutomaticBootCheck(device: pair.deviceId, transport: transport) {
                traceControl = nil // Boot validation uses the selected transport without diagnostic CDC polling.
                if mode == "usb" { usb.verifyBoot(prepared: true) { _ in } }
            }
            let summary = service.firmwareControl.summary
            let elapsed = ProcessInfo.processInfo.systemUptime - began
            if traceControl != nil && elapsed >= nextTrace {
                nextTrace = elapsed + 1
                if let trace = try? traceControl?.otaTrace() {
                    deviceTraces.append(["seconds": elapsed, "trace": trace])
                    if MacTab5Protocol.integer(trace["stage"]) == 10,
                       initialTrace.map({ initial in ["stage", "offset", "total", "error"].contains { MacTab5Protocol.integer(initial[$0]) != MacTab5Protocol.integer(trace[$0]) } }) ?? true {
                        let value: [String: Any] = ["mode": mode, "version": image.version, "sha256": image.sha256,
                            "verified": false, "terminalDeviceFailure": trace, "deviceTraces": deviceTraces,
                            "progress": progress, "bleTransfer": bluetooth.transferStatistics]
                        try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]).write(to: reportURL, options: .atomic)
                        throw MacTab5Error.unconfirmed
                    }
                } else { traceControl = nil } // Reboot can remove CDC; radio verification remains authoritative.
            }
            if summary != previous {
                previous = summary
                progress.append(["seconds": ProcessInfo.processInfo.systemUptime - began, "state": summary])
                print(String(format: "%.1fs ", ProcessInfo.processInfo.systemUptime - began) + summary)
                fflush(stdout)
            }
            if summary.contains("自动启动核验未完成") {
                let value: [String: Any] = ["mode": mode, "version": image.version, "sha256": image.sha256,
                    "verified": false, "bootVerificationFailed": true, "deviceTraces": deviceTraces,
                    "progress": progress, "bleTransfer": bluetooth.transferStatistics]
                try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]).write(to: reportURL, options: .atomic)
                throw MacTab5Error.unconfirmed
            }
            if summary.contains("启动核验通过") {
                let elapsed = ProcessInfo.processInfo.systemUptime - began
                let after = try settings()
                let value: [String: Any] = ["mode": mode, "version": image.version, "sha256": image.sha256,
                    "elfSha256": image.elfSHA256, "elapsedThroughBootVerification": elapsed,
                    "settingsPreserved": before == after, "progress": progress, "verified": true,
                    "bleTransfer": isBLE ? bluetooth.transferStatistics : [:], "connections": connections, "deviceTraces": deviceTraces]
                try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]).write(to: reportURL, options: .atomic)
                guard before == after else { throw MacTab5Error.unconfirmed }
                return
            }
        }
        throw MacTab5Error.timeout
    }
}
