import Foundation

enum LocalNetworkIdentity {
    static func privateIPv4() -> String? {
        Host.current().addresses.first { value in
            let parts = value.split(separator: ".").compactMap { Int($0) }
            guard parts.count == 4 else { return false }
            return parts[0] == 10 || parts[0] == 192 && parts[1] == 168 ||
                parts[0] == 172 && parts[1] >= 16 && parts[1] <= 31
        }
    }
}
