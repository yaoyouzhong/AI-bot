import Foundation
import MacKeychainSupport
import Security

enum MacKeychainAccess {
    private static let lock = NSLock()
    private static func perform(_ request: BridgeKeychain.Request) throws -> BridgeKeychain.Response {
        try request.validate()
        lock.lock(); defer { lock.unlock() }
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
        DispatchQueue.global().asyncAfter(deadline: .now() + 120, execute: timeout)
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
    static func probe() throws {
        guard Bundle.main.bundleURL.pathExtension == "app",
              let code = try? BridgeKeychain.staticCode(Bundle.main.bundleURL),
              BridgeKeychain.certificate(code) != nil else { throw BridgeKeychain.Failure.identity }
        guard try perform(.init(operation: "probe", account: "pairing-token")).status == errSecSuccess else { throw BridgeKeychain.Failure.unavailable }
    }
    static func save(_ account: String, _ value: Data, addOnly: Bool = false) throws {
        let result = try perform(.init(operation: addOnly ? "add" : "save", account: account, value: value))
        guard result.status == errSecSuccess else { throw BridgeKeychain.Failure.status(result.status) }
    }
}
