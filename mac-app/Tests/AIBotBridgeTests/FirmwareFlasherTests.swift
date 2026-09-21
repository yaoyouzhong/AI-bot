import XCTest
@testable import AIBotBridge

final class FirmwareFlasherTests: XCTestCase {
    func testDeviceSelectionDoesNotGuessOrSwitchOnDisconnect() {
        let screen = "/dev/cu.usbserialSCREEN", other = "/dev/cu.usbserialOTHER"
        var selection = MacFlashDeviceSelection(bridgePort: screen)
        selection.update([screen, other]); XCTAssertEqual(selection.selected, screen)
        selection.update([other]); XCTAssertNil(selection.selected)
        selection.update([screen, other]); XCTAssertEqual(selection.selected, screen)
        selection.scanFailed(); XCTAssertNil(selection.selected)
        selection.update([screen, other]); XCTAssertEqual(selection.selected, screen)
        var single = MacFlashDeviceSelection(); single.update([screen]); XCTAssertEqual(single.selected, screen)
        single.update([]); XCTAssertNil(single.selected); XCTAssertEqual(single.message, "正在确认连接…")
        single.update([screen]); XCTAssertEqual(single.selected, screen)
        var ambiguous = MacFlashDeviceSelection(); ambiguous.update([screen, other]); XCTAssertNil(ambiguous.selected)
        ambiguous.update([other]); XCTAssertNil(ambiguous.selected)
        ambiguous.update([screen, other]); XCTAssertEqual(ambiguous.selected, screen)
        var empty = MacFlashDeviceSelection(); empty.update([]); empty.update([screen, other]); XCTAssertNil(empty.selected)
    }
    func testCapacity() throws {
        XCTAssertEqual(try MacFirmwareFlasher.flashSize("Detected flash size: 4MB"), 4 * 1024 * 1024)
        XCTAssertEqual(try MacFirmwareFlasher.flashSize("Detected flash size: 512KB"), 512 * 1024)
        XCTAssertThrowsError(try MacFirmwareFlasher.flashSize("connection failed"))
    }

    func testBackupGatesWriteAndVerification() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let firmware = root.appendingPathComponent("firmware.bin")
        try Data(repeating: 0, count: 2048).write(to: firmware)
        for scenario in ["success", "backup-only", "short-backup", "backup-error", "write-error", "verify-error"] {
            var commands: [String] = []
            let flasher = MacFirmwareFlasher(run: { args in
                let command = args.first { ["image_info", "flash_id", "read_flash", "write_flash", "verify_flash"].contains($0) }!
                commands.append(command)
                if command == "flash_id" { return "Detected flash size: 1MB" }
                if command == "read_flash" {
                    if scenario == "backup-error" { throw FlashError.failed("simulated backup error") }
                    try Data(repeating: 0, count: scenario == "short-backup" ? 32 : 1048576).write(to: URL(fileURLWithPath: args.last!))
                }
                if command == "write_flash" && scenario == "write-error" { throw FlashError.failed("simulated write error") }
                if command == "verify_flash" && scenario == "verify-error" { throw FlashError.failed("simulated verify error") }
                return "OK"
            }, stage: { _ in })
            let action = { try flasher.execute(port: "/dev/cu.usbserialTEST", firmware: scenario == "backup-only" ? nil : firmware, backupDirectory: root.appendingPathComponent(scenario)) }
            if ["success", "backup-only"].contains(scenario) { XCTAssertNoThrow(try action()) }
            else { XCTAssertThrowsError(try action()) }
            if ["backup-only", "short-backup", "backup-error"].contains(scenario) { XCTAssertFalse(commands.contains("write_flash")) }
            if scenario == "write-error" { XCTAssertFalse(commands.contains("verify_flash")) }
            if scenario == "success" { XCTAssertEqual(commands, ["image_info", "flash_id", "read_flash", "write_flash", "verify_flash"]) }
        }
    }
}
