import XCTest
@testable import AIBotBridge

final class MacTab5VoiceTests: XCTestCase {
    private final class Backend {
        var callback: ((String, Bool, Bool) -> Void)?, feeds = 0, finishes = 0, cancels = 0
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
        backend.callback?("待确认文字", false, false)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["canInsert"] as? Bool, false)
        _ = voice.handle(request("stop", id), device: "A"); _ = voice.handle(request("stop", id), device: "A"); XCTAssertEqual(backend.finishes, 1)
        backend.callback?("最终文字", true, false)
        let review = voice.handle(request("poll", id), device: "A"); XCTAssertEqual(review.body["state"] as? String, "review"); XCTAssertEqual(review.body["canInsert"] as? Bool, true)
        _ = voice.handle(request("cancel", id), device: "A"); backend.callback?("晚到结果", true, false)
        XCTAssertEqual(voice.handle(request("poll", id), device: "A").body["text"] as? String, "")
    }
    func testTimeoutStopsCaptureAndUnconfirmedPartialRemainsError() {
        let backend = Backend(); var now: TimeInterval = 0
        let voice = MacTab5Voice(enabled: { true }, clock: { now }, backend: backend.backend()), id = start(voice)
        backend.callback?("部分文字", false, false); now = 9
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
        backend.callback?(String(repeating: "字", count: 1000), true, false)
        let result = voice.handle(request("poll", id), device: "A")
        XCTAssertEqual(result.body["state"] as? String, "error"); XCTAssertEqual(result.body["canInsert"] as? Bool, false)
        XCTAssertLessThanOrEqual((result.body["text"] as! String).utf8.count, 2000)
    }
    func testRecognizerFinalOrErrorAlwaysReleasesCapture() {
        let backend = Backend(), voice = MacTab5Voice(enabled: { true }, backend: backend.backend())
        let first = start(voice); backend.callback?("自然结束", true, false)
        XCTAssertEqual(backend.finishes, 1); XCTAssertEqual(voice.handle(request("poll", first), device: "A").body["state"] as? String, "review")
        let second = start(voice), before = backend.cancels; backend.callback?("", false, true)
        XCTAssertEqual(backend.cancels, before + 1); XCTAssertEqual(voice.handle(request("poll", second), device: "A").body["state"] as? String, "error")
    }
}
