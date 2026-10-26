import CryptoKit
import Foundation

struct MacTab5FirmwareImage {
    static let maximum = 0x6e0000
    let bytes: Data
    let version: String
    let sha256: String
    let elfSHA256: String
    let notes: String
    let offerID = MacTab5Protocol.identifier()
    var offer: [String: Any] { ["sha256": sha256, "size": bytes.count, "version": version, "offerId": offerID, "notes": notes] }

    init(bytes: Data, sidecar: Data? = nil) throws {
        guard (1024...Self.maximum).contains(bytes.count) else { throw MacTab5LocalError.invalid("TAB5 应用镜像大小无效。") }
        try Self.validateESPImage(bytes)
        guard MacTab5Mailbox.u32(bytes, 28) >= 256, MacTab5Mailbox.u32(bytes, 32) == 0xabcd5432,
              try Self.text(bytes, 80, 32) == "aibot_tab5" else { throw MacTab5LocalError.invalid("请选择 AI-bot TAB5 应用镜像。") }
        self.bytes = bytes; version = try Self.text(bytes, 48, 32); sha256 = MacTab5Protocol.sha256(bytes)
        elfSHA256 = bytes[176..<208].map { String(format: "%02x", $0) }.joined()
        var notes = ""
        if let sidecar {
            guard sidecar.count <= 8192 else { throw MacTab5Error.invalidPacket }
            let root = try MacTab5Protocol.object(sidecar)
            guard root["version"] as? String == version, root["sha256"] as? String == sha256,
                  let text = root["notes"] as? String else { throw MacTab5LocalError.invalid("更新说明与固件版本或 SHA-256 不一致。") }
            notes = text.replacingOccurrences(of: "\r\n", with: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
            guard notes.utf8.count <= 1024, notes.unicodeScalars.allSatisfy({ $0.value == 10 || !CharacterSet.controlCharacters.contains($0) }) else { throw MacTab5LocalError.invalid("更新说明格式或长度无效。") }
        }
        self.notes = notes
    }
    static func load(_ url: URL) throws -> Self {
        if url.pathExtension.lowercased() == "zip" {
            let archive = try MacTab5Archive(url, maximumSize: 32 * 1024 * 1024, allowNested: true)
            if archive.names.contains("factory.bin"), archive.names.contains("manifest.json") {
                let package = try MacTab5InstallPackage.load(url), part = package.manifest.segments[3]
                return try Self(bytes: Data(package.image[part.offset..<part.offset + part.size]))
            }
            let bytes = try archive.read("aibot_tab5.bin", maximum: maximum)
            let sidecar = archive.names.contains("aibot_tab5.bin.notes.json") ? try archive.read("aibot_tab5.bin.notes.json", maximum: 8192) : nil
            return try Self(bytes: bytes, sidecar: sidecar)
        }
        guard let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize, (1024...maximum).contains(size) else { throw MacTab5Error.invalidPacket }
        let notesURL = URL(fileURLWithPath: url.path + ".notes.json")
        let sidecar: Data?
        if FileManager.default.fileExists(atPath: notesURL.path) {
            guard (try notesURL.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 8193) <= 8192 else { throw MacTab5Error.invalidPacket }
            sidecar = try Data(contentsOf: notesURL)
        } else { sidecar = nil }
        return try Self(bytes: Data(contentsOf: url), sidecar: sidecar)
    }
    static func text(_ data: Data, _ offset: Int, _ count: Int) throws -> String {
        guard offset >= 0, offset + count <= data.count else { throw MacTab5Error.invalidPacket }
        let bytes = data[offset..<offset + count]
        guard let end = bytes.firstIndex(of: 0), end > offset,
              bytes[..<end].allSatisfy({ (32...126).contains($0) }) else { throw MacTab5Error.invalidPacket }
        return String(decoding: bytes[..<end], as: UTF8.self)
    }
    static func validateESPImage(_ bytes: Data) throws {
        guard bytes.count >= 48, bytes[0] == 0xe9, (1...16).contains(Int(bytes[1])), MacTab5Mailbox.u16(bytes, 12) == 18 else { throw MacTab5LocalError.invalid("镜像不是 ESP32-P4 固件。") }
        var offset = 24, checksum: UInt8 = 0xef
        for _ in 0..<bytes[1] {
            guard offset <= bytes.count - 8 else { throw MacTab5Error.invalidPacket }
            let size = Int(MacTab5Mailbox.u32(bytes, offset + 4)); offset += 8
            guard size <= bytes.count - offset else { throw MacTab5Error.invalidPacket }
            for byte in bytes[offset..<offset + size] { checksum ^= byte }; offset += size
        }
        let end = (offset | 15) + 1
        guard end < bytes.count, bytes[end - 1] == checksum, bytes[23] == 1, bytes.count == end + 32,
              Data(SHA256.hash(data: bytes.prefix(end))) == Data(bytes.suffix(32)) else { throw MacTab5LocalError.invalid("镜像段、校验和或附加 SHA-256 无效。") }
    }
}

final class MacTab5Firmware {
    private let lock = NSLock()
    private var image: MacTab5FirmwareImage?
    private var remote: (id: String, device: String, expires: Int64, started: Bool)?
    private var probe: (id: String, device: String, transport: String, expires: Int64, previous: Int64, count: Int)?
    private var leaseUntil: TimeInterval = 0
    private var message = "尚未选择固件"
    private var matchedVersion = false
    private let epoch: () -> Int64, tick: () -> TimeInterval
    init(epoch: @escaping () -> Int64 = {Int64(Date().timeIntervalSince1970*1000)},
         tick: @escaping () -> TimeInterval = {ProcessInfo.processInfo.systemUptime}) {
        self.epoch = epoch;self.tick = tick
    }
    var summary: String { lock.lock(); defer { lock.unlock() }; return message }
    var selected: MacTab5FirmwareImage? { lock.lock(); defer { lock.unlock() }; return image }
    var busy: Bool { lock.lock(); defer { lock.unlock() }; return tick() < leaseUntil || remote != nil && (remote?.expires ?? 0) > epoch() || probe != nil && (probe?.expires ?? 0) > epoch() }
    func select(_ value: MacTab5FirmwareImage?) throws {
        lock.lock(); defer { lock.unlock() }
        guard tick() >= leaseUntil, remote == nil || (remote?.expires ?? 0) < epoch(),
              probe == nil || (probe?.expires ?? 0) < epoch() else { throw MacTab5Error.busy }
        image = value; remote = nil; probe = nil; matchedVersion = false; message = value.map { "已选择 \($0.version)，尚未开始升级\nSHA-256：\($0.sha256)\n\($0.notes)" } ?? "尚未选择固件"
    }
    func start(device: String) throws {
        lock.lock(); defer { lock.unlock() }
        guard image != nil, MacTab5Protocol.isHex(device, count: 12), tick() >= leaseUntil,
              remote == nil || (remote?.expires ?? 0) < epoch() else { throw MacTab5Error.busy }
        remote = (MacTab5Protocol.identifier(), device, epoch() + 30000, false)
        matchedVersion = false
        message = "正在请求 TAB5 保存草稿并开始升级；尚未确认。"
    }
    func beginBootCheck(device: String, transport: String) throws {
        lock.lock(); defer { lock.unlock() }
        guard image != nil, ["USB", "Wi-Fi", "BLE"].contains(transport), tick() >= leaseUntil else { throw MacTab5Error.busy }
        probe = (MacTab5Protocol.identifier(), device, transport, epoch() + 10000, -1, 0)
        message = "正在核验精确固件、运行分区及三个实时界面样本。"
    }
    func observeVersion(_ version: String) {
        lock.lock(); defer { lock.unlock() }
        if version == image?.version {
            let pending = remote != nil || leaseUntil > tick() || !matchedVersion
            leaseUntil = 0;remote = nil;matchedVersion = true
            if pending { message = "设备已运行 \(version)，等待启动核验。" }
        }
    }
    func completeUSBCheck(hellos: [[String: Any]], diagnostic: [String: Any], device: String) throws {
        lock.lock(); defer { lock.unlock() }
        guard let image, hellos.count == 3, probe?.device == device, probe?.transport == "USB" else { throw MacTab5Error.unconfirmed }
        var previous: Int64 = -1
        do { for hello in hellos { previous = try Self.validateBoot(hello: hello, diagnostic: diagnostic, image: image, device: device, previous: previous) } }
        catch { probe = nil; message = "USB 启动核验失败，未确认成功。"; throw error }
        probe = nil; leaseUntil = 0; remote = nil; matchedVersion = true; message = "USB 启动核验通过：精确 ELF、VALID 分区和三个实时界面样本一致。"
    }
    private func advertisedExpiry(_ deadline: Int64, frame: Int64?, bound: Int64) -> Int64? {
        guard deadline > epoch() else { return nil }
        guard let frame else { return deadline }
        guard frame > 0, frame <= Int64.max-bound else { return nil }
        let expires = min(deadline,frame+bound)
        return expires > epoch() && expires >= frame ? expires:nil
    }
    func projection(frameEpochMilliseconds: Int64? = nil) -> (offer: [String: Any]?, health: [String: Any]) {
        lock.lock(); defer { lock.unlock() }
        var health: [String: Any] = [:]
        if let r = remote, let image {
            if r.expires < epoch() {
                remote = nil
                if !r.started && tick() >= leaseUntil { message = "升级请求未确认，请核对设备后重试；不会自动重发。" }
            } else if !r.started, let expires = advertisedExpiry(r.expires,frame:frameEpochMilliseconds,bound:45000) {
                health["otaCommand"] = ["version":1,"requestId":r.id,"offerId":image.offerID,"sha256":image.sha256,"expiresAt":expires]
            }
        }
        if let p = probe {
            if p.expires < epoch() { probe = nil; message = "启动核验超时，尚未确认成功。" }
            else if let expires = advertisedExpiry(p.expires,frame:frameEpochMilliseconds,bound:15000) {
                health["bootVerifyCommand"] = ["version":1,"requestId":p.id,"transport":p.transport,"expiresAt":expires]
            }
        }
        return (image?.offer, health)
    }
    func handle(_ root: [String: Any], device: String, transport: String, binary: Bool, bulk: Bool) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        guard let image else { return .error(404, "offer_changed") }
        switch root["kind"] as? String {
        case "ota", "ota_probe":
            if root["kind"] as? String == "ota_probe", transport != "BLE" { return .error(400, "probe_requires_ble") }
            guard root["offerId"] as? String == image.offerID, root["sha256"] as? String == image.sha256,
                  let offset = MacTab5Protocol.integer(root["offset"]), let count = MacTab5Protocol.integer(root["count"]),
                  offset >= 0, count > 0, count <= (bulk ? 49152 : transport == "USB" ? 16384 : 8192), offset <= Int64(image.bytes.count) - count else { return .error(400, "invalid_range") }
            if root["kind"] as? String != "ota_probe" {
                if var r = remote { r.started = true;remote = r }
                leaseUntil = tick()+90;message = "已向设备提供 \(offset + count)/\(image.bytes.count) 字节，等待设备写入、校验和启动确认。"
            }
            return .init(200, ["offset": offset], bytes: Data(image.bytes[Int(offset)..<Int(offset + count)]))
        case "ota-control":
            guard var r = remote, r.device == device, root["requestId"] as? String == r.id, root["offerId"] as? String == image.offerID,
                  root["sha256"] as? String == image.sha256, r.expires >= epoch() else { return .error(409, "request_expired") }
            switch root["stage"] as? String {
            case "ready": leaseUntil = tick() + 40; message = "设备已确认保存草稿，等待开始。"; return .init(200, ["commit": true])
            case "started": r.started = true; remote = r; leaseUntil = tick() + 30; message = "设备已确认开始升级，请保持供电；启动尚未核验。"; return .init(200, ["accepted": true])
            case "rejected":
                remote = nil;leaseUntil = 0
                let reasons = ["device_busy":"设备正忙，请先结束录音、拍照或网络操作。", "draft_pending":"还有待确认或待同步的草稿，请先处理送达与同步状态。", "draft_save_failed":"设备草稿保存失败，升级尚未开始。", "no_link":"设备没有可用的升级通道，请检查连接模式与连接状态。", "offer_changed":"设备上的固件提供已变化，请重新选择候选。", "request_failed":"设备未能启动固件更新，请检查设备提示。"]
                message = (root["error"] as? String).flatMap { reasons[$0] } ?? "设备拒绝开始升级，请检查设备提示；原固件仍保留。"
                return .init(200,["accepted":true])
            default: return .error(400, "invalid_stage")
            }
        case "boot-verify":
            guard var p = probe, p.device == device, p.transport == transport, p.expires >= epoch(),
                  root["requestId"] as? String == p.id, let hello = root["hello"] as? [String: Any], let diagnostic = root["diagnostic"] as? [String: Any] else { return .error(409, "boot_probe_expired") }
            do { p.previous = try Self.validateBoot(hello: hello, diagnostic: diagnostic, image: image, device: device, previous: p.previous) }
            catch { probe = nil; message = "启动核验失败，设备身份、运行镜像或界面状态不符。"; return .error(400, "boot_verification_failed") }
            p.count += 1
            if p.count == 3 { probe = nil; remote = nil; matchedVersion = true; message = "启动核验通过：\(image.version)，精确 ELF、VALID 分区、三个实时样本一致。" }
            else { p.id = MacTab5Protocol.identifier(); p.expires = epoch() + 10000; probe = p; message = "启动核验：\(p.count)/3 个实时样本通过。" }
            return .init(200, ["accepted": true])
        default: return .error(400, "invalid_operation")
        }
    }
    func bytes(sha: String) -> Data? {
        lock.lock(); defer { lock.unlock() }
        guard let image, image.sha256 == sha else { return nil }
        if var r = remote { r.started = true;remote = r }
        leaseUntil = tick()+120;message = "正在向 TAB5 提供固件，等待设备写入和启动核验。";return image.bytes
    }
    static func validateBoot(hello: [String: Any], diagnostic: [String: Any], image: MacTab5FirmwareImage, device: String, previous: Int64) throws -> Int64 {
        guard hello["type"] as? String == "tab5_hello", MacTab5Protocol.integer(hello["version"]) == 1,
              hello["deviceId"] as? String == device, hello["firmware"] as? String == image.version,
              let uptime = MacTab5Protocol.integer(hello["uptimeMs"]), uptime > previous,
              let age = MacTab5Protocol.integer(hello["uiAgeMs"]), (0...2000).contains(age),
              MacTab5Protocol.integer(hello["flushErrors"]) == 0, (MacTab5Protocol.integer(hello["flushCount"]) ?? 0) > 0,
              diagnostic["type"] as? String == "tab5_ota_diagnostic", diagnostic["firmware"] as? String == image.version,
              diagnostic["elfSha256"] as? String == image.elfSHA256, MacTab5Protocol.integer(diagnostic["stateError"]) == 0, MacTab5Protocol.integer(diagnostic["state"]) == 2,
              (diagnostic["partition"] as? String == "ota_0" && MacTab5Protocol.integer(diagnostic["address"]) == 0x20000 || diagnostic["partition"] as? String == "ota_1" && MacTab5Protocol.integer(diagnostic["address"]) == 0x700000) else { throw MacTab5Error.unconfirmed }
        return uptime
    }
}
