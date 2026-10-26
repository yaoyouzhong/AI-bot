import CryptoKit
import Foundation
import Security

public enum BridgeKeychain {
    public static let service = "io.github.yaoyouzhong.AI-bot"
    public static let appID = "io.github.yaoyouzhong.aibot.bridge"
    public static let helperID = appID + ".keychain"
    public static let limit = 131_072
    public enum Failure: Error { case invalid, identity, unavailable, status(OSStatus) }

    public struct Request: Codable {
        public let operation: String
        public let account: String
        public let value: Data?
        public init(operation: String, account: String, value: Data? = nil) {
            self.operation = operation; self.account = account; self.value = value
        }
        public func validate() throws {
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
        public init(status: OSStatus, value: Data? = nil) { self.status = status; self.value = value }
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
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword,
                                   kSecAttrService as String: service, kSecAttrAccount as String: request.account]
        if request.operation == "remove" {
            let result = SecItemDelete(query as CFDictionary)
            return Response(status:result == errSecItemNotFound ? errSecSuccess:result)
        }
        if request.operation == "read" {
            var read = query; read[kSecReturnData as String] = true; read[kSecMatchLimit as String] = kSecMatchLimitOne
            var result: CFTypeRef?
            let status = SecItemCopyMatching(read as CFDictionary, &result)
            guard status != errSecSuccess || result is Data else { return Response(status: errSecDecode) }
            let bytes = result as? Data
            guard bytes.map({ $0.count <= 65_536 }) ?? true else { return Response(status: errSecDecode) }
            return Response(status: status, value: status == errSecSuccess ? bytes : nil)
        }
        var status = request.operation == "add" ? errSecItemNotFound :
            SecItemUpdate(query as CFDictionary, [kSecValueData as String: request.value!] as CFDictionary)
        if status == errSecItemNotFound {
            var item = query; item[kSecValueData as String] = request.value!
            item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            status = SecItemAdd(item as CFDictionary, nil)
        }
        return Response(status: status)
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
