import Foundation

struct MacTab5SpeechBackend {
    var start: (String, @escaping (String, Bool, Bool) -> Void) throws -> Void
    var feed: ([Int16]) throws -> Void
    var finish: () -> Void
    var cancel: () -> Void
}

final class MacTab5Voice {
    static let enabledKey = "tab5_voice_enabled"
    static let agendaOwner = "a1b07a65-0000-4000-8000-00000000a5ed"
    private let lock = NSRecursiveLock()
    private let backend: MacTab5SpeechBackend, enabled: () -> Bool, clock: () -> TimeInterval
    private var id = "", device = "", task = "", session = "", source = "tab5", state = "idle", text = "", message = ""
    private var started: TimeInterval = 0, seen: TimeInterval = 0, stopped: TimeInterval = 0
    private var sequence = 0, samples = 0, level = 0
    private var hashes: [Int: String] = [:]
    private var isFinal = false
    private var timer: DispatchSourceTimer?
    init(enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, backend: MacTab5SpeechBackend? = nil) {
        self.enabled = enabled; self.clock = clock; self.backend = backend ?? MacTab5Speech().backend()
        let timer = DispatchSource.makeTimerSource(queue: DispatchQueue(label: "AI-bot.voice-timeout")); timer.schedule(deadline: .now() + 1, repeating: 1)
        timer.setEventHandler { [weak self] in guard let self else { return }; self.lock.lock(); self.tick(); self.lock.unlock() }; self.timer = timer; timer.resume()
    }
    var isEnabled: Bool { enabled() }
    deinit { timer?.cancel(); backend.cancel() }
    func stop() { lock.lock(); defer { lock.unlock() }; backend.cancel(); state = "cancelled"; text = ""; id = "" }
    private func tick() {
        if !enabled(), ["recording", "recognizing"].contains(state) { backend.cancel(); state = "cancelled"; text = "" }
        let now = clock()
        if state == "recording", now - started > 60 || now - seen > 8 {
            state = "recognizing"; stopped = now; message = "收音已停止，等待最终文字"; backend.finish()
        }
        if state == "recognizing", now - stopped > 15 { state = "error"; message = "识别结果未完整确认，请检查文字后重新录入"; backend.cancel() }
    }
    private func snapshot() -> MacTab5RPCResult {
        .init(200, ["voiceId": id, "taskId": task, "source": source, "sourceName": source == "tab5" ? "TAB5 麦克风 · Mac 本地识别" : "Mac 默认麦克风 · 本地识别", "state": state, "message": message, "text": text, "ready": state == "recording", "level": state == "recording" ? level : 0, "silent": state == "recording" && samples > 16000 && level == 0, "canInsert": state == "review" && isFinal && !text.isEmpty])
    }
    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        guard enabled() else { if !id.isEmpty { backend.cancel(); state = "cancelled" }; return .error(403, "voice_not_enabled") }
        tick()
        guard let op = root["op"] as? String else { return .error(400, "invalid_voice_request") }
        if op == "start" {
            guard let task = MacTab5Codex.id(root["taskId"]), let session = root["session"] as? String else { return .error(400, "invalid_voice_owner") }
            if ["recording", "recognizing"].contains(state) { return .error(409, "voice_busy") }
            backend.cancel(); id = UUID().uuidString.lowercased(); self.device = device; self.task = task; self.session = session
            source = root["source"] as? String == "tab5" ? "tab5" : "pc"; state = "recording"; text = ""; message = "请开始说话"; isFinal = false
            sequence = 0; samples = 0; level = 0; hashes.removeAll(); started = clock(); seen = started
            do { try beginBackend(); return snapshot() }
            catch { state = "error"; message = "本地语音不可用，请在 Mac 授权语音识别和麦克风，并确认该语言支持离线识别。"; backend.cancel(); return snapshot() }
        }
        guard !id.isEmpty, root["voiceId"] as? String == id, device == self.device, root["session"] as? String == session else { return .error(409, "voice_session_changed") }
        seen = clock()
        if op == "poll" { return snapshot() }
        if op == "cancel" { backend.cancel(); text = ""; state = "cancelled"; message = "已取消"; isFinal = false; return snapshot() }
        if op == "stop" {
            if state == "recording" { state = "recognizing"; stopped = clock(); message = "正在完成本地识别"; backend.finish() }
            return snapshot()
        }
        if op == "tab5", state == "recording", source == "pc", samples == 0, text.isEmpty {
            backend.cancel(); source = "tab5"
            do { try beginBackend(); return snapshot() } catch { state = "error"; message = "TAB5 收音初始化失败"; return snapshot() }
        }
        guard op == "audio", state == "recording", source == "tab5", let seq64 = MacTab5Protocol.integer(root["seq"]), (0...600).contains(seq64),
              let encoded = root["pcm"] as? String, encoded.utf8.count <= 9000, let bytes = Data(base64Encoded: encoded) else { return .error(400, "invalid_voice_audio") }
        let seq = Int(seq64), hash = MacTab5Protocol.sha256(bytes + Data((root["codec"] as? String ?? "pcm").utf8))
        if seq < sequence { return hashes[seq] == hash ? snapshot() : .error(409, "voice_chunk_conflict") }
        guard seq == sequence else { return .error(409, "voice_audio_gap") }
        do {
            let pcm = try MacTab5VoiceAudio.decode(bytes, codec: root["codec"] as? String ?? "pcm")
            guard samples + pcm.count <= 60 * 16000 else { return .error(413, "voice_duration_exceeded") }
            try backend.feed(pcm); samples += pcm.count; sequence += 1; hashes[seq] = hash
            level = min(100, Int((pcm.map { abs(Int($0)) }.max() ?? 0) * 100 / 12000)); return snapshot()
        } catch { state = "error"; message = "音频无效，本段已停止"; backend.cancel(); return .error(400, "invalid_voice_audio") }
    }
    private func beginBackend() throws {
        let owner = id
        try backend.start(source) { [weak self] result, done, error in
            guard let self else { return }; self.lock.lock(); defer { self.lock.unlock() }
            guard self.id == owner, ["recording", "recognizing"].contains(self.state) else { return }
            if !result.isEmpty { self.text = Self.bounded(result, maximum: 2000) }
            if result.utf8.count > 2000 { self.state = "error"; self.message = "识别文字超过长度限制，请分段录入"; self.backend.cancel(); return }
            if error { self.state = "error"; self.message = "识别中断，文字可能不完整，请检查后重新录入" }
            else if done { self.isFinal = true; self.state = "review"; self.message = self.text.isEmpty ? "没有识别到文字，请重录" : "请检查文字，确认后再发送" }
        }
    }
    static func bounded(_ text: String, maximum: Int) -> String { var result = ""; for char in text { let next = String(char); if result.utf8.count + next.utf8.count > maximum { break }; result += next }; return result }
}

enum MacTab5VoiceAudio {
    private static let steps = [7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767]
    static func decode(_ data: Data, codec: String) throws -> [Int16] {
        let bytes = [UInt8](data)
        if codec == "pcm" {
            guard !bytes.isEmpty, bytes.count <= 6400, bytes.count % 2 == 0 else { throw MacTab5Error.invalidPacket }
            return stride(from: 0, to: bytes.count, by: 2).map { Int16(bitPattern: UInt16(bytes[$0]) | UInt16(bytes[$0 + 1]) << 8) }
        }
        guard ["ima-adpcm", "ima-adpcm8k"].contains(codec), bytes.count >= 6 else { throw MacTab5Error.invalidPacket }
        let count = Int(bytes[0]) | Int(bytes[1]) << 8
        guard (1...3200).contains(count), bytes.count == 6 + count / 2, bytes[4] <= 88, bytes[5] == 0 else { throw MacTab5Error.invalidPacket }
        var predictor = Int(Int16(bitPattern: UInt16(bytes[2]) | UInt16(bytes[3]) << 8)), index = Int(bytes[4]), pcm = [Int16(predictor)]
        for sample in 1..<count {
            let nibble = Int(bytes[6 + (sample - 1) / 2] >> ((sample - 1) % 2 * 4)) & 15, step = steps[index]
            var delta = step >> 3; if nibble & 4 != 0 { delta += step }; if nibble & 2 != 0 { delta += step >> 1 }; if nibble & 1 != 0 { delta += step >> 2 }
            predictor = max(-32768, min(32767, predictor + (nibble & 8 != 0 ? -delta : delta)))
            index = max(0, min(88, index + [-1,-1,-1,-1,2,4,6,8][nibble & 7])); pcm.append(Int16(predictor))
        }
        return codec == "ima-adpcm8k" ? pcm.flatMap { [$0, $0] } : pcm
    }
}
