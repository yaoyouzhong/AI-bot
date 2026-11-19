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

    private func app(_ url: URL, version: String) throws {
        let contents = url.appendingPathComponent("Contents")
        let binaries = contents.appendingPathComponent("MacOS")
        try FileManager.default.createDirectory(at: binaries, withIntermediateDirectories: true)
        try FileManager.default.copyItem(at: URL(fileURLWithPath: "/usr/bin/true"), to: binaries.appendingPathComponent("AIBotBridge"))
        let info = ["CFBundlePackageType": "APPL", "CFBundleIdentifier": "io.github.yaoyouzhong.aibot.bridge",
                    "CFBundleExecutable": "AIBotBridge", "CFBundleShortVersionString": version]
        try PropertyListSerialization.data(fromPropertyList: info, format: .xml, options: 0)
            .write(to: contents.appendingPathComponent("Info.plist"))
        _ = try MacTab5LocalProcess.run("/usr/bin/codesign", ["--force", "--sign", "-", url.path])
    }
}
