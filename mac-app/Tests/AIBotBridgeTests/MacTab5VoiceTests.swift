import XCTest
@testable import AIBotBridge

final class MacTab5VoiceTests: XCTestCase {
    private final class Backend {
        var callback: ((String, Bool, MacTab5SpeechFailure?) -> Void)?, feeds = 0, finishes = 0, cancels = 0
        func backend() -> MacTab5SpeechBackend { .init(start: { _, callback in self.callback = callback }, feed: { _ in self.feeds += 1 }, finish: { self.finishes += 1 }, cancel: { self.cancels += 1 }) }
    }
    private let task = UUID().uuidString.lowercased(), session = "session"
    private func start(_ voice: MacTab5Voice) -> String { voice.handle(["op": "start", "taskId": task, "session": session, "source": "tab5"], device: "A").body["voiceId"] as! String }
    private func request(_ op: String, _ id: String) -> [String: Any] { ["op": op, "voiceId": id, "session": session] }
    func testExplicitConsentAndDeviceSessionBinding() {
        let backend = Backend(); var enabled = false
        let voice = MacTab5Voice(enabled: { enabled }, backend: backend.backend())
        XCTAssertEqual(voice.handle(["op": "start", "taskId": task, "session": session], device: "A").status, 403)
        enabled = true; let id = start(voice)
        XCTAssertEqual(voice.handle(request("poll", id), device: "B").status, 409)
        var wrong = request("poll", id); wrong["session"] = "another"; XCTAssertEqual(voice.handle(wrong, device: "A").status, 409)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["state"] as? String, "recording")
    }
    func testChunkOrderAndDuplicateCannotFeedTwice() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        var audio = request("audio", id); audio["seq"] = 0; audio["pcm"] = Data(repeating: 0, count: 640).base64EncodedString()
        XCTAssertEqual(voice.handle(audio, device: "A").status, 200); XCTAssertEqual(voice.handle(audio, device: "A").status, 200); XCTAssertEqual(backend.feeds, 1)
        audio["seq"] = 2; XCTAssertEqual(voice.handle(audio, device: "A").status, 409)
        audio["seq"] = 0; audio["pcm"] = Data(repeating: 1, count: 640).base64EncodedString(); XCTAssertEqual(voice.handle(audio, device: "A").status, 409)
    }
    func testFinalRecognitionIsReviewOnlyAndCancelDiscardsLateCallbacks() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        backend.callback?("待确认文字", false, nil)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["canInsert"] as? Bool, false)
        _ = voice.handle(request("stop", id), device: "A"); _ = voice.handle(request("stop", id), device: "A"); XCTAssertEqual(backend.finishes, 1)
        backend.callback?("最终文字", true, nil)
        let review = voice.handle(request("poll", id), device: "A"); XCTAssertEqual(review.body["state"] as? String, "review"); XCTAssertEqual(review.body["canInsert"] as? Bool, true)
        _ = voice.handle(request("cancel", id), device: "A"); backend.callback?("晚到结果", true, nil)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["text"] as? String, "")
    }
    func testTimeoutStopsCaptureAndUnconfirmedPartialRemainsError() {
        let backend = Backend(); var now: TimeInterval = 0
        let voice = MacTab5Voice(enabled: { true }, clock: { now }, backend: backend.backend()), id = start(voice)
        backend.callback?("部分文字", false, nil); now = 9
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["state"] as? String, "recognizing"); XCTAssertEqual(backend.finishes, 1)
        now = 25; let result = voice.handle(request("poll", id), device: "A")
        XCTAssertEqual(result.body["state"] as? String, "error"); XCTAssertEqual(result.body["text"] as? String, "部分文字"); XCTAssertEqual(result.body["canInsert"] as? Bool, false)
    }
    func testIMAIndependentBlocksAndEightKHzUpsampling() throws {
        let bytes = Data([4, 0, 0, 0, 0, 0, 0x77, 0x07])
        XCTAssertEqual(try MacTab5VoiceAudio.decode(bytes, codec: "ima-adpcm"), [0, 11, 41, 104])
        XCTAssertEqual(try MacTab5VoiceAudio.decode(bytes, codec: "ima-adpcm8k"), [0,0,11,11,41,41,104,104])
        XCTAssertThrowsError(try MacTab5VoiceAudio.decode(Data(bytes.dropLast()), codec: "ima-adpcm"))
        XCTAssertThrowsError(try MacTab5VoiceAudio.decode(Data([0]), codec: "pcm"))
        XCTAssertThrowsError(try MacTab5VoiceAudio.decode(bytes, codec: "unknown"))
    }
    func testOversizedTranscriptCannotBecomeCompleteDraft() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        backend.callback?(String(repeating: "字", count: 1000), true, nil)
        let result = voice.handle(request("poll", id), device: "A")
        XCTAssertEqual(result.body["state"] as? String, "error"); XCTAssertEqual(result.body["canInsert"] as? Bool, false)
        XCTAssertLessThanOrEqual((result.body["text"] as! String).utf8.count, 2000)
    }
    func testRecognizerFinalOrErrorAlwaysReleasesCapture() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend())
        let first = start(voice); backend.callback?("自然结束", true, nil)
        XCTAssertEqual(backend.finishes, 1); XCTAssertEqual(voice.handle(request("poll", first), device: "A").body["state"] as? String, "review")
        let second = start(voice), before = backend.cancels; backend.callback?("", false, .other(0))
        XCTAssertEqual(backend.cancels, before + 1); XCTAssertEqual(voice.handle(request("poll", second), device: "A").body["state"] as? String, "error")
    }

    func testNoSpeechAfterStopIsEmptyReview() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        _ = voice.handle(request("stop", id), device: "A")
        backend.callback?("", false, .noSpeech)
        let result = voice.handle(request("poll", id), device: "A")
        XCTAssertEqual(result.body["state"] as? String, "review")
        XCTAssertEqual(result.body["canInsert"] as? Bool, false)
        XCTAssertTrue((result.body["message"] as? String)?.contains("没有识别到文字") == true)
    }

    func testNoSpeechDoesNotConfirmPartialOrHidePrematureEnd() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend())
        let first = start(voice); backend.callback?("", false, .noSpeech)
        XCTAssertEqual(voice.handle(request("poll", first), device: "A").body["state"] as? String, "error")
        let second = start(voice); backend.callback?("未完成", false, nil)
        _ = voice.handle(request("stop", second), device: "A"); backend.callback?("", false, .noSpeech)
        let result = voice.handle(request("poll", second), device: "A")
        XCTAssertEqual(result.body["state"] as? String, "error")
        XCTAssertEqual(result.body["text"] as? String, "未完成")
        XCTAssertEqual(result.body["canInsert"] as? Bool, false)
    }

    func testAppleSpeechErrorsAreClassifiedByDomainAndCode() {
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kAFAssistantErrorDomain", code: 1110)), .noSpeech)
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kLSRErrorDomain", code: 102)), .assetsMissing)
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kLSRErrorDomain", code: 201)), .permission)
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kAFAssistantErrorDomain", code: 1700)), .permission)
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kLSRErrorDomain", code: 300)), .unavailable)
        XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kAFAssistantErrorDomain", code: 1100)), .recognizerBusy)
        for code in [1101, 1107] { XCTAssertEqual(MacTab5SpeechFailure(NSError(domain: "kAFAssistantErrorDomain", code: code)), .serviceInterrupted(code)) }
        let unknown = MacTab5SpeechFailure(NSError(domain: "unrelated", code: 1110, userInfo: [NSLocalizedDescriptionKey: "不得显示的原始错误内容"]))
        XCTAssertEqual(unknown, .other(1110))
        XCTAssertFalse(unknown.message.contains("不得显示的原始错误内容"))
    }

    func testNoSpeechAfterConnectionTimeoutIsNotManualEmptyReview() {
        let backend = Backend(); var now: TimeInterval = 0
        let voice = MacTab5Voice(enabled: { true }, clock: { now }, backend: backend.backend()), id = start(voice)
        now = 9; _ = voice.handle(request("poll", id), device: "A")
        backend.callback?("", false, .noSpeech)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["state"] as? String, "error")
    }

    func testSpeechSummaryNeverIncludesTranscriptAndFollowsOptOut() {
        let backend = Backend(); var enabled = true
        let voice = MacTab5Voice(enabled: { enabled }, backend: backend.backend()), id = start(voice)
        XCTAssertTrue(voice.summary.contains("正在收音"))
        _ = voice.handle(request("stop", id), device: "A")
        XCTAssertTrue(voice.summary.contains("正在本地识别"))
        backend.callback?("只应在设备预览的文字", true, nil)
        XCTAssertFalse(voice.summary.contains("只应在设备预览的文字"))
        XCTAssertTrue(voice.summary.contains("请在 TAB5 检查"))
        enabled = false; XCTAssertEqual(voice.summary, "尚未启用")
    }

    func testInFlightAudioPreservesRecognizerFinalAndError() {
        for failure: MacTab5SpeechFailure? in [nil, .serviceInterrupted(1107)] {
            let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
            var audio = request("audio", id); audio["seq"] = 0; audio["pcm"] = Data(repeating: 0, count: 640).base64EncodedString()
            XCTAssertEqual(voice.handle(audio, device: "A").status, 200)
            backend.callback?("识别文字", failure == nil, failure)
            audio["seq"] = 1
            let result = voice.handle(audio, device: "A")
            XCTAssertEqual(result.status, 200)
            XCTAssertEqual(result.body["state"] as? String, failure == nil ? "review" : "error")
            XCTAssertEqual(result.body["canInsert"] as? Bool, failure == nil)
            XCTAssertEqual(result.body["text"] as? String, "识别文字")
            if failure != nil { XCTAssertTrue((result.body["message"] as? String)?.contains("1107") == true) }
            XCTAssertEqual(backend.feeds, 1)
        }
    }

    func testInFlightAudioAfterStopOrCancelDoesNotResumeCapture() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        var audio = request("audio", id); audio["seq"] = 0; audio["pcm"] = Data(repeating: 0, count: 640).base64EncodedString()
        _ = voice.handle(request("stop", id), device: "A")
        let stopped = voice.handle(audio, device: "A")
        XCTAssertEqual(stopped.status, 200); XCTAssertEqual(stopped.body["state"] as? String, "recognizing")
        backend.callback?("尚未确认", false, nil)
        _ = voice.handle(request("cancel", id), device: "A")
        let cancelled = voice.handle(audio, device: "A")
        XCTAssertEqual(cancelled.status, 200); XCTAssertEqual(cancelled.body["state"] as? String, "cancelled")
        XCTAssertEqual(cancelled.body["text"] as? String, ""); XCTAssertEqual(cancelled.body["canInsert"] as? Bool, false)
        XCTAssertEqual(backend.feeds, 0); XCTAssertEqual(backend.finishes, 1)
    }

    func testTerminalAudioStillRequiresConsentAndExactOwner() {
        let backend = Backend(); var enabled = true
        let voice = MacTab5Voice(enabled: { enabled }, backend: backend.backend()), id = start(voice)
        backend.callback?("最终文字", true, nil)
        var audio = request("audio", id); audio["seq"] = 0; audio["pcm"] = Data(repeating: 0, count: 640).base64EncodedString()
        XCTAssertEqual(voice.handle(audio, device: "B").status, 409)
        var wrong = audio; wrong["session"] = "other"
        XCTAssertEqual(voice.handle(wrong, device: "A").status, 409)
        _ = start(voice)
        XCTAssertEqual(voice.handle(audio, device: "A").status, 409)
        enabled = false
        XCTAssertEqual(voice.handle(audio, device: "A").status, 403)
        XCTAssertEqual(backend.feeds, 0)
    }

    func testCaptureDiagnosticsCountOnlyAcceptedAudioAndRetainPeak() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend()), id = start(voice)
        var audio = request("audio", id); audio["seq"] = 0
        audio["pcm"] = Data((0..<3200).flatMap { _ in [UInt8(0xb0), 0x04] }).base64EncodedString()
        _ = voice.handle(audio, device: "A"); _ = voice.handle(audio, device: "A")
        XCTAssertTrue(voice.diagnostics.contains("已接收 1 块 · 0.2 秒"))
        XCTAssertTrue(voice.diagnostics.contains("最近电平 10% · 峰值 10%"))
        audio["seq"] = 1; audio["pcm"] = Data(repeating: 0, count: 6400).base64EncodedString()
        _ = voice.handle(audio, device: "A")
        XCTAssertTrue(voice.diagnostics.contains("已接收 2 块 · 0.4 秒"))
        XCTAssertTrue(voice.diagnostics.contains("最近电平 0% · 峰值 10%"))
        XCTAssertEqual(voice.handle(["op": "start", "taskId": task, "session": session, "source": "tab5"], device: "A").status, 409)
        _ = voice.handle(request("cancel", id), device: "A")
        _ = start(voice)
        XCTAssertTrue(voice.diagnostics.contains("已接收 0 块 · 0.0 秒"))
    }

    func testRequestDiagnosticsUseFixedLabelsAndClearFailuresForNewTake() {
        let backend = Backend(); var enabled = true
        let voice = MacTab5Voice(enabled: { enabled }, backend: backend.backend())
        voice.observeReply(op: "private-request-body", transport: "private-device-id", status: 401)
        XCTAssertTrue(voice.diagnostics.contains("连接 · 请求 · 401"))
        XCTAssertFalse(voice.diagnostics.contains("private"))
        voice.observeReply(op: "start", transport: "Wi-Fi", status: 200)
        XCTAssertFalse(voice.diagnostics.contains("最近拒绝"))
        voice.observeReply(op: "audio", transport: "USB", status: 409)
        voice.observeReply(op: "poll", transport: "USB", status: 200)
        XCTAssertTrue(voice.diagnostics.contains("最近拒绝：USB · 音频 · 409"))
        enabled = false; XCTAssertEqual(voice.diagnostics, "")
    }

    func testVoiceHTTPAuthenticationFailureIsVisibleWithoutSensitiveData() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend())
        let service = MacTab5Service(port: 0, voice: voice, load: { nil }, save: { _ in })
        XCTAssertEqual(service.voicePacket(device: "private-device-id", nonce: "private-nonce", proof: "private-proof", packet: Data(), transport: "Wi-Fi").0, 401)
        XCTAssertTrue(voice.diagnostics.contains("Wi-Fi · 请求 · 401"))
        XCTAssertFalse(voice.diagnostics.contains("private"))
    }
}
