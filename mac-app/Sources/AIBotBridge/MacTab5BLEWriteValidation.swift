import Foundation

enum MacTab5BLEWriteValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ value: Bool) throws {
            guard value else { throw MacTab5Error.unconfirmed }; checks += 1
        }
        let caps = MacTab5BLEWriteSchedule.Capabilities(["mailboxWindow": 8, "rpcWriteWindow": 64, "rpcReplay": 1])
        let completed = "cancelled;BLE,up,1,262144,100,0;BLE,down,1,262144,200,0;"
        try check(MacTab5BLELinkCheck.verified(completed))
        try check(!MacTab5BLELinkCheck.verified(completed.replacingOccurrences(of: "200,0", with: "200,1")))
        try check(!MacTab5BLELinkCheck.verified(completed.replacingOccurrences(of: "262144,200", with: "262143,200")))
        try check(!MacTab5BLELinkCheck.verified(completed.replacingOccurrences(of: "cancelled", with: "running")))
        try check(!MacTab5BLELinkCheck.verified("failed:unavailable"))
        var wait = MacTab5BLEWaitMetrics()
        wait.begin(1); wait.begin(1.05); wait.end(1.08); wait.end(1.1)
        try check(wait.count == 1 && (79...80).contains(wait.totalMs))
        wait.begin(2); wait.end(2.2, cancelled: true)
        try check(wait.count == 1 && (199...200).contains(wait.cancelledMs))
        wait.begin(3); wait.end(3.3)
        try check(wait.count == 2 && (299...300).contains(wait.maximumMs))
        try check(wait.values["lt100ms"] == 1 && wait.values["ge250ms"] == 1)
        try check(MacTab5BLEWriteSchedule.continuationDelay(now: 2, lastControl: 2, completed: true) == 0)
        try check(MacTab5BLEWriteSchedule.continuationDelay(now: 2.9, lastControl: 2, completed: false) == 0.015)
        try check(MacTab5BLEWriteSchedule.continuationDelay(now: 3, lastControl: 2, completed: false) == nil)
        try check(MacTab5BLEWriteSchedule.continuationDelay(now: 2, lastControl: .infinity, completed: false) == nil)
        try check(MacTab5BLEWriteSchedule.prioritizeBulk(now: 5.9, lastBulk: 5, lastFrame: 0))
        try check(!MacTab5BLEWriteSchedule.prioritizeBulk(now: 6, lastBulk: 5.5, lastFrame: 0))
        try check(!MacTab5BLEWriteSchedule.prioritizeBulk(now: 2, lastBulk: 1, lastFrame: 0))
        try check(!MacTab5BLEWriteSchedule.prioritizeBulk(now: 2, lastBulk: 3, lastFrame: 0))
        try check(caps.regular == 8 && caps.bulk == 64 && caps.replay)
        for offered in [-1, 0, 7, 65] {
            let fallback = MacTab5BLEWriteSchedule.Capabilities(["mailboxWindow": 8, "rpcWriteWindow": offered])
            try check(fallback.bulk == 8 && !fallback.replay)
        }
        // Simulated firmware cursor: gaps fail, identical accepted prefixes pass.
        // Lose each possible command once across three windows, then repair only
        // that response. No new RPC invocation or ciphertext is created.
        for lost in -1..<130 where lost == -1 || (lost + 1) % 64 != 0 && lost != 129 {
            var schedule = MacTab5BLEWriteSchedule(count: 130, mailbox: true, bulk: true, withoutResponse: true, capabilities: caps)
            let ciphertext = (0..<130).map { Data(repeating: UInt8($0), count: 32) }
            var received: [Data] = [], index = 0, dropped = false, barriers = 0, submissions = 0
            while index < ciphertext.count {
                submissions += 1; try check(submissions < 260)
                let acknowledged = schedule.requiresResponse(at: index)
                let accepted: Bool
                if index == lost && !dropped {
                    try check(!acknowledged); dropped = true; accepted = false
                } else if index < received.count { accepted = received[index] == ciphertext[index] }
                else if index == received.count { received.append(ciphertext[index]); accepted = true }
                else { accepted = false }
                index += 1
                if acknowledged {
                    barriers += 1
                    if accepted { schedule.acknowledged(next: index) }
                    else {
                        guard let restart = schedule.recover(protocolError: 14) else { throw MacTab5Error.unconfirmed }
                        index = restart
                        try check(schedule.recover(protocolError: 14) == nil)
                    }
                }
            }
            try check(received == ciphertext && schedule.start == 130)
            try check(schedule.replayCount == (lost == -1 ? 0 : 1))
            if lost == -1 { try check(barriers == 3) }
        }
        for error in [1, 5, 8, 13, 15] {
            var schedule = MacTab5BLEWriteSchedule(count: 130, mailbox: true, bulk: true, withoutResponse: true, capabilities: caps)
            try check(schedule.recover(protocolError: error) == nil)
        }
        for withoutResponse in [true, false] {
            var legacy = MacTab5BLEWriteSchedule(count: 130, mailbox: true, bulk: true, withoutResponse: withoutResponse, capabilities: .init())
            try check((0..<130).allSatisfy { legacy.requiresResponse(at: $0) })
            try check(legacy.recover(protocolError: 14) == nil)
        }
        for mailbox in [true, false] {
            var regular = MacTab5BLEWriteSchedule(count: 19, mailbox: mailbox, bulk: false, withoutResponse: true, capabilities: caps)
            var barriers: [Int] = []
            for i in 0..<19 where regular.requiresResponse(at: i) { barriers.append(i); regular.acknowledged(next: i + 1) }
            try check(barriers == (mailbox ? [7, 15, 18] : [4, 9, 14, 18]))
            try check(regular.recover(protocolError: 14) == nil)
        }
        return checks
    }
}
