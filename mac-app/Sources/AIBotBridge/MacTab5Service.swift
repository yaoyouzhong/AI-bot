import Foundation
import CryptoKit

final class MacTab5Service {
    typealias DisplayCompletion = (Result<MacTab5DisplaySettings, Error>) -> Void
    private let lock = NSLock()
    private let localDiagnostics: MacTab5LocalDiagnostics?
    private let loadPairing: () throws -> MacTab5Pairing?
    private let savePairing: (MacTab5Pairing) throws -> Void
    private let host: () -> String?
    private let latestMusic: () -> (snapshot: MusicSnapshot?, cover: Data?)?
    private let deviceEnabled: () -> Bool
    private let sourceEnabled: (String) -> Bool
    let port: UInt16
    let session = MacTab5Protocol.identifier()
    private var pairing: MacTab5Pairing?
    private var pairingReadable = true
    private var snapshot: MacStatusSnapshot?
    private var sequence: Int64 = 0
    private var musicFrames = 0, unavailableMusicFrames = 0
    private var usbConfirmedSessions = 0, usbInterruptions = 0
    private var usbRecentFailures: [(TimeInterval,String)] = []
    private var usbOperationStarted: TimeInterval?, usbOperationLast = 0.0, usbOperationMax = 0.0
    private var usbOperationHeartbeats = 0
    private var maximumMusicAge: TimeInterval = 0
    private var lanReady = false
    private var stopped = false
    private var maintenance = false
    private var benchmarkMode = 0
    private var lastUSB: TimeInterval = -.infinity
    private var usbReportedActive: String?
    private var lastWiFi: TimeInterval = -.infinity
    private var lastBLE: TimeInterval = -.infinity
    private var bleNote = "尚未启用"
    private var firmware: String?
    private var firmwareDevice: String?
    private var usbNote = "尚未连接"
    private var usbAuthenticationRejected = false
    private var discoveryNote = "等待启动"
    private var nonces: [String: TimeInterval] = [:]
    private let display = MacTab5DisplayTransaction()
    let assets = MacTab5Assets()
    let gallery: MacTab5Gallery
    let computer: MacTab5Computer
    let unlock: MacHostUnlock
    let codex: MacTab5Codex
    let voice: MacTab5Voice
    let desktop: MacTab5Desktop
    let birthdays: MacBirthdays
    let agenda: MacAgenda
    let interactionDiagnostics = MacTab5InteractionDiagnostics()
    let bleDiagnostics = MacTab5BLEDiagnostics()
    let deviceDiagnostics = MacTab5RuntimeDiagnostics()
    let screenPreview = MacTab5ScreenPreview()
    let firmwareControl = MacTab5Firmware()
    private let runtimeHealth = MacRuntimeHealth()
    private let networkDiagnostics: () -> MacNetworkDiagnosticSample?
    private var activeOperations = 0
    private var displayCompletion: DisplayCompletion?

    init(port: UInt16, host: @escaping () -> String? = LocalNetworkIdentity.privateIPv4, gallery: MacTab5Gallery = MacTab5Gallery(), computer: MacTab5Computer = MacTab5Computer(), codex: MacTab5Codex = MacTab5Codex(), voice: MacTab5Voice = MacTab5Voice(), birthdays: MacBirthdays = .shared, agenda: MacAgenda = .shared,
         networkDiagnostics: @escaping () -> MacNetworkDiagnosticSample? = { nil }, unlock: MacHostUnlock = MacHostUnlock(),
         music: @escaping () -> (snapshot: MusicSnapshot?, cover: Data?)? = { nil },
         enabled: @escaping () -> Bool = { true }, sourceEnabled: @escaping (String) -> Bool = { _ in true },
         load: @escaping () throws -> MacTab5Pairing? = { try MacTab5PairingStore().load() },
         save: @escaping (MacTab5Pairing) throws -> Void = { try MacTab5PairingStore().save($0) }, diagnostics: MacTab5LocalDiagnostics? = nil) {
        self.localDiagnostics = diagnostics
        self.port = port; self.host = host; self.latestMusic = music; self.gallery = gallery; self.computer = computer; self.codex = codex; self.voice = voice; loadPairing = load; savePairing = save
        self.networkDiagnostics = networkDiagnostics; self.unlock = unlock
        self.deviceEnabled = enabled; self.sourceEnabled = sourceEnabled
        self.birthdays = birthdays; self.agenda = agenda
        desktop = MacTab5Desktop(codex: codex, diagnostics: diagnostics)
        interactionDiagnostics.localDiagnostics = diagnostics
        assets.diagnostics = diagnostics.map { log in { log.cover($0) } }
        codex.taskAppend = { [weak desktop] task, text, authorized, captureBaseline in
            guard let desktop else { throw MacCodexDesktopError.rejected("desktop_owner_unavailable") }
            try desktop.appendTask(task: task, text: text, authorized: authorized, captureBaseline: captureBaseline)
        }
        voice.beforeStart = { [weak desktop] task, device in desktop?.prepareVoice(task: task, device: device) }
        computer.unlockConfigured = { [unlock] in unlock.hasCredential }
        do { pairing = try load(); try pairing?.validate() }
        catch { pairingReadable = false; usbNote = "无法读取配对记录，请检查钥匙串权限" }
    }

    var currentPairing: MacTab5Pairing? { lock.lock(); defer { lock.unlock() }; return pairing }
    var isEnabled: Bool { deviceEnabled() }
    private var unlockSuspended:Bool { lock.lock(); defer { lock.unlock() }; return stopped || maintenance || benchmarkMode != 0 }
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
        if value && (benchmarkMode != 0 || activeOperations > 0 || displayCompletion != nil || firmwareControl.busy) { lock.unlock(); return false }
        maintenance = value; lock.unlock(); if value { voice.stop(); deviceDiagnostics.cancel(); screenPreview.stop(); unlock.cancel() }; return true
    }
    var usbActive: Bool { lock.lock(); defer { lock.unlock() }; return ProcessInfo.processInfo.systemUptime - lastUSB < 8 }
    var usbOwnsActiveState: Bool {
        lock.lock(); defer { lock.unlock() }
        return !usbAuthenticationRejected && MacTab5USBDisplayOwnership.owns(
            now: ProcessInfo.processInfo.systemUptime, acknowledgedAt: lastUSB, active: usbReportedActive)
    }
    var configurationBusy: Bool { lock.lock(); defer { lock.unlock() }; return benchmarkMode != 0 || displayCompletion != nil || activeOperations > 0 || firmwareControl.busy }
    var bootTransport: String? {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        return now - lastWiFi < 8 ? "Wi-Fi" : now - lastBLE < 8 ? "BLE" : nil
    }
    func beginBenchmark(gallery:Bool) throws {
        lock.lock(); defer { lock.unlock() }
        guard !stopped, !maintenance, benchmarkMode == 0, displayCompletion == nil, activeOperations == 0, !firmwareControl.busy, !voice.busy else { throw MacTab5Error.busy }
        benchmarkMode = gallery ? 2:1
    }
    func endBenchmark() { lock.lock(); benchmarkMode = 0; lock.unlock() }
    private var currentBenchmark: Int { lock.lock(); defer { lock.unlock() }; return benchmarkMode }
    func requestUpgrade() throws {
        lock.lock(); defer { lock.unlock() }
        guard benchmarkMode == 0 else { throw MacTab5Error.busy }
        guard !usbAuthenticationRejected else { throw MacTab5LocalError.invalid("USB 已连接，但此 Mac 未通过设备认证；请先通过 USB 验证配对，固件尚未传输。") }
        let now = ProcessInfo.processInfo.systemUptime
        guard deviceEnabled(), !stopped, !maintenance, activeOperations == 0, now - lastUSB < 8 || now - lastWiFi < 8 || now - lastBLE < 8, let pair = pairing, pair.confirmed, let version = firmware, let image = firmwareControl.selected,
              displayCompletion == nil else { throw MacTab5LocalError.invalid("请先连接 TAB5、选择固件并完成当前操作。") }
        let parts = version.replacingOccurrences(of: "-ui", with: "").split(separator: ".").compactMap { Int($0) }
        guard parts.count == 3, parts.lexicographicallyPrecedes([0, 2, 190]) == false else { throw MacTab5LocalError.invalid("此设备需先在 TAB5 上确认一次升级，之后才支持电脑端开始升级。") }
        guard version != image.version || Self.supportsReinstall(version) else { throw MacTab5LocalError.invalid("设备固件尚不支持同版本重装，请先升级到 0.2.286-ui 或更新版本。") }
        try firmwareControl.start(device: pair.deviceId, reinstall: version == image.version)
    }
    static func supportsReinstall(_ version:String) -> Bool {
        let parts = version.split(separator:".")
        guard parts.count == 3, parts[0] == "0", parts[1] == "2", parts[2].hasSuffix("-ui"),
              let patch = Int(parts[2].dropLast(3)) else { return false }
        return patch >= 286
    }
    struct ConnectionStatus {
        let device: String
        let firmware: String
        let usb: String
        let wifi: String
        let bluetooth: String
        let discovery: String
        let confirmation: String
    }
    var connectionStatus: ConnectionStatus {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        let device = pairing.map { "TAB5 · \($0.deviceId)" } ?? "尚未配对 TAB5"
        let usb = now - lastUSB < 8 ? "数据已确认" : usbNote
        let wifi = !lanReady ? "LAN 服务未就绪" : now - lastWiFi < 8 ? "收到已认证请求" : "等待设备连接"
        let confirmation = pairing?.confirmed == false ? "\nUSB 配对结果未确认，请重新配对。" : ""
        return ConnectionStatus(device: device, firmware: firmware ?? "未知", usb: usb, wifi: wifi,
                                bluetooth: bleNote, discovery: discoveryNote, confirmation: confirmation)
    }
    var connectionSummary: String {
        let status = connectionStatus
        return "\(status.device)\nUSB：\(status.usb)\nWi-Fi：\(status.wifi)\n蓝牙：\(status.bluetooth)\n自动发现：\(status.discovery)\(status.confirmation)\n\(hostBindingSummary)"
    }
    var hostBindingSummary: String {
        lock.lock(); defer { lock.unlock() }
        if usbAuthenticationRejected { return "此 Mac 未通过设备配对认证，请通过 USB 验证配对。" }
        return ProcessInfo.processInfo.systemUptime - lastUSB < 8 ? "当前电脑：\(MacTab5HostBinding.name(ProcessInfo.processInfo.hostName))（USB 已确认）":"电脑归属：由设备选择；其他电脑无线待命。"
    }
    func rejectUSBHost() { lock.lock(); usbAuthenticationRejected = true; lock.unlock() }
    var screenPreviewContext: MacTab5ScreenPreview.Context? {
        lock.lock(); defer { lock.unlock() }
        guard let pair = pairing, pair.confirmed else { return nil }
        let now = ProcessInfo.processInfo.systemUptime
        var transports = Set<String>()
        if lastUSB > 0, now >= lastUSB, now-lastUSB < 8, !usbAuthenticationRejected { transports.insert("USB") }
        if lastWiFi > 0, now >= lastWiFi, now-lastWiFi < 8 { transports.insert("Wi-Fi") }
        if lastBLE > 0, now >= lastBLE, now-lastBLE < 8 { transports.insert("BLE") }
        return .init(device:pair.deviceId,host:MacTab5HostBinding.id(pair),transports:transports,
                     available:deviceEnabled() && !stopped && !maintenance,
                     allowsCapture:MacTab5ScreenPreview.allowsCapture(firmware:firmware),
                     highDefinition:MacTab5ScreenPreview.supportsHD(firmware:firmware),
                     responsiveCapture:MacTab5ScreenPreview.supportsResponsiveCapture(firmware:firmware))
    }
    var deviceDiagnosticContext: MacTab5RuntimeDiagnostics.Context? {
        lock.lock(); defer { lock.unlock() }
        return diagnosticContextLocked()
    }
    private func diagnosticContextLocked() -> MacTab5RuntimeDiagnostics.Context? {
        guard let pair = pairing, pair.confirmed else { return nil }
        let now = ProcessInfo.processInfo.systemUptime
        var transports = Set<String>()
        if now - lastWiFi < 8 { transports.insert("Wi-Fi") }
        if now - lastBLE < 8 { transports.insert("BLE") }
        return .init(device:pair.deviceId,host:MacTab5HostBinding.id(pair),firmware:firmware ?? "",transports:transports,
                     available:deviceEnabled() && !stopped && !maintenance)
    }
    var deviceDiagnosticSummary: String { deviceDiagnostics.summary(context:deviceDiagnosticContext) }
    func refreshDeviceDiagnostics(completion: @escaping () -> Void = {}) {
        deviceDiagnostics.refresh(context:{ [weak self] in self?.deviceDiagnosticContext },completion:completion)
    }
    var lanConfiguration: (String, UInt16)? {
        lock.lock(); let ready = lanReady && !stopped; lock.unlock()
        guard ready, let address = host(), PrivateIPv4Address.isValid(address) else { return nil }
        // Enumeration must never hold the shared transport/state lock. Recheck
        // availability afterward so a stopped listener cannot publish an endpoint.
        lock.lock(); defer { lock.unlock() }
        guard lanReady, !stopped else { return nil }
        return (address, port)
    }
    func setLANReady(_ ready: Bool) { lock.lock(); lanReady = ready && !stopped; if !lanReady { lastWiFi = -.infinity }; lock.unlock() }
    func setDiscoveryState(_ note: String) { lock.lock(); discoveryNote = note; lock.unlock() }
    func bleState(_ note: String, confirmed: Bool = false) {
        lock.lock(); bleNote = note; lastBLE = confirmed ? ProcessInfo.processInfo.systemUptime : -.infinity; lock.unlock()
        if !confirmed { unlock.cancel(transport:"BLE") }
    }
    func observeBLEInfo(_ root: [String: Any], nonce: String) {
        lock.lock(); defer { lock.unlock() }
        guard let pair = pairing else { return }
        if let ids = root["assetIds"] as? String, let proof = root["assetProof"] as? String,
           MacTab5Protocol.verify(key: pair.key, message: "ASSETS|\(nonce)|\(ids)", proof: proof) { assets.acknowledge(ids) }
        guard let version = root["firmware"] as? String, version.utf8.count <= 40,
              let proof = root["firmwareProof"] as? String,
              MacTab5Protocol.verify(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)", proof: proof) else { return }
        firmware = version; firmwareDevice = pair.deviceId
        firmwareControl.observeVersion(version)
    }
    func observeUSBInfo(_ root: [String: Any]) {
        lock.lock(); defer { lock.unlock() }
        guard root["deviceId"] as? String == pairing?.deviceId else { return }
        localDiagnostics?.device(root)
        usbReportedActive = (root["active"] as? String).flatMap { ["USB", "Wi-Fi", "BLE", "Offline"].contains($0) ? $0 : nil }
        if let ids = root["assetIds"] as? String { assets.acknowledge(ids) }
        if let version = root["firmware"] as? String, version.utf8.count <= 40 { firmware = version; firmwareDevice = pairing?.deviceId; firmwareControl.observeVersion(version) }
        firmwareControl.observeDeviceOTA(root["otaDiag"])
        if let context = diagnosticContextLocked() { deviceDiagnostics.recordUSB(root,context:context) }
    }
    func publish(_ value: MacStatusSnapshot) { lock.lock(); snapshot = value; lock.unlock() }
    func usbState(_ note: String, confirmed: Bool = false) {
        lock.lock()
        if confirmed, !lastUSB.isFinite { usbConfirmedSessions += 1 }
        if !confirmed, lastUSB.isFinite { usbInterruptions += 1 }
        if !confirmed, note.hasPrefix("USB 未确认"), note != usbNote {
            localDiagnostics?.transport(note)
            usbRecentFailures.append((ProcessInfo.processInfo.systemUptime,note))
            usbRecentFailures = Array(usbRecentFailures.suffix(8))
        }
        usbNote = note
        lastUSB = confirmed ? ProcessInfo.processInfo.systemUptime : -.infinity
        if !confirmed { usbReportedActive = nil }
        if confirmed { usbAuthenticationRejected = false }
        lock.unlock()
        if !confirmed { unlock.cancel(transport:"USB") }
    }
    var usbDiagnosticSummary: String {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        let history = usbRecentFailures.reversed().map { "\(Int(max(0,now-$0.0))) 秒前 · \($0.1)" }
        let waiting = usbOperationStarted.map { "处理中 \(Int(max(0,now-$0)*1000)) ms" } ?? "空闲"
        return (["Mac USB 会话：确认 \(usbConfirmedSessions)，中断 \(usbInterruptions)",
            "USB 请求等待：\(waiting)；保活 \(usbOperationHeartbeats) 次；上次 \(Int(usbOperationLast*1000)) ms；最大 \(Int(usbOperationMax*1000)) ms"] + history).joined(separator:"\n")
    }
    func usbOperationBegan() { lock.lock(); usbOperationStarted = ProcessInfo.processInfo.systemUptime; lock.unlock() }
    func usbOperationHeartbeat() { lock.lock(); usbOperationHeartbeats += 1; lock.unlock() }
    func usbOperationEnded() {
        lock.lock(); defer { lock.unlock() }
        if let started = usbOperationStarted {
            usbOperationLast = max(0,ProcessInfo.processInfo.systemUptime-started)
            usbOperationMax = max(usbOperationMax,usbOperationLast)
        }
        usbOperationStarted = nil
    }

    // Save a new key BEFORE sending it. An uncertain USB ACK can then be retried
    // explicitly with the same key, without silently invalidating the device.
    func preparePairing(device: String, path: String) throws -> MacTab5Pairing {
        lock.lock(); defer { lock.unlock() }
        guard deviceEnabled(), !stopped, !maintenance, benchmarkMode == 0 else { throw MacTab5Error.unavailable }
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

    func frame(includeResources: Bool = true) throws -> Data {
        lock.lock(); defer { lock.unlock() }
        return try frameLocked(includeResources: includeResources)
    }
    func nextSequence(for pair: MacTab5Pairing) throws -> Int64 {
        lock.lock(); defer { lock.unlock() }
        guard deviceEnabled(), !stopped, !maintenance, pair.confirmed, pairing == pair else { throw MacTab5Error.identity }
        return try nextSequenceLocked()
    }
    private func nextSequenceLocked() throws -> Int64 {
        guard sequence < 9_007_199_254_740_991 else { throw MacTab5Error.invalidPacket }
        sequence += 1; return sequence
    }
    func resourceFrame(maximum: Int = 4) throws -> Data {
        lock.lock(); defer { lock.unlock() }
        guard benchmarkMode == 0 else { throw MacTab5Error.busy }
        guard deviceEnabled(), !stopped, !maintenance, let pair = pairing, pair.confirmed else { throw MacTab5Error.pairingRequired }
        let chunks = assets.batch(maximum)
        guard !chunks.isEmpty else { throw MacTab5Error.unavailable }
        _ = try nextSequenceLocked()
        return try MacTab5Protocol.json(["version": 1, "type": "tab5_resources", "deviceId": pair.deviceId,
                                         "session": session, "sequence": sequence, "resources": chunks])
    }
    private func frameLocked(includeResources: Bool = true) throws -> Data {
        guard deviceEnabled(), !stopped, !maintenance else { throw MacTab5Error.unavailable }
        guard let pair = pairing, pair.confirmed, let snapshot else { throw MacTab5Error.pairingRequired }
        _ = try nextSequenceLocked()
        let media = sourceEnabled("music") ? latestMusic() : nil
        let now = Date()
        if let media { assets.updateCover(media.cover, jpegSupported: MacTab5Asset.supportsJPEG(firmware)) }
        var additions = assets.projection(includeResource: includeResources && benchmarkMode == 0)
        var calendar = MacTab5Calendar.snapshot(epoch: snapshot.epochUtc, offset: snapshot.utcOffsetSeconds)
        var calendarDetail = calendar["detail"] as? [String: Any] ?? [:]
        // An unreadable local profile is omitted, never replaced with an empty list.
        if let values = try? birthdays.projection() { calendarDetail["birthdays"] = values }
        calendar["detail"] = calendarDetail; additions["calendar"] = calendar
        additions["agenda"] = try? agenda.summary()
        additions["runtimeHealth"] = sourceEnabled("system") ? runtimeHealth.snapshot(metrics: snapshot.systemMetrics, now: now,
            logicalProcessors: ProcessInfo.processInfo.activeProcessorCount, codexEnabled: codex.isEnabled,
            desktopObserved: codex.canMutate, network: networkDiagnostics()) : nil
        additions["rpcBinary"] = 1; additions["rpcBulk"] = 1
        additions["rpcOtaZlib"] = 1; additions["rpcOtaResume"] = 1
        additions["rpcOtaBleRadioLease"] = 1
        let offer = firmwareControl.projection(frameEpochMilliseconds:Int64(snapshot.capturedAt.timeIntervalSince1970*1000))
        additions["ota"] = offer.offer.map { $0 as Any } ?? NSNull()
        var health = offer.health
        health["hostBindingVersion"] = 1; health["hostId"] = MacTab5HostBinding.id(pair)
        health["hostName"] = MacTab5HostBinding.name(ProcessInfo.processInfo.hostName)
        health["displayCommand"] = display.command.map { $0 as Any } ?? NSNull()
        health["galleryPacks"] = gallery.revisions(); health["gallerySyncVersion"] = 1
        if computer.hasCapabilities { health["computerControlVersion"] = 1 }
        health["wifiUnlockVersion"] = unlock.configured(pair) ? 1:0
        if unlock.configured(pair,transport:"USB") { health["hostUnlockVersion"] = 1 }
        health["voiceEnabled"] = voice.isEnabled
        health["runtimeDiagnosticCommand"] = deviceDiagnostics.command
        health["screenPreviewCommand"] = screenPreview.command(frameEpochMilliseconds:Int64(snapshot.capturedAt.timeIntervalSince1970*1000))
        additions["connectionHealth"] = health
        additions["codexTasks"] = sourceEnabled("activity") ? codex.snapshot() : nil
        if codex.isEnabled { additions["imageBinary"] = 1 }
        for count in [64, 48, 32, 16, 8, 4, 1] {
            additions["codexTasks"] = sourceEnabled("activity") ? codex.snapshot(taskLimit: count) : nil
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
            guard deviceEnabled(), !stopped, !maintenance, benchmarkMode == 0 else { throw MacTab5Error.unavailable }
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
        lock.unlock(); callback?(.failure(MacTab5Error.unconfirmed)); deviceDiagnostics.cancel(); screenPreview.stop(); codex.stop(); voice.stop(); unlock.stop()
    }

    func status(device: String, nonce: String, proof: String, headers: [String: String] = [:]) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        guard deviceEnabled(), !stopped, !maintenance else { return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "GET|\(device)|\(nonce)", proof: proof) else { return (401, Data()) }
        do {
            if let ids = headers["x-aibot-assets"], let assetProof = headers["x-aibot-assets-proof"],
               MacTab5Protocol.verify(key: pair.key, message: "ASSETS|\(nonce)|\(ids)", proof: assetProof) { assets.acknowledge(ids) }
            if let version = headers["x-tab5-firmware"], version.utf8.count <= 40, let firmwareProof = headers["x-tab5-firmware-proof"],
               MacTab5Protocol.verify(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)", proof: firmwareProof) { firmware = version; firmwareDevice = pair.deviceId; firmwareControl.observeVersion(version) }
            let full = try frameLocked()
            let wire = try MacTab5WiFiStatus.wire(full,headers:headers,pair:pair,nonce:nonce)
            let packet = try MacTab5Protocol.encrypt(wire, key: pair.key, challenge: nonce)
            lastWiFi = ProcessInfo.processInfo.systemUptime
            return (200, packet)
        } catch { return (503, Data()) }
    }

    func rpc(device: String, nonce: String, proof: String, packet: Data, transport: String = "USB", queued: TimeInterval? = nil) -> (Int, Data) {
        lock.lock()
        guard deviceEnabled(), !stopped, !maintenance else { lock.unlock(); return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device,
              MacTab5Protocol.isHex(nonce, count: 32), (28...MacTab5Protocol.maximumRPC).contains(packet.count),
              MacTab5Protocol.verify(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof) else {
            if transport == "USB" { usbAuthenticationRejected = true }
            lock.unlock(); return (401, Data())
        }
        let root: [String: Any]
        let raw: Data
        do { (root, raw) = try MacTab5Binary.open(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) }
        catch { if transport == "USB" { usbAuthenticationRejected = true }; lock.unlock(); return (401, Data()) }
        if transport == "USB" { usbAuthenticationRejected = false }
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
        let diagnostic = dispatchOperation ? interactionDiagnostics.begin(kind: root["kind"] as? String,
            operation: root["op"] as? String, transport: transport, queued: queued) : nil
        if dispatchOperation {
            if currentBenchmark != 0, ["image-chunk","screen-preview","ota","ota_probe","ota-control","voice"].contains(root["kind"] as? String ?? "") { result = .error(409,"benchmark_busy") }
            else if root["kind"] as? String == "benchmark" { result = MacTab5Benchmark.handle(root,raw:raw,binary:root["binaryReply"] as? Bool == true,bulk:root["bulkReply"] as? Bool == true) }
            else if root["kind"] as? String == "image-chunk", codex.isEnabled, let task = MacTab5Codex.id(root["taskId"]), codex.contains(task) {
                let bytes = raw.isEmpty ? Data(base64Encoded: root["data"] as? String ?? "") ?? Data() : raw
                result = codex.photos.upload(root, bytes: bytes, device: device)
            }
            else if root["kind"] as? String == "screen-preview" {
                let bytes = raw.isEmpty ? Data(base64Encoded:root["data"] as? String ?? "") ?? Data():raw
                result = screenPreview.receive(root,bytes:bytes,context:screenPreviewContext,device:device,transport:transport)
            }
            else if !raw.isEmpty { result = .error(400, "unexpected_binary_data") }
            else if root["kind"] as? String == "runtime-diagnostics" {
                result = deviceDiagnostics.receive(root,context:deviceDiagnosticContext,device:device,transport:transport)
            }
            else if root["kind"] as? String == "gallery", currentBenchmark == 1 { result = .error(409,"benchmark_busy") }
            else if root["kind"] as? String == "gallery" { result = gallery.handle(root, binary: root["binaryReply"] as? Bool == true, bulk: root["bulkReply"] as? Bool == true) }
            else if root["kind"] as? String == "computer-control" { result = computer.handle(root) }
            else if ["wifi-unlock","host-unlock"].contains(root["kind"] as? String ?? "") {
                result = unlock.handle(root,pair:pair,transport:transport,session:session,allowed:{ [weak self] in
                    guard let self,self.isEnabled,self.currentPairing == pair else { return false }
                    return !self.unlockSuspended
                })
            }
            else if root["kind"] as? String == "agenda" { result = agenda.handle(root) }
            else if root["kind"] as? String == "codex", voice.needsTransportPriority,
                    ["read","read-ack"].contains(root["op"] as? String ?? "") {
                // A background read must release the serial mailbox promptly;
                // the device retries its idempotent read receipt after voice.
                result = .error(503,"voice_busy")
            }
            else if root["kind"] as? String == "codex" { result = codex.handleDevice(root, device: device) }
            else if root["kind"] as? String == "voice" {
                result = voice.handle(root, device: device, waitForFinal: transport == "BLE" || transport == "USB")
                if transport == "BLE", result.status == 200, let operation = MacTab5BLEDiagnostics.VoiceOperation(rawValue: root["op"] as? String ?? "") {
                    bleDiagnostics.voice(operation, terminal: result.status == 200 && ["review", "cancelled", "error"].contains(result.body["state"] as? String ?? ""))
                }
                voice.observeReply(op: root["op"] as? String, transport: transport, status: result.status, response: result.body, nonce: nonce)
            }
            else if root["kind"] as? String == "desktop" { result = desktop.handle(root, device: device) }
            else { result = firmwareControl.handle(root, device: device, transport: transport, binary: root["binaryReply"] as? Bool == true, bulk: root["bulkReply"] as? Bool == true) }
            lock.lock(); activeOperations -= 1; lock.unlock()
        }
        computer.observe(root, result: result, transport: transport, started: monotonic, queued: queued)
        let binary = root["binaryReply"] as? Bool == true, bulk = binary && root["bulkReply"] as? Bool == true
        var clear = try? MacTab5Binary.encode(result, binary: binary, bulk: bulk)
        if (clear?.count ?? Int.max) > MacTab5Binary.maximumMailbox - 28 { clear = try? MacTab5Binary.encode(.error(413, "response_too_large"), binary: false, bulk: false) }
        let reply = clear.flatMap { try? MacTab5Protocol.encrypt($0, key: pair.key, challenge: nonce, maximum: MacTab5Binary.maximumMailbox) }
        interactionDiagnostics.finish(diagnostic, status: result.status, body:result.body)
        if let delivery { callback?(delivery) }
        return reply.map { (200, $0) } ?? (500, Data())
    }

    var pipelinedOTA: Bool {
        lock.lock(); defer { lock.unlock() }
        return firmwareDevice == pairing?.deviceId && MacTab5OTAEncoding.supportsRanges(firmware,minimumPatch:288)
    }
    func ota(device: String, nonce: String, proof: String, sha: String, compressed:Bool = false, transfer:UUID = UUID()) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        guard deviceEnabled(), !stopped, !maintenance else { return (503, Data()) }
        guard let pair = pairing, pair.confirmed, pair.deviceId == device, MacTab5Protocol.isHex(sha, count: 64), MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "GET|/tab5/v1/ota/\(sha)|\(device)|\(nonce)", proof: proof) else { return (401, Data()) }
        // .257+ already switches an initial 503 to authenticated range RPCs.
        // Avoid the Mac long-stream retransmission stall from the first block;
        // unknown/older peers retain the original streaming response.
        if compressed, firmwareDevice == device, MacTab5OTAEncoding.supportsRanges(firmware),
           !MacTab5OTAEncoding.supportsRanges(firmware,minimumPatch:288) {
            return (firmwareControl.beginRanges(sha:sha) ? 503:404, Data())
        }
        guard let bytes = firmwareControl.bytes(sha:sha,compressed:compressed,transfer:transfer) else { return (404, Data()) }
        return (200, bytes)
    }

    func codexHTTP(device: String, nonce: String, proof: String, packet: Data, operation: String, queued: TimeInterval? = nil) -> (Int, Data) {
        guard ["read", "turn"].contains(operation), let pair = currentPairing, pair.confirmed, pair.deviceId == device,
              packet.count <= 8028, MacTab5Protocol.isHex(nonce, count: 32),
              MacTab5Protocol.verify(key: pair.key, message: "POST|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof),
              var root = try? MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) else { return (401, Data()) }
        let op = root["op"] as? String ?? ""
        guard operation == "read" ? ["read", "draft-load", "receipt"].contains(op) : ["send", "steer", "interrupt", "draft-save", "receipt-confirm"].contains(op) else { return (400, Data()) }
        root["kind"] = "codex"
        do {
            let sealed = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: pair.key, challenge: nonce)
            let rpcProof = MacTab5Protocol.proof(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(sealed))")
            let result = rpc(device: device, nonce: nonce, proof: rpcProof, packet: sealed, transport: "Wi-Fi", queued: queued)
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
            guard deviceEnabled(), !stopped, !maintenance, currentIdentityMatches(pair), root["session"] as? String == session, let issued = MacTab5Protocol.integer(root["issuedAt"]), issued >= now - 90000, issued <= now + 10000 else { lock.unlock(); return (400, Data()) }
            nonces = nonces.filter { tick - $0.value < 100 }
            guard nonces[nonce] == nil, nonces.count < 8192 else { lock.unlock(); return (409, Data()) }
            nonces[nonce] = tick; activeOperations += 1; lock.unlock()
            let result = codex.photos.upload(root, bytes: bytes, device: device)
            lock.lock(); activeOperations -= 1; lock.unlock()
            return (result.status, try MacTab5Protocol.json(result.body))
        } catch { return (400, Data()) }
    }
    private func currentIdentityMatches(_ pair: MacTab5Pairing) -> Bool { pairing == pair }

    func voicePacket(device: String, nonce: String, proof: String, packet: Data, transport: String, queued: TimeInterval? = nil) -> (Int, Data) {
        let result = voiceResponse(device: device, nonce: nonce, proof: proof, packet: packet, transport: transport, queued: queued)
        if result.0 != 200 { voice.observeReply(op: nil, transport: transport, status: result.0) }
        return result
    }
    private func voiceResponse(device: String, nonce: String, proof: String, packet: Data, transport: String, queued: TimeInterval?) -> (Int, Data) {
        guard let pair = currentPairing, pair.confirmed, pair.deviceId == device, packet.count <= 16028,
              MacTab5Protocol.isHex(nonce, count: 32), MacTab5Protocol.verify(key: pair.key, message: "POST|/tab5/v1/voice|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))", proof: proof),
              var root = try? MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: pair.key, challenge: nonce)) else { return (401, Data()) }
        root["kind"] = "voice"
        do {
            let sealed = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: pair.key, challenge: nonce)
            let rpcProof = MacTab5Protocol.proof(key: pair.key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(sealed))")
            let result = rpc(device: device, nonce: nonce, proof: rpcProof, packet: sealed, transport: transport, queued: queued)
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
        guard deviceEnabled(), !stopped, lanReady, !maintenance, let pair = pairing, pair.confirmed, parts.count == 4, parts[0] == "TAB5_DISCOVER_V1",
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
