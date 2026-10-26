import XCTest
@testable import AIBotBridge

final class MacDiskMetricsTests: XCTestCase {
    private func counter(_ reads: UInt64, _ writes: UInt64, _ readNs: UInt64, _ writeNs: UInt64) -> MacDiskCounters {
        MacDiskCounters(reads: reads, writes: writes, readNanoseconds: readNs, writeNanoseconds: writeNs)
    }
    func testWeightedDeltaUsesNanosecondsAndStableDeviceIdentity() throws {
        var current: [UInt64: MacDiskCounters]? = [1: counter(100, 50, 500_000_000, 250_000_000)]
        let sampler = MacDiskMetrics(read: { current })
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 100), uptime: 1))
        // A newly attached device must establish its own baseline, not add its lifetime totals.
        current = [1: counter(102, 51, 504_000_000, 255_000_000), 2: counter(900, 800, 900_000_000, 800_000_000)]
        let sample = try XCTUnwrap(sampler.capture(now: Date(timeIntervalSince1970: 102), uptime: 3))
        XCTAssertEqual(sample.milliseconds, 3, accuracy: 0.0001)
        current = [1: counter(103, 51, 505_000_000, 255_000_000), 2: counter(902, 801, 904_000_000, 805_000_000)]
        XCTAssertEqual(try XCTUnwrap(sampler.capture(now: Date(timeIntervalSince1970: 104), uptime: 5)).milliseconds, 2.5, accuracy: 0.0001)
    }
    func testIdleFailureResetAndSleepDoNotInventZeroOrLifetimeAverages() throws {
        var current: [UInt64: MacDiskCounters]? = [1: counter(10, 10, 10_000_000, 10_000_000)]
        let sampler = MacDiskMetrics(read: { current })
        _ = sampler.capture(now: Date(timeIntervalSince1970: 100), uptime: 1)
        current = [1: counter(11, 10, 12_000_000, 10_000_000)]
        let good = try XCTUnwrap(sampler.capture(now: Date(timeIntervalSince1970: 102), uptime: 3))
        XCTAssertEqual(sampler.capture(now: Date(timeIntervalSince1970: 104), uptime: 5), good)
        current = nil
        XCTAssertEqual(sampler.capture(now: Date(timeIntervalSince1970: 106), uptime: 7), good)
        current = [1: counter(1, 1, 1, 1)]
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 110), uptime: 11))
        current = [1: counter(0, 0, 0, 0)]
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 112), uptime: 13))
        current = [1: counter(400, 400, 400_000_000, 400_000_000)]
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 130), uptime: 31))
    }
    func testRuntimeProjectionUsesFreshDiskSampleAndDoesNotChangeMetricsJSON() throws {
        let now = Date(timeIntervalSince1970: 1000)
        var metrics = SystemMetricsSnapshot(cpuPercent: 20, memoryPercent: 40, uploadBytesPerSecond: 0,
            downloadBytesPerSecond: 0, updatedAt: now, pressureObservedAt: now,
            diskObservation: MacDiskObservation(milliseconds: 2.5, observedAt: now))
        let health = MacRuntimeHealth()
        func project(_ date: Date) -> [String: Any] {
            health.snapshot(metrics: metrics, now: date, logicalProcessors: 8, codexEnabled: false, desktopObserved: false)!
        }
        let pressure = try XCTUnwrap(project(now)["pressure"] as? [String: Any])
        XCTAssertEqual(pressure["diskLatencyMs"] as? Double, 2.5)
        XCTAssertNil(pressure["diskQueue"])
        let encoded = try JSONEncoder().encode(metrics)
        XCTAssertNil((try JSONSerialization.jsonObject(with: encoded) as? [String: Any])?["diskObservation"])
        XCTAssertNil(try JSONDecoder().decode(SystemMetricsSnapshot.self, from: encoded).diskObservation)
        metrics.pressureObservedAt = now.addingTimeInterval(9)
        XCTAssertNil((project(now.addingTimeInterval(9))["pressure"] as? [String: Any])?["diskLatencyMs"])
        metrics.diskObservation = MacDiskObservation(milliseconds: 1, observedAt: now.addingTimeInterval(12))
        XCTAssertNil((project(now.addingTimeInterval(9))["pressure"] as? [String: Any])?["diskLatencyMs"])
    }
    func testDriverWithoutTimingStatisticsRemainsUnavailable() {
        var value = counter(10, 10, 0, 0)
        let sampler = MacDiskMetrics(read: { [1: value] })
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 100), uptime: 1))
        value = counter(20, 20, 0, 0)
        XCTAssertNil(sampler.capture(now: Date(timeIntervalSince1970: 102), uptime: 3))
    }
}
