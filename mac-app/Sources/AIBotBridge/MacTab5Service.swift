import Foundation
import CryptoKit

final class MacTab5Service {
    typealias DisplayCompletion = (Result<MacTab5DisplaySettings, Error>) -> Void
    private let lock = NSLock()
    private let loadPairing: () throws -> MacTab5Pairing?
    private let savePairing: (MacTab5Pairing) throws -> Void
    private let host: () -> String?
    private let latestMusic: () -> (snapshot: MusicSnapshot?, cover: Data?)?
    let port: UInt16
    let session = MacTab5Protocol.identifier()
    private var pairing: MacTab5Pairing?
    private var pairingReadable = true
    private var snapshot: MacStatusSnapshot?
    private var sequence: Int64 = 0
    private var musicFrames = 0, unavailableMusicFrames = 0
    private var maximumMusicAge: TimeInterval = 0
    private var lanReady = false
    private var stopped = false
    private var maintenance = false
    private var lastUSB: TimeInterval = -.infinity
    private var lastWiFi: TimeInterval = -.infinity
    private var lastBLE: TimeInterval = -.infinity
    private var bleNote = "尚未启用"
    private var firmware: String?
    private var usbNote = "尚未连接"
    private var discoveryNote = "等待启动"
    private var nonces: [String: TimeInterval] = [:]
    private let display = MacTab5DisplayTransaction()
    let assets = MacTab5Assets()
    let gallery: MacTab5Gallery
    let computer: MacTab5Computer
    let codex: MacTab5Codex
    let voice: MacTab5Voice
    let desktop: MacTab5Desktop
    let firmwareControl = MacTab5Firmware()
    private var activeOperations = 0
    private var displayCompletion: DisplayCompletion?

    init(port: UInt16, host: @escaping () -> String? = LocalNetworkIdentity.privateIPv4, gallery: MacTab5Gallery = MacTab5Gallery(), computer: MacTab5Computer = MacTab5Computer(), codex: MacTab5Codex = MacTab5Codex(), voice: MacTab5Voice = MacTab5Voice(),
         music: @escaping () -> (snapshot: MusicSnapshot?, cover: Data?)? = { nil },
         load: @escaping () throws -> MacTab5Pairing? = { try MacTab5PairingStore().load() },
         save: @escaping (MacTab5Pairing) throws -> Void = { try MacTab5PairingStore().save($0) }) {
        self.port = port; self.host = host; self.latestMusic = music; self.gallery = gallery; self.computer = computer; self.codex = codex; self.voice = voice; loadPairing = load; savePairing = save
        desktop = MacTab5Desktop(codex: codex)
        do { pairing = try load(); try pairing?.validate() }
        catch { pairingReadable = false; usbNote = "无法读取配对记录，请检查钥匙串权限" }
    }

    var currentPairing: MacTab5Pairing? { lock.lock(); defer { lock.unlock() }; return pairing }
    var currentMusic: MusicSnapshot? { latestMusic()?.snapshot }
    var musicDiagnostics: String {
        lock.lock(); defer { lock.unlock() }
        guard let sample = latestMusic()?.snapshot, !sample.title.isEmpty else { return "音乐：无活动曲目" }
        let age = Date().timeIntervalSince(sample.updatedAt)
        return String(format: "音乐采样：%.2f 秒前 · 生成 %d 帧 · 进度不可用 %d 帧 · 最大样本年龄 %.2f 秒",
                      max(0, age), musicFrames, unavailableMusicFrames, maximumMusicAge)
    }
    func setMaintenance(_ value: Bool) -> Bool {
        lock.lock()
        if value && (activeOperations > 0 || displayCompletion != nil || firmwareControl.busy) { lock.unlock(); return false }
        maintenance = value; lock.unlock(); if value { voice.stop() }; return true
    }
    var usbActive: Bool { lock.lock(); defer { lock.unlock() }; return ProcessInfo.processInfo.systemUptime - lastUSB < 8 }
    var configurationBusy: Bool { lock.lock(); defer { lock.unlock() }; return displayCompletion != nil || activeOperations > 0 || firmwareControl.busy }
    var bootTransport: String? {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        return now - lastWiFi < 8 ? "Wi-Fi" : now - lastBLE < 8 ? "BLE" : nil
    }
    func requestUpgrade() throws {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        guard !stopped, !maintenance, activeOperations == 0, now - lastUSB < 8 || now - lastWiFi < 8 || now - lastBLE < 8, let pair = pairing, pair.confirmed, let version = firmware, let image = firmwareControl.selected,
              version != image.version, displayCompletion == nil else { throw MacTab5LocalError.invalid("请先连接 TAB5、选择不同版本固件并完成当前操作。") }
        let parts = version.replacingOccurrences(of: "-ui", with: "").split(separator: ".").compactMap { Int($0) }
        guard parts.count == 3, parts.lexicographicallyPrecedes([0, 2, 190]) == false else { throw MacTab5LocalError.invalid("此设备需先在 TAB5 上确认一次升级，之后才支持电脑端开始升级。") }
        try firmwareControl.start(device: pair.deviceId)
    }
    var connectionSummary: String {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        let device = pairing.map { "TAB5 · \($0.deviceId)" } ?? "尚未配对 TAB5"
        let usb = now - lastUSB < 8 ? "数据已确认" : usbNote
        let wifi = !lanReady ? "LAN 服务未就绪" : now - lastWiFi < 8 ? "收到已认证请求" : "等待设备连接"
        let confirmation = pairing?.confirmed == false ? "\nUSB 配对结果未确认，请重新配对。" : ""
        return "\(device)\nUSB：\(usb)\nWi-Fi：\(wifi)\n蓝牙：\(bleNote)\n自动发现：\(discoveryNote)\(confirmation)"
    }
    var lanConfiguration: (String, UInt16)? {
        lock.lock(); defer { lock.unlock() }
        guard lanReady, let address = host(), PrivateIPv4Address.isValid(address) else { return nil }
        return (address, port)
    }
    func setLANReady(_ ready: Bool) { lock.lock(); lanReady = ready && !stopped; if !lanReady { lastWiFi = -.infinity }; lock.unlock() }
    func setDiscoveryState(_ note: String) { lock.lock(); discoveryNote = note; lock.unlock() }
    func bleState(_ note: String, confirmed: Bool = false) {
        lock.lock(); bleNote = note; lastBLE = confirmed ? ProcessInfo.processInfo.systemUptime : -.infinity; lock.unlock()
    }
    func observeBLEInfo(_ root: [String: Any], nonce: String) {
        lock.lock(); defer { lock.unlock() }
        guard let pair = pairing else { return }
        if let ids = root["assetIds"] as? String, let proof = root["assetProof"] as? String,
           MacTab5Protocol.verify(key: pair.key, message: "ASSETS|\(nonce)|\(ids)", proof: proof) { assets.acknowledge(ids) }
        guard let version = root["firmware"] as? String, version.utf8.count <= 40,
              let proof = root["firmwareProof"] as? String,
              MacTab5Protocol.verify(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)", proof: proof) else { return }
        firmware = version
        firmwareControl.observeVersion(version)
    }
    func observeUSBInfo(_ root: [String: Any]) {
        lock.lock(); defer { lock.unlock() }
        guard root["deviceId"] as? String == pairing?.deviceId else { return }
        if let ids = root["assetIds"] as? String { assets.acknowledge(ids) }
        if let version = root["firmware"] as? String, version.utf8.count <= 40 { firmware = version; firmwareControl.observeVersion(version) }
    }
    func publish(_ value: MacStatusSnapshot) { lock.lock(); snapshot = value; lock.unlock() }
    func usbState(_ note: String, confirmed: Bool = false) {
        lock.lock(); usbNote = note
        lastUSB = confirmed ? ProcessInfo.processInfo.systemUptime : -.infinity
        lock.unlock()
    }

    // Save a new key BEFORE sending it. An uncertain USB ACK can then be retried
    // explicitly with the same key, without silently invalidating the device.
    func preparePairing(device: String, path: String) throws -> MacTab5Pairing {
        lock.lock(); defer { lock.unlock() }
        guard !stopped, !maintenance else { throw MacTab5Error.unavailable }
        if !pairingReadable {
            let recovered = try loadPairing(); try recovered?.validate()
            pairing = recovered; pairingReadable = true
        }
        guard displayCompletion == nil, activeOperations == 0, !firmwareControl.busy else { throw MacTab5Error.busy }
        if let existing = pairing, existing.deviceId != device { throw MacTab5Error.identity }
        var key = pairing?.key ?? Data()
        if key.isEmpty {
            key = try MacTab5Random.key()
        }
        let record = MacTab5Pairing(deviceId: device, usbPath: path, key: key, confirmed: false)
        try record.validate(); try savePairing(record)
        pairing = record; lastUSB = -.infinity; lastWiFi = -.infinity
        return record
    }
    func confirmPairing(_ record: MacTab5Pairing) throws {
        lock.lock(); defer { lock.unlock() }
        guard pairing == record else { throw MacTab5Error.identity }
        var confirmed = record; confirmed.confirmed = true
        try savePairing(confirmed); pairing = confirmed
        usbNote = "配对完成，等待状态确认"
    }

    func frame() throws -> Data {
        lock.lock(); defer { lock.unlock() }
        return try frameLocked()
    }
    func resourceFrame() throws -> Data {
        lock.lock(); defer { lock.unlock() }
        guard !stopped, !maintenance, let pair = pairing, pair.confirmed else { throw MacTab5Error.pairingRequired }
        let chunks = assets.batch(4)
        guard !chunks.isEmpty else { throw MacTab5Error.unavailable }
        sequence += 1
        return try MacTab5Protocol.json(["version": 1, "type": "tab5_resources", "deviceId": pair.deviceId,
                                         "session": session, "sequence": sequence, "resources": chunks])
    }
    private func frameLocked() throws -> Data {
        guard !stopped, !maintenance else { throw MacTab5Error.unavailable }
        guard let pair = pairing, pair.confirmed, let snapshot else { throw MacTab5Error.pairingRequired }
        sequence += 1
        let media = latestMusic()
        let now = Date()
        if let media { assets.updateCover(media.cover) }
        var additions = assets.projection()
        additions["rpcBinary"] = 1; additions["rpcBulk"] = 1
        let offer = firmwareControl.projection()
        additions["ota"] = offer.offer.map { $0 as Any } ?? NSNull()
        var health = offer.health
        health["displayCommand"] = display.command.map { $0 as Any } ?? NSNull()
        health["galleryPacks"] = gallery.revisions(); health["gallerySyncVersion"] = 1
        if computer.hasCapabilities { health["computerControlVersion"] = 1 }
        health["voiceEnabled"] = voice.isEnabled
        additions["connectionHealth"] = health
        additions["codexTasks"] = codex.snapshot()
        if codex.isEnabled { additions["imageBinary"] = 1 }
        for count in [16, 8, 4, 1] {
            additions["codexTasks"] = codex.snapshot(taskLimit: count)
            do {
                let frame = try MacTab5Protocol.snapshot(snapshot, device: pair.deviceId, session: session, sequence: sequence, command: display.command, additions: additions, hasArtwork: assets.hasCover, music: media?.snapshot, now: now)
                if let track = media?.snapshot ?? snapshot.music, !track.title.isEmpty {
                    musicFrames += 1
                    if !track.timeline(at: now).available { unavailableMusicFrames += 1 }
                    maximumMusicAge = max(maximumMusicAge, now.timeIntervalSince(track.updatedAt))
                }
                return frame
            }
            catch MacTab5Error.invalidPacket { if count == 1 { throw MacTab5Error.invalidPacket } }
        }
        throw MacTab5Error.invalidPacket
    }

    func displaySettings(_ desired: MacTab5DisplaySettings?, completion: @escaping DisplayCompletion) {
        lock.lock()
        do {
            guard !stopped, !maintenance else { throw MacTab5Error.unavailable }
            guard pairing?.confirmed == true else { throw MacTab5Error.pairingRequired }
            let now = ProcessInfo.processInfo.systemUptime
            guard now - lastUSB < 8 || now - lastWiFi < 8 || now - lastBLE < 8 else { throw MacTab5Error.unavailable }
            let id = try display.begin(desired, now: Int64(Date().timeIntervalSince1970 * 1000))
            displayCompletion = completion
            lock.unlock()
            DispatchQueue.global().asyncAfter(deadline: .now() + 25) { [weak self] in self?.expireDisplay(id) }
        } catch { lock.unlock(); completion(.failure(error)) }
    }
    private func expireDisplay(_ id: String) {
        lock.lock()
        let callback = display.cancel(id) ? displayCompletion : nil
        if callback != nil { displayCompletion = nil }
        lock.unlock(); callback?(.failure(MacTab5Error.unconfirmed))
    }
    func stop() {
        lock.lock()
        let callback = displayCompletion; displayCompletion = nil; display.cancel()
        stopped = true
        lastUSB = -.infinity; lastWiFi = -.infinity; lastBLE = -.infinity; lanReady = false
        lock.unlock(); callback?(.failure(MacTab5Error.unconfirmed)); codex.stop(); voice.stop()
    }

    func status(device: String, nonce: String, proof: String, headers: [String: String] = [:]) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        guard !stopped, !maintenance else { return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "GET|\(device)|\(nonce)", proof: proof) else { return (401, Data()) }
        do {
            if let ids = headers["x-aibot-assets"], let assetProof = headers["x-aibot-assets-proof"],
               MacTab5Protocol.verify(key: pair.key, message: "ASSETS|\(nonce)|\(ids)", proof: assetProof) { assets.acknowledge(ids) }
            if let version = headers["x-tab5-firmware"], version.utf8.count <= 40, let firmwareProof = headers["x-tab5-firmware-proof"],
               MacTab5Protocol.verify(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)", proof: firmwareProof) { firmware = version; firmwareControl.observeVersion(version) }
            let packet = try MacTab5Protocol.encrypt(frameLocked(), key: pair.key, challenge: nonce)
            lastWiFi = ProcessInfo.processInfo.systemUptime
            return (200, packet)
        } catch { return (503, Data()) }
    }

    func rpc(device: String, nonce: String, proof: String, packet: Data, transport: String = "USB") -> (Int, Data) {
        lock.lock()
        guard !stopped, !maintenance else { lock.unlock(); return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32), (28...MacTab5Protocol.maximumRPC).contains(packet.count),
              MacTab5Protocol.verify(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof) else {
            lock.unlock(); return (401, Data())
        }
        let root: [String: Any]
        let raw: Data
        do { (root, raw) = try MacTab5Binary.open(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) }
        catch { lock.unlock(); return (401, Data()) }
        let now = Int64(Date().timeIntervalSince1970 * 1000), monotonic = ProcessInfo.processInfo.systemUptime
        var status = 400, body: [String: Any] = ["error": "invalid_session_or_time"]
        var delivery: Result<MacTab5DisplaySettings, Error>?
        var callback: DisplayCompletion?
        var dispatchOperation = false
        if root["session"] as? String == session, let issued = MacTab5Protocol.integer(root["issuedAt"]),
           issued >= now - 90_000, issued <= now + 10_000 {
            nonces = nonces.filter { monotonic - $0.value < 100 }
            if nonces[nonce] != nil || nonces.count >= 8192 { status = 409; body = ["error": "already_submitted"] }
            else {
                nonces[nonce] = monotonic
                if root["kind"] as? String == "display-settings", raw.isEmpty {
                    do {
                        (status, delivery) = try display.receive(root, monotonic: monotonic)
                        body = status == 200 ? ["accepted": true] : ["error": "request_expired"]
                        if delivery != nil { callback = displayCompletion; displayCompletion = nil }
                    } catch { status = 400; body = ["error": "invalid_settings"] }
                } else { dispatchOperation = true; activeOperations += 1 }
            }
        }
        lock.unlock()
        var result = MacTab5RPCResult(status, body)
        if dispatchOperation {
            if root["kind"] as? String == "image-chunk", codex.isEnabled, let task = MacTab5Codex.id(root["taskId"]), codex.contains(task) {
                let bytes = raw.isEmpty ? Data(base64Encoded: root["data"] as? String ?? "") ?? Data() : raw
                result = codex.photos.upload(root, bytes: bytes, device: device)
            }
            else if !raw.isEmpty { result = .error(400, "unexpected_binary_data") }
            else if root["kind"] as? String == "gallery" { result = gallery.handle(root, binary: root["binaryReply"] as? Bool == true, bulk: root["bulkReply"] as? Bool == true) }
            else if root["kind"] as? String == "computer-control" { result = computer.handle(root) }
            else if root["kind"] as? String == "codex" { result = codex.handle(root, device: device) }
            else if root["kind"] as? String == "voice" {
                result = voice.handle(root, device: device)
                voice.observeReply(op: root["op"] as? String, transport: transport, status: result.status)
            }
            else if root["kind"] as? String == "desktop" { result = desktop.handle(root, device: device) }
            else { result = firmwareControl.handle(root, device: device, transport: transport, binary: root["binaryReply"] as? Bool == true, bulk: root["bulkReply"] as? Bool == true) }
            lock.lock(); activeOperations -= 1; lock.unlock()
        }
        let binary = root["binaryReply"] as? Bool == true, bulk = binary && root["bulkReply"] as? Bool == true
        var clear = try? MacTab5Binary.encode(result, binary: binary, bulk: bulk)
        if (clear?.count ?? Int.max) > MacTab5Binary.maximumMailbox - 28 { clear = try? MacTab5Binary.encode(.error(413, "response_too_large"), binary: false, bulk: false) }
        let reply = clear.flatMap { try? MacTab5Protocol.encrypt($0, key: pair.key, challenge: nonce, maximum: MacTab5Binary.maximumMailbox) }
        if let delivery { callback?(delivery) }
        return reply.map { (200, $0) } ?? (500, Data())
    }

    func ota(device: String, nonce: String, proof: String, sha: String) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        guard !stopped, !maintenance else { return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device, MacTab5Protocol.isHex(sha, count: 64), MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "GET|/tab5/v1/ota/\(sha)|\(device)|\(nonce)", proof: proof) else { return (401, Data()) }
        guard let bytes = firmwareControl.bytes(sha: sha) else { return (404, Data()) }
        return (200, bytes)
    }

    func codexHTTP(device: String, nonce: String, proof: String, packet: Data, operation: String) -> (Int, Data) {
        guard ["read", "turn"].contains(operation), let pair = currentPairing, pair.confirmed, pair.deviceId == device,
              packet.count <= 8028, MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "POST|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof),
              var root = try? MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) else { return (401, Data()) }
        let op = root["op"] as? String ?? ""
        guard operation == "read" ? ["read", "draft-load", "receipt"].contains(op) : ["send", "steer", "interrupt", "draft-save"].contains(op) else { return (400, Data()) }
        root["kind"] = "codex"
        do {
            let sealed = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: pair.key, challenge: nonce)
            let rpcProof = MacTab5Protocol.proof(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(sealed))")
            let result = rpc(device: device, nonce: nonce, proof: rpcProof, packet: sealed, transport: "Wi-Fi")
            guard result.0 == 200 else { return result }
            let decoded = try MacTab5Protocol.object(MacTab5Protocol.decrypt(result.1, key: pair.key, challenge: nonce))
            guard let status = decoded["status"] as? Int, let body = decoded["body"] as? [String: Any] else { return (500, Data()) }
            if op == "read", status == 200 {
                let reply = try MacTab5Protocol.encrypt(MacTab5Protocol.json(body), key: pair.key, challenge: nonce)
                return (200, try MacTab5Protocol.json(["encrypted": reply.base64EncodedString()]))
            }
            return (status, try MacTab5Protocol.json(body))
        } catch { return (500, Data()) }
    }

    func imageHTTP(device: String, nonce: String, proof: String, packet: Data) -> (Int, Data) {
        guard codex.isEnabled, let pair = currentPairing, pair.confirmed, pair.deviceId == device, packet.count <= 3 * 1024 * 1024,
              MacTab5Protocol.isHex(nonce, count: 32), MacTab5Protocol.verify(key: pair.key, message: "POST|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof),
              let clear = try? MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce, maximum: 3 * 1024 * 1024) else { return (401, Data()) }
        do {
            let root: [String: Any], bytes: Data
            if clear.starts(with: Data("T5I1".utf8)) {
                guard clear.count >= 10 else { return (400, Data()) }; let count = Int(MacTab5Mailbox.u32(clear, 4))
                guard (2...4096).contains(count), clear.count > 8 + count else { return (400, Data()) }
                root = try MacTab5Protocol.object(Data(clear[8..<8 + count])); bytes = Data(clear.dropFirst(8 + count))
            } else { root = try MacTab5Protocol.object(clear); bytes = Data(base64Encoded: root["image"] as? String ?? "") ?? Data() }
            guard root["op"] as? String == "image-upload", let task = MacTab5Codex.id(root["taskId"]), codex.contains(task) else { return (400, Data()) }
            let now = Int64(Date().timeIntervalSince1970 * 1000), tick = ProcessInfo.processInfo.systemUptime
            lock.lock()
            guard !stopped, !maintenance, currentIdentityMatches(pair), root["session"] as? String == session, let issued = MacTab5Protocol.integer(root["issuedAt"]), issued >= now - 90000, issued <= now + 10000 else { lock.unlock(); return (400, Data()) }
            nonces = nonces.filter { tick - $0.value < 100 }
            guard nonces[nonce] == nil, nonces.count < 8192 else { lock.unlock(); return (409, Data()) }
            nonces[nonce] = tick; activeOperations += 1; lock.unlock()
            let result = codex.photos.upload(root, bytes: bytes, device: device)
            lock.lock(); activeOperations -= 1; lock.unlock()
            return (result.status, try MacTab5Protocol.json(result.body))
        } catch { return (400, Data()) }
    }
    private func currentIdentityMatches(_ pair: MacTab5Pairing) -> Bool { pairing == pair }

    func voicePacket(device: String, nonce: String, proof: String, packet: Data, transport: String) -> (Int, Data) {
        let result = voiceResponse(device: device, nonce: nonce, proof: proof, packet: packet, transport: transport)
        if result.0 != 200 { voice.observeReply(op: nil, transport: transport, status: result.0) }
        return result
    }
    private func voiceResponse(device: String, nonce: String, proof: String, packet: Data, transport: String) -> (Int, Data) {
        guard let pair = currentPairing, pair.confirmed, pair.deviceId == device, packet.count <= 16028,
              MacTab5Protocol.isHex(nonce, count: 32), MacTab5Protocol.verify(key: pair.key, message: "POST|/tab5/v1/voice|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof),
              var root = try? MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) else { return (401, Data()) }
        root["kind"] = "voice"
        do {
            let sealed = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: pair.key, challenge: nonce)
            let rpcProof = MacTab5Protocol.proof(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(sealed))")
            let result = rpc(device: device, nonce: nonce, proof: rpcProof, packet: sealed, transport: transport)
            guard result.0 == 200 else { return result }
            let decoded = try MacTab5Protocol.object(MacTab5Protocol.decrypt(result.1, key: pair.key, challenge: nonce))
            guard let status = decoded["status"] as? Int, let body = decoded["body"] as? [String: Any] else { return (500, Data()) }
            return (status, try MacTab5Protocol.encrypt(MacTab5Protocol.json(body), key: pair.key, challenge: nonce))
        } catch { return (500, Data()) }
    }

    func discovery(_ packet: Data, address: String) -> Data? {
        guard packet.count <= 160, PrivateIPv4Address.isValid(address), let text = String(data: packet, encoding: .ascii) else { return nil }
        let parts = text.components(separatedBy: "|")
        lock.lock(); defer { lock.unlock() }
        guard lanReady, !maintenance, let pair = pairing, pair.confirmed, parts.count == 4, parts[0] == "TAB5_DISCOVER_V1",
              parts[1] == pair.deviceId, MacTab5Protocol.isHex(parts[2], count: 32),
              MacTab5Protocol.verify(key: pair.key, message: parts.prefix(3).joined(separator: "|"), proof: parts[3]) else { return nil }
        let body = "TAB5_BRIDGE_V1|\(pair.deviceId)|\(parts[2])|\(address)|\(port)"
        return Data((body + "|" + MacTab5Protocol.proof(key: pair.key, message: body)).utf8)
    }
}

private enum MacTab5Random {
    static func key() throws -> Data {
        // CryptoKit's symmetric key initializer uses the system CSPRNG.
        SymmetricKey(size: .bits256).withUnsafeBytes { Data($0) }
    }
}
