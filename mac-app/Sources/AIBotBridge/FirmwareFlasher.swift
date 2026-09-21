import Foundation
import CryptoKit

enum FlashError: LocalizedError {
    case failed(String)
    var errorDescription: String? { if case .failed(let message) = self { return message }; return nil }
}

/// All blocking work runs off the main thread. Only official, hash-pinned tools are executed.
final class MacFirmwareFlasher {
    static let toolURL = URL(string: "https://github.com/espressif/esptool/releases/download/v4.9.1/esptool-v4.9.1-macos-arm64.tar.gz")!
    static let toolHash = "13d9e4667a0a61096d4f6650e87cf4d9d99b4d66bdcc58dace2a127016c38e49"
    static var root: URL { FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("AI-bot") }
    static var backupDirectory: URL { root.appendingPathComponent("device-backups") }
    typealias Runner = ([String]) throws -> String
    private let run: Runner
    private let stage: (String) -> Void
    init(run: @escaping Runner, stage: @escaping (String) -> Void) { self.run = run; self.stage = stage }

    static func hash(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }

    struct ToolInstall {
        let executable: URL
        let folder: URL
        func cleanup() { try? FileManager.default.removeItem(at: folder) }
    }
    static func prepareTool(stage: (String) -> Void) throws -> ToolInstall {
        let cache = root.appendingPathComponent("flash-tools")
        try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
        guard let resources = Bundle.main.resourceURL else { throw FlashError.failed("安装文件不完整，请重新安装完整版刷机工具。") }
        let archive = resources.appendingPathComponent("flash-tools/esptool-4.9.1.tar.gz")
        stage("正在准备…")
        guard (try? hash(Data(contentsOf: archive))) == toolHash else {
            throw FlashError.failed("安装文件缺失或校验失败，请重新安装完整版刷机工具。")
        }
        let folder = cache.appendingPathComponent("run-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        do {
            _ = try process("/usr/bin/tar", ["-xzf", archive.path, "-C", folder.path])
            let files = FileManager.default.enumerator(at: folder, includingPropertiesForKeys: nil)?.allObjects as? [URL] ?? []
            guard let tool = files.first(where: { $0.lastPathComponent == "esptool" && FileManager.default.isExecutableFile(atPath: $0.path) }) else {
                throw FlashError.failed("刷机工具不完整，请重试。")
            }
            return ToolInstall(executable: tool, folder: folder)
        } catch { try? FileManager.default.removeItem(at: folder); throw error }
    }

    static func prepareFirmware(_ selected: URL, at folder: URL) throws -> URL {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let firmware = folder.appendingPathComponent("firmware.bin")
        let bytes: Data
        if selected.pathExtension.lowercased() == "zip" {
            bytes = try processData("/usr/bin/unzip", ["-p", selected.path, "firmware.bin"], maxBytes: 4 * 1024 * 1024)
        } else {
            let size = try selected.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard (1024...4 * 1024 * 1024).contains(size) else { throw FlashError.failed("固件大小异常。") }
            bytes = try Data(contentsOf: selected)
        }
        guard (1024...4 * 1024 * 1024).contains(bytes.count), bytes.first == 0xE9 else {
            throw FlashError.failed("请选择本项目的固件材料 ZIP 或 firmware.bin。")
        }
        try bytes.write(to: firmware, options: .atomic)
        return firmware
    }

    static func flashSize(_ text: String) throws -> Int {
        let regex = try NSRegularExpression(pattern: "Detected flash size:\\s*(\\d+)\\s*(KB|MB)", options: .caseInsensitive)
        let value = text as NSString
        guard let match = regex.firstMatch(in: text, range: NSRange(location: 0, length: value.length)),
              let number = Int(value.substring(with: match.range(at: 1))) else { throw FlashError.failed("未能确认 Flash 容量，已停止。") }
        let size = number * (value.substring(with: match.range(at: 2)).uppercased() == "MB" ? 1024 * 1024 : 1024)
        guard (256 * 1024...16 * 1024 * 1024).contains(size) else { throw FlashError.failed("不支持的 Flash 容量。") }
        return size
    }

    func execute(port: String, firmware: URL?, backupDirectory: URL = MacFirmwareFlasher.backupDirectory) throws -> URL {
        guard port.hasPrefix("/dev/cu."), !port.contains(".."), !port.contains("\n") else { throw FlashError.failed("请选择设备串口。") }
        var baud = "460800"
        func command(_ arguments: [String]) -> [String] { ["--chip", "esp8266", "--port", port, "--baud", baud] + arguments }
        func readWithFallback(_ arguments: [String], message: String) throws -> String {
            stage(message)
            do { return try run(command(arguments)) }
            catch {
                guard baud == "460800" else { throw error }
                baud = "115200"; stage("正在以兼容速度重试，" + message)
                return try run(command(arguments))
            }
        }
        if let firmware {
            stage("正在检查固件…")
            _ = try run(["--chip", "esp8266", "image_info", firmware.path])
        }
        let size = try Self.flashSize(readWithFallback(["flash_id"], message: "正在检查设备…"))
        if let firmware, (try firmware.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? Int.max) > size { throw FlashError.failed("固件超过设备存储容量，已停止。") }
        try FileManager.default.createDirectory(at: backupDirectory, withIntermediateDirectories: true)
        let backup = backupDirectory.appendingPathComponent("backup-\(Int(Date().timeIntervalSince1970))-\(UUID().uuidString).bin")
        _ = try readWithFallback(["read_flash", "0", String(size), backup.path], message: "正在备份，请保持连接…")
        guard (try? backup.resourceValues(forKeys: [.fileSizeKey]).fileSize) == size else { throw FlashError.failed("备份不完整，已停止，未写入新固件。") }
        let digest = try Self.hash(Data(contentsOf: backup))
        try (digest + "  " + backup.lastPathComponent + "\n").write(to: backup.appendingPathExtension("sha256"), atomically: true, encoding: .utf8)
        if let firmware {
            stage("正在刷机，请勿拔线…")
            _ = try run(command(["write_flash", "0x0", firmware.path]))
            stage("即将完成，请保持连接…")
            _ = try run(command(["verify_flash", "0x0", firmware.path]))
        }
        return backup
    }

    @discardableResult
    static func process(_ executable: String, _ arguments: [String], log: @escaping (String) -> Void = { _ in }) throws -> String {
        String(decoding: try processData(executable, arguments, log: log), as: UTF8.self)
    }

    private static func processData(_ executable: String, _ arguments: [String], maxBytes: Int = 8 * 1024 * 1024,
                                    log: @escaping (String) -> Void = { _ in }) throws -> Data {
        let process = Process(), pipe = Pipe()
        process.executableURL = URL(fileURLWithPath: executable); process.arguments = arguments
        process.standardOutput = pipe; process.standardError = pipe
        try process.run()
        let readState = NSLock()
        var lastReadOutput = ProcessInfo.processInfo.systemUptime
        let readWatchdog = DispatchSource.makeTimerSource(queue: .global())
        if arguments.contains("read_flash") {
            readWatchdog.schedule(deadline: .now() + 30, repeating: 1)
            readWatchdog.setEventHandler {
                readState.lock(); let idle = ProcessInfo.processInfo.systemUptime - lastReadOutput; readState.unlock()
                if idle >= 30 && process.isRunning { process.terminate() }
            }
        }
        readWatchdog.resume()
        defer { readWatchdog.cancel() }
        let timeout = DispatchWorkItem { if process.isRunning { process.terminate() } }
        DispatchQueue.global().asyncAfter(deadline: .now() + 900, execute: timeout)
        defer { timeout.cancel() }
        var data = Data()
        while true {
            let chunk = pipe.fileHandleForReading.availableData
            if chunk.isEmpty { break }
            readState.lock(); lastReadOutput = ProcessInfo.processInfo.systemUptime; readState.unlock()
            if data.count + chunk.count > maxBytes {
                process.terminate(); process.waitUntilExit()
                throw FlashError.failed("工具输出或固件大小异常，已停止。")
            }
            data.append(chunk)
            if !arguments.contains("-p") { log(String(decoding: chunk, as: UTF8.self)) }
        }
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw FlashError.failed("工具执行失败。请查看详细记录，检查网络、数据线、端口占用和设备型号后重试。") }
        return data
    }
}
