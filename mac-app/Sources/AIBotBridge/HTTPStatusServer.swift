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
    private let espEnabled: () -> Bool
    private let queue = DispatchQueue(label: "AI-bot.status-server")
    private let requests: OperationQueue = { let queue = OperationQueue(); queue.name = "AI-bot.tab5-requests"; queue.maxConcurrentOperationCount = 2; return queue }()
    private var pendingRequests = 0 // owned by the listener queue
    private var listener: NWListener?
    private var listening = false
    private let loopbackOnly: Bool
    private var wanted = false, listenerGeneration: UInt64 = 0
    private var listenerRecovery = MacHTTPListenerRecovery()
    private let observationLock = NSLock()
    private var lastESP: Date?
    private let statusDiagnosticLock = NSLock()
    private var statusRequests = 0, statusResponses = 0, statusSends = 0, statusSendErrors = 0
    private var statusCode = 0, statusBytes = 0, statusResponseMS = 0, statusMaximumMS = 0, statusSendMS = 0, statusSendError: Int32 = 0
    private var listenerDiagnostic = "尚未启动"
    var statusDiagnosticSummary: String {
        statusDiagnosticLock.lock(); defer { statusDiagnosticLock.unlock() }
        return "Mac LAN 监听：\(listenerDiagnostic)\nMac Wi-Fi 状态回包：请求 \(statusRequests)，响应 \(statusResponses)，发送完成 \(statusSends)，发送失败 \(statusSendErrors)\nHTTP \(statusCode)，响应字节 \(statusBytes)，生成 \(statusResponseMS) ms，最大生成 \(statusMaximumMS) ms，发送 \(statusSendMS) ms，错误码 \(statusSendError)\n发送完成只表示本机网络栈已接收，不等于设备已收到。"
    }
    var lastESPRequest: Date? { observationLock.lock(); defer { observationLock.unlock() }; return lastESP }

    init(port: UInt16, token: @escaping () -> String?, resources: @escaping () -> [MacResourcePayload] = { [] }, event: @escaping (String,String,String)->Bool = {_,_,_ in false}, acknowledge: @escaping ()->Void = {}, tab5: MacTab5Service? = nil, espEnabled: @escaping () -> Bool = { true }, loopbackOnly: Bool = false, snapshot: @escaping () -> Data) {
        self.port = port == 0 ? .any : NWEndpoint.Port(rawValue: port)!
        self.token = token
        self.snapshot = snapshot
        self.resources = resources
        self.event=event;self.acknowledge=acknowledge
        self.tab5 = tab5
        self.espEnabled = espEnabled
        self.loopbackOnly = loopbackOnly
    }

    func start() throws {
        try queue.sync {
            guard !wanted else { return }
            wanted=true; listenerGeneration &+= 1; listenerRecovery.begin(ProcessInfo.processInfo.systemUptime)
            do { try installListener() } catch { wanted=false; throw error }
        }
    }
    private func listenerNote(_ value:String) {
        statusDiagnosticLock.lock(); listenerDiagnostic=value; statusDiagnosticLock.unlock()
    }
    private func installListener() throws {
        let parameters = NWParameters.tcp
        if loopbackOnly { parameters.requiredLocalEndpoint = .hostPort(host:.ipv4(.loopback),port:port) }
        // Keep exclusive ownership. SO_REUSEPORT could route requests to two
        // bridge generations with different sessions instead of fixing startup.
        let listener = try loopbackOnly ? NWListener(using:parameters) : NWListener(using:parameters,on:port)
        listenerNote("正在启动")
        listener.stateUpdateHandler = { [weak self, weak listener] state in
            guard let self, let listener, self.listener === listener else { return }
            switch state {
            case .ready: self.listening = true; self.listenerNote("已就绪"); self.tab5?.setLANReady(true)
            case .failed(let error):
                if self.listening { self.listenerRecovery.begin(ProcessInfo.processInfo.systemUptime) }
                self.listening=false; self.tab5?.setLANReady(false); self.listener=nil
                listener.cancel(); self.retryListener(error)
            case .cancelled: self.listening=false; self.tab5?.setLANReady(false)
            default: break
            }
        }
        listener.newConnectionHandler = { [weak self] connection in self?.accept(connection) }
        self.listener = listener
        listener.start(queue: queue)
    }
    private func retryListener(_ error:NWError) {
        let busy:Bool
        if case .posix(.EADDRINUSE) = error { busy=true } else { busy=false }
        guard wanted, let delay=listenerRecovery.nextDelay(now:ProcessInfo.processInfo.systemUptime,addressInUse:busy) else {
            let code:String
            switch error { case .posix(let e): code="POSIX \(e.rawValue)"; case .dns(let e): code="DNS \(e)"; case .tls(let e): code="TLS \(e)"; @unknown default: code="未知错误" }
            listenerNote("未就绪（\(code)）"); return
        }
        listenerNote("端口暂被占用，正在重试")
        let generation=listenerGeneration
        queue.asyncAfter(deadline:.now()+delay) { [weak self] in
            guard let self, self.wanted, self.listenerGeneration==generation, self.listener==nil else { return }
            do { try self.installListener() }
            catch { self.listenerNote("重新启动未完成") }
        }
    }
    func stop() {
        queue.sync {
            wanted=false; listenerGeneration &+= 1; listening=false
            tab5?.setLANReady(false); listener?.cancel(); listener=nil; listenerNote("已停止")
        }
    }
    var boundPort: UInt16? { queue.sync { guard listening, let value = listener?.port?.rawValue, value != 0 else { return nil }; return value } }

    private func accept(_ connection: NWConnection) {
        connection.start(queue: queue)
        readNext(connection, remaining: 640, expires: ProcessInfo.processInfo.systemUptime + 120)
    }

    private func readNext(_ connection: NWConnection, remaining: Int, expires: TimeInterval) {
        let interval = expires - ProcessInfo.processInfo.systemUptime
        guard remaining > 0, interval > 0 else { connection.cancel(); return }
        let timeout = DispatchWorkItem { connection.cancel() }
        queue.asyncAfter(deadline: .now() + min(10, interval), execute: timeout)
        read(connection, buffer: Data(), timeout: timeout, remaining: remaining, expires: expires)
    }

    private func read(_ connection: NWConnection, buffer: Data, timeout: DispatchWorkItem, remaining: Int, expires: TimeInterval) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8_192) { [weak self] data, _, complete, error in
            guard let self else { connection.cancel(); return }
            var next = buffer
            if let data { next.append(data) }
            do {
                guard let request = try MacHTTPRequest.parse(next) else {
                    if !complete && error == nil { self.read(connection, buffer: next, timeout: timeout, remaining: remaining, expires: expires) }
                    else { connection.cancel() }
                    return
                }
                timeout.cancel()
                var loopback = false
                if case let .hostPort(host, _) = connection.endpoint { loopback = ["127.0.0.1", "::1"].contains(String(describing: host)) }
                if request.method == "POST", request.target.hasPrefix("/tab5/") {
                    guard self.pendingRequests < 8 else { self.deliver(self.http(status: "503 Service Unavailable", body: Data()), connection: connection); return }
                    self.pendingRequests += 1
                    let raw = next, local = loopback, queuedAt = ProcessInfo.processInfo.systemUptime
                    self.requests.addOperation { [weak self] in
                        guard let self else { connection.cancel(); return }
                        // Device voice and RPC workers reuse their HTTP client.
                        // Keep sequential responses on that socket; each request
                        // still authenticates independently, with no replay/retry.
                        let close = (request.headers["connection"] ?? "").lowercased().split(separator: ",").contains { $0.trimmingCharacters(in: .whitespaces) == "close" }
                        let persist = ["/tab5/v1/voice", "/tab5/v1/rpc"].contains(request.target) && request.version == "HTTP/1.1" && !close && remaining > 1 && ProcessInfo.processInfo.systemUptime < expires
                        let queued = ProcessInfo.processInfo.systemUptime - queuedAt
                        let response = self.response(for: request, raw: raw, loopback: local, keepAlive: persist, queued: queued)
                        self.queue.async {
                            self.pendingRequests -= 1
                            let again: (() -> Void)? = persist && response.starts(with: Data("HTTP/1.1 200 ".utf8)) ? { self.readNext(connection, remaining: remaining - 1, expires: expires) } : nil
                            self.deliver(response, connection: connection, completion: again)
                        }
                    }
                } else {
                    let observe = request.method == "GET" && request.target == "/tab5/v1/status"
                    let start = ProcessInfo.processInfo.systemUptime
                    if observe { self.statusDiagnosticLock.lock(); self.statusRequests += 1; self.statusDiagnosticLock.unlock() }
                    let response = self.response(for: request, raw: next, loopback: loopback)
                    let generated = ProcessInfo.processInfo.systemUptime
                    if observe {
                        let code = Int(String(data:response.prefix(12),encoding:.utf8)?.split(separator:" ").last ?? "") ?? 0
                        self.statusDiagnosticLock.lock()
                        self.statusResponses += 1; self.statusCode = code; self.statusBytes = response.count
                        self.statusResponseMS = Int(max(0,generated-start)*1000); self.statusMaximumMS = max(self.statusMaximumMS,self.statusResponseMS)
                        self.statusDiagnosticLock.unlock()
                    }
                    self.deliver(response, connection: connection, observation: observe ? { [weak self] error in
                        guard let self else { return }; self.statusDiagnosticLock.lock(); defer { self.statusDiagnosticLock.unlock() }
                        if error == 0 { self.statusSends += 1 } else { self.statusSendErrors += 1 }
                        self.statusSendError = error; self.statusSendMS = Int(max(0,ProcessInfo.processInfo.systemUptime-generated)*1000)
                    } : nil)
                }
            } catch {
                connection.send(content: self.http(status: "400 Bad Request", body: Data()), completion: .contentProcessed { _ in connection.cancel() })
            }
        }
    }

    private func deliver(_ response: Data, connection: NWConnection, completion: (() -> Void)? = nil, observation: ((Int32) -> Void)? = nil) {
        // All callbacks run on the connection's listener queue. Keep this
        // ephemeral observation numeric and complete it at most once.
        var observed = false
        let finish: (Int32) -> Void = { code in if !observed { observed = true; observation?(code) } }
        let timeout = DispatchWorkItem { finish(-1); connection.cancel() }
        queue.asyncAfter(deadline: .now() + 180, execute: timeout)
        send(response, offset: 0, connection: connection, timeout: timeout, completion: completion, observation:finish)
    }

    private func send(_ bytes: Data, offset: Int, connection: NWConnection, timeout: DispatchWorkItem, completion: (() -> Void)?, observation: @escaping (Int32) -> Void) {
        guard offset < bytes.count else { timeout.cancel(); observation(0); if let completion { completion() } else { connection.cancel() }; return }
        let end = min(offset + 16384, bytes.count)
        connection.send(content: Data(bytes[offset..<end]), completion: .contentProcessed { [weak self] error in
            if let error { timeout.cancel(); observation(Int32(truncatingIfNeeded:(error as NSError).code)); connection.cancel() }
            else { self?.send(bytes, offset: end, connection: connection, timeout: timeout, completion: completion, observation:observation) }
        })
    }

    func response(for data: Data, loopback: Bool = false) -> Data {
        guard let request = try? MacHTTPRequest.parse(data) else { return http(status: "400 Bad Request", body: Data()) }
        return response(for: request, raw: data, loopback: loopback)
    }

    private func response(for request: MacHTTPRequest, raw: Data, loopback: Bool, keepAlive: Bool = false, queued: TimeInterval? = nil) -> Data {
        if request.target.hasPrefix("/tab5/") {
            guard request.headers["origin"] == nil else { return http(status: "403 Forbidden", body: Data()) }
            guard let tab5 else { return http(status: "404 Not Found", body: Data()) }
            let device = request.headers["x-aibot-device"] ?? ""
            let nonce = request.headers["x-aibot-nonce"] ?? ""
            let proof = request.headers["x-aibot-proof"] ?? ""
            let result: (Int, Data)
            if request.method == "GET", request.target == "/tab5/v1/status", request.body.isEmpty {
                result = tab5.status(device: device, nonce: nonce, proof: proof, headers: request.headers)
            } else if request.method == "POST", request.target == "/tab5/v1/rpc" {
                result = tab5.rpc(device: device, nonce: nonce, proof: proof, packet: request.body, transport: "Wi-Fi", queued: queued)
            } else if request.method == "POST", request.target == "/tab5/v1/codex/read" || request.target == "/tab5/v1/codex/turn" {
                result = tab5.codexHTTP(device: device, nonce: nonce, proof: proof, packet: request.body, operation: request.target.hasSuffix("/read") ? "read" : "turn", queued: queued)
            } else if request.method == "POST", request.target == "/tab5/v1/codex/image" {
                result = tab5.imageHTTP(device: device, nonce: nonce, proof: proof, packet: request.body)
            } else if request.method == "POST", request.target == "/tab5/v1/voice" {
                result = tab5.voicePacket(device: device, nonce: nonce, proof: proof, packet: request.body, transport: "Wi-Fi", queued: queued)
            } else if request.method == "GET", request.target.hasPrefix("/tab5/v1/ota/"), request.body.isEmpty {
                result = tab5.ota(device: device, nonce: nonce, proof: proof, sha: String(request.target.dropFirst("/tab5/v1/ota/".count)))
            } else { result = (404, Data()) }
            let names = [200: "OK", 400: "Bad Request", 401: "Unauthorized", 404: "Not Found", 500: "Internal Server Error", 503: "Service Unavailable"]
            return http(status: "\(result.0) \(names[result.0] ?? "Error")", body: result.1, type: "application/octet-stream", keepAlive: keepAlive && result.0 == 200)
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
        guard espEnabled() else { return http(status: "503 Service Unavailable", body: Data("{\"error\":\"device_disabled\"}".utf8)) }
        if path == "/status" { if !loopback { observationLock.lock(); lastESP = Date(); observationLock.unlock() }; return http(status: "200 OK", body: snapshot()) }
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

    private func http(status: String, body: Data, type:String = "application/json", keepAlive: Bool = false) -> Data {
        var result = Data("HTTP/1.1 \(status)\r\nContent-Type: \(type)\r\nContent-Length: \(body.count)\r\nConnection: \(keepAlive ? "keep-alive" : "close")\r\n\r\n".utf8)
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
