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
    private var previousNetworkAt: Date?

    func capture(now: Date = Date()) -> SystemMetricsSnapshot? {
        guard let cpu = Self.readCpu(), let memory = Self.readMemoryPercent() else { return nil }
        let cpuPercent = previousCpu.map { Self.cpuPercent(previous: $0, current: cpu) } ?? 0
        previousCpu = cpu

        var upload: Int64 = 0
        var download: Int64 = 0
        if let network = Self.readNetworkTotals() {
            if let previousNetwork, let previousNetworkAt {
                let elapsed = now.timeIntervalSince(previousNetworkAt)
                upload = Self.rate(previous: previousNetwork.uploaded, current: network.uploaded, elapsed: elapsed)
                download = Self.rate(previous: previousNetwork.downloaded, current: network.downloaded, elapsed: elapsed)
            }
            previousNetwork = network
            previousNetworkAt = now
        }
        return SystemMetricsSnapshot(cpuPercent: cpuPercent, memoryPercent: memory,
                                     uploadBytesPerSecond: upload, downloadBytesPerSecond: download,
                                     updatedAt: now)
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
        guard current >= previous, elapsed > 0 else { return 0 }
        return Int64(Double(current - previous) / elapsed)
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
