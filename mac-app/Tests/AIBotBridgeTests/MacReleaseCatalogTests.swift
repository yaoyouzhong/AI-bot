import Foundation
import XCTest
@testable import AIBotBridge

final class MacReleaseCatalogTests: XCTestCase {
    func testUpdateCheckCadenceSharesTheLatestAttemptAcrossTriggers() {
        let start = Date(timeIntervalSince1970: 1_000)
        var cadence = MacUpdateCheckCadence()
        XCTAssertTrue(cadence.isDue(.foreground, at: start))
        XCTAssertTrue(cadence.isDue(.background, at: start))

        cadence.record(at: start, succeeded: true)
        XCTAssertTrue(cadence.isDue(.manual, at: start))
        XCTAssertFalse(cadence.isDue(.foreground, at: start.addingTimeInterval(299)))
        let foregroundRefresh = start.addingTimeInterval(300)
        XCTAssertTrue(cadence.isDue(.foreground, at: foregroundRefresh))
        cadence.record(at: foregroundRefresh, succeeded: true)
        XCTAssertFalse(cadence.isDue(.background, at: foregroundRefresh.addingTimeInterval(3_599)))

        let failure = foregroundRefresh.addingTimeInterval(3_600)
        XCTAssertTrue(cadence.isDue(.background, at: failure))
        cadence.record(at: failure, succeeded: false)
        XCTAssertTrue(cadence.isDue(.manual, at: failure))
        XCTAssertFalse(cadence.isDue(.foreground, at: failure.addingTimeInterval(59)))
        XCTAssertTrue(cadence.isDue(.foreground, at: failure.addingTimeInterval(60)))
        XCTAssertFalse(cadence.isDue(.background, at: failure.addingTimeInterval(899)))
        let recovery = failure.addingTimeInterval(900)
        XCTAssertTrue(cadence.isDue(.background, at: recovery))
        cadence.record(at: recovery, succeeded: true)
        XCTAssertFalse(cadence.isDue(.foreground, at: recovery.addingTimeInterval(60)))
        XCTAssertFalse(cadence.isDue(.background, at: recovery.addingTimeInterval(900)))
    }

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
        func release(_ tag: String, _ names: [String]) -> [String: Any] {
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
        let compact = release("esp8266-v0.5.8", [flash, flash + ".sha256"])
        let materials = release("esp8266-v0.5.8", [full, "SHA256SUMS.txt"])
        XCTAssertEqual(try select([materials, compact]).asset.name, flash)
        XCTAssertEqual(try select([compact, materials]).checksum?.name, flash + ".sha256")
        XCTAssertEqual(try select([release("esp8266-v0.5.8", [flash, full, "SHA256SUMS.txt"])]).asset.name, full)
        XCTAssertEqual(try select([compact, release("esp8266-v0.5.9", [newer, "SHA256SUMS.txt"])]).asset.name, newer)
        XCTAssertEqual(try select([release("v0.6.26", [flash, full, flash + ".sha256", "SHA256SUMS.txt"])]).asset.name, flash)
        XCTAssertEqual(try select([release("v0.6.26", [flash, full, "SHA256SUMS.txt"])]).asset.name, full)
    }
}
