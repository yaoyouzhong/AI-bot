import AppKit
import Darwin
import Security

enum MacDesktopUpdate {
    private static let appID = "io.github.yaoyouzhong.aibot.bridge"

    enum SigningTransition: Equatable { case unchanged, adoptingCertificate }

    enum SigningError: LocalizedError {
        case unreadable, differentSigner

        var errorDescription: String? {
            switch self {
            case .unreadable: "无法确认当前应用或更新包的签名身份；没有安装更新。"
            case .differentSigner: "更新包的签名与已安装版本不一致；保留当前版本。"
            }
        }
    }

    private struct SigningIdentity {
        let code: SecStaticCode
        let requirement: SecRequirement
        let leaf: Data?

        init(_ url: URL) throws {
            var code: SecStaticCode?
            var info: CFDictionary?
            var requirement: SecRequirement?
            guard SecStaticCodeCreateWithPath(url as CFURL, [], &code) == errSecSuccess, let code,
                  SecCodeCopySigningInformation(code, SecCSFlags(rawValue: kSecCSSigningInformation), &info) == errSecSuccess,
                  let details = info as NSDictionary?, let flags = details[kSecCodeInfoFlags] as? NSNumber,
                  SecCodeCopyDesignatedRequirement(code, [], &requirement) == errSecSuccess, let requirement else {
                throw SigningError.unreadable
            }
            self.code = code
            self.requirement = requirement
            if flags.uint32Value & SecCodeSignatureFlags.adhoc.rawValue != 0 {
                leaf = nil
            } else if let certificate = (details[kSecCodeInfoCertificates] as? [SecCertificate])?.first {
                leaf = SecCertificateCopyData(certificate) as Data
            } else {
                throw SigningError.unreadable
            }
        }

        func satisfies(_ requirement: SecRequirement) -> Bool {
            SecStaticCodeCheckValidity(code, SecCSFlags(rawValue: kSecCSStrictValidate), requirement) == errSecSuccess
        }
    }

    // Legacy ad-hoc clients have no publisher identity to pin. The existing
    // release transport, SHA-256, bundle ID and strict signature validation
    // still apply to that one-time adoption. Once signed, never fall back.
    private static func signingTransition(from installed: URL, to candidate: URL) throws -> SigningTransition {
        let current = try SigningIdentity(installed), next = try SigningIdentity(candidate)
        guard let leaf = current.leaf else { return next.leaf == nil ? .unchanged : .adoptingCertificate }
        guard next.leaf == leaf, next.satisfies(current.requirement), current.satisfies(next.requirement) else {
            throw SigningError.differentSigner
        }
        return .unchanged
    }

    static func prepare(_ package: URL, replacing installed: URL, version: String) throws -> (app: URL, signing: SigningTransition) {
        let app = try unpack(package, version: version)
        do { return (app, try signingTransition(from: installed, to: app)) }
        catch {
            try? FileManager.default.removeItem(at: app.deletingLastPathComponent())
            throw error
        }
    }

    static func unpack(_ package: URL, version: String) throws -> URL {
        let folder = package.deletingLastPathComponent().resolvingSymlinksInPath().appendingPathComponent("app-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: false,
                                               attributes: [.posixPermissions: 0o700])
        do {
            // bsdtar protects the staging directory against absolute paths, parent traversal and symlink traversal.
            _ = try MacTab5LocalProcess.run("/usr/bin/tar", ["-xf", package.path, "-C", folder.path], timeout: 60)
            let app = folder.appendingPathComponent("AIBotBridge.app", isDirectory: true)
            try verify(app, version: version)
            return app
        } catch {
            try? FileManager.default.removeItem(at: folder)
            throw error
        }
    }

    private static func verify(_ app: URL, version: String) throws {
        guard app.resolvingSymlinksInPath().path == app.path, let bundle = Bundle(url: app),
              bundle.bundleIdentifier == appID,
              bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String == version,
              bundle.executableURL?.path == app.appendingPathComponent("Contents/MacOS/AIBotBridge").path else {
            throw MacTab5LocalError.invalid("安装包中的应用标识或版本不匹配。")
        }
        _ = try MacTab5LocalProcess.run("/usr/bin/codesign", ["--verify", "--deep", "--strict", app.path], timeout: 45)
    }

    // Replace on the same volume in one operation. The previous bundle stays in
    // a private staging directory until the old process exits and launch is accepted.
    static func install(_ app: URL, replacing installed: URL, version: String,
                        relaunch: (URL, URL, URL) throws -> Void = { try launchAfterExit($0, $1, $2) }) throws {
        let fm = FileManager.default
        let parent = installed.deletingLastPathComponent()
        guard installed.pathExtension == "app", installed.resolvingSymlinksInPath().path == installed.path,
              Bundle(url: installed)?.bundleIdentifier == appID,
              fm.isWritableFile(atPath: parent.path), fm.isWritableFile(atPath: installed.path) else {
            throw MacTab5LocalError.invalid("当前应用位置需要手动替换。")
        }
        let staging = parent.appendingPathComponent(".aibot-update-" + UUID().uuidString, isDirectory: true)
        try fm.createDirectory(at: staging, withIntermediateDirectories: false, attributes: [.posixPermissions: 0o700])
        let replacement = staging.appendingPathComponent("AIBotBridge.app", isDirectory: true)
        var keepStaging = false
        defer { if !keepStaging { try? fm.removeItem(at: staging) } }
        _ = try MacTab5LocalProcess.run("/usr/bin/ditto", [app.path, replacement.path], timeout: 60)
        try verify(replacement, version: version)
        _ = try signingTransition(from: installed, to: replacement)
        func swap() -> Bool {
            renameatx_np(AT_FDCWD, replacement.path, AT_FDCWD, installed.path, UInt32(RENAME_SWAP)) == 0
        }
        guard swap() else { throw MacTab5LocalError.invalid("系统未允许替换当前应用。") }
        do {
            try relaunch(installed, staging, app.deletingLastPathComponent())
            keepStaging = true
        } catch {
            keepStaging = !swap()
            throw error
        }
    }

    static func launchAfterExit(_ installed: URL, _ staging: URL, _ unpacked: URL, arguments: [String] = []) throws {
        let launcher = Process()
        launcher.executableURL = URL(fileURLWithPath: "/bin/sh")
        launcher.arguments = ["-c", """
            previous_pid="$1"; installed="$2"; staging="$3"; unpacked="$4"; shift 4
            for i in $(/usr/bin/jot 120); do
                if ! kill -0 "$previous_pid" 2>/dev/null; then
                    if /usr/bin/open -n "$installed" "$@"; then
                        /bin/rm -rf -- "$staging" "$unpacked"
                    elif /bin/mv -- "$installed" "$staging/Failed.app"; then
                        if /bin/mv -- "$staging/AIBotBridge.app" "$installed"; then /usr/bin/open -n "$installed" "$@"
                        else /bin/mv -- "$staging/Failed.app" "$installed"; fi
                    fi
                    exit
                fi
                /bin/sleep 0.25
            done
            """, "aibot-relaunch", String(ProcessInfo.processInfo.processIdentifier), installed.path, staging.path,
            unpacked.path]
        if !arguments.isEmpty { launcher.arguments?.append(contentsOf: ["--args"] + arguments) }
        try launcher.run()
    }
}
