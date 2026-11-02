import Foundation

enum MacDirectAuthorization {
    static let requestedKey = "tab5_codex_accessibility_requested"
    // Called only by the normal application, after first-run defaults are registered.
    static func shouldRequest(defaults: UserDefaults, trusted: Bool) -> Bool {
        guard defaults.bool(forKey: MacTab5Codex.enabledKey),
              defaults.bool(forKey: MacTab5Desktop.enabledKey), !trusted,
              !defaults.bool(forKey: requestedKey) else { return false }
        defaults.set(true, forKey: requestedKey)
        return true
    }
}
