import Foundation

enum MacTab5USBResourceBurstValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ name: String, _ condition: @autoclosure () -> Bool) throws {
            guard condition() else { throw MacTab5LocalError.invalid("USB resource burst: " + name) }
            checked += 1
        }
        // Fast ACKs use idle capacity, but cannot monopolize the serial queue.
        var now = 100.0, chunks = 0
        let fast = MacTab5USBResourceBurst.run(clock: { now }, shouldSend: { _ in true }) {
            chunks += 1; now += 0.003; return true
        }
        try check("fast ACKs bounded by chunk count", fast == 8 && chunks == 8)
        // A slow exchange may finish, but must not start another after the budget.
        chunks = 0; now = 100
        let slow = MacTab5USBResourceBurst.run(clock: { now }, shouldSend: { _ in true }) {
            chunks += 1; now += 0.035; return true
        }
        try check("slow ACKs yield on elapsed time", slow == 2 && chunks == 2)
        chunks = 0; now = 100
        let stalled = MacTab5USBResourceBurst.run(clock: { now }, shouldSend: { _ in true }) {
            chunks += 1; now += 4; return true
        }
        try check("existing reply timeout cannot multiply within batch", stalled == 1 && chunks == 1)
        // A new control/configuration/voice event is rechecked before selecting data.
        chunks = 0
        let interrupted = MacTab5USBResourceBurst.run(clock: { 100 }, shouldSend: { $0 < 1 }) {
            chunks += 1; return true
        }
        try check("new priority work stops burst", interrupted == 1 && chunks == 1)
        chunks = 0
        let busy = MacTab5USBResourceBurst.run(clock: { 100 }, shouldSend: { _ in false }) {
            chunks += 1; return true
        }
        try check("busy does not consume a fragment", busy == 0 && chunks == 0)
        var attempts = 0
        let drained = MacTab5USBResourceBurst.run(clock: { 100 }, shouldSend: { _ in true }) {
            attempts += 1; return attempts == 1
        }
        try check("completed asset stops immediately", drained == 1 && attempts == 2)
        enum Failure: Error { case reply }
        attempts = 0
        do {
            try MacTab5USBResourceBurst.run(clock: { 100 }, shouldSend: { _ in true }) {
                attempts += 1; throw Failure.reply
            }
            throw MacTab5LocalError.invalid("USB resource burst swallowed a reply error")
        } catch Failure.reply {
            try check("failed exchange returns to reconnect path", attempts == 1)
        }
        return checked
    }
}
