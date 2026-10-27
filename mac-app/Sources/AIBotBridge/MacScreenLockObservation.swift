import AppKit
import CoreGraphics

/// Distributed notifications never authorize credential input or prove unlock.
/// A pending lock observation may veto contradictory desktop focus, since
/// remote UI automation can leave AX and NSWorkspace focused behind the lock.
final class MacScreenLockObservation {
    static let shared = MacScreenLockObservation()
    enum Event { case locked, unlocked, invalidate }
    private let lock = NSLock()
    private var observed: Bool?
    private var events = (locked:0,unlocked:0,invalidated:0,offConsole:0)
    private var statusChanges:[(TimeInterval,String)] = []
    private var observers: [(NotificationCenter, NSObjectProtocol)] = []

    func receive(_ event: Event, onConsole: Bool) {
        lock.lock(); defer { lock.unlock() }
        switch event {
        case .locked: events.locked += 1
        case .unlocked: events.unlocked += 1
        case .invalidate: events.invalidated += 1
        }
        if !onConsole { events.offConsole += 1 }
        guard onConsole else { if observed != true { observed = nil }; return }
        switch event {
        case .locked: observed = true
        case .unlocked: observed = false
        case .invalidate: if observed != true { observed = nil }
        }
    }
    func value(onConsole: Bool) -> Bool? {
        lock.lock(); defer { lock.unlock() }
        return onConsole ? observed:nil
    }
    private static var onConsole: Bool {
        guard let session = CGSessionCopyCurrentDictionary() as? [String: Any],
              let flag = session[kCGSessionOnConsoleKey as String],
              CFGetTypeID(flag as CFTypeRef) == CFBooleanGetTypeID(),
              let uid = session[kCGSessionUserIDKey as String] as? NSNumber,
              CFGetTypeID(uid) != CFBooleanGetTypeID() else { return false }
        return flag as? Bool == true && uid.uint32Value == getuid()
    }
    /// Optional compatibility metadata returned by a public CoreGraphics API.
    /// The key is undocumented: missing/wrong types remain unknown, never false.
    /// A positive value only vetoes desktop claims; it cannot authorize input.
    static func lockHint(_ value:Any?) -> Bool? {
        guard let value,CFGetTypeID(value as CFTypeRef) == CFBooleanGetTypeID() else { return nil }
        return value as? Bool
    }
    private static var sessionHint:Bool? {
        guard onConsole,let session = CGSessionCopyCurrentDictionary() as? [String:Any] else { return nil }
        return lockHint(session["CGSSessionScreenIsLocked"])
    }
    var displayedValue: Bool? {
        guard Self.onConsole else { return nil }
        if Self.sessionHint == true { return true }
        return value(onConsole:true)
    }
    var diagnostic:String {
        func flag(_ value:Bool?) -> String { value.map { $0 ? "锁定":"未锁定" } ?? "未知" }
        let current = "锁状态观察：通知 \(flag(value(onConsole:Self.onConsole)))；系统兼容标记 \(flag(Self.sessionHint))。标记不授权密码输入。"
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        return current + "\n状态变化（仅内存，旧→新）：\n" + statusChanges.map { "\(Int(max(0,now-$0.0))) 秒前："+$0.1 }.joined(separator:"\n")
    }
    /// Local diagnostics only. Never changes the wire/credential decision and
    /// never receives windows, titles, screenshots, identities or field values.
    func recordStatus(focus:Bool?,returned:Bool?,windows:String,protection:String,now:TimeInterval = ProcessInfo.processInfo.systemUptime) {
        let console = Self.onConsole, hint = Self.sessionHint
        func flag(_ value:Bool?) -> String { value.map { $0 ? "锁":"未锁" } ?? "?" }
        lock.lock(); defer { lock.unlock() }
        let sample = "回包 \(flag(returned))；核验焦点 \(flag(focus))；通知 \(flag(observed))；标记 \(flag(hint))；控制台 \(console ? 1:0)；\(windows)；\(protection)；事件锁/解/失效/非控制台 \(events.locked)/\(events.unlocked)/\(events.invalidated)/\(events.offConsole)"
        guard statusChanges.last?.1 != sample else { return }
        statusChanges.append((now,sample)); statusChanges = Array(statusChanges.suffix(12))
    }
    static func credentialState(focus: Bool?, observed: Bool?, protection:Bool? = false) -> Bool? {
        // Only independent loginwindow verification may return true here.
        // An unlock notification alone must never produce a success result.
        guard protection == false else { return nil }
        if focus == false && observed != false { return nil }
        return focus
    }
    static func desktopFocus(regularFront:Bool,ownProcess:Bool,displayOnly:Bool) -> Bool {
        regularFront || (displayOnly && ownProcess)
    }
    static func displayState(focus: Bool?, observed: Bool?, protection:Bool? = false, desktopEvidence:Bool = false) -> Bool? {
        if focus == true || observed == true || protection == true { return true }
        guard protection == false else { return nil }
        if desktopEvidence { return false }
        if focus == false && observed == false { return false }
        return nil
    }
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
