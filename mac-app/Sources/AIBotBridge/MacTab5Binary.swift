import Foundation

struct MacTab5RPCResult {
    let status: Int
    var body: [String: Any]
    var bytes = Data()
    init(_ status: Int, _ body: [String: Any], bytes: Data = Data()) { self.status = status; self.body = body; self.bytes = bytes }
    static func error(_ status: Int, _ code: String) -> Self { Self(status, ["error": code]) }
}

enum MacTab5Binary {
    static let maximumMailbox = 65535
    static let maximumRequest = maximumMailbox - 96
    static let bulk = 49152

    static func open(_ clear: Data, response: Bool = false) throws -> ([String: Any], Data) {
        let magic = String(decoding: clear.prefix(4), as: UTF8.self)
        guard magic == "T5R1" || magic == "T5R2" else { return (try MacTab5Protocol.object(clear), Data()) }
        guard clear.count >= 10 else { throw MacTab5Error.invalidPacket }
        let size = Int(MacTab5Mailbox.u32(clear, 4))
        guard (2...4096).contains(size), clear.count > 8 + size,
              clear.count - 8 - size <= (magic == "T5R2" ? bulk : response ? 16384 : 8192) else { throw MacTab5Error.invalidPacket }
        let metadata = try MacTab5Protocol.object(Data(clear[8..<8 + size]))
        let target = response ? metadata["body"] as? [String: Any] : metadata
        guard let target, target["data"] == nil else { throw MacTab5Error.invalidPacket }
        return (metadata, Data(clear.dropFirst(8 + size)))
    }

    static func pack(_ metadata: [String: Any], bytes: Data, bulk: Bool, response: Bool = true) throws -> Data {
        let json = try MacTab5Protocol.json(metadata)
        let target = response ? metadata["body"] as? [String: Any] : metadata
        guard let target, target["data"] == nil, (2...4096).contains(json.count),
              (1...(bulk ? Self.bulk : response ? 16384 : 8192)).contains(bytes.count) else { throw MacTab5Error.invalidPacket }
        var wire = Data((bulk ? "T5R2" : "T5R1").utf8)
        MacTab5Mailbox.append32(UInt32(json.count), to: &wire); wire.append(json); wire.append(bytes); return wire
    }
    static func encode(_ result: MacTab5RPCResult, binary: Bool, bulk: Bool) throws -> Data {
        var body = result.body
        if !result.bytes.isEmpty {
            if binary { return try pack(["status": result.status, "body": body], bytes: result.bytes, bulk: bulk) }
            body["data"] = result.bytes.base64EncodedString()
        }
        return try MacTab5Protocol.json(["status": result.status, "body": body])
    }
}
