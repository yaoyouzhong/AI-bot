import Foundation

// Short synthetic data probe using the existing authenticated benchmark. USB
// carries control/diagnostics only, with no status heartbeat or firmware bytes.
enum MacTab5BLELinkCheck {
    static func verified(_ result: String) -> Bool {
        guard result.hasPrefix("cancelled;") || result.hasPrefix("complete;") else { return false }
        let samples = result.split(separator: ";").map { $0.split(separator: ",") }
        return ["up", "down"].allSatisfy { direction in
            samples.contains { fields in
                fields.count >= 6 && fields[0] == "BLE" && fields[1] == direction &&
                fields[2] == "1" && fields[3] == "262144" &&
                (Int(fields[4]) ?? -1) >= 0 && fields[5] == "0"
            }
        }
    }
    static func run(mode: String, reportURL: URL) throws {
        guard ["normal", "active", "single-pdu", "confirmed", "yield8"].contains(mode) else { throw MacTab5Error.invalidPacket }
        let activity = mode == "active" ? ProcessInfo.processInfo.beginActivity(options: .userInitiatedAllowingIdleSystemSleep, reason: "TAB5 BLE acceptance") : nil
        defer { if let activity { ProcessInfo.processInfo.endActivity(activity) } }
        let stored = UserDefaults.standard.integer(forKey: "status_port")
        let port = UInt16((1...65535).contains(stored) ? stored : 8765)
        guard let address = LocalNetworkIdentity.privateIPv4() else { throw MacTab5Error.unavailable }
        let service = MacTab5Service(port: port), reader = SessionActivityReader()
        guard let pair = service.currentPairing, pair.confirmed else { throw MacTab5Error.pairingRequired }
        let bluetooth = MacTab5BLE(service: service, diagnosticSinglePDU: mode == "single-pdu", diagnosticWriteMode: mode)
        service.publish(reader.capture()); bluetooth.setEnabled(true)
        defer { bluetooth.stopAndWait(); service.stop() }
        var lastPublish = 0.0
        func pump() {
            let now = ProcessInfo.processInfo.systemUptime
            if now - lastPublish >= 1 { service.publish(reader.capture()); lastPublish = now }
            RunLoop.current.run(until: Date(timeIntervalSinceNow: 0.05))
        }
        var state = ""
        let connectUntil = ProcessInfo.processInfo.systemUptime + 60
        while ProcessInfo.processInfo.systemUptime < connectUntil {
            pump(); let next = service.connectionStatus.bluetooth
            if state != next { state = next; print("BLE " + next); fflush(stdout) }
            if service.bootTransport == "BLE" { break }
        }
        guard service.bootTransport == "BLE" else { throw MacTab5Error.unavailable }
        let settle = ProcessInfo.processInfo.systemUptime + 9
        while ProcessInfo.processInfo.systemUptime < settle { pump() }
        let control = try MacTab5BLEProbeControl(pair: pair, endpoint: (address, port))
        let hello = try control.snapshot()
        guard hello["active"] as? String == "BLE" else { throw MacTab5Error.unconfirmed }
        let initial = try control.command("status")
        guard let initialResult = initial["result"] as? String, !initialResult.hasPrefix("running;") else { throw MacTab5Error.busy }
        try service.beginBenchmark(gallery: false)
        defer { service.endBenchmark() }
        var started = false, finished = false
        defer { if started && !finished { _ = try? control.command("cancel") } }
        let before = bluetooth.transferStatistics, began = ProcessInfo.processInfo.systemUptime
        var rows: [[String: Any]] = [], lastResult = "", nextPoll = began
        started = true
        var reply = try control.command("start")
        while ProcessInfo.processInfo.systemUptime - began < 150 {
            pump()
            let now = ProcessInfo.processInfo.systemUptime
            guard now >= nextPoll else { continue }; nextPoll = now + 1
            guard let result = reply["result"] as? String, result.utf8.count <= 8192,
                  let radio = reply["bleRadio"] as? String, radio.split(separator: ",").count == 16,
                  radio.split(separator: ",").allSatisfy({ Int($0) != nil }) else { throw MacTab5Error.invalidPacket }
            let active = try control.snapshot()["active"] as? String
            // A long mailbox exchange can age the display heartbeat to Offline.
            // It must never switch data to USB/Wi-Fi during this isolated probe.
            guard active == "BLE" || active == "Offline" else { throw MacTab5Error.unconfirmed }
            let stats = bluetooth.transferStatistics
            rows.append(["seconds": now - began, "result": result, "radio": radio, "active": active ?? "unknown", "stats": stats])
            let report: [String: Any] = ["mode": mode, "firmware": service.connectionStatus.firmware, "before": before, "samples": rows, "finished": !result.hasPrefix("running;"), "verified": Self.verified(result)]
            try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: reportURL, options: .atomic)
            if result != lastResult { print(String(format: "%.1fs ", now - began) + result + " radio=" + radio); fflush(stdout); lastResult = result }
            if !result.hasPrefix("running;") {
                finished = true
                guard Self.verified(result) else { throw MacTab5Error.unconfirmed }
                return
            }
            // One completed down sample is enough to compare scheduling; drain
            // the worker's cancellation before ending the authenticated link.
            let completedDown = result.contains("BLE,down,1,262144,")
            reply = try control.command(completedDown ? "cancel" : "status")
        }
        throw MacTab5Error.timeout
    }
}
