import CryptoKit
import XCTest
@testable import AIBotBridge

final class MacTab5Tests: XCTestCase {
    private let device = "001122334455"
    private let key = Data(0..<32)
    private var settings: MacTab5DisplaySettings {
        MacTab5DisplaySettings(revision: "1234abcd", brightness: 75, volume: 40, muted: false,
                               selected: -1, cycle: true, interval: 15, saverMinutes: 5, alerts: true,
                               enabled: Array(repeating: 1, count: 8), order: Array(0..<8))
    }
    private func fixture() throws -> [String: Any] {
        let url = try XCTUnwrap(Bundle.module.url(forResource: "tab5-v1", withExtension: "json", subdirectory: "Fixtures"))
        return try MacTab5Protocol.object(Data(contentsOf: url))
    }
    private func bytes(_ hex: String) -> Data {
        let chars = Array(hex)
        return Data(stride(from: 0, to: chars.count, by: 2).map { UInt8(String(chars[$0..<$0+2]), radix: 16)! })
    }
    private func snapshot() -> MacStatusSnapshot {
        let date = Date(timeIntervalSince1970: 1800000000)
        return MacStatusSnapshot(version: 1, time: "12:00:00", epochUtc: 1800000000, utcOffsetSeconds: 28800,
                                 capturedAt: date, codex: ToolState(state: "working", ageSeconds: 1),
                                 claude: ToolState(state: "idle", ageSeconds: 10), musicPlaying: false,
                                 weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: nil,
                                 displayPolicy: MacDisplayPolicy(selectedMode: "weather", cycleEnabled: false, intervalSeconds: 30, pages: ["weather"]))
    }
    private func service(computer: MacTab5Computer = MacTab5Computer()) -> MacTab5Service {
        let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.usbmodem-fixture", key: key, confirmed: true)
        let service = MacTab5Service(port: 8765, host: { "192.168.1.20" }, computer: computer, load: { pair }, save: { _ in })
        service.publish(snapshot()); service.usbState("fixture", confirmed: true); return service
    }
    private func send(_ root: [String: Any], to service: MacTab5Service, nonce: String = MacTab5Protocol.identifier()) throws -> (Int, [String: Any]) {
        let packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: key, challenge: nonce)
        let proof = MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
        let (status, reply) = service.rpc(device: device, nonce: nonce, proof: proof, packet: packet)
        return (status, try MacTab5Protocol.object(MacTab5Protocol.decrypt(reply, key: key, challenge: nonce)))
    }
    private func command(_ service: MacTab5Service) throws -> [String: Any] {
        let root = try MacTab5Protocol.object(service.frame())
        let data = try XCTUnwrap(root["data"] as? [String: Any])
        let health = try XCTUnwrap(data["connectionHealth"] as? [String: Any])
        return try XCTUnwrap(health["displayCommand"] as? [String: Any])
    }
    private func report(_ command: [String: Any], service: MacTab5Service, ok: Bool = true, error: String = "") -> [String: Any] {
        ["kind": "display-settings", "requestId": command["requestId"]!, "session": service.session,
         "issuedAt": Int64(Date().timeIntervalSince1970 * 1000), "ok": ok, "error": error, "settings": settings.object]
    }

    func testCryptoMatchesIndependentGoldenVectorAndRejectsTampering() throws {
        let f = try fixture(), challenge = try XCTUnwrap(f["challenge"] as? String)
        let clear = bytes(try XCTUnwrap(f["clearHex"] as? String)), packet = bytes(try XCTUnwrap(f["packetHex"] as? String))
        XCTAssertEqual(try MacTab5Protocol.encrypt(clear, key: key, challenge: challenge, nonce: bytes(f["nonceHex"] as! String)), packet)
        XCTAssertEqual(try MacTab5Protocol.decrypt(packet, key: key, challenge: challenge), clear)
        XCTAssertEqual(MacTab5Protocol.proof(key: key, message: f["proofMessage"] as! String), f["proof"] as? String)
        for position in [0, 12, 28, packet.count - 1] {
            var changed = packet; changed[position] ^= 1
            XCTAssertThrowsError(try MacTab5Protocol.decrypt(changed, key: key, challenge: challenge))
        }
        XCTAssertThrowsError(try MacTab5Protocol.decrypt(packet, key: key, challenge: String(repeating: "f", count: 32)))
        XCTAssertThrowsError(try MacTab5Protocol.decrypt(packet, key: Data(repeating: 7, count: 32), challenge: challenge))
        XCTAssertFalse(MacTab5Protocol.verify(key: key, message: "other", proof: f["proof"] as! String))
        XCTAssertFalse(MacTab5Protocol.isHex(String(repeating: "A", count: 32), count: 32))
    }

    func testSettingsValidationMatchesFirmwareRanges() throws {
        let f = try fixture()
        try settings.validate()
        for invalid in f["invalidSettings"] as! [[String: Any]] {
            XCTAssertThrowsError(try JSONDecoder().decode(MacTab5DisplaySettings.self, from: MacTab5Protocol.json(invalid)).validate())
        }
    }

    func testSnapshotSeparatesESPSettingsAndOmitsUnsupportedFeatures() throws {
        let root = try MacTab5Protocol.object(MacTab5Protocol.snapshot(snapshot(), device: device, session: "1", sequence: 7, command: nil))
        XCTAssertEqual(root["type"] as? String, "tab5_status")
        let data = try XCTUnwrap(root["data"] as? [String: Any])
        XCTAssertNil(data["displayPolicy"]); XCTAssertNil(data["codexTasks"]); XCTAssertNil(data["rpcBinary"]); XCTAssertNil(data["ota"])
        let health = try XCTUnwrap(data["connectionHealth"] as? [String: Any])
        XCTAssertTrue(health["displayCommand"] is NSNull)
        XCTAssertNil(health["computerControlVersion"]); XCTAssertNil(health["voiceEnabled"])
        XCTAssertEqual((data["calendar"] as? [String: Any])?["lunar"] as? String, LunarDate.format(epoch: 1800000000, utcOffset: 28800))
    }

    func testMusicOnlyPermissionAdvertisesTheSharedControlTransport() throws {
        let suite = "AI-bot-music-capability-test-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        defaults.set(true, forKey: MacMusicService.enabledKey)
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in false }))
        let service = service(computer: control)
        func health() throws -> [String: Any] {
            let root = try MacTab5Protocol.object(service.frame())
            return try XCTUnwrap((root["data"] as? [String: Any])?["connectionHealth"] as? [String: Any])
        }
        XCTAssertFalse(control.isEnabled)
        XCTAssertEqual(try health()["computerControlVersion"] as? Int, 1)
        defaults.set(false, forKey: MacMusicService.enabledKey)
        XCTAssertNil(try health()["computerControlVersion"])
    }

    func testQuotaProjectionUsesActualTAB5KeysAndCodexPlanBoundary() throws {
        var source = snapshot()
        let q = ProviderQuotaSnapshot(provider: "codex", plan: "pro", primaryPercent: 30, primaryResetsAt: Date(),
                                      weeklyPercent: 80, weeklyResetsAt: Date(), resetCreditsAvailable: 2,
                                      resetCreditExpiresAt: [], updatedAt: Date(), stale: true)
        source = MacStatusSnapshot(version: source.version, time: source.time, epochUtc: source.epochUtc, utcOffsetSeconds: source.utcOffsetSeconds,
                                   capturedAt: source.capturedAt, codex: source.codex, claude: source.claude, musicPlaying: nil,
                                   weather: nil, stocks: nil, systemMetrics: nil, quotas: QuotaSnapshot(claude: nil, codex: q), music: nil)
        let root = try MacTab5Protocol.object(MacTab5Protocol.snapshot(source, device: device, session: "1", sequence: 1, command: nil))
        let details = (root["data"] as! [String: Any])["quotaDetails"] as! [[String: Any]]
        XCTAssertNil(details[1]["primary"]); XCTAssertEqual(details[1]["weekly"] as? Int, 80)
        XCTAssertEqual(details[1]["credits"] as? Int, 2); XCTAssertEqual(details[1]["stale"] as? Bool, true)
        XCTAssertNil(details[1]["primaryPercent"])
    }

    func testDailyPayloadPreservesSourcesAndStaleFlags() throws {
        let source = snapshot(), date = source.capturedAt
        let weather = WeatherSnapshot(city: "北京", condition: "晴", temperature: 20, high: 25, low: 10, humidity: 30,
                                      weatherCode: 0, pm25: 15, airQualityIndex: 20, source: "fixture", updatedAt: date, stale: true)
        let quotes = (0..<25).map { StockQuote(symbol: "s\($0)", code: "\($0)", name: "行情\($0)", price: "1.25", changePercent: "+1%", trend: 1) }
        let metrics = SystemMetricsSnapshot(cpuPercent: 25, memoryPercent: 50, uploadBytesPerSecond: 1234,
                                            downloadBytesPerSecond: 5678, updatedAt: date)
        let music = MusicSnapshot(hasArtwork: true, title: "歌曲", artist: "歌手", album: "专辑", playing: true,
                                  elapsedSeconds: 25, durationSeconds: 100, updatedAt: date)
        let daily = MacStatusSnapshot(version: 1, time: source.time, epochUtc: source.epochUtc, utcOffsetSeconds: source.utcOffsetSeconds,
                                      capturedAt: date, codex: source.codex, claude: source.claude, musicPlaying: true,
                                      weather: weather, stocks: StockSnapshot(quotes: quotes, updatedAt: date, stale: true),
                                      systemMetrics: metrics, quotas: nil, music: music)
        let root = try MacTab5Protocol.object(MacTab5Protocol.snapshot(daily, device: device, session: "1", sequence: 1, command: nil))
        let data = root["data"] as! [String: Any]
        XCTAssertEqual((data["weather"] as? [String: Any])?["source"] as? String, "fixture")
        XCTAssertEqual((data["weather"] as? [String: Any])?["stale"] as? Bool, true)
        let stocks = data["stocks"] as! [String: Any]
        XCTAssertEqual((stocks["quotes"] as? [Any])?.count, 20); XCTAssertEqual(stocks["stale"] as? Bool, true)
        XCTAssertEqual((data["systemMetrics"] as? [String: Any])?["uploadBytesPerSecond"] as? Int, 1234)
        XCTAssertEqual((data["music"] as? [String: Any])?["title"] as? String, "歌曲")
        XCTAssertEqual((data["music"] as? [String: Any])?["hasArtwork"] as? Bool, false)
    }

    func testStatusRequiresDeviceSpecificProofAndIncrementsSequence() throws {
        let service = self.service(), nonce = MacTab5Protocol.identifier()
        XCTAssertEqual(service.status(device: device, nonce: nonce, proof: "token").0, 401)
        let proof = MacTab5Protocol.proof(key: key, message: "GET|\(device)|\(nonce)")
        XCTAssertEqual(service.status(device: "ffffffffffff", nonce: nonce, proof: proof).0, 401)
        let first = service.status(device: device, nonce: nonce, proof: proof)
        let second = service.status(device: device, nonce: nonce, proof: proof)
        XCTAssertEqual(first.0, 200)
        let a = try MacTab5Protocol.object(MacTab5Protocol.decrypt(first.1, key: key, challenge: nonce))
        let b = try MacTab5Protocol.object(MacTab5Protocol.decrypt(second.1, key: key, challenge: nonce))
        XCTAssertGreaterThan(b["sequence"] as! Int, a["sequence"] as! Int)
    }

    func testReadThenSaveRequiresMatchingReceiptAndFreshRevision() throws {
        let service = self.service()
        var delivered = 0
        service.displaySettings(nil) { result in XCTAssertEqual(try? result.get(), self.settings); delivered += 1 }
        let read = try command(service)
        XCTAssertTrue(read["settings"] is NSNull); XCTAssertEqual(delivered, 0)
        XCTAssertEqual(try send(report(read, service: service), to: service).1["status"] as? Int, 200)
        XCTAssertEqual(delivered, 1)
        var desired = settings; desired.brightness = 90
        service.displaySettings(desired) { _ in delivered += 1 }
        let write = try command(service)
        XCTAssertEqual((write["settings"] as? [String: Any])?["brightness"] as? Int, 90)
        var receipt = report(write, service: service)
        var actual = desired; actual.revision = "deadbeef"; receipt["settings"] = actual.object
        XCTAssertEqual(try send(receipt, to: service).1["status"] as? Int, 200); XCTAssertEqual(delivered, 2)
        service.displaySettings(desired) { result in
            guard case .failure(let error) = result else { return XCTFail("stale revision accepted") }
            XCTAssertEqual(error as? MacTab5Error, .staleRevision)
        }
    }

    func testReplayAndDuplicateReceiptsNeverCompleteTwice() throws {
        let service = self.service(); var delivered = 0
        service.displaySettings(nil) { _ in delivered += 1 }
        let read = try command(service), receipt = report(read, service: service), nonce = MacTab5Protocol.identifier()
        XCTAssertEqual(try send(receipt, to: service, nonce: nonce).1["status"] as? Int, 200)
        XCTAssertEqual(try send(receipt, to: service, nonce: nonce).1["status"] as? Int, 409)
        XCTAssertEqual(try send(receipt, to: service).1["status"] as? Int, 200)
        var changed = receipt; var other = settings; other.volume = 99; changed["settings"] = other.object
        XCTAssertEqual(try send(changed, to: service).1["status"] as? Int, 409)
        XCTAssertEqual(delivered, 1)
    }

    func testInvalidSessionTimeRequestAndConflictDoNotReportSaved() throws {
        let service = self.service(); var outcome: Result<MacTab5DisplaySettings, Error>?
        service.displaySettings(nil) { outcome = $0 }
        let read = try command(service)
        for field in ["session", "issuedAt", "requestId"] {
            var bad = report(read, service: service)
            if field == "issuedAt" { bad[field] = 0 } else { bad[field] = "bad" }
            XCTAssertNotEqual(try send(bad, to: service).1["status"] as? Int, 200); XCTAssertNil(outcome)
        }
        XCTAssertEqual(try send(report(read, service: service, ok: false, error: "conflict"), to: service).1["status"] as? Int, 200)
        guard let outcome, case .failure(let error) = outcome else { return XCTFail("conflict reported as saved") }
        XCTAssertEqual(error as? MacTab5Error, .staleRevision)
        service.displaySettings(settings) { result in
            guard case .failure = result else { return XCTFail("write allowed without reread") }
        }
    }

    func testPendingBusyStopAndExpiredTransaction() throws {
        let service = self.service(); var failed = 0
        service.displaySettings(nil) { if case .failure = $0 { failed += 1 } }
        service.displaySettings(nil) { if case .failure(let error) = $0, error as? MacTab5Error == .busy { failed += 1 } }
        XCTAssertEqual(failed, 1); service.stop(); XCTAssertEqual(failed, 2)
        let transaction = MacTab5DisplayTransaction(), id = try transaction.begin(nil, now: 100)
        XCTAssertEqual(transaction.command?["expiresAt"] as? Int64, 20100)
        XCTAssertFalse(transaction.cancel("other")); XCTAssertTrue(transaction.cancel(id)); XCTAssertFalse(transaction.cancel(id))
        XCTAssertThrowsError(try transaction.begin(settings, now: 200))
    }

    func testPairingStagesKeyBeforeDeviceAndNeverOverwritesForeignRecord() throws {
        var disk: MacTab5Pairing?
        let service = MacTab5Service(port: 8765, load: { disk }, save: { disk = $0 })
        let staged = try service.preparePairing(device: device, path: "/dev/cu.usbmodem-fixture")
        XCTAssertEqual(staged, disk); XCTAssertFalse(staged.confirmed); XCTAssertEqual(staged.key.count, 32)
        let again = try service.preparePairing(device: device, path: "/dev/cu.usbmodem-new")
        XCTAssertEqual(again.key, staged.key)
        try service.confirmPairing(again); XCTAssertEqual(disk?.confirmed, true)
        XCTAssertThrowsError(try service.preparePairing(device: "ffffffffffff", path: "/dev/cu.usbmodem-other"))
        XCTAssertEqual(disk?.deviceId, device)
    }

    func testKeychainFailureDoesNotCreateOrReplacePairing() {
        let service = MacTab5Service(port: 8765, load: { throw MacTab5Error.keychain }, save: { _ in XCTFail("must not overwrite unreadable pairing") })
        XCTAssertThrowsError(try service.preparePairing(device: device, path: "/dev/cu.usbmodem-fixture"))
        XCTAssertNil(service.currentPairing)
    }

    func testDiscoveryIsAuthenticatedAndBindsNonceHostPort() throws {
        let service = self.service(), f = try fixture(), request = Data((f["discovery"] as! String).utf8)
        XCTAssertNil(service.discovery(request, address: "192.168.1.20"))
        service.setLANReady(true)
        XCTAssertEqual(service.discovery(request, address: "192.168.1.20"), Data((f["discoveryReply"] as! String).utf8))
        var bad = request; bad[bad.count - 1] ^= 1
        XCTAssertNil(service.discovery(bad, address: "192.168.1.20")); XCTAssertNil(service.discovery(request, address: "8.8.8.8"))
    }
}
