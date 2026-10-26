import Foundation

enum MacFeaturePreferences {
    // Registration supplies first-run defaults without overriding an explicit
    // opt-out. Run only in the normal app, never in offline UI/test fixtures.
    static func register(_ defaults: UserDefaults = .standard) {
        // Preserve the old combined music permission on upgrade. New installs
        // collect playback but cannot control it until separately enabled.
        if defaults.object(forKey: MacMusicService.controlKey) == nil {
            defaults.set(defaults.bool(forKey: MacMusicService.enabledKey), forKey: MacMusicService.controlKey)
        }
        let manualWeather = !(defaults.string(forKey: "weather_city") ?? "").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            || (defaults.object(forKey: "weather_latitude") != nil
                && defaults.object(forKey: "weather_longitude") != nil)
        defaults.register(defaults: [
            MacMusicService.enabledKey: true,
            MacTab5Codex.enabledKey: true,
            MacTab5BLE.enabledKey: true,
            "weather_automatic_location": !manualWeather
        ])
    }

    static func musicControlEnabled(_ defaults: UserDefaults = .standard) -> Bool {
        defaults.bool(forKey: MacMusicService.enabledKey)
            && (defaults.object(forKey: MacMusicService.controlKey) == nil
                ? defaults.bool(forKey: MacMusicService.enabledKey)
                : defaults.bool(forKey: MacMusicService.controlKey))
    }
}
