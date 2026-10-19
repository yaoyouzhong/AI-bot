import CoreBluetooth
import Foundation

enum MacTab5BLEWire {
    static let service = CBUUID(string: "7af50001-7f23-4a91-bc65-667a19320101")
    static let info = CBUUID(string: "7af50002-7f23-4a91-bc65-667a19320101")
    static let input = CBUUID(string: "7af50003-7f23-4a91-bc65-667a19320101")
    static let voice = CBUUID(string: "7af50004-7f23-4a91-bc65-667a19320101")
    static let rpc = CBUUID(string: "7af50005-7f23-4a91-bc65-667a19320101")

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
    private var deadline: TimeInterval = 0
    private var nextScan: TimeInterval = 0
    private var lastFrame: TimeInterval = -.infinity
    private var busy = false
    private var readCompletion: ((Result<Data, Error>) -> Void)?
    private var reading: CBUUID?
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
            } else { self.central?.stopScan(); self.reset("蓝牙已关闭"); self.timer?.cancel(); self.timer = nil }
        }
    }
    func stop() { setEnabled(false) }
    private var now: TimeInterval { ProcessInfo.processInfo.systemUptime }
    private func reset(_ message: String) {
        readCompletion = nil; writeCompletion = nil; reading = nil; writing = nil; packets = []; awaitingWrite = false
        busy = false; nonce = nil; authenticatedDevice = nil; characteristics = [:]; completed = [:]
        if let peer { central?.cancelPeripheralConnection(peer) }
        peer = nil; lastFrame = -.infinity; nextScan = now + 3
        service.bleState(message)
    }
    private func tick() {
        guard enabled else { return }
        if busy && now > deadline { reset("蓝牙操作超时，等待重连"); return }
        guard central?.state == .poweredOn, let pair = service.currentPairing, pair.confirmed else { return }
        if peer == nil {
            if now >= nextScan { central?.scanForPeripherals(withServices: [MacTab5BLEWire.service]); nextScan = now + 8 }
            return
        }
        guard nonce != nil, !busy else { return }
        guard authenticatedDevice == pair.deviceId else { reset("配对已改变，重新认证"); return }
        // USB remains preferred. No replay or channel switching of an in-flight RPC.
        guard !service.usbActive else { service.bleState("已认证 · USB 优先待命"); return }
        busy = true; deadline = now + 75
        if now - lastFrame >= 2 { sendStatus(pair) }
        else if service.assets.hasPending && resourceTurn { resourceTurn = false; sendStatus(pair, resources: true) }
        else {
            resourceTurn = true
            let uuid = voiceTurn ? MacTab5BLEWire.voice : MacTab5BLEWire.rpc; voiceTurn.toggle()
            if let endpoint = characteristics[uuid] { pump(endpoint, pair: pair) }
            else { busy = false }
        }
    }
    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        switch central.state {
        case .poweredOn: nextScan = 0; service.bleState("正在寻找已配对 TAB5")
        case .unauthorized: reset("蓝牙权限被拒绝，请在系统设置中允许 AI-bot")
        case .poweredOff: reset("请打开 Mac 蓝牙")
        case .unsupported: reset("这台 Mac 不支持低功耗蓝牙")
        default: reset("蓝牙正在准备")
        }
    }
    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        guard enabled, peer == nil, service.currentPairing?.confirmed == true else { return }
        central.stopScan(); peer = peripheral; peripheral.delegate = self
        busy = true; deadline = now + 15; central.connect(peripheral)
    }
    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) { if peer === peripheral { peripheral.discoverServices(nil) } }
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
        read(info) { [weak self] result in
            guard let self else { return }
            do {
                let (nonce, _, root) = try MacTab5BLEWire.authenticate(result.get(), pair: pair)
                self.nonce = nonce; self.authenticatedDevice = pair.deviceId; self.service.observeBLEInfo(root, nonce: nonce)
                self.busy = false; self.service.bleState("设备已认证，等待数据确认")
            } catch { self.reset("蓝牙配对认证失败，请通过 USB 重新核对配对") }
        }
    }
    private func read(_ characteristic: CBCharacteristic, completion: @escaping (Result<Data, Error>) -> Void) {
        reading = characteristic.uuid; readCompletion = completion; peer?.readValue(for: characteristic)
    }
    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peer === peripheral, reading == characteristic.uuid, let callback = readCompletion else { return }
        reading = nil; readCompletion = nil
        if let error { callback(.failure(error)) }
        else if let data = characteristic.value { callback(.success(data)) }
        else { callback(.failure(MacTab5Error.invalidPacket)) }
    }
    private func write(_ values: [Data], to characteristic: CBCharacteristic, completion: @escaping (Result<Void, Error>) -> Void) {
        guard characteristic.properties.contains(.write), !values.isEmpty else { completion(.failure(MacTab5Error.unsupported)); return }
        packets = values; packetIndex = 0; writing = characteristic; writeCompletion = completion; awaitingWrite = false; writeNext()
    }
    private func writeNext() {
        guard let peer, let characteristic = writing, !awaitingWrite else { return }
        while packetIndex < packets.count {
            // Four no-response submissions followed by an ATT barrier. The final
            // packet always has a response; only the signed whole-frame ACK confirms data.
            let fast = characteristic.properties.contains(.writeWithoutResponse) && packetIndex % 5 != 4 && packetIndex + 1 < packets.count
            if fast && !peer.canSendWriteWithoutResponse { return }
            let bytes = packets[packetIndex]; packetIndex += 1
            awaitingWrite = !fast
            peer.writeValue(bytes, for: characteristic, type: fast ? .withoutResponse : .withResponse)
            if awaitingWrite { return }
        }
        let callback = writeCompletion; writing = nil; writeCompletion = nil; packets = []; callback?(.success(Void()))
    }
    func peripheralIsReady(toSendWriteWithoutResponse peripheral: CBPeripheral) { if peer === peripheral { writeNext() } }
    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard peer === peripheral, writing?.uuid == characteristic.uuid, awaitingWrite else { return }
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
            let clear = try (resources ? service.resourceFrame() : service.frame()), root = try MacTab5Protocol.object(clear)
            guard let sequence = MacTab5Protocol.integer(root["sequence"]) else { throw MacTab5Error.invalidPacket }
            let packet = try MacTab5Protocol.encrypt(clear, key: pair.key, challenge: nonce)
            write(try MacTab5BLEWire.fragments(packet, maximum: writeMaximum(peer), lengthPrefix: true), to: input) { [weak self] result in
                guard let self else { return }
                do { try result.get(); self.confirmFrame(pair, nonce: nonce, sequence: sequence, attempts: 0, resources: resources) }
                catch { self.reset("蓝牙发送未确认，等待重连") }
            }
        } catch { reset("蓝牙状态帧无效") }
    }
    private func confirmFrame(_ pair: MacTab5Pairing, nonce: String, sequence: Int64, attempts: Int, resources: Bool) {
        guard self.nonce == nonce, authenticatedDevice == pair.deviceId, enabled else { return }
        guard let info = characteristics[MacTab5BLEWire.info] else { reset("蓝牙服务已断开"); return }
        read(info) { [weak self] result in
            guard let self else { return }
            do {
                let (_, ack, root) = try MacTab5BLEWire.authenticate(result.get(), pair: pair, nonce: nonce)
                if ack != sequence {
                    guard attempts < 15 else { throw MacTab5Error.unconfirmed }
                    self.queue.asyncAfter(deadline: .now() + 0.1) { [weak self] in self?.confirmFrame(pair, nonce: nonce, sequence: sequence, attempts: attempts + 1, resources: resources) }; return
                }
                self.service.observeBLEInfo(root, nonce: nonce)
                if !resources { self.lastFrame = self.now }
                self.busy = false; self.service.bleState("数据已确认", confirmed: true)
            } catch { self.reset("蓝牙整帧确认失败，等待重连") }
        }
    }
    private func pump(_ endpoint: CBCharacteristic, pair: MacTab5Pairing) {
        read(endpoint) { [weak self] result in
            guard let self else { return }
            do {
                let first = try result.get()
                guard first.count >= 8 else { throw MacTab5Error.invalidPacket }
                let id = MacTab5Mailbox.u32(first, 0), total = Int(MacTab5Mailbox.u16(first, 6))
                if id == 0 || self.completed[endpoint.uuid] == id { self.busy = false; return }
                guard (124...(MacTab5Protocol.maximumRPC + 96)).contains(total) else { throw MacTab5Error.invalidPacket }
                self.receiveRequest(first, id: id, total: total, bytes: Data(), endpoint: endpoint, pair: pair)
            } catch { self.reset("蓝牙请求读取失败，等待重连") }
        }
    }
    private func receiveRequest(_ part: Data, id: UInt32, total: Int, bytes: Data, endpoint: CBCharacteristic, pair: MacTab5Pairing) {
        do {
            var request = bytes; try MacTab5Mailbox.append(part, id: id, total: total, to: &request)
            if request.count < total {
                var ack = Data([1]); MacTab5Mailbox.append32(id, to: &ack); MacTab5Mailbox.append16(request.count, to: &ack)
                write([ack], to: endpoint) { [weak self] result in
                    guard let self else { return }
                    do { try result.get(); self.read(endpoint) { [weak self] next in
                        do { self?.receiveRequest(try next.get(), id: id, total: total, bytes: request, endpoint: endpoint, pair: pair) }
                        catch { self?.reset("蓝牙请求分片失败") }
                    } } catch { self.reset("蓝牙分片确认失败") }
                }; return
            }
            let array = [UInt8](request)
            guard let nonce = String(bytes: array[0..<32], encoding: .ascii), let proof = String(bytes: array[32..<96], encoding: .ascii), let peer else { throw MacTab5Error.invalidPacket }
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
                do { try result.get(); self?.completed[endpoint.uuid] = id; self?.busy = false }
                catch { self?.reset("蓝牙回复未确认，不自动重发操作") }
            }
        } catch { reset("蓝牙请求无效，不执行操作") }
    }
}
