import Darwin
import Foundation

final class SerialBridge {
    private static let prefix = "@AIBOT "
    private static let maximumLineBytes = 6_144
    private let queue = DispatchQueue(label: "AI-bot.serial-bridge")
    private let lock = NSLock()
    private let status: () -> MacStatusSnapshot
    private let lanConfiguration: () -> (host: String, port: UInt16, token: String)?
    private let resources: () -> [MacResourcePayload]
    private var activeDescriptor: Int32 = -1
    private var activePort: String?
    private var activeDeviceHost: String?
    private var stopped = false

    init(status: @escaping () -> MacStatusSnapshot,
         lanConfiguration: @escaping () -> (host: String, port: UInt16, token: String)?,
         resources: @escaping () -> [MacResourcePayload] = { [] }) {
        self.status = status
        self.lanConfiguration = lanConfiguration
        self.resources = resources
    }

    var portName: String? {
        lock.lock()
        defer { lock.unlock() }
        return activePort
    }

    var deviceHost: String? {
        lock.lock()
        defer { lock.unlock() }
        return activeDeviceHost
    }

    func start() {
        lock.lock()
        stopped = false
        lock.unlock()
        queue.async { [weak self] in self?.run() }
    }

    func stop() {
        notifyHostGoingAway()
        lock.lock()
        stopped = true
        lock.unlock()
    }

    @discardableResult
    func sendDisplayMode(_ mode: String) -> Bool {
        guard Self.displayModes.contains(mode) else { return false }
        return send(Self.controlFrame(type: "display", field: "mode", value: mode))
    }

    @discardableResult
    func sendBrightness(_ level: Int) -> Bool {
        send(Self.controlFrame(type: "brightness", field: "level", value: min(100, max(0, level))))
    }

    func notifyHostGoingAway() {
        let frame = Self.controlFrame(type: "host_going_away")
        for attempt in 0..<3 {
            _ = send(frame)
            if attempt < 2 { usleep(25_000) }
        }
    }

    @discardableResult
    func sendResource(kind: MacBinaryResourceKind, data: Data) -> Bool {
        guard let chunks = try? MacBinaryResourceProtocol.createChunks(
            kind: kind, data: data, transferId: arc4random()) else { return false }
        lock.lock()
        defer { lock.unlock() }
        guard activeDescriptor >= 0 else { return false }
        for chunk in chunks {
            var acknowledged = false
            for _ in 0..<3 where !acknowledged {
                guard Self.write(chunk.wireBytes, to: activeDescriptor) else { return false }
                acknowledged = Self.waitForResourceAck(from: activeDescriptor,
                    transferId: chunk.transferId, sequence: chunk.sequence)
            }
            if !acknowledged { return false }
        }
        return true
    }

    @discardableResult
    func provisionLan() -> Bool {
        guard let configuration = lanConfiguration(), configuration.token.utf8.count >= 32,
              PrivateIPv4Address.isValid(configuration.host) else { return false }
        return send(Self.lanFrame(host: configuration.host, port: configuration.port,
                                  token: configuration.token))
    }

    private func run() {
        while !isStopped {
            for path in Self.candidatePorts() where !isStopped {
                run(path: path)
            }
            wait(milliseconds: 3_000)
        }
    }

    private func run(path: String) {
        let descriptor = Darwin.open(path, O_RDWR | O_NOCTTY | O_NONBLOCK)
        guard descriptor >= 0 else { return }
        defer {
            clearActive(descriptor)
            Darwin.close(descriptor)
        }
        guard Self.configure(descriptor) else { return }
        wait(milliseconds: 1_200)
        guard !isStopped else { return }
        tcflush(descriptor, TCIOFLUSH)
        guard Self.write(Self.controlFrame(type: "ping"), to: descriptor),
              let handshake = Self.waitForPong(
                from: descriptor, stopped: { [weak self] in self?.isStopped ?? true }) else { return }

        setActive(descriptor, path: path, deviceHost: handshake.deviceHost)
        _ = provisionLan()
        var sentRevisions: [MacBinaryResourceKind: Int] = [:]
        var nextDeviceProbeAt = Date().addingTimeInterval(handshake.deviceHost == nil ? 5 : 30)
        while !isStopped {
            if Date() >= nextDeviceProbeAt {
                refreshDeviceHost(descriptor)
                nextDeviceProbeAt = Date().addingTimeInterval(deviceHost == nil ? 5 : 30)
            }
            for resource in resources() {
                if sentRevisions[resource.kind] == resource.revision { continue }
                if sendResource(kind: resource.kind, data: resource.data) {
                    sentRevisions[resource.kind] = resource.revision
                }
            }
            guard let frame = Self.statusFrame(status()) else { return }
            guard send(frame) else { return }
            wait(milliseconds: 2_000)
        }
    }

    private var isStopped: Bool {
        lock.lock()
        defer { lock.unlock() }
        return stopped
    }

    private func setActive(_ descriptor: Int32, path: String, deviceHost: String?) {
        lock.lock()
        activeDescriptor = descriptor
        activePort = path
        activeDeviceHost = deviceHost
        lock.unlock()
    }

    private func clearActive(_ descriptor: Int32) {
        lock.lock()
        if activeDescriptor == descriptor {
            activeDescriptor = -1
            activePort = nil
            activeDeviceHost = nil
        }
        lock.unlock()
    }

    private func send(_ data: Data) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        guard activeDescriptor >= 0 else { return false }
        return Self.write(data, to: activeDescriptor)
    }

    private func refreshDeviceHost(_ descriptor: Int32) {
        lock.lock()
        defer { lock.unlock() }
        guard activeDescriptor == descriptor,
              Self.write(Self.controlFrame(type: "ping"), to: descriptor),
              let handshake = Self.waitForPong(from: descriptor, stopped: { false }) else { return }
        activeDeviceHost = handshake.deviceHost
    }

    private func wait(milliseconds: UInt32) {
        var remaining = milliseconds
        while remaining > 0 && !isStopped {
            let step = min(remaining, 100)
            usleep(step * 1_000)
            remaining -= step
        }
    }

    static let displayModes: Set<String> = [
        "auto", "dual", "weather", "stocks", "quotas", "domestic", "system", "music", "pet", "screensaver"
    ]

    static func isCandidateDeviceName(_ name: String) -> Bool {
        ["cu.wchusbserial", "cu.usbserial", "cu.SLAB_USBtoUART", "cu.usbmodem"].contains {
            name.hasPrefix($0)
        }
    }

    static func statusFrame(_ snapshot: MacStatusSnapshot) -> Data? {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        guard let payload = try? encoder.encode(StatusEnvelope(version: 1, type: "status", data: snapshot)) else {
            return nil
        }
        return line(payload)
    }

    private static func candidatePorts() -> [String] {
        let discovered = (try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? []
        var result = discovered.filter(isCandidateDeviceName).sorted().map { "/dev/\($0)" }
        if let preferred = ProcessInfo.processInfo.environment["AIBOT_SERIAL_PORT"],
           preferred.hasPrefix("/dev/"), isCandidateDeviceName(String(preferred.dropFirst(5))) {
            result.removeAll { $0 == preferred }
            result.insert(preferred, at: 0)
        }
        return result
    }

    private static func configure(_ descriptor: Int32) -> Bool {
        var settings = termios()
        guard tcgetattr(descriptor, &settings) == 0 else { return false }
        cfmakeraw(&settings)
        settings.c_cflag |= tcflag_t(CLOCAL) | tcflag_t(CREAD)
        settings.c_cflag &= ~tcflag_t(CRTSCTS)
        guard cfsetspeed(&settings, speed_t(460_800)) == 0,
              tcsetattr(descriptor, TCSANOW, &settings) == 0 else { return false }
        return true
    }

    private static func waitForPong(from descriptor: Int32,
                                    stopped: () -> Bool) -> SerialHandshake? {
        let deadline = Date().addingTimeInterval(3)
        var buffer = Data()
        while Date() < deadline && !stopped() {
            var bytes = [UInt8](repeating: 0, count: 512)
            let count = bytes.withUnsafeMutableBytes {
                Darwin.read(descriptor, $0.baseAddress, $0.count)
            }
            if count > 0 {
                buffer.append(contentsOf: bytes.prefix(count))
                while let newline = buffer.firstIndex(of: 10) {
                    let raw = buffer[..<newline]
                    buffer.removeSubrange(...newline)
                    let line = String(decoding: raw, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
                    if let handshake = parsePong(line) { return handshake }
                }
                if buffer.count > maximumLineBytes { buffer.removeAll(keepingCapacity: true) }
            } else if count < 0 && errno != EAGAIN && errno != EWOULDBLOCK {
                return nil
            }
            usleep(10_000)
        }
        return nil
    }

    static func parsePong(_ line: String) -> SerialHandshake? {
        guard line.hasPrefix(prefix),
              let payload = line.data(using: .utf8)?.dropFirst(prefix.utf8.count),
              let response = try? JSONDecoder().decode(PongEnvelope.self, from: Data(payload)),
              response.version == 1, response.type == "pong",
              response.device == "esp8266" else { return nil }
        let host = response.ip.flatMap { PrivateIPv4Address.isValid($0) ? $0 : nil }
        return SerialHandshake(deviceHost: host)
    }

    private static func write(_ data: Data, to descriptor: Int32) -> Bool {
        data.withUnsafeBytes { raw in
            guard let base = raw.bindMemory(to: UInt8.self).baseAddress else { return false }
            var offset = 0
            let deadline = Date().addingTimeInterval(2)
            while offset < raw.count && Date() < deadline {
                let written = Darwin.write(descriptor, base.advanced(by: offset), raw.count - offset)
                if written > 0 {
                    offset += written
                } else if written < 0 && errno != EAGAIN && errno != EWOULDBLOCK {
                    return false
                } else {
                    usleep(10_000)
                }
            }
            return offset == raw.count
        }
    }

    private static func waitForResourceAck(from descriptor: Int32, transferId: UInt32,
                                           sequence: UInt16) -> Bool {
        let deadline = Date().addingTimeInterval(3)
        var buffer = Data()
        while Date() < deadline {
            var bytes = [UInt8](repeating: 0, count: 512)
            let count = bytes.withUnsafeMutableBytes {
                Darwin.read(descriptor, $0.baseAddress, $0.count)
            }
            if count > 0 {
                buffer.append(contentsOf: bytes.prefix(count))
                while let newline = buffer.firstIndex(of: 10) {
                    let raw = buffer[..<newline]
                    buffer.removeSubrange(...newline)
                    let line = String(decoding: raw, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
                    guard line.hasPrefix(prefix),
                          let payload = line.data(using: .utf8)?.dropFirst(prefix.utf8.count),
                          let object = try? JSONSerialization.jsonObject(with: Data(payload)) as? [String: Any],
                          object["type"] as? String == "resource_ack",
                          (object["transferId"] as? NSNumber)?.uint32Value == transferId,
                          (object["sequence"] as? NSNumber)?.uint16Value == sequence else { continue }
                    return (object["ok"] as? NSNumber)?.boolValue == true
                }
                if buffer.count > maximumLineBytes { buffer.removeAll(keepingCapacity: true) }
            } else if count < 0 && errno != EAGAIN && errno != EWOULDBLOCK {
                return false
            }
            usleep(10_000)
        }
        return false
    }

    private static func controlFrame(type: String) -> Data {
        jsonLine(["version": 1, "type": type])
    }

    private static func controlFrame(type: String, field: String, value: Any) -> Data {
        jsonLine(["version": 1, "type": type, field: value])
    }

    private static func lanFrame(host: String, port: UInt16, token: String) -> Data {
        jsonLine(["version": 1, "type": "lan_config", "data": [
            "host": host, "port": Int(port), "token": token
        ]])
    }

    private static func jsonLine(_ object: [String: Any]) -> Data {
        line((try? JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])) ?? Data())!
    }

    private static func line(_ payload: Data) -> Data? {
        var result = Data(prefix.utf8)
        result.append(payload)
        result.append(10)
        return result.count <= maximumLineBytes ? result : nil
    }

    private struct StatusEnvelope: Encodable {
        let version: Int
        let type: String
        let data: MacStatusSnapshot
    }

    private struct PongEnvelope: Decodable {
        let version: Int
        let type: String
        let device: String
        let ip: String?
    }
}

struct SerialHandshake {
    let deviceHost: String?
}
