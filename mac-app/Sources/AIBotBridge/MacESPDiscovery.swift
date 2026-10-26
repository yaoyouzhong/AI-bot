import Foundation

struct MacDiscoveryResponse {
    let bytes: Data
    // ESP firmware verifies that the UDP sender equals the advertised host.
    let sourceAddress: String?
}

final class MacESPDiscovery {
    private let counterLock = NSLock()
    private var responseCounter = 0
    var responseCount: Int { counterLock.lock(); defer { counterLock.unlock() }; return responseCounter }
    private let token: () -> String?, enabled: () -> Bool, port: () -> UInt16?
    private let address: (String) -> String?
    init(token: @escaping () -> String?, enabled: @escaping () -> Bool,
         port: @escaping () -> UInt16?, address: @escaping (String) -> String? = {
             LocalNetworkIdentity.select(LocalNetworkIdentity.addresses(),matching:$0)
         }) {
        self.token = token; self.enabled = enabled; self.port = port; self.address = address
    }
    func response(_ packet: Data, peer: String) -> MacDiscoveryResponse? {
        guard enabled(), packet.count <= 160, let text = String(data:packet,encoding:.ascii) else { return nil }
        let parts = text.components(separatedBy:"|")
        guard parts.count == 3, parts[0] == "AIBOT_DISCOVER_V1",
              MacTab5Protocol.isHex(parts[1],count:16), MacTab5Protocol.isHex(parts[2],count:64),
              let token = token(), (32...128).contains(token.utf8.count),
              Self.verify(token:token,message:parts.prefix(2).joined(separator:"|"),proof:parts[2]),
              let host = address(peer),
              let port = port(), port > 0, enabled() else { return nil }
        // The HTTP listener is wildcard-bound and follows address changes. Its
        // boundPort is non-nil only after ready; never restart it for discovery.
        let body = "AIBOT_BRIDGE_V1|\(parts[1])|\(host)|\(port)"
        counterLock.lock(); responseCounter += 1; counterLock.unlock()
        return .init(bytes:Data((body + "|" + MacTab5Protocol.proof(key:Data(token.utf8),message:body)).utf8),sourceAddress:host)
    }
    private static func verify(token: String, message: String, proof: String) -> Bool {
        // ESP uses the UTF-8 token as the HMAC key, not TAB5's 32-byte key.
        let expected = MacTab5Protocol.proof(key:Data(token.utf8),message:message)
        return zip(expected.utf8,proof.utf8).reduce(UInt8(0)) { $0 | ($1.0 ^ $1.1) } == 0
    }
}
