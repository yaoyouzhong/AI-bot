import Foundation

// Derive a public identity without replacing the existing Keychain pairing key.
enum MacTab5HostBinding {
    static func id(_ pair: MacTab5Pairing) -> String {
        String(MacTab5Protocol.proof(key: pair.key, message: "AIBOT_TAB5_HOST_V1").prefix(32))
    }
    static func name(_ input: String) -> String {
        var result = ""
        for scalar in input.unicodeScalars {
            let next = CharacterSet.controlCharacters.contains(scalar) || scalar == "|" ? " " : String(scalar)
            guard result.utf8.count + next.utf8.count <= 63 else { break }
            result += next
        }
        return result.isEmpty ? "Mac" : result
    }
    static func claim(_ pair: MacTab5Pairing, nonce: String, address: String, port: UInt16, name: String) throws -> [String: Any] {
        try pair.validate()
        guard MacTab5Protocol.isHex(nonce, count: 32), (1...63).contains(name.utf8.count),
              !name.unicodeScalars.contains(where: { CharacterSet.controlCharacters.contains($0) || $0 == "|" }),
              (address.isEmpty ? port == 0 : PrivateIPv4Address.isValid(address)) else { throw MacTab5Error.invalidPacket }
        let host = id(pair)
        return ["version": 1, "type": "tab5_host_claim", "deviceId": pair.deviceId, "hostId": host,
                "nonce": nonce, "host": address, "port": Int(port), "name": name,
                "proof": MacTab5Protocol.proof(key: pair.key, message: "TAB5_HOST_V1|\(pair.deviceId)|\(host)|\(nonce)|\(address)|\(port)|\(name)")]
    }
    static func confirm(_ reply: [String: Any], pair: MacTab5Pairing, nonce: String) throws {
        if reply["error"] as? String == "device_busy" { throw MacTab5Error.busy }
        guard reply["type"] as? String == "tab5_host_claimed", reply["error"] == nil,
              MacTab5Protocol.isHex(nonce, count: 32), reply["deviceId"] as? String == pair.deviceId,
              reply["hostId"] as? String == id(pair), let proof = reply["proof"] as? String,
              MacTab5Protocol.verify(key: pair.key, message: "TAB5_HOST_ACK_V1|\(pair.deviceId)|\(id(pair))|\(nonce)", proof: proof)
        else { throw MacTab5Error.hostNotTrusted }
    }
}

// One instance per open CDC connection. Consume the nonce before I/O even on
// uncertain failure; changing the endpoint requires a fresh device challenge.
struct MacTab5USBHostSession {
    private(set) var required = false
    private var device: String?
    private var nonce: String?
    private var endpoint: String?
    private var authenticatedHost: String?
    mutating func observe(_ hello: [String: Any]) throws {
        nonce = nil; endpoint = nil; authenticatedHost = nil
        guard hello["type"] as? String == "tab5_hello", MacTab5Protocol.integer(hello["version"]) == 1,
              let id = hello["deviceId"] as? String, MacTab5Protocol.isHex(id, count: 12) else { throw MacTab5Error.identity }
        if let device, device != id { throw MacTab5Error.identity }
        let advertised = hello["hostBindingVersion"] != nil
        if advertised {
            guard MacTab5Protocol.integer(hello["hostBindingVersion"]) == 1,
                  let challenge = hello["hostNonce"] as? String, MacTab5Protocol.isHex(challenge, count: 32)
            else { throw MacTab5Error.invalidPacket }
            nonce = challenge
        } else {
            guard !required else { throw MacTab5Error.invalidPacket }
            nonce = nil
        }
        device = id; required = advertised; endpoint = nil; authenticatedHost = nil
    }
    mutating func authenticate(_ pair: MacTab5Pairing, address: String, port: UInt16, name: String,
                               refresh: () throws -> [String: Any], exchange: ([String: Any]) throws -> [String: Any]) throws {
        guard device == pair.deviceId else { throw MacTab5Error.identity }
        guard required else { return }
        let target = "\(address):\(port)"
        if endpoint == target, authenticatedHost == MacTab5HostBinding.id(pair) { return }
        if nonce == nil { try observe(refresh()) }
        guard let challenge = nonce else { throw MacTab5Error.invalidPacket }
        nonce = nil; endpoint = nil; authenticatedHost = nil
        let command = try MacTab5HostBinding.claim(pair, nonce: challenge, address: address, port: port, name: name)
        try MacTab5HostBinding.confirm(exchange(command), pair: pair, nonce: challenge)
        endpoint = target; authenticatedHost = MacTab5HostBinding.id(pair)
    }
}

// Late callbacks from an old connection or pairing cannot open the RPC gate.
struct MacTab5BLEAdmission {
    private(set) var generation: UInt64 = 0
    private var pair: MacTab5Pairing?
    private(set) var ready = false
    private var proofRequired = false
    mutating func reset() { generation &+= 1; pair = nil; ready = false; proofRequired = false }
    mutating func begin(_ pair: MacTab5Pairing) { reset(); self.pair = pair }
    func accepts(_ generation: UInt64, pair: MacTab5Pairing?) -> Bool {
        self.generation == generation && self.pair != nil && self.pair == pair && pair?.confirmed == true
    }
    mutating func authenticated(_ generation: UInt64, pair: MacTab5Pairing, requiresProof: Bool) throws {
        guard accepts(generation, pair: pair) else { throw MacTab5Error.identity }
        proofRequired = requiresProof; ready = !requiresProof
    }
    mutating func acknowledge(_ bytes: Data, generation: UInt64, pair: MacTab5Pairing, nonce: String, sequence: Int64) throws -> Bool {
        guard accepts(generation, pair: pair), proofRequired, !ready,
              (1...9_007_199_254_740_991).contains(sequence) else { throw MacTab5Error.identity }
        let (_, ack, _) = try MacTab5BLEWire.authenticate(bytes, pair: pair, nonce: nonce)
        guard ack == sequence else { return false }
        ready = true; return true
    }
}
