import XCTest
@testable import AIBotBridge

final class MacTab5BLEVoiceWindowTests: XCTestCase {
    private func part(_ bytes: Data, offset: Int, count: Int, id: UInt32 = 7, total: Int? = nil) -> Data {
        var data = Data(); MacTab5Mailbox.append32(id, to: &data)
        MacTab5Mailbox.append16(offset, to: &data); MacTab5Mailbox.append16(total ?? bytes.count, to: &data)
        data.append(bytes[offset..<min(bytes.count, offset + count)]); return data
    }
    func testEncryptedRequestRoundTripsAcrossSmallAndLargeMTUAndPartialWindows() throws {
        let key = Data(0..<32), nonce = String(repeating: "a", count: 32)
        let clear = try MacTab5Protocol.json(["fixture": String(repeating: "测试", count: 1100)])
        let encrypted = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce)
        let request = Data(nonce.utf8) + Data(MacTab5Protocol.proof(key: key, message: nonce).utf8) + encrypted
        for capacity in [20, 244, 488] {
            let chunk = capacity - 8
            var prefix = Data(request.prefix(chunk)), grants = 0
            while prefix.count < request.count {
                var window = try MacTab5BLEVoiceWindow(id: 7, total: request.count, prefix: prefix, credits: 8, now: 10)
                XCTAssertEqual(window.grant.count, 8); XCTAssertEqual(window.grant[0], 3)
                XCTAssertEqual(MacTab5Mailbox.u32(window.grant, 1), 7)
                XCTAssertEqual(Int(MacTab5Mailbox.u16(window.grant, 5)), prefix.count)
                XCTAssertEqual(window.grant[7], 8)
                try window.markGrantSubmitted(now: 10.01)
                while !window.ready {
                    try window.accept(part(request, offset: window.bytes.count, count: chunk), now: 10.1)
                }
                prefix = window.bytes; grants += 1
            }
            XCTAssertEqual(prefix, request)
            XCTAssertEqual(try MacTab5Protocol.decrypt(Data(prefix.dropFirst(96)), key: key, challenge: nonce), clear)
            XCTAssertLessThanOrEqual(grants, (request.count / chunk + 7) / 8)
        }
    }
    func testNotificationsBeforeGrantAckCannotDispatchAndShortFinalWindowCompletes() throws {
        let data = Data(repeating: 9, count: 124)
        var window = try MacTab5BLEVoiceWindow(id: 7, total: 124, prefix: Data(data.prefix(100)), credits: 8, now: 0)
        try window.accept(part(data, offset: 100, count: 24), now: 0.1)
        XCTAssertFalse(window.ready)
        try window.markGrantSubmitted(now: 0.2); XCTAssertTrue(window.ready)
        XCTAssertThrowsError(try window.accept(part(data, offset: 100, count: 24), now: 0.3))
        XCTAssertThrowsError(try window.markGrantSubmitted(now: 0.3))
    }
    func testGapsDuplicatesWrongIdentityAndTotalNeverAdvanceAcceptedPrefix() throws {
        let data = Data(repeating: 4, count: 1000), prefix = Data(data.prefix(100))
        for packet in [part(data, offset: 101, count: 50), part(data, offset: 0, count: 50),
                       part(data, offset: 100, count: 50, id: 8), part(data, offset: 100, count: 50, total: 999),
                       Data(repeating: 0, count: 8), part(data, offset: 100, count: 481)] {
            var window = try MacTab5BLEVoiceWindow(id: 7, total: 1000, prefix: prefix, credits: 8, now: 1)
            XCTAssertThrowsError(try window.accept(packet, now: 1.1)); XCTAssertEqual(window.bytes, prefix)
            XCTAssertFalse(window.ready)
        }
    }
    func testDroppedOrExtraPacketsCannotCompleteOrExpandWindow() throws {
        let data = Data(repeating: 4, count: 1000)
        var window = try MacTab5BLEVoiceWindow(id: 7, total: 1000, prefix: Data(data.prefix(100)), credits: 2, now: 0)
        try window.markGrantSubmitted(now: 0.1)
        try window.accept(part(data, offset: 100, count: 100), now: 0.2)
        XCTAssertFalse(window.ready); XCTAssertTrue(window.expired(at: 4))
        XCTAssertThrowsError(try window.accept(part(data, offset: 200, count: 100), now: 4))
        var full = try MacTab5BLEVoiceWindow(id: 7, total: 1000, prefix: Data(data.prefix(100)), credits: 1, now: 0)
        try full.accept(part(data, offset: 100, count: 100), now: 0.2)
        XCTAssertThrowsError(try full.accept(part(data, offset: 200, count: 100), now: 0.3))
        XCTAssertThrowsError(try full.markGrantSubmitted(now: 4))
        XCTAssertFalse(full.ready)
    }
    func testCapabilitiesBoundsAndAbnormalClocksFailClosed() throws {
        for value in [0, 9, -1, true, "8", 1.5, NSNull()] as [Any] {
            XCTAssertEqual(MacTab5BLEVoiceWindow.credits(["mailboxWindow": value]), 0)
        }
        XCTAssertEqual(MacTab5BLEVoiceWindow.credits([:]), 0)
        for value in 1...8 { XCTAssertEqual(MacTab5BLEVoiceWindow.credits(["mailboxWindow": value]), value) }
        for total in [123, 12289] {
            XCTAssertThrowsError(try MacTab5BLEVoiceWindow(id: 7, total: total, prefix: Data([1]), credits: 4, now: 0))
        }
        XCTAssertThrowsError(try MacTab5BLEVoiceWindow(id: 0, total: 124, prefix: Data([1]), credits: 4, now: 0))
        for size in [0, 124, 125] {
            XCTAssertThrowsError(try MacTab5BLEVoiceWindow(id: 7, total: 124, prefix: Data(repeating: 0, count: size), credits: 4, now: 0))
        }
        let window = try MacTab5BLEVoiceWindow(id: 7, total: 124, prefix: Data([1]), credits: 4, now: 10)
        XCTAssertTrue(window.expired(at: 9)); XCTAssertTrue(window.expired(at: .infinity))
        XCTAssertFalse(window.expired(at: 13.99))
    }
    func testBackgroundProbesOnlyAtConfirmedNonfinalBoundaries() {
        for index in [0, 1, 4, 6, 19, 20, 21] {
            XCTAssertFalse(MacTab5BLEVoiceWindow.shouldProbe(now: 2, lastProbe: 0, index: index, count: 20))
        }
        for index in [5, 10, 15] {
            XCTAssertTrue(MacTab5BLEVoiceWindow.shouldProbe(now: 2, lastProbe: 0, index: index, count: 20))
            XCTAssertFalse(MacTab5BLEVoiceWindow.shouldProbe(now: 0.9, lastProbe: 0, index: index, count: 20))
        }
        XCTAssertTrue(MacTab5BLEVoiceWindow.shouldProbe(now: 0, lastProbe: -.infinity, index: 5, count: 20))
        XCTAssertFalse(MacTab5BLEVoiceWindow.shouldProbe(now: .nan, lastProbe: 0, index: 5, count: 20))
    }
    func testCommandSubmissionNeedsExactWindowAndLostGrantCannotCompleteOrReplay() throws {
        let data = Data(repeating: 4, count: 1000)
        var lost = try MacTab5BLEVoiceWindow(id: 7, total: data.count, prefix: Data(data.prefix(480)), credits: 8, now: 0)
        try lost.markGrantSubmitted(now: 0.01)
        XCTAssertFalse(lost.ready)
        XCTAssertThrowsError(try lost.markGrantSubmitted(now: 0.02))
        XCTAssertTrue(lost.expired(at: 4)); XCTAssertFalse(lost.ready)
        XCTAssertThrowsError(try lost.accept(part(data, offset: 480, count: 480), now: 4))

        var delivered = try MacTab5BLEVoiceWindow(id: 7, total: data.count, prefix: Data(data.prefix(480)), credits: 8, now: 10)
        try delivered.markGrantSubmitted(now: 10.01)
        XCTAssertThrowsError(try delivered.accept(part(data, offset: 480, count: 480, id: 8), now: 10.02))
        XCTAssertEqual(delivered.bytes.count, 480)
        try delivered.accept(part(data, offset: 480, count: 480), now: 10.03)
        XCTAssertFalse(delivered.ready)
        try delivered.accept(part(data, offset: 960, count: 40), now: 10.04)
        XCTAssertTrue(delivered.ready); XCTAssertEqual(delivered.bytes, data)
        // Delivery can complete without any ATT grant acknowledgement, but not
        // without all matching packets. The caller still authenticates ciphertext.
    }
}
