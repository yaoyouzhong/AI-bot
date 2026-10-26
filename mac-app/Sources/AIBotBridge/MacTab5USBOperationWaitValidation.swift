import Foundation

enum MacTab5USBOperationWaitValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ label: String, _ value: Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("USB operation wait: " + label) }; count += 1
        }
        var now = 0.0, touched = 0.0, beats = [Double](), cadence = MacTab5USBLeaseCadence()
        cadence.acknowledge(started: 0)
        var maxPause = 0.0, expired = false
        // The inspected old receiver has an eight-second ownership lease. A
        // twelve-second host operation must not stop the serial publication loop.
        let result: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { now },
            poll: { now >= 12 ? 42 : nil }, pause: { seconds in
                maxPause = max(maxPause, seconds); now += seconds; expired = expired || now-touched >= 8
            }, cancelled: { false }, due: { cadence.isDue(now) }, heartbeat: {
                beats.append(now); touched = now; cadence.acknowledge(started: now)
            })
        try check("slow operation returns its one result", result == 42)
        try check("old synchronous path would expire", now >= 8)
        try check("ownership remains alive across slow operation", !expired)
        try check("multiple two-second full refreshes", beats.count >= 5 && beats.count <= 7)
        try check("waiting is bounded and does not busy spin", maxPause > 0 && maxPause <= 0.1)
        try check("refreshes stay well within lease", zip(beats.dropFirst(), beats).allSatisfy { $0-$1 < 2.2 })

        // A completed RPC must reach the device before optional maintenance.
        // The existing mailbox reply renews USB ownership on the receiver.
        // A due full-state ACK may stall: it must not discard a result that
        // is already ready, including a voice stop/final response.
        now = 2; var unnecessaryBeats = 0
        let completed: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { now },
            poll: { 73 }, pause: { _ in }, cancelled: { false }, due: { true }, heartbeat: {
                unnecessaryBeats += 1; throw MacTab5Error.timeout
            })
        try check("ready result precedes due heartbeat", completed == 73 && unnecessaryBeats == 0)

        now = 0; var readyAtBoundary = false
        let boundary: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { now },
            poll: { readyAtBoundary ? 74 : nil }, pause: { _ in now = 2; readyAtBoundary = true },
            cancelled: { false }, due: { now >= 2 }, heartbeat: {
                unnecessaryBeats += 1; throw MacTab5Error.timeout
            })
        try check("completion waking at lease cadence avoids unrelated ACK", boundary == 74 && unnecessaryBeats == 0)

        do {
            let _: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { 2 },
                poll: { 75 }, pause: { _ in }, cancelled: { true }, due: { true }, heartbeat: {})
            try check("completed result still respects cancellation", false)
        } catch MacTab5Error.serial { count += 1 }
        do {
            let _: Int = try MacTab5USBOperationWait.run(deadline: 2, clock: { 2 },
                poll: { 76 }, pause: { _ in }, cancelled: { false }, due: { true }, heartbeat: {})
            try check("completed result cannot bypass original deadline", false)
        } catch MacTab5Error.timeout { count += 1 }

        now = 0; var failedBeats = 0
        do {
            let _: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { now }, poll: { nil },
                pause: { now += $0 }, cancelled: { false }, due: { now >= 2 }, heartbeat: {
                    failedBeats += 1; throw MacTab5Error.invalidPacket
                })
            try check("heartbeat failure must stop", false)
        } catch MacTab5Error.invalidPacket { count += 1 }
        try check("failed heartbeat never retries", failedBeats == 1 && now < 2.2)

        now = 0
        do {
            let _: Int = try MacTab5USBOperationWait.run(deadline: 0.4, clock: { now }, poll: { nil },
                pause: { now += $0 }, cancelled: { false }, due: { false }, heartbeat: {})
            try check("original request deadline must stop", false)
        } catch MacTab5Error.timeout { count += 1 }
        try check("request deadline stays bounded", now >= 0.4 && now < 0.5)
        now = 0
        do {
            let _: Int = try MacTab5USBOperationWait.run(deadline: 75, clock: { now }, poll: { nil },
                pause: { now += $0 }, cancelled: { now >= 0.3 }, due: { false }, heartbeat: {})
            try check("disable or pairing change must stop", false)
        } catch MacTab5Error.serial { count += 1 }
        try check("cancellation checked within one short wait", now < 0.5)

        // Real host work executes once on a different queue. Completion wakes
        // the waiter; neither polling nor heartbeats execute the work again.
        let caller = Thread.current
        let pending = MacTab5USBOperationResult { (Thread.current != caller, UUID().uuidString) }
        let actual = try MacTab5USBOperationWait.run(deadline: ProcessInfo.processInfo.systemUptime+3,
            clock: { ProcessInfo.processInfo.systemUptime }, poll: pending.poll, pause: pending.pause,
            cancelled: { false }, due: { false }, heartbeat: {})
        try check("host operation moves off serial caller", actual.0 && !actual.1.isEmpty)
        try check("completion retained without another dispatch", pending.poll()?.1 == actual.1)
        try check("recognizes exact existing authentication-required reply",
            MacTab5USBAuthenticationRequired.matches(["version":1,"type":"tab5_host_required"]))
        try check("other or malformed replies cannot imply authentication",
            !MacTab5USBAuthenticationRequired.matches(["version":1,"type":"tab5_ack"]) &&
            !MacTab5USBAuthenticationRequired.matches(["type":"tab5_host_required"]))
        return count
    }
}
