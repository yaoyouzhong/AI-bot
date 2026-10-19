import Darwin
import Foundation

enum MacTab5LocalError: LocalizedError {
    case invalid(String)
    var errorDescription: String? { if case .invalid(let text) = self { return text }; return nil }
}

enum MacTab5LocalProcess {
    // Arguments are never interpreted by a shell. Combined bounded output also
    // drains stderr, so a failing tool cannot deadlock behind a full error pipe.
    static func run(_ executable: String, _ arguments: [String], maximum: Int = 16384, timeout: TimeInterval = 15) throws -> Data {
        let process = Process(), pipe = Pipe()
        process.executableURL = URL(fileURLWithPath: executable); process.arguments = arguments
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
    init(_ url: URL, maximumSize: Int = 2 * 1024 * 1024 * 1024, allowNested: Bool = false) throws {
        guard url.isFileURL, url.pathExtension.lowercased() == "zip",
              let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize, size > 0, size <= maximumSize else { throw MacTab5LocalError.invalid("请选择有效的 AI-bot ZIP 包。") }
        self.url = url
        let listing = try MacTab5LocalProcess.run("/usr/bin/unzip", ["-Z1", url.path], maximum: 2 * 1024 * 1024)
        guard let text = String(data: listing, encoding: .utf8) else { throw MacTab5Error.invalidPacket }
        names = text.split(separator: "\n").map(String.init)
        guard !names.isEmpty, names.count <= 8192, Set(names.map { $0.lowercased() }).count == names.count,
              names.allSatisfy({ allowNested ? Self.safePath($0) : Self.safeName($0) }) else { throw MacTab5LocalError.invalid("ZIP 含重复或不安全的文件名。") }
    }
    static func safeName(_ name: String) -> Bool {
        !name.isEmpty && !name.contains("..") && name.range(of: "^[A-Za-z0-9][A-Za-z0-9_.-]*$", options: .regularExpression) != nil
    }
    static func safePath(_ name: String) -> Bool {
        let value = name.hasSuffix("/") ? String(name.dropLast()) : name
        let parts = value.split(separator: "/", omittingEmptySubsequences: false)
        return parts.count <= 8 && parts.allSatisfy { safeName(String($0)) }
    }
    func read(_ name: String, maximum: Int) throws -> Data {
        guard Self.safeName(name), names.contains(name) else { throw MacTab5LocalError.invalid("包内缺少必需文件。") }
        // Stream the exact validated entry; never extract paths or symlinks.
        return try MacTab5LocalProcess.run("/usr/bin/unzip", ["-p", url.path, name], maximum: maximum, timeout: 60)
    }
}
