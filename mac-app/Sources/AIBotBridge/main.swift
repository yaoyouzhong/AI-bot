import AppKit
import MacKeychainSupport
if CommandLine.arguments.dropFirst().first == "--claude-terminal" {
    do { exit(try MacClaudeTerminalRelay.run(Array(CommandLine.arguments.dropFirst(2)))) }
    catch {
        fputs("Claude terminal could not start. Run in a terminal tab with optional --resume <session UUID>.\n", stderr)
        exit(1)
    }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--self-test-claude-events-server", let port = UInt16(CommandLine.arguments[2]), port > 0 {
    do { try MacClaudeEventValidation.serve(port:port); exit(0) } catch { exit(1) }
}
if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--claude-hook", let port = UInt16(CommandLine.arguments[2]), port > 0 {
    MacClaudeHook.run(port:port); exit(0)
}

if Array(CommandLine.arguments.dropFirst()) == ["--inspect-claude-native-clients"] {
    // Report aggregate identity and capability only. No drafts, paths, IDs,
    // permissions prompts, subprocess launches of Claude, or device writes.
    let registry = MacClaudeHarnesses.standard
    var counts: [String: Int] = [:], writable: [String: Int] = [:]
    var liveIDs = Set<UUID>()
    for id in registry.liveSessions() {
        switch registry.owner(id) {
        case .verified(let client):
            liveIDs.insert(id)
            counts[client.source.id, default: 0] += 1
            if client.allows("send", images: false) { writable[client.source.id, default: 0] += 1 }
        case .conflict: counts["multiple", default: 0] += 1
        case .invalid, .unavailable: counts["unverified", default: 0] += 1
        }
    }
    let isolated = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
    func catalogSummary() throws -> [String: Any] {
        defer { try? FileManager.default.removeItem(at: isolated) }
        let key = Data((0..<32).map { _ in UInt8.random(in: .min ... .max) })
        let store = MacTab5PrivateStore(root: isolated, key: { key })
        let backend = MacClaudeWorkspaceBackend(store: store, key: { key }, harnesses: registry)
        var params: [String: Any] = [:], rows: [[String: Any]] = []
        for _ in 0..<8 {
            let page = try backend.call("thread/list", params)
            rows += page["data"] as? [[String: Any]] ?? []
            guard let cursor = page["nextCursor"] as? String else { break }
            params["cursor"] = cursor
        }
        let active = Set(liveIDs.compactMap { backend.alias(session: $0) })
        var liveSources: [String: Int] = [:], offlineSources: [String: Int] = [:]
        for row in rows {
            let source = (row["source"] as? [String: String])?["id"] ?? "unknown"
            if active.contains(row["id"] as? String ?? "") { liveSources[source, default: 0] += 1 }
            else { offlineSources[source, default: 0] += 1 }
        }
        return ["live": liveSources, "other": offlineSources]
    }
    let result: [String: Any] = ["verifiedBySource": counts.filter { $0.key != "unverified" }, "canSendBySource": writable,
                                 "unverified": ["count": counts["unverified", default: 0]],
                                 "catalog": (try? catalogSummary()) as Any? ?? NSNull()]
    if let bytes = try? MacTab5Protocol.json(result) {
        FileHandle.standardOutput.write(bytes + Data([10])); exit(0)
    }
    exit(1)
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--check-codex-delivery-baseline" {
    do { try MacCodexBaselineCheck.run(URL(fileURLWithPath: CommandLine.arguments[2])); exit(0) }
    catch { fputs("Codex read-only delivery baseline incomplete; no submission attempted.\n", stderr); exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--check-voice-metadata" {
    do { try MacVoiceMetadataCheck.run(URL(fileURLWithPath: CommandLine.arguments[2])); exit(0) }
    catch { fputs("Voice metadata check incomplete; no settings changed.\n", stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-domestic-capture"] {
    do { let count = try MacDomesticCaptureValidation.run(); print("MAC_DOMESTIC_CAPTURE_OK \(count) checks; offscreen synthetic WebKit only"); exit(0) }
    catch { fputs("Domestic capture check failed: \(error)\n", stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-domestic-contract"] {
    do { let count = try MacDomesticContractValidation.run(); print("MAC_DOMESTIC_CONTRACT_OK \(count) checks; synthetic payloads and isolated cache only"); exit(0) }
    catch { fputs("Domestic contract check failed: \(error)\n", stderr); exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--render-task-recovery-ui" {
    do { try MacTab5DeliveryRecoveryAlert.render(URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)); exit(0) }
    catch { fputs("Recovery UI render failed.\n", stderr); exit(1) }
}
if Array(CommandLine.arguments.dropFirst()) == ["--self-test-task-append"] {
    do { try MacTab5TaskAppendValidation.run(); exit(0) }
    catch { fputs("Task append validation failed: \(error)\n", stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-ota-transfer"] {
    do { let count = try MacTab5OTATransferValidation.loopback();print("MAC_OTA_TRANSFER_OK \(count) checks; synthetic image and credentials, loopback only");exit(0) }
    catch { fputs("OTA transfer self-test failed: \(error)\n",stderr);exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--render-esp-weather-header" {
    do { try MacLocalizedTextValidation.renderWeatherHeaderPreview(URL(fileURLWithPath:CommandLine.arguments[2])); print("ESP_WEATHER_HEADER_PREVIEW_OK"); exit(0) }
    catch { fputs("Weather header preview failed.\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--observe-esp-input-changes"] {
    do { let value = try MacESPDisplayCheck.observe(); let bytes = try JSONSerialization.data(withJSONObject:value,options:[.sortedKeys]); print(String(decoding:bytes,as:UTF8.self)); exit(0) }
    catch { fputs("ESP input observation unavailable; no settings changed.\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--check-esp-display"] {
    do { let value = try MacESPDisplayCheck.run(); let bytes = try JSONSerialization.data(withJSONObject:value,options:[.prettyPrinted,.sortedKeys]); print(String(decoding:bytes,as:UTF8.self)); exit(0) }
    catch { fputs("ESP display read-only check unavailable; no settings changed.\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-esp-discovery"] {
    do { let count = try MacESPDiscoveryValidation.loopback(); print("MAC_ESP_DISCOVERY_LOOPBACK_OK \(count) checks; synthetic token, ephemeral loopback only, no device/settings"); exit(0) }
    catch { fputs("\(error)\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-device-preview-lifecycle"] {
    do { try MacPreviewLifecycleValidation.run(); exit(0) }
    catch { fputs("\(error)\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-http-recovery"] {
    do { let count=try MacHTTPListenerRecoveryValidation.loopback(); print("MAC_HTTP_LISTENER_RECOVERY_OK \(count) checks; loopback fixtures only, no credentials/device/settings"); exit(0) }
    catch { fputs("\(error.localizedDescription)\n",stderr);exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-music-sampling"] {
    Task {
        do { let count = try await MacMusicService.validateSampling(); print("MAC_MUSIC_SAMPLING_OK \(count) checks; isolated storage, no player/network"); exit(0) }
        catch { fputs("\(error.localizedDescription)\n",stderr); exit(1) }
    }
    RunLoop.main.run()
}

if Array(CommandLine.arguments.dropFirst()) == ["--check-mac-screen-sharing"] {
    do { try MacARDConnection.probe(); exit(0) }
    catch { fputs("\(error.localizedDescription)\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-parity"] {
    do { try MacParityValidation.run(); exit(0) }
    catch { fputs("\(error.localizedDescription)\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-keychain-helper"] {
    do { try MacKeychainAccess.probe(); print("MAC_KEYCHAIN_HELPER_OK identity and private pipes; no credential accessed"); exit(0) }
    catch { fputs("Keychain helper identity check failed\n", stderr); exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--self-test-tab5-ui" {
    do { try MacTab5UIValidation.run(URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)); exit(0) }
    catch { fputs("Native UI fixture render failed: \(error.localizedDescription)\n", stderr); exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--self-test-device-preview-ui" {
    do { try MacTab5ScreenPreviewValidation.render(URL(fileURLWithPath:CommandLine.arguments[2],isDirectory:true)); exit(0) }
    catch { fputs("Device preview fixture failed: \(error.localizedDescription)\n",stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--self-test-flash-tool"] {
    do { try MacFirmwareFlasher.checkBundledTool(); exit(0) }
    catch { fputs("\(error.localizedDescription)\n", stderr); exit(1) }
}

let instance: MacApplicationInstance
do { instance = try MacApplicationInstance() }
catch MacApplicationInstance.Failure.alreadyRunning { print("BRIDGE_ALREADY_RUNNING"); exit(0) }
catch { fputs("Bridge instance lock unavailable; no bridge started.\n", stderr); exit(1) }

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--tab5-automation" {
    do { try MacTab5AutomationRunner.run(directory: URL(fileURLWithPath:CommandLine.arguments[2],isDirectory:true)); exit(0) }
    catch { fputs("TAB5 automation incomplete; no automatic action retry.\n",stderr); exit(1) }
}
if CommandLine.arguments.count == 5, CommandLine.arguments[1] == "--tab5-automation", CommandLine.arguments[3] == "--claude-fixture" {
    do { try MacTab5AutomationRunner.run(directory:URL(fileURLWithPath:CommandLine.arguments[2],isDirectory:true),fixture:URL(fileURLWithPath:CommandLine.arguments[4],isDirectory:true)); exit(0) }
    catch let error as MacTab5WorkspaceAcceptance.Failure { fputs("TAB5 synthetic workspace fixture rejected: \(error.rawValue)\n",stderr); exit(1) }
    catch let error as MacTab5Error { fputs("TAB5 synthetic workspace unavailable: \(error.localizedDescription)\n",stderr); exit(1) }
    catch { fputs("TAB5 synthetic workspace automation failed\n",stderr); exit(1) }
}

if CommandLine.arguments.count == 3, CommandLine.arguments[1] == "--capture-tab5-screen" {
    do { try MacTab5HeadlessCapture.run(output: URL(fileURLWithPath: CommandLine.arguments[2])); exit(0) }
    catch { fputs("TAB5 screen capture incomplete; no touch, settings or firmware change requested.\n", stderr); exit(1) }
}

if CommandLine.arguments.count == 2, ["--prepare-keychain-vault", "--prepare-keychain-vault-legacy-helper"].contains(CommandLine.arguments[1]) {
    do {
        if CommandLine.arguments[1] == "--prepare-keychain-vault-legacy-helper" { try MacKeychainAccess.prepareUsingLegacyHelper() }
        else { try MacKeychainAccess.prepare(); try MacKeychainAccess.verifyMigration() }
        print("MAC_KEYCHAIN_VAULT_VERIFIED legacy values unchanged; no credentials exported")
        exit(0)
    } catch BridgeKeychain.Failure.status(let status) {
        fputs("Keychain migration not confirmed: status=\(status); existing records retained.\n", stderr)
        exit(1)
    } catch {
        fputs("Keychain migration not confirmed; existing records retained.\n", stderr)
        exit(1)
    }
}

if CommandLine.arguments.count == 4, CommandLine.arguments[1] == "--test-tab5-ble-link" {
    do {
        try MacTab5BLELinkCheck.run(mode: CommandLine.arguments[2], reportURL: URL(fileURLWithPath: CommandLine.arguments[3]))
        print("MAC_TAB5_BLE_LINK_CHECK_OK"); exit(0)
    } catch {
        if let failure = error as? MacTab5Error { fputs("BLE link check: \(failure)\n", stderr) }
        fputs("BLE link acceptance incomplete; no firmware installation requested.\n", stderr); exit(1)
    }
}

if CommandLine.arguments.count == 4, CommandLine.arguments[1] == "--test-tab5-usb-link" {
    do {
        try MacTab5USBLinkCheck.run(mode:CommandLine.arguments[2], reportURL:URL(fileURLWithPath:CommandLine.arguments[3]))
        print("MAC_TAB5_USB_LINK_CHECK_OK"); exit(0)
    } catch {
        fputs("USB link acceptance incomplete; no firmware installation requested.\n",stderr); exit(1)
    }
}

if CommandLine.arguments.count == 5, CommandLine.arguments[1] == "--test-tab5-upgrade" {
    do {
        try MacTab5UpgradeCheck.run(mode: CommandLine.arguments[2], imageURL: URL(fileURLWithPath: CommandLine.arguments[3]), reportURL: URL(fileURLWithPath: CommandLine.arguments[4]))
        print("MAC_TAB5_UPGRADE_CHECK_OK"); exit(0)
    } catch { fputs("TAB5 acceptance check incomplete; inspect device state before retrying.\n", stderr); exit(1) }
}

if Array(CommandLine.arguments.dropFirst()) == ["--restore-cycle-default"] {
    MacDisplayPolicy.restoreCycleDefault()
    UserDefaults.standard.synchronize()
    print("ESP_CYCLE_DEFAULT_RESTORED auto cycle=true; page order and interval preserved; no TAB5 changes")
    exit(0)
}

MacFeaturePreferences.register()
let application = NSApplication.shared
MacScreenLockObservation.shared.start()
let delegate = AppDelegate()
application.delegate = delegate
application.setActivationPolicy(.accessory)
withExtendedLifetime(instance) { application.run() }
