import Foundation
import Network

final class HTTPStatusServer {
    private let port: NWEndpoint.Port
    private let token: () -> String?
    private let snapshot: () -> Data
    private let resources: () -> [MacResourcePayload]
    private let event: (String,String,String) -> Bool
    private let acknowledge: () -> Void
    private let tab5: MacTab5Service?
    private let queue = DispatchQueue(label: "AI-bot.status-server")
    private var listener: NWListener?

    init(port: UInt16, token: @escaping () -> String?, resources: @escaping () -> [MacResourcePayload] = { [] }, event: @escaping (String,String,String)->Bool = {_,_,_ in false}, acknowledge: @escaping ()->Void = {}, tab5: MacTab5Service? = nil, snapshot: @escaping () -> Data) {
        self.port = NWEndpoint.Port(rawValue: port)!
        self.token = token
        self.snapshot = snapshot
        self.resources = resources
        self.event=event;self.acknowledge=acknowledge
        self.tab5 = tab5
    }

    func start() throws {
        let listener = try NWListener(using: .tcp, on: port)
        listener.stateUpdateHandler = { [weak self] state in
            switch state {
            case .ready: self?.tab5?.setLANReady(true)
            case .failed, .cancelled: self?.tab5?.setLANReady(false)
            default: break
            }
        }
        listener.newConnectionHandler = { [weak self] connection in self?.accept(connection) }
        listener.start(queue: queue)
        self.listener = listener
    }

    func stop() {
        tab5?.setLANReady(false)
        listener?.cancel()
        listener = nil
    }

    private func accept(_ connection: NWConnection) {
        connection.start(queue: queue)
        queue.asyncAfter(deadline:.now()+10) {connection.cancel()}
        read(connection, buffer: Data())
    }

    private func read(_ connection: NWConnection, buffer: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8_192) { [weak self] data, _, complete, error in
            guard let self else { connection.cancel(); return }
            var next = buffer
            if let data { next.append(data) }
            do {
                guard let request = try MacHTTPRequest.parse(next) else {
                    if !complete && error == nil { self.read(connection, buffer: next) }
                    else { connection.cancel() }
                    return
                }
                var loopback = false
                if case let .hostPort(host, _) = connection.endpoint { loopback = ["127.0.0.1", "::1"].contains(String(describing: host)) }
                let response = self.response(for: request, raw: next, loopback: loopback)
                connection.send(content: response, completion: .contentProcessed { _ in connection.cancel() })
            } catch {
                connection.send(content: self.http(status: "400 Bad Request", body: Data()), completion: .contentProcessed { _ in connection.cancel() })
            }
        }
    }

    func response(for data: Data, loopback: Bool = false) -> Data {
        guard let request = try? MacHTTPRequest.parse(data) else { return http(status: "400 Bad Request", body: Data()) }
        return response(for: request, raw: data, loopback: loopback)
    }

    private func response(for request: MacHTTPRequest, raw: Data, loopback: Bool) -> Data {
        if request.target.hasPrefix("/tab5/") {
            guard request.headers["origin"] == nil else { return http(status: "403 Forbidden", body: Data()) }
            guard let tab5 else { return http(status: "404 Not Found", body: Data()) }
            let device = request.headers["x-aibot-device"] ?? ""
            let nonce = request.headers["x-aibot-nonce"] ?? ""
            let proof = request.headers["x-aibot-proof"] ?? ""
            let result: (Int, Data)
            if request.method == "GET", request.target == "/tab5/v1/status", request.body.isEmpty {
                result = tab5.status(device: device, nonce: nonce, proof: proof)
            } else if request.method == "POST", request.target == "/tab5/v1/rpc" {
                result = tab5.rpc(device: device, nonce: nonce, proof: proof, packet: request.body)
            } else { result = (404, Data()) }
            let names = [200: "OK", 400: "Bad Request", 401: "Unauthorized", 404: "Not Found", 500: "Internal Server Error", 503: "Service Unavailable"]
            return http(status: "\(result.0) \(names[result.0] ?? "Error")", body: result.1, type: "application/octet-stream")
        }
        guard let text = String(data: raw, encoding: .utf8) else { return http(status: "400 Bad Request", body: Data()) }
        return response(for: text, loopback: loopback)
    }

    func response(for request: String, loopback: Bool = false) -> Data {
        let lines = request.components(separatedBy: "\r\n")
        let parts = (lines.first ?? "").split(separator: " ")
        guard parts.count == 3 else {
            return http(status: "404 Not Found", body: Data("{\"error\":\"not_found\"}".utf8))
        }
        if parts[0] == "POST", loopback, !lines.contains(where:{$0.lowercased().hasPrefix("origin:")}) {
            if parts[1] == "/completion/ack" {acknowledge();return http(status:"200 OK",body:Data("{\"ok\":true}".utf8))}
            if parts[1] == "/event", let separator=request.range(of:"\r\n\r\n"),let data=String(request[separator.upperBound...]).data(using:.utf8),
               let body=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any],let agent=body["agent"] as? String,let kind=body["event"] as? String,event(agent,kind,body["message"] as? String ?? "") {return http(status:"200 OK",body:Data("{\"ok\":true}".utf8))}
            return http(status:"400 Bad Request",body:Data())
        }
        guard parts[0] == "GET" else{return http(status:"404 Not Found",body:Data())}
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
        let path = String(parts[1])
        if path == "/status" { return http(status: "200 OK", body: snapshot()) }
        let values = resources()
        if path == "/resources" {
            let entries: [[String: Any]] = values.map { ["kind": Int($0.kind.rawValue), "crc": MacBinaryResourceProtocol.crc32(Array($0.data)), "length": $0.data.count] }
            let data = (try? JSONSerialization.data(withJSONObject: ["version": 1, "resources": entries])) ?? Data("{}".utf8)
            return http(status: "200 OK", body: data)
        }
        let components = path.split(separator: "/")
        if components.count == 3, components[0] == "resources", let kind = UInt8(components[1]), let crc = UInt32(components[2]),
           let resource = values.first(where: { $0.kind.rawValue == kind }) {
            guard MacBinaryResourceProtocol.crc32(Array(resource.data)) == crc else { return http(status: "409 Conflict", body: Data()) }
            return http(status: "200 OK", body: resource.data, type:"application/octet-stream")
        }
        return http(status: "404 Not Found", body: Data())
    }

    private func http(status: String, body: Data, type:String = "application/json") -> Data {
        var result = Data("HTTP/1.1 \(status)\r\nContent-Type: \(type)\r\nContent-Length: \(body.count)\r\nConnection: close\r\n\r\n".utf8)
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
