import Foundation

// Existing Windows/firmware CDC protocol. Capabilities belong to one open
// serial connection; a failed exchange must close it, never retry in place.
struct MacTab5USBMailbox {
    static let maximum = 65535, largeChunk = maximum - 9
    let binary: Bool, chunk: Int, receiveBatch: Int, readyWait: Int
    init(_ hello: [String: Any] = [:]) {
        binary = MacTab5Protocol.integer(hello["rpcUsbBinary"]) == 1
        chunk = binary && MacTab5Protocol.integer(hello["rpcUsbBinaryChunk"]) == Int64(Self.largeChunk)
            ? Self.largeChunk : MacTab5Protocol.integer(hello["rpcUsbChunk"]) == 16384 ? 16384 : 2048
        receiveBatch = binary && MacTab5Protocol.integer(hello["usbRxBatch"]) == 4096 ? 4096 : 0
        readyWait = binary && MacTab5Protocol.integer(hello["rpcUsbWait"]) == 20 ? 20 : 0
    }
    func exchange(device: String, data: Data?, tag: UInt32 = .random(in: .min ... .max),
                  send: ([String: Any]) throws -> Void, write: (Data) throws -> Void,
                  read: (Int) throws -> Data) throws -> Data {
        guard binary, (data?.count ?? 0) <= chunk + 9 else { throw MacTab5Error.invalidPacket }
        try send(["version": 1, "type": "tab5_rpc_binary", "deviceId": device, "tag": tag,
                  "count": data?.count ?? 0, "chunk": chunk, "rxBatch": receiveBatch,
                  "waitMs": data == nil ? readyWait : 0])
        if let data { try write(Self.terminate(data)) }
        let header = try read(16)
        guard header.count == 16, header.prefix(4) == Data("AIB2".utf8),
              MacTab5Mailbox.u32(header, 4) == tag, MacTab5Mailbox.u32(header, 12) == 1 else { throw MacTab5Error.invalidPacket }
        let count = Int(MacTab5Mailbox.u32(header, 8))
        guard count <= Self.maximum, data != nil ? count == 0 : (8...min(Self.maximum, chunk + 8)).contains(count) else { throw MacTab5Error.invalidPacket }
        let body = try read(count)
        guard body.count == count else { throw MacTab5Error.invalidPacket }
        return body
    }
    // End a full USB packet outside its declared body, as Windows does. The
    // device ignores this newline only after consuming the exact binary count.
    static func terminate(_ data: Data) -> Data {
        guard !data.isEmpty, data.count % 64 == 0 else { return data }
        var result = data; result.append(10); return result
    }
}

struct MacTab5USBRPCPollCadence {
    private var lastBulk: TimeInterval?
    mutating func completed(bytes: Int, at now: TimeInterval) { if bytes > 12288 { lastBulk = now } }
    func milliseconds(at now: TimeInterval, active: Bool, voicePriority: Bool) -> Int {
        guard active, !voicePriority, let lastBulk, now >= lastBulk, now - lastBulk < 0.25 else { return 100 }
        // A short lease after actual progress removes one100ms timer gap per
        // range. Idle/duplicate reads cannot renew it; status still runs first.
        return 10
    }
}
