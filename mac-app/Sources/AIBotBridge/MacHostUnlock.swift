import AppKit
import Foundation
import MacKeychainSupport

/// The device carries intent and progress only. Credentials stay in this Mac's
/// Keychain and are bound to the current user and exact existing pairing key.
final class MacHostUnlock {
    static let account = "tab5-mac-unlock-v1", enabledKey = "tab5_mac_unlock_enabled"
    static let localEnabledKey = "tab5_mac_local_unlock_enabled"
    struct Credential: Codable {
        var version = 1
        let uid: UInt32, username: String, binding: String
        var password: Data
        func validate() throws {
            guard version == 1, uid == getuid(), username == NSUserName(), MacTab5Protocol.isHex(binding,count:64) else { throw MacTab5Error.invalidPacket }
            try MacARDConnection.validate(username:Data(username.utf8),password:password)
            try MacVNCUnlockTest.validate(password)
        }
    }
    private final class Attempt {
        let id:String, owner:String, transport:String, offer = MacTab5Protocol.identifier()
        var next = 0, stage = "preparing", lease:TimeInterval, deadline:TimeInterval
        var localStage = "请求已接收", stopReason = ""
        var milestones:[(label:String,time:TimeInterval)]
        var finished:Bool?, cancelled = false
        init(_ id:String,_ owner:String,_ transport:String,_ now:TimeInterval) { self.id = id; self.owner = owner; self.transport = transport; lease = now+2.5; deadline = now+40; milestones = [("请求已接收",now)] }
    }
    private let lock = NSLock(), defaults:UserDefaults
    private let read:() throws -> Data?, write:(Data) throws -> Void, remove:() throws -> Void
    private let clock:() -> TimeInterval, locked:() -> Bool?
    private let execute:(Data,TimeInterval,@escaping () -> Bool,@escaping (String) -> Void) throws -> Bool
    private let probe:(@escaping () -> Bool,@escaping (String) -> Void) throws -> Void
    private var pendingProbe:(owner:String,until:TimeInterval)?
    private let schedule:(@escaping () -> Void) -> Void
    private var credential:Credential?, current:Attempt?, used:[String:TimeInterval] = [:]
    private var generation = UUID(), stopped = false
    private var linkGenerations = ["USB":UUID(), "BLE":UUID()]
    private var observers:[NSObjectProtocol] = []
    private var note = "未配置"
    private var recentTimings:[String] = []
    private var sleepCount = 0, wakeCount = 0, displaySleepCount = 0, displayWakeCount = 0
    private var lastSleep:TimeInterval?, lastWake:TimeInterval?, lastBegin:TimeInterval?
    var powerDiagnostic:String {
        lock.lock(); defer { lock.unlock() }
        let time = clock()
        func age(_ value:TimeInterval?) -> String { value.map { "\(Int(max(0,time-$0))) 秒前" } ?? "本次运行未观察到" }
        return "整机睡眠 \(sleepCount) 次（\(age(lastSleep))）；唤醒 \(wakeCount) 次（\(age(lastWake))）；屏幕熄灭／亮起 \(displaySleepCount)／\(displayWakeCount) 次；解锁请求：\(age(lastBegin))。熄屏不等于整机睡眠。"
    }
    private func powerEvent(sleep:Bool) {
        lock.lock(); defer { lock.unlock() }
        if sleep { sleepCount += 1; lastSleep = clock(); invalidate() }
        else { wakeCount += 1; lastWake = clock() }
    }
    init(defaults:UserDefaults = .standard, read:@escaping () throws -> Data? = { try MacKeychainAccess.read(account) },
         write:@escaping (Data) throws -> Void = { try MacKeychainAccess.save(account,$0) }, remove:@escaping () throws -> Void = { try MacKeychainAccess.remove(account) },
         clock:@escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, locked:@escaping () -> Bool? = { MacVNCUnlockTest.lockedState() },
         execute:@escaping (Data,TimeInterval,@escaping () -> Bool,@escaping (String) -> Void) throws -> Bool = { try MacVNCUnlockTest.unlockSaved($0,isCancelled:$2,progress:$3,stableDesktopSeconds:$1) },
         probe:@escaping (@escaping () -> Bool,@escaping (String) -> Void) throws -> Void = { try MacVNCUnlockTest.probePrintablePreparation(isCancelled:$0,progress:$1) },
         schedule:@escaping (@escaping () -> Void) -> Void = { work in DispatchQueue.global(qos:.userInitiated).async(execute:work) }) {
        self.defaults = defaults; self.read = read; self.write = write; self.remove = remove; self.clock = clock; self.locked = locked; self.execute = execute; self.probe = probe; self.schedule = schedule
        observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:NSWorkspace.sessionDidResignActiveNotification,object:nil,queue:.main) { [weak self] _ in self?.cancel() })
        for (name,sleep) in [(NSWorkspace.screensDidSleepNotification,true),(NSWorkspace.screensDidWakeNotification,false)] {
            observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main) { [weak self] _ in
                guard let self else { return }
                self.lock.lock(); defer { self.lock.unlock() }
                if sleep { self.displaySleepCount += 1 } else { self.displayWakeCount += 1 }
            })
        }
        for (name,sleep) in [(NSWorkspace.willSleepNotification,true),(NSWorkspace.didWakeNotification,false)] {
            observers.append(NSWorkspace.shared.notificationCenter.addObserver(forName:name,object:nil,queue:.main) { [weak self] _ in self?.powerEvent(sleep:sleep) })
        }
    }
    static func binding(_ pair:MacTab5Pairing) -> String { MacTab5Protocol.sha256(Data((pair.deviceId+"|"+MacTab5HostBinding.id(pair)).utf8)) }
    var diagnostic:String { lock.lock(); defer { lock.unlock() }; return note }
    // Fixed stage names and bounded durations only; no IDs, text or secrets.
    var timingDiagnostic:String { lock.lock(); defer { lock.unlock() }; return recentTimings.reversed().joined(separator:"\n") }
    var enabled:Bool { defaults.bool(forKey:Self.enabledKey) }
    var localEnabled:Bool { defaults.bool(forKey:Self.localEnabledKey) }
    var hasCredential:Bool { lock.lock(); defer { lock.unlock() }; return !stopped && (enabled || localEnabled) && credential != nil }
    func configured(_ pair:MacTab5Pairing?,transport:String = "Wi-Fi") -> Bool {
        lock.lock(); defer { lock.unlock() }; return configuredLocked(pair,transport:transport)
    }
    private func configuredLocked(_ pair:MacTab5Pairing?,transport:String = "Wi-Fi") -> Bool {
        let permitted = transport == "Wi-Fi" ? enabled : ["USB","BLE"].contains(transport) && localEnabled
        guard !stopped, permitted, let pair, pair.confirmed, let credential else { return false }
        return credential.binding == Self.binding(pair) && credential.uid == getuid() && credential.username == NSUserName()
    }
    /// Called once at ordinary app startup, never by device RPC or polling.
    func load(force:Bool = false) {
        guard enabled || localEnabled || force else { return }
        lock.lock(); let expected = generation; lock.unlock()
        do {
            guard let bytes = try read(),bytes.count <= 4096 else { throw MacTab5Error.invalidPacket }
            let value = try JSONDecoder().decode(Credential.self,from:bytes); try value.validate()
            lock.lock(); defer { lock.unlock() }
            guard generation == expected,!stopped else { return }
            credential = value; note = "已读取钥匙串配置"
        } catch { lock.lock(); note = "无法读取解锁配置；请在解锁设置中重新接入"; lock.unlock() }
    }
    /// Caller must complete the public account/keyboard check first. This is
    /// only called from the explicit local Save Password action, never RPC.
    func saveVerified(_ password:Data,pair:MacTab5Pairing) throws {
        try pair.validate(); guard pair.confirmed else { throw MacTab5Error.pairingRequired }
        let value = Credential(uid:getuid(),username:NSUserName(),binding:Self.binding(pair),password:password)
        try value.validate()
        try write(JSONEncoder().encode(value))
        lock.lock(); defer { lock.unlock() }
        invalidate(); credential = value; note = "密码已验证并保存在本机钥匙串"
    }
    func setEnabled(_ value:Bool,pair:MacTab5Pairing?) throws {
        lock.lock(); defer { lock.unlock() }
        if value {
            guard let pair, pair.confirmed, credential?.binding == Self.binding(pair), credential?.uid == getuid() else { throw MacTab5LocalError.invalid("请先验证并保存当前配对设备的 Mac 密码。") }
        }
        invalidate(); defaults.set(value,forKey:Self.enabledKey)
        note = value ? "Wi-Fi 一键解锁已开启":"Wi-Fi 一键解锁已关闭，已保存密码保留"
    }
    func setLocalEnabled(_ value:Bool,pair:MacTab5Pairing?) throws {
        lock.lock(); defer { lock.unlock() }
        if value {
            guard let pair,pair.confirmed,credential?.binding == Self.binding(pair),credential?.uid == getuid() else {
                throw MacTab5LocalError.invalid("请先验证并保存当前配对设备的 Mac 密码。")
            }
        }
        invalidate(); defaults.set(value,forKey:Self.localEnabledKey)
        note = value ? "USB／蓝牙本机解锁已开启，需支持的 TAB5 固件":"USB／蓝牙本机解锁已关闭，Wi-Fi 设置保留"
    }
    /// Save both UI choices atomically; an invalid new permission must not
    /// partially change the already working Wi-Fi permission.
    func setPermissions(wifi:Bool,local:Bool,pair:MacTab5Pairing?) throws {
        lock.lock(); defer { lock.unlock() }
        if wifi || local {
            guard let pair,pair.confirmed,credential?.binding == Self.binding(pair),credential?.uid == getuid() else {
                throw MacTab5LocalError.invalid("请先验证并保存当前配对设备的 Mac 密码。")
            }
        }
        if wifi != enabled || local != localEnabled { invalidate() }
        defaults.set(wifi,forKey:Self.enabledKey); defaults.set(local,forKey:Self.localEnabledKey)
        note = "已保存：Wi-Fi \(wifi ? "开启":"关闭")；USB／蓝牙 \(local ? "开启":"关闭")。"
    }
    /// Local UI only; consumes one authenticated begin within two minutes.
    /// Expiry rejects that begin rather than silently sending a real password.
    func armPreparationProbe(pair:MacTab5Pairing?) throws {
        lock.lock(); defer { lock.unlock() }
        guard configuredLocked(pair),let pair,current == nil else { throw MacTab5LocalError.invalid("需已启用当前设备解锁，且没有进行中的请求。") }
        pendingProbe = (Self.binding(pair),clock()+120)
        note = "已准备下一次公开字符诊断（两分钟有效）：只发 a，不填密码、不按回车。测试后请自行清空密码框。"
    }
    func delete() throws {
        lock.lock(); invalidate(); defaults.set(false,forKey:Self.enabledKey); defaults.set(false,forKey:Self.localEnabledKey); credential = nil; lock.unlock()
        try remove()
        lock.lock(); note = "已关闭并删除本机解锁密码"; lock.unlock()
    }
    private func invalidate() { generation = UUID(); current?.cancelled = true; current = nil }
    func stop() { lock.lock(); stopped = true; invalidate(); if var value = credential { credential = nil; value.password.resetBytes(in:0..<value.password.count) }; lock.unlock() }
    func cancel(transport:String? = nil) {
        lock.lock(); defer { lock.unlock() }
        if let transport,linkGenerations[transport] != nil { linkGenerations[transport] = UUID() }
        if transport == nil || current?.transport == transport { invalidate() }
    }
    private func valid(_ attempt:Attempt,pair:MacTab5Pairing,allowed:() -> Bool) -> Bool {
        guard allowed() else { return false }
        lock.lock(); defer { lock.unlock() }
        let time = clock()
        if current === attempt, attempt.stopReason.isEmpty {
            if time >= attempt.lease { attempt.stopReason = "设备续期超时" }
            else if time >= attempt.deadline { attempt.stopReason = "解锁总时限已到" }
        }
        return current === attempt && !attempt.cancelled && configuredLocked(pair,transport:attempt.transport) && time < attempt.lease && time < attempt.deadline
    }
    private func result(_ attempt:Attempt) -> MacTab5RPCResult {
        if let success = attempt.finished {
            return .init(200,success ? ["ok":true,"unlocked":true,"stage":"unlocked"] : ["ok":false,"unlocked":false,"error":"unlock_stopped_no_retry"])
        }
        return .init(200,["ok":true,"offer":attempt.offer,"next":attempt.next,"stage":attempt.stage,"unlocked":false])
    }
    func handle(_ root:[String:Any],pair:MacTab5Pairing,transport:String,session:String,allowed:@escaping () -> Bool) -> MacTab5RPCResult {
        func error(_ text:String) -> MacTab5RPCResult {
            // Callers release the lock first. Only fixed local error codes,
            // never request fields or credentials, enter this diagnostic.
            let label = transport == "USB" ? "USB":transport == "BLE" ? "蓝牙":"Wi-Fi"
            lock.lock()
            if root["op"] as? String != "cancel" { note = "\(label) 解锁未开始或已停止（\(text)）" }
            lock.unlock()
            return .init(200,["ok":false,"error":text,"unlocked":false])
        }
        // Keep the accepted Wi-Fi entry unchanged. The optional host-unlock
        // entry is a separate USB/BLE intent path, never a PIN grant or fallback.
        let kind = root["kind"] as? String
        if kind == "wifi-unlock" {
            guard transport == "Wi-Fi" else { return .error(409,"unlock_requires_wifi") }
        } else if kind == "host-unlock" {
            guard ["USB","BLE"].contains(transport) else { return .error(409,"host_unlock_requires_usb_ble") }
        } else { return error("invalid_unlock_request") }
        let fields:Set<String> = ["kind","op","requestId","offer","next","session","issuedAt","binaryReply","bulkReply"]
        guard Set(root.keys).isSubset(of:fields),let op = root["op"] as? String,["begin","poll","cancel"].contains(op),
              root["session"] as? String == session,let raw = root["requestId"] as? String,let uuid = UUID(uuidString:raw),raw.lowercased() == uuid.uuidString.lowercased() else { return error("invalid_unlock_request") }
        let id = uuid.uuidString.lowercased(), owner = Self.binding(pair)+"|"+session
        guard allowed() else { cancel(transport:transport); return error(kind == "host-unlock" ? "host_unlock_disabled":"wifi_unlock_disabled") }
        lock.lock()
        let time = clock()
        if let current, time >= current.lease || time >= current.deadline { invalidate() }
        if op == "begin" {
            used = used.filter { time-$0.value < 120 }
            let key = owner+"|"+id
            guard used[key] == nil,used.count < 256 else { lock.unlock(); return error("unlock_request_consumed") }
            used[key] = time; lastBegin = time
            guard current == nil else { lock.unlock(); return error("unlock_busy") }
            guard configuredLocked(pair,transport:transport),let credential else { lock.unlock(); return error(kind == "host-unlock" ? "host_unlock_disabled":"wifi_unlock_disabled") }
            let expectedGeneration = generation, expectedLink = linkGenerations[transport]
            lock.unlock()
            // Unknown focus may enter wake-only preparation; the backend must
            // independently confirm signed loginwindow before touching a secret.
            // USB/BLE have a two-second exchange budget. AX focus inspection
            // must not hold their first mailbox reply; reserve the attempt and
            // perform that same check in the cancellable worker instead.
            let deferredLockCheck = kind == "host-unlock"
            if !deferredLockCheck, locked() == false { return error("session_not_locked") }
            lock.lock()
            guard current == nil,generation == expectedGeneration,linkGenerations[transport] == expectedLink,
                  configuredLocked(pair,transport:transport) else { lock.unlock(); return error("session_changed") }
            let armed = pendingProbe; pendingProbe = nil
            if let armed,transport != "Wi-Fi" || armed.owner != Self.binding(pair) || clock() >= armed.until {
                lock.unlock(); return error("preparation_probe_expired_no_input")
            }
            let useProbe = armed != nil
            let attempt = Attempt(id,owner,transport,clock()); current = attempt
            note = useProbe ? "正在执行一次公开字符诊断，不提交登录":"正在准备一次 \(transport) 解锁"
            let initial = result(attempt); lock.unlock()
            schedule { [weak self] in
                guard let self else { return }
                var secret = useProbe ? Data():credential.password
                defer { secret.resetBytes(in:0..<secret.count) }
                let succeeded:Bool, failure:String
                let progress:(String) -> Void = { stage in
                    self.lock.lock(); defer { self.lock.unlock() }
                    let labels = ["checking-lock":"核对锁屏状态", "revealing-field":"确认字符已唤出密码页", "public-character-probe":"仅发送公开测试字符", "waking-display":"唤醒显示器", "connecting":"连接本机屏幕共享", "authenticating":"认证本机账户", "waiting-input":"等待键鼠静止", "preparing-field":"确认登录密码框", "sending":"填入一次登录输入", "pressing-login-button":"按下已确认的登录按钮", "confirming":"确认桌面恢复", "confirming-disconnected":"确认断开后桌面"]
                    if self.current === attempt,!attempt.cancelled,let label = labels[stage] {
                        if attempt.milestones.last?.label != label,attempt.milestones.count < 16 {
                            attempt.milestones.append((label,self.clock()))
                        }
                        attempt.localStage = label
                        attempt.stage = stage == "confirming-disconnected" ? "confirming" : stage == "pressing-login-button" ? "sending" : ["sending","confirming"].contains(stage) ? stage : "preparing"
                    }
                }
                let cancelled = { !self.valid(attempt,pair:pair,allowed:allowed) }
                do {
                    if deferredLockCheck {
                        progress("checking-lock")
                        guard !cancelled() else { throw MacTab5Error.unconfirmed }
                        let state = self.locked()
                        guard !cancelled(),state != false else { throw MacTab5Error.unconfirmed }
                    }
                    if useProbe {
                        try self.probe(cancelled,progress)
                        succeeded = false
                        failure = "公开字符 a 已投递但不证明送达；未发送密码、回车或删除键。请观察密码框是否出现，随后自行清空并解锁。"
                    } else {
                        succeeded = try self.execute(secret,attempt.transport == "Wi-Fi" ? 2:0.5,cancelled,progress)
                        failure = "未确认桌面恢复"
                    }
                } catch {
                    succeeded = false
                    // Only our bounded, credential-free error descriptions are
                    // surfaced locally. Never serialize them into device replies.
                    failure = (error as? MacTab5LocalError).map { String($0.localizedDescription.prefix(600)) } ??
                        (error as? MacTab5Error).map { String(describing:$0) } ?? "本机解锁操作失败"
                }
                self.lock.lock(); defer { self.lock.unlock() }
                guard self.current === attempt,!attempt.cancelled else { return }
                attempt.finished = succeeded
                let end = self.clock()
                // A slow BLE read may arrive after input has already finished.
                // Retain only the terminal receipt for one local RPC window;
                // the active-input lease and Wi-Fi behavior stay unchanged.
                if attempt.transport != "Wi-Fi" { attempt.lease = min(attempt.deadline,max(attempt.lease,end+2.5)) }
                let stages = attempt.milestones + [(label:"结束",time:end)]
                func ms(_ duration:TimeInterval) -> Int { duration.isFinite ? Int(max(0,min(40,duration))*1000):0 }
                let timings = zip(stages,stages.dropFirst()).map { "\($0.0.label) \(ms($0.1.time-$0.0.time)) ms" }.joined(separator:"；")
                self.recentTimings.append("\(attempt.transport) 解锁\(succeeded ? "成功":"停止")：总计 \(ms(end-stages[0].time)) ms；\(timings)")
                self.recentTimings = Array(self.recentTimings.suffix(2))
                self.note = succeeded ? "本次解锁已确认成功" : "本次解锁未确认，已停止且不重试；阶段：\(attempt.localStage)；\(attempt.stopReason.isEmpty ? failure : attempt.stopReason)；已收到 \(attempt.next) 次续期"
            }
            return initial
        }
        guard let attempt = current,attempt.id == id,attempt.owner == owner,attempt.transport == transport,root["offer"] as? String == attempt.offer else { lock.unlock(); return error("unlock_offer_changed") }
        if op == "cancel" {
            // Firmware sends best-effort cancel even after a terminal result.
            // Revoke the attempt without erasing the actual success/failure.
            if attempt.finished == nil { note = "设备已结束解锁请求；阶段：\(attempt.localStage)；已收到 \(attempt.next) 次续期" }
            invalidate(); lock.unlock(); return .init(200,["ok":true,"cancelled":true,"unlocked":false])
        }
        guard let step = MacTab5Protocol.integer(root["next"]),step == Int64(attempt.next) else { invalidate(); lock.unlock(); return error("unlock_step_consumed") }
        attempt.next += 1; attempt.lease = time+2.5
        guard configuredLocked(pair,transport:transport) else { invalidate(); lock.unlock(); return error(kind == "host-unlock" ? "host_unlock_disabled":"wifi_unlock_disabled") }
        let reply = result(attempt); lock.unlock(); return reply
    }
    deinit { for observer in observers { NSWorkspace.shared.notificationCenter.removeObserver(observer) } }
}
