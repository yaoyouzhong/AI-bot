import XCTest
@testable import AIBotBridge

final class MacModuleParityTests: XCTestCase {
    // AppKit validation pumps the run loop while waiting for main-queue actions.
    // XCTest may invoke a test from a main-queue block, which cannot re-enter
    // that queue. Use the existing CLI self-test at a fresh process entry point
    // instead of changing the production menu dispatch or weakening assertions.
    private func runParitySelfTest() throws {
        let executable = Bundle(for: Self.self).bundleURL.deletingLastPathComponent()
            .appendingPathComponent("AIBotBridge")
        guard FileManager.default.isExecutableFile(atPath: executable.path) else {
            XCTFail("Missing built parity self-test executable: \(executable.path)")
            return
        }
        let log = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-parity-test-" + UUID().uuidString)
        FileManager.default.createFile(atPath: log.path, contents: nil)
        defer { try? FileManager.default.removeItem(at: log) }
        let output = try FileHandle(forWritingTo: log)
        defer { try? output.close() }
        let process = Process()
        process.executableURL = executable
        process.arguments = ["--self-test-parity"]
        process.standardOutput = output
        process.standardError = output
        let ended = DispatchSemaphore(value: 0)
        process.terminationHandler = { _ in ended.signal() }
        try process.run()
        guard ended.wait(timeout: .now() + 30) == .success else {
            process.terminate()
            XCTFail("Parity self-test exceeded 30 seconds")
            return
        }
        let text = try String(contentsOf: log, encoding: .utf8)
        XCTAssertEqual(process.terminationStatus, 0, text)
        XCTAssertTrue(text.contains("MAC_PARITY_SELF_TEST_OK"), text)
    }

    func testModuleStorageAndProtocolContracts() throws { try runParitySelfTest() }

    func testParityValidationAlsoCompletesWhenCalledFromMainQueue() {
        let done = expectation(description: "parity validation from main queue")
        DispatchQueue.main.async {
            do { try self.runParitySelfTest() }
            catch { XCTFail("Parity subprocess failed: \(error)") }
            done.fulfill()
        }
        wait(for: [done], timeout: 35)
    }
}
