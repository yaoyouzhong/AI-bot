import Foundation
import Network

final class HTTPStatusServer {
    private let port: NWEndpoint.Port
    private let token: () -> String?
    private let snapshot: () -> Data
    private let queue = DispatchQueue(label: "AI-bot.status-server")
    private var listener: NWListener?

    init(port: UInt16, token: @escaping () -> String?, snapshot: @escaping () -> Data) {
        self.port = NWEndpoint.Port(rawValue: port)!
        self.token = token
        self.snapshot = snapshot
    }

    func start() throws {
        let listener = try NWListener(using: .tcp, on: port)
        listener.newConnectionHandler = { [weak self] connection in self?.accept(connection) }
        listener.start(queue: queue)
        self.listener = listener
    }

    func stop() {
        listener?.cancel()
        listener = nil
    }

    private func accept(_ connection: NWConnection) {
        connection.start(queue: queue)
        read(connection, buffer: Data())
    }

    private func read(_ connection: NWConnection, buffer: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8_192) { [weak self] data, _, complete, error in
            guard let self else { connection.cancel(); return }
            var next = buffer
            if let data { next.append(data) }
            let headerComplete = next.range(of: Data("\r\n\r\n".utf8)) != nil
            if !headerComplete && !complete && error == nil && next.count < 65_536 {
                self.read(connection, buffer: next)
                return
            }
            guard headerComplete, let request = String(data: next, encoding: .utf8) else {
                connection.cancel()
                return
            }
            let response = self.response(for: request)
            connection.send(content: response, completion: .contentProcessed { _ in connection.cancel() })
        }
    }

    private func response(for request: String) -> Data {
        let lines = request.components(separatedBy: "\r\n")
        guard lines.first?.hasPrefix("GET /status ") == true else {
            return http(status: "404 Not Found", body: Data("{\"error\":\"not_found\"}".utf8))
        }
        guard let expected = token(), expected.utf8.count >= 32 else {
            return http(status: "503 Service Unavailable", body: Data("{\"error\":\"pairing_required\"}".utf8))
        }
        let provided = lines.dropFirst().compactMap { line -> String? in
            guard let separator = line.firstIndex(of: ":"),
                  String(line[..<separator]).caseInsensitiveCompare("X-AIBot-Token") == .orderedSame else { return nil }
            return String(line[line.index(after: separator)...]).trimmingCharacters(in: .whitespaces)
        }.first
        guard let provided, Self.constantTimeEqual(provided, expected) else {
            return http(status: "401 Unauthorized", body: Data("{\"error\":\"unauthorized\"}".utf8))
        }
        return http(status: "200 OK", body: snapshot())
    }

    private func http(status: String, body: Data) -> Data {
        var result = Data("HTTP/1.1 \(status)\r\nContent-Type: application/json\r\nContent-Length: \(body.count)\r\nConnection: close\r\n\r\n".utf8)
        result.append(body)
        return result
    }

    static func constantTimeEqual(_ left: String, _ right: String) -> Bool {
        let a = Array(left.utf8)
        let b = Array(right.utf8)
        var difference = UInt8(truncatingIfNeeded: a.count ^ b.count)
        for index in 0..<max(a.count, b.count) {
            difference |= (index < a.count ? a[index] : 0) ^ (index < b.count ? b[index] : 0)
        }
        return difference == 0
    }
}
