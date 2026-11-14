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
    static let shownKey = "automatic_system_permissions_shown_v4"

    static func startupSteps(defaults: UserDefaults = .standard) -> [Step] {
        // On upgrade, revisit only the permissions newly covered by this guide.
        if defaults.bool(forKey: "automatic_system_permissions_shown_v3") { return [.terminal] }
        return defaults.bool(forKey: "automatic_system_permissions_shown_v2")
            ? [.speech, .microphone, .bluetooth, .terminal, .music, .spotify, .localNetwork]
            : Step.allCases
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
            case .accessibility: return "在系统设置中开启 AI-bot Bridge；若未列出，点＋添加。"
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

    static func state(_ step: Step, enabled: (Step) -> Bool) async -> State {
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
            guard let target = running(id) else {
                return NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) == nil ? .unavailable : .openApp
            }
            switch await automation(target, prompt: false) {
            case noErr: return .allowed
            case OSStatus(errAEEventWouldRequireUserConsent): return .request
            case OSStatus(errAEEventNotPermitted): return .settings
            case OSStatus(procNotFound):
                return .openApp
            default: return .unavailable
            }
        }
    }

    static func shouldOffer(_ states: [State], defaults: UserDefaults = .standard) -> Bool {
        !defaults.bool(forKey: shownKey) && states.contains(where: \.needsPermission)
    }

    static func runInitial(
        steps: [Step] = Step.allCases, state: (Step) async -> State, request: (Step, State) async -> Void
    ) async {
        for step in steps {
            guard !Task.isCancelled else { return }
            let current = await state(step)
            guard !Task.isCancelled else { return }
            if step == .localNetwork, current == .settings {
                await request(step, .request)
            } else if current == .request || current == .openApp
                || (step == .accessibility && current == .settings) {
                await request(step, current)
            }
        }
    }

    static func request(_ step: Step, state: State) async {
        guard !Task.isCancelled, state.needsPermission else { return }
        if state == .settings {
            if step == .accessibility, !AXIsProcessTrusted() {
                // Ask macOS to register the app, then open the required pane even
                // when macOS doesn't present its own alert.
                _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
                openSettings(step)
                while !Task.isCancelled, !AXIsProcessTrusted() {
                    try? await Task.sleep(for: .milliseconds(300))
                }
                if !Task.isCancelled, !CGPreflightPostEventAccess() {
                    _ = CGRequestPostEventAccess()
                }
            } else {
                openSettings(step)
            }
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
            _ = CGRequestPostEventAccess()
        case .bluetooth:
            return
        default:
            guard let id = step.target else { return }
            var target = running(id)
            if target == nil, state == .openApp,
               let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) {
                let configuration = NSWorkspace.OpenConfiguration()
                configuration.activates = false
                configuration.addsToRecentItems = false
                target = try? await NSWorkspace.shared.openApplication(at: url, configuration: configuration)
            }
            guard let target, !Task.isCancelled else { return }
            for attempt in 0..<8 {
                let status = await automation(target, prompt: false)
                guard !Task.isCancelled else { return }
                if status == OSStatus(errAEEventWouldRequireUserConsent) {
                    _ = await automation(target, prompt: true)
                    return
                }
                guard status == OSStatus(procNotFound), attempt < 7 else { return }
                try? await Task.sleep(for: .milliseconds(250))
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
