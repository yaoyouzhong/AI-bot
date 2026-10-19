import Foundation
import Network

final class MacTab5Discovery {
    private let service: MacTab5Service
    private let queue = DispatchQueue(label: "AI-bot.tab5-discovery")
    private var listener: NWListener?

    init(service: MacTab5Service) { self.service = service }
    func start() throws {
        let listener = try NWListener(using: .udp, on: NWEndpoint.Port(rawValue: 18766)!)
        listener.stateUpdateHandler = { [weak self] state in
            switch state {
            case .ready: self?.service.setDiscoveryState("已就绪")
            case .failed: self?.service.setDiscoveryState("未启动；可通过 USB 写入电脑地址")
            case .cancelled: self?.service.setDiscoveryState("已停止")
            default: break
            }
        }
        listener.newConnectionHandler = { [weak self] connection in
            guard let self else { connection.cancel(); return }
            connection.start(queue: self.queue)
            self.queue.asyncAfter(deadline: .now() + 3) { connection.cancel() }
            connection.receiveMessage { [weak self] bytes, _, _, error in
                guard error == nil, let bytes, let self,
                      let endpoint = connection.currentPath?.localEndpoint,
                      case let .hostPort(address, _) = endpoint,
                      let reply = self.service.discovery(bytes, address: String(describing: address)) else { connection.cancel(); return }
                connection.send(content: reply, completion: .contentProcessed { _ in connection.cancel() })
            }
        }
        listener.start(queue: queue); self.listener = listener
    }
    func stop() { listener?.cancel(); listener = nil }
}
