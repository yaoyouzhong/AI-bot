import Foundation
import Security

enum MacDirectAuthorization {
    static let requestedKey = "tab5_codex_accessibility_requested"
    static let requestedBuildKey = "tab5_codex_accessibility_requested_build"
    static func buildIdentity() -> String {
        let bundle = Bundle.main
        var code: SecStaticCode?, info: CFDictionary?
        if SecStaticCodeCreateWithPath(bundle.bundleURL as CFURL, [], &code) == errSecSuccess,
           let code,
           SecCodeCopySigningInformation(code, SecCSFlags(rawValue: kSecCSSigningInformation), &info) == errSecSuccess,
           let hash = (info as? [String: Any])?[kSecCodeInfoUnique as String] as? Data, !hash.isEmpty {
            return hash.map { String(format: "%02x", $0) }.joined()
        }
        let source = (try? String(contentsOf: bundle.bundleURL.appendingPathComponent("Contents/Resources/SOURCE_SHA.txt"), encoding: .utf8)) ?? "development"
        return (bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "development") + ":" + source.trimmingCharacters(in: .whitespacesAndNewlines)
    }
    static func required(defaults: UserDefaults, trusted: Bool) -> Bool {
        defaults.bool(forKey: MacTab5Codex.enabledKey)
            && defaults.bool(forKey: MacTab5Desktop.enabledKey) && !trusted
    }
    // Consume the marker only when the app-owned guide will actually be shown.
    static func shouldRequest(defaults: UserDefaults, trusted: Bool, identity: String, interactive: Bool = true) -> Bool {
        guard interactive, required(defaults: defaults, trusted: trusted),
              defaults.string(forKey: requestedBuildKey) != identity else { return false }
        defaults.set(true, forKey: requestedKey)
        defaults.set(identity, forKey: requestedBuildKey)
        return true
    }
}
