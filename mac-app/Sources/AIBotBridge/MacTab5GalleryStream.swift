import Foundation

enum MacTab5GalleryStream {
    static func encode(_ result: MacTab5RPCResult, key: Data, nonce: String) throws -> Data {
        let count = result.bytes.count
        guard let offset = MacTab5Protocol.integer(result.body["offset"]), offset >= 0,
              (1...MacTab5GalleryBatch.maximumWire).contains(count),
              offset <= MacTab5GalleryBatch.maximumWire - count else { throw MacTab5Error.invalidPacket }
        let size = MacTab5Binary.bulk, blocks = (count + size - 1) / size
        var output = Data("T5S1".utf8)
        for value in [Int(offset), count, blocks] { MacTab5Mailbox.append32(UInt32(value), to: &output) }
        for block in 0..<blocks {
            let start = block * size, length = min(size, count - start)
            let body = ["offset": Int(offset) + start, "count": length, "streamOffset": Int(offset),
                        "streamBytes": count, "block": block, "blocks": blocks]
            let clear = try MacTab5Binary.pack(["status": 200, "body": body],
                                               bytes: result.bytes.subdata(in: start..<start + length), bulk: true)
            let packet = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce, maximum: MacTab5Binary.maximumMailbox)
            MacTab5Mailbox.append32(UInt32(packet.count), to: &output); output.append(packet)
        }
        return output
    }
}
