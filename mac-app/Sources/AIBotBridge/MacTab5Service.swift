import Foundation
import CryptoKit

final class MacTab5Service {
    typealias DisplayCompletion = (Result<MacTab5DisplaySettings, Error>) -> Void
    private let lock = NSLock()
    private let loadPairing: () throws -> MacTab5Pairing?
    private let savePairing: (MacTab5Pairing) throws -> Void
    private let host: () -> String?
    let port: UInt16
    let session = MacTab5Protocol.identifier()
    private var pairing: MacTab5Pairing?
    private var pairingReadable = true
    private var snapshot: MacStatusSnapshot?
    private var sequence: Int64 = 0
    private var lanReady = false
    private var stopped = false
    private var lastUSB: TimeInterval = -.infinity
    private var lastWiFi: TimeInterval = -.infinity
    private var usbNote = "尚未连接"
    private var discoveryNote = "等待启动"
    private var nonces: [String: TimeInterval] = [:]
    private let display = MacTab5DisplayTransaction()
    private var displayCompletion: DisplayCompletion?

    init(port: UInt16, host: @escaping () -> String? = LocalNetworkIdentity.privateIPv4,
         load: @escaping () throws -> MacTab5Pairing? = { try MacTab5PairingStore().load() },
         save: @escaping (MacTab5Pairing) throws -> Void = { try MacTab5PairingStore().save($0) }) {
        self.port = port; self.host = host; loadPairing = load; savePairing = save
        do { pairing = try load(); try pairing?.validate() }
        catch { pairingReadable = false; usbNote = "无法读取配对记录，请检查钥匙串权限" }
    }

    var currentPairing: MacTab5Pairing? { lock.lock(); defer { lock.unlock() }; return pairing }
    var configurationBusy: Bool { lock.lock(); defer { lock.unlock() }; return displayCompletion != nil }
    var connectionSummary: String {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        let device = pairing.map { "TAB5 · \($0.deviceId)" } ?? "尚未配对 TAB5"
        let usb = now - lastUSB < 8 ? "数据已确认" : usbNote
        let wifi = !lanReady ? "LAN 服务未就绪" : now - lastWiFi < 8 ? "收到已认证请求" : "等待设备连接"
        let confirmation = pairing?.confirmed == false ? "\nUSB 配对结果未确认，请重新配对。" : ""
        return "\(device)\nUSB：\(usb)\nWi-Fi：\(wifi)\n自动发现：\(discoveryNote)\(confirmation)"
    }
    var lanConfiguration: (String, UInt16)? {
        lock.lock(); defer { lock.unlock() }
        guard lanReady, let address = host(), PrivateIPv4Address.isValid(address) else { return nil }
        return (address, port)
    }
    func setLANReady(_ ready: Bool) { lock.lock(); lanReady = ready && !stopped; if !lanReady { lastWiFi = -.infinity }; lock.unlock() }
    func setDiscoveryState(_ note: String) { lock.lock(); discoveryNote = note; lock.unlock() }
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
        guard !stopped else { throw MacTab5Error.unavailable }
        if !pairingReadable {
            let recovered = try loadPairing(); try recovered?.validate()
            pairing = recovered; pairingReadable = true
        }
        guard displayCompletion == nil else { throw MacTab5Error.busy }
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
    private func frameLocked() throws -> Data {
        guard !stopped else { throw MacTab5Error.unavailable }
        guard let pair = pairing, pair.confirmed, let snapshot else { throw MacTab5Error.pairingRequired }
        sequence += 1
        return try MacTab5Protocol.snapshot(snapshot, device: pair.deviceId, session: session,
                                            sequence: sequence, command: display.command)
    }

    func displaySettings(_ desired: MacTab5DisplaySettings?, completion: @escaping DisplayCompletion) {
        lock.lock()
        do {
            guard !stopped else { throw MacTab5Error.unavailable }
            guard pairing?.confirmed == true else { throw MacTab5Error.pairingRequired }
            let now = ProcessInfo.processInfo.systemUptime
            guard now - lastUSB < 8 || now - lastWiFi < 8 else { throw MacTab5Error.unavailable }
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
        lastUSB = -.infinity; lastWiFi = -.infinity; lanReady = false
        lock.unlock(); callback?(.failure(MacTab5Error.unconfirmed))
    }

    func status(device: String, nonce: String, proof: String) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        guard !stopped else { return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "GET|\(device)|\(nonce)", proof: proof) else { return (401, Data()) }
        do {
            let packet = try MacTab5Protocol.encrypt(frameLocked(), key: pair.key, challenge: nonce)
            lastWiFi = ProcessInfo.processInfo.systemUptime
            return (200, packet)
        } catch { return (503, Data()) }
    }

    func rpc(device: String, nonce: String, proof: String, packet: Data) -> (Int, Data) {
        lock.lock()
        guard !stopped else { lock.unlock(); return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32), (28...MacTab5Protocol.maximumRPC).contains(packet.count),
              MacTab5Protocol.verify(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof) else {
            lock.unlock(); return (401, Data())
        }
        let root: [String: Any]
        do { root = try MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) }
        catch { lock.unlock(); return (401, Data()) }
        let now = Int64(Date().timeIntervalSince1970 * 1000), monotonic = ProcessInfo.processInfo.systemUptime
        var status = 400, body: [String: Any] = ["error": "invalid_session_or_time"]
        var delivery: Result<MacTab5DisplaySettings, Error>?
        var callback: DisplayCompletion?
        if root["session"] as? String == session, let issued = MacTab5Protocol.integer(root["issuedAt"]),
           issued >= now - 90_000, issued <= now + 10_000 {
            nonces = nonces.filter { monotonic - $0.value < 100 }
            if nonces[nonce] != nil || nonces.count >= 8192 { status = 409; body = ["error": "already_submitted"] }
            else {
                nonces[nonce] = monotonic
                if root["kind"] as? String == "display-settings" {
                    do {
                        (status, delivery) = try display.receive(root, monotonic: monotonic)
                        body = status == 200 ? ["accepted": true] : ["error": "request_expired"]
                        if delivery != nil { callback = displayCompletion; displayCompletion = nil }
                    } catch { status = 400; body = ["error": "invalid_settings"] }
                } else { status = 400; body = ["error": "invalid_operation"] }
            }
        }
        let reply = try? MacTab5Protocol.encrypt(MacTab5Protocol.json(["status": status, "body": body]), key: pair.key, challenge: nonce)
        lock.unlock()
        if let delivery { callback?(delivery) }
        return reply.map { (200, $0) } ?? (500, Data())
    }

    func discovery(_ packet: Data, address: String) -> Data? {
        guard packet.count <= 160, PrivateIPv4Address.isValid(address), let text = String(data: packet, encoding: .ascii) else { return nil }
        let parts = text.components(separatedBy: "|")
        lock.lock(); defer { lock.unlock() }
        guard lanReady, let pair = pairing, pair.confirmed, parts.count == 4, parts[0] == "TAB5_DISCOVER_V1",
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
