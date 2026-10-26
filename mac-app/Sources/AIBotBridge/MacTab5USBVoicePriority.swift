import Foundation

enum MacTab5USBVoicePriority {
    // The serial descriptor stays on its owner queue. A live take pumps its
    // control request first; the existing status/ownership heartbeat still runs.
    // Recheck after RPC: a start may have begun recording in this same step.
    static func run(rpcSupported: Bool, voiceBusy: () -> Bool,
                    claim: () throws -> Void, status: () throws -> Void, rpc: () throws -> Void,
                    resources: () throws -> Int) rethrows -> Int {
        let voiceFirst = rpcSupported && voiceBusy()
        if voiceFirst { try claim(); try rpc() }
        try status()
        if rpcSupported && !voiceFirst { try rpc() }
        return voiceBusy() ? 0 : try resources()
    }
}
