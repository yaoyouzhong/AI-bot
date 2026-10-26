import XCTest
@testable import AIBotBridge

final class MacTab5BLEDiagnosticsTests: XCTestCase {
    func testGattTimingIncludesFragmentReadsWritesAndFinalConfirmation() {
        var now: TimeInterval = 10
        let diagnostics = MacTab5BLEDiagnostics(clock: { now }), id = diagnostics.begin(.status)
        diagnostics.stage(.writing); diagnostics.submit(488); diagnostics.submit(200)
        now = 11.25
        XCTAssertTrue(diagnostics.summary.contains("状态帧：等待写分片 · 1250 ms"))
        XCTAssertTrue(diagnostics.summary.contains("提交 688 B/2 次"))
        diagnostics.stage(.reading); diagnostics.read(400); now = 11.5
        XCTAssertTrue(diagnostics.summary.contains("等待读分片"))
        diagnostics.finish(id, success: true); now = 40
        XCTAssertTrue(diagnostics.summary.contains("状态帧：流程完成 · 1500 ms"))
        XCTAssertTrue(diagnostics.summary.contains("读取 400 B/1 次"))
        XCTAssertFalse(diagnostics.summary.contains("等待"))
    }
    func testResetAndLateCallbacksCannotConfirmAnotherTransfer() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now }), old = diagnostics.begin(.voice)
        diagnostics.read(50); diagnostics.submit(20); now = 2; diagnostics.failCurrent()
        XCTAssertTrue(diagnostics.summary.contains("语音邮箱：未确认 · 2000 ms"))
        let next = diagnostics.begin(.rpc); now = 3
        diagnostics.finish(old, success: true); diagnostics.discard(old)
        XCTAssertTrue(diagnostics.summary.contains("控制邮箱：本机处理 · 1000 ms"))
        XCTAssertTrue(diagnostics.summary.contains("语音邮箱：未确认"))
        diagnostics.finish(next, success: true)
        XCTAssertTrue(diagnostics.summary.contains("控制邮箱：流程完成"))
    }
    func testIdleMailboxPollsDoNotEraseLastActualTransferAndStorageIsBounded() {
        let diagnostics = MacTab5BLEDiagnostics(clock: { 1 })
        XCTAssertTrue(diagnostics.summary.isEmpty)
        diagnostics.read(100); diagnostics.submit(100); diagnostics.stage(.reading)
        XCTAssertTrue(diagnostics.summary.isEmpty)
        for _ in 0..<1000 {
            for kind in MacTab5BLEDiagnostics.Kind.allCases {
                let id = diagnostics.begin(kind); diagnostics.read(20); diagnostics.finish(id, success: true)
                let idle = diagnostics.begin(kind); diagnostics.read(8); diagnostics.discard(idle)
            }
        }
        XCTAssertEqual(diagnostics.summary.split(separator: "\n").count, 4)
        XCTAssertTrue(diagnostics.summary.contains("语音邮箱：流程完成 · 0 ms · 读取 20 B/1 次"))
        XCTAssertFalse(diagnostics.summary.contains("读取 8 B"))
        XCTAssertFalse(diagnostics.summary.contains("voiceId")); XCTAssertFalse(diagnostics.summary.contains("nonce"))
    }
    func testCountersAndClockFailuresRemainNumericAndBounded() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now }), id = diagnostics.begin(.resource)
        diagnostics.read(-10); diagnostics.submit(Int.max)
        now = .infinity
        XCTAssertTrue(diagnostics.summary.contains("0 ms"))
        XCTAssertTrue(diagnostics.summary.contains("读取 0 B/1 次 · 提交 65536 B/1 次"))
        now = 500; diagnostics.finish(id, success: false)
        XCTAssertTrue(diagnostics.summary.contains("资源帧：未确认 · 180000 ms"))
        let replaced = diagnostics.begin(.voice); now = 501; _ = diagnostics.begin(.status)
        diagnostics.finish(replaced, success: true)
        XCTAssertTrue(diagnostics.summary.contains("语音邮箱：未确认 · 1000 ms"))
    }
    func testCaptureSeparatesAudioPhasesBackgroundIdleAndFinalResponse() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now })
        let start = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        now = 1; diagnostics.finish(start, success: true)
        now = 2
        let audio = diagnostics.begin(.voice); diagnostics.stage(.reading)
        diagnostics.voiceGrant(); diagnostics.voiceNotification(); diagnostics.voiceNotification()
        now = 4; diagnostics.stage(.writing)
        now = 5; diagnostics.stage(.processing); diagnostics.voice(.audio, terminal: false)
        now = 6; diagnostics.finish(audio, success: true)
        let background = diagnostics.begin(.status); now = 8; diagnostics.finish(background, success: true)
        let control = diagnostics.begin(.rpc); now = 9; diagnostics.discard(control)
        let idle = diagnostics.begin(.voice); now = 10; diagnostics.discard(idle)
        let stop = diagnostics.begin(.voice); diagnostics.voice(.stop, terminal: false)
        now = 11; diagnostics.finish(stop, success: true)
        let final = diagnostics.begin(.voice); diagnostics.voice(.poll, terminal: true)
        now = 12; diagnostics.finish(final, success: true)
        let summary = diagnostics.summary
        XCTAssertTrue(summary.contains("终态响应已提交 · 跨度 12000 ms"))
        XCTAssertTrue(summary.contains("音频传输：1 次 · 累计 4000 ms · 最长 4000 ms · 读阶段 2000／写阶段 1000／处理阶段 1000 ms"))
        XCTAssertTrue(summary.contains("窗口授权 1 次／通知 2 片"))
        XCTAssertTrue(summary.contains("语音 8000 ms · 状态与资源 2000 ms · 控制 1000 ms · 其中空邮箱 2000 ms · 其余间隔 1000 ms"))
        XCTAssertTrue(summary.contains("结束请求：1 次／1000 ms · 结果查询：1 次／1000 ms"))
        now = 100; let later = diagnostics.begin(.voice); diagnostics.voice(.poll, terminal: true)
        now = 101; diagnostics.finish(later, success: true)
        XCTAssertEqual(diagnostics.summary.components(separatedBy: "本轮蓝牙累计：").last,
                       summary.components(separatedBy: "本轮蓝牙累计：").last)
    }
    func testResetClosesCaptureAndOnlySuccessfulNewStartReplacesIt() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now })
        let start = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        now = 1; diagnostics.finish(start, success: true)
        let audio = diagnostics.begin(.voice); diagnostics.voice(.audio, terminal: false)
        now = 2; diagnostics.failCurrent(); diagnostics.finish(audio, success: true)
        XCTAssertTrue(diagnostics.summary.contains("未确认结束 · 跨度 2000 ms"))
        let failedStart = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        now = 3; diagnostics.finish(failedStart, success: false)
        XCTAssertTrue(diagnostics.summary.contains("未确认结束 · 跨度 2000 ms"))
        let next = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        now = 4; diagnostics.finish(next, success: true)
        XCTAssertTrue(diagnostics.summary.contains("进行中 · 跨度 1000 ms"))
        XCTAssertTrue(diagnostics.summary.contains("音频传输：0 次"))
        diagnostics.failCurrent()
        XCTAssertTrue(diagnostics.summary.contains("未确认结束 · 跨度 1000 ms"))
    }
    func testControlCannotTagVoiceAndCountersStayBoundedWithoutPayloads() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now })
        let control = diagnostics.begin(.rpc); diagnostics.voice(.start, terminal: false)
        diagnostics.voiceGrant(); diagnostics.voiceNotification(); diagnostics.finish(control, success: true)
        XCTAssertFalse(diagnostics.summary.contains("本轮蓝牙累计"))
        let start = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false); diagnostics.finish(start, success: true)
        for _ in 0..<5000 {
            let audio = diagnostics.begin(.voice); diagnostics.voice(.audio, terminal: false)
            now += 1; diagnostics.voiceGrant(command: true); diagnostics.voiceNotification(); diagnostics.finish(audio, success: true)
        }
        XCTAssertTrue(diagnostics.summary.contains("音频传输：4096 次 · 累计 180000 ms"))
        XCTAssertTrue(diagnostics.summary.contains("其中免应答授权 5000 次"))
        XCTAssertLessThanOrEqual(diagnostics.summary.split(separator: "\n").count, 9)
        XCTAssertFalse(diagnostics.summary.contains("nonce")); XCTAssertFalse(diagnostics.summary.contains("voiceId"))
        diagnostics.voiceLink(credits: 8, available: true, subscribed: false, failure: .att, code: 8)
        diagnostics.voiceConfiguration(NSNumber(value: 1))
        XCTAssertTrue(diagnostics.summary.contains("CCCD 1"))
        XCTAssertTrue(diagnostics.summary.contains("订阅 未确认"))
        for invalid: Any in [true, -1, 4, 1.5, "private-descriptor-content", Data([1, 0])] {
            diagnostics.voiceConfiguration(invalid)
            XCTAssertTrue(diagnostics.summary.contains("CCCD 未知"))
            XCTAssertFalse(diagnostics.summary.contains("private-descriptor-content"))
        }
        diagnostics.voiceLink(credits: 8, available: true, subscribed: false)
        XCTAssertFalse(diagnostics.summary.contains("CCCD"))
    }
    func testNotificationTimingSeparatesFirstPacketFromGapsAcrossWindowsAndRequests() {
        var now: TimeInterval = 0
        let diagnostics = MacTab5BLEDiagnostics(clock: { now })
        let start = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        diagnostics.finish(start, success: true)
        now = 1
        let audio = diagnostics.begin(.voice); diagnostics.voice(.audio, terminal: false)
        diagnostics.voiceGrant(command: true)
        now = 3; diagnostics.voiceNotification()
        now = 4; diagnostics.voiceNotification()
        now = 6; diagnostics.voiceNotification()
        now = 7; diagnostics.voiceGrant(command: true)
        now = 8; diagnostics.voiceNotification()
        now = 9; diagnostics.voiceNotification()
        now = 10; diagnostics.finish(audio, success: true)
        XCTAssertTrue(diagnostics.summary.contains("授权提交至首片累计 3000 ms · 后续片间隔累计 4000 ms／最长 2000 ms"))
        let control = diagnostics.begin(.rpc); diagnostics.voiceGrant(); now = 20; diagnostics.voiceNotification()
        diagnostics.finish(control, success: true)
        XCTAssertTrue(diagnostics.summary.contains("授权提交至首片累计 3000 ms · 后续片间隔累计 4000 ms／最长 2000 ms"))
        let next = diagnostics.begin(.voice); diagnostics.voice(.audio, terminal: false); diagnostics.voiceGrant()
        now = 21; diagnostics.voiceNotification(); diagnostics.finish(next, success: true)
        XCTAssertTrue(diagnostics.summary.contains("授权提交至首片累计 4000 ms · 后续片间隔累计 4000 ms／最长 2000 ms"))
        let newTake = diagnostics.begin(.voice); diagnostics.voice(.start, terminal: false)
        diagnostics.finish(newTake, success: true)
        XCTAssertTrue(diagnostics.summary.contains("授权提交至首片累计 0 ms · 后续片间隔累计 0 ms／最长 0 ms"))
    }
}
