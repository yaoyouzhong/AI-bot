import AppKit
import ApplicationServices
import AVFoundation
import CoreBluetooth
import CoreLocation
import Darwin
import Speech
import OSLog
import UserNotifications

@MainActor
enum MacPermissionSetup {
    private static let log = Logger(subsystem: Bundle.main.bundleIdentifier ?? "AIBotBridge", category: "SystemPermissions")
    static let checkedVersionKey = "automatic_system_permissions_reviewed_version"

    enum LaunchMode { case firstLaunch, upgrade }

    static func launchMode(version: String, defaults: UserDefaults = .standard) -> LaunchMode? {
        let checkedVersion = defaults.string(forKey: checkedVersionKey)
        guard checkedVersion != version else { return nil }
        let previousGuide = (2...4).contains { defaults.bool(forKey: "automatic_system_permissions_shown_v\($0)") }
        return checkedVersion != nil || previousGuide ? .upgrade : .firstLaunch
    }

    static func pendingSteps(_ states: [Step: State], mode: LaunchMode) -> [Step] {
        Step.allCases.filter { step in
            let state = states[step] ?? .checking
            guard state.needsPermission else { return false }
            guard mode == .upgrade else { return true }
            // System Events is a background helper. Retry if it stopped or
            // wasn't ready during preflight. Don't open other apps on upgrade.
            return step != .localNetwork && (state != .openApp || step == .systemEvents)
        }
    }

    enum State: Equatable {
        case unused, checking, request, settings, allowed, openApp, unavailable

        var needsPermission: Bool { self == .request || self == .settings || self == .openApp }
        var caption: String {
            switch self {
            case .unused: return "当前未使用"
            case .checking: return "正在检查"
            case .request: return "待授权"
            case .settings: return "请在系统设置中确认"
            case .allowed: return "已授权"
            case .openApp: return "打开应用后可授权"
            case .unavailable: return "当前不可用"
            }
        }
    }

    enum Step: CaseIterable {
        // Accessibility opens System Settings and has no completion callback.
        // Keep it last so it cannot cover the earlier permission dialogs.
        case location, speech, microphone, notifications, bluetooth, terminal, music, spotify, systemEvents, localNetwork, accessibility

        var title: String {
            switch self {
            case .location: return "天气定位"
            case .speech: return "语音识别"
            case .microphone: return "麦克风"
            case .notifications: return "系统通知"
            case .bluetooth: return "蓝牙"
            case .localNetwork: return "本地网络"
            case .terminal: return "Terminal 自动化"
            case .music: return "音乐自动化"
            case .spotify: return "Spotify 自动化"
            case .systemEvents: return "系统事件自动化"
            case .accessibility: return "辅助功能"
            }
        }
        var detail: String {
            switch self {
            case .location: return "使用大致位置更新本地天气。"
            case .speech: return "供 Mac 本地识别使用，可与豆包设置并存。"
            case .microphone: return "仅在你开始使用 Mac 麦克风的录音后收音。"
            case .notifications: return "接收任务、额度、运行异常和版本更新提醒。"
            case .bluetooth: return "连接已配对的 TAB5。"
            case .localNetwork: return "发现设备并支持 Wi-Fi 连接。"
            case .terminal: return "可提前授权；配对 TAB5 后才能操作 Claude Code 会话。"
            case .music, .spotify: return "显示播放状态，并支持从 TAB5 控制播放。"
            case .systemEvents: return "允许从 TAB5 锁定 Mac。"
            case .accessibility: return "允许从设备操作 Mac 上的应用。"
            }
        }
        var target: String? {
            switch self {
            case .terminal: return "com.apple.Terminal"
            case .music: return MacMusicProvider.appleMusic.bundleIdentifier
            case .spotify: return MacMusicProvider.spotify.bundleIdentifier
            case .systemEvents: return "com.apple.systemevents"
            default: return nil
            }
        }
        var settingsPane: String {
            switch self {
            case .location: return "com.apple.preference.security?Privacy_LocationServices"
            case .speech: return "com.apple.preference.security?Privacy_SpeechRecognition"
            case .microphone: return "com.apple.preference.security?Privacy_Microphone"
            case .notifications: return "com.apple.Notifications-Settings.extension"
            case .bluetooth: return "com.apple.preference.security?Privacy_Bluetooth"
            case .localNetwork: return "com.apple.preference.security?Privacy_LocalNetwork"
            case .accessibility: return "com.apple.preference.security?Privacy_Accessibility"
            default: return "com.apple.preference.security?Privacy_Automation"
            }
        }
    }

    static func location(enabled: Bool, status: CLAuthorizationStatus) -> State {
        guard enabled else { return .unused }
        switch status {
        case .authorizedAlways, .authorizedWhenInUse: return .allowed
        case .notDetermined: return .request
        default: return .settings
        }
    }

    static func state(_ step: Step, enabled: (Step) -> Bool, startSystemEvents: Bool = false) async -> State {
        guard enabled(step) else { return .unused }
        switch step {
        case .location: return location(enabled: true, status: MacWeatherLocation.shared.authorizationStatus)
        case .speech:
            switch SFSpeechRecognizer.authorizationStatus() {
            case .authorized: return .allowed
            case .notDetermined: return .request
            default: return .settings
            }
        case .microphone:
            switch AVCaptureDevice.authorizationStatus(for: .audio) {
            case .authorized: return .allowed
            case .notDetermined: return .request
            default: return .settings
            }
        case .notifications:
            switch await UNUserNotificationCenter.current().notificationSettings().authorizationStatus {
            case .authorized, .provisional, .ephemeral: return .allowed
            case .notDetermined: return .request
            default: return .settings
            }
        case .bluetooth:
            switch CBManager.authorization {
            case .allowedAlways: return .allowed
            case .notDetermined: return .request
            default: return .settings
            }
        case .localNetwork:
            if #available(macOS 15, *) { return .settings }
            return .unused
        case .accessibility:
            guard AXIsProcessTrusted() else { return .settings }
            return CGPreflightPostEventAccess() ? .allowed : .request
        default:
            guard let id = step.target else { return .unavailable }
            let checkSystemEvents = step == .systemEvents && startSystemEvents
            var target = running(id)
            if target == nil, checkSystemEvents { target = await startAutomationTarget(id) }
            guard let target else {
                return NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) == nil ? .unavailable : .openApp
            }
            switch await preflightAutomation(target, waitForLaunch: checkSystemEvents) {
            case noErr: return .allowed
            case OSStatus(errAEEventWouldRequireUserConsent): return .request
            case OSStatus(errAEEventNotPermitted): return .settings
            case OSStatus(procNotFound):
                return .openApp
            default: return .unavailable
            }
        }
    }

    static func runInitial(
        steps: [Step] = Step.allCases, mode: LaunchMode = .firstLaunch,
        state: (Step) async -> State, request: (Step, State) async -> Void
    ) async {
        for step in steps {
            guard !Task.isCancelled else { return }
            let current = await state(step)
            guard !Task.isCancelled else { return }
            if step == .localNetwork, current == .settings, mode == .firstLaunch {
                await request(step, .request)
            } else if current == .request || (current == .openApp && (mode == .firstLaunch || step == .systemEvents))
                || (step == .accessibility && current == .settings) {
                await request(step, current)
            }
        }
    }

    static func request(_ step: Step, state: State) async {
        guard !Task.isCancelled, state.needsPermission else { return }
        if state == .settings {
            if step == .accessibility {
                guard !AXIsProcessTrusted() else { return }
                // Register this running app, including after an upgrade. Opening
                // Settings alone can leave the previous build's entry selected.
                _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
            }
            openSettings(step)
            return
        }
        switch step {
        case .location:
            MacWeatherLocation.shared.requestAuthorization()
            while !Task.isCancelled, MacWeatherLocation.shared.authorizationStatus == .notDetermined {
                try? await Task.sleep(for: .milliseconds(250))
            }
        case .speech:
            await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
                SFSpeechRecognizer.requestAuthorization { _ in continuation.resume() }
            }
        case .microphone:
            _ = await AVCaptureDevice.requestAccess(for: .audio)
        case .notifications:
            let center = UNUserNotificationCenter.current()
            await requestNotifications(state: {
                await self.state(.notifications, enabled: { _ in true })
            }, request: {
                do {
                    _ = try await center.requestAuthorization(options: [.alert, .sound])
                } catch {
                    let error = error as NSError
                    log.error("Notification authorization request failed: \(error.domain, privacy: .public) (\(error.code, privacy: .public))")
                }
            })
        case .localNetwork:
            LocalNetworkIdentity.preparePrivacyPrompt()
        case .accessibility:
            guard AXIsProcessTrusted(), !CGPreflightPostEventAccess() else { return }
            _ = CGRequestPostEventAccess()
        case .bluetooth:
            return
        default:
            guard let id = step.target else { return }
            var target = running(id)
            if target == nil, state == .openApp || step == .systemEvents {
                target = await startAutomationTarget(id)
            }
            guard let target, !Task.isCancelled else { return }
            let status = await preflightAutomation(target, waitForLaunch: true)
            if !Task.isCancelled, status == OSStatus(errAEEventWouldRequireUserConsent) {
                _ = await automation(target, prompt: true)
            }
        }
    }

    // Notification Center can return before its first request has registered
    // a decision. Give it time to settle and retry once only if still undecided.
    static func requestNotifications(
        state: () async -> State, request: () async -> Void,
        settle: () async -> Void = { try? await Task.sleep(for: .milliseconds(750)) }
    ) async {
        for attempt in 0..<2 {
            guard !Task.isCancelled, await state() == .request, !Task.isCancelled else { return }
            await request()
            if attempt == 0 { await settle() }
        }
    }

    static func openSettings(_ step: Step) {
        if let url = URL(string: "x-apple.systempreferences:" + step.settingsPane) {
            NSWorkspace.shared.open(url)
        }
    }

    private static func running(_ id: String) -> NSRunningApplication? {
        NSRunningApplication.runningApplications(withBundleIdentifier: id).first { !$0.isTerminated }
    }

    private static func startAutomationTarget(_ id: String) async -> NSRunningApplication? {
        guard !Task.isCancelled, let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) else { return nil }
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = false
        configuration.addsToRecentItems = false
        return try? await NSWorkspace.shared.openApplication(at: url, configuration: configuration)
    }

    private static func preflightAutomation(_ app: NSRunningApplication, waitForLaunch: Bool) async -> OSStatus {
        for attempt in 0..<8 {
            let status = await automation(app, prompt: false)
            guard status == OSStatus(procNotFound), waitForLaunch, attempt < 7, !Task.isCancelled else { return status }
            try? await Task.sleep(for: .milliseconds(250))
        }
        return OSStatus(procNotFound)
    }

    private static func automation(_ app: NSRunningApplication, prompt: Bool) async -> OSStatus {
        let pid = app.processIdentifier
        return await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                let target = NSAppleEventDescriptor(processIdentifier: pid)
                continuation.resume(returning: AEDeterminePermissionToAutomateTarget(
                    target.aeDesc, typeWildCard, typeWildCard, prompt))
            }
        }
    }
}
