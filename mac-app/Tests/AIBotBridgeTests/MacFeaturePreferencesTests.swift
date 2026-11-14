import XCTest
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
        XCTAssertEqual(requested, [.location, .notifications, .terminal, .music, .systemEvents, .localNetwork, .accessibility])
    }

    @MainActor func testPreviousGuideRequestsOnlyNewlyIncludedPermissions() async {
        let (defaults, name) = preferences()
        defer { defaults.removePersistentDomain(forName: name) }
        XCTAssertEqual(MacPermissionSetup.startupSteps(defaults: defaults), MacPermissionSetup.Step.allCases)
        defaults.set(true, forKey: "automatic_system_permissions_shown_v2")
        var requested: [MacPermissionSetup.Step] = []
        let steps = MacPermissionSetup.startupSteps(defaults: defaults)
        await MacPermissionSetup.runInitial(steps: steps, state: { _ in .request }, request: { step, _ in
            requested.append(step)
        })
        XCTAssertEqual(requested, [.speech, .microphone, .bluetooth, .terminal, .music, .spotify, .localNetwork])
        defaults.set(true, forKey: "automatic_system_permissions_shown_v3")
        XCTAssertEqual(MacPermissionSetup.startupSteps(defaults: defaults), [.terminal])
        XCTAssertTrue(MacPermissionSetup.shouldOffer([.request], defaults: defaults))
        defaults.set(true, forKey: MacPermissionSetup.shownKey)
        XCTAssertFalse(MacPermissionSetup.shouldOffer([.request], defaults: defaults))
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
