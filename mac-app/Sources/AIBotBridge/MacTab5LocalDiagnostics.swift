import Foundation

// Current metadata records, owned by this user. Never accept message text,
// task/request IDs, file paths, credentials or arbitrary provider errors.
final class MacTab5LocalDiagnostics {
    static let shared = MacTab5LocalDiagnostics()
    struct Record: Codable {
        let time: Double, pid: Int32
        let status: String, detail: String
    }
    private let queue = DispatchQueue(label: "AI-bot.local-diagnostics")
    private let file: MacProfileFile<[String: Record]>
    private let deviceFile: MacProfileFile<[String: String]>
    private let voiceFile: MacProfileFile<[String: Int]>
    private let clock: () -> TimeInterval
    private var lastDeviceWrite: TimeInterval = 0 // diagnostics queue only
    private var lastVoiceWrite = -Double.infinity, lastVoiceState: Int?
    private static let deviceNumericKeys = ["uiPage", "uptimeMs", "uiAgeMs", "uiDataAgeMs",
        "internalFree", "psramFree", "saverActive", "pcWakeCount", "lcdUnderruns",
        "flushCount", "flushErrors", "backlightPercent", "backlightChanges"]
    private static let deviceTextKeys = ["displayDiag", "galleryDiag", "usbDiag"]
    private static let voiceBounds = ["state": 5, "chunks": 601, "audioMs": 60000, "startupMs": 180000,
        "captureReadyMs": 180000, "firstResultMs": 180000, "stopToFinalMs": 180000, "finalToResponseMs": 180000]
    init(directory: URL? = nil, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) {
        self.clock = clock
        voiceFile = MacProfileFile("tab5-voice-diagnostics.json", directory: directory, initial: { [:] }, validate: {
            guard $0.count <= 10, $0.allSatisfy({ key, value in
                if key == "pid" || key == "time" { return value >= 0 }
                return Self.voiceBounds[key].map { (0...$0).contains(value) } ?? false
            }) else { throw MacTab5Error.invalidPacket }
        })
        deviceFile = MacProfileFile("tab5-device-diagnostics.json", directory: directory, initial: { [:] }, validate: {
            let allowed = Set(Self.deviceNumericKeys + Self.deviceTextKeys + ["pid", "time"])
            guard Set($0.keys).isSubset(of: allowed) else { throw MacTab5Error.invalidPacket }
        })
        assetFile = MacProfileFile<[String: Int]>("tab5-cover-diagnostics.json", directory: directory, initial: { [:] }, validate: {
            guard $0.count <= 14, Set($0.keys).isSubset(of: ["width","height","bytes","offset","passes","jpeg","confirmed","limited","usbChunks","generation","acknowledgements","pid","time"]) else { throw MacTab5Error.invalidPacket }
        })
        file = MacProfileFile("tab5-desktop-diagnostics.json", directory: directory, initial: { [:] }, validate: {
            guard $0.count <= 3, Set($0.keys).isSubset(of: ["desktop", "request", "transport"]),
                  $0.values.allSatisfy({ $0.status.utf8.count <= 500 && $0.detail.utf8.count <= 3000 && $0.time.isFinite }) else { throw MacTab5Error.invalidPacket }
        })
    }
    func desktop(_ check: MacTab5DesktopCheck, evidence: String) {
        let safe = evidence.range(of: "^[A-Za-z0-9_.,=: >-]*$", options: .regularExpression) != nil
        record("desktop", status: check.rawValue, detail: safe ? evidence : "structure-unavailable")
    }
    func request(_ label: String, status: Int, outcome: String?) {
        guard ["发送消息", "追加消息", "查询送达", "确认送达", "提交草稿", "定位会话"].contains(label) else { return }
        // outcome is already allowlisted by MacTab5InteractionDiagnostics.
        let allowed = ["composer_changed", "delivery_baseline_unavailable", "desktop_storage_unavailable", "native_submit_unavailable", "native_input_unavailable", "native_session_unavailable", "native_session_changed", "native_history_or_state_unconfirmed", "pending_delivery_requires_receipt", "conversation_target_or_text_changed", "question_target_or_text_changed", "desktop_draft_present", "desktop_attachments_present", "desktop_photo_append_unsupported", "pending_delivery_changed", "task_waiting_for_input", "task_busy_or_state_unknown", "turn_changed",
            "delivery_unknown", "receipt_not_found", "request_id_conflict", "desktop_result_unknown", "desktop_foreground_unconfirmed",
            "desktop_steer_requires_expected_turn_support", "codex_or_private_storage_unavailable", "codex_catalog_stale",
            "codex_send_not_enabled", "invalid_request", "codex_read_only_connection"]
        record("request", status: label + " / " + String((100...599).contains(status) ? status : 0),
               detail: outcome.map { allowed.contains($0) ? $0 : "unclassified" } ?? "no-error-code")
    }
    // Fixed numeric metadata only: no artwork, song names, IDs or credentials.
    private let assetFile: MacProfileFile<[String: Int]>
    func cover(_ values: [String: Int]) {
        var safe = values
        safe["pid"] = Int(ProcessInfo.processInfo.processIdentifier); safe["time"] = Int(Date().timeIntervalSince1970)
        let sample = safe
        queue.async { [assetFile] in try? assetFile.write(sample) }
    }
    func transport(_ note: String) { record("transport", status: "USB failure", detail: note) }
    func voice(_ values: [String: Int]) {
        let safe = values.filter { key, value in Self.voiceBounds[key].map { (0...$0).contains(value) } ?? false }
        guard let state = safe["state"] else { return }
        queue.async { [self] in
            let now = clock()
            guard state != lastVoiceState || now - lastVoiceWrite >= 1 else { return }
            var sample = safe
            sample["pid"] = Int(ProcessInfo.processInfo.processIdentifier); sample["time"] = Int(Date().timeIntervalSince1970)
            if (try? voiceFile.write(sample)) != nil { lastVoiceWrite = now; lastVoiceState = state }
        }
    }
    func device(_ root: [String: Any]) {
        guard root["type"] as? String == "tab5_ack",
              MacTab5Protocol.integer(root["uptimeMs"]) != nil else { return }
        var values: [String: String] = [:]
        for key in Self.deviceNumericKeys {
            if let value = MacTab5Protocol.integer(root[key]) { values[key] = String(value) }
        }
        for key in Self.deviceTextKeys {
            if let value = root[key] as? String, value.utf8.count <= 1024,
               value.range(of: "^[A-Za-z0-9,;_.:/ -]*$", options: .regularExpression) != nil { values[key] = value }
        }
        values["pid"] = String(ProcessInfo.processInfo.processIdentifier)
        values["time"] = String(Int(Date().timeIntervalSince1970))
        let sample = values
        queue.async { [self] in
            let now = ProcessInfo.processInfo.systemUptime
            guard now - lastDeviceWrite >= 1 else { return }
            lastDeviceWrite = now; try? deviceFile.write(sample)
        }
    }
    func waitUntilIdle() { queue.sync {} }
    private func record(_ kind: String, status: String, detail: String) {
        let value = Record(time: Date().timeIntervalSince1970, pid: ProcessInfo.processInfo.processIdentifier, status: status, detail: detail)
        queue.async { [file] in try? file.update { $0[kind] = value } }
    }
}
