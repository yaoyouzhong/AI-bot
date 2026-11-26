import XCTest
import CoreLocation
@testable import AIBotBridge

final class MacFeaturePreferencesTests: XCTestCase {
    @MainActor func testInitialPermissionRequestsIncludeInstalledAppsAndLocalNetwork() async {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        let states: [Step: State] = [.location: .request, .speech: .allowed, .microphone: .settings,
            .notifications: .request, .terminal: .openApp, .music: .request, .spotify: .unavailable,
            .systemEvents: .openApp, .localNetwork: .settings, .accessibility: .settings]
        var requested: [Step] = []
        await MacPermissionSetup.runInitial(state: { states[$0] ?? .unused }, request: { step, _ in
            await Task.yield()
            requested.append(step)
        })
        XCTAssertEqual(requested, [.location, .terminal, .music, .localNetwork, .accessibility])
    }

    @MainActor func testConflictingAccessibilityCopyIsVisibleWithoutRequestingConsent() async {
        for mode in [MacPermissionSetup.LaunchMode.firstLaunch, .upgrade] {
            let steps = MacPermissionSetup.pendingSteps([.accessibility: .conflictingCopy, .systemEvents: .request], mode: mode)
            XCTAssertTrue(steps.contains(.accessibility))
            var requested: [MacPermissionSetup.Step] = []
            await MacPermissionSetup.runInitial(steps: steps, mode: mode,
                state: { $0 == .accessibility ? .conflictingCopy : .request },
                request: { step, _ in requested.append(step) })
            XCTAssertEqual(requested, [.systemEvents])
        }
    }

    @MainActor func testPermissionReviewRunsOncePerVersionIncludingLegacyInstalls() {
        let (defaults, name) = preferences()
        defer { defaults.removePersistentDomain(forName: name) }
        XCTAssertEqual(MacPermissionSetup.launchMode(version: "0.6.22", defaults: defaults), .firstLaunch)
        for old in 2...4 {
            let key = "automatic_system_permissions_shown_v\(old)"
            defaults.set(true, forKey: key)
            XCTAssertEqual(MacPermissionSetup.launchMode(version: "0.6.22", defaults: defaults), .upgrade)
            defaults.removeObject(forKey: key)
        }
        defaults.set("0.6.22", forKey: MacPermissionSetup.checkedVersionKey)
        XCTAssertNil(MacPermissionSetup.launchMode(version: "0.6.22", defaults: defaults))
        XCTAssertEqual(MacPermissionSetup.launchMode(version: "0.6.23", defaults: defaults), .upgrade)
    }

    @MainActor func testUpgradeReviewsActualMissingPermissionsAndRechecksBeforePrompting() async {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        var states: [Step: State] = [.location: .request, .speech: .request, .microphone: .request,
            .notifications: .allowed, .bluetooth: .unused, .terminal: .allowed, .music: .settings,
            .spotify: .unavailable, .systemEvents: .allowed, .localNetwork: .settings, .accessibility: .settings]
        let steps = MacPermissionSetup.pendingSteps(states, mode: .upgrade)
        XCTAssertEqual(steps, [.location, .speech, .microphone, .music, .accessibility])
        // Permission may change while the previous system sheet is displayed.
        states[.microphone] = .allowed
        var requested: [Step] = []
        await MacPermissionSetup.runInitial(steps: steps, mode: .upgrade, state: { states[$0] ?? .unused },
            request: { step, _ in requested.append(step) })
        XCTAssertEqual(requested, [.location, .speech, .accessibility])
        XCTAssertTrue(MacPermissionSetup.pendingSteps([.notifications: .allowed, .localNetwork: .settings,
            .terminal: .openApp], mode: .upgrade).isEmpty)
    }

    @MainActor func testUnverifiedSystemEventsDoesNotRequestAuthorization() async {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        XCTAssertTrue(MacPermissionSetup.pendingSteps([.systemEvents: .allowed], mode: .upgrade).isEmpty)
        XCTAssertTrue(MacPermissionSetup.pendingSteps([.systemEvents: .openApp, .terminal: .openApp], mode: .upgrade).isEmpty)
        XCTAssertEqual(MacPermissionSetup.pendingSteps([.systemEvents: .request], mode: .upgrade), [.systemEvents])
        for state in [State.openApp, .request, .allowed, .lastCheckedAllowed, .unavailable] {
            var requested: [State] = []
            await MacPermissionSetup.runInitial(steps: [Step.systemEvents], mode: .upgrade, state: { _ in state },
                request: { _, current in requested.append(current) })
            XCTAssertEqual(requested, state == .request ? [state] : [])
        }
    }

    @MainActor func testSystemEventsStartupAndWindowShareOneBackgroundCheck() async {
        var starts = 0
        var live = MacPermissionSetup.State.allowed
        let review = MacSystemEventsPermissionReview { startsHelper in
            if startsHelper { starts += 1; await Task.yield() }
            return live
        }
        let startup = Task { await review.checkAtLaunch() }
        let opened = Task { await review.current() }
        let launchState = await startup.value
        let firstVisible = await opened.value
        XCTAssertEqual(launchState, .allowed)
        XCTAssertEqual(firstVisible, .allowed)
        live = .openApp
        for _ in 0..<3 {
            let observed = await review.current()
            XCTAssertEqual(observed, .lastCheckedAllowed)
        }
        live = .request
        let revoked = await review.current()
        XCTAssertEqual(revoked, .request)
        live = .openApp
        let unchecked = await review.current()
        XCTAssertEqual(unchecked, .openApp)
        XCTAssertEqual(starts, 1)
    }

    @MainActor func testSystemEventsAndAccessibilityAreRequestedIndependently() async {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        let cases: [([Step: State], [Step])] = [
            ([.systemEvents: .request, .accessibility: .settings], [.systemEvents, .accessibility]),
            ([.systemEvents: .request, .accessibility: .allowed], [.systemEvents]),
            ([.systemEvents: .allowed, .accessibility: .settings], [.accessibility]),
        ]
        for mode in [MacPermissionSetup.LaunchMode.firstLaunch, .upgrade] {
            for (states, expected) in cases {
                var requested: [Step] = []
                await MacPermissionSetup.runInitial(steps: MacPermissionSetup.pendingSteps(states, mode: mode),
                    mode: mode, state: { states[$0] ?? .unused }, request: { step, _ in requested.append(step) })
                XCTAssertEqual(requested, expected)
            }
        }
    }

    @MainActor func testNotificationRequestRetriesOnlyWhileSystemHasNoDecision() async {
        for decision in [MacPermissionSetup.State.allowed, .settings] {
            var state = MacPermissionSetup.State.request
            var attempts = 0
            await MacPermissionSetup.requestNotifications(state: { state }, request: {
                attempts += 1
                if attempts == 2 { state = decision }
            }, settle: {})
            XCTAssertEqual(attempts, 2)
            await MacPermissionSetup.requestNotifications(state: { state }, request: {
                attempts += 1
            }, settle: {})
            XCTAssertEqual(attempts, 2)
        }
    }

    @MainActor func testUndecidedWeatherLocationDoesNotBlockLaterPermissions() async {
        typealias Step = MacPermissionSetup.Step
        let states: [Step: MacPermissionSetup.State] = [.location: .request, .systemEvents: .request, .accessibility: .settings]
        var requested: [Step] = []
        await MacPermissionSetup.runInitial(steps: [.location, .systemEvents, .accessibility], mode: .upgrade,
            state: { states[$0] ?? .unused }, request: { step, _ in
                if step == .location {
                    await MacPermissionSetup.waitForLocationDecision(status: { .notDetermined }, settle: {})
                }
                requested.append(step)
            })
        XCTAssertEqual(requested, [.location, .systemEvents, .accessibility])
    }

    @MainActor func testCancellingGuideStopsAfterCurrentPrompt() async {
        var requested: [MacPermissionSetup.Step] = []
        let task = Task {
            await MacPermissionSetup.runInitial(state: { _ in .request }, request: { step, _ in
                requested.append(step)
                withUnsafeCurrentTask { $0?.cancel() }
            })
        }
        await task.value
        XCTAssertEqual(requested, [.location])
    }

    func testNotificationDefaultsAndSavedOptOut() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let service = MacNotifications(directory: directory)
        XCTAssertTrue(service.usesSystemNotifications)
        try service.file.write(.init(attention: false, quota: false, health: false))
        XCTAssertFalse(service.usesSystemNotifications)
    }

    private func preferences() -> (UserDefaults, String) {
        let name = "AI-bot-preferences-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        // NSRegistrationDomain is shared even by distinct named suites.
        let registration = defaults.volatileDomain(forName: UserDefaults.registrationDomain)
        defaults.setVolatileDomain([:], forName: UserDefaults.registrationDomain)
        addTeardownBlock { defaults.setVolatileDomain(registration, forName: UserDefaults.registrationDomain) }
        return (defaults, name)
    }
    func testFreshDefaultsEnableAvailableActions() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        MacFeaturePreferences.register(defaults)
        for key in [MacMusicService.enabledKey, MacMusicService.controlKey, MacTab5Codex.enabledKey, MacTab5Codex.sendKey, MacTab5Desktop.enabledKey, MacTab5Computer.enabledKey, MacTab5BLE.enabledKey, MacUpdateWindow.automaticKey, "weather_automatic_location"] {
            XCTAssertTrue(defaults.bool(forKey: key), key)
        }
        var calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in calls += 1; return true }))
        _ = control.handle(["op": "media", "action": "play-pause", "requestId": UUID().uuidString])
        XCTAssertEqual(calls, 1)
    }
    func testExplicitOptOutSurvivesReregistration() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        MacFeaturePreferences.register(defaults)
        for key in [MacMusicService.enabledKey, MacMusicService.controlKey, MacTab5Codex.enabledKey, MacTab5Codex.sendKey, MacTab5Desktop.enabledKey, MacTab5Computer.enabledKey, MacTab5BLE.enabledKey, MacUpdateWindow.automaticKey, "weather_automatic_location"] { defaults.set(false, forKey: key) }
        let reopened = UserDefaults(suiteName: name)!
        MacFeaturePreferences.register(reopened)
        for key in [MacMusicService.enabledKey, MacMusicService.controlKey, MacTab5Codex.enabledKey, MacTab5Codex.sendKey, MacTab5Desktop.enabledKey, MacTab5Computer.enabledKey, MacTab5BLE.enabledKey, MacUpdateWindow.automaticKey, "weather_automatic_location"] { XCTAssertFalse(reopened.bool(forKey: key), key) }
    }
    func testExistingMusicPermissionMigratesOnceAndControlOptOutPersists() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        defaults.set(true, forKey: MacMusicService.enabledKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertTrue(MacFeaturePreferences.musicControlEnabled(defaults))
        defaults.set(false, forKey: MacMusicService.controlKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertFalse(MacFeaturePreferences.musicControlEnabled(defaults))
        defaults.set(true, forKey: MacMusicService.controlKey)
        defaults.set(false, forKey: MacMusicService.enabledKey)
        XCTAssertFalse(MacFeaturePreferences.musicControlEnabled(defaults))
    }
    func testManualWeatherAndExistingControlChoicesArePreserved() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        defaults.set("Test city", forKey: "weather_city")
        defaults.set(true, forKey: MacTab5Computer.enabledKey)
        defaults.set(true, forKey: MacTab5Voice.enabledKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertFalse(defaults.bool(forKey: "weather_automatic_location"))
        XCTAssertTrue(defaults.bool(forKey: MacTab5Computer.enabledKey))
        XCTAssertTrue(defaults.bool(forKey: MacTab5Voice.enabledKey))
    }
}

final class MacPrivateKeyCacheTests: XCTestCase {
    func testConcurrentReceiptsLoadKeyOnlyOnce() {
        var reads = 0
        let secret = Data(repeating: 7, count: 32)
        let cache = MacPrivateKeyCache { reads += 1; return secret }
        DispatchQueue.concurrentPerform(iterations: 32) { _ in
            do { let value = try cache.read(); XCTAssertEqual(value, secret) }
            catch { XCTFail("Cached key read failed: \(error)") }
        }
        XCTAssertEqual(reads, 1)
    }
    func testDeniedKeyIsNotRetriedByEveryReceipt() {
        enum Failure: Error { case denied }
        var reads = 0
        let cache = MacPrivateKeyCache { reads += 1; throw Failure.denied }
        for _ in 0..<32 { XCTAssertThrowsError(try cache.read()) }
        XCTAssertEqual(reads, 1)
    }
}
