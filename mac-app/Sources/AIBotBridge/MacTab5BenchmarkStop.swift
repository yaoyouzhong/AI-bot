import Foundation

@MainActor enum MacTab5BenchmarkStop {
    static func run(gallery:Bool, request:(String) async throws -> [String:Any],
                    now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                    wait:() async throws -> Void = { try await Task.sleep(nanoseconds:250_000_000) }) async throws -> String {
        var reply = try await request(gallery ? "gallery_cancel":"cancel")
        let deadline = now() + 15
        while true {
            guard let value = reply["result"] as? String, value.utf8.count <= 8192 else { throw MacTab5Error.invalidPacket }
            if !value.hasPrefix("running;") { return value }
            guard now() < deadline else { throw MacTab5Error.timeout }
            try await wait()
            reply = try await request(gallery ? "gallery_status":"status")
        }
    }
}
