import Darwin
import Foundation

struct MacCpuTicks: Equatable {
    let user: UInt64
    let system: UInt64
    let idle: UInt64
    let nice: UInt64
}

struct MacNetworkTotals: Equatable {
    let uploaded: UInt64
    let downloaded: UInt64
}

final class MacSystemMetricsService {
    private var previousCpu: MacCpuTicks?
    private var previousNetwork: MacNetworkTotals?
    private var previousNetworkAt: TimeInterval?
    private var cpuAt = -Double.infinity
    private var cpuPercent = 0.0, memoryPercent = 0.0
    private var pressureObservedAt: Date?
    private let disk = MacDiskMetrics()
    private var diskObservation: MacDiskObservation?
    private var history = MacNetworkHistory()
    private var snapshot: SystemMetricsSnapshot?

    func capture(now: Date = Date(), uptime: TimeInterval = ProcessInfo.processInfo.systemUptime) -> SystemMetricsSnapshot? {
        if uptime - cpuAt >= 2, let cpu = Self.readCpu(), let memory = Self.readMemoryPercent() {
            if previousCpu != nil { pressureObservedAt = now }
            cpuPercent = previousCpu.map { Self.cpuPercent(previous: $0, current: cpu) } ?? 0
            memoryPercent = memory; previousCpu = cpu; cpuAt = uptime
            diskObservation = disk.capture(now: now, uptime: uptime)
        }
        guard let network = Self.readNetworkTotals() else { return snapshot }
        defer { previousNetwork = network; previousNetworkAt = uptime }
        guard let previousNetwork, let previousNetworkAt else { return snapshot }
        let elapsed = uptime - previousNetworkAt
        guard elapsed > 0 else { return snapshot }
        let sample = MacNetworkSample(
            upload: Self.rate(previous: previousNetwork.uploaded, current: network.uploaded, elapsed: elapsed),
            download: Self.rate(previous: previousNetwork.downloaded, current: network.downloaded, elapsed: elapsed))
        if let publication = history.record(sample, at: uptime, elapsed: elapsed) {
            snapshot = SystemMetricsSnapshot(cpuPercent: cpuPercent, memoryPercent: memoryPercent,
                uploadBytesPerSecond: publication.mean.upload, downloadBytesPerSecond: publication.mean.download,
                updatedAt: now, sampleSession: history.session, sampleSequence: publication.sequence,
                samples: publication.samples, pressureObservedAt: pressureObservedAt, diskObservation: diskObservation)
        }
        return snapshot
    }

    static func cpuPercent(previous: MacCpuTicks, current: MacCpuTicks) -> Double {
        let previousTotal = previous.user + previous.system + previous.idle + previous.nice
        let currentTotal = current.user + current.system + current.idle + current.nice
        guard currentTotal > previousTotal, current.idle >= previous.idle else { return 0 }
        let total = currentTotal - previousTotal
        let idle = current.idle - previous.idle
        return min(100, max(0, 100 * Double(total - idle) / Double(total)))
    }

    static func rate(previous: UInt64, current: UInt64, elapsed: TimeInterval) -> Int64 {
        guard current >= previous, elapsed.isFinite, elapsed > 0 else { return 0 }
        let value = Double(current - previous) / elapsed
        return value >= Double(Int64.max) ? Int64.max : Int64(value)
    }

    private static func readCpu() -> MacCpuTicks? {
        var info = host_cpu_load_info_data_t()
        var count = mach_msg_type_number_t(
            MemoryLayout<host_cpu_load_info_data_t>.stride / MemoryLayout<integer_t>.stride)
        let result = withUnsafeMutablePointer(to: &info) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return nil }
        return MacCpuTicks(user: UInt64(info.cpu_ticks.0), system: UInt64(info.cpu_ticks.1),
                           idle: UInt64(info.cpu_ticks.2), nice: UInt64(info.cpu_ticks.3))
    }

    private static func readMemoryPercent() -> Double? {
        var info = vm_statistics64_data_t()
        var count = mach_msg_type_number_t(
            MemoryLayout<vm_statistics64_data_t>.stride / MemoryLayout<integer_t>.stride)
        let result = withUnsafeMutablePointer(to: &info) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics64(mach_host_self(), HOST_VM_INFO64, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return nil }
        var pageSize: vm_size_t = 0
        guard host_page_size(mach_host_self(), &pageSize) == KERN_SUCCESS else { return nil }
        let total = ProcessInfo.processInfo.physicalMemory
        let availablePages = UInt64(info.free_count) + UInt64(info.inactive_count) +
            UInt64(info.speculative_count)
        let available = min(total, availablePages * UInt64(pageSize))
        return total > 0 ? 100 * Double(total - available) / Double(total) : 0
    }

    private static func readNetworkTotals() -> MacNetworkTotals? {
        var first: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&first) == 0, let first else { return nil }
        defer { freeifaddrs(first) }
        var uploaded: UInt64 = 0
        var downloaded: UInt64 = 0
        var pointer: UnsafeMutablePointer<ifaddrs>? = first
        while let current = pointer {
            let interface = current.pointee
            let flags = Int32(bitPattern: interface.ifa_flags)
            if flags & IFF_UP != 0, flags & IFF_LOOPBACK == 0,
               let address = interface.ifa_addr,
               address.pointee.sa_family == UInt8(AF_LINK),
               let raw = interface.ifa_data {
                let data = raw.assumingMemoryBound(to: if_data.self).pointee
                uploaded += UInt64(data.ifi_obytes)
                downloaded += UInt64(data.ifi_ibytes)
            }
            pointer = interface.ifa_next
        }
        return MacNetworkTotals(uploaded: uploaded, downloaded: downloaded)
    }
}

// Existing TAB5 contract: 250 ms raw samples, one immutable publication per
// second, trailing four-sample mean and at most 64 samples for radio backfill.
struct MacNetworkHistory {
    struct Publication {
        let sequence: Int64
        let samples: [MacNetworkSample]
        let mean: MacNetworkSample
    }
    let session = UUID().uuidString
    private var sequence: Int64 = 0
    private var samples: [MacNetworkSample] = []
    private var nextPublication: TimeInterval?

    mutating func record(_ sample: MacNetworkSample, at tick: TimeInterval, elapsed: TimeInterval) -> Publication? {
        guard tick.isFinite, elapsed.isFinite, elapsed > 0 else { return nil }
        // A suspended sampler cannot manufacture missed quarter-second values.
        if elapsed > 0.75 {
            sequence += Int64(min(1_000_000, floor(elapsed / 0.25) - 1))
            samples.removeAll(keepingCapacity: true)
        }
        sequence += 1
        samples.append(sample)
        if samples.count > 64 { samples.removeFirst(samples.count - 64) }
        if nextPublication == nil { nextPublication = tick + 0.75 }
        guard let next = nextPublication, tick >= next else { return nil }
        nextPublication = next + floor(tick - next) + 1
        let recent = samples.suffix(4)
        // Divide before summation to avoid overflowing large cumulative counters.
        func mean(_ values: [Int64]) -> Int64 {
            let n = Int64(values.count)
            return values.reduce(0) { $0 + $1 / n } + values.reduce(0) { $0 + $1 % n } / n
        }
        return Publication(sequence: sequence, samples: samples,
                           mean: MacNetworkSample(upload: mean(recent.map(\.upload)),
                                                  download: mean(recent.map(\.download))))
    }
}
