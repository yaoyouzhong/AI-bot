import Foundation

// Explicit RAM-only comparison. The same production path serves authenticated
// 256KiB up/down samples; no firmware offer, flash write or mode change.
enum MacTab5USBLinkCheck {
    static func verified(_ result: String) -> Bool {
        guard result.hasPrefix("cancelled;") || result.hasPrefix("complete;") else { return false }
        let samples = result.split(separator:";").map { $0.split(separator:",") }
        return ["up","down"].allSatisfy { direction in
            samples.contains { $0.count >= 6 && $0[0] == "USB" && $0[1] == Substring(direction) &&
                $0[2] == "1" && $0[3] == "262144" && (Int($0[4]) ?? 0) > 0 && $0[5] == "0" }
        }
    }
    static func run(mode: String, reportURL: URL) throws {
        guard ["legacy","negotiated"].contains(mode) else { throw MacTab5Error.invalidPacket }
        let stored = UserDefaults.standard.integer(forKey:"status_port")
        let port = UInt16((1...65535).contains(stored) ? stored : 8765)
        guard let address = LocalNetworkIdentity.privateIPv4() else { throw MacTab5Error.unavailable }
        let service = MacTab5Service(port:port), reader = SessionActivityReader()
        guard service.currentPairing?.confirmed == true else { throw MacTab5Error.pairingRequired }
        let usb = MacTab5USB(service:service, authenticationEndpoint:{ (address,port) }, diagnosticLegacyMailbox:mode == "legacy")
        service.publish(reader.capture()); usb.start()
        defer { usb.stopAndWait(); service.stop() }
        var lastPublish = 0.0
        func pump() {
            let now = ProcessInfo.processInfo.systemUptime
            if now-lastPublish >= 1 { service.publish(reader.capture()); lastPublish = now }
            RunLoop.current.run(until:Date(timeIntervalSinceNow:0.05))
        }
        let until = ProcessInfo.processInfo.systemUptime+45
        while !service.usbActive, ProcessInfo.processInfo.systemUptime < until { pump() }
        guard service.usbActive else { throw MacTab5Error.unavailable }
        let settle = ProcessInfo.processInfo.systemUptime+9
        while ProcessInfo.processInfo.systemUptime < settle { pump() }
        func command(_ action: String) throws -> [String:Any] {
            let lock = NSLock(); var outcome: Result<[String:Any],Error>?
            usb.benchmark(action) { result in lock.lock(); outcome = result; lock.unlock() }
            let deadline = ProcessInfo.processInfo.systemUptime+10
            while ProcessInfo.processInfo.systemUptime < deadline {
                lock.lock(); let result = outcome; lock.unlock()
                if let result { return try result.get() }; pump()
            }
            throw MacTab5Error.timeout
        }
        guard let previous = try command("status")["result"] as? String, !previous.hasPrefix("running;") else { throw MacTab5Error.busy }
        try service.beginBenchmark(gallery:false)
        defer { service.endBenchmark() }
        var started = false, finished = false
        defer { if started && !finished { _ = try? command("cancel") } }
        let began = ProcessInfo.processInfo.systemUptime
        var rows: [[String:Any]] = [], nextPoll = began, previousResult = ""
        started = true
        var reply = try command("start")
        while ProcessInfo.processInfo.systemUptime-began < 120 {
            pump(); let now = ProcessInfo.processInfo.systemUptime
            guard now >= nextPoll else { continue }; nextPoll = now+1
            guard let result = reply["result"] as? String, result.utf8.count <= 8192,
                  !result.split(separator:";").contains(where:{ $0.hasPrefix("BLE,") || $0.hasPrefix("Wi-Fi,") }) else { throw MacTab5Error.unconfirmed }
            rows.append(["seconds":now-began,"result":result])
            try JSONSerialization.data(withJSONObject:["mode":mode,"firmware":service.connectionStatus.firmware,
                "samples":rows,"verified":verified(result)], options:[.prettyPrinted,.sortedKeys]).write(to:reportURL,options:.atomic)
            if result != previousResult { print(String(format:"%.1fs ",now-began)+result); fflush(stdout); previousResult = result }
            if !result.hasPrefix("running;") {
                finished = true; guard verified(result) else { throw MacTab5Error.unconfirmed }; return
            }
            reply = try command(result.contains("USB,down,1,262144,") ? "cancel" : "status")
        }
        throw MacTab5Error.timeout
    }
}
