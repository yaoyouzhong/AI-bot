import CoreBluetooth
import Foundation

// A peer can enable its live CCCD before failing to persist that subscription.
// Permit one standard CoreBluetooth reconciliation only after exact readback.
// This is never notification admission: a successful system callback is still required.
struct MacTab5BLENotificationRecovery {
    private enum Phase { case initial, reading, finished }
    private var phase = Phase.initial

    mutating func needsReadback(after error: Error?) -> Bool {
        guard phase == .initial else { return false }
        phase = .finished
        guard let error = error as NSError?, error.domain == CBATTErrorDomain,
              error.code == CBATTError.insufficientAuthorization.rawValue else { return false }
        phase = .reading
        return true
    }
    mutating func shouldReconcile(_ value: Any?) -> Bool {
        guard phase == .reading else { return false }
        phase = .finished
        return MacTab5Protocol.integer(value) == 1
    }
}
