import Foundation

/// Existing .250 device protocol, independently implemented for macOS. Only the
/// service's authenticated RPC dispatcher may deliver wireless samples here.
final class MacTab5RuntimeDiagnostics {
    struct Context {
        let device: String, host: String, firmware: String
        var transports: Set<String>
        var available = true
    }
    private struct Probe {
        let context: Context, id: String, transport: String, expires: Int64, started: TimeInterval, attempt: Int
    }
    private struct Sample {
        let device: String, host: String, transport: String, received: Date, tick: TimeInterval, body: String
    }
    private let lock = NSLock()
    private let date: () -> Date, tick: () -> TimeInterval
    private let schedule: (TimeInterval, @escaping () -> Void) -> Void
    private var probe: Probe?, sample: Sample?
    private var previewMemory = MacPreviewMemoryObservation()
    private var callbacks: [() -> Void] = []
    private var note = ""

    init(date: @escaping () -> Date = Date.init, tick: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
         schedule: @escaping (TimeInterval, @escaping () -> Void) -> Void = { delay, action in DispatchQueue.global().asyncAfter(deadline: .now() + delay, execute: DispatchWorkItem(block: action)) }) {
        self.date = date; self.tick = tick; self.schedule = schedule
    }
    static func supports(_ firmware: String) -> Bool {
        firmware.hasSuffix("-ui") && MacReleaseCatalog.version(firmware).map { !$0.lexicographicallyPrecedes([0,2,250]) } == true
    }
    var command: [String: Any]? {
        lock.lock(); defer { lock.unlock() }
        guard let p = probe, epoch <= p.expires, tick() - p.started <= 10 else { return nil }
        return ["version":1,"requestId":p.id,"transport":p.transport,"expiresAt":p.expires]
    }
    private var epoch: Int64 { Int64(date().timeIntervalSince1970 * 1000) }
    private func makeProbe(_ context: Context, transport: String, attempt: Int) -> Probe {
        Probe(context:context,id:MacTab5Protocol.identifier(),transport:transport,expires:epoch+10_000,started:tick(),attempt:attempt)
    }
    private func matches(_ context: Context?, _ p: Probe) -> Bool {
        context?.available == true && context?.device == p.context.device && context?.host == p.context.host
    }
    func refresh(context: @escaping () -> Context?, completion: @escaping () -> Void = {}) {
        // Never call back into the service while holding this lock.
        let current = context()
        lock.lock()
        if probe != nil { callbacks.append(completion); lock.unlock(); return }
        if let c = current, let s = sample, s.device == c.device, s.host == c.host,
           tick() >= s.tick, tick() - s.tick < (s.transport == "USB" ? 8 : 2) {
            note = ""; lock.unlock(); completion(); return
        }
        guard let c = current, c.available, !c.transports.isEmpty else {
            note = "当前没有可读取的绑定设备连接，保留上次采样。"; lock.unlock(); completion(); return
        }
        guard Self.supports(c.firmware) else {
            note = "当前固件仅提供 USB 详细诊断；无线诊断需要 0.2.250-ui 或更新版本。"; lock.unlock(); completion(); return
        }
        let p = makeProbe(c,transport:c.transports.contains("Wi-Fi") ? "Wi-Fi":"BLE",attempt:0)
        probe = p; callbacks = [completion]; note = "正在读取设备运行诊断…"
        lock.unlock(); expireLater(p, context:context)
    }
    private func expireLater(_ p: Probe, context: @escaping () -> Context?) {
        schedule(8) { [weak self] in self?.expire(p.id,context:context) }
    }
    private func expire(_ id: String, context: @escaping () -> Context?) {
        let current = context()
        lock.lock()
        guard let p = probe, p.id == id else { lock.unlock(); return }
        let other = p.transport == "Wi-Fi" ? "BLE":"Wi-Fi"
        if p.attempt == 0, matches(current,p), let c = current, c.transports.contains(other) {
            let next = makeProbe(c,transport:other,attempt:1); probe = next
            note = "首个通道未回复，正在通过同一电脑的\(other)读取…"
            lock.unlock(); expireLater(next,context:context); return
        }
        note = "无线读取超时或连接已变化，未取得新采样；上次数据保留。"
        let finished = finishLocked(); lock.unlock(); finished.forEach { $0() }
    }
    private func finishLocked() -> [() -> Void] {
        probe = nil; let finished = callbacks; callbacks = []; return finished
    }
    func cancel() {
        lock.lock(); note = "诊断读取已取消，保留上次采样。"
        let finished = finishLocked(); lock.unlock(); finished.forEach { $0() }
    }
    func receive(_ root: [String: Any], context: Context?, device: String, transport: String) -> MacTab5RPCResult {
        lock.lock()
        guard let p = probe, matches(context,p), device == p.context.device, transport == p.transport,
              root["requestId"] as? String == p.id, epoch <= p.expires, tick() - p.started <= 10 else {
            lock.unlock(); return .error(409,"runtime_probe_expired")
        }
        guard let snapshot = root["snapshot"] as? [String:Any], Self.valid(snapshot,device:device,host:p.context.host) else {
            lock.unlock(); return .error(400,"invalid_runtime_snapshot")
        }
        if sample == nil || sample!.device != device || sample!.host != p.context.host || sample!.tick < p.started {
            recordLocked(snapshot,device:device,host:p.context.host,transport:transport)
        }
        note = ""; let finished = finishLocked(); lock.unlock(); finished.forEach { $0() }
        return MacTab5RPCResult(200,["accepted":true])
    }
    static func valid(_ value: [String:Any], device: String, host: String) -> Bool {
        guard let data = try? JSONSerialization.data(withJSONObject:value), data.count <= 8192,
              value["deviceId"] as? String == device, value["hostId"] as? String == host,
              supports(value["firmware"] as? String ?? "") else { return false }
        for (key, maximum) in [("diagnosticVersion",Int64(1)),("sampleAgeMs",8000),("uptimeMs",Int64.max),
                               ("uiAgeMs",Int64(UInt32.max)),("flushCount",Int64(UInt32.max)),("flushErrors",Int64(UInt32.max)),("lcdUnderruns",Int64(UInt32.max))] {
            guard let n = MacTab5Protocol.integer(value[key]), n >= (key == "diagnosticVersion" ? 1:0), n <= maximum else { return false }
        }
        return ["displayDiag","cameraDiag","photoDiag","photoUploadDiag"].allSatisfy { safeText(value[$0]) != nil }
    }
    private static func safeText(_ value: Any?) -> String? {
        guard let text = value as? String, (1...1024).contains(text.utf8.count),
              text.utf8.allSatisfy({ (48...57).contains($0) || (65...90).contains($0) || (97...122).contains($0) || ",;-_.:/ ".utf8.contains($0) }) else { return nil }
        return text
    }
    func recordUSB(_ value: [String:Any], context: Context) {
        // Only full status ACKs contain these counters. Resource ACKs cannot
        // overwrite a useful sample with an empty health record.
        guard value["type"] as? String == "tab5_ack", value["deviceId"] as? String == context.device,
              MacTab5Protocol.integer(value["uptimeMs"]) != nil else { return }
        lock.lock()
        previewMemory.observe(value,device:context.device,host:context.host)
        recordLocked(value,device:context.device,host:context.host,transport:"USB")
        if probe == nil { note = "" }; lock.unlock()
    }
    private func recordLocked(_ value: [String:Any], device: String, host: String, transport: String) {
        let numeric = [("uptimeMs","运行毫秒"),("uiAgeMs","界面心跳年龄"),("uiDataAgeMs","界面数据年龄"),("uiPage","页面"),("flushCount","显示刷新"),("flushErrors","显示错误"),("lcdUnderruns","显示欠载"),("resetReason","重启原因"),("internalFree","内部可用内存"),("internalLargest","内部最大连续内存"),("psramFree","PSRAM 可用内存"),("voiceCheck","语音启动检查"),("voiceAgeMs","启动时状态年龄"),("voiceStage","语音请求阶段"),("voiceHttp","语音 HTTP 状态"),("voiceRequestMs","最近语音请求耗时 ms"),("voiceSeq","语音音频序号")]
        let strings = [("displayDiag","显示"),("cameraDiag","相机"),("photoDiag","照片"),("photoUploadDiag","照片上传"),("wifiDiag","Wi-Fi"),("wifiRecoveryDiag","Wi-Fi 恢复"),("wifiHttpDiag","Wi-Fi HTTP"),("wifiPathDiag","Wi-Fi 路径"),("wirelessBootDiag","无线启动"),("rpcDiag","RPC"),("usbDiag","USB"),("galleryDiag","图库"),("artOrientation","艺术屏保方向"),("cameraLogHex","相机驱动记录")]
        var lines = ["固件：" + (Self.safeText(value["firmware"]) ?? "未知")]
        lines += numeric.map { key,label in label + "：" + (MacTab5Protocol.integer(value[key]).map(String.init) ?? "—") }
        for (key,label) in [("bleConnectCount","蓝牙连接次数"),("bleDisconnectReason","蓝牙最近断开原因")] {
            if let n = MacTab5Protocol.integer(value[key]), n >= 0 { lines.append(label + "：\(n)") }
        }
        if let ages = value["ageMs"] as? [Any], ages.count == 3,
           ages.allSatisfy({ MacTab5Protocol.integer($0) != nil }) {
            lines.append("USB/Wi-Fi/BLE 状态年龄：" + ages.map { String(MacTab5Protocol.integer($0)!) }.joined(separator:","))
        }
        lines += strings.compactMap { key,label in Self.safeText(value[key]).map { label + "：" + $0 } }
        let age = min(8000,max(0,MacTab5Protocol.integer(value["sampleAgeMs"]) ?? 0))
        sample = Sample(device:device,host:host,transport:transport,received:date(),tick:tick()-Double(age)/1000,body:lines.joined(separator:"\n"))
    }
    func summary(context: Context?) -> String {
        lock.lock(); defer { lock.unlock() }
        guard let s = sample, s.device == context?.device, s.host == context?.host else {
            return "设备运行：等待当前绑定设备的诊断。" + (note.isEmpty ? "":"\n"+note)
        }
        let age = max(0,tick()-s.tick), format = DateFormatter(); format.dateFormat = "yyyy-MM-dd HH:mm:ss"
        let memory = previewMemory.summary(device:s.device,host:s.host).map { "\n" + $0 } ?? ""
        return "诊断采样：\(s.transport)；接收时间：\(format.string(from:s.received))；年龄：\(Int(age*1000)) ms；\(age > 10 ? "已过期，仅供参考":"实时")\n" + s.body + memory + (note.isEmpty ? "":"\n"+note)
    }
}
