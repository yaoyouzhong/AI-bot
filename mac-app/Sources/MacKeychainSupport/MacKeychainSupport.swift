import CryptoKit
import Darwin
import Foundation
import LocalAuthentication
import Security

public enum BridgeKeychain {
    public static let service = "io.github.yaoyouzhong.AI-bot"
    public static let appID = "io.github.yaoyouzhong.aibot.bridge"
    public static let helperID = appID + ".keychain"
    public static let limit = 2_097_152
    public enum Failure: Error { case invalid, identity, unavailable, migrationRequired, status(OSStatus) }

    public struct Request: Codable {
        public let operation: String
        public let account: String
        public let value: Data?
        public init(operation: String, account: String, value: Data? = nil) {
            self.operation = operation; self.account = account; self.value = value
        }
        public func validate() throws {
            if ["snapshot", "migrate", "verify-migration"].contains(operation), account == BridgeCredentialVault.account, value == nil { return }
            guard BridgeKeychain.allowed(account),
                  (["read", "probe"].contains(operation) && value == nil) ||
                  (operation == "remove" && account == "tab5-mac-unlock-v1" && value == nil) ||
                  (["save", "add"].contains(operation) && value.map { !$0.isEmpty && $0.count <= 65_536 } == true)
            else { throw Failure.invalid }
        }
    }
    public struct Response: Codable {
        public let status: OSStatus
        public let value: Data?
        public let migrationRequired: Bool
        public let vaultProtocol: Int?
        public init(status: OSStatus, value: Data? = nil, migrationRequired: Bool = false) { self.status = status; self.value = value; self.migrationRequired = migrationRequired; vaultProtocol = 1 }
        private enum CodingKeys: String, CodingKey { case status, value, migrationRequired, vaultProtocol }
        public init(from decoder: Decoder) throws {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            status = try container.decode(OSStatus.self, forKey: .status)
            value = try container.decodeIfPresent(Data.self, forKey: .value)
            migrationRequired = try container.decodeIfPresent(Bool.self, forKey: .migrationRequired) ?? false
            vaultProtocol = try container.decodeIfPresent(Int.self, forKey: .vaultProtocol)
        }
    }
    public static func allowed(_ account: String) -> Bool {
        if ["qwen", "kimi", "minimax", "deepseek", "zhipu", "stepfun", "baidu", "xiaomi"].contains(where: { account == "domestic-provider-v1-" + $0 }) { return true }
        if ["pairing-token", "tab5-pairing-v1", "tab5-private-v1", "deepseek-api-key-v1", "tab5-mac-unlock-v1"].contains(account) { return true }
        let prefix = "qweather-api-key-v1:"
        guard account.hasPrefix(prefix) else { return false }
        let host = String(account.dropFirst(prefix.count))
        return host.count <= 253 && host.hasSuffix(".qweatherapi.com") && host.count > ".qweatherapi.com".count &&
            host.utf8.allSatisfy { (97...122).contains($0) || (48...57).contains($0) || $0 == 45 || $0 == 46 } &&
            host.split(separator: ".", omittingEmptySubsequences: false).allSatisfy { !$0.isEmpty && $0.count <= 63 && $0.first != "-" && $0.last != "-" }
    }
    public static func perform(_ request: Request) throws -> Response {
        try request.validate()
        if request.operation == "probe" { return Response(status: errSecSuccess) }
        return try withVaultLock {
            let vault = BridgeCredentialVault(backend: .init(read: rawRead, write: rawWrite, accounts: legacyAccounts, remove: rawRemove))
            do { return try vault.perform(request) }
            catch Failure.migrationRequired { return Response(status: errSecInteractionNotAllowed, migrationRequired: true) }
            catch Failure.status(let status) { return Response(status: status) }
            catch { return Response(status: errSecDecode) }
        }
    }
    // Deployment-only bridge from an already-authorized, unchanged helper. The
    // supplied reader must validate that helper and its containing signed app.
    // A normal startup never selects this path or silently switches accessors.
    public static func prepareUsingLegacyHelper(read: @escaping (String) throws -> Data?) throws {
        try withVaultLock {
            let vault = BridgeCredentialVault(backend: .init(read: { account, interactive in
                account == BridgeCredentialVault.account ? try rawRead(account, interactive) : try read(account)
            }, write: rawWrite, accounts: legacyAccounts, remove: rawRemove))
            let loaded = try vault.perform(.init(operation: "snapshot", account: BridgeCredentialVault.account))
            guard loaded.status == errSecSuccess,
                  try vault.perform(.init(operation: "verify-migration", account: BridgeCredentialVault.account)).status == errSecSuccess else { throw Failure.invalid }
        }
    }
    private static func withVaultLock<T>(_ body: () throws -> T) throws -> T {
        let directory = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/AI-bot")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let fd = Darwin.open(directory.appendingPathComponent("keychain-vault.lock").path, O_RDWR | O_CREAT | O_CLOEXEC | O_NOFOLLOW, mode_t(0o600))
        guard fd >= 0 else { throw Failure.unavailable }
        defer { Darwin.close(fd) }
        var info = stat()
        guard fstat(fd, &info) == 0, info.st_mode & S_IFMT == S_IFREG, info.st_uid == geteuid(), flock(fd, LOCK_EX) == 0 else { throw Failure.unavailable }
        return try body()
    }
    private static func query(_ account: String) -> [String: Any] {
        [kSecClass as String: kSecClassGenericPassword,
         kSecAttrService as String: service, kSecAttrAccount as String: account]
    }
    private static func rawRead(_ account: String, _ interactive: Bool) throws -> Data? {
        var read = query(account)
        read[kSecReturnData as String] = true; read[kSecMatchLimit as String] = kSecMatchLimitOne
        if !interactive { read[kSecUseAuthenticationContext as String] = noninteractiveContext() }
        var result: CFTypeRef?
        let status = SecItemCopyMatching(read as CFDictionary, &result)
        if status == errSecItemNotFound { return nil }
        guard status == errSecSuccess else { throw Failure.status(status) }
        guard let bytes = result as? Data, bytes.count <= (account == BridgeCredentialVault.account ? BridgeCredentialSnapshot.maximumBytes : 65_536) else { throw Failure.invalid }
        return bytes
    }
    private static func legacyAccounts() throws -> [String] {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
                                   kSecReturnAttributes as String: true, kSecMatchLimit as String: kSecMatchLimitAll,
                                   kSecUseAuthenticationContext as String: noninteractiveContext()]
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecItemNotFound { return [] }
        guard status == errSecSuccess else { throw Failure.status(status) }
        guard let rows = result as? [[String: Any]], rows.count <= 256 else { throw Failure.invalid }
        return try rows.map { row in
            guard let account = row[kSecAttrAccount as String] as? String else { throw Failure.invalid }
            return account
        }
    }
    private static func rawRemove(_ account: String) throws {
        // Only the user's explicit Remove Password action reaches this path;
        // let macOS authorize removal of an archived legacy password if needed.
        let status = SecItemDelete(query(account) as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else { throw Failure.status(status) }
    }
    private static func rawWrite(_ account: String, _ bytes: Data, _ addOnly: Bool) throws {
        let query = query(account)
        var status = addOnly ? errSecItemNotFound : SecItemUpdate(query as CFDictionary, [kSecValueData as String: bytes] as CFDictionary)
        if status == errSecItemNotFound {
            var item = query
            item[kSecValueData as String] = bytes
            item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            item[kSecAttrLabel as String] = "AI-bot Bridge 资料"
            status = SecItemAdd(item as CFDictionary, nil)
        }
        guard status == errSecSuccess else { throw Failure.status(status) }
    }
    private static func noninteractiveContext() -> LAContext {
        let context = LAContext()
        context.interactionNotAllowed = true
        return context
    }

    // The helper accepts only its actual containing app, signed by the same
    // leaf certificate. No path-only, identifier-only or ad-hoc authorization.
    public static func certificate(_ code: SecStaticCode) -> String? {
        var info: CFDictionary?
        guard SecCodeCopySigningInformation(code, SecCSFlags(rawValue: kSecCSSigningInformation), &info) == errSecSuccess,
              let cert = (info as? [String: Any])?[kSecCodeInfoCertificates as String] as? [SecCertificate],
              let leaf = cert.first else { return nil }
        return Insecure.SHA1.hash(data: SecCertificateCopyData(leaf) as Data).map { String(format: "%02x", $0) }.joined()
    }
    public static func staticCode(_ url: URL) throws -> SecStaticCode {
        var code: SecStaticCode?
        guard SecStaticCodeCreateWithPath(url as CFURL, [], &code) == errSecSuccess, let code else { throw Failure.identity }
        return code
    }
    public static func requirement(identifier: String, certificate: String) throws -> SecRequirement {
        guard [appID, helperID].contains(identifier), certificate.count == 40,
              certificate.utf8.allSatisfy({ (48...57).contains($0) || (97...102).contains($0) }) else { throw Failure.identity }
        var requirement: SecRequirement?
        let text = "identifier \"\(identifier)\" and certificate leaf = H\"\(certificate)\""
        guard SecRequirementCreateWithString(text as CFString, [], &requirement) == errSecSuccess, let requirement else { throw Failure.identity }
        return requirement
    }
    public static func verify(_ code: SecStaticCode, identifier: String, certificate: String) throws {
        let requirement = try requirement(identifier: identifier, certificate: certificate)
        guard SecStaticCodeCheckValidity(code, SecCSFlags(rawValue: kSecCSStrictValidate), requirement) == errSecSuccess else { throw Failure.identity }
    }
    public static func readBounded(_ handle: FileHandle) throws -> Data {
        var bytes = Data()
        while let chunk = try handle.read(upToCount: min(8192, limit + 1 - bytes.count)), !chunk.isEmpty {
            bytes.append(chunk)
            guard bytes.count <= limit else { throw Failure.invalid }
        }
        return bytes
    }
}
