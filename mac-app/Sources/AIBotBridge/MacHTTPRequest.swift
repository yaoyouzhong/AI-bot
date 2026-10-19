import Foundation

struct MacHTTPRequest {
    let method: String
    let target: String
    let headers: [String: String]
    let body: Data

    // nil means more bytes are required. Reject ambiguous HTTP framing before
    // dispatch; binary bodies never pass through a UTF-8 conversion.
    static func parse(_ data: Data) throws -> MacHTTPRequest? {
        let bytes = Data(data)
        guard let end = bytes.range(of: Data("\r\n\r\n".utf8)) else {
            guard bytes.count <= 8192 else { throw MacTab5Error.invalidPacket }; return nil
        }
        guard end.upperBound <= 8192, let head = String(data: bytes[..<end.lowerBound], encoding: .utf8) else { throw MacTab5Error.invalidPacket }
        let lines = head.components(separatedBy: "\r\n")
        let start = (lines.first ?? "").split(separator: " ", omittingEmptySubsequences: false)
        guard start.count == 3, ["GET", "POST"].contains(String(start[0])), start[1].hasPrefix("/"),
              ["HTTP/1.0", "HTTP/1.1"].contains(String(start[2])),
              start[1].utf8.allSatisfy({ $0 > 32 && $0 < 127 }) else { throw MacTab5Error.invalidPacket }
        var headers: [String: String] = [:]
        for line in lines.dropFirst() {
            guard let colon = line.firstIndex(of: ":") else { throw MacTab5Error.invalidPacket }
            let name = String(line[..<colon]).lowercased()
            let raw = line[line.index(after: colon)...]
            guard !name.isEmpty, name.utf8.allSatisfy({ (97...122).contains($0) || (48...57).contains($0) || $0 == 45 }),
                  raw.utf8.allSatisfy({ $0 == 9 || $0 >= 32 && $0 != 127 }), headers[name] == nil else { throw MacTab5Error.invalidPacket }
            headers[name] = raw.trimmingCharacters(in: .whitespaces)
        }
        guard headers["transfer-encoding"] == nil else { throw MacTab5Error.invalidPacket }
        let lengthText = headers["content-length"] ?? "0"
        guard !lengthText.isEmpty, lengthText.utf8.allSatisfy({ (48...57).contains($0) }),
              let length = Int(lengthText), (0...16384).contains(length) else { throw MacTab5Error.invalidPacket }
        let actual = bytes.count - end.upperBound
        if actual < length { return nil }
        guard actual == length else { throw MacTab5Error.invalidPacket }
        return MacHTTPRequest(method: String(start[0]), target: String(start[1]), headers: headers,
                              body: Data(bytes[end.upperBound...]))
    }
}
