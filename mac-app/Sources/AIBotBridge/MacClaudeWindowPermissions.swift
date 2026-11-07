import AppKit
import ApplicationServices

enum MacClaudeWindowPermissions {
    static let hosts = [("Terminal", "com.apple.Terminal")]
    static var accessible: Bool { AXIsProcessTrusted() && CGPreflightPostEventAccess() }

    private struct Check { var status: OSStatus; var checked: Double; var running: Bool }
    private static let lock = NSLock()
    private static var checks: [pid_t: Check] = [:]
    private static func permission(_ app: NSRunningApplication, request: Bool) -> OSStatus {
        let target = NSAppleEventDescriptor(processIdentifier: app.processIdentifier)
        return AEDeterminePermissionToAutomateTarget(target.aeDesc, typeWildCard, typeWildCard, request)
    }
    static func automation(_ app: NSRunningApplication, request: Bool) -> OSStatus {
        guard !app.isTerminated else { return OSStatus(procNotFound) }
        let pid = app.processIdentifier
        if request {
            let status = permission(app, request: true)
            lock.lock(); checks[pid] = .init(status: status, checked: ProcessInfo.processInfo.systemUptime, running: false); lock.unlock()
            return status
        }
        // TCC may block even a no-prompt query while another app has an open
        // consent dialog. Keep UI/capability polling bounded and fail closed.
        let now = ProcessInfo.processInfo.systemUptime
        lock.lock()
        if let cached = checks[pid], cached.running || now - cached.checked < 1 {
            lock.unlock(); return cached.status
        }
        if checks.count > 16 { checks = checks.filter { $0.value.running } }
        checks[pid] = .init(status: OSStatus(-1744), checked: now, running: true)
        lock.unlock()
        let finished = DispatchSemaphore(value: 0)
        DispatchQueue.global(qos: .utility).async {
            let status = permission(app, request: false)
            lock.lock(); checks[pid] = .init(status: status, checked: ProcessInfo.processInfo.systemUptime, running: false); lock.unlock()
            finished.signal()
        }
        _ = finished.wait(timeout: .now() + 0.1)
        lock.lock(); defer { lock.unlock() }
        return checks[pid]?.status ?? OSStatus(-1744)
    }

    static func summary() -> String {
        var lines = [accessible ? "窗口输入权限已就绪" : "窗口输入需要系统辅助功能权限"]
        for (name, bundle) in hosts {
            if let app = NSRunningApplication.runningApplications(withBundleIdentifier: bundle).first {
                let status = automation(app, request: false)
                lock.lock(); let checking = checks[app.processIdentifier]?.running == true; lock.unlock()
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
