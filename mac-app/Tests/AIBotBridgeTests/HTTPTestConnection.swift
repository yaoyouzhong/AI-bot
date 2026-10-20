import Darwin
import Foundation

// A sequential HTTP client that never reconnects automatically. This models
// the device stream and catches failures hidden by URLSession reconnection.
final class HTTPTestConnection {
    private let socket: Int32
    init(port: UInt16) throws {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw failure() }
        var timeout = timeval(tv_sec: 3, tv_usec: 0), enabled: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &timeout, socklen_t(MemoryLayout.size(ofValue: timeout)))
        setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, &timeout, socklen_t(MemoryLayout.size(ofValue: timeout)))
        setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &enabled, socklen_t(MemoryLayout.size(ofValue: enabled)))
        var address = sockaddr_in(); address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET); address.sin_port = port.bigEndian
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        let result = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size)) }
        }
        guard result == 0 else { Darwin.close(fd); throw failure() }
        socket = fd
    }
    deinit { Darwin.close(socket) }
    func exchange(_ request: Data) throws -> (head: String, body: Data) {
        try request.withUnsafeBytes { buffer in
            var offset = 0
            while offset < buffer.count {
                let count = Darwin.send(socket, buffer.baseAddress!.advanced(by: offset), buffer.count - offset, 0)
                guard count > 0 else { throw failure() }; offset += count
            }
        }
        var response = Data()
        while response.range(of: Data("\r\n\r\n".utf8)) == nil {
            try receive(into: &response); guard response.count <= 8192 else { throw failure() }
        }
        let boundary = response.range(of: Data("\r\n\r\n".utf8))!
        let head = String(decoding: response[..<boundary.lowerBound], as: UTF8.self)
        let length = head.components(separatedBy: "\r\n").first(where: { $0.hasPrefix("Content-Length: ") }).flatMap { Int($0.dropFirst(16)) }
        guard let length, (0...65535).contains(length) else { throw failure() }
        while response.count - boundary.upperBound < length { try receive(into: &response) }
        guard response.count - boundary.upperBound == length else { throw failure() }
        return (head, Data(response[boundary.upperBound...]))
    }
    func isClosed() -> Bool {
        var byte: UInt8 = 0
        let count = Darwin.recv(socket, &byte, 1, MSG_PEEK)
        return count == 0 || count < 0 && [ECONNRESET, ENOTCONN].contains(errno)
    }
    private func receive(into response: inout Data) throws {
        var buffer = [UInt8](repeating: 0, count: 8192)
        let count = buffer.withUnsafeMutableBytes { Darwin.recv(socket, $0.baseAddress, $0.count, 0) }
        guard count > 0 else { throw failure() }; response.append(contentsOf: buffer.prefix(count))
    }
}
private func failure() -> Error { NSError(domain: "HTTPTestConnection", code: 1) }
