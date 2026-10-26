import Foundation
import Darwin

enum LocalNetworkIdentity {
    struct Address {
        let interface: String, value: String, flags: UInt32
        var netmask: String? = nil
    }
    static func select(_ addresses: [Address]) -> String? {
        let eligible = addresses.filter {
            $0.flags & UInt32(IFF_UP | IFF_RUNNING) == UInt32(IFF_UP | IFF_RUNNING) &&
            $0.flags & UInt32(IFF_LOOPBACK | IFF_POINTOPOINT) == 0 && PrivateIPv4Address.isValid($0.value)
        }
        // Prefer an active Ethernet/Wi-Fi interface to local VM bridges. No
        // hostname, reverse-DNS or network request belongs on the USB queue.
        return (eligible.first { $0.interface.hasPrefix("en") } ?? eligible.first)?.value
    }
    static func select(_ addresses: [Address], matching peer: String) -> String? {
        guard PrivateIPv4Address.isValid(peer), let remote = number(peer) else { return nil }
        let matching = addresses.filter { address in
            guard let maskText = address.netmask, let mask = number(maskText),
                  let local = number(address.value), mask != 0 else { return false }
            let inverse = ~mask
            guard inverse & (inverse &+ 1) == 0, inverse > 1 else { return false }
            let host = remote & inverse
            return local != remote && host != 0 && host != inverse && local & mask == remote & mask
        }.sorted { (number($0.netmask ?? "")?.nonzeroBitCount ?? 0) > (number($1.netmask ?? "")?.nonzeroBitCount ?? 0) }
        return select(matching)
    }
    private static func number(_ value: String) -> UInt32? {
        let parts = value.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 4 else { return nil }
        var result: UInt32 = 0
        for part in parts {
            guard let byte = UInt8(part), String(byte) == part else { return nil }
            result = (result << 8) | UInt32(byte)
        }
        return result
    }
    static func privateIPv4() -> String? { select(addresses()) }
    static func addresses() -> [Address] {
        var first: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&first) == 0 else { return [] }
        defer { freeifaddrs(first) }
        func string(_ address: UnsafeMutablePointer<sockaddr>?) -> String? {
            guard let address, address.pointee.sa_family == UInt8(AF_INET) else { return nil }
            var ip = UnsafeRawPointer(address).assumingMemoryBound(to:sockaddr_in.self).pointee.sin_addr
            var buffer = [CChar](repeating:0,count:Int(INET_ADDRSTRLEN))
            guard inet_ntop(AF_INET,&ip,&buffer,socklen_t(INET_ADDRSTRLEN)) != nil else { return nil }
            return String(cString:buffer)
        }
        var result: [Address] = [], cursor = first
        while let node = cursor {
            let entry = node.pointee; cursor = entry.ifa_next
            guard let value = string(entry.ifa_addr) else { continue }
            result.append(.init(interface:String(cString:entry.ifa_name),value:value,flags:entry.ifa_flags,netmask:string(entry.ifa_netmask)))
        }
        return result
    }
}
