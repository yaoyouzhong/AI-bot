import AppKit

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
