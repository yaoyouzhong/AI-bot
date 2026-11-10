import AppKit
import ApplicationServices

final class MacClaudeAutomationProbe {
    private static let unconfirmed = OSStatus(-1744)
    private struct Check {
        var status = MacClaudeAutomationProbe.unconfirmed
        var checked = -Double.infinity
        var pending: DispatchGroup?
        func recent(at now: Double) -> OSStatus? { now - checked < 3 ? status : nil }
    }
    private let clock: () -> Double
    private let lock = NSLock()
    private var checks: [pid_t: Check] = [:]

    init(clock: @escaping () -> Double = { ProcessInfo.processInfo.systemUptime }) { self.clock = clock }

    func status(pid: pid_t, request: Bool, observing: Bool, permission: @escaping () -> OSStatus) -> OSStatus {
        if request {
            let result = permission()
            lock.lock(); checks[pid] = .init(status: result, checked: clock()); lock.unlock()
            return result
        }
        let now = clock()
        lock.lock()
        var check = checks[pid] ?? Check()
        if check.pending == nil, now - check.checked < 1 { lock.unlock(); return check.status }
        let pending: DispatchGroup
        if let current = check.pending { pending = current }
        else {
            if checks.count > 16 { checks = checks.filter { $0.value.pending != nil } }
            pending = DispatchGroup(); pending.enter()
            check.pending = pending; checks[pid] = check
            DispatchQueue.global(qos: .utility).async {
                let result = permission()
                self.lock.lock()
                if self.checks[pid]?.pending === pending {
                    self.checks[pid] = .init(status: result, checked: self.clock())
                }
                self.lock.unlock()
                pending.leave()
            }
        }
        lock.unlock()
        // A refresh in flight is not a denial. Only passive presentation can
        // keep showing a recently checked result while that refresh runs.
        if observing, let recent = check.recent(at: now) { return recent }
        _ = pending.wait(timeout: .now() + 0.1)
        lock.lock(); defer { lock.unlock() }
        guard let result = checks[pid], result.pending == nil else { return Self.unconfirmed }
        return result.status
    }

    func isChecking(_ pid: pid_t) -> Bool {
        lock.lock(); defer { lock.unlock() }
        return checks[pid]?.pending != nil
    }
}

enum MacClaudeWindowPermissions {
    static let hosts = [("Terminal", "com.apple.Terminal")]
    static var accessible: Bool { AXIsProcessTrusted() && CGPreflightPostEventAccess() }

    private static let probe = MacClaudeAutomationProbe()
    private static func permission(_ app: NSRunningApplication, request: Bool) -> OSStatus {
        let target = NSAppleEventDescriptor(processIdentifier: app.processIdentifier)
        return AEDeterminePermissionToAutomateTarget(target.aeDesc, typeWildCard, typeWildCard, request)
    }
    static func automation(_ app: NSRunningApplication, request: Bool, observing: Bool = false) -> OSStatus {
        guard !app.isTerminated else { return OSStatus(procNotFound) }
        return probe.status(pid: app.processIdentifier, request: request, observing: observing) {
            permission(app, request: request)
        }
    }

    static func summary() -> String {
        var lines = [accessible ? "窗口输入权限已就绪" : "窗口输入需要系统辅助功能权限"]
        for (name, bundle) in hosts {
            if let app = NSRunningApplication.runningApplications(withBundleIdentifier: bundle).first {
                let status = automation(app, request: false)
                let checking = probe.isChecking(app.processIdentifier)
                lines.append(name + (checking ? " 自动化权限核验中" : status == noErr ? " 自动化权限已就绪" : " 自动化权限待设置"))
            } else {
                let installed = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundle) != nil
                lines.append(name + (installed ? " 请打开后检查权限" : " 尚未安装"))
            }
        }
        if MacScreenLockObservation.shared.displayedValue == true { lines.append("Mac 已锁定，解锁后可向 Terminal 输入") }
        return lines.joined(separator: "\n")
    }

    static func configure(completion: @escaping () -> Void) {
        if !AXIsProcessTrusted() {
            _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
            if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
                NSWorkspace.shared.open(url)
            }
        }
        // One explicit setup action owns all prompts. Discovery never prompts.
        let apps = hosts.compactMap { NSRunningApplication.runningApplications(withBundleIdentifier: $0.1).first }
        DispatchQueue.global(qos: .userInitiated).async {
            for app in apps where automation(app, request: false) != noErr {
                _ = automation(app, request: true)
            }
            DispatchQueue.main.async(execute: completion)
        }
    }
}
