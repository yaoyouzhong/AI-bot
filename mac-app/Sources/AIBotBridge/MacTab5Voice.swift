import Foundation

enum MacTab5SpeechFailure: Equatable {
    case noSpeech, assetsMissing, permission, unavailable, recognizerBusy, serviceInterrupted(Int), other(Int)
    case external(String)

    init(_ error: Error) {
        let error = error as NSError
        switch (error.domain, error.code) {
        case ("kAFAssistantErrorDomain", 1110): self = .noSpeech
        case ("kLSRErrorDomain", 102): self = .assetsMissing
        case ("kLSRErrorDomain", 201), ("kAFAssistantErrorDomain", 1700): self = .permission
        case ("kLSRErrorDomain", 300): self = .unavailable
        case ("kAFAssistantErrorDomain", 1100): self = .recognizerBusy
        case ("kAFAssistantErrorDomain", 1101), ("kAFAssistantErrorDomain", 1107): self = .serviceInterrupted(error.code)
        default: self = .other(error.code)
        }
    }

    var message: String {
        switch self {
        case .external(let message): return message
        case .noSpeech: return "没有识别到文字，请准备好后重录。"
        case .assetsMissing: return "中文本地识别资源未安装，请在 Mac 检查语音资源后重试。"
        case .permission: return "语音识别未获允许或听写已关闭，请在 Mac 检查权限。"
        case .unavailable: return "本地识别器未能启动，请稍后重新录入。"
        case .recognizerBusy: return "上一段本地识别仍在结束，请稍后重新录入。"
        case .serviceInterrupted(let code): return "Mac 本地识别服务连接中断（\(code)），请检查后重新录入。"
        case .other(let code): return "本地识别中断（错误码 \(code)），文字可能不完整，请重新录入。"
        }
    }
}

struct MacTab5SpeechBackend {
    var start: (String, @escaping (String, Bool, MacTab5SpeechFailure?) -> Void) throws -> Void
    var feed: ([Int16]) throws -> Void
    var finish: () -> Void
    var cancel: () -> Void
    var diagnostics: () -> String = { "" }
    var ready: () -> Bool = { true }
}

final class MacTab5Voice {
    var busy: Bool { lock.lock(); defer { lock.unlock() }; return ["recording","recognizing"].contains(state) }
    // Preserve transport scheduling until the device requests its terminal
    // result. Completion of recognition can precede that final poll.
    var needsTransportPriority: Bool {
        lock.lock(); defer { lock.unlock() }
        guard enabled() else { return false }
        return ["recording","recognizing"].contains(state) ||
            (!id.isEmpty && !terminalResponseServed && clock() >= seen && clock()-seen < 30)
    }
    private var terminalResponseServed = false
    private var usbTerminalReply: (nonce:String, voice:String)?
    static let enabledKey = "tab5_voice_enabled"
    static let agendaOwner = "a1b07a65-0000-4000-8000-00000000a5ed"
    private let lock = NSRecursiveLock()
    private let backend: MacTab5SpeechBackend, enabled: () -> Bool, clock: () -> TimeInterval
    private let usesDoubao: () -> Bool
    private var externalRecognition = false
    private var id = "", device = "", task = "", session = "", source = "tab5", state = "idle", text = "", message = ""
    private var started: TimeInterval = 0, seen: TimeInterval = 0, stopped: TimeInterval = 0
    private var sequence = 0, samples = 0, level = 0
    private var peak = 0
    private var silentSamples = 0
    private var lastReply = "", lastFailure = ""
    private var recentSessions: [String] = []
    private var hashes: [Int: String] = [:]
    // Before ready, device audio is a transport probe, not a recording. Do not
    // replay preparation time after the ready cue. Once capture has begun,
    // retain audio across a temporary readiness loss without dropping samples.
    private var captureWasReady = false, probeSamples = 0
    private var preparingAudio: [[Int16]] = []
    private var preparingSamples = 0
    private var isFinal = false
    private var stoppedByUser = false
    private var backendGeneration = UUID()
    private var lastSpeechFailure = "", endReason = ""
    private var captureRequested: TimeInterval = 0
    private var startupTime: TimeInterval?, firstResultTime: TimeInterval?, finalTime: TimeInterval?, reviewTime: TimeInterval?
    private var reviewReady: TimeInterval?
    private var finalWaiter: DispatchSemaphore?
    private var timer: DispatchSourceTimer?
    init(enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, backend: MacTab5SpeechBackend? = nil, usesDoubao: (() -> Bool)? = nil) {
        self.enabled = enabled; self.clock = clock; self.backend = backend ?? MacSpeechRouter().backend()
        self.usesDoubao = usesDoubao ?? (backend == nil ? { MacSpeechProvider.doubao } : { false })
        let timer = DispatchSource.makeTimerSource(queue: DispatchQueue(label: "AI-bot.voice-timeout")); timer.schedule(deadline: .now() + 1, repeating: 1)
        timer.setEventHandler { [weak self] in guard let self else { return }; self.lock.lock(); self.tick(); self.lock.unlock() }; self.timer = timer; timer.resume()
    }
    var isEnabled: Bool { enabled() }
    var summary: String {
        lock.lock(); defer { lock.unlock() }
        guard enabled() else { return "尚未启用" }
        switch state {
        case "recording": return source == "tab5" ? "正在收音 · TAB5 麦克风" : "正在收音 · Mac 默认麦克风"
        case "recognizing": return externalRecognition ? "等待在 Mac 确认豆包草稿":"收音已停止，正在本地识别"
        case "review": return text.isEmpty ? "没有识别到文字，请重新录入" : "识别已结束，请在 TAB5 检查文字"
        case "cancelled": return "已取消收音"
        case "error": return message
        default: return "已启用，等待 TAB5 发起收音"
        }
    }
    var diagnostics: String {
        lock.lock(); defer { lock.unlock() }
        guard enabled() else { return "" }
        var details: [String] = []
        let recognition = backend.diagnostics()
        if !recognition.isEmpty { details.append(recognition) }
        var timings: [String] = []
        for (label, value) in [("本地启动", startupTime), ("首段结果（含说话等待）", firstResultTime),
                               ("停止至最终识别", finalTime), ("最终结果至响应生成", reviewTime)] {
            if let value, value.isFinite { timings.append("\(label) \(Int((min(180, max(0, value)) * 1000).rounded())) ms") }
        }
        if !timings.isEmpty { details.append("本机分段计时：" + timings.joined(separator: " · ")) }
        if !id.isEmpty, source == "tab5" {
            details.append(String(format: "TAB5 音频：已接收 %d 块 · %.1f 秒 · 最近电平 %d%% · 峰值 %d%%", sequence, Double(samples) / 16000, level, peak))
            if probeSamples > 0 { details.append(String(format: "准备探测音频 %.1f 秒；提示可以说话后才正式收音", Double(probeSamples) / 16000)) }
            if preparingSamples > 0 { details.append(String(format: "等待豆包收音：已缓冲 %.1f 秒（上限 8 秒）",Double(preparingSamples)/16000)) }
        }
        if !lastReply.isEmpty { details.append("最近请求：" + lastReply) }
        if !lastFailure.isEmpty { details.append("最近拒绝：" + lastFailure) }
        if !endReason.isEmpty { details.append("最近结束：" + endReason) }
        if !lastSpeechFailure.isEmpty { details.append("最近识别异常：" + lastSpeechFailure) }
        if !recentSessions.isEmpty { details.append("此前语音（最近三次，不含文字）：\n" + recentSessions.reversed().joined(separator: "\n")) }
        return details.joined(separator: "\n")
    }
    func observeReply(op: String?, transport: String, status: Int, response: [String:Any]? = nil, nonce: String? = nil) {
        lock.lock(); defer { lock.unlock() }
        if status == 200, response?["voiceId"] as? String == id,
           let phase = response?["state"] as? String, ["review","error","cancelled"].contains(phase) {
            if transport == "USB", let nonce { usbTerminalReply = (nonce,id) }
            else if transport != "USB" { terminalResponseServed = true }
        }
        let channel = ["USB", "Wi-Fi", "BLE"].contains(transport) ? transport : "连接"
        let operation = ["start": "开始", "audio": "音频", "poll": "查询", "stop": "停止", "cancel": "取消", "tab5": "切换收音"][op ?? ""] ?? "请求"
        // Only fixed labels and bounded status numbers reach the UI, never
        // request bodies, identifiers, authentication material or transcripts.
        let reply = "\(channel) · \(operation) · \((100...599).contains(status) ? status : 0)"
        lastReply = reply
        if op == "start", status == 200 { lastFailure = "" }
        if status != 200 { lastFailure = reply }
    }
    deinit { timer?.cancel(); backend.cancel() }
    private func wakeFinalWaiter() { finalWaiter?.signal(); finalWaiter = nil }
    func stop() { lock.lock(); defer { lock.unlock() }; backend.cancel(); state = "cancelled"; text = ""; id = ""; clearPreparingAudio(); wakeFinalWaiter() }
    private func clearPreparingAudio() { preparingAudio.removeAll(); preparingSamples = 0 }
    // Called only after the serial owner receives every response-fragment ACK.
    // A generated/queued terminal reply does not yet release USB priority.
    func acknowledgeUSBReply(nonce: String) {
        lock.lock(); defer { lock.unlock() }
        guard let pending = usbTerminalReply, pending.nonce == nonce, pending.voice == id else { return }
        terminalResponseServed = true; usbTerminalReply = nil
    }
    private func forwardPreparedAudio() {
        guard state == "recording", !preparingAudio.isEmpty, backend.ready() else { return }
        do {
            for pcm in preparingAudio { try backend.feed(pcm) }
            clearPreparingAudio()
        } catch {
            clearPreparingAudio(); state = "error"; message = "豆包音频转送未完成，本段已停止"
            lastSpeechFailure = (error as? MacTab5LocalError)?.localizedDescription ?? message
            backend.cancel()
        }
    }
    private func tick() {
        if !enabled(), ["recording", "recognizing"].contains(state) { backend.cancel(); state = "cancelled"; text = "" }
        let now = clock()
        if state == "recording", now - started > 60 || now - seen > 8 {
            forwardPreparedAudio()
            guard state == "recording" else { return }
            if !preparingAudio.isEmpty {
                state = "error"; message = "豆包准备期间连接已结束，本段音频未完整转送"; lastSpeechFailure = message
                clearPreparingAudio(); backend.cancel(); return
            }
            endReason = now - started > 60 ? "达到一分钟录音上限" : "设备连接超过八秒无有效请求"
            state = "recognizing"; stopped = now; message = "收音已停止，等待最终文字"; backend.finish()
        }
        if state == "recognizing", now - stopped > (externalRecognition ? 120:15) { state = "error"; message = "识别结果未完整确认，请检查文字后重新录入"; lastSpeechFailure = message; backend.cancel() }
    }
    private func snapshot() -> MacTab5RPCResult {
        forwardPreparedAudio()
        if state != "recording" { clearPreparingAudio() }
        let ready = state == "recording" && backend.ready()
        if ready { captureWasReady = true }
        if externalRecognition, state == "recording" {
            message = ready ? "可以说话了" : captureWasReady ? "正在确认豆包收音状态，音频暂存中" : "正在准备豆包，提示可以说话后才开始录音"
        }
        // Measures Mac response generation, never device delivery/acknowledgment.
        if state == "review", reviewTime == nil, let reviewReady { reviewTime = clock() - reviewReady }
        return .init(200, ["voiceId": id, "taskId": task, "source": source, "sourceName": externalRecognition ? (source == "tab5" ? "TAB5 麦克风 · 豆包输入法":"Mac／DJI 麦克风 · 豆包输入法") : source == "tab5" ? "TAB5 麦克风 · Mac 本地识别" : "Mac 麦克风 · 本地识别", "state": state, "message": message, "text": text, "ready": ready, "level": state == "recording" ? level : 0, "silent": state == "recording" && silentSamples >= 3 * 16000, "canInsert": state == "review" && isFinal && !text.isEmpty])
    }
    private func resetTimings() {
        captureRequested = clock(); startupTime = nil; firstResultTime = nil; finalTime = nil; reviewTime = nil; reviewReady = nil
    }
    private func retainSessionDiagnostics() {
        guard !id.isEmpty else { return }
        let result = state == "review" ? (isFinal && !text.isEmpty ? "已生成最终文字" : "无最终文字") : state == "error" ? "失败" : "已取消"
        let parts = [externalRecognition ? "豆包" : "本地识别", source == "tab5" ? "TAB5 收音" : "Mac 收音", result,
                     "\(sequence) 块音频", lastReply, lastFailure, endReason, lastSpeechFailure, backend.diagnostics()].filter { !$0.isEmpty }
        recentSessions.append(parts.joined(separator: " · "))
        recentSessions = Array(recentSessions.suffix(3))
    }
    func handle(_ root: [String: Any], device: String, waitForFinal: Bool = false) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        guard enabled() else { if !id.isEmpty { backend.cancel(); state = "cancelled" }; return .error(403, "voice_not_enabled") }
        tick()
        guard let op = root["op"] as? String else { return .error(400, "invalid_voice_request") }
        if op == "start" {
            guard let task = MacTab5Codex.id(root["taskId"]), let session = root["session"] as? String else { return .error(400, "invalid_voice_owner") }
            if ["recording", "recognizing"].contains(state) { return .error(409, "voice_busy") }
            retainSessionDiagnostics()
            resetTimings()
            externalRecognition = usesDoubao()
            backend.cancel(); id = UUID().uuidString.lowercased(); self.device = device; self.task = task; self.session = session
            source = root["source"] as? String == "tab5" ? "tab5" : "pc"; state = "recording"; text = ""; message = externalRecognition ? "正在准备豆包与所选麦克风，确认实际收音后开始":"请开始说话"; isFinal = false; stoppedByUser = false
            terminalResponseServed = false; usbTerminalReply = nil; sequence = 0; samples = 0; level = 0; peak = 0; silentSamples = 0; hashes.removeAll(); clearPreparingAudio(); endReason = ""; lastSpeechFailure = ""; started = clock(); seen = started
            do { try beginBackend(); startupTime = clock() - captureRequested; return snapshot() }
            catch { state = "error"; message = externalRecognition ? (error as? MacTab5LocalError)?.localizedDescription ?? "豆包语音未能启动，请检查输入法和辅助功能权限。" : "本地语音不可用，请在 Mac 授权语音识别和麦克风，并确认该语言支持离线识别。"; backend.cancel(); return snapshot() }
        }
        guard !id.isEmpty, root["voiceId"] as? String == id, device == self.device, root["session"] as? String == session else { return .error(409, "voice_session_changed") }
        seen = clock()
        if op == "poll" { return snapshot() }
        if op == "cancel" { backend.cancel(); text = ""; state = "cancelled"; message = "已取消"; isFinal = false; wakeFinalWaiter(); return snapshot() }
        if op == "stop" {
            forwardPreparedAudio()
            if externalRecognition, state == "recording", !captureWasReady {
                state = "error"; message = "豆包尚未开始正式收音，请等可以说话后重新录入"; lastSpeechFailure = message
                clearPreparingAudio(); backend.cancel(); return snapshot()
            }
            if state == "recording", !preparingAudio.isEmpty {
                state = "error"; message = "豆包尚未就绪，本段音频未完整转送，请重新录入"; lastSpeechFailure = message
                clearPreparingAudio(); backend.cancel(); return snapshot()
            }
            if state == "recording" {
                let owner = id, generation = backendGeneration
                let waiter = waitForFinal ? DispatchSemaphore(value: 0) : nil
                finalWaiter = waiter
                state = "recognizing"; stoppedByUser = true; endReason = "设备请求停止收音"; stopped = clock(); message = externalRecognition ? "正在等待豆包停止收音并提交完整文字":"正在完成本地识别"; backend.finish()
                if let waiter, state == "recognizing" {
                    // BLE polling costs another radio round trip. Let a fast
                    // local final result join this reply, without holding the
                    // owner lock needed by recognition/cancel callbacks.
                    lock.unlock()
                    _ = waiter.wait(timeout: .now() + .milliseconds(200))
                    lock.lock()
                }
                if let waiter, finalWaiter === waiter { finalWaiter = nil }
                guard enabled() else { return .error(403, "voice_not_enabled") }
                guard id == owner, backendGeneration == generation else { return .error(409, "voice_session_changed") }
            }
            return snapshot()
        }
        // Recognition can finish between audio requests. Return its state to
        // the same owner so queued audio cannot turn it into transport recovery.
        if op == "audio", state != "recording" { return snapshot() }
        if op == "tab5", state == "recording", source == "pc", samples == 0, text.isEmpty {
            resetTimings()
            backend.cancel(); source = "tab5"
            do { try beginBackend(); startupTime = clock() - captureRequested; return snapshot() } catch { state = "error"; message = "TAB5 收音初始化失败"; return snapshot() }
        }
        guard op == "audio", state == "recording", source == "tab5", let seq64 = MacTab5Protocol.integer(root["seq"]), (0...600).contains(seq64),
              let encoded = root["pcm"] as? String, encoded.utf8.count <= 9000, let bytes = Data(base64Encoded: encoded) else { return .error(400, "invalid_voice_audio") }
        let seq = Int(seq64), hash = MacTab5Protocol.sha256(bytes + Data((root["codec"] as? String ?? "pcm").utf8))
        if seq < sequence { return hashes[seq] == hash ? snapshot() : .error(409, "voice_chunk_conflict") }
        guard seq == sequence else { return .error(409, "voice_audio_gap") }
        do {
            let pcm = try MacTab5VoiceAudio.decode(bytes, codec: root["codec"] as? String ?? "pcm")
            guard samples + pcm.count <= 60 * 16000 else { return .error(413, "voice_duration_exceeded") }
            if externalRecognition, !captureWasReady, !backend.ready() {
                // Acknowledge the device's preparation probes so it can reach
                // its ready cue, but never build a seconds-long playback lag.
                probeSamples += pcm.count
            } else if externalRecognition {
                captureWasReady = true
                guard preparingSamples + pcm.count <= 128000 else {
                    clearPreparingAudio(); state = "error"; message = "豆包准备超过音频缓冲上限，本段已停止"; lastSpeechFailure = message
                    backend.cancel(); return snapshot()
                }
                preparingAudio.append(pcm); preparingSamples += pcm.count
            } else { try backend.feed(pcm) }
            samples += pcm.count; sequence += 1; hashes[seq] = hash
            level = min(100, Int((pcm.map { abs(Int($0)) }.max() ?? 0) * 100 / 12000)); peak = max(peak, level)
            // Debounce by accepted audio duration, not request timing. Normal
            // pauses and duplicate chunks must not flash the no-sound hint.
            silentSamples = level == 0 ? silentSamples + pcm.count : 0
            return snapshot()
        } catch { state = "error"; message = "音频无效，本段已停止"; backend.cancel(); return .error(400, "invalid_voice_audio") }
    }
    private func beginBackend() throws {
        captureWasReady = false; probeSamples = 0; clearPreparingAudio()
        let owner = id, generation = UUID(); backendGeneration = generation
        try backend.start(source) { [weak self] result, done, error in
            guard let self else { return }; self.lock.lock(); defer { self.lock.unlock() }
            guard self.id == owner, self.backendGeneration == generation, ["recording", "recognizing"].contains(self.state) else { return }
            if !result.isEmpty, self.firstResultTime == nil { self.firstResultTime = self.clock() - self.captureRequested }
            if !result.isEmpty { self.text = Self.bounded(result, maximum: 2000) }
            if result.utf8.count > 2000 { self.state = "error"; self.message = "识别文字超过长度限制，请分段录入"; self.backend.cancel(); self.wakeFinalWaiter(); return }
            if let error {
                self.lastSpeechFailure = error.message
                // No speech after an explicit stop is an empty review, not a
                // broken connection. Never promote an unconfirmed partial.
                let emptyStop = error == .noSpeech && self.stoppedByUser && self.state == "recognizing" && self.text.isEmpty
                if emptyStop { self.finalTime = self.clock() - self.stopped; self.reviewReady = self.clock() }
                self.state = emptyStop ? "review" : "error"; self.isFinal = emptyStop
                self.message = error.message; self.backend.cancel()
            }
            else if done {
                let wasRecording = self.state == "recording"
                if self.state == "recognizing" { self.finalTime = self.clock() - self.stopped }
                self.reviewReady = self.clock()
                if wasRecording { self.endReason = "本地识别器结束收音" }
                self.isFinal = true; self.state = "review"; self.message = self.text.isEmpty ? "没有识别到文字，请重录" : "请检查文字，确认后再发送"
                if wasRecording { self.backend.finish() }
            }
            if !["recording", "recognizing"].contains(self.state) { self.wakeFinalWaiter() }
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
