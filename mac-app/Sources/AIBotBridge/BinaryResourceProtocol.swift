import Foundation

enum MacBinaryResourceKind: UInt8 {
    case textBitmap = 1
    case musicCover = 2
    case petAsset = 3
    case weatherText = 4
    case stockLabels = 5
}

struct MacBinaryResourceChunk {
    let kind: MacBinaryResourceKind
    let transferId: UInt32
    let sequence: UInt16
    let totalChunks: UInt16
    let totalLength: Int
    let wholeCrc32: UInt32
    let payload: Data
    let wireBytes: Data
}

enum MacBinaryResourceError: Error {
    case invalidResourceSize
    case malformedFrame
    case invalidHeader
    case invalidLength
    case invalidChecksum
}

enum MacBinaryResourceProtocol {
    static let maximumPayload = 768
    private static let maximumResourceSize = 1_048_576
    private static let headerLength = 24
    private static let magic: [UInt8] = [65, 73, 66, 49]

    static func createChunks(kind: MacBinaryResourceKind, data: Data,
                             transferId: UInt32) throws -> [MacBinaryResourceChunk] {
        guard !data.isEmpty, data.count <= maximumResourceSize else {
            throw MacBinaryResourceError.invalidResourceSize
        }
        let source = [UInt8](data)
        let totalChunks = UInt16((source.count + maximumPayload - 1) / maximumPayload)
        let wholeCrc = crc32(source)
        return (0..<Int(totalChunks)).map { number in
            let sequence = UInt16(number)
            let offset = number * maximumPayload
            let length = min(maximumPayload, source.count - offset)
            let payload = Array(source[offset..<(offset + length)])
            var decoded = [UInt8](repeating: 0, count: headerLength + length + 4)
            decoded.replaceSubrange(0..<4, with: magic)
            decoded[4] = 1
            decoded[5] = kind.rawValue
            writeLe32(transferId, to: &decoded, at: 6)
            writeLe16(sequence, to: &decoded, at: 10)
            writeLe16(totalChunks, to: &decoded, at: 12)
            writeLe32(UInt32(source.count), to: &decoded, at: 14)
            writeLe16(UInt16(length), to: &decoded, at: 18)
            writeLe32(wholeCrc, to: &decoded, at: 20)
            decoded.replaceSubrange(headerLength..<(headerLength + length), with: payload)
            let chunkCrc = crc32(Array(decoded[0..<(headerLength + length)]))
            writeLe32(chunkCrc, to: &decoded, at: headerLength + length)
            let wire = Data([UInt8(0)] + cobsEncode(decoded) + [UInt8(0)])
            return MacBinaryResourceChunk(kind: kind, transferId: transferId, sequence: sequence,
                                          totalChunks: totalChunks, totalLength: source.count,
                                          wholeCrc32: wholeCrc, payload: Data(payload), wireBytes: wire)
        }
    }

    static func decodeWire(_ wire: Data) throws -> MacBinaryResourceChunk {
        let framed = [UInt8](wire)
        guard framed.count >= 3, framed.first == 0, framed.last == 0 else {
            throw MacBinaryResourceError.malformedFrame
        }
        let decoded = try cobsDecode(Array(framed.dropFirst().dropLast()))
        guard decoded.count >= headerLength + 4,
              Array(decoded[0..<4]) == magic, decoded[4] == 1,
              let kind = MacBinaryResourceKind(rawValue: decoded[5]) else {
            throw MacBinaryResourceError.invalidHeader
        }
        let payloadLength = Int(readLe16(decoded, at: 18))
        let sequence = readLe16(decoded, at: 10)
        let totalChunks = readLe16(decoded, at: 12)
        let totalLength = Int(readLe32(decoded, at: 14))
        guard payloadLength <= maximumPayload, totalChunks > 0, sequence < totalChunks,
              (1...maximumResourceSize).contains(totalLength),
              decoded.count == headerLength + payloadLength + 4 else {
            throw MacBinaryResourceError.invalidLength
        }
        let expectedCrc = readLe32(decoded, at: headerLength + payloadLength)
        guard crc32(Array(decoded[0..<(headerLength + payloadLength)])) == expectedCrc else {
            throw MacBinaryResourceError.invalidChecksum
        }
        return MacBinaryResourceChunk(
            kind: kind, transferId: readLe32(decoded, at: 6), sequence: sequence,
            totalChunks: totalChunks, totalLength: totalLength,
            wholeCrc32: readLe32(decoded, at: 20),
            payload: Data(decoded[headerLength..<(headerLength + payloadLength)]), wireBytes: wire)
    }

    static func cobsEncode(_ input: [UInt8]) -> [UInt8] {
        var output = [UInt8](repeating: 0, count: input.count + input.count / 254 + 2)
        var read = 0
        var write = 1
        var codeIndex = 0
        var code: UInt8 = 1
        while read < input.count {
            if input[read] == 0 {
                output[codeIndex] = code
                code = 1
                codeIndex = write
                write += 1
                read += 1
            } else {
                output[write] = input[read]
                write += 1
                read += 1
                code &+= 1
                if code == 0xFF {
                    output[codeIndex] = code
                    code = 1
                    codeIndex = write
                    write += 1
                }
            }
        }
        output[codeIndex] = code
        return Array(output[0..<write])
    }

    static func cobsDecode(_ input: [UInt8]) throws -> [UInt8] {
        var output = [UInt8](repeating: 0, count: input.count)
        var read = 0
        var write = 0
        while read < input.count {
            let code = Int(input[read])
            read += 1
            guard code != 0, read + code - 1 <= input.count else {
                throw MacBinaryResourceError.malformedFrame
            }
            if code > 1 {
                for _ in 1..<code {
                    output[write] = input[read]
                    write += 1
                    read += 1
                }
            }
            if code != 0xFF, read < input.count {
                output[write] = 0
                write += 1
            }
        }
        return Array(output[0..<write])
    }

    static func crc32(_ data: [UInt8]) -> UInt32 {
        var crc: UInt32 = 0xFFFF_FFFF
        for value in data {
            crc ^= UInt32(value)
            for _ in 0..<8 {
                crc = crc & 1 == 1 ? (crc >> 1) ^ 0xEDB8_8320 : crc >> 1
            }
        }
        return ~crc
    }

    private static func writeLe16(_ value: UInt16, to data: inout [UInt8], at offset: Int) {
        data[offset] = UInt8(truncatingIfNeeded: value)
        data[offset + 1] = UInt8(truncatingIfNeeded: value >> 8)
    }

    private static func writeLe32(_ value: UInt32, to data: inout [UInt8], at offset: Int) {
        for byte in 0..<4 { data[offset + byte] = UInt8(truncatingIfNeeded: value >> UInt32(byte * 8)) }
    }

    private static func readLe16(_ data: [UInt8], at offset: Int) -> UInt16 {
        UInt16(data[offset]) | UInt16(data[offset + 1]) << 8
    }

    private static func readLe32(_ data: [UInt8], at offset: Int) -> UInt32 {
        UInt32(data[offset]) | UInt32(data[offset + 1]) << 8 |
            UInt32(data[offset + 2]) << 16 | UInt32(data[offset + 3]) << 24
    }
}
