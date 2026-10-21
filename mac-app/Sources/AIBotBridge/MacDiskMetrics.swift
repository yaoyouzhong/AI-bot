import Foundation
import IOKit

struct MacDiskCounters: Equatable {
    let reads: UInt64, writes: UInt64, readNanoseconds: UInt64, writeNanoseconds: UInt64
}

struct MacDiskObservation: Equatable {
    let milliseconds: Double
    let observedAt: Date
}

/// IOBlockStorageDriver.h defines Total Time as cumulative nanoseconds and
/// Operations as completed requests. Never substitute busy time or load average.
final class MacDiskMetrics {
    private let read: () -> [UInt64: MacDiskCounters]?
    private var previous: [UInt64: MacDiskCounters]?
    private var previousTick: TimeInterval?
    private var latest: MacDiskObservation?

    init(read: @escaping () -> [UInt64: MacDiskCounters]? = MacDiskMetrics.readCounters) { self.read = read }

    func capture(now: Date, uptime: TimeInterval) -> MacDiskObservation? {
        if let latest, now.timeIntervalSince(latest.observedAt) >= 8 || now < latest.observedAt { self.latest = nil }
        guard uptime.isFinite, let current = read() else { previous = nil; previousTick = nil; return latest }
        defer { previous = current; previousTick = uptime }
        guard let previous, let tick = previousTick, uptime > tick, uptime - tick <= 5 else { return latest }
        var operations = 0.0, nanoseconds = 0.0
        for (id, value) in current {
            guard let old = previous[id], value.reads >= old.reads, value.writes >= old.writes,
                  value.readNanoseconds >= old.readNanoseconds, value.writeNanoseconds >= old.writeNanoseconds else { continue }
            let reads = value.reads - old.reads, writes = value.writes - old.writes
            guard reads > 0 || writes > 0 else { continue }
            operations += Double(reads) + Double(writes)
            nanoseconds += Double(value.readNanoseconds - old.readNanoseconds) + Double(value.writeNanoseconds - old.writeNanoseconds)
        }
        // Some drivers expose counters without implementing timing statistics.
        // Completed operations with no measured time do not prove zero latency.
        if operations > 0, nanoseconds > 0 {
            let ms = nanoseconds / operations / 1_000_000
            if ms.isFinite, ms >= 0 { latest = MacDiskObservation(milliseconds: ms, observedAt: now) }
        }
        return latest
    }

    static func readCounters() -> [UInt64: MacDiskCounters]? {
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault, IOServiceMatching("IOBlockStorageDriver"), &iterator) == KERN_SUCCESS else { return nil }
        defer { IOObjectRelease(iterator) }
        var values: [UInt64: MacDiskCounters] = [:], visited = 0
        while true {
            let service = IOIteratorNext(iterator)
            guard service != 0 else { break }
            defer { IOObjectRelease(service) }
            visited += 1
            guard visited <= 128 else { return nil }
            var id: UInt64 = 0
            guard IORegistryEntryGetRegistryEntryID(service, &id) == KERN_SUCCESS,
                  let stats = IORegistryEntryCreateCFProperty(service, "Statistics" as CFString, kCFAllocatorDefault, 0)?.takeRetainedValue() as? [String: Any] else { continue }
            func number(_ key: String) -> UInt64? {
                guard let value = stats[key] as? NSNumber, CFGetTypeID(value) != CFBooleanGetTypeID(),
                      value.doubleValue.isFinite, value.doubleValue >= 0 else { return nil }
                return value.uint64Value
            }
            guard let reads = number("Operations (Read)"), let writes = number("Operations (Write)"),
                  let readTime = number("Total Time (Read)"), let writeTime = number("Total Time (Write)") else { continue }
            values[id] = MacDiskCounters(reads: reads, writes: writes, readNanoseconds: readTime, writeNanoseconds: writeTime)
        }
        return values.isEmpty ? nil : values
    }
}
