import Foundation

// Existing voice-only type-3 grant. Owns one bounded window, never retries.
struct MacTab5BLEVoiceWindow {
    static func credits(_ root: [String: Any]) -> Int {
        guard let value = MacTab5Protocol.integer(root["mailboxWindow"]), (1...8).contains(value) else { return 0 }
        return Int(value)
    }
    static func shouldProbe(now: TimeInterval, lastProbe: TimeInterval, index: Int, count: Int) -> Bool {
        now.isFinite && index > 0 && index < count && index % 5 == 0 && now - lastProbe >= 1
    }
    let id: UInt32, total: Int, began: TimeInterval
    private(set) var bytes: Data
    private(set) var remaining: Int
    private var submitted = false
    let grant: Data

    init(id: UInt32, total: Int, prefix: Data, credits: Int, now: TimeInterval) throws {
        guard id != 0, (124...12288).contains(total), !prefix.isEmpty, prefix.count < total,
              (1...8).contains(credits), now.isFinite else { throw MacTab5Error.invalidPacket }
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
    // Called when submitting a local command, or after ACK on the fallback.
    // Submission alone never completes a window; matching packets must arrive.
    mutating func markGrantSubmitted(now: TimeInterval) throws {
        guard !expired(at: now), !submitted else { throw MacTab5Error.invalidPacket }
        submitted = true
    }
    var ready: Bool { submitted && (remaining == 0 || bytes.count == total) }
}
