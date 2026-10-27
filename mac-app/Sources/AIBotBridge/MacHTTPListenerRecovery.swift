import Foundation

struct MacHTTPListenerRecovery {
    private var started: TimeInterval = 0
    private var attempt = 0
    mutating func begin(_ now: TimeInterval) { started=now; attempt=0 }
    mutating func nextDelay(now:TimeInterval,addressInUse:Bool) -> TimeInterval? {
        guard addressInUse, now >= started, now-started < 30 else { return nil }
        let delay = min(0.5*pow(2,Double(min(attempt,3))),4)
        guard now+delay-started <= 30 else { return nil }
        attempt += 1; return delay
    }
}
