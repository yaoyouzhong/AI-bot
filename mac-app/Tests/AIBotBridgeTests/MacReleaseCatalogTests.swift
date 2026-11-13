import Foundation
import XCTest
@testable import AIBotBridge

final class MacReleaseCatalogTests: XCTestCase {
    func testProductPageAndIndependentBridgeTag() throws {
        func release(_ tag: String, _ names: [String]) -> [String: Any] {
            ["tag_name": tag, "draft": false, "prerelease": false, "assets": names.map {
                ["name": $0, "size": 100, "browser_download_url": "https://github.com/yaoyouzhong/AI-bot/releases/download/\(tag)/\($0)"] as [String: Any]
            }]
        }
        let rows = [release("v0.6.21", []),
                    release("bridge-v0.6.20", ["AI-bot-bridge-v0.6.20-macos-arm64.zip", "SHA256SUMS.txt"]),
                    release("tab5-v0.2.321-ui", ["AI-bot-tab5-0.2.321-ui-upgrade.zip", "SHA256SUMS.txt"])]
        let (candidates, _) = try MacReleaseCatalog.parse(JSONSerialization.data(withJSONObject: rows))
        XCTAssertEqual(candidates.first { $0.component == "bridge" }?.version, "0.6.20")
        XCTAssertEqual(candidates.first { $0.component == "tab5" }?.version, "0.2.321-ui")
        XCTAssertEqual(candidates.count, 2)
    }

    func testCompactESPSelectionAndLegacyFallback() throws {
        let flash = "AI-bot-esp8266-0.5.8-flash.zip"
        let full = "AI-bot-esp8266-0.5.8-firmware.zip"
        let newer = "AI-bot-esp8266-0.5.9-firmware.zip"
        func release(_ version: String, _ names: [String]) -> [String: Any] {
            let tag = "esp8266-v" + version
            return ["tag_name": tag, "draft": false, "prerelease": false, "assets": names.map {
                ["name": $0, "size": 100, "browser_download_url": "https://github.com/yaoyouzhong/AI-bot/releases/download/\(tag)/\($0)"] as [String: Any]
            }]
        }
        func select(_ rows: [[String: Any]]) throws -> MacComponentRelease {
            let (candidates, _) = try MacReleaseCatalog.parse(JSONSerialization.data(withJSONObject: rows))
            let selected = candidates.reduce(nil as MacComponentRelease?) { current, candidate in
                if let current, !MacReleaseCatalog.prefers(candidate, over: current) { return current }
                return candidate
            }
            return try XCTUnwrap(selected)
        }
        let compact = release("0.5.8", [flash, flash + ".sha256"])
        let materials = release("0.5.8", [full, "SHA256SUMS.txt"])
        XCTAssertEqual(try select([materials, compact]).asset.name, flash)
        XCTAssertEqual(try select([compact, materials]).checksum?.name, flash + ".sha256")
        XCTAssertEqual(try select([release("0.5.8", [flash, full, "SHA256SUMS.txt"])]).asset.name, full)
        XCTAssertEqual(try select([compact, release("0.5.9", [newer, "SHA256SUMS.txt"])]).asset.name, newer)
    }
}
