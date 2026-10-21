import AppKit
import CoreGraphics

/// Display-only observation. Distributed notifications are not an authenticated
/// credential-entry gate, and must never authorize automatic unlock or typing.
final class MacScreenLockObservation {
    static let shared = MacScreenLockObservation()
    enum Event { case locked, unlocked, invalidate }
    private let lock = NSLock()
    private var observed: Bool?
    private var observers: [(NotificationCenter, NSObjectProtocol)] = []

    func receive(_ event: Event, onConsole: Bool) {
        lock.lock(); defer { lock.unlock() }
        guard onConsole else { observed = nil; return }
        switch event {
        case .locked: observed = true
        case .unlocked: observed = false
        case .invalidate: observed = nil
        }
    }
    func value(onConsole: Bool) -> Bool? {
        lock.lock(); defer { lock.unlock() }
        if !onConsole { observed = nil }
        return observed
    }
    private static var onConsole: Bool {
        guard let session = CGSessionCopyCurrentDictionary() as? [String: Any],
              let flag = session[kCGSessionOnConsoleKey as String],
              CFGetTypeID(flag as CFTypeRef) == CFBooleanGetTypeID() else { return false }
        return flag as? Bool == true
    }
    var displayedValue: Bool? { value(onConsole: Self.onConsole) }
    func start() {
        precondition(Thread.isMainThread)
        guard observers.isEmpty else { return }
        let distributed = DistributedNotificationCenter.default()
        // These OS notification names are a compatibility observation, not a
        // documented lock-state API. Missing events remain unknown at startup.
        for (name, event) in [("com.apple.screenIsLocked", Event.locked), ("com.apple.screenIsUnlocked", .unlocked)] {
            let token = distributed.addObserver(forName: Notification.Name(name), object: nil, queue: .main) { [weak self] _ in
                self?.receive(event, onConsole: Self.onConsole)
            }
            observers.append((distributed, token))
        }
        let workspace = NSWorkspace.shared.notificationCenter
        for name in [NSWorkspace.sessionDidResignActiveNotification, NSWorkspace.sessionDidBecomeActiveNotification,
                     NSWorkspace.willSleepNotification, NSWorkspace.didWakeNotification] {
            let token = workspace.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                self?.receive(.invalidate, onConsole: false)
            }
            observers.append((workspace, token))
        }
    }
    deinit { for (center, token) in observers { center.removeObserver(token) } }
}
