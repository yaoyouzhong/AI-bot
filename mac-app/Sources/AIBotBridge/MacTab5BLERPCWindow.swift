import Foundation

// Existing type-3 RPC grants. Voice retains its separate eight-packet path.
struct MacTab5BLERPCWindow {
    static func credits(_ root: [String: Any]) -> Int {
        if let value = MacTab5Protocol.integer(root["rpcNotifyWindow"]), (1...32).contains(value) { return Int(value) }
        return MacTab5BLEVoiceWindow.credits(root)
    }
    let id: UInt32, total: Int, began: TimeInterval
    private(set) var bytes: Data
    private(set) var remaining: Int
    private var submitted = false
    let grant: Data

    init(id: UInt32, total: Int, prefix: Data, credits: Int, now: TimeInterval) throws {
        guard id != 0, (124...(MacTab5Protocol.maximumRPC + 96)).contains(total),
              !prefix.isEmpty, prefix.count < total, (1...32).contains(credits), now.isFinite else { throw MacTab5Error.invalidPacket }
        self.id = id; self.total = total; began = now; bytes = prefix; remaining = credits
        var command = Data([3]); MacTab5Mailbox.append32(id, to: &command)
        MacTab5Mailbox.append16(prefix.count, to: &command); command.append(UInt8(credits)); grant = command
    }
    func expired(at now: TimeInterval) -> Bool { !now.isFinite || now < began || now - began >= 4 }
    mutating func accept(_ packet: Data, now: TimeInterval) throws {
        guard !expired(at: now), remaining > 0, bytes.count < total, (9...488).contains(packet.count)
        else { throw MacTab5Error.invalidPacket }
        try MacTab5Mailbox.append(packet, id: id, total: total, to: &bytes)
        remaining -= 1
    }
    mutating func markGrantSubmitted(now: TimeInterval) throws {
        guard !expired(at: now), !submitted else { throw MacTab5Error.invalidPacket }
        submitted = true
    }
    var ready: Bool { submitted && (remaining == 0 || bytes.count == total) }
}
