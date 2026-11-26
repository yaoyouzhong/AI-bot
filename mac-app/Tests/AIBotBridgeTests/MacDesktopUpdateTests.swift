import XCTest
@testable import AIBotBridge

final class MacDesktopUpdateTests: XCTestCase {
    func testValidatesVersionAndRestoresOldBundleIfRelaunchCannotStart() throws {
        let files = FileManager.default
        let root = files.temporaryDirectory.resolvingSymlinksInPath().appendingPathComponent(UUID().uuidString)
        defer { try? files.removeItem(at: root) }
        let payload = root.appendingPathComponent("payload")
        let installed = root.appendingPathComponent("Installed.app")
        try app(installed, version: "0.6.25")
        try app(payload.appendingPathComponent("AIBotBridge.app"), version: "0.6.26")
        let zip = root.appendingPathComponent("update.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/ditto", ["-c", "-k", payload.path, zip.path])

        XCTAssertThrowsError(try MacDesktopUpdate.unpack(zip, version: "0.6.27"))
        let prepared = try MacDesktopUpdate.unpack(zip, version: "0.6.26")
        func version(_ url: URL) throws -> String? {
            let data = try Data(contentsOf: url.appendingPathComponent("Contents/Info.plist"))
            return (try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: String])?["CFBundleShortVersionString"]
        }
        XCTAssertThrowsError(try MacDesktopUpdate.install(prepared, replacing: installed, version: "0.6.26") { target, _, _ in
            XCTAssertEqual(try version(target), "0.6.26")
            throw CocoaError(.fileWriteNoPermission)
        })
        XCTAssertEqual(try version(installed), "0.6.25")
        try MacDesktopUpdate.install(prepared, replacing: installed, version: "0.6.26") { target, old, _ in
            XCTAssertEqual(try version(target), "0.6.26")
            XCTAssertEqual(try version(old.appendingPathComponent("AIBotBridge.app")), "0.6.25")
        }
        XCTAssertEqual(try version(installed), "0.6.26")
    }

    func testFixedSigningAcceptsFirstMigrationThenRejectsDifferentSignerAndDowngrade() throws {
        let env = ProcessInfo.processInfo.environment
        guard let signer = env["AIBOT_TEST_MAC_SIGNER"], let other = env["AIBOT_TEST_MAC_OTHER_SIGNER"],
              let keychain = env["AIBOT_MAC_SIGN_KEYCHAIN"] else {
            throw XCTSkip("Run with two isolated local signing identities to exercise certificate upgrades.")
        }
        let files = FileManager.default
        let root = files.temporaryDirectory.resolvingSymlinksInPath().appendingPathComponent(UUID().uuidString)
        defer { try? files.removeItem(at: root) }
        let installed = root.appendingPathComponent("Installed.app")
        try app(installed, version: "0.6.25")
        func candidate(_ version: String, _ identity: String) throws -> URL {
            let payload = root.appendingPathComponent("source-\(UUID().uuidString)")
            try app(payload.appendingPathComponent("AIBotBridge.app"), version: version, identity: identity, keychain: keychain)
            let zip = payload.appendingPathExtension("zip")
            _ = try MacTab5LocalProcess.run("/usr/bin/ditto", ["-c", "-k", payload.path, zip.path])
            return zip
        }
        let first = try MacDesktopUpdate.prepare(candidate("0.6.26", signer), replacing: installed, version: "0.6.26")
        XCTAssertEqual(first.signing, .adoptingCertificate)
        try MacDesktopUpdate.install(first.app, replacing: installed, version: "0.6.26") { _, _, _ in }
        let second = try MacDesktopUpdate.prepare(candidate("0.6.27", signer), replacing: installed, version: "0.6.27")
        XCTAssertEqual(second.signing, .unchanged)
        try MacDesktopUpdate.install(second.app, replacing: installed, version: "0.6.27") { _, _, _ in }
        let before = try Data(contentsOf: installed.appendingPathComponent("Contents/MacOS/AIBotBridge"))
        for blocked in ["-", other] {
            let archive = try candidate("0.6.28", blocked)
            let entries = Set(try files.contentsOfDirectory(atPath: root.path))
            XCTAssertThrowsError(try MacDesktopUpdate.prepare(archive, replacing: installed, version: "0.6.28")) {
                XCTAssertEqual($0 as? MacDesktopUpdate.SigningError, .differentSigner)
            }
            XCTAssertEqual(Set(try files.contentsOfDirectory(atPath: root.path)), entries)
            let unprepared = try MacDesktopUpdate.unpack(archive, version: "0.6.28")
            XCTAssertThrowsError(try MacDesktopUpdate.install(unprepared, replacing: installed, version: "0.6.28") { _, _, _ in
                XCTFail("An identity mismatch must not reach relaunch")
            }) { XCTAssertEqual($0 as? MacDesktopUpdate.SigningError, .differentSigner) }
            XCTAssertEqual(try Data(contentsOf: installed.appendingPathComponent("Contents/MacOS/AIBotBridge")), before)
        }
    }

    private func app(_ url: URL, version: String, identity: String = "-", keychain: String? = nil) throws {
        let contents = url.appendingPathComponent("Contents")
        let binaries = contents.appendingPathComponent("MacOS")
        try FileManager.default.createDirectory(at: binaries, withIntermediateDirectories: true)
        try FileManager.default.copyItem(at: URL(fileURLWithPath: "/usr/bin/true"), to: binaries.appendingPathComponent("AIBotBridge"))
        let info = ["CFBundlePackageType": "APPL", "CFBundleIdentifier": "io.github.yaoyouzhong.aibot.bridge",
                    "CFBundleExecutable": "AIBotBridge", "CFBundleShortVersionString": version]
        try PropertyListSerialization.data(fromPropertyList: info, format: .xml, options: 0)
            .write(to: contents.appendingPathComponent("Info.plist"))
        let keychainArgs = identity == "-" ? [] : keychain.map { ["--keychain", $0] } ?? []
        _ = try MacTab5LocalProcess.run("/usr/bin/codesign", ["--force", "--sign", identity, "--timestamp=none"] + keychainArgs + [url.path])
    }
}
