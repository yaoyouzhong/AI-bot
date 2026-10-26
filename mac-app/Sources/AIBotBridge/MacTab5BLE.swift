import CoreBluetooth
import Foundation

enum MacTab5BLEWire {
    static let service = CBUUID(string: "7af50001-7f23-4a91-bc65-667a19320101")
    static let info = CBUUID(string: "7af50002-7f23-4a91-bc65-667a19320101")
    static let input = CBUUID(string: "7af50003-7f23-4a91-bc65-667a19320101")
    static let voice = CBUUID(string: "7af50004-7f23-4a91-bc65-667a19320101")
    static let rpc = CBUUID(string: "7af50005-7f23-4a91-bc65-667a19320101")
    static let hostBinding = CBUUID(string: "7af50007-7f23-4a91-bc65-667a19320101")

    static func ownerHint(_ bytes: Data, pair: MacTab5Pairing) throws -> (selected: Bool, proofRequired: Bool) {
        guard bytes.count <= 128 else { throw MacTab5Error.invalidPacket }
        let root = try MacTab5Protocol.object(bytes)
        guard MacTab5Protocol.integer(root["version"]) == 1, let id = root["hostId"] as? String,
              id.isEmpty || MacTab5Protocol.isHex(id, count: 32),
              root["bleProofRequired"] == nil || MacTab5Protocol.integer(root["bleProofRequired"]) == 1
        else { throw MacTab5Error.invalidPacket }
        return (id == MacTab5HostBinding.id(pair), root["bleProofRequired"] != nil)
    }
    static func ownershipProof(_ pair: MacTab5Pairing, sequence: Int64) throws -> Data {
        try pair.validate()
        guard (1...9_007_199_254_740_991).contains(sequence) else { throw MacTab5Error.invalidPacket }
        return try MacTab5Protocol.json(["version": 1, "type": "tab5_ble_owner", "deviceId": pair.deviceId,
                                        "hostId": MacTab5HostBinding.id(pair), "sequence": sequence])
    }

    static func authenticate(_ bytes: Data, pair: MacTab5Pairing, nonce: String? = nil) throws -> (String, Int64, [String: Any]) {
        guard bytes.count <= 2048 else { throw MacTab5Error.invalidPacket }
        let root = try MacTab5Protocol.object(bytes)
        guard root["deviceId"] as? String == pair.deviceId, let challenge = root["nonce"] as? String,
              MacTab5Protocol.isHex(challenge, count: 32), nonce == nil || nonce == challenge,
              let ack = MacTab5Protocol.integer(root["ack"]), ack >= 0,
              let proof = root["proof"] as? String,
              MacTab5Protocol.verify(key: pair.key, message: "\(challenge)|\(ack)", proof: proof) else { throw MacTab5Error.identity }
        return (challenge, ack, root)
    }

    static func fragments(_ bytes: Data, maximum: Int, lengthPrefix: Bool) throws -> [Data] {
        guard (10...512).contains(maximum), !bytes.isEmpty, bytes.count <= 65536 else { throw MacTab5Error.invalidPacket }
        var framed = Data()
        if lengthPrefix { MacTab5Mailbox.append32(UInt32(bytes.count), to: &framed) }
        framed.append(bytes)
        // The firmware caps each attribute value at 488 bytes, independent of MTU.
        let size = min(maximum, 488)
        return stride(from: 0, to: framed.count, by: size).map { Data(framed[$0..<min($0 + size, framed.count)]) }
    }
}

// CoreBluetooth and every mutable transport field live on one serial queue.
// Instantiating the bridge does not request Bluetooth permission; start is explicit.
final class MacTab5BLE: NSObject, CBCentralManagerDelegate, CBPeripheralDelegate {
    static let enabledKey = "tab5_bluetooth_enabled"
    private let service: MacTab5Service
    private let queue = DispatchQueue(label: "AI-bot.tab5-ble")
    private var central: CBCentralManager?
    private var peer: CBPeripheral?
    private var characteristics: [CBUUID: CBCharacteristic] = [:]
    private var timer: DispatchSourceTimer?
    private var enabled = false
    private var nonce: String?
    private var authenticatedDevice: String?
    private var packedStatus = false
    private var deltaStatus = false, statusDelta = MacTab5BLEDelta()
    private var voiceCredits = 0, voiceSubscriptionAttempted = false, voiceSubscriptionPending = false, voiceNotifications = false
    private var voiceDescriptorDiscovery = false
    private var voiceDescriptor: CBDescriptor?
    private var voiceSubscriptionRecovery = MacTab5BLENotificationRecovery()
    private var voiceWindow: MacTab5BLEVoiceWindow?
    private var voiceWindowCompletion: ((Data) -> Void)?
    private var voiceGrantPending = false
    private var rpcCredits = 0, rpcSubscriptionAttempted = false, rpcSubscriptionPending = false, rpcNotifications = false
    private var rpcWindow: MacTab5BLERPCWindow?
    private var rpcWindowCompletion: ((Data) -> Void)?
    private var rpcGrantPending = false
    private var rpcDescriptorDiscovery = false
    private var rpcDescriptor: CBDescriptor?
    private var rpcSubscriptionRecovery = MacTab5BLENotificationRecovery()
    private var lastVoiceProbe: TimeInterval = -.infinity
    private var lastVoiceActivity: TimeInterval = -.infinity
    private var lastControlProbe: TimeInterval = -.infinity
    private var lastControlActivity: TimeInterval = -.infinity
    private var probingVoice = false
    private var admission = MacTab5BLEAdmission()
    private var deadline: TimeInterval = 0
    private var nextScan: TimeInterval = 0
    private var lastFrame: TimeInterval = -.infinity
    private var busy = false
    private var readCompletion: ((Result<Data, Error>) -> Void)?
    private var reading: CBCharacteristic?
    private var writeCompletion: ((Result<Void, Error>) -> Void)?
    private var writing: CBCharacteristic?
    private var packets: [Data] = []
    private var packetIndex = 0
    private var awaitingWrite = false
    private var completed: [CBUUID: UInt32] = [:]
    private var resourceTurn = false
    private var voiceTurn = false

    init(service: MacTab5Service) { self.service = service; super.init() }
    func setEnabled(_ value: Bool) {
        queue.async { [weak self] in
            guard let self else { return }
            self.enabled = value
            if value {
                if self.central == nil { self.central = CBCentralManager(delegate: self, queue: self.queue) }
                if self.timer == nil {
                    let timer = DispatchSource.makeTimerSource(queue: self.queue)
                    timer.schedule(deadline: .now(), repeating: .milliseconds(200))
                    timer.setEventHandler { [weak self] in self?.tick() }
                    self.timer = timer; timer.resume()
                }
                self.nextScan = 0
                // Re-enabling reuses the manager, whose unchanged state does
                // not trigger another delegate callback. Refresh it explicitly.
                if let central = self.central { self.centralManagerDidUpdateState(central) }
            } else { self.central?.stopScan(); self.reset("蓝牙已关闭"); self.timer?.cancel(); self.timer = nil }
        }
    }
    func stop() { setEnabled(false) }
    func stopAndWait() { queue.sync { enabled = false; central?.stopScan(); reset("蓝牙已暂停，正在操作设备"); timer?.cancel(); timer = nil } }
    private var now: TimeInterval { ProcessInfo.processInfo.systemUptime }
    private func reset(_ message: String) {
        service.bleDiagnostics.failCurrent()
        voiceWindow = nil; voiceWindowCompletion = nil; voiceGrantPending = false
        rpcWindow = nil; rpcWindowCompletion = nil; rpcGrantPending = false
        rpcCredits = 0; rpcSubscriptionAttempted = false; rpcSubscriptionPending = false; rpcNotifications = false
        rpcDescriptorDiscovery = false; rpcDescriptor = nil; rpcSubscriptionRecovery = MacTab5BLENotificationRecovery()
        voiceCredits = 0; voiceSubscriptionAttempted = false; voiceSubscriptionPending = false; voiceNotifications = false
        voiceDescriptorDiscovery = false; voiceDescriptor = nil
        voiceSubscriptionRecovery = MacTab5BLENotificationRecovery()
        lastVoiceProbe = -.infinity; lastVoiceActivity = -.infinity; lastControlProbe = -.infinity; lastControlActivity = -.infinity; probingVoice = false
        admission.reset()
        readCompletion = nil; writeCompletion = nil; reading = nil; writing = nil; packets = []; awaitingWrite = false
        busy = false; nonce = nil; authenticatedDevice = nil; packedStatus = false; deltaStatus = false; statusDelta.reset(); characteristics = [:]; completed = [:]
        if let peer { central?.cancelPeripheralConnection(peer) }
        peer = nil; lastFrame = -.infinity; nextScan = now + 3
        service.bleState(message)
    }
    private func tick() {
        guard enabled else { return }
        guard service.isEnabled else {
            central?.stopScan()
            if peer != nil { reset("设备已停用") } else { service.bleState("设备已停用") }
            return
        }
        if peer != nil, !admission.accepts(admission.generation, pair: service.currentPairing) {
            reset("配对已改变，重新认证"); return
        }
        if let voiceWindow, voiceWindow.expired(at: now) { reset("蓝牙语音窗口超时，不重发操作"); return }
        if let rpcWindow, rpcWindow.expired(at: now) { reset("蓝牙数据窗口超时，不重发操作"); return }
        if busy && now > deadline { reset("蓝牙操作超时，等待重连"); return }
        guard central?.state == .poweredOn, let pair = service.currentPairing, pair.confirmed else { return }
        if peer == nil {
            if now >= nextScan, let central {
                nextScan = now + 8
                // macOS can retain the device's HID connection across sleep.
                // Connected TAB5s stop advertising, so scanning alone cannot
                // recover our GATT session. Identity is still authenticated below.
                if let connected = central.retrieveConnectedPeripherals(withServices: [MacTab5BLEWire.service]).first {
                    connect(connected, central: central)
                } else { central.scanForPeripherals(withServices: [MacTab5BLEWire.service]) }
            }
            return
        }
        guard nonce != nil, admission.ready, !busy else { return }
        guard authenticatedDevice == pair.deviceId else { reset("配对已改变，重新认证"); return }
        // Defer only while the device actually selects USB; a management ACK
        // must not suppress an explicitly selected BLE/Wi-Fi mode.
        // No replay or channel switching of an in-flight RPC.
        guard !service.usbOwnsActiveState else { service.bleState("已认证 · USB 优先待命"); return }
        // Establish the legacy owner path with an acknowledged encrypted state
        // before attempting subscription. This alone does not resolve ATT/8.
        if !voiceSubscriptionAttempted, lastFrame.isFinite {
            voiceSubscriptionAttempted = true
            service.bleDiagnostics.voiceLink(credits: voiceCredits,
                available: characteristics[MacTab5BLEWire.voice]?.properties.contains(.notify) == true, subscribed: false)
            if voiceCredits > 0, let endpoint = characteristics[MacTab5BLEWire.voice], endpoint.properties.contains(.notify) {
                busy = true; voiceSubscriptionPending = true; deadline = now + 15; peer?.setNotifyValue(true, for: endpoint); return
            }
        }
        if !rpcSubscriptionAttempted, lastFrame.isFinite {
            rpcSubscriptionAttempted = true
            service.bleDiagnostics.rpcLink(credits:rpcCredits,available:characteristics[MacTab5BLEWire.rpc]?.properties.contains(.notify) == true,subscribed:false)
            if rpcCredits > 0, let endpoint = characteristics[MacTab5BLEWire.rpc], endpoint.properties.contains(.notify) {
                busy = true; rpcSubscriptionPending = true; deadline = now + 15; peer?.setNotifyValue(true, for: endpoint); return
            }
        }
        busy = true; deadline = now + 75
        // Fresh mailbox activity gets a short gap before another background frame.
        // Still allow status after four seconds; resources wait for voice idle.
        if let priority = MacTab5BLEVoicePacing.priority(now: now, lastVoice: lastVoiceActivity,
                                                       lastControl: lastControlProbe, lastFrame: lastFrame) {
            let uuid = priority == .voice ? MacTab5BLEWire.voice : MacTab5BLEWire.rpc
            if let endpoint = characteristics[uuid] { pump(endpoint, pair: pair) } else { busy = false }
        }
        else if MacTab5BLEControlPacing.active(now:now,lastControl:lastControlActivity,lastFrame:lastFrame),let endpoint = characteristics[MacTab5BLEWire.rpc] { pump(endpoint,pair:pair) }
        else if now - lastFrame >= 2 { sendStatus(pair) }
        else if !service.configurationBusy && service.assets.hasPending && resourceTurn && now - lastVoiceActivity >= 1.5 { resourceTurn = false; sendStatus(pair, resources: true) }
        else {
            resourceTurn = true
            let uuid = voiceTurn ? MacTab5BLEWire.voice : MacTab5BLEWire.rpc; voiceTurn.toggle()
            if let endpoint = characteristics[uuid] { pump(endpoint, pair: pair) }
            else { busy = false }
        }
    }
    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        guard enabled else { return }
        switch central.state {
        case .poweredOn: nextScan = 0; service.bleState("正在寻找已配对 TAB5")
        case .unauthorized: reset("蓝牙权限被拒绝，请在系统设置中允许 AI-bot")
        case .poweredOff: reset("请打开 Mac 蓝牙")
        case .unsupported: reset("这台 Mac 不支持低功耗蓝牙")
        default: reset("蓝牙正在准备")
        }
    }
    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        connect(peripheral, central: central)
    }
    private func connect(_ peripheral: CBPeripheral, central: CBCentralManager) {
        guard enabled, service.isEnabled, peer == nil, let pair = service.currentPairing, pair.confirmed else { return }
        admission.begin(pair)
        central.stopScan(); peer = peripheral; peripheral.delegate = self
        // Numeric comparison requires user approval on macOS and TAB5. The
        // device allows 30 seconds; do not cancel its dialog after only 15.
        busy = true; deadline = now + 60
        service.bleState("正在连接 TAB5；如出现配对提示，请核对并确认两端数字")
        central.connect(peripheral)
    }
    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) { if peer === peripheral { peripheral.discoverServices([MacTab5BLEWire.service]) } }
    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        if peer === peripheral { reset("蓝牙连接失败，等待重连") }
    }
    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        if peer === peripheral { reset("蓝牙已断开，等待重连") }
    }
    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        guard peer === peripheral else { return }
        guard error == nil, let target = peripheral.services?.first(where: { $0.uuid == MacTab5BLEWire.service }) else { reset("未发现 TAB5 蓝牙服务"); return }
        peripheral.discoverCharacteristics(nil, for: target)
    }
    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor target: CBService, error: Error?) {
        guard peer === peripheral else { return }
        guard error == nil else { reset("无法读取蓝牙服务"); return }
        for characteristic in target.characteristics ?? [] { characteristics[characteristic.uuid] = characteristic }
        guard let info = characteristics[MacTab5BLEWire.info], characteristics[MacTab5BLEWire.input] != nil,
              let pair = service.currentPairing else { reset("蓝牙服务不完整"); return }
        if let ownership = characteristics[MacTab5BLEWire.hostBinding] {
            read(ownership) { [weak self] result in
                guard let self else { return }
                do {
                    let hint = try MacTab5BLEWire.ownerHint(result.get(), pair: pair)
                    guard hint.selected else {
                        self.reset("待命 · 设备当前由其他电脑控制"); self.nextScan = self.now + 20; return
                    }
                    self.authenticatePeer(info, pair: pair, requiresProof: hint.proofRequired)
                } catch { self.reset("电脑归属读取失败，等待重连") }
            }
        } else { authenticatePeer(info, pair: pair, requiresProof: false) }
    }
    private func authenticatePeer(_ info: CBCharacteristic, pair: MacTab5Pairing, requiresProof: Bool) {
        read(info) { [weak self] result in
            guard let self else { return }
            do {
                let (nonce, _, root) = try MacTab5BLEWire.authenticate(result.get(), pair: pair)
                self.packedStatus = MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce)
                self.deltaStatus = MacTab5BLEDelta.supports(root, pair: pair, nonce: nonce)
                self.voiceCredits = MacTab5BLEVoiceWindow.credits(root)
                self.rpcCredits = MacTab5BLERPCWindow.credits(root)
                self.nonce = nonce; self.authenticatedDevice = pair.deviceId; self.service.observeBLEInfo(root, nonce: nonce)
                try self.admission.authenticated(self.admission.generation, pair: pair, requiresProof: requiresProof)
                if requiresProof { self.sendOwnerProof(pair, nonce: nonce) }
                else { self.busy = false; self.service.bleState("设备已认证，等待数据确认") }
            } catch { self.reset("蓝牙配对认证失败，请通过 USB 重新核对配对") }
        }
    }
    private func sendOwnerProof(_ pair: MacTab5Pairing, nonce: String) {
        guard let peer, let input = characteristics[MacTab5BLEWire.input] else { reset("蓝牙归属服务不可用"); return }
        do {
            let sequence = try service.nextSequence(for: pair), generation = admission.generation
            let clear = try MacTab5BLEWire.ownershipProof(pair, sequence: sequence)
            let packet = try MacTab5Protocol.encrypt(clear, key: pair.key, challenge: nonce)
            busy = true; deadline = now + 15
            write(try MacTab5BLEWire.fragments(packet, maximum: writeMaximum(peer), lengthPrefix: true), to: input) { [weak self] result in
                guard let self else { return }
                do {
                    try result.get()
                    self.confirmOwner(pair, nonce: nonce, sequence: sequence, generation: generation, attempts: 0)
                } catch { self.reset("蓝牙归属证明未确认，等待重连") }
            }
        } catch { reset("蓝牙归属证明不可用") }
    }
    private func confirmOwner(_ pair: MacTab5Pairing, nonce: String, sequence: Int64, generation: UInt64, attempts: Int) {
        guard enabled, self.nonce == nonce, admission.accepts(generation, pair: service.currentPairing) else { return }
        guard let info = characteristics[MacTab5BLEWire.info] else { reset("蓝牙服务已断开"); return }
        read(info) { [weak self] result in
            guard let self else { return }
            do {
                if try !self.admission.acknowledge(result.get(), generation: generation, pair: pair, nonce: nonce, sequence: sequence) {
                    guard attempts < 15 else { throw MacTab5Error.unconfirmed }
                    self.queue.asyncAfter(deadline: .now() + 0.1) { [weak self] in
                        self?.confirmOwner(pair, nonce: nonce, sequence: sequence, generation: generation, attempts: attempts + 1)
                    }; return
                }
                // This opens admission before USB standby, without marking display delivery or a USB lease.
                self.busy = false; self.service.bleState("电脑归属已确认")
            } catch { self.reset("蓝牙归属确认失败，等待重连") }
        }
    }
    func peripheral(_ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?) {
        if peer === peripheral, characteristic === characteristics[MacTab5BLEWire.rpc], rpcSubscriptionPending {
            rpcSubscriptionPending = false
            // Only a successful subscription enables grants; rejected peers
            // retain the original read/cursor-ACK path without replay.
            rpcNotifications = error == nil && characteristic.isNotifying
            let failure = error as NSError?
            let domain: MacTab5BLEDiagnostics.FailureDomain = failure == nil ? .none : failure?.domain == CBATTErrorDomain ? .att : failure?.domain == CBErrorDomain ? .bluetooth : .other
            service.bleDiagnostics.rpcLink(credits:rpcCredits,available:characteristic.properties.contains(.notify),subscribed:rpcNotifications,failure:domain,code:failure?.code ?? 0)
            if rpcSubscriptionRecovery.needsReadback(after:error) {
                rpcDescriptorDiscovery = true; deadline = now + 15
                peripheral.discoverDescriptors(for:characteristic); return
            }
            busy = false; return
        }
        guard peer === peripheral, characteristic === characteristics[MacTab5BLEWire.voice], voiceSubscriptionPending else { return }
        voiceSubscriptionPending = false
        // A rejected subscription keeps the original read/cursor-ACK transport.
        voiceNotifications = error == nil && characteristic.isNotifying
        let failure = error as NSError?
        let domain: MacTab5BLEDiagnostics.FailureDomain = failure == nil ? .none : failure?.domain == CBATTErrorDomain ? .att : failure?.domain == CBErrorDomain ? .bluetooth : .other
        service.bleDiagnostics.voiceLink(credits: voiceCredits, available: characteristic.properties.contains(.notify), subscribed: voiceNotifications, failure: domain, code: failure?.code ?? 0)
        if voiceSubscriptionRecovery.needsReadback(after: error) {
            // Read-only evidence: some peer stacks update the live CCCD before
            // a persistence error. Never treat readback as subscription success.
            voiceDescriptorDiscovery = true; deadline = now + 15
            peripheral.discoverDescriptors(for: characteristic); return
        }
        busy = false
    }
    func peripheral(_ peripheral: CBPeripheral, didDiscoverDescriptorsFor characteristic: CBCharacteristic, error: Error?) {
        if peer === peripheral, characteristic === characteristics[MacTab5BLEWire.rpc], rpcDescriptorDiscovery {
            rpcDescriptorDiscovery = false
            let descriptors = characteristic.descriptors?.filter { $0.uuid == CBUUID(string:CBUUIDClientCharacteristicConfigurationString) } ?? []
            guard error == nil, descriptors.count == 1 else {
                _ = rpcSubscriptionRecovery.shouldReconcile(nil); service.bleDiagnostics.rpcConfiguration(nil); busy = false; return
            }
            rpcDescriptor = descriptors[0]; deadline = now + 15; peripheral.readValue(for:descriptors[0]); return
        }
        guard peer === peripheral, characteristic === characteristics[MacTab5BLEWire.voice], voiceDescriptorDiscovery else { return }
        voiceDescriptorDiscovery = false
        let descriptors = characteristic.descriptors?.filter { $0.uuid == CBUUID(string: CBUUIDClientCharacteristicConfigurationString) } ?? []
        guard error == nil, descriptors.count == 1 else {
            _ = voiceSubscriptionRecovery.shouldReconcile(nil)
            service.bleDiagnostics.voiceConfiguration(nil); busy = false; return
        }
        voiceDescriptor = descriptors[0]; deadline = now + 15
        peripheral.readValue(for: descriptors[0])
    }
    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor descriptor: CBDescriptor, error: Error?) {
        if peer === peripheral, descriptor === rpcDescriptor {
            rpcDescriptor = nil
            let value = error == nil ? descriptor.value:nil; service.bleDiagnostics.rpcConfiguration(value)
            if rpcSubscriptionRecovery.shouldReconcile(value), enabled, admission.ready,
               admission.accepts(admission.generation,pair:service.currentPairing),let endpoint = characteristics[MacTab5BLEWire.rpc] {
                rpcSubscriptionPending = true; deadline = now + 15; peripheral.setNotifyValue(true,for:endpoint); return
            }
            busy = false; return
        }
        guard peer === peripheral, descriptor === voiceDescriptor else { return }
        voiceDescriptor = nil
        let value = error == nil ? descriptor.value : nil
        service.bleDiagnostics.voiceConfiguration(value)
        if voiceSubscriptionRecovery.shouldReconcile(value), enabled, admission.ready,
           admission.accepts(admission.generation, pair: service.currentPairing),
           let endpoint = characteristics[MacTab5BLEWire.voice] {
            // Same live connection, same enabled value; no descriptor write,
            // bond reset or user-operation replay. Callback success still gates grants.
            voiceSubscriptionPending = true; deadline = now + 15
            peripheral.setNotifyValue(true, for: endpoint); return
        }
        busy = false
    }
    private func read(_ characteristic: CBCharacteristic, completion: @escaping (Result<Data, Error>) -> Void) {
        service.bleDiagnostics.stage(.reading)
        let generation = admission.generation
        reading = characteristic; readCompletion = { [weak self] result in
            guard let self, self.enabled, self.admission.accepts(generation, pair: self.service.currentPairing) else { return }
            guard !self.busy || self.now <= self.deadline else { self.reset("蓝牙操作超时，等待重连"); return }
            completion(result)
        }
        peer?.readValue(for: characteristic)
    }
    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peer === peripheral else { return }
        if characteristic === characteristics[MacTab5BLEWire.rpc], rpcWindow != nil {
            do {
                if let error { throw error }
                guard !rpcGrantPending, let data = characteristic.value else { throw MacTab5Error.invalidPacket }
                try rpcWindow?.accept(data, now: now); service.bleDiagnostics.read(data.count); drainRPCWindow()
            } catch { reset("蓝牙数据通知无效，不重发操作") }
            return
        }
        if characteristic === characteristics[MacTab5BLEWire.voice], voiceWindow != nil {
            do {
                if let error { throw error }
                guard !voiceGrantPending else { throw MacTab5Error.invalidPacket }
                guard let data = characteristic.value else { throw MacTab5Error.invalidPacket }
                try voiceWindow?.accept(data, now: now); service.bleDiagnostics.read(data.count); service.bleDiagnostics.voiceNotification()
                drainVoiceWindow()
            } catch { reset("蓝牙语音通知无效，不重发操作") }
            return
        }
        guard reading === characteristic, let callback = readCompletion else {
            if rpcNotifications && characteristic === characteristics[MacTab5BLEWire.rpc] { reset("蓝牙数据通知不在授权窗口内") }
            if voiceNotifications && characteristic === characteristics[MacTab5BLEWire.voice] {
                reset("蓝牙语音通知不在授权窗口内")
            }
            return
        }
        if characteristic === characteristics[MacTab5BLEWire.voice] { lastVoiceProbe = now }
        if characteristic === characteristics[MacTab5BLEWire.rpc] { lastControlProbe = now }
        reading = nil; readCompletion = nil
        if let error { callback(.failure(error)) }
        else if let data = characteristic.value { service.bleDiagnostics.read(data.count); callback(.success(data)) }
        else { callback(.failure(MacTab5Error.invalidPacket)) }
    }
    private func write(_ values: [Data], to characteristic: CBCharacteristic, completion: @escaping (Result<Void, Error>) -> Void) {
        service.bleDiagnostics.stage(.writing)
        guard characteristic.properties.contains(.write), !values.isEmpty else { completion(.failure(MacTab5Error.unsupported)); return }
        let generation = admission.generation
        packets = values; packetIndex = 0; writing = characteristic; awaitingWrite = false
        writeCompletion = { [weak self] result in
            guard let self, self.enabled, self.admission.accepts(generation, pair: self.service.currentPairing) else { return }
            guard !self.busy || self.now <= self.deadline else { self.reset("蓝牙操作超时，等待重连"); return }
            completion(result)
        }
        writeNext()
    }
    private func writeNext() {
        guard let peer, let characteristic = writing, !awaitingWrite, !probingVoice else { return }
        guard admission.accepts(admission.generation, pair: service.currentPairing) else { reset("配对已改变，取消旧传输"); return }
        // Only probe at an acknowledged boundary, without advancing the mailbox
        // cursor or executing its request. A full voice exchange could exceed the
        // device's five-second partial-status limit; it stays after the full frame.
        if characteristic === characteristics[MacTab5BLEWire.input], admission.ready,
           MacTab5BLEVoiceWindow.shouldProbe(now: now, lastProbe: lastVoiceProbe, index: packetIndex, count: packets.count),
           let endpoint = characteristics[MacTab5BLEWire.voice] {
            probingVoice = true
            read(endpoint) { [weak self] result in
                guard let self else { return }
                do {
                    let bytes = try result.get()
                    guard (8...488).contains(bytes.count) else { throw MacTab5Error.invalidPacket }
                    self.probingVoice = false; self.writeNext()
                } catch { self.reset("蓝牙语音就绪检查失败") }
            }
            return
        }
        service.bleDiagnostics.stage(.writing)
        while packetIndex < packets.count {
            // Four no-response submissions followed by an ATT barrier. The final
            // packet always has a response; only the signed whole-frame ACK confirms data.
            let fast = characteristic.properties.contains(.writeWithoutResponse) && packetIndex % 5 != 4 && packetIndex + 1 < packets.count
            if fast && !peer.canSendWriteWithoutResponse { return }
            let bytes = packets[packetIndex]; packetIndex += 1
            awaitingWrite = !fast
            service.bleDiagnostics.submit(bytes.count)
            peer.writeValue(bytes, for: characteristic, type: fast ? .withoutResponse : .withResponse)
            if awaitingWrite { return }
        }
        let callback = writeCompletion; writing = nil; writeCompletion = nil; packets = []; callback?(.success(Void()))
    }
    func peripheralIsReady(toSendWriteWithoutResponse peripheral: CBPeripheral) {
        guard peer === peripheral else { return }
        if voiceGrantPending { submitVoiceGrant() } else if rpcGrantPending { submitRPCGrant() } else { writeNext() }
    }
    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peer === peripheral, writing === characteristic, awaitingWrite else { return }
        awaitingWrite = false
        if let error { let callback = writeCompletion; writing = nil; writeCompletion = nil; callback?(.failure(error)) }
        else { writeNext() }
    }
    private func writeMaximum(_ peer: CBPeripheral) -> Int {
        min(488, peer.maximumWriteValueLength(for: .withResponse), peer.maximumWriteValueLength(for: .withoutResponse))
    }
    private func sendStatus(_ pair: MacTab5Pairing, resources: Bool = false) {
        guard let peer, let input = characteristics[MacTab5BLEWire.input], let nonce else { reset("蓝牙状态不可用"); return }
        do {
            // Keep image bytes out of periodic status even when the dedicated
            // resource turn is deferred for voice. A single idle image chunk
            // also bounds the work ahead of a newly arriving start request.
            let clear = try (resources ? service.resourceFrame(maximum: 1) : service.frame(includeResources: false))
            let diagnostic = service.bleDiagnostics.begin(resources ? .resource : .status)
            let root = try MacTab5Protocol.object(clear)
            guard let sequence = MacTab5Protocol.integer(root["sequence"]) else { throw MacTab5Error.invalidPacket }
            let wire = resources ? clear : try statusDelta.wire(clear, packed: packedStatus, supported: deltaStatus)
            let packet = try MacTab5Protocol.encrypt(wire, key: pair.key, challenge: nonce), generation = admission.generation
            write(try MacTab5BLEWire.fragments(packet, maximum: writeMaximum(peer), lengthPrefix: true), to: input) { [weak self] result in
                guard let self else { return }
                do { try result.get(); self.confirmFrame(pair, nonce: nonce, sequence: sequence, generation: generation, attempts: 0, resources: resources, full: clear, diagnostic: diagnostic) }
                catch { self.reset("蓝牙发送未确认，等待重连") }
            }
        } catch MacTab5Error.busy { busy = false }
        catch { reset("蓝牙状态帧无效") }
    }
    private func confirmFrame(_ pair: MacTab5Pairing, nonce: String, sequence: Int64, generation: UInt64, attempts: Int, resources: Bool, full: Data, diagnostic: UInt64) {
        guard self.nonce == nonce, authenticatedDevice == pair.deviceId, enabled, admission.ready,
              admission.accepts(generation, pair: service.currentPairing) else { return }
        guard let info = characteristics[MacTab5BLEWire.info] else { reset("蓝牙服务已断开"); return }
        read(info) { [weak self] result in
            guard let self else { return }
            do {
                let (_, ack, root) = try MacTab5BLEWire.authenticate(result.get(), pair: pair, nonce: nonce)
                if ack != sequence {
                    guard attempts < 15 else { throw MacTab5Error.unconfirmed }
                    self.queue.asyncAfter(deadline: .now() + 0.1) { [weak self] in self?.confirmFrame(pair, nonce: nonce, sequence: sequence, generation: generation, attempts: attempts + 1, resources: resources, full: full, diagnostic: diagnostic) }; return
                }
                self.service.observeBLEInfo(root, nonce: nonce)
                self.packedStatus = MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce)
                self.deltaStatus = MacTab5BLEDelta.supports(root, pair: pair, nonce: nonce)
                self.service.bleDiagnostics.finish(diagnostic, success: true)
                if !resources { self.statusDelta.acknowledge(full, sequence: sequence); self.lastFrame = self.now }
                self.service.bleState("数据已确认", confirmed: true)
                // A slow background frame must not age out the voice lease and
                // immediately queue another resource frame ahead of captured audio.
                if !self.service.usbOwnsActiveState, let endpoint = self.characteristics[MacTab5BLEWire.voice] {
                    self.pump(endpoint, pair: pair)
                } else { self.busy = false }
            } catch { self.reset("蓝牙整帧确认失败，等待重连") }
        }
    }
    private func pump(_ endpoint: CBCharacteristic, pair: MacTab5Pairing) {
        if endpoint.uuid == MacTab5BLEWire.voice { deadline = now + 12 }
        let diagnostic = service.bleDiagnostics.begin(endpoint.uuid == MacTab5BLEWire.voice ? .voice : .rpc)
        read(endpoint) { [weak self] result in
            guard let self else { return }
            do {
                let first = try result.get()
                guard (8...488).contains(first.count) else { throw MacTab5Error.invalidPacket }
                let id = MacTab5Mailbox.u32(first, 0), total = Int(MacTab5Mailbox.u16(first, 6))
                if id == 0 || self.completed[endpoint.uuid] == id { self.service.bleDiagnostics.discard(diagnostic); self.finishMailbox(); return }
                let maximum = endpoint.uuid == MacTab5BLEWire.voice ? 12288 : MacTab5Protocol.maximumRPC + 96
                guard (124...maximum).contains(total) else { throw MacTab5Error.invalidPacket }
                self.receiveRequest(first, id: id, total: total, bytes: Data(), endpoint: endpoint, pair: pair, diagnostic: diagnostic)
            } catch { self.reset("蓝牙请求读取失败，等待重连") }
        }
    }
    private func receiveRequest(_ part: Data, id: UInt32, total: Int, bytes: Data, endpoint: CBCharacteristic, pair: MacTab5Pairing, diagnostic: UInt64) {
        do {
            var request = bytes; try MacTab5Mailbox.append(part, id: id, total: total, to: &request)
            continueRequest(request, id: id, total: total, endpoint: endpoint, pair: pair, diagnostic: diagnostic)
        } catch { reset("蓝牙请求无效，不执行操作") }
    }
    private func continueRequest(_ request: Data, id: UInt32, total: Int, endpoint: CBCharacteristic, pair: MacTab5Pairing, diagnostic: UInt64) {
        do {
            if request.count < total {
                if endpoint.uuid == MacTab5BLEWire.rpc && rpcNotifications {
                    try requestRPCWindow(request, id: id, total: total, endpoint: endpoint, pair: pair, diagnostic: diagnostic)
                    return
                }
                if endpoint.uuid == MacTab5BLEWire.voice && voiceNotifications {
                    try requestVoiceWindow(request, id: id, total: total, endpoint: endpoint, pair: pair, diagnostic: diagnostic)
                    return
                }
                var ack = Data([1]); MacTab5Mailbox.append32(id, to: &ack); MacTab5Mailbox.append16(request.count, to: &ack)
                write([ack], to: endpoint) { [weak self] result in
                    guard let self else { return }
                    do { try result.get(); self.read(endpoint) { [weak self] next in
                        do { self?.receiveRequest(try next.get(), id: id, total: total, bytes: request, endpoint: endpoint, pair: pair, diagnostic: diagnostic) }
                        catch { self?.reset("蓝牙请求分片失败") }
                    } } catch { self.reset("蓝牙分片确认失败") }
                }; return
            }
            let array = [UInt8](request)
            guard let nonce = String(bytes: array[0..<32], encoding: .ascii), let proof = String(bytes: array[32..<96], encoding: .ascii), let peer else { throw MacTab5Error.invalidPacket }
            service.bleDiagnostics.stage(.processing)
            let reply = endpoint.uuid == MacTab5BLEWire.voice
                ? service.voicePacket(device: pair.deviceId, nonce: nonce, proof: proof, packet: Data(array[96...]), transport: "BLE")
                : service.rpc(device: pair.deviceId, nonce: nonce, proof: proof, packet: Data(array[96...]), transport: "BLE")
            guard reply.0 == 200 else { throw MacTab5Error.invalidPacket }
            let size = writeMaximum(peer) - 9
            guard size > 0 else { throw MacTab5Error.invalidPacket }
            let fragments = stride(from: 0, to: reply.1.count, by: size).map { offset -> Data in
                var packet = Data([2]); MacTab5Mailbox.append32(id, to: &packet); MacTab5Mailbox.append16(offset, to: &packet); MacTab5Mailbox.append16(reply.1.count, to: &packet)
                packet.append(reply.1[offset..<min(offset + size, reply.1.count)]); return packet
            }
            write(fragments, to: endpoint) { [weak self] result in
                do {
                    try result.get(); guard let self else { return }
                    self.service.bleDiagnostics.finish(diagnostic, success: true); self.completed[endpoint.uuid] = id
                    if endpoint.uuid == MacTab5BLEWire.voice { self.lastVoiceActivity = self.now }
                    self.finishMailbox(completedVoice: endpoint.uuid == MacTab5BLEWire.voice,completedControl:endpoint.uuid == MacTab5BLEWire.rpc)
                }
                catch { self?.reset("蓝牙回复未确认，不自动重发操作") }
            }
        } catch { reset("蓝牙请求无效，不执行操作") }
    }
    private func finishMailbox(completedVoice: Bool = false,completedControl:Bool = false) {
        busy = false
        if completedControl { lastControlActivity = now }
        let voiceDelay = MacTab5BLEVoicePacing.continuationDelay(now:now,lastVoice:lastVoiceActivity,completedVoice:completedVoice)
        guard let delay = voiceDelay ?? (completedControl ? 0.02:nil) else { return }
        let generation = admission.generation
        queue.asyncAfter(deadline: .now() + delay) { [weak self] in
            guard let self, self.admission.generation == generation else { return }; self.tick()
        }
    }
    private func requestRPCWindow(_ prefix: Data, id: UInt32, total: Int, endpoint: CBCharacteristic, pair: MacTab5Pairing, diagnostic: UInt64) throws {
        guard rpcWindow == nil else { throw MacTab5Error.invalidPacket }
        let window = try MacTab5BLERPCWindow(id:id,total:total,prefix:prefix,credits:rpcCredits,now:now)
        rpcWindow = window
        rpcWindowCompletion = { [weak self] bytes in
            self?.continueRequest(bytes,id:id,total:total,endpoint:endpoint,pair:pair,diagnostic:diagnostic)
        }
        if endpoint.properties.contains(.writeWithoutResponse), let peer,
           peer.maximumWriteValueLength(for:.withoutResponse) >= window.grant.count {
            rpcGrantPending = true; submitRPCGrant(); return
        }
        write([window.grant],to:endpoint) { [weak self] result in
            guard let self else { return }
            do { try result.get(); try self.rpcWindow?.markGrantSubmitted(now:self.now); self.drainRPCWindow() }
            catch { self.reset("蓝牙数据窗口未确认，不重发操作") }
        }
    }
    private func submitRPCGrant() {
        guard rpcGrantPending, let window = rpcWindow, let peer,
              let endpoint = characteristics[MacTab5BLEWire.rpc] else { return }
        guard enabled, admission.ready, admission.accepts(admission.generation,pair:service.currentPairing) else {
            reset("配对已改变，取消旧传输"); return
        }
        guard peer.canSendWriteWithoutResponse else { return }
        do {
            try rpcWindow?.markGrantSubmitted(now:now); rpcGrantPending = false
            service.bleDiagnostics.submit(window.grant.count)
            peer.writeValue(window.grant,for:endpoint,type:.withoutResponse)
            service.bleDiagnostics.stage(.reading); drainRPCWindow()
        } catch { reset("蓝牙数据窗口未确认，不重发操作") }
    }
    private func drainRPCWindow() {
        guard let window = rpcWindow, window.ready else { return }
        let completion = rpcWindowCompletion; rpcWindow = nil; rpcWindowCompletion = nil
        completion?(window.bytes)
    }
    private func requestVoiceWindow(_ prefix: Data, id: UInt32, total: Int, endpoint: CBCharacteristic, pair: MacTab5Pairing, diagnostic: UInt64) throws {
        guard voiceWindow == nil else { throw MacTab5Error.invalidPacket }
        let window = try MacTab5BLEVoiceWindow(id: id, total: total, prefix: prefix, credits: voiceCredits, now: now)
        voiceWindow = window
        voiceWindowCompletion = { [weak self] bytes in
            self?.continueRequest(bytes, id: id, total: total, endpoint: endpoint, pair: pair, diagnostic: diagnostic)
        }
        if endpoint.properties.contains(.writeWithoutResponse),
           let peer, peer.maximumWriteValueLength(for: .withoutResponse) >= window.grant.count {
            voiceGrantPending = true; service.bleDiagnostics.stage(.writing)
            submitVoiceGrant(); return
        }
        service.bleDiagnostics.voiceGrant()
        write([window.grant], to: endpoint) { [weak self] result in
            guard let self else { return }
            do {
                try result.get(); try self.voiceWindow?.markGrantSubmitted(now: self.now)
                self.service.bleDiagnostics.stage(.reading); self.drainVoiceWindow()
            } catch { self.reset("蓝牙语音窗口未确认，不重发操作") }
        }
    }
    private func submitVoiceGrant() {
        guard voiceGrantPending, let window = voiceWindow, let peer,
              let endpoint = characteristics[MacTab5BLEWire.voice] else { return }
        guard enabled, admission.ready, admission.accepts(admission.generation, pair: service.currentPairing) else {
            reset("配对已改变，取消旧传输"); return
        }
        guard peer.canSendWriteWithoutResponse else { return }
        do {
            try voiceWindow?.markGrantSubmitted(now: now)
            voiceGrantPending = false
            service.bleDiagnostics.voiceGrant(command: true); service.bleDiagnostics.submit(window.grant.count)
            peer.writeValue(window.grant, for: endpoint, type: .withoutResponse)
            // Local submission is not peer acceptance. Only the complete,
            // exact id/cursor/length-checked notification window advances work.
            service.bleDiagnostics.stage(.reading); drainVoiceWindow()
        } catch { reset("蓝牙语音窗口未确认，不重发操作") }
    }
    private func drainVoiceWindow() {
        guard let window = voiceWindow, window.ready else { return }
        let completion = voiceWindowCompletion; voiceWindow = nil; voiceWindowCompletion = nil
        completion?(window.bytes)
    }

}
