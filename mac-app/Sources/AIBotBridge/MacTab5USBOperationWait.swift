import Foundation

/// Host work never touches the serial descriptor. Only the USB queue writes,
/// including full-state heartbeats while a slower host operation is pending.
final class MacTab5USBOperationResult<Value: Sendable>: @unchecked Sendable {
    private let lock = NSLock(), ready = DispatchSemaphore(value: 0)
    private let work: () -> Value
    private var result: Value?
    init(work: @escaping () -> Value) {
        self.work = work
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            let value = self.work()
            lock.lock(); result = value; lock.unlock(); ready.signal()
        }
    }
    func poll() -> Value? { lock.lock(); defer { lock.unlock() }; return result }
    func pause(_ seconds: TimeInterval) { _ = ready.wait(timeout: .now() + seconds) }
}

enum MacTab5USBOperationWait {
    static func run<Value>(deadline: TimeInterval, clock: () -> TimeInterval,
                           poll: () -> Value?, pause: (TimeInterval) -> Void,
                           cancelled: () -> Bool, due: () -> Bool,
                           heartbeat: () throws -> Void) throws -> Value {
        while true {
            guard !cancelled() else { throw MacTab5Error.serial }
            let now = clock()
            guard now < deadline else { throw MacTab5Error.timeout }
            // Deliver a ready reply before a due full-state refresh. Its
            // receiver-side RPC acknowledgement also renews USB ownership.
            // A separate status timeout must not discard this ready result.
            if let value = poll() { return value }
            // Keep the existing short ownership lease; do not lengthen it to
            // cover the host request or replay the operation after a failure.
            if due() { try heartbeat() }
            pause(min(0.1, deadline - now))
        }
    }
}
