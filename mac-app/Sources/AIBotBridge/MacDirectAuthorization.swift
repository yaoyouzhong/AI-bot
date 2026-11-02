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
    // Called only by the normal application, after first-run defaults are registered.
    static func shouldRequest(defaults: UserDefaults, trusted: Bool, identity: String) -> Bool {
        guard defaults.bool(forKey: MacTab5Codex.enabledKey),
              defaults.bool(forKey: MacTab5Desktop.enabledKey), !trusted,
              defaults.string(forKey: requestedBuildKey) != identity else { return false }
        defaults.set(true, forKey: requestedKey)
        defaults.set(identity, forKey: requestedBuildKey)
        return true
    }
}
