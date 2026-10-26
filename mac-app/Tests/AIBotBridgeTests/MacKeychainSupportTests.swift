import Foundation
import MacKeychainSupport
import XCTest

final class MacKeychainSupportTests: XCTestCase {
    func testHelperCannotAccessOtherServicesAccountsOrOperations() throws {
        for account in ["pairing-token", "tab5-pairing-v1", "tab5-private-v1", "deepseek-api-key-v1", "qweather-api-key-v1:abc.qweatherapi.com"] {
            XCTAssertNoThrow(try BridgeKeychain.Request(operation: "read", account: account).validate())
            XCTAssertNoThrow(try BridgeKeychain.Request(operation: "save", account: account, value: Data([1])).validate())
            XCTAssertThrowsError(try BridgeKeychain.Request(operation: "delete", account: account).validate())
            XCTAssertThrowsError(try BridgeKeychain.Request(operation: "read", account: account, value: Data([1])).validate())
        }
        for account in ["login", "", "tab5-private-v2", "qweather-api-key-v1:qweatherapi.com", "qweather-api-key-v1:abc.qweatherapi.com.evil", "qweather-api-key-v1:user@abc.qweatherapi.com", "qweather-api-key-v1:abc..qweatherapi.com"] {
            XCTAssertThrowsError(try BridgeKeychain.Request(operation: "read", account: account).validate())
        }
        XCTAssertThrowsError(try BridgeKeychain.Request(operation: "save", account: "pairing-token", value: Data()).validate())
        XCTAssertThrowsError(try BridgeKeychain.Request(operation: "save", account: "pairing-token", value: Data(repeating: 1, count: 65_537)).validate())
        // This is a pure identity/protocol probe, with no Keychain operation.
        XCTAssertEqual(try BridgeKeychain.perform(.init(operation: "probe", account: "pairing-token")).status, 0)
    }
    func testIdentityRequirementCannotBecomeAnIdentifierOnlyOrInjectedRequirement() throws {
        let cert = String(repeating: "a", count: 40)
        XCTAssertNoThrow(try BridgeKeychain.requirement(identifier: BridgeKeychain.appID, certificate: cert))
        XCTAssertNoThrow(try BridgeKeychain.requirement(identifier: BridgeKeychain.helperID, certificate: cert))
        XCTAssertThrowsError(try BridgeKeychain.requirement(identifier: "other", certificate: cert))
        XCTAssertThrowsError(try BridgeKeychain.requirement(identifier: BridgeKeychain.appID, certificate: ""))
        XCTAssertThrowsError(try BridgeKeychain.requirement(identifier: BridgeKeychain.appID, certificate: "\" or true"))
    }
    func testPipeInputIsCompleteAndBoundedBeforeDecoding() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let bytes = Data(repeating: 3, count: BridgeKeychain.limit)
        try bytes.write(to: root)
        let file = try FileHandle(forReadingFrom: root); defer { try? file.close() }
        XCTAssertEqual(try BridgeKeychain.readBounded(file), bytes)
        try (bytes + Data([4])).write(to: root)
        let oversized = try FileHandle(forReadingFrom: root); defer { try? oversized.close() }
        XCTAssertThrowsError(try BridgeKeychain.readBounded(oversized))
    }
}
