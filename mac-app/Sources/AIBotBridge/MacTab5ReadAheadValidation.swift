import Foundation

enum MacTab5ReadAheadValidation {
    static func run() throws -> Int {
        var count = 0, now:TimeInterval = 10, jobs:[() -> Void] = [], reads = 0
        func check(_ name:String,_ condition:@autoclosure () -> Bool) throws {
            guard condition() else { throw MacTab5LocalError.invalid("Read ahead: " + name) }; count += 1
        }
        let cache = MacTab5ReadAhead(clock:{now},schedule:{jobs.append($0)})
        let ok = { () -> MacTab5RPCResult in reads += 1; return .init(200,["text":"fixture"]) }
        try check("first device request never waits for history",cache.response(key:"device/session/page",read:ok).status == 429 && reads == 0 && jobs.count == 1)
        for _ in 0..<20 { _ = cache.response(key:"device/session/page",read:ok) }
        try check("retries share one background read",jobs.count == 1)
        try check("another session cannot create an unbounded queue",cache.response(key:"other/session/page",read:ok).status == 429 && jobs.count == 1)
        jobs.removeFirst()()
        try check("completed exact page is returned",cache.response(key:"device/session/page",read:ok).body["text"] as? String == "fixture" && jobs.isEmpty)
        now += 2
        try check("recent page survives a refresh",cache.response(key:"device/session/page",read:{.error(503,"fixture")}).status == 200 && jobs.count == 1)
        jobs.removeFirst()()
        try check("failure preserves last public page",cache.response(key:"device/session/page",read:ok).status == 200)
        jobs.removeFirst()()
        try check("cached text is never returned to another owner",cache.response(key:"other/session/page",read:ok).status == 429)
        jobs.removeFirst()()
        try check("new owner replaces bounded cache",cache.response(key:"device/session/page",read:ok).status == 429)
        jobs.removeFirst()()
        now += 16
        try check("expired page is not returned",cache.response(key:"device/session/page",read:{.error(404,"fixture")}).status == 429)
        jobs.removeFirst()()
        try check("terminal read failure is delivered",cache.response(key:"device/session/page",read:ok).status == 404 && jobs.isEmpty)
        now = 0
        try check("clock rollback cannot validate a cache entry",cache.response(key:"device/session/page",read:ok).status == 429)
        jobs.removeFirst()()
        now = 10; jobs.removeAll()
        let faster = MacTab5ReadAhead(clock: { now }, freshness: 0.5, schedule: { jobs.append($0) })
        _ = faster.response(key: "claude", read: ok); jobs.removeFirst()()
        now += 0.4
        try check("fresh Claude page avoids a duplicate read", faster.response(key: "claude", read: ok).status == 200 && jobs.isEmpty)
        now += 0.2
        try check("Claude page refreshes in background after half a second", faster.response(key: "claude", read: ok).status == 200 && jobs.count == 1)
        jobs.removeFirst()()
        try check("refreshed page remains bound to its owner", faster.response(key: "other", read: ok).status == 429)
        jobs.removeFirst()()
        // Compare a changing reply at the device's 750 ms cadence. A cached
        // page stays readable while its replacement is fetched asynchronously.
        now = 100; var slowJobs: [() -> Void] = [], fastJobs: [() -> Void] = [], reply = "old"
        let normalCadence = MacTab5ReadAhead(clock: { now }, schedule: { slowJobs.append($0) })
        let claudeCadence = MacTab5ReadAhead(clock: { now }, freshness: 0.5, schedule: { fastJobs.append($0) })
        let changing = { MacTab5RPCResult(200, ["text": reply]) }
        _ = normalCadence.response(key: "same", read: changing); slowJobs.removeFirst()()
        _ = claudeCadence.response(key: "same", read: changing); fastJobs.removeFirst()()
        reply = "new"; now += 0.75
        try check("old threshold defers refresh at 750 ms", normalCadence.response(key: "same", read: changing).body["text"] as? String == "old" && slowJobs.isEmpty)
        try check("Claude schedules a refresh at 750 ms", claudeCadence.response(key: "same", read: changing).body["text"] as? String == "old" && fastJobs.count == 1)
        fastJobs.removeFirst()(); now += 0.75
        try check("next Claude poll returns updated reply", claudeCadence.response(key: "same", read: changing).body["text"] as? String == "new")
        try check("normal threshold still returns old page before its refresh finishes", normalCadence.response(key: "same", read: changing).body["text"] as? String == "old" && slowJobs.count == 1)
        slowJobs.removeFirst()()
        let entered = DispatchSemaphore(value:0), release = DispatchSemaphore(value:0)
        let async = MacTab5ReadAhead()
        defer { release.signal() }
        let began = ProcessInfo.processInfo.systemUptime
        let pending = async.response(key:"slow") {
            entered.signal(); _ = release.wait(timeout:.now()+2)
            return .init(200,["text":"slow public reply"])
        }
        try check("real slow reader releases the device call immediately",pending.status == 429 && ProcessInfo.processInfo.systemUptime-began < 0.5)
        try check("reader actually runs separately",entered.wait(timeout:.now()+1) == .success)
        try check("blocked reader cannot block another device poll",async.response(key:"slow",read:ok).status == 429)
        return count
    }
}
