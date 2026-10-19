import XCTest
import CryptoKit
@testable import AIBotBridge

final class MacTab5InstallTests: XCTestCase {
    private func directory() throws -> URL { let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString); try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true); addTeardownBlock { try? FileManager.default.removeItem(at: root) }; return root }
    private func package() throws -> MacTab5InstallPackage {
        var partitions = Data(repeating: 255, count: 224)
        let rows: [(String, UInt8, UInt8, UInt32, UInt32)] = [("nvs",1,2,0x9000,0x6000),("phy_init",1,1,0xf000,0x1000),("otadata",1,0,0x10000,0x2000),("ota_0",0,0x10,0x20000,0x6e0000),("ota_1",0,0x11,0x700000,0x6e0000),("assets",1,0x40,0xe00000,0x200000)]
        for (index, row) in rows.enumerated() {
            let at = index * 32; partitions[at] = 0xaa; partitions[at + 1] = 0x50; partitions[at + 2] = row.1; partitions[at + 3] = row.2
            for byte in 0..<4 { partitions[at + 4 + byte] = UInt8(truncatingIfNeeded: row.3 >> (8 * byte)); partitions[at + 8 + byte] = UInt8(truncatingIfNeeded: row.4 >> (8 * byte)); partitions[at + 28 + byte] = 0 }
            partitions.replaceSubrange(at + 12..<at + 12 + row.0.utf8.count, with: row.0.utf8); partitions[at + 12 + row.0.utf8.count] = 0
        }
        partitions[192] = 0xeb; partitions[193] = 0xeb; partitions.replaceSubrange(208..<224, with: Data(Insecure.MD5.hash(data: partitions.prefix(192))))
        let parts = [MacTab5TestData.firmware(), partitions, Data(repeating: 255, count: 8192), MacTab5TestData.firmware()]
        let offsets = [0x2000,0x8000,0x10000,0x20000], names = ["bootloader.bin","partition-table.bin","otadata.bin","application.bin"]
        var image = Data(repeating: 255, count: MacTab5InstallPackage.flashSize), segments: [MacTab5InstallPackage.Segment] = []
        for index in 0..<4 { image.replaceSubrange(offsets[index]..<offsets[index] + parts[index].count, with: parts[index]); segments.append(.init(name: names[index], offset: offsets[index], size: parts[index].count, sha256: MacTab5Protocol.sha256(parts[index]))) }
        return try MacTab5InstallPackage(manifest: .init(format: 1, kind: "aibot-tab5-first-install", chip: "esp32p4", project: "aibot_tab5", version: "0.2.234-ui", flashBytes: image.count, imageSha256: MacTab5Protocol.sha256(image), segments: segments), image: image)
    }
    private final class Device {
        var calls: [[String]] = [], short = false, verifyFailure = false, security = false, swapped = false, macReads = 0
        var flash = Data(repeating: 255, count: MacTab5InstallPackage.flashSize)
        func run(_ arguments: [String]) throws -> String {
            calls.append(arguments)
            if arguments.contains("flash_id") { return "Chip is ESP32-P4 (revision v0.1)\nDetected flash size: 16MB\n" }
            if arguments.contains("get_security_info") { return "Secure Boot: \(security ? "Enabled" : "Disabled")\nFlash Encryption: Disabled\n" }
            if arguments.contains("read_mac") { macReads += 1; return "MAC: \(swapped && macReads > 1 ? "00:11:22:33:44:55" : "aa:bb:cc:dd:ee:ff")\n" }
            if arguments.contains("read_flash") { try (short ? Data(flash.prefix(1024)) : flash).write(to: URL(fileURLWithPath: arguments.last!)) }
            if arguments.contains("verify_flash"), verifyFailure { throw MacTab5Error.unconfirmed }
            return "ok"
        }
    }
    func testFirstInstallValidatesEveryRegionAndRejectsPrivateGapWithUpdatedHash() throws {
        let value = try package(); XCTAssertEqual(value.image.count, 16 * 1024 * 1024)
        var image = value.image; image[0x9000] = 0
        let m = value.manifest
        XCTAssertThrowsError(try MacTab5InstallPackage(manifest: .init(format: m.format, kind: m.kind, chip: m.chip, project: m.project, version: m.version, flashBytes: m.flashBytes, imageSha256: MacTab5Protocol.sha256(image), segments: m.segments), image: image))
        var partitions = Data(value.image[0x8000..<0x8000 + 224]); partitions[209] ^= 1; XCTAssertThrowsError(try MacTab5InstallPackage.partitions(partitions))
    }
    func testFirstInstallZIPCanAlsoSelectExactApplicationForBootVerification() throws {
        let root = try directory(), value = try package(), image = root.appendingPathComponent("factory.bin"), manifest = root.appendingPathComponent("manifest.json"), zip = root.appendingPathComponent("fixture.zip")
        try value.image.write(to: image); try JSONEncoder().encode(value.manifest).write(to: manifest)
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-q", "-j", zip.path, image.path, manifest.path], timeout: 60)
        let selected = try MacTab5FirmwareImage.load(zip)
        XCTAssertEqual(selected.version, value.manifest.version); XCTAssertEqual(selected.sha256, value.manifest.segments[3].sha256)
    }
    func testBackupAndVerifyPrecedeWriteAndNoWriteRetriesOrReset() throws {
        let root = try directory(), device = Device(), installer = MacTab5Installer(run: device.run)
        let backup = try installer.execute(port: "/dev/cu.usbmodem-test", package: package(), directory: root, work: root.appendingPathComponent("work"))
        let record = try MacTab5FlashBackup.read(backup.appendingPathExtension("json")); XCTAssertEqual(record.0.mac, "aa:bb:cc:dd:ee:ff")
        let write = try XCTUnwrap(device.calls.firstIndex { $0.contains("write_flash") })
        XCTAssertTrue(device.calls[..<write].contains { $0.contains("read_flash") }); XCTAssertTrue(device.calls[..<write].contains { $0.contains("verify_flash") })
        XCTAssertEqual(device.calls.filter { $0.contains("write_flash") }.count, 1)
        XCTAssertFalse(device.calls.joined().contains("erase_flash")); XCTAssertFalse(device.calls.joined().contains("--force")); XCTAssertTrue(device.calls.allSatisfy { $0.contains("no_reset") })
    }
    func testUnverifiedOrIncompleteBackupNeverWrites() throws {
        let root = try directory(), value = try package()
        for short in [true, false] {
            let device = Device(); device.short = short; device.verifyFailure = !short
            XCTAssertThrowsError(try MacTab5Installer(run: device.run).execute(port: "/dev/cu.test", package: value, directory: root, work: root.appendingPathComponent("work")))
            XCTAssertFalse(device.calls.contains { $0.contains("write_flash") })
        }
    }
    func testSecurityAndDeviceSwapBlockMutation() throws {
        let root = try directory(), value = try package()
        let secure = Device(); secure.security = true
        XCTAssertThrowsError(try MacTab5Installer(run: secure.run).execute(port: "/dev/cu.test", package: value, directory: root, work: root)); XCTAssertFalse(secure.calls.contains { $0.contains("read_flash") })
        let swapped = Device(); swapped.swapped = true
        XCTAssertThrowsError(try MacTab5Installer(run: swapped.run).execute(port: "/dev/cu.test", package: value, directory: root, work: root)); XCTAssertFalse(swapped.calls.contains { $0.contains("write_flash") })
    }
    func testExistingInstallationIsBackedUpAndDirectedToUpgrade() throws {
        let root = try directory(), device = Device(); device.flash.replaceSubrange(0x20000 + 80..<0x20000 + 91, with: "aibot_tab5\0".utf8)
        let installer = MacTab5Installer(run: device.run)
        XCTAssertThrowsError(try installer.execute(port: "/dev/cu.test", package: package(), directory: root, work: root))
        XCTAssertNotNil(installer.backup); XCTAssertFalse(device.calls.contains { $0.contains("write_flash") })
    }
    func testRestoreRequiresMatchingHardwareAndKeepsCurrentBackup() throws {
        let root = try directory(), first = Device(), installer = MacTab5Installer(run: first.run)
        let original = try installer.execute(port: "/dev/cu.test", directory: root, work: root)
        let second = Device(), restorer = MacTab5Installer(run: second.run)
        let current = try restorer.execute(port: "/dev/cu.test", restoring: original.appendingPathExtension("json"), directory: root, work: root.appendingPathComponent("restore"))
        XCTAssertNotEqual(original, current); XCTAssertTrue(FileManager.default.fileExists(atPath: original.path)); XCTAssertTrue(FileManager.default.fileExists(atPath: current.path))
        XCTAssertEqual(second.calls.filter { $0.contains("write_flash") }.count, 1)
        try Data([1]).write(to: original); XCTAssertThrowsError(try MacTab5FlashBackup.read(original.appendingPathExtension("json")))
    }
}
