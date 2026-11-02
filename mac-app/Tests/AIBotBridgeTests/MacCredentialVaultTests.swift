import XCTest
import MacKeychainSupport
import Security
@testable import AIBotBridge

final class MacCredentialVaultTests: XCTestCase {
    private final class Memory {
        var items: [String: Data] = [:]
        var denied = Set<String>(), writes = 0, reads: [(String, Bool)] = []
        var failWrite = false, failDelete = false
        func vault() -> BridgeCredentialVault {
            .init(backend: .init(read: { account, interactive in
                self.reads.append((account, interactive))
                if self.denied.contains(account) { throw BridgeKeychain.Failure.status(errSecInteractionNotAllowed) }
                return self.items[account]
            }, write: { account, value, addOnly in
                if self.failWrite { throw BridgeKeychain.Failure.status(errSecNotAvailable) }
                if addOnly && self.items[account] != nil { throw BridgeKeychain.Failure.status(errSecDuplicateItem) }
                self.writes += 1; self.items[account] = value
            }, accounts: { Array(self.items.keys) }, remove: { account in
                if self.failDelete { throw BridgeKeychain.Failure.status(errSecInteractionNotAllowed) }
                self.items.removeValue(forKey: account)
            }))
        }
        func snapshot() throws -> BridgeCredentialSnapshot {
            try .decode(items[BridgeCredentialVault.account]!)
        }
    }
    private let snapshot = BridgeKeychain.Request(operation: "snapshot", account: BridgeCredentialVault.account)
    func testLegacyHelperResponseIsRecognizedWithoutLooseningRequestValidation() throws {
        let legacy = try JSONDecoder().decode(BridgeKeychain.Response.self, from: Data("{\"status\":0}".utf8))
        XCTAssertNil(legacy.vaultProtocol); XCTAssertFalse(legacy.migrationRequired)
        let current = try JSONDecoder().decode(BridgeKeychain.Response.self, from: JSONEncoder().encode(BridgeKeychain.Response(status: errSecSuccess)))
        XCTAssertEqual(current.vaultProtocol, 1)
    }
    func testFreshInstallationAndCompleteLosslessMigration() throws {
        let empty = Memory()
        XCTAssertEqual(try empty.vault().perform(snapshot).status, errSecSuccess)
        XCTAssertTrue(try empty.snapshot().values.isEmpty)
        let old = Memory()
        old.items = ["pairing-token": Data(repeating: 7, count: 40), "tab5-pairing-v1": Data([1, 2]),
                     "tab5-private-v1": Data(repeating: 8, count: 32), "domestic-provider-v1-kimi": Data([3]),
                     "qweather-api-key-v1:fixture.qweatherapi.com": Data([4]), "unrelated": Data([5])]
        let original = old.items
        _ = try old.vault().perform(snapshot)
        XCTAssertEqual(try old.snapshot().values, original.filter { BridgeKeychain.allowed($0.key) })
        for (key, value) in original { XCTAssertEqual(old.items[key], value) }
        XCTAssertTrue(old.reads.filter { $0.0 != BridgeCredentialVault.account }.allSatisfy { !$0.1 })
        XCTAssertEqual(old.writes, 1)
        XCTAssertEqual(try old.vault().perform(.init(operation: "verify-migration", account: BridgeCredentialVault.account)).status, errSecSuccess)
        old.items["pairing-token"] = Data([99])
        XCTAssertEqual(try old.vault().perform(.init(operation: "verify-migration", account: BridgeCredentialVault.account)).status, errSecDecode)
    }
    func testDeniedLegacyMigrationAndFailedWriteNeverCreatePartialVault() throws {
        let memory = Memory(); memory.items = ["pairing-token": Data([1]), "tab5-private-v1": Data([2])]
        let old = memory.items; memory.denied.insert("tab5-private-v1")
        XCTAssertThrowsError(try memory.vault().perform(snapshot))
        XCTAssertEqual(memory.items, old)
        memory.denied.removeAll(); memory.failWrite = true
        XCTAssertThrowsError(try memory.vault().perform(snapshot))
        XCTAssertEqual(memory.items, old)
    }
    func testInteractiveLegacyReadRequiresExplicitMigrationRequest() throws {
        var stored: Data?, requested: [Bool] = []
        let vault = BridgeCredentialVault(backend: .init(read: { account, interactive in
            if account == BridgeCredentialVault.account { return stored }
            requested.append(interactive)
            if !interactive { throw BridgeKeychain.Failure.status(errSecInteractionNotAllowed) }
            return Data([9])
        }, write: { _, bytes, _ in stored = bytes }, accounts: { ["pairing-token"] }, remove: { _ in }))
        XCTAssertThrowsError(try vault.perform(snapshot)) { error in
            guard case BridgeKeychain.Failure.migrationRequired = error else { return XCTFail("Expected explicit migration") }
        }
        XCTAssertNil(stored)
        let response = try vault.perform(.init(operation: "migrate", account: BridgeCredentialVault.account))
        XCTAssertEqual(try BridgeCredentialSnapshot.decode(response.value!).values["pairing-token"], Data([9]))
        XCTAssertEqual(requested, [false, true])
    }
    func testCorruptOrUnknownVersionNeverFallsBackOrOverwrites() throws {
        for bytes in [Data("broken".utf8), Data("{\"version\":2,\"values\":{}}".utf8)] {
            let memory = Memory(); memory.items = [BridgeCredentialVault.account: bytes, "pairing-token": Data([1])]
            XCTAssertThrowsError(try memory.vault().perform(snapshot))
            XCTAssertEqual(memory.writes, 0)
            XCTAssertEqual(memory.reads.count, 1)
        }
    }
    func testMutationsAreAuthoritativeAndDoNotResurrectOldRecords() throws {
        let memory = Memory(); memory.items = ["pairing-token": Data([1]), "tab5-mac-unlock-v1": Data([2])]
        _ = try memory.vault().perform(snapshot)
        XCTAssertEqual(try memory.vault().perform(.init(operation: "add", account: "pairing-token", value: Data([3]))).status, errSecDuplicateItem)
        _ = try memory.vault().perform(.init(operation: "save", account: "pairing-token", value: Data([4])))
        XCTAssertEqual(try memory.vault().perform(.init(operation: "read", account: "pairing-token")).value, Data([4]))
        XCTAssertEqual(memory.items["pairing-token"], Data([1])) // rollback baseline, never a current fallback
        memory.failDelete = true
        XCTAssertThrowsError(try memory.vault().perform(.init(operation: "remove", account: "tab5-mac-unlock-v1")))
        XCTAssertEqual(try memory.snapshot().values["tab5-mac-unlock-v1"], Data([2]))
        memory.failDelete = false
        _ = try memory.vault().perform(.init(operation: "remove", account: "tab5-mac-unlock-v1"))
        XCTAssertNil(memory.items["tab5-mac-unlock-v1"])
        memory.items["tab5-mac-unlock-v1"] = Data([99])
        XCTAssertEqual(try memory.vault().perform(.init(operation: "read", account: "tab5-mac-unlock-v1")).status, errSecItemNotFound)
    }
    func testFailedMutationPreservesCommittedSnapshot() throws {
        let memory = Memory(); _ = try memory.vault().perform(snapshot)
        memory.failWrite = true
        XCTAssertThrowsError(try memory.vault().perform(.init(operation: "save", account: "pairing-token", value: Data([8]))))
        XCTAssertTrue(try memory.snapshot().values.isEmpty)
    }
    func testSnapshotBoundsAndRequestAllowlist() throws {
        XCTAssertThrowsError(try BridgeCredentialSnapshot(values: ["unrelated": Data([1])]).encoded())
        XCTAssertThrowsError(try BridgeCredentialSnapshot(values: ["pairing-token": Data()]).encoded())
        XCTAssertThrowsError(try BridgeCredentialSnapshot(values: ["pairing-token": Data(count: 65_537)]).encoded())
        XCTAssertThrowsError(try BridgeCredentialSnapshot.decode(Data(count: BridgeCredentialSnapshot.maximumBytes + 1)))
        XCTAssertThrowsError(try BridgeKeychain.Request(operation: "snapshot", account: "pairing-token").validate())
        XCTAssertThrowsError(try BridgeKeychain.Request(operation: "read", account: BridgeCredentialVault.account).validate())
    }
    func testConcurrentAccountReadsUseOneSnapshotAndDenialDoesNotReprompt() throws {
        let value = try BridgeCredentialSnapshot(values: ["pairing-token": Data([1]), "tab5-private-v1": Data([2])]).encoded()
        var calls = 0
        let cache = MacCredentialCache { request in
            calls += 1; XCTAssertEqual(request.operation, "snapshot")
            return .init(status: errSecSuccess, value: value)
        }
        DispatchQueue.concurrentPerform(iterations: 32) { i in
            XCTAssertEqual(try? cache.perform(.init(operation: "read", account: i % 2 == 0 ? "pairing-token" : "tab5-private-v1")).status, errSecSuccess)
        }
        XCTAssertEqual(calls, 1)
        var deniedCalls = 0
        let denied = MacCredentialCache { _ in deniedCalls += 1; return .init(status: errSecAuthFailed) }
        for account in ["pairing-token", "tab5-private-v1", "tab5-pairing-v1"] {
            XCTAssertThrowsError(try denied.perform(.init(operation: "read", account: account)))
        }
        XCTAssertEqual(deniedCalls, 1)
    }
    func testExplicitSaveCanRecoverDeniedReadAndCacheOnlyCommittedChanges() throws {
        let memory = Memory(); let vault = memory.vault()
        var denySnapshot = true, calls = 0
        let cache = MacCredentialCache { request in
            calls += 1
            if request.operation == "snapshot" && denySnapshot { return .init(status: errSecAuthFailed) }
            return try vault.perform(request)
        }
        XCTAssertThrowsError(try cache.perform(.init(operation: "read", account: "pairing-token")))
        denySnapshot = false
        _ = try cache.perform(.init(operation: "save", account: "pairing-token", value: Data([4])))
        memory.failWrite = true
        XCTAssertThrowsError(try cache.perform(.init(operation: "save", account: "pairing-token", value: Data([5]))))
        XCTAssertEqual(try cache.perform(.init(operation: "read", account: "pairing-token")).value, Data([4]))
        XCTAssertEqual(calls, 3)
    }
    func testMigrationRefusalIsCachedAndOnlyExplicitMigrationRetries() throws {
        var calls: [String] = []
        let bytes = try BridgeCredentialSnapshot(values: ["pairing-token": Data([7])]).encoded()
        let cache = MacCredentialCache { request in
            calls.append(request.operation)
            return request.operation == "migrate" ? .init(status: errSecSuccess, value: bytes) :
                .init(status: errSecInteractionNotAllowed, migrationRequired: true)
        }
        for operation in ["snapshot", "read"] {
            XCTAssertThrowsError(try cache.perform(.init(operation: operation, account: operation == "snapshot" ? BridgeCredentialVault.account : "pairing-token"))) { error in
                guard case BridgeKeychain.Failure.migrationRequired = error else { return XCTFail("Missing migration reason") }
            }
        }
        _ = try cache.perform(.init(operation: "migrate", account: BridgeCredentialVault.account))
        XCTAssertEqual(try cache.perform(.init(operation: "read", account: "pairing-token")).value, Data([7]))
        XCTAssertEqual(calls, ["snapshot", "migrate"])
    }
}
