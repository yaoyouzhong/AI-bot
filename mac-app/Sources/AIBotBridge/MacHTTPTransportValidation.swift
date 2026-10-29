import Foundation
import Network
import Darwin

enum MacHTTPTransportValidation {
    static func run() throws -> Int {
        var checks = 0
        let queue = DispatchQueue(label:"AI-bot.socket-test")
        func connect(_ port: UInt16) throws -> Int32 {
            let fd = Darwin.socket(AF_INET,SOCK_STREAM,0)
            guard fd >= 0 else { throw MacTab5Error.unavailable }
            var address = sockaddr_in(); address.sin_len = UInt8(MemoryLayout.size(ofValue:address)); address.sin_family = sa_family_t(AF_INET)
            address.sin_port = port.bigEndian; address.sin_addr.s_addr = INADDR_LOOPBACK.bigEndian
            let status = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.connect(fd,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
            guard status == 0 else { Darwin.close(fd); throw MacTab5Error.unavailable }
            return fd
        }
        // Cancel real blocked syscalls, repeatedly reusing descriptors. Neither
        // old reads nor queued writes may touch a newly accepted connection.
        for writing in [false,true] {
            for _ in 0..<12 {
                let listener = try MacHTTPListener(port:0,loopbackOnly:true)
                guard let port = listener.port?.rawValue else { throw MacTab5Error.invalidPacket }
                let ready = DispatchSemaphore(value:0), completed = DispatchSemaphore(value:0)
                listener.stateUpdateHandler = { if case .ready = $0 { ready.signal() } }
                listener.newConnectionHandler = { connection in
                    connection.start(queue:queue)
                    if writing {
                        connection.send(content:Data(repeating:7,count:8*1024*1024),completion:.contentProcessed { _ in completed.signal() })
                    } else {
                        connection.receive(minimumIncompleteLength:1,maximumLength:1024) { _,_,_,_ in completed.signal() }
                    }
                    queue.asyncAfter(deadline:.now()+0.05) { connection.cancel() }
                }
                listener.start(queue:queue)
                guard ready.wait(timeout:.now()+3) == .success else { listener.cancel(); throw MacTab5Error.timeout }
                let client = try connect(port)
                let finished = completed.wait(timeout:.now()+3) == .success
                Darwin.close(client); listener.cancel()
                guard finished else { throw MacTab5Error.timeout }; checks += 1
            }
        }
        let owner = try MacHTTPListener(port:0,loopbackOnly:true)
        guard let port = owner.port?.rawValue else { throw MacTab5Error.invalidPacket }
        do {
            let duplicate = try MacHTTPListener(port:port,loopbackOnly:true); duplicate.cancel(); owner.cancel()
            throw MacTab5Error.invalidPacket
        } catch let error as NWError {
            guard error == .posix(.EADDRINUSE) else { owner.cancel(); throw error }; checks += 1
        }
        owner.cancel() // Cancellation before start must release the descriptor.
        let replacement = try MacHTTPListener(port:port,loopbackOnly:true); replacement.cancel(); checks += 1
        print("MAC_HTTP_SOCKET_OK \(checks) blocked read/write cancellation, descriptor reuse, exclusive listener and pre-start release checks")
        return checks
    }
}
