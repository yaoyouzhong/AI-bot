import Darwin
import Foundation

// Compatibility CDC mailbox, intentionally limited to phase-one small RPCs.
// All port I/O and user operations execute on this single queue.
final class MacTab5USB {
    private let service: MacTab5Service
    private let queue = DispatchQueue(label: "AI-bot.tab5-usb")
    private var timer: DispatchSourceTimer?
    private var wire: MacTab5SerialWire?
    private var nextProbe: TimeInterval = 0
    private var probeIndex = 0
    private var lastStatus: TimeInterval = -.infinity
    private var rpcSupported = false
    private var completed: UInt32 = 0

    init(service: MacTab5Service) { self.service = service }
    static func ports() -> [String] {
        ((try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? [])
            .filter(SerialBridge.isCandidateDeviceName).sorted().map { "/dev/" + $0 }
    }
    func start() {
        queue.async { [weak self] in
            guard let self, self.timer == nil else { return }
            let timer = DispatchSource.makeTimerSource(queue: self.queue)
            timer.schedule(deadline: .now(), repeating: .milliseconds(100))
            timer.setEventHandler { [weak self] in self?.step() }
            self.timer = timer; timer.resume()
        }
    }
    func stopAndWait() {
        queue.sync { timer?.cancel(); timer = nil; close("USB 已停止") }
    }
    private func close(_ note: String) {
        wire = nil; rpcSupported = false; completed = 0; lastStatus = -.infinity
        service.usbState(note)
    }

    func pair(path: String, completion: @escaping (Result<Void, Error>) -> Void) {
        queue.async { [weak self] in
            guard let self else { return }
            do {
                guard !self.service.configurationBusy else { throw MacTab5Error.busy }
                guard Self.ports().contains(path) else { throw MacTab5Error.serial }
                self.close("正在 USB 配对")
                let wire = try MacTab5SerialWire(path: path)
                let hello = try wire.identity()
                let record = try self.service.preparePairing(device: hello.device, path: path)
                var command: [String: Any] = ["version": 1, "type": "tab5_pair", "deviceId": record.deviceId,
                                               "key": record.key.base64EncodedString()]
                if let (host, port) = self.service.lanConfiguration { command["host"] = host; command["port"] = Int(port) }
                try wire.send(command)
                let reply = try wire.reply("tab5_paired")
                guard reply["deviceId"] as? String == record.deviceId else { throw MacTab5Error.identity }
                try self.service.confirmPairing(record)
                self.wire = wire; self.rpcSupported = hello.rpc
                self.lastStatus = -.infinity
                completion(.success(Void()))
            } catch { self.close("USB 配对未确认"); completion(.failure(error)) }
        }
    }

    func configureWiFi(ssid: String, password: String, completion: @escaping (Result<Void, Error>) -> Void) {
        queue.async { [weak self] in
            guard let self else { return }
            do {
                guard !self.service.configurationBusy else { throw MacTab5Error.busy }
                guard (1...32).contains(ssid.utf8.count), password.utf8.count <= 63,
                      !ssid.utf8.contains(0), !password.utf8.contains(0) else { throw MacTab5Error.invalidWiFi }
                guard let pair = self.service.currentPairing, pair.confirmed else { throw MacTab5Error.pairingRequired }
                guard let wire = self.wire else { throw MacTab5Error.unavailable }
                let hello = try wire.identity()
                guard hello.device == pair.deviceId else { throw MacTab5Error.identity }
                try wire.send(["version": 1, "type": "tab5_wifi", "deviceId": pair.deviceId, "ssid": ssid, "password": password])
                let ack = try wire.reply("tab5_wifi_saved")
                guard ack["deviceId"] as? String == pair.deviceId else { throw MacTab5Error.identity }
                completion(.success(Void()))
            } catch { self.close("Wi-Fi 写入结果未确认"); completion(.failure(error)) }
        }
    }

    private func step() {
        guard let pair = service.currentPairing, pair.confirmed else { return }
        do {
            if wire == nil {
                let now = ProcessInfo.processInfo.systemUptime
                guard now >= nextProbe else { return }; nextProbe = now + 2
                var ports = Self.ports(); ports.removeAll { $0 == pair.usbPath }; ports.insert(pair.usbPath, at: 0)
                let path = ports[probeIndex % ports.count]; probeIndex += 1
                let candidate = try MacTab5SerialWire(path: path)
                let hello = try candidate.identity()
                guard hello.device == pair.deviceId else { throw MacTab5Error.identity }
                wire = candidate; rpcSupported = hello.rpc; lastStatus = -.infinity
            }
            guard let wire else { return }
            if ProcessInfo.processInfo.systemUptime - lastStatus >= 2 {
                if let (host, port) = service.lanConfiguration {
                    try wire.send(["version": 1, "type": "tab5_host", "host": host, "port": Int(port)])
                }
                let frame = try service.frame(), root = try MacTab5Protocol.object(frame)
                try wire.sendJSON(frame)
                let ack = try wire.reply("tab5_ack")
                guard ack["deviceId"] as? String == pair.deviceId,
                      MacTab5Protocol.integer(ack["sequence"]) == MacTab5Protocol.integer(root["sequence"]) else { throw MacTab5Error.invalidPacket }
                service.usbState("数据已确认", confirmed: true)
                service.observeUSBInfo(ack)
                lastStatus = ProcessInfo.processInfo.systemUptime
            }
            if rpcSupported { try pump(wire, device: pair.deviceId) }
            if !service.configurationBusy, let chunk = service.assets.projection()["resource"] as? [String: Any] {
                try wire.send(["version": 1, "type": "tab5_resource", "deviceId": pair.deviceId, "resource": chunk])
                let ack = try wire.reply("tab5_resource_ack")
                guard ack["deviceId"] as? String == pair.deviceId else { throw MacTab5Error.identity }
                service.observeUSBInfo(ack)
            }
        } catch { close("USB 未确认，等待重连") }
    }

    private func mailbox(_ wire: MacTab5SerialWire, device: String, data: Data? = nil) throws -> Data {
        let tag = MacTab5Protocol.identifier()
        var command: [String: Any] = ["version": 1, "type": "tab5_rpc", "deviceId": device, "tag": tag, "chunk": 2048]
        command["data"] = data.map { $0.base64EncodedString() as Any } ?? NSNull()
        try wire.send(command)
        let reply = try wire.reply("tab5_rpc")
        guard reply["deviceId"] as? String == device, reply["tag"] as? String == tag,
              reply["ok"] as? Bool == true else { throw MacTab5Error.invalidPacket }
        if data != nil { return Data() }
        guard let text = reply["data"] as? String, text.utf8.count <= 2744, let bytes = Data(base64Encoded: text),
              (8...2056).contains(bytes.count) else { throw MacTab5Error.invalidPacket }
        return bytes
    }

    private func pump(_ wire: MacTab5SerialWire, device: String) throws {
        var part = try mailbox(wire, device: device)
        let id = MacTab5Mailbox.u32(part, 0)
        if id == 0 || id == completed { return }
        let total = Int(MacTab5Mailbox.u16(part, 6))
        guard (124...(MacTab5Protocol.maximumRPC + 96)).contains(total) else { throw MacTab5Error.invalidPacket }
        var request = Data()
        let deadline = ProcessInfo.processInfo.systemUptime + 75
        while true {
            guard ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.timeout }
            try MacTab5Mailbox.append(part, id: id, total: total, to: &request)
            if request.count == total { break }
            var ack = Data([1]); MacTab5Mailbox.append32(id, to: &ack); MacTab5Mailbox.append16(request.count, to: &ack)
            _ = try mailbox(wire, device: device, data: ack)
            part = try mailbox(wire, device: device)
        }
        let bytes = [UInt8](request)
        guard let nonce = String(bytes: bytes[0..<32], encoding: .ascii),
              let proof = String(bytes: bytes[32..<96], encoding: .ascii) else { throw MacTab5Error.invalidPacket }
        let (status, response) = service.rpc(device: device, nonce: nonce, proof: proof, packet: Data(bytes[96...]))
        guard status == 200, (28...MacTab5Binary.maximumMailbox).contains(response.count) else { throw MacTab5Error.invalidPacket }
        for fragment in try MacTab5Mailbox.response(response, id: id) {
            guard ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.timeout }
            _ = try mailbox(wire, device: device, data: fragment)
        }
        completed = id
    }
}

enum MacTab5Mailbox {
    static func u16(_ bytes: Data, _ offset: Int) -> UInt16 {
        UInt16(bytes[bytes.startIndex + offset]) | UInt16(bytes[bytes.startIndex + offset + 1]) << 8
    }
    static func u32(_ bytes: Data, _ offset: Int) -> UInt32 {
        UInt32(u16(bytes, offset)) | UInt32(u16(bytes, offset + 2)) << 16
    }
    static func append16(_ number: Int, to data: inout Data) { data.append(UInt8(number & 255)); data.append(UInt8((number >> 8) & 255)) }
    static func append32(_ number: UInt32, to data: inout Data) { append16(Int(number & 65535), to: &data); append16(Int(number >> 16), to: &data) }
    static func append(_ part: Data, id: UInt32, total: Int, to request: inout Data) throws {
        guard (9...2056).contains(part.count), u32(part, 0) == id, Int(u16(part, 4)) == request.count,
              Int(u16(part, 6)) == total, request.count + part.count - 8 <= total else { throw MacTab5Error.invalidPacket }
        request.append(part.dropFirst(8))
    }
    static func response(_ packet: Data, id: UInt32) throws -> [Data] {
        guard id != 0, (28...MacTab5Binary.maximumMailbox).contains(packet.count) else { throw MacTab5Error.invalidPacket }
        return stride(from: 0, to: packet.count, by: 2048).map { offset in
            var fragment = Data([2]); append32(id, to: &fragment); append16(offset, to: &fragment); append16(packet.count, to: &fragment)
            fragment.append(packet.subdata(in: offset..<min(offset + 2048, packet.count))); return fragment
        }
    }
}

private final class MacTab5SerialWire {
    private let descriptor: Int32
    private let path: String
    private var input = Data()

    init(path: String) throws {
        guard MacTab5USB.ports().contains(path), MacSerialPortLease.acquire(path) else { throw MacTab5Error.serial }
        let fd = Darwin.open(path, O_RDWR | O_NOCTTY | O_NONBLOCK)
        guard fd >= 0 else { MacSerialPortLease.release(path); throw MacTab5Error.serial }
        var settings = termios()
        guard tcgetattr(fd, &settings) == 0 else { Darwin.close(fd); MacSerialPortLease.release(path); throw MacTab5Error.serial }
        cfmakeraw(&settings); settings.c_cflag |= tcflag_t(CLOCAL) | tcflag_t(CREAD); settings.c_cflag &= ~tcflag_t(CRTSCTS)
        guard cfsetspeed(&settings, speed_t(460_800)) == 0, tcsetattr(fd, TCSANOW, &settings) == 0 else {
            Darwin.close(fd); MacSerialPortLease.release(path); throw MacTab5Error.serial
        }
        descriptor = fd; self.path = path
        usleep(250_000); tcflush(fd, TCIOFLUSH)
    }
    deinit { Darwin.close(descriptor); MacSerialPortLease.release(path) }
    func identity() throws -> (device: String, rpc: Bool) {
        try send(["version": 1, "type": "tab5_ping"])
        let hello = try reply("tab5_hello")
        guard MacTab5Protocol.integer(hello["version"]) == 1, let device = hello["deviceId"] as? String,
              MacTab5Protocol.isHex(device, count: 12) else { throw MacTab5Error.identity }
        return (device, MacTab5Protocol.integer(hello["rpcVersion"]) == 1)
    }
    func send(_ value: [String: Any]) throws { try write(MacTab5Protocol.line(value)) }
    func sendJSON(_ json: Data) throws {
        var line = Data(MacTab5Protocol.prefix.utf8); line.append(json); line.append(10); try write(line)
    }
    private func write(_ bytes: Data) throws {
        let deadline = ProcessInfo.processInfo.systemUptime + 4
        try bytes.withUnsafeBytes { buffer in
            guard let base = buffer.baseAddress else { return }
            var offset = 0
            while offset < buffer.count {
                guard ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.timeout }
                let count = Darwin.write(descriptor, base.advanced(by: offset), buffer.count - offset)
                if count > 0 { offset += count }
                else if count < 0 && errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR { throw MacTab5Error.serial }
                else { usleep(2_000) }
            }
        }
    }
    func reply(_ expected: String) throws -> [String: Any] {
        let deadline = ProcessInfo.processInfo.systemUptime + 4
        while ProcessInfo.processInfo.systemUptime < deadline {
            while let newline = input.firstIndex(of: 10) {
                let line = Data(input[..<newline]); input.removeSubrange(...newline)
                guard line.starts(with: Data(MacTab5Protocol.prefix.utf8)),
                      let root = try? MacTab5Protocol.object(Data(line.dropFirst(7))), let type = root["type"] as? String else { continue }
                if type == expected { return root }
                if type == expected + "_rejected" { throw MacTab5Error.invalidPacket }
            }
            var bytes = [UInt8](repeating: 0, count: 512)
            let count = bytes.withUnsafeMutableBytes { Darwin.read(descriptor, $0.baseAddress, $0.count) }
            if count > 0 { input.append(contentsOf: bytes.prefix(count)); guard input.count <= 8192 else { throw MacTab5Error.invalidPacket } }
            else if count < 0 && errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR { throw MacTab5Error.serial }
            else { usleep(2_000) }
        }
        throw MacTab5Error.timeout
    }
}
