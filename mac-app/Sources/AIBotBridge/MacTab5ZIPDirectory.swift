import Foundation

// Read only the bounded ZIP central directory, preserving UTF-8 names rather
// than parsing locale-dependent `unzip -Z1` output. Decompression remains the
// system tool, with explicit size limits and application SHA validation.
// Format reference: https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
enum MacTab5ZIPDirectory {
    private static func u64(_ bytes: Data, _ at: Int) -> UInt64 { UInt64(MacTab5Mailbox.u32(bytes, at)) | UInt64(MacTab5Mailbox.u32(bytes, at + 4)) << 32 }
    static func names(_ url: URL, maximumEntries: Int) throws -> [String] {
        guard maximumEntries > 0 else { throw MacTab5Error.invalidPacket }
        let file = try FileHandle(forReadingFrom: url); defer { try? file.close() }
        let size = try file.seekToEnd()
        guard size >= 22 else { throw MacTab5Error.invalidPacket }
        func read(_ offset: UInt64, _ count: Int) throws -> Data {
            guard offset <= size, count >= 0, UInt64(count) <= size - offset, count <= 16 * 1024 * 1024 else { throw MacTab5Error.invalidPacket }
            try file.seek(toOffset: offset); var bytes = Data()
            while bytes.count < count { guard let next = try file.read(upToCount: count - bytes.count), !next.isEmpty else { throw MacTab5Error.invalidPacket }; bytes.append(next) }; return bytes
        }
        let tailSize = Int(min(size, 65557)), tail = try read(size - UInt64(tailSize), tailSize)
        guard let end = stride(from: tail.count - 22, through: 0, by: -1).first(where: { MacTab5Mailbox.u32(tail, $0) == 0x06054b50 && $0 + 22 + Int(MacTab5Mailbox.u16(tail, $0 + 20)) == tail.count }),
              MacTab5Mailbox.u16(tail, end + 4) == 0, MacTab5Mailbox.u16(tail, end + 6) == 0 else { throw MacTab5LocalError.invalid("ZIP 目录不完整，或为不支持的分卷包。") }
        let endOffset = size - UInt64(tailSize) + UInt64(end)
        var centralEnd = endOffset
        var count = UInt64(MacTab5Mailbox.u16(tail, end + 10)), length = UInt64(MacTab5Mailbox.u32(tail, end + 12)), offset = UInt64(MacTab5Mailbox.u32(tail, end + 16))
        if count == 0xffff || length == 0xffffffff || offset == 0xffffffff {
            guard endOffset >= 20 else { throw MacTab5Error.invalidPacket }
            let locator = try read(endOffset - 20, 20)
            guard MacTab5Mailbox.u32(locator, 0) == 0x07064b50, MacTab5Mailbox.u32(locator, 4) == 0, MacTab5Mailbox.u32(locator, 16) == 1 else { throw MacTab5Error.invalidPacket }
            let recordOffset = u64(locator, 8)
            guard endOffset >= 76, recordOffset <= endOffset - 76 else { throw MacTab5Error.invalidPacket }
            let record = try read(recordOffset, 56)
            guard MacTab5Mailbox.u32(record, 0) == 0x06064b50, u64(record, 4) >= 44, MacTab5Mailbox.u32(record, 16) == 0, MacTab5Mailbox.u32(record, 20) == 0, u64(record, 24) == u64(record, 32) else { throw MacTab5Error.invalidPacket }
            guard u64(record, 4) <= endOffset - 20 - recordOffset - 12 else { throw MacTab5Error.invalidPacket }; centralEnd = recordOffset
            count = u64(record, 32); length = u64(record, 40); offset = u64(record, 48)
        } else { guard MacTab5Mailbox.u16(tail, end + 8) == MacTab5Mailbox.u16(tail, end + 10) else { throw MacTab5Error.invalidPacket } }
        guard count > 0, count <= UInt64(maximumEntries), length <= 16 * 1024 * 1024, offset <= centralEnd, length <= centralEnd - offset else { throw MacTab5Error.invalidPacket }
        let directory = try read(offset, Int(length)); var at = 0, result: [String] = []
        for _ in 0..<Int(count) {
            guard at <= directory.count - 46, MacTab5Mailbox.u32(directory, at) == 0x02014b50,
                  MacTab5Mailbox.u16(directory, at + 8) & 1 == 0, MacTab5Mailbox.u16(directory, at + 34) == 0,
                  MacTab5Mailbox.u32(directory, at + 38) >> 16 & 0xf000 != 0xa000 else { throw MacTab5Error.invalidPacket }
            let nameSize = Int(MacTab5Mailbox.u16(directory, at + 28)), extraSize = Int(MacTab5Mailbox.u16(directory, at + 30)), commentSize = Int(MacTab5Mailbox.u16(directory, at + 32)), end = at + 46 + nameSize + extraSize + commentSize
            guard nameSize > 0, end <= directory.count else { throw MacTab5Error.invalidPacket }
            let raw = Data(directory[at + 46..<at + 46 + nameSize]), extra = Data(directory[at + 46 + nameSize..<at + 46 + nameSize + extraSize])
            var unicode: String?, position = 0
            while position < extra.count {
                guard position <= extra.count - 4 else { throw MacTab5Error.invalidPacket }
                let tag = MacTab5Mailbox.u16(extra, position), length = Int(MacTab5Mailbox.u16(extra, position + 2)); position += 4
                guard length <= extra.count - position else { throw MacTab5Error.invalidPacket }
                if tag == 0x7075 {
                    guard unicode == nil, length >= 5, extra[position] == 1, MacTab5Mailbox.u32(extra, position + 1) == MacBinaryResourceProtocol.crc32(Array(raw)),
                          let text = String(data: Data(extra[position + 5..<position + length]), encoding: .utf8) else { throw MacTab5Error.invalidPacket }; unicode = text
                }
                position += length
            }
            guard let name = unicode ?? String(data: raw, encoding: .utf8), !name.utf8.contains(0) else { throw MacTab5LocalError.invalid("ZIP 文件名缺少有效 UTF-8 标识。") }
            if MacTab5Mailbox.u16(directory, at + 8) & 0x800 != 0 { guard let decoded = String(data: raw, encoding: .utf8), unicode == nil || unicode == decoded else { throw MacTab5Error.invalidPacket } }
            result.append(name); at = end
        }
        guard at == directory.count else { throw MacTab5Error.invalidPacket }; return result
    }
}
