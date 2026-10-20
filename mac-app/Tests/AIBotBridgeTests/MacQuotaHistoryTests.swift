import XCTest
@testable import AIBotBridge

final class MacQuotaHistoryTests: XCTestCase {
    private let account = String(repeating: "a", count: 64)
    private let midnight = Date(timeIntervalSince1970: 1_792_425_600) // 2026-10-20, Asia/Shanghai
    private func row(_ seconds: Double, _ used: Double, reset: Double = 604800,
                     account: String? = String(repeating: "a", count: 64)) -> MacQuotaHistory.Observation {
        .init(at: midnight.addingTimeInterval(seconds), plan: "pro", weekly: used,
              reset: midnight.addingTimeInterval(reset), account: account)
    }
    private func day(_ rows: [MacQuotaHistory.Observation], now: Double = 1000) -> MacQuotaTrend.Day {
        MacQuotaHistory.build(rows, now: midnight.addingTimeInterval(now)).days.last!
    }
    private func quota(_ used: Double = 20, at: Date, stale: Bool = false) -> ProviderQuotaSnapshot {
        .init(provider: "codex", plan: "pro", primaryPercent: nil, primaryResetsAt: nil,
              weeklyPercent: used, weeklyResetsAt: midnight.addingTimeInterval(604800),
              resetCreditsAvailable: 3, resetCreditExpiresAt: [], updatedAt: at, stale: stale,
              weeklyWindowSeconds: 604800)
    }
    func testUnknownHistoryIsNotZeroAndOnlyObservedGrowthIsCounted() {
        let empty = MacQuotaHistory.build([], now: midnight)
        XCTAssertEqual(empty.days.count, 7)
        XCTAssertTrue(empty.days.allSatisfy { $0.used == nil && $0.partial })
        XCTAssertNil(day([row(10, 20)]).used)
        XCTAssertEqual(day([row(10, 20), row(130, 20)]).used, 0)
        XCTAssertEqual(day([row(10, 20), row(130, 23)]).used, 3)
        XCTAssertTrue(day([row(10, 20), row(130, 23)]).partial)
    }
    func testResetsCorrectionsAndAccountChangesDoNotInventUsage() {
        XCTAssertEqual(day([row(10, 80, reset: 100), row(130, 2, reset: 604900)]).used, 2)
        XCTAssertEqual(day([row(10, 80), row(130, 2, reset: 700000)]).used, 2)
        XCTAssertNil(day([row(10, 80), row(130, 2)]).used) // correction without reset evidence
        XCTAssertNil(day([row(10, 20), row(130, 40, account: String(repeating: "b", count: 64))]).used)
        XCTAssertTrue(day([row(10, 20, account: nil), row(130, 22, account: nil)]).partial)
        XCTAssertEqual(day([row(10, 20), row(130, 21, reset: 604830)]).used, 1)
    }
    func testMidnightOwnershipAndLongGapAreExplicit() {
        XCTAssertEqual(day([row(-60, 20), row(60, 22)]).used, 2)
        XCTAssertNil(day([row(-3600, 20), row(60, 22)]).used)
        XCTAssertEqual(day([row(-3600, 20), row(60, 20)]).used, 0)
        XCTAssertEqual(day([row(10, 20), row(900, 25)]).used, 5)
        let complete = MacQuotaHistory.build([row(-60, 20), row(60, 22), row(86340, 22), row(86460, 22)],
                                            now: midnight.addingTimeInterval(86500))
        XCTAssertFalse(complete.days[5].partial)
        XCTAssertEqual(complete.days[5].used, 2)
    }
    func testPersistenceReloadStaleRejectionAndCorruptionPreservation() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let file = directory.appendingPathComponent("history.json"), history = MacQuotaHistory(file: file)
        history.record(quota(at: midnight), account: account)
        history.record(quota(23, at: midnight.addingTimeInterval(120)), account: account)
        history.record(quota(90, at: midnight.addingTimeInterval(240), stale: true), account: account)
        history.record(quota(50, at: midnight), account: account)
        let loaded = MacQuotaHistory(file: file).snapshot(now: midnight.addingTimeInterval(300))
        XCTAssertEqual(loaded.days.last?.used, 3)
        XCTAssertFalse(loaded.storageWarning)
        let damaged = Data("unreadable history".utf8)
        try damaged.write(to: file)
        let recovery = MacQuotaHistory(file: file)
        recovery.record(quota(at: midnight), account: account)
        recovery.record(quota(21, at: midnight.addingTimeInterval(120)), account: account)
        XCTAssertEqual(recovery.snapshot(now: midnight.addingTimeInterval(300)).days.last?.used, 1)
        XCTAssertTrue(recovery.snapshot().storageWarning)
        XCTAssertEqual(try Data(contentsOf: file), damaged)
    }
    func testCreditNetworkFailureDoesNotDiscardUsageOrHistory() async throws {
        let suite = "quota-optional-test-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer {
            defaults.removePersistentDomain(forName: suite)
            try? FileManager.default.removeItem(at: directory)
        }
        let file = directory.appendingPathComponent("history.json")
        let history = MacQuotaHistory(file: file)
        let store = MacDataStore(defaults: defaults, quotaHistory: history)
        let service = MacQuotaService(store: store)
        let usage = Data(#"{"plan_type":"pro","rate_limit":{"primary_window":{"used_percent":24,"limit_window_seconds":604800,"reset_at":1900000000}},"rate_limit_reset_credits":{"available_count":3}}"#.utf8)
        let result = await service.collectCodex(credentialPlan: nil, accountID: "synthetic-account") { address in
            if address.hasSuffix("/usage") { return usage }
            throw URLError(.timedOut)
        }
        XCTAssertEqual(result?.weeklyPercent, 24)
        XCTAssertEqual(result?.weeklyWindowSeconds, 604800)
        XCTAssertEqual(result?.resetCreditsAvailable, 3)
        XCTAssertEqual(try JSONDecoder().decode([MacQuotaHistory.Observation].self, from: Data(contentsOf: file)).count, 1)
        let malformed = try MacQuotaService.parseCodex(usage, credits: Data("bad".utf8))
        XCTAssertEqual(malformed.weeklyPercent, 24)
        XCTAssertEqual(malformed.resetCreditsAvailable, 3)
    }
    func testWindowDurationsPersistAndUnknownDurationIsNotGuessed() throws {
        let suite = "quota-duration-test-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let store = MacDataStore(defaults: defaults)
        store.mergeQuotas(claude: nil, codex: quota(at: midnight))
        XCTAssertEqual(MacDataStore(defaults: defaults).snapshot().quotas?.codex?.weeklyWindowSeconds, 604800)
        for value in ["null", "true", "-1", "0", "0.5", "2147483648", #""NaN""#] {
            let data = Data("{\"rate_limit\":{\"primary_window\":{\"used_percent\":10,\"limit_window_seconds\":\(value)}}}".utf8)
            let parsed = try MacQuotaService.parseCodex(data)
            XCTAssertNil(parsed.primaryWindowSeconds); XCTAssertNil(parsed.weeklyWindowSeconds)
        }
        let claude = try MacQuotaService.parseClaude(Data(#"{"five_hour":{},"seven_day":{}}"#.utf8))
        XCTAssertEqual(claude.primaryWindowSeconds, 18000); XCTAssertEqual(claude.weeklyWindowSeconds, 604800)
    }
}
