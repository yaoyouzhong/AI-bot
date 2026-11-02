import Foundation
import MacKeychainSupport
import Security

enum MacKeychainAccess {
    private static let cache = MacCredentialCache(call: transport)
    private static func perform(_ request: BridgeKeychain.Request) throws -> BridgeKeychain.Response {
        try cache.perform(request)
    }
    private static func transport(_ request: BridgeKeychain.Request) throws -> BridgeKeychain.Response {
        try request.validate()
        let bundle = Bundle.main.bundleURL
        let helper = bundle.appendingPathComponent("Contents/Helpers/AIBotKeychainHelper")
        // CLI/debug and ad-hoc CI retain the original direct Keychain behavior.
        // A configured certificate-signed app must fail closed if its helper is
        // missing/tampered, rather than silently prompting through another path.
        guard bundle.pathExtension == "app", let appCode = try? BridgeKeychain.staticCode(bundle),
              let cert = BridgeKeychain.certificate(appCode) else { return try BridgeKeychain.perform(request) }
        let code = try BridgeKeychain.staticCode(helper)
        try BridgeKeychain.verify(code, identifier: BridgeKeychain.helperID, certificate: cert)
        let process = Process(), input = Pipe(), output = Pipe()
        process.executableURL = helper
        process.environment = ["PATH": "/usr/bin:/bin:/usr/sbin:/sbin"]
        process.standardInput = input; process.standardOutput = output; process.standardError = FileHandle.nullDevice
        try process.run()
        let timeout = DispatchWorkItem { if process.isRunning { process.terminate() } }
        // Onboarding may include reading the system dialog and entering a
        // password. Do not end that interaction after the normal RPC deadline.
        let deadline: TimeInterval = ["snapshot", "migrate"].contains(request.operation) ? 600 : 120
        DispatchQueue.global().asyncAfter(deadline: .now() + deadline, execute: timeout)
        defer {
            timeout.cancel()
            try? input.fileHandleForWriting.close(); try? output.fileHandleForReading.close()
            if process.isRunning { process.terminate() }
        }
        try input.fileHandleForWriting.write(contentsOf: JSONEncoder().encode(request))
        try input.fileHandleForWriting.close()
        // Keychain UI is owned by macOS. A stuck/cancelled request is never
        // retried by this layer and never replaces existing credentials.
        let data = try BridgeKeychain.readBounded(output.fileHandleForReading)
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw BridgeKeychain.Failure.unavailable }
        guard data.count <= BridgeKeychain.limit else { throw BridgeKeychain.Failure.invalid }
        return try JSONDecoder().decode(BridgeKeychain.Response.self, from: data)
    }
    static func read(_ account: String) throws -> Data? {
        let result = try perform(.init(operation: "read", account: account))
        if result.status == errSecItemNotFound { return nil }
        guard result.status == errSecSuccess, let bytes = result.value else { throw BridgeKeychain.Failure.status(result.status) }
        return bytes
    }
    static func prepare() throws { _ = try perform(.init(operation: "snapshot", account: BridgeCredentialVault.account)) }
    static func verifyMigration() throws {
        let response = try transport(.init(operation: "verify-migration", account: BridgeCredentialVault.account))
        guard response.status == errSecSuccess else { throw BridgeKeychain.Failure.status(response.status) }
    }
    static func prepareUsingLegacyHelper() throws {
        let probe = try transport(.init(operation: "probe", account: "pairing-token"))
        guard probe.status == errSecSuccess, probe.vaultProtocol == nil else { throw BridgeKeychain.Failure.invalid }
        try BridgeKeychain.prepareUsingLegacyHelper { account in
            let response = try transport(.init(operation: "read", account: account))
            if response.status == errSecItemNotFound { return nil }
            guard response.status == errSecSuccess, let bytes = response.value else { throw BridgeKeychain.Failure.status(response.status) }
            return bytes
        }
    }
    static func migrate() throws {
        let response = try perform(.init(operation: "migrate", account: BridgeCredentialVault.account))
        guard response.status == errSecSuccess else { throw BridgeKeychain.Failure.status(response.status) }
    }
    static func probe() throws {
        guard Bundle.main.bundleURL.pathExtension == "app",
              let code = try? BridgeKeychain.staticCode(Bundle.main.bundleURL),
              BridgeKeychain.certificate(code) != nil else { throw BridgeKeychain.Failure.identity }
        guard try perform(.init(operation: "probe", account: "pairing-token")).status == errSecSuccess else { throw BridgeKeychain.Failure.unavailable }
    }
    static func remove(_ account:String) throws {
        let result = try perform(.init(operation:"remove",account:account))
        guard result.status == errSecSuccess else { throw BridgeKeychain.Failure.status(result.status) }
    }
    static func save(_ account: String, _ value: Data, addOnly: Bool = false) throws {
        let result = try perform(.init(operation: addOnly ? "add" : "save", account: account, value: value))
        guard result.status == errSecSuccess else { throw BridgeKeychain.Failure.status(result.status) }
    }
}

// One authorized snapshot per application process. A declined read is sticky;
// polling other accounts cannot open more Keychain prompts. Explicit writes may
// retry authorization and replace the cache only after the transaction succeeds.
final class MacCredentialCache {
    private let lock = NSLock()
    private let call: (BridgeKeychain.Request) throws -> BridgeKeychain.Response
    private var snapshot: Result<BridgeCredentialSnapshot, Error>?
    init(call: @escaping (BridgeKeychain.Request) throws -> BridgeKeychain.Response) { self.call = call }
    func perform(_ request: BridgeKeychain.Request) throws -> BridgeKeychain.Response {
        try request.validate()
        lock.lock(); defer { lock.unlock() }
        if request.operation == "probe" { return try call(request) }
        if ["read", "snapshot"].contains(request.operation) {
            if snapshot == nil {
                snapshot = Result { try Self.decode(call(.init(operation: "snapshot", account: BridgeCredentialVault.account))) }
            }
            let loaded = try snapshot!.get()
            if request.operation == "snapshot" { return .init(status: errSecSuccess, value: try loaded.encoded()) }
            let value = loaded.values[request.account]
            return .init(status: value == nil ? errSecItemNotFound : errSecSuccess, value: value)
        }
        let response = try call(request)
        guard response.status == errSecSuccess else { return response }
        let decoded = Result { try Self.decode(response) }
        snapshot = decoded
        _ = try decoded.get()
        return response
    }
    private static func decode(_ response: BridgeKeychain.Response) throws -> BridgeCredentialSnapshot {
        if response.migrationRequired { throw BridgeKeychain.Failure.migrationRequired }
        guard response.status == errSecSuccess else { throw BridgeKeychain.Failure.status(response.status) }
        guard let value = response.value else { throw BridgeKeychain.Failure.invalid }
        return try .decode(value)
    }
}
