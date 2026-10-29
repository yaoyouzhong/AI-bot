import Foundation
import Network
import Darwin

// BSD TCP preserves the peer's negotiated MSS for LAN firmware streams.
// All protocol parsing, authentication and deadlines stay in HTTPStatusServer.
final class MacHTTPConnection {
    let endpoint: NWEndpoint
    private let fd: Int32
    private let lock = NSLock()
    private var cancelled = false, closed = false, operations = 0
    private var callbackQueue = DispatchQueue.global()
    private let reads = DispatchQueue(label: "AI-bot.http-socket-read")
    private let writes = DispatchQueue(label: "AI-bot.http-socket-write")
    var onClose: (() -> Void)?
    init(fd: Int32, endpoint: NWEndpoint) {
        self.fd = fd; self.endpoint = endpoint
        var enabled: Int32 = 1
        setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &enabled, socklen_t(MemoryLayout.size(ofValue: enabled)))
        setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &enabled, socklen_t(MemoryLayout.size(ofValue: enabled)))
    }
    deinit { cancel() }
    func start(queue: DispatchQueue) { callbackQueue = queue }
    private func begin() -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard !cancelled else { return false }; operations += 1; return true
    }
    private func finish() {
        lock.lock(); operations -= 1
        let release = cancelled && operations == 0 && !closed
        if release { closed = true }; lock.unlock()
        if release { Darwin.close(fd) }
    }
    func cancel() {
        lock.lock()
        guard !cancelled else { lock.unlock(); return }
        cancelled = true
        // Shutdown unblocks outstanding reads/writes; close only after their
        // final syscall so a reused descriptor cannot be accessed by old work.
        Darwin.shutdown(fd, SHUT_RDWR)
        let release = operations == 0 && !closed
        if release { closed = true }; lock.unlock()
        if release { Darwin.close(fd) }
        onClose?()
    }
    static func error(_ code: Int32) -> NWError { .posix(POSIXErrorCode(rawValue: code) ?? .EIO) }
    func receive(minimumIncompleteLength: Int, maximumLength: Int,
                 completion: @escaping (Data?, NWConnection.ContentContext?, Bool, NWError?) -> Void) {
        reads.async { [self] in
            guard begin() else { callbackQueue.async { completion(nil,nil,true,Self.error(ECANCELED)) }; return }
            var bytes = [UInt8](repeating: 0, count: maximumLength)
            var count: Int
            repeat { count = Darwin.recv(fd, &bytes, bytes.count, 0) } while count < 0 && errno == EINTR
            let error = count < 0 ? Self.error(errno):nil
            let data = count > 0 ? Data(bytes.prefix(count)):nil
            finish()
            callbackQueue.async { completion(data,nil,count <= 0,error) }
        }
    }
    func send(content: Data, completion: NWConnection.SendCompletion) {
        let callback: (NWError?) -> Void
        switch completion {
        case .contentProcessed(let action): callback = action
        case .idempotent: callback = { _ in }
        @unknown default: callback = { _ in }
        }
        writes.async { [self] in
            guard begin() else { callbackQueue.async { callback(Self.error(ECANCELED)) }; return }
            var error: NWError?
            content.withUnsafeBytes { buffer in
                var offset = 0
                while offset < buffer.count {
                    let written = Darwin.send(fd, buffer.baseAddress!.advanced(by: offset), buffer.count-offset, 0)
                    if written < 0 && errno == EINTR { continue }
                    if written <= 0 { error = Self.error(written < 0 ? errno:EIO); break }
                    offset += written
                }
            }
            finish(); let result = error
            callbackQueue.async { callback(result) }
        }
    }
}

final class MacHTTPListener {
    let port: NWEndpoint.Port?
    var stateUpdateHandler: ((NWListener.State) -> Void)?
    var newConnectionHandler: ((MacHTTPConnection) -> Void)?
    private let fd: Int32
    private let lock = NSLock()
    private var stopped = false, started = false, closed = false
    private var connections: [UUID: MacHTTPConnection] = [:]
    init(port: UInt16, loopbackOnly: Bool) throws {
        let family = loopbackOnly ? AF_INET:AF_INET6
        let socket = Darwin.socket(family, SOCK_STREAM, 0)
        guard socket >= 0 else { throw MacHTTPConnection.error(errno) }
        var success = false
        defer { if !success { Darwin.close(socket) } }
        var yes: Int32 = 1
        // Reclaim TIME_WAIT addresses, without SO_REUSEPORT or shared listeners.
        guard setsockopt(socket,SOL_SOCKET,SO_REUSEADDR,&yes,socklen_t(MemoryLayout.size(ofValue:yes))) == 0 else { throw MacHTTPConnection.error(errno) }
        var bound: Int32
        if loopbackOnly {
            var address = sockaddr_in(); address.sin_len = UInt8(MemoryLayout.size(ofValue:address)); address.sin_family = sa_family_t(AF_INET)
            address.sin_port = port.bigEndian; address.sin_addr.s_addr = INADDR_LOOPBACK.bigEndian
            bound = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.bind(socket,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        } else {
            var no: Int32 = 0
            guard setsockopt(socket,IPPROTO_IPV6,IPV6_V6ONLY,&no,socklen_t(MemoryLayout.size(ofValue:no))) == 0 else { throw MacHTTPConnection.error(errno) }
            var address = sockaddr_in6(); address.sin6_len = UInt8(MemoryLayout.size(ofValue:address)); address.sin6_family = sa_family_t(AF_INET6)
            address.sin6_port = port.bigEndian
            bound = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.bind(socket,$0,socklen_t(MemoryLayout<sockaddr_in6>.size)) } }
        }
        guard bound == 0, Darwin.listen(socket,16) == 0 else { throw MacHTTPConnection.error(errno) }
        var address = sockaddr_storage(), length = socklen_t(MemoryLayout<sockaddr_storage>.size)
        guard withUnsafeMutablePointer(to:&address, { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { getsockname(socket,$0,&length) } }) == 0,
              let endpoint = Self.endpoint(&address,length:length), case .hostPort(_,let actualPort) = endpoint else { throw MacHTTPConnection.error(errno) }
        self.fd = socket; self.port = actualPort; success = true
    }
    private static func endpoint(_ address: inout sockaddr_storage, length: socklen_t) -> NWEndpoint? {
        var host = [CChar](repeating:0,count:Int(NI_MAXHOST)), service = [CChar](repeating:0,count:Int(NI_MAXSERV))
        let result = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { getnameinfo($0,length,&host,socklen_t(host.count),&service,socklen_t(service.count),NI_NUMERICHOST|NI_NUMERICSERV) } }
        guard result == 0, let number = UInt16(String(cString:service)), let port = NWEndpoint.Port(rawValue:number) else { return nil }
        var name = String(cString:host)
        if name.hasPrefix("::ffff:") { name = String(name.dropFirst(7)) }
        return .hostPort(host:NWEndpoint.Host(name),port:port)
    }
    func start(queue: DispatchQueue) {
        lock.lock(); guard !started && !stopped else { lock.unlock(); return }; started = true; lock.unlock()
        queue.async { [self] in lock.lock(); let active = !stopped; lock.unlock(); if active { stateUpdateHandler?(.ready) } }
        DispatchQueue.global(qos:.utility).async { [self] in
            defer { lock.lock(); closed = true; Darwin.close(fd); lock.unlock() }
            while true {
                var address = sockaddr_storage(), length = socklen_t(MemoryLayout<sockaddr_storage>.size)
                let accepted = withUnsafeMutablePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.accept(fd,$0,&length) } }
                let code = errno
                lock.lock(); let ending = stopped; lock.unlock()
                if ending { if accepted >= 0 { Darwin.close(accepted) }; break }
                if accepted < 0 {
                    if code == EINTR { continue }
                    queue.async { [self] in stateUpdateHandler?(.failed(MacHTTPConnection.error(code))) }; break
                }
                guard let endpoint = Self.endpoint(&address,length:length) else { Darwin.close(accepted); continue }
                let id = UUID(), connection = MacHTTPConnection(fd:accepted,endpoint:endpoint)
                connection.onClose = { [weak self] in guard let self else { return }; self.lock.lock(); self.connections.removeValue(forKey:id); self.lock.unlock() }
                lock.lock(); let admit = !stopped && connections.count < 32
                if admit { connections[id] = connection }; lock.unlock()
                if admit { queue.async { [self] in newConnectionHandler?(connection) } } else { connection.cancel() }
            }
        }
    }
    func cancel() {
        lock.lock(); guard !stopped else { lock.unlock(); return }; stopped = true
        if !closed { Darwin.shutdown(fd,SHUT_RDWR) }
        let closeNow = !started && !closed, pending = Array(connections.values)
        if closeNow { closed = true; Darwin.close(fd) }; lock.unlock()
        pending.forEach { $0.cancel() }
    }
    deinit { cancel() }
}
