import Foundation
import CryptoKit

struct MacTab5InstallPackage {
    static let flashSize = 0x1000000
    struct Segment: Codable { let name: String; let offset: Int; let size: Int; let sha256: String }
    struct Manifest: Codable { let format: Int; let kind: String; let chip: String; let project: String; let version: String; let flashBytes: Int; let imageSha256: String; let segments: [Segment] }
    let manifest: Manifest, image: Data
    static func load(_ url: URL) throws -> Self {
        let archive = try MacTab5Archive(url, maximumSize: 32 * 1024 * 1024, allowNested: true)
        let manifest = try JSONDecoder().decode(Manifest.self, from: archive.read("manifest.json", maximum: 16384))
        return try Self(manifest: manifest, image: archive.read("factory.bin", maximum: flashSize))
    }
    init(manifest: Manifest, image: Data) throws {
        guard manifest.format == 1, manifest.kind == "aibot-tab5-first-install", manifest.chip == "esp32p4", manifest.project == "aibot_tab5", manifest.flashBytes == Self.flashSize,
              image.count == Self.flashSize, MacTab5Protocol.sha256(image) == manifest.imageSha256, manifest.segments.count == 4 else { throw MacTab5LocalError.invalid("请选择完整 TAB5 首刷 ZIP，型号、大小和 SHA-256 必须匹配。") }
        let layout = [("bootloader.bin", 0x2000, 0x6000), ("partition-table.bin", 0x8000, 0x1000), ("otadata.bin", 0x10000, 0x2000), ("application.bin", 0x20000, 0x6e0000)]
        var end = 0
        for (index, part) in manifest.segments.enumerated() {
            let expected = layout[index]
            guard part.name == expected.0, part.offset == expected.1, part.size > 0, part.size <= expected.2,
                  image[end..<part.offset].allSatisfy({ $0 == 255 }) else { throw MacTab5LocalError.invalid("首刷包布局错误，或含未声明的设备数据。") }
            let bytes = Data(image[part.offset..<part.offset + part.size])
            guard MacTab5Protocol.sha256(bytes) == part.sha256 else { throw MacTab5Error.invalidPacket }
            if index == 0 || index == 3 { try MacTab5FirmwareImage.validateESPImage(bytes) }
            if index == 1 { try Self.partitions(bytes) }
            if index == 2 { guard part.size == 0x2000, bytes.allSatisfy({ $0 == 255 }) else { throw MacTab5Error.invalidPacket } }
            if index == 3 { guard try MacTab5FirmwareImage(bytes: bytes).version == manifest.version else { throw MacTab5Error.invalidPacket } }
            end = part.offset + part.size
        }
        guard image[end...].allSatisfy({ $0 == 255 }) else { throw MacTab5LocalError.invalid("首刷包包含 NVS、备用分区或私有设备数据。") }
        self.manifest = manifest; self.image = image
    }
    static func partitions(_ bytes: Data) throws {
        let expected: [(String, UInt8, UInt8, UInt32, UInt32)] = [("nvs",1,2,0x9000,0x6000),("phy_init",1,1,0xf000,0x1000),("otadata",1,0,0x10000,0x2000),("ota_0",0,0x10,0x20000,0x6e0000),("ota_1",0,0x11,0x700000,0x6e0000),("assets",1,0x40,0xe00000,0x200000)]
        guard bytes.count >= 224 else { throw MacTab5Error.invalidPacket }
        for (index, row) in expected.enumerated() {
            let at = index * 32
            guard MacTab5Mailbox.u16(bytes, at) == 0x50aa, bytes[at + 2] == row.1, bytes[at + 3] == row.2, MacTab5Mailbox.u32(bytes, at + 4) == row.3, MacTab5Mailbox.u32(bytes, at + 8) == row.4,
                  try MacTab5FirmwareImage.text(bytes, at + 12, 16) == row.0, MacTab5Mailbox.u32(bytes, at + 28) == 0 else { throw MacTab5Error.invalidPacket }
        }
        guard MacTab5Mailbox.u16(bytes, 192) == 0xebeb, bytes[194..<208].allSatisfy({ $0 == 255 }), Data(Insecure.MD5.hash(data: bytes.prefix(192))) == Data(bytes[208..<224]), bytes[224...].allSatisfy({ $0 == 255 }) else { throw MacTab5Error.invalidPacket }
    }
}

struct MacTab5FlashBackup: Codable {
    let format: Int, chip: String, mac: String, flashBytes: Int, sha256: String
    static func read(_ metadata: URL) throws -> (MacTab5FlashBackup, Data) {
        guard metadata.path.hasSuffix(".bin.json"), (try metadata.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 4097) <= 4096 else { throw MacTab5Error.invalidPacket }
        // Windows writes PascalCase; both platforms read this portable sidecar.
        let raw = try MacTab5Protocol.object(Data(contentsOf: metadata)); var normalized: [String: Any] = [:]
        for (key, value) in raw { let name = key.prefix(1).lowercased() + key.dropFirst(); guard normalized[name] == nil else { throw MacTab5Error.invalidPacket }; normalized[name] = value }
        let record = try JSONDecoder().decode(Self.self, from: MacTab5Protocol.json(normalized))
        let imageURL = metadata.deletingPathExtension()
        guard record.format == 1, record.chip == "esp32p4", record.flashBytes == MacTab5InstallPackage.flashSize, record.mac.range(of: "^[0-9a-f]{2}(:[0-9a-f]{2}){5}$", options: .regularExpression) != nil,
              try imageURL.resourceValues(forKeys: [.fileSizeKey]).fileSize == record.flashBytes else { throw MacTab5Error.invalidPacket }
        let bytes = try Data(contentsOf: imageURL); guard MacTab5Protocol.sha256(bytes) == record.sha256 else { throw MacTab5Error.invalidPacket }; return (record, bytes)
    }
}

final class MacTab5Installer {
    typealias Runner = ([String]) throws -> String
    private let run: Runner, stage: (String) -> Void
    private(set) var backup: URL?
    init(run: @escaping Runner, stage: @escaping (String) -> Void = { _ in }) { self.run = run; self.stage = stage }
    private func command(_ port: String, _ arguments: [String]) throws -> String { try run(["--chip", "esp32p4", "--port", port, "--baud", "460800", "--before", "no_reset", "--after", "no_reset"] + arguments) }
    private func match(_ text: String, _ pattern: String) throws -> String {
        let regex = try NSRegularExpression(pattern: pattern, options: [.caseInsensitive, .anchorsMatchLines]), source = text as NSString
        guard let result = regex.firstMatch(in: text, range: NSRange(location: 0, length: source.length)), result.numberOfRanges > 1 else { throw MacTab5LocalError.invalid("无法确认芯片、安全状态或硬件身份，未写入设备。") }; return source.substring(with: result.range(at: 1)).lowercased()
    }
    private func mac(_ port: String) throws -> String { try match(command(port, ["read_mac"]), "^MAC:\\s*([0-9a-f]{2}(?::[0-9a-f]{2}){5})\\s*$") }
    private func identify(_ port: String) throws -> (String, Int) {
        guard port.range(of: "^/dev/cu\\.[A-Za-z0-9_.-]+$", options: .regularExpression) != nil else { throw MacTab5Error.serial }
        stage("正在核对 ESP32-P4、16 MiB 容量与安全状态")
        let info = try command(port, ["flash_id"])
        guard info.contains("Chip is ESP32-P4"), try MacFirmwareFlasher.flashSize(info) == MacTab5InstallPackage.flashSize else { throw MacTab5Error.unsupported }
        let major = try match(info, "Chip is ESP32-P4.*revision v(\\d+)\\.\\d+"), minor = try match(info, "Chip is ESP32-P4.*revision v\\d+\\.(\\d+)")
        let security = try command(port, ["get_security_info"])
        _ = try match(security, "^Secure Boot:\\s*(Disabled)\\s*$"); _ = try match(security, "^Flash Encryption:\\s*(Disabled)\\s*$")
        return (try mac(port), (Int(major) ?? 9999) * 100 + (Int(minor) ?? 9999))
    }
    private func backup(_ port: String, mac: String, directory: URL) throws -> Data {
        stage("正在备份原机 16 MiB Flash，请保持连接")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let path = directory.appendingPathComponent("tab5-" + UUID().uuidString.lowercased() + ".bin"); backup = path
        _ = try command(port, ["read_flash", "0x0", "0x1000000", path.path])
        guard try path.resourceValues(forKeys: [.fileSizeKey]).fileSize == MacTab5InstallPackage.flashSize else { throw MacTab5LocalError.invalid("备份不完整，未写入新固件。") }
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: path.path)
        _ = try command(port, ["verify_flash", "0x0", path.path])
        let bytes = try Data(contentsOf: path), record = MacTab5FlashBackup(format: 1, chip: "esp32p4", mac: mac, flashBytes: bytes.count, sha256: MacTab5Protocol.sha256(bytes))
        try JSONEncoder().encode(record).write(to: path.appendingPathExtension("json"), options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: path.appendingPathExtension("json").path)
        return bytes
    }
    @discardableResult
    func execute(port: String, package: MacTab5InstallPackage? = nil, restoring: URL? = nil, directory: URL, work: URL) throws -> URL {
        guard package == nil || restoring == nil else { throw MacTab5Error.invalidPacket }
        let restored = try restoring.map(MacTab5FlashBackup.read)
        let (identity, revision) = try identify(port)
        if let restored, restored.0.mac != identity { throw MacTab5LocalError.invalid("备份属于另一台 TAB5，拒绝恢复。") }
        if let package {
            for part in package.manifest.segments where part.name == "bootloader.bin" || part.name == "application.bin" {
                let bytes = Data(package.image[part.offset..<part.offset + part.size]), minimum = max(Int(bytes[14]) * 100, Int(MacTab5Mailbox.u16(bytes, 15))), maximum = Int(MacTab5Mailbox.u16(bytes, 17))
                guard revision >= minimum, maximum == 0 || revision <= maximum else { throw MacTab5Error.unsupported }
            }
        }
        let previous = try backup(port, mac: identity, directory: directory)
        if let package {
            guard ![0x10000, 0x20000, 0x700000].contains(where: { Data(previous[$0 + 80..<$0 + 91]) == Data("aibot_tab5\0".utf8) }) else { throw MacTab5LocalError.invalid("已安装 AI-bot，请使用升级流程。原机备份已保留。") }
            try write(port, identity: identity, bytes: package.image, sha: package.manifest.imageSha256, work: work)
        } else if let restored { try write(port, identity: identity, bytes: restored.1, sha: restored.0.sha256, work: work) }
        return backup!
    }
    private func write(_ port: String, identity: String, bytes: Data, sha: String, work: URL) throws {
        try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let path = work.appendingPathComponent(UUID().uuidString + ".bin"); try bytes.write(to: path, options: .withoutOverwriting)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: path.path)
        defer { try? FileManager.default.removeItem(at: path) }
        guard try mac(port) == identity, MacTab5Protocol.sha256(try Data(contentsOf: path)) == sha else { throw MacTab5Error.identity }
        stage("正在写入；此步骤不自动重试，请勿拔线")
        _ = try command(port, ["write_flash", "--flash_mode", "keep", "--flash_freq", "keep", "--flash_size", "keep", "0x0", path.path])
        stage("正在核验 Flash 写入内容")
        _ = try command(port, ["verify_flash", "0x0", path.path])
    }
}
