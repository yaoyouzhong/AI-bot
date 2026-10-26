import Foundation
import Network

// One listener serves both existing protocols on UDP 18766. Separate listeners
// with port reuse could send a request to the wrong device family.
final class MacTab5Discovery {
    typealias Respond = (Data, String, String?) -> MacDiscoveryResponse?
    private let respond: Respond, note: (String) -> Void
    private let port: NWEndpoint.Port, loopbackOnly: Bool
    private let queue = DispatchQueue(label: "AI-bot.device-discovery")
    private var listener: NWListener?, ready = false
    private var connections: [UUID:NWConnection] = [:]

    convenience init(service: MacTab5Service, esp: MacESPDiscovery? = nil) {
        self.init(respond:{ packet,peer,local in
            Self.route(packet,peer:peer,local:local,esp:esp,tab5:service.discovery)
        },note:service.setDiscoveryState)
    }
    static func route(_ packet: Data, peer: String, local: String?, esp: MacESPDiscovery?, tab5: (Data,String) -> Data?) -> MacDiscoveryResponse? {
        if packet.starts(with:Data("AIBOT_DISCOVER_V1|".utf8)) { return esp?.response(packet,peer:peer) }
        guard let local, let bytes = tab5(packet,local) else { return nil }
        return .init(bytes:bytes,sourceAddress:nil)
    }
    init(port: UInt16 = 18766, loopbackOnly: Bool = false, respond: @escaping Respond, note: @escaping (String) -> Void = { _ in }) {
        self.port = port == 0 ? .any : NWEndpoint.Port(rawValue:port)!
        self.loopbackOnly = loopbackOnly; self.respond = respond; self.note = note
    }
    func start() throws {
        try queue.sync {
            guard listener == nil else { return }
            let parameters = NWParameters.udp
            if loopbackOnly { parameters.requiredLocalEndpoint = .hostPort(host:.ipv4(.loopback),port:port) }
            let listener = try loopbackOnly ? NWListener(using:parameters) : NWListener(using:parameters,on:port)
            listener.stateUpdateHandler = { [weak self, weak listener] state in
                guard let self, let listener, self.listener === listener else { return }
                switch state {
                case .ready: self.ready = true; self.note("已就绪")
                case .failed: self.ready = false; self.note("未启动；可通过 USB 写入电脑地址")
                case .cancelled: self.ready = false; self.note("已停止")
                default: break
                }
            }
            listener.newConnectionHandler = { [weak self, weak listener] connection in
                guard let self, let listener, self.listener === listener else { connection.cancel(); return }
                self.accept(connection)
            }
            listener.start(queue:queue); self.listener = listener
        }
    }
    var boundPort: UInt16? { queue.sync { ready ? listener?.port?.rawValue : nil } }
    func stop() {
        queue.sync {
            listener?.cancel(); listener = nil; ready = false
            for connection in connections.values { connection.cancel() }; connections.removeAll()
            note("已停止")
        }
    }
    private func finish(_ id: UUID) { connections.removeValue(forKey:id)?.cancel() }
    private func accept(_ connection: NWConnection) {
        guard ready, connections.count < 32 else { connection.cancel(); return }
        let id = UUID(); connections[id] = connection
        connection.start(queue:queue)
        queue.asyncAfter(deadline:.now()+3) { [weak self] in self?.finish(id) }
        receive(id, connection)
    }
    private func receive(_ id: UUID, _ connection: NWConnection) {
        connection.receiveMessage { [weak self] packet,_,_,error in
            guard let self, self.connections[id] === connection else { return }
            guard error == nil, let packet, packet.count <= 160,
                  case let .hostPort(peer,_) = connection.endpoint else { self.finish(id); return }
            let local: String?
            if case let .hostPort(host,_)? = connection.currentPath?.localEndpoint { local = String(describing:host) }
            else { local = nil }
            guard let reply = self.respond(packet,String(describing:peer),local) else { self.receive(id,connection); return }
            guard let host = reply.sourceAddress else {
                connection.send(content:reply.bytes,completion:.contentProcessed { [weak self] error in
                    guard let self, self.connections[id] === connection else { return }
                    if error != nil { self.finish(id) } else { self.receive(id,connection) }
                }); return
            }
            guard let source = IPv4Address(host), self.connections.count < 32 else { self.finish(id); return }
            let parameters = NWParameters.udp
            parameters.requiredLocalEndpoint = .hostPort(host:.ipv4(source),port:.any)
            let sender = NWConnection(to:connection.endpoint,using:parameters)
            // Keep receiving on the bounded incoming flow: cancelling it after
            // every reply can discard the next datagram from the same peer.
            let senderID = UUID(); self.connections[senderID] = sender
            self.queue.asyncAfter(deadline:.now()+3) { [weak self] in self?.finish(senderID) }
            sender.stateUpdateHandler = { [weak self, weak sender] state in
                guard let self, let sender, self.connections[senderID] === sender else { return }
                switch state {
                case .ready:
                    sender.stateUpdateHandler = nil
                    sender.send(content:reply.bytes,completion:.contentProcessed { [weak self] _ in self?.finish(senderID) })
                case .failed, .cancelled: self.finish(senderID)
                default: break
                }
            }
            sender.start(queue:self.queue)
            self.receive(id,connection)
        }
    }
}
