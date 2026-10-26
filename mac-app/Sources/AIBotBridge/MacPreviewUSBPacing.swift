import Foundation

// Only an open USB viewer uses spare serial time. Each exchange keeps its
// original deadline/ownership checks; never catch up or replay a mailbox ID.
enum MacPreviewUSBPacing {
    @discardableResult
    static func run(active: Bool, clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                    mayContinue: () -> Bool, exchange: () throws -> Bool) rethrows -> Int {
        let started = clock()
        var count = 0
        repeat {
            guard try exchange() else { break }
            count += 1
        } while active && count < 4 && clock()-started < 0.060 && mayContinue()
        return count
    }
}

struct MacPreviewUSBPollCadence {
    private var lastCompletion: TimeInterval?
    mutating func completed(at now: TimeInterval) { lastCompletion = now }
    func milliseconds(at now: TimeInterval, active: Bool, voicePriority: Bool, foregroundBusy: Bool) -> Int {
        guard active, !voicePriority, !foregroundBusy, let lastCompletion,
              now >= lastCompletion, now-lastCompletion < 0.25 else { return 100 }
        // Firmware needs a scheduling turn after its ACK before publishing the
        // next range. A short post-progress window avoids100ms per fragment,
        // without fast polling during encoding, idle or foreground voice work.
        return 10
    }
}

struct MacPreviewUSBPublication {
    private var acknowledged: String?
    private static func request(_ command: [String:Any]?) -> String? {
        guard command?["transport"] as? String == "USB", let id = command?["requestId"] as? String,
              MacTab5Protocol.isHex(id,count:32) else { return nil }
        return id
    }
    func isDue(_ command: [String:Any]?, voicePriority: Bool) -> Bool {
        guard !voicePriority, let id = Self.request(command) else { return false }
        return id != acknowledged
    }
    // Caller invokes this only after the exact whole-frame ACK. Read the frame
    // actually sent, not a newer request created while the ACK was in flight.
    mutating func acknowledge(_ root: [String:Any]) {
        acknowledged = Self.request(((root["data"] as? [String:Any])?["connectionHealth"] as? [String:Any])?["screenPreviewCommand"] as? [String:Any])
    }
}
