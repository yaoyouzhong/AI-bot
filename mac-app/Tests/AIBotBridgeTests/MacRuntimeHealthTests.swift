import XCTest
@testable import AIBotBridge

final class MacRuntimeHealthTests: XCTestCase {
    private func sample(_ time: Double, cpu: Double = 20, memory: Double = 40) -> SystemMetricsSnapshot {
        SystemMetricsSnapshot(cpuPercent: cpu, memoryPercent: memory, uploadBytesPerSecond: 10,
                              downloadBytesPerSecond: 20, updatedAt: Date(timeIntervalSince1970: time),
                              pressureObservedAt: Date(timeIntervalSince1970: time))
    }
    private func take(_ monitor: MacRuntimeHealth, _ metrics: SystemMetricsSnapshot, now: Double? = nil) -> [String: Any] {
        monitor.snapshot(metrics: metrics, now: Date(timeIntervalSince1970: now ?? metrics.updatedAt.timeIntervalSince1970),
                         logicalProcessors: 8, codexEnabled: true, desktopObserved: true)!
    }
    private func state(_ value: [String: Any], _ key: String) -> String? {
        (value["checks"] as? [[String: Any]])?.first { $0["key"] as? String == key }?["state"] as? String
    }
    func testActualCountersAppearWithoutInventingNetworkOrDiskMeasurements() throws {
        let value = take(MacRuntimeHealth(), sample(1000))
        XCTAssertEqual(state(value, "cpu"), "ok"); XCTAssertEqual(state(value, "memory"), "ok")
        XCTAssertEqual(state(value, "bridge"), "ok"); XCTAssertEqual(state(value, "codex"), "observing")
        for key in ["queue", "disk", "vpn", "pac"] { XCTAssertEqual(state(value, key), "unknown") }
        XCTAssertEqual((value["latencyHistory"] as? [Any])?.count, 0)
        let pressure = try XCTUnwrap(value["pressure"] as? [String: Any])
        XCTAssertEqual(pressure["logicalProcessors"] as? Int, 8)
        for key in ["processorQueue", "diskLatencyMs", "availableMemoryMb", "commitPercent"] { XCTAssertNil(pressure[key]) }
        XCTAssertEqual((value["alerts"] as? [Any])?.count, 0)
    }
    func testRepeatedFramesCannotConfirmAlarmAndAlarmIdentityDoesNotRepeat() {
        let monitor = MacRuntimeHealth()
        let first = take(monitor, sample(1000, cpu: 99))
        XCTAssertEqual(state(first, "cpu"), "observing")
        for _ in 0..<20 { XCTAssertEqual(take(monitor, sample(1000, cpu: 99), now: 1001)["sequence"] as? Int64, 0) }
        var value = first
        for tick in stride(from: 1002.0, through: 1030.0, by: 2) { value = take(monitor, sample(tick, cpu: 99)) }
        XCTAssertEqual(state(value, "cpu"), "warning")
        XCTAssertEqual(value["sequence"] as? Int64, 1)
        let repeated = take(monitor, sample(1030, cpu: 99), now: 1031)
        XCTAssertEqual(repeated["sequence"] as? Int64, 1)
        XCTAssertEqual(repeated["session"] as? String, first["session"] as? String)
        for tick in stride(from: 1032.0, through: 1042.0, by: 2) { value = take(monitor, sample(tick)) }
        XCTAssertEqual(state(value, "cpu"), "ok"); XCTAssertEqual((value["alerts"] as? [Any])?.count, 0)
        XCTAssertEqual(value["sequence"] as? Int64, 1)
    }
    func testFreshNetworkCannotRenewExpiredPressureAndGapsResetObservation() {
        let monitor = MacRuntimeHealth()
        _ = take(monitor, sample(1000, cpu: 99))
        var old = sample(1000, cpu: 99)
        // Network update is new, pressure observation remains old.
        old = SystemMetricsSnapshot(cpuPercent: old.cpuPercent, memoryPercent: old.memoryPercent,
                                    uploadBytesPerSecond: 999, downloadBytesPerSecond: 999,
                                    updatedAt: Date(timeIntervalSince1970: 1040), pressureObservedAt: old.pressureObservedAt)
        let stale = take(monitor, old)
        XCTAssertEqual(stale["epochUtc"] as? Int64, 1000)
        XCTAssertEqual(state(take(monitor, sample(1042, cpu: 99)), "cpu"), "observing")
        XCTAssertEqual(state(take(monitor, sample(1100, cpu: 99)), "cpu"), "observing")
    }
    func testNoObservationOrFutureObservationDoesNotFabricateMonitor() {
        let monitor = MacRuntimeHealth(), now = Date(timeIntervalSince1970: 1000)
        var value = sample(1000); value.pressureObservedAt = nil
        XCTAssertNil(monitor.snapshot(metrics: value, now: now, logicalProcessors: 8, codexEnabled: false, desktopObserved: false))
        XCTAssertNil(monitor.snapshot(metrics: sample(1010), now: now, logicalProcessors: 8, codexEnabled: false, desktopObserved: false))
    }
    func testPrivateFreshnessDoesNotChangeSystemMetricsWireContract() throws {
        let encoded = try JSONEncoder().encode(sample(1000))
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        XCTAssertNil(object["pressureObservedAt"])
        XCTAssertNil(try JSONDecoder().decode(SystemMetricsSnapshot.self, from: encoded).pressureObservedAt)
    }
    func testNormalPairedFrameIncludesMonitorCalendarAndDefaultPetResources() throws {
        let now = Date()
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.usbmodem-fixture",
                                 key: Data(repeating: 8, count: 32), confirmed: true)
        let service = MacTab5Service(port: 8765, host: { "192.168.1.20" },
                                     codex: MacTab5Codex(enabled: { false }, call: { _, _ in [:] }),
                                     load: { pair }, save: { _ in })
        service.publish(MacStatusSnapshot(version: 1, time: "12:00:00", epochUtc: Int64(now.timeIntervalSince1970),
                                          utcOffsetSeconds: 28800, capturedAt: now,
                                          codex: ToolState(state: "idle", ageSeconds: 1),
                                          claude: ToolState(state: "idle", ageSeconds: 1), musicPlaying: false,
                                          weather: nil, stocks: nil, systemMetrics: sample(now.timeIntervalSince1970),
                                          quotas: nil, music: nil))
        service.assets.updatePets([nil, nil])
        let frame = try MacTab5Protocol.object(service.frame())
        let data = try XCTUnwrap(frame["data"] as? [String: Any])
        let health = try XCTUnwrap(data["runtimeHealth"] as? [String: Any])
        XCTAssertEqual(state(health, "cpu"), "ok")
        XCTAssertEqual(state(health, "codex"), "disabled")
        XCTAssertNotNil((data["calendar"] as? [String: Any])?["detail"])
        XCTAssertTrue((data["assetIds"] as? [Any])?[0] is String)
        XCTAssertNil((data["systemMetrics"] as? [String: Any])?["pressureObservedAt"])
    }
}
