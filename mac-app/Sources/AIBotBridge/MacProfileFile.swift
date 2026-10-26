import Foundation

/// Local, non-secret application data. A corrupt file is never replaced by a default.
final class MacProfileFile<Value: Codable> {
    static var directory: URL { FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/AI-bot", isDirectory: true) }
    let url: URL
    private let lock = NSRecursiveLock()
    private let initial: () -> Value
    private let validate: (Value) throws -> Void
    private var cached: Value?
    init(_ name: String, directory: URL? = nil, initial: @escaping () -> Value, validate: @escaping (Value) throws -> Void) {
        url = (directory ?? Self.directory).appendingPathComponent(name)
        self.initial = initial; self.validate = validate
    }
    func read() throws -> Value {
        lock.lock(); defer { lock.unlock() }
        if let cached { return cached }
        if !FileManager.default.fileExists(atPath: url.path) { let value = initial(); try validate(value); cached = value; return value }
        guard (try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? Int.max) <= 4 * 1024 * 1024 else { throw MacTab5LocalError.invalid("配置文件过大，原文件已保留。") }
        let value = try JSONDecoder().decode(Value.self, from: Data(contentsOf: url)); try validate(value)
        cached = value; return value
    }
    func write(_ value: Value) throws {
        lock.lock(); defer { lock.unlock() }
        _ = try read(); try validate(value)
        let bytes = try JSONEncoder().encode(value)
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        try bytes.write(to: url, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
        cached = value
    }
    @discardableResult func update<T>(_ change: (inout Value) throws -> T) throws -> T {
        lock.lock(); defer { lock.unlock() }
        var value = try read()
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys]
        let before = try encoder.encode(value)
        let result = try change(&value)
        if try encoder.encode(value) != before { try write(value) }
        return result
    }
}

enum MacProfileValidation {
    static func text(_ value: String, limit: Int) -> Bool {
        !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && value.utf16.count <= limit &&
            !value.unicodeScalars.contains { CharacterSet.controlCharacters.contains($0) }
    }
    static func id() -> String { UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased() }
    static func object<T: Encodable>(_ value: T) throws -> [String: Any] { try MacTab5Protocol.object(JSONEncoder().encode(value)) }
}
