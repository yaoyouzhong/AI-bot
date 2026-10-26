import Darwin
import Foundation
import CryptoKit

enum MacTab5LocalError: LocalizedError {
    case invalid(String)
    var errorDescription: String? { if case .invalid(let text) = self { return text }; return nil }
}

enum MacTab5LocalProcess {
    // Arguments are never interpreted by a shell. Combined bounded output also
    // drains stderr, so a failing tool cannot deadlock behind a full error pipe.
    static func run(_ executable: String, _ arguments: [String], maximum: Int = 16384, timeout: TimeInterval = 15, directory: URL? = nil) throws -> Data {
        let process = Process(), pipe = Pipe()
        process.executableURL = URL(fileURLWithPath: executable); process.arguments = arguments
        process.currentDirectoryURL = directory
        process.standardOutput = pipe; process.standardError = pipe; process.standardInput = FileHandle.nullDevice
        try process.run()
        let terminate = DispatchWorkItem {
            if process.isRunning { process.terminate() }
            DispatchQueue.global().asyncAfter(deadline: .now() + 1) { if process.isRunning { kill(process.processIdentifier, SIGKILL) } }
        }
        DispatchQueue.global().asyncAfter(deadline: .now() + timeout, execute: terminate)
        defer { terminate.cancel(); try? pipe.fileHandleForReading.close() }
        var data = Data()
        while true {
            let chunk = pipe.fileHandleForReading.availableData
            if chunk.isEmpty { break }
            guard data.count + chunk.count <= maximum else { terminate.perform(); process.waitUntilExit(); throw MacTab5LocalError.invalid("工具输出超过上限，操作已停止。") }
            data.append(chunk)
        }
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw MacTab5LocalError.invalid("工具执行失败或超时（\(process.terminationStatus)），操作未确认。") }
        return data
    }
}

struct MacTab5Archive {
    let url: URL
    let names: [String]
    private let nested: Bool, unicode: Bool
    init(_ url: URL, maximumSize: Int = 2 * 1024 * 1024 * 1024, allowNested: Bool = false, allowUnicode: Bool = false, maximumEntries: Int = 8192) throws {
        guard url.isFileURL, url.pathExtension.lowercased() == "zip",
              let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize, size > 0, size <= maximumSize else { throw MacTab5LocalError.invalid("请选择有效的 AI-bot ZIP 包。") }
        self.url = url; nested = allowNested; unicode = allowUnicode
        names = try MacTab5ZIPDirectory.names(url, maximumEntries: maximumEntries)
        guard !names.isEmpty, names.count <= maximumEntries, Set(names.map { $0.lowercased() }).count == names.count,
              names.allSatisfy({ allowUnicode ? Self.safeUnicodePath($0) : allowNested ? Self.safePath($0) : Self.safeName($0) }) else { throw MacTab5LocalError.invalid("ZIP 含重复或不安全的文件名。") }
    }
    static func safeName(_ name: String) -> Bool {
        !name.isEmpty && !name.contains("..") && name.range(of: "^[A-Za-z0-9][A-Za-z0-9_.-]*$", options: .regularExpression) != nil
    }
    static func safePath(_ name: String) -> Bool {
        let value = name.hasSuffix("/") ? String(name.dropLast()) : name
        let parts = value.split(separator: "/", omittingEmptySubsequences: false)
        // Published firmware ZIPs include dependency licenses nine components
        // deep. Keep bounded paths while validating every component as before.
        return value.utf8.count <= 720 && parts.count <= 16 && parts.allSatisfy { safeName(String($0)) }
    }
    static func safeUnicodePath(_ name: String) -> Bool {
        let value = name.hasSuffix("/") ? String(name.dropLast()) : name
        let parts = value.split(separator: "/", omittingEmptySubsequences: false)
        return value.utf8.count <= 720 && parts.count <= 8 && parts.allSatisfy { part in
            !part.isEmpty && part != "." && part != ".." && !part.hasSuffix(".") && !part.hasSuffix(" ") && !part.hasPrefix("-") && part.unicodeScalars.allSatisfy { !CharacterSet.controlCharacters.contains($0) && !"\\:<>\"|?*".unicodeScalars.contains($0) }
        }
    }
    private func containsFile(_ name: String) -> Bool { !name.hasSuffix("/") && (unicode ? Self.safeUnicodePath(name) : nested ? Self.safePath(name) : Self.safeName(name)) && names.contains(name) }
    private func reader(_ name:String) -> (String,[String]) {
        // macOS unzip does not consistently preserve UTF-8 ZIP names. bsdtar
        // handles them; escape its glob metacharacters to select one exact entry.
        if unicode {
            let literal = name.replacingOccurrences(of:"[",with:"\\[").replacingOccurrences(of:"]",with:"\\]")
            return ("/usr/bin/tar",["-xOf",url.path,"--",literal])
        }
        return ("/usr/bin/unzip",["-p",url.path,name])
    }
    func read(_ name: String, maximum: Int) throws -> Data {
        guard containsFile(name) else { throw MacTab5LocalError.invalid("包内缺少必需文件。") }
        // Stream the exact validated entry; never extract paths or symlinks.
        let command = reader(name)
        return try MacTab5LocalProcess.run(command.0, command.1, maximum: maximum, timeout: 60)
    }
    func copy(_ name: String, to destination: URL, maximum: Int, sha256: String, check: () throws -> Void = {}) throws {
        guard containsFile(name), MacTab5Protocol.isHex(sha256, count: 64), !FileManager.default.fileExists(atPath: destination.path) else { throw MacTab5Error.invalidPacket }
        let process = Process(), pipe = Pipe()
        let command = reader(name)
        process.executableURL = URL(fileURLWithPath:command.0); process.arguments = command.1
        process.standardOutput = pipe; process.standardError = FileHandle.nullDevice; process.standardInput = FileHandle.nullDevice
        guard FileManager.default.createFile(atPath: destination.path, contents: nil, attributes: [.posixPermissions: 0o600]) else { throw MacTab5Error.invalidPacket }
        let output = try FileHandle(forWritingTo: destination); defer { try? output.close(); try? pipe.fileHandleForReading.close() }
        try process.run()
        let timeout = DispatchWorkItem { if process.isRunning { process.terminate() }; DispatchQueue.global().asyncAfter(deadline: .now() + 1) { if process.isRunning { kill(process.processIdentifier, SIGKILL) } } }
        DispatchQueue.global().asyncAfter(deadline: .now() + 300, execute: timeout); defer { timeout.cancel() }
        var count = 0, digest = SHA256()
        do {
            while true { try check(); let bytes = pipe.fileHandleForReading.availableData; if bytes.isEmpty { break }; count += bytes.count; guard count <= maximum else { throw MacTab5Error.invalidPacket }; digest.update(data: bytes); try output.write(contentsOf: bytes) }
            process.waitUntilExit()
            guard process.terminationStatus == 0, count > 0, digest.finalize().map({ String(format: "%02x", $0) }).joined() == sha256 else { throw MacTab5LocalError.invalid("内置 TAB5 ZIP 的长度、读取结果或 SHA-256 不匹配。") }
        } catch { timeout.perform(); process.waitUntilExit(); throw error }
    }
}
