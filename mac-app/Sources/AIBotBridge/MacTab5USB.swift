import Darwin
import Foundation

struct MacTab5USBRejected: Error {
    let reason: Int
}

private struct MacTab5USBReadTimeout: Error {
    let written:Int,received:Int,lines:Int,malformed:Int,other:Int,buffered:Int
    let expected:String,writeMs:Int,readMs:Int,zeroReads:Int,wouldBlockReads:Int
    var summary:String {
        let target = ["tab5_ack":"状态", "tab5_rpc":"RPC", "tab5_hello":"身份", "tab5_ota_diagnostic":"启动", "tab5_host_claimed":"认证"][expected] ?? "管理"
        return "超时；等\(target)，写\(written)/读\(received)B，写\(writeMs)/读\(readMs)ms，空读\(zeroReads)/暂不可读\(wouldBlockReads)，行\(lines)，坏JSON\(malformed)，其他\(other)，残留\(buffered)B"
    }
}
struct MacTab5USBAuthenticationRequired: Error {
    static func matches(_ root: [String: Any]) -> Bool {
        root["type"] as? String == "tab5_host_required" && MacTab5Protocol.integer(root["version"]) == 1
    }
}

enum MacTab5USBStatus {
    static func supportsPacked(_ hello: [String:Any], device: String, field: String = "usbPacked") -> Bool {
        hello["type"] as? String == "tab5_hello" && MacTab5Protocol.integer(hello["version"]) == 1 &&
            hello["deviceId"] as? String == device && MacTab5Protocol.integer(hello[field]) == 1
    }
    static func deltaRenewsOwnership(_ hello: [String:Any], device: String) -> Bool {
        guard supportsPacked(hello,device:device,field:"usbDelta"),
              let firmware = hello["firmware"] as? String,
              firmware.hasPrefix("0.2."), firmware.hasSuffix("-ui") else { return false }
        let patch = String(firmware.dropFirst(4).dropLast(3))
        // The inspected269 receiver renews ownership on a valid status delta.
        // Older/unknown receivers still need their full-state refreshes.
        return Int(patch).map { $0 >= 269 && String($0) == patch } == true
    }
    static func wire(_ frame:Data, baseline:inout MacTab5BLEDelta, packed:Bool,
                     delta:Bool, ownershipRefresh:Bool, deltaRenewsOwnership:Bool) throws -> Data {
        try baseline.wire(frame,packed:packed,supported:delta && (!ownershipRefresh || deltaRenewsOwnership))
    }
    static func exchange(full: Data, wire: Data, write: (Data) throws -> Void,
                         read: () throws -> [String:Any]) throws -> [String:Any] {
        try write(wire)
        do { return try read() }
        catch let error as MacTab5USBRejected where wire != full && [0,1,2].contains(error.reason) {
            // One representation fallback, retaining the exact session/sequence.
            // Identity, sequence, timeout and serial errors must still reconnect.
            try write(full); return try read()
        }
    }
}

private enum MacTab5USBPhase: String {
    case hostClaim = "电脑认证"
    case discovery = "设备发现", statusBuild = "状态生成", statusWrite = "状态发送", statusReply = "状态回执"
    case rpc = "请求通道", resourceWrite = "资源发送", resourceReply = "资源回执"
    func failure(_ error: Error) -> String {
        let detail: String
        if error is MacTab5USBAuthenticationRequired {
            detail = "设备要求重新认证"
        } else if let read = error as? MacTab5USBReadTimeout {
            detail = read.summary
        } else if let rejected = error as? MacTab5USBRejected {
            detail = "设备拒收 \(rejected.reason)"
        } else {
            switch error as? MacTab5Error {
            case .timeout: detail = "超时"
            case .serial: detail = "串口不可用"
            case .identity: detail = "身份不匹配"
            case .invalidPacket: detail = "校验失败"
            case .pairingRequired, .keychain: detail = "配对记录不可用"
            case .hostNotTrusted: detail = "此 Mac 未受信任，请明确点击 USB 配对"
            case .busy: detail = "设备忙，等待恢复"
            default: detail = "未完成"
            }
        }
        // Only fixed labels and a bounded protocol code; never error descriptions or payloads.
        return "USB 未确认，等待重连（\(rawValue)：\(detail)）"
    }
}

struct MacTab5StatusCadence {
    private var next: TimeInterval = 0
    private var interval: TimeInterval = 2
    func isDue(_ now: TimeInterval) -> Bool { now >= next }
    static func interval(for music: MusicSnapshot?, at now: Date) -> TimeInterval {
        guard let music, !music.title.isEmpty, music.timeline(at: now).available else { return 2 }
        // Match the existing Windows USB path for an available music timeline.
        return 0.5
    }
    mutating func acknowledge(started: TimeInterval, finished: TimeInterval, urgent: Bool,
                              interval requested: TimeInterval = 2) {
        // Preserve phase across ACK delays, resetting only on cadence/control changes.
        let deadline = next == 0 || urgent || requested != interval || started - next >= requested
            ? started + requested : next + requested
        interval = requested
        // A blocked transport must not send a burst to catch up missed slots.
        next = deadline > finished ? deadline : finished + requested
    }
}

enum MacTab5USBResourceBurst {
    // Reuse the existing 1 KiB + ACK exchange; yield frequently to status, RPC and voice.
    @discardableResult
    static func run(clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                    shouldSend: (Int) -> Bool, sendOne: () throws -> Bool) rethrows -> Int {
        let started = clock()
        var sent = 0
        while sent < 8, clock() - started < 0.060, shouldSend(sent) {
            guard try sendOne() else { break }
            sent += 1
        }
        return sent
    }
}

struct MacTab5USBLeaseCadence {
    private var fullStarted: TimeInterval?
    func isDue(_ now: TimeInterval) -> Bool {
        guard let fullStarted else { return true }
        // Refresh within the existing8 s lease, without relying on user RPCs.
        // The wire selector retains full states for receivers older than269.
        return now < fullStarted || now - fullStarted >= 2
    }
    mutating func acknowledge(started: TimeInterval) { fullStarted = started }
}

// Compatibility CDC mailbox, intentionally limited to phase-one small RPCs.
// All port I/O and user operations execute on this single queue.
final class MacTab5USB {
    private let service: MacTab5Service
    private let authenticationEndpoint: () -> (String, UInt16)?
    private let queue = DispatchQueue(label: "AI-bot.tab5-usb")
    private var timer: DispatchSourceTimer?
    private var wire: MacTab5SerialWire?
    private var nextProbe: TimeInterval = 0
    private var probeIndex = 0
    private var statusCadence = MacTab5StatusCadence()
    private var statusDelta = MacTab5BLEDelta()
    private var leaseCadence = MacTab5USBLeaseCadence()
    private var lastMusic: MusicSnapshot?
    private var previewPublication = MacPreviewUSBPublication()
    private var previewPoll = MacPreviewUSBPollCadence()
    private var pollMilliseconds = 100
    private var rpcSupported = false
    private var completed: UInt32 = 0

    init(service: MacTab5Service, authenticationEndpoint: (() -> (String, UInt16)?)? = nil) {
        self.service = service
        self.authenticationEndpoint = authenticationEndpoint ?? { service.lanConfiguration }
    }
    static func ports() -> [String] {
        ((try? FileManager.default.contentsOfDirectory(atPath: "/dev")) ?? [])
            .filter(SerialBridge.isCandidateDeviceName).sorted().map { "/dev/" + $0 }
    }
    func start() {
        queue.async { [weak self] in
            guard let self, self.timer == nil else { return }
            let timer = DispatchSource.makeTimerSource(queue: self.queue)
            self.pollMilliseconds = 100
            timer.schedule(deadline: .now(), repeating: .milliseconds(100))
            timer.setEventHandler { [weak self] in self?.step() }
            self.timer = timer; timer.resume()
        }
    }
    func stopAndWait() {
        queue.sync { timer?.cancel(); timer = nil; close("USB 已停止") }
    }
    private func close(_ note: String) {
        wire = nil; rpcSupported = false; completed = 0; statusCadence = .init(); lastMusic = nil
        statusDelta.reset(); leaseCadence = .init()
        previewPublication = .init()
        previewPoll = .init()
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
                                               "key": record.key.base64EncodedString(), "hostId": MacTab5HostBinding.id(record),
                                               "name": MacTab5HostBinding.name(ProcessInfo.processInfo.hostName)]
                if let (host, port) = self.service.lanConfiguration { command["host"] = host; command["port"] = Int(port) }
                try wire.send(command)
                let reply = try wire.reply("tab5_paired")
                guard reply["deviceId"] as? String == record.deviceId else { throw MacTab5Error.identity }
                // Enrollment is explicit, and its legacy ACK is not host authentication.
                guard try wire.identity().device == record.deviceId else { throw MacTab5Error.identity }
                try wire.authenticate(record, endpoint: self.authenticationEndpoint())
                try self.service.confirmPairing(record)
                self.wire = wire; self.rpcSupported = hello.rpc
                self.statusCadence = .init(); self.statusDelta.reset(); self.leaseCadence = .init()
                self.previewPublication = .init()
                completion(.success(Void()))
            } catch { self.close("USB 配对未确认"); completion(.failure(error)) }
        }
    }

    func configureWiFi(ssid: String, password: String, label: String = "", completion: @escaping (Result<Void, Error>) -> Void) {
        do { try MacSavedWiFi.validate(ssid:ssid,label:label); guard password.utf8.count <= 63, !password.utf8.contains(0) else { throw MacTab5Error.invalidWiFi } }
        catch { completion(.failure(error)); return }
        management("tab5_wifi", reply:"tab5_wifi_saved", fields:["ssid":ssid,"password":password,"label":label]) { completion($0.map { _ in () }) }
    }
    func readWiFi(completion:@escaping (Result<[MacSavedWiFi],Error>)->Void) {
        management("tab5_wifi_list",reply:"tab5_wifi_list") { outcome in
            completion(outcome.flatMap { reply in Result { try MacSavedWiFi.parse(reply,device:reply["deviceId"] as? String ?? "") } })
        }
    }
    func labelWiFi(ssid:String,label:String,completion:@escaping (Result<Void,Error>)->Void) {
        do { try MacSavedWiFi.validate(ssid:ssid,label:label) } catch { completion(.failure(error)); return }
        management("tab5_wifi_label",reply:"tab5_wifi_label_saved",fields:["ssid":ssid,"label":label]) { completion($0.map { _ in () }) }
    }
    func forgetWiFi(ssid:String,completion:@escaping (Result<Void,Error>)->Void) {
        do { try MacSavedWiFi.validate(ssid:ssid,label:"") } catch { completion(.failure(error)); return }
        management("tab5_wifi_forget",reply:"tab5_wifi_forgotten",fields:["ssid":ssid]) { completion($0.map { _ in () }) }
    }
    func benchmark(_ action:String,completion:@escaping (Result<[String:Any],Error>)->Void) {
        guard ["start","status","cancel","gallery_start","gallery_status","gallery_cancel"].contains(action) else { completion(.failure(MacTab5Error.invalidPacket)); return }
        management("tab5_benchmark",reply:"tab5_benchmark",fields:["action":action],benchmark:true,completion:completion)
    }
    private func management(_ type:String,reply:String,fields:[String:Any] = [:],benchmark:Bool = false,completion:@escaping (Result<[String:Any],Error>)->Void) {
        queue.async { [weak self] in guard let self else { return }; do {
            guard benchmark || !self.service.configurationBusy else { throw MacTab5Error.busy }
            guard let pair = self.service.currentPairing, pair.confirmed, let wire = self.wire else { throw MacTab5Error.unavailable }
            guard try wire.identity().device == pair.deviceId else { throw MacTab5Error.identity }
            try wire.authenticate(pair,endpoint:self.authenticationEndpoint())
            var request = fields; request["type"] = type; request["version"] = 1; request["deviceId"] = pair.deviceId
            try wire.send(request); let response = try wire.reply(reply)
            try MacTab5USBManagementReply.validate(response,type:reply,device:pair.deviceId)
            completion(.success(response))
        } catch { self.close("USB 操作未确认，等待重连"); completion(.failure(error)) } }
    }
    func verifyBoot(prepared: Bool = false, completion: @escaping (Result<Void, Error>) -> Void) {
        queue.async { [weak self] in
            guard let self else { return }
            do {
                guard let pair = self.service.currentPairing, pair.confirmed, let wire = self.wire else { throw MacTab5Error.unavailable }
                if !prepared { try self.service.firmwareControl.beginBootCheck(device: pair.deviceId, transport: "USB") }
                var hellos: [[String: Any]] = []
                for index in 0..<3 {
                    try wire.send(["version": 1, "type": "tab5_ping"])
                    hellos.append(try wire.reply("tab5_hello"))
                    if index < 2 { Thread.sleep(forTimeInterval: 1.2) }
                }
                try wire.send(["version": 1, "type": "tab5_ota_status"])
                let diagnostic = try wire.reply("tab5_ota_diagnostic")
                try self.service.firmwareControl.completeUSBCheck(hellos: hellos, diagnostic: diagnostic, device: pair.deviceId)
                completion(.success(Void()))
            } catch { completion(.failure(error)) }
        }
    }

    private func step() {
        defer { updatePreviewPollCadence() }
        guard service.isEnabled else { if wire != nil { close("设备已停用") }; return }
        guard let pair = service.currentPairing, pair.confirmed else { return }
        var phase = MacTab5USBPhase.discovery
        let stepStarted = ProcessInfo.processInfo.systemUptime
        var statusTime = 0.0, generationTime = 0.0, rpcTime = 0.0, statusBytes = 0, wireBytes = 0, chunks = 0
        defer { service.assets.recordUSBStep(started:stepStarted,status:statusTime,rpc:rpcTime,
                                             generation:generationTime,rawBytes:statusBytes,wireBytes:wireBytes,chunks:chunks) }
        do {
            if wire == nil {
                let now = ProcessInfo.processInfo.systemUptime
                guard now >= nextProbe else { return }; nextProbe = now + 2
                var ports = Self.ports(); ports.removeAll { $0 == pair.usbPath }; ports.insert(pair.usbPath, at: 0)
                let path = ports[probeIndex % ports.count]; probeIndex += 1
                let candidate = try MacTab5SerialWire(path: path)
                let hello = try candidate.identity()
                guard hello.device == pair.deviceId else { throw MacTab5Error.identity }
                wire = candidate; rpcSupported = hello.rpc; statusCadence = .init(); statusDelta.reset(); leaseCadence = .init()
                previewPublication = .init()
            }
            guard let wire else { return }
            let music = service.currentMusic
            let urgent = MusicSnapshot.changedUrgently(from: lastMusic, to: music)
            chunks = try MacTab5USBVoicePriority.run(rpcSupported: rpcSupported,
                voiceBusy: { self.service.voice.needsTransportPriority }, claim: {
                phase = .hostClaim
                try wire.authenticate(pair, endpoint: authenticationEndpoint())
            }, status: {
                let started = ProcessInfo.processInfo.systemUptime
                let previewUrgent = previewPublication.isDue(service.screenPreview.command,voicePriority:service.voice.needsTransportPriority)
                if statusCadence.isDue(started) || urgent || previewUrgent {
                    phase = .hostClaim
                    try wire.authenticate(pair, endpoint: authenticationEndpoint())
                    phase = .statusBuild
                    let frame = try service.frame(includeResources: !service.voice.needsTransportPriority), root = try MacTab5Protocol.object(frame)
                    statusBytes = frame.count
                    let fullRefresh = leaseCadence.isDue(started)
                    let encoded = try MacTab5USBStatus.wire(frame,baseline:&statusDelta,packed:wire.packedStatus,
                        delta:wire.deltaStatus,ownershipRefresh:fullRefresh,deltaRenewsOwnership:wire.deltaLease)
                    generationTime = ProcessInfo.processInfo.systemUptime-started
                    let ack = try MacTab5USBStatus.exchange(full:frame,wire:encoded,write: { bytes in
                        phase = .statusWrite; wireBytes += bytes.count; try wire.sendJSON(bytes)
                    },read: {
                        phase = .statusReply; return try wire.reply("tab5_ack")
                    })
                    statusTime = ProcessInfo.processInfo.systemUptime-started
                    guard ack["deviceId"] as? String == pair.deviceId,
                          let sequence = MacTab5Protocol.integer(root["sequence"]),
                          MacTab5Protocol.integer(ack["sequence"]) == sequence else { throw MacTab5Error.invalidPacket }
                    statusDelta.acknowledge(frame,sequence:sequence)
                    previewPublication.acknowledge(root)
                    if fullRefresh { leaseCadence.acknowledge(started:started) }
                    service.usbState("数据已确认", confirmed: true)
                    service.observeUSBInfo(ack)
                    statusCadence.acknowledge(started: started, finished: ProcessInfo.processInfo.systemUptime,
                                             urgent: urgent || previewUrgent, interval: MacTab5StatusCadence.interval(for: music, at: Date()))
                    lastMusic = music
                }
            }, rpc: {
                phase = .rpc
                let rpcStarted = ProcessInfo.processInfo.systemUptime
                if rpcSupported {
                    let completedRequests = try MacPreviewUSBPacing.run(active:service.screenPreview.command?["transport"] as? String == "USB" && !service.voice.needsTransportPriority,
                        mayContinue: {
                            self.service.isEnabled && self.service.currentPairing == pair && !self.service.configurationBusy &&
                                !self.service.voice.needsTransportPriority && self.service.screenPreview.command?["transport"] as? String == "USB" &&
                                !self.statusCadence.isDue(ProcessInfo.processInfo.systemUptime)
                        }, exchange: { try self.pump(wire,device:pair.deviceId) })
                    if completedRequests > 0 { previewPoll.completed(at:ProcessInfo.processInfo.systemUptime) }
                }
                rpcTime += ProcessInfo.processInfo.systemUptime-rpcStarted
            }, resources: {
                return try MacTab5USBResourceBurst.run(shouldSend: { _ in
                    self.service.isEnabled && !self.service.configurationBusy &&
                        !self.service.voice.needsTransportPriority &&
                        !self.statusCadence.isDue(ProcessInfo.processInfo.systemUptime)
                }) {
                    guard let chunk = self.service.assets.projection()["resource"] as? [String: Any] else { return false }
                    let chunkStarted = ProcessInfo.processInfo.systemUptime
                    phase = .resourceWrite
                    try wire.send(["version": 1, "type": "tab5_resource", "deviceId": pair.deviceId, "resource": chunk])
                    phase = .resourceReply
                    let ack = try wire.reply("tab5_resource_ack")
                    guard ack["deviceId"] as? String == pair.deviceId else { throw MacTab5Error.identity }
                    service.observeUSBInfo(ack)
                    service.assets.recordUSBChunk(chunk,elapsed:ProcessInfo.processInfo.systemUptime-chunkStarted)
                    return true
                }
            })
        } catch {
            if error as? MacTab5Error == .hostNotTrusted { service.rejectUSBHost() }
            close(phase.failure(error))
            if error as? MacTab5Error == .hostNotTrusted || error as? MacTab5Error == .busy {
                nextProbe = ProcessInfo.processInfo.systemUptime + 20
            }
        }
    }

    private func updatePreviewPollCadence() {
        guard let timer else { return }
        let milliseconds = previewPoll.milliseconds(at:ProcessInfo.processInfo.systemUptime,
            active:wire != nil && service.isEnabled && service.screenPreview.command?["transport"] as? String == "USB",
            voicePriority:service.voice.needsTransportPriority,foregroundBusy:service.configurationBusy)
        guard milliseconds != pollMilliseconds else { return }
        pollMilliseconds = milliseconds
        timer.schedule(deadline: .now() + .milliseconds(milliseconds), repeating: .milliseconds(milliseconds))
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

    @discardableResult private func pump(_ wire: MacTab5SerialWire, device: String) throws -> Bool {
        var part = try mailbox(wire, device: device)
        let id = MacTab5Mailbox.u32(part, 0)
        if id == 0 || id == completed { return false }
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
        let packet = Data(bytes[96...]), service = self.service
        guard let pairing = service.currentPairing, pairing.deviceId == device else { throw MacTab5Error.identity }
        service.usbOperationBegan()
        defer { service.usbOperationEnded() }
        let pending = MacTab5USBOperationResult {
            service.rpc(device: device, nonce: nonce, proof: proof, packet: packet)
        }
        let (status, response) = try MacTab5USBOperationWait.run(deadline: deadline,
            clock: { ProcessInfo.processInfo.systemUptime }, poll: pending.poll, pause: pending.pause,
            cancelled: { !service.isEnabled || service.currentPairing != pairing },
            due: { self.leaseCadence.isDue(ProcessInfo.processInfo.systemUptime) },
            heartbeat: { try self.operationHeartbeat(wire, pairing: pairing) })
        guard status == 200, (28...MacTab5Binary.maximumMailbox).contains(response.count) else { throw MacTab5Error.invalidPacket }
        for fragment in try MacTab5Mailbox.response(response, id: id) {
            guard ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.timeout }
            _ = try mailbox(wire, device: device, data: fragment)
        }
        completed = id
        service.voice.acknowledgeUSBReply(nonce:nonce)
        return true
    }

    private func operationHeartbeat(_ wire: MacTab5SerialWire, pairing: MacTab5Pairing) throws {
        let started = ProcessInfo.processInfo.systemUptime
        let frame = try service.frame(includeResources: false), root = try MacTab5Protocol.object(frame)
        let full = try MacTab5USBStatus.wire(frame,baseline:&statusDelta,packed:wire.packedStatus,
            delta:wire.deltaStatus,ownershipRefresh:true,deltaRenewsOwnership:wire.deltaLease)
        let ack = try MacTab5USBStatus.exchange(full: frame, wire: full,
            write: wire.sendJSON, read: { try wire.reply("tab5_ack") })
        guard ack["deviceId"] as? String == pairing.deviceId,
              let sequence = MacTab5Protocol.integer(root["sequence"]),
              MacTab5Protocol.integer(ack["sequence"]) == sequence else { throw MacTab5Error.invalidPacket }
        statusDelta.acknowledge(frame, sequence: sequence)
        previewPublication.acknowledge(root)
        leaseCadence.acknowledge(started: started)
        service.usbState("数据已确认", confirmed: true); service.observeUSBInfo(ack)
        service.usbOperationHeartbeat()
        let music = service.currentMusic
        statusCadence.acknowledge(started: started, finished: ProcessInfo.processInfo.systemUptime,
            urgent: false, interval: MacTab5StatusCadence.interval(for: music, at: Date()))
        lastMusic = music
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

// Explicit acceptance-only control. Authentication does not send a USB status
// heartbeat: benchmark data continues over BLE. Never expose pairing material.
final class MacTab5BLEProbeControl {
    private let wire: MacTab5SerialWire
    private let pair: MacTab5Pairing
    private let endpoint: (String, UInt16)
    init(pair: MacTab5Pairing, endpoint: (String, UInt16)) throws {
        guard let path = MacTab5USB.ports().first else { throw MacTab5Error.unavailable }
        wire = try MacTab5SerialWire(path: path); self.pair = pair; self.endpoint = endpoint
        guard try wire.identity().device == pair.deviceId else { throw MacTab5Error.identity }
        let hello = try snapshot()
        guard hello["currentHostId"] as? String == MacTab5HostBinding.id(pair) else { throw MacTab5Error.identity }
    }
    func snapshot() throws -> [String: Any] {
        try wire.send(["version": 1, "type": "tab5_ping"])
        return try wire.reply("tab5_hello")
    }
    func command(_ action: String) throws -> [String: Any] {
        guard ["start", "status", "cancel"].contains(action), try wire.identity().device == pair.deviceId else { throw MacTab5Error.invalidPacket }
        try wire.authenticate(pair, endpoint: endpoint)
        try wire.send(["version": 1, "type": "tab5_benchmark", "deviceId": pair.deviceId, "action": action])
        let reply = try wire.reply("tab5_benchmark")
        guard reply["deviceId"] as? String == pair.deviceId else { throw MacTab5Error.identity }
        return reply
    }
    func otaTrace() throws -> [String: Any] {
        try wire.send(["version": 1, "type": "tab5_crash_status"])
        let reply = try wire.reply("tab5_crash_diagnostic")
        guard let trace = reply["trace"] as? [String: Any] else { throw MacTab5Error.invalidPacket }
        var safe: [String: Any] = [:]
        for key in ["stage", "offset", "total", "error", "crash"] {
            guard let value = MacTab5Protocol.integer(trace[key]) else { throw MacTab5Error.invalidPacket }
            safe[key] = value
        }
        let hello = try snapshot()
        if let radio = hello["wifiIsolation"] as? String, radio.split(separator: ",").count == 7,
           radio.split(separator: ",").allSatisfy({ Int($0) != nil }) { safe["wifiIsolation"] = radio }
        if let elapsed = MacTab5Protocol.integer(trace["otaMs"]) { safe["otaMs"] = elapsed }
        if let phases = trace["otaPhases"] as? [String: Any] {
            var values: [String: Int64] = [:]
            for key in ["transport", "prepareMs", "writeMs", "freeWaitMs", "readyWaitMs", "verifyMs", "installMs"] {
                if let number = MacTab5Protocol.integer(phases[key]) { values[key] = number }
            }
            safe["otaPhases"] = values
        }
        return safe
    }
}

private final class MacTab5SerialWire {
    private let descriptor: Int32
    private let path: String
    private var input = Data()
    private var lastWriteSize = 0
    private var lastWriteMs = 0
    private var hostSession = MacTab5USBHostSession()
    private(set) var packedStatus = false
    private(set) var deltaStatus = false
    private(set) var deltaLease = false

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
        try hostSession.observe(hello)
        packedStatus = MacTab5USBStatus.supportsPacked(hello,device:device)
        deltaStatus = MacTab5USBStatus.supportsPacked(hello,device:device,field:"usbDelta")
        deltaLease = MacTab5USBStatus.deltaRenewsOwnership(hello,device:device)
        return (device, MacTab5Protocol.integer(hello["rpcVersion"]) == 1)
    }
    func authenticate(_ pair: MacTab5Pairing, endpoint: (String, UInt16)?) throws {
        if !hostSession.required {
            if let (host, port) = endpoint { try send(["version": 1, "type": "tab5_host", "host": host, "port": Int(port)]) }
            return
        }
        try hostSession.authenticate(pair, address: endpoint?.0 ?? "", port: endpoint?.1 ?? 0,
                                     name: MacTab5HostBinding.name(ProcessInfo.processInfo.hostName), refresh: {
            try self.send(["version": 1, "type": "tab5_ping"])
            return try self.reply("tab5_hello")
        }, exchange: { command in
            try self.send(command); return try self.reply("tab5_host_claimed")
        })
    }
    func send(_ value: [String: Any]) throws { try write(MacTab5Protocol.line(value)) }
    func sendJSON(_ json: Data) throws {
        var line = Data(MacTab5Protocol.prefix.utf8); line.append(json); line.append(10); try write(line)
    }
    private func write(_ bytes: Data) throws {
        lastWriteSize=bytes.count
        let began = ProcessInfo.processInfo.systemUptime, deadline = began + 4
        defer { lastWriteMs = Int(min(60_000,max(0,ProcessInfo.processInfo.systemUptime-began)*1000)) }
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
        let began = ProcessInfo.processInfo.systemUptime, deadline = began + 4
        var received=0,lines=0,malformed=0,other=0,zeroReads=0,wouldBlockReads=0
        while ProcessInfo.processInfo.systemUptime < deadline {
            while let newline = input.firstIndex(of: 10) {
                let line = Data(input[..<newline]); input.removeSubrange(...newline)
                lines += 1
                guard line.starts(with: Data(MacTab5Protocol.prefix.utf8)),
                      let root = try? MacTab5Protocol.object(Data(line.dropFirst(7))), let type = root["type"] as? String else { malformed += 1; continue }
                if MacTab5USBAuthenticationRequired.matches(root) { throw MacTab5USBAuthenticationRequired() }
                if type == expected { return root }
                if type == expected + "_rejected" {
                    let code = MacTab5Protocol.integer(root["rejectReason"])
                    throw MacTab5USBRejected(reason: code.flatMap { (0...5).contains($0) ? Int($0) : nil } ?? -1)
                }
                other += 1
            }
            var bytes = [UInt8](repeating: 0, count: 512)
            let count = bytes.withUnsafeMutableBytes { Darwin.read(descriptor, $0.baseAddress, $0.count) }
            if count > 0 { received += count;input.append(contentsOf: bytes.prefix(count)); guard input.count <= 8192 else { throw MacTab5Error.invalidPacket } }
            else if count < 0 && errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR { throw MacTab5Error.serial }
            else {
                if count == 0 { zeroReads += 1 }
                else if errno == EAGAIN || errno == EWOULDBLOCK { wouldBlockReads += 1 }
                usleep(2_000)
            }
        }
        throw MacTab5USBReadTimeout(written:lastWriteSize,received:received,lines:lines,malformed:malformed,other:other,buffered:input.count,
            expected:expected,writeMs:lastWriteMs,readMs:Int(min(60_000,max(0,ProcessInfo.processInfo.systemUptime-began)*1000)),zeroReads:zeroReads,wouldBlockReads:wouldBlockReads)
    }
}
