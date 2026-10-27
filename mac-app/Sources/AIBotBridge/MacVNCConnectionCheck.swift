import AppKit
import ApplicationServices
import CommonCrypto
import Darwin

/// Bounded RFC 6143 connection check and shared IPv4-loopback transport.
/// The check entry point only authenticates/initializes; explicit unlock input
/// is implemented separately. Connection-only checks never request pixels;
/// input sessions send KeyEvent only after authentication and initialization.
enum MacVNCConnectionCheck {
    static func response(challenge:Data,password:Data) throws -> Data {
        guard challenge.count == 16, (1...8).contains(password.count), password.allSatisfy({ (0x20...0x7e).contains($0) }) else { throw MacTab5LocalError.invalid("请输入 1–8 位英文字符的 VNC 专用密码；不是 Mac 登录密码。") }
        var key = [UInt8](repeating:0,count:8), output = [UInt8](repeating:0,count:16)
        defer { _ = key.withUnsafeMutableBytes { $0.initializeMemory(as:UInt8.self,repeating:0) }; _ = output.withUnsafeMutableBytes { $0.initializeMemory(as:UInt8.self,repeating:0) } }
        for (i,byte) in password.enumerated() { var value = byte, reversed:UInt8 = 0; for _ in 0..<8 { reversed = (reversed << 1) | (value & 1); value >>= 1 }; key[i] = reversed }
        var written = 0
        let status = key.withUnsafeBytes { key in challenge.withUnsafeBytes { input in output.withUnsafeMutableBytes { output in
            CCCrypt(CCOperation(kCCEncrypt),CCAlgorithm(kCCAlgorithmDES),CCOptions(kCCOptionECBMode),key.baseAddress,8,nil,input.baseAddress,16,output.baseAddress,16,&written)
        } } }
        guard status == kCCSuccess, written == 16 else { throw MacTab5Error.unavailable }
        return Data(output)
    }
    static func check(password:Data,isCancelled:@escaping () -> Bool = { false }) throws -> String {
        guard (1...8).contains(password.count),password.allSatisfy({ (0x20...0x7e).contains($0) }) else { throw MacTab5LocalError.invalid("请输入 1–8 位英文字符的 VNC 专用密码。") }
        return try withConnection(isCancelled:isCancelled) { read,send,_ in try negotiate(password:password,read:read,send:send) }
    }
    static func withConnection<T>(timeout:TimeInterval = 8,isCancelled:@escaping () -> Bool,operation:((Int) throws -> Data,(Data) throws -> Void,() -> String?) throws -> T) throws -> T {
        guard !isCancelled() else { throw MacTab5LocalError.invalid("连接验证已取消。") }
        let socket = Darwin.socket(AF_INET,SOCK_STREAM,0)
        guard socket >= 0 else { throw MacTab5Error.unavailable }
        defer { Darwin.close(socket) }
        var noSignal:Int32 = 1, noDelay:Int32 = 1
        guard setsockopt(socket,SOL_SOCKET,SO_NOSIGPIPE,&noSignal,socklen_t(MemoryLayout<Int32>.size)) == 0,
              setsockopt(socket,IPPROTO_TCP,TCP_NODELAY,&noDelay,socklen_t(MemoryLayout<Int32>.size)) == 0,
              fcntl(socket,F_SETFL,O_NONBLOCK) == 0 else { throw MacTab5Error.unavailable }
        let deadline = ProcessInfo.processInfo.systemUptime+min(90,max(1,timeout))
        func wait(_ events:Int16) throws {
            while true {
                guard !isCancelled() else { throw MacTab5LocalError.invalid("连接验证已取消。") }
                let remaining = deadline-ProcessInfo.processInfo.systemUptime
                guard remaining > 0 else { throw MacTab5LocalError.invalid("本机 VNC 连接验证超时；未自动重试。") }
                var fd = pollfd(fd:socket,events:events,revents:0)
                let result = poll(&fd,1,Int32(min(100,remaining*1000)))
                if result < 0, errno == EINTR { continue }
                guard result >= 0 else { throw MacTab5LocalError.invalid("本机 VNC 连接未完成；未自动重试。") }
                if result == 0 { continue }
                guard !isCancelled() else { throw MacTab5LocalError.invalid("连接验证已取消。") }
                if fd.revents & events != 0 { return }
                throw MacTab5LocalError.invalid("本机 VNC 连接已结束；未自动重试。")
            }
        }
        var address = sockaddr_in(); address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size); address.sin_family = sa_family_t(AF_INET); address.sin_port = UInt16(5900).bigEndian
        inet_pton(AF_INET,"127.0.0.1",&address.sin_addr)
        let connected = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.connect(socket,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        if connected != 0 {
            guard errno == EINPROGRESS else { throw MacTab5LocalError.invalid("127.0.0.1:5900 未连接；请检查系统屏幕共享。") }
            try wait(Int16(POLLOUT)); var error:Int32 = 0, size = socklen_t(MemoryLayout<Int32>.size)
            guard getsockopt(socket,SOL_SOCKET,SO_ERROR,&error,&size) == 0, error == 0 else { throw MacTab5LocalError.invalid("本机 VNC 服务不可用。") }
        }
        func read(_ count:Int) throws -> Data {
            guard (1...4096).contains(count) else { throw MacTab5Error.invalidPacket }
            var bytes = [UInt8](repeating:0,count:count), offset = 0
            while offset < count {
                try wait(Int16(POLLIN))
                let n = bytes.withUnsafeMutableBytes { recv(socket,$0.baseAddress!.advanced(by:offset),count-offset,0) }
                if n < 0, [EAGAIN,EWOULDBLOCK,EINTR].contains(errno) { continue }
                guard n > 0 else { throw MacTab5LocalError.invalid("VNC 连接已结束。") }; offset += n
            }
            return Data(bytes)
        }
        func send(_ bytes:Data) throws {
            var offset = 0
            while offset < bytes.count {
                try wait(Int16(POLLOUT))
                let n = bytes.withUnsafeBytes { Darwin.send(socket,$0.baseAddress!.advanced(by:offset),bytes.count-offset,0) }
                if n < 0, [EAGAIN,EWOULDBLOCK,EINTR].contains(errno) { continue }
                guard n > 0 else { throw MacTab5LocalError.invalid("VNC 连接已结束。") }; offset += n
            }
        }
        func headerNote() -> String? {
            guard !isCancelled(),ProcessInfo.processInfo.systemUptime < deadline else { return nil }
            func peek(_ count:Int) -> Data? {
                var bytes = [UInt8](repeating:0,count:count)
                let received = bytes.withUnsafeMutableBytes { recv(socket,$0.baseAddress!,count,MSG_PEEK) }
                return received == count ? Data(bytes):nil
            }
            // Nonblocking and optional. Inspect only a known update header;
            // never peek at pixel, clipboard, name or other message payloads.
            guard let type = peek(1),type[0] == 0,let prefix = peek(4),prefix[2] != 0 || prefix[3] != 0,
                  let header = peek(16) else { return nil }
            return updateHeaderNote(header)
        }
        return try operation(read,send,headerNote)
    }
    static func updateHeaderNote(_ header:Data) -> String? {
        guard header.count == 16 else { return nil }
        let bytes = [UInt8](header)
        func u16(_ offset:Int) -> Int { Int(bytes[offset])*256+Int(bytes[offset+1]) }
        guard bytes[0] == 0,u16(2) > 0 else { return nil }
        let encoding = Int32(bitPattern:bytes[12..<16].reduce(UInt32(0)) { ($0 << 8) | UInt32($1) })
        return "图像响应头（仅诊断）：rectangles=\(u16(2)), x=\(u16(4)), y=\(u16(6)), width=\(u16(8)), height=\(u16(10)), encoding=\(encoding)。"
    }
    static func negotiate(password:Data,read:(Int) throws -> Data,send:(Data) throws -> Void) throws -> String {
        let banner = try read(12)
        guard banner.starts(with:Data("RFB 003.".utf8)),banner.last == 10 else { throw MacTab5Error.invalidPacket }
        try send(Data("RFB 003.008\n".utf8))
        let count = Int(try read(1)[0]); guard count > 0 else { throw MacTab5LocalError.invalid("VNC 服务拒绝协议协商。") }
        let methods = try read(count)
        guard methods.contains(2) else { throw MacTab5LocalError.invalid("服务未提供 VNC 密码认证；不会使用无认证模式。") }
        try send(Data([2]))
        var reply = try response(challenge:read(16),password:password)
        defer { reply.resetBytes(in:0..<reply.count) }
        try send(reply)
        guard try read(4) == Data(repeating:0,count:4) else { throw MacTab5LocalError.invalid("VNC 密码未获接受；未重试，也未发送按键。") }
        let (width,height) = try sharedSize(read:read,send:send)
        return "VNC 认证与 RFB 初始化通过（\(width)×\(height)）。这不证明连接到当前桌面或能够解锁；未发送按键。"
    }
    static func sharedSize(read:(Int) throws -> Data,send:(Data) throws -> Void) throws -> (Int,Int) {
        try send(Data([1])) // Shared session: never disconnect an existing viewer.
        let info = [UInt8](try read(24)), width = Int(info[0])*256+Int(info[1]), height = Int(info[2])*256+Int(info[3])
        let nameLength = info[20...23].reduce(0) { $0*256+Int($1) }
        guard (1...32768).contains(width),(1...32768).contains(height),nameLength <= 4096 else { throw MacTab5Error.invalidPacket }
        if nameLength > 0 { _ = try read(nameLength) }
        return (width,height)
    }
}

/// One-use delayed authentication probe, never an unlock/input authorization.
/// Cancellation and expiry discard the pending secret before a connection starts.
final class MacVNCCheckAttempt {
    private let lock = NSLock()
    private var id:UUID?, password:Data?, due:TimeInterval = 0, expiry:TimeInterval = 0
    func begin(password:Data,delay:TimeInterval,now:TimeInterval) -> UUID {
        lock.lock(); defer { lock.unlock() }
        clear(); let next = UUID(); id = next; self.password = password
        due = now+delay; expiry = due+10; return next
    }
    func take(_ expected:UUID,now:TimeInterval) -> Data? {
        lock.lock(); defer { lock.unlock() }
        guard id == expected else { return nil }
        guard now <= expiry else { clear(); return nil }
        guard now >= due, let value = password else { return nil }
        password = nil; return value
    }
    func isCurrent(_ expected:UUID) -> Bool { lock.lock(); defer { lock.unlock() }; return id == expected }
    func finish(_ expected:UUID) -> Bool {
        lock.lock(); defer { lock.unlock() }; guard id == expected else { return false }; clear(); return true
    }
    func cancel() { lock.lock(); defer { lock.unlock() }; clear() }
    private func clear() { if var bytes = password { password = nil; bytes.resetBytes(in:0..<bytes.count) }; id = nil }
    deinit { clear() }
}

/// A fresh user action after authentication starts the keyboard stage. The
/// system Screen Sharing connection popover may activate another application
/// after ServerInit; successful authentication alone must never start typing.
final class MacVNCKeyboardStart {
    static let settleSeconds:TimeInterval = 3, waitSeconds:TimeInterval = 30
    private let lock = NSLock()
    private var id:UUID?, due:TimeInterval = 0, expiry:TimeInterval = 0, approved = false
    func begin(_ id:UUID,now:TimeInterval) {
        lock.lock(); defer { lock.unlock() }
        self.id = id; due = now+Self.settleSeconds; expiry = now+Self.waitSeconds; approved = false
    }
    func canApprove(_ id:UUID,now:TimeInterval) -> Bool {
        lock.lock(); defer { lock.unlock() }
        return self.id == id && !approved && now >= due && now < expiry
    }
    func approve(_ id:UUID,now:TimeInterval) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard self.id == id,!approved,now >= due,now < expiry else { return false }
        approved = true; return true
    }
    func consume(_ id:UUID,now:TimeInterval) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard self.id == id,approved,now < expiry else { return false }
        self.id = nil; approved = false; return true
    }
    func cancel() { lock.lock(); defer { lock.unlock() }; id = nil; approved = false }
}

final class MacUnlockConnectionWindow: NSWindowController, NSWindowDelegate {
    private static var lastResult = "尚未开始实际解锁测试；TAB5 一键解锁能力保持关闭。"
    private let password = NSSecureTextField(), status = MacBridgeSettingsStyle.note("")
    private let loginPassword = NSSecureTextField()
    private let keyboardProbe = MacVNCKeyboardProbe()
    private let attempt = MacVNCCheckAttempt()
    private let keyboardStart = MacVNCKeyboardStart()
    private var waitingKeyboardID:UUID?
    private lazy var continueKeyboard = MacSettingsButton("继续键盘验证") { [weak self] in self?.continueKeyboardCheck() }
    private var pending:DispatchWorkItem?, countdown:Timer?
    private var sleepObserver:NSObjectProtocol?
    private var busy = false
    private var restoreActivation:NSApplication.ActivationPolicy?
    private var foregroundReady = false
    private let enrollment:Bool
    private var verified:((Data) throws -> Void)?
    init(enrollment:Bool = false) {
        self.enrollment = enrollment
        super.init(window:MacBridgeSettingsStyle.window("一键解锁 · 设置与测试",width:620,height:680,minimumHeight:500))
        window?.delegate = self
        sleepObserver = NSWorkspace.shared.notificationCenter.addObserver(forName:NSWorkspace.willSleepNotification,object:nil,queue:.main) { [weak self] _ in
            guard let self, self.busy else { return }; self.cancel(); self.status.stringValue = "电脑进入睡眠，本次验证已取消；唤醒后请重新开始。"
        }
        let page = MacBridgeSettingsPage(); page.heading("一键解锁测试",subtitle:"测试期间会显示 Dock 图标，关闭窗口后恢复菜单栏模式。TAB5 一键解锁仍待验收。")
        page.add(MacBridgeSettingsStyle.note(enrollment ? "本次为明确发起的密码保存操作：先验证本机账户和公开键盘样本，通过后才写入本机钥匙串。取消、失败或关闭不保存。":"仅连接本机 127.0.0.1:5900。密码仅供本次使用，不保存；点击、取消、关闭或睡眠时清除待用凭据。"))
        page.section("实际解锁测试")
        page.field("Mac 登录密码（当前登录账户）",loginPassword)
        page.add(MacBridgeSettingsStyle.note(enrollment ? "正在验证你明确提交保存的密码。成功后只保存到本机钥匙串，不发送给 TAB5；账户认证上限为 63 个 UTF-8 字节。":"使用当前 Mac 登录密码连接本机 Apple 屏幕共享，仅供本次测试使用，不保存。账户认证上限为 63 个 UTF-8 字节，不截断；实际解锁输入支持英文、数字及符号。"))
        page.add(MacSettingsButton("开始实际解锁测试") { [weak self] in self?.test(delay:0,unlock:true) })
        page.add(MacBridgeSettingsStyle.note("先验证键盘，再显示 15 秒倒计时。只有看到倒计时后才锁屏。"))
        page.add(MacSettingsButton("仅检查键盘（不会解锁）") { [weak self] in self?.test(delay:0,keyboard:true) })
        page.add(MacSettingsButton("本机键盘自检（无需密码）") { [weak self] in self?.localKeyboardCheck() })
        page.section("解锁流程与键盘样本")
        page.add(MacBridgeSettingsStyle.note("点击“开始实际解锁测试”，认证后等待系统屏幕共享提示，再点页脚“继续键盘验证”并松开键鼠。看到“实际解锁测试：键盘验证通过”及 15 秒倒计时后，按 Control-Command-Q 锁屏。锁屏后不要按键或触碰 Touch ID；30 秒仍未解锁，请自行解锁查看结果。仅检查键盘完成后连接即结束，不会在锁屏后解锁。"))
        page.field("键盘测试框（仅使用公开测试字符）",keyboardProbe.field)
        page.add(MacBridgeSettingsStyle.note("只尝试一次。键盘验证失败时直接显示原因，无需锁屏；测试取消、断连或窗口变化时停止，不自动重试。实际解锁仍待本机验收。"))
        page.section("VNC 连接诊断（不会解锁）")
        page.field("VNC 专用密码（1–8 位英文字符）",password)
        page.add(MacBridgeSettingsStyle.note("VNC 专用密码在本机连接到了独立虚拟登录会话。此处只验证连接；当前桌面键盘和实际解锁请使用上方 Mac 账户入口。"))
        page.add(MacBridgeSettingsStyle.note("连接检查只验证 VNC 认证，不会解锁，也不会发送按键。只需填写 VNC 专用密码。"))
        page.add(MacSettingsButton("仅检查连接") { [weak self] in self?.test(delay:0) })
        page.add(MacSettingsButton("15 秒后仅检查锁屏连接") { [weak self] in self?.test(delay:15) })
        status.stringValue = Self.lastResult
        continueKeyboard.isEnabled = false
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[continueKeyboard,MacSettingsButton("取消测试") { [weak self] in self?.cancel() },MacSettingsButton("关闭") { [weak self] in self?.window?.performClose(nil) }])
    }
    required init?(coder:NSCoder) { fatalError() }
    func open() {
        // A menu-bar agent can lose active/key status when the system VNC
        // server delivers HID input. Use the public ordinary-app presentation
        // for this window's lifetime, before any authentication or key event.
        if !foregroundReady {
            let previous = NSApp.activationPolicy()
            foregroundReady = previous == .regular || NSApp.setActivationPolicy(.regular)
            if foregroundReady,previous != .regular { restoreActivation = previous }
        }
        MacSettingsControls.open(self)
        if !foregroundReady { status.stringValue = "无法准备前台测试窗口，未开始账户认证或按键；请关闭后重新打开。" }
    }
    func beginEnrollment(_ password:Data,save:@escaping (Data) throws -> Void) {
        open(); verified = save
        test(delay:0,keyboard:true,supplied:password)
    }
    private func localKeyboardCheck() {
        guard !busy else { return }
        password.stringValue = ""; loginPassword.stringValue = ""
        busy = true; password.isEnabled = false; loginPassword.isEnabled = false
        let id = attempt.begin(password:Data(),delay:0,now:ProcessInfo.processInfo.systemUptime)
        status.stringValue = "本机自检准备中：稍后点击“继续键盘验证”，不需要密码、不连接 VNC、不锁屏。"
        let probe = keyboardProbe, attempt = self.attempt
        DispatchQueue.global(qos:.userInitiated).async { [weak self] in
            let result:String
            do {
                guard let self else { throw MacTab5Error.unavailable }
                try probe.requireStart(); try self.awaitKeyboardStart(id,authenticated:false)
                try probe.begin(); try probe.waitForInputSource(isCancelled:{ !attempt.isCurrent(id) })
                DispatchQueue.main.async { [weak self] in
                    guard attempt.isCurrent(id) else { return }
                    self?.status.stringValue = "本机自检：请在测试框依次输入 Ab9!@_-+，20 秒内完成；不需要密码、不连接 VNC、不锁屏。"
                }
                let deadline = ProcessInfo.processInfo.systemUptime+20
                while true {
                    guard attempt.isCurrent(id),ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5LocalError.invalid("本机自检已取消或超时；不连接 VNC，不向登录密码框输入。") }
                    let value = try probe.read()
                    guard MacVNCUnlockTest.keyboardSample.hasPrefix(value) else { throw MacTab5LocalError.invalid("本机自检字符不匹配；不连接 VNC，不向登录密码框输入。") }
                    if value == MacVNCUnlockTest.keyboardSample { break }
                    Thread.sleep(forTimeInterval:0.025)
                }
                result = "本机键盘自检通过；未连接 VNC，未使用账户凭据。\n焦点状态："+probe.diagnostic+"\n"+probe.preparationNote
            } catch { result = (error as? MacTab5LocalError)?.localizedDescription ?? "本机自检未完成；不连接 VNC，不向登录密码框输入。" }
            let counts = probe.keyCounts
            probe.end()
            DispatchQueue.main.async { [weak self] in
                guard attempt.finish(id),let self else { return }
                self.busy = false; self.password.isEnabled = true; self.loginPassword.isEnabled = true
                let receipt = result+"（本测试框收到按下 \(counts.down) 次、抬起 \(counts.up) 次。）"
                Self.lastResult = receipt; self.status.stringValue = receipt
            }
        }
    }
    /// No keys, probe focus changes or app activation occur while waiting.
    /// The timer only updates our own status/button. A late/stale click cannot
    /// authorize a different connection, nor can a local self-check approve it.
    private func awaitKeyboardStart(_ id:UUID,authenticated:Bool = true,unlock:Bool = false) throws {
        let start = ProcessInfo.processInfo.systemUptime
        keyboardStart.begin(id,now:start)
        DispatchQueue.main.async { [weak self] in
            guard let self,self.attempt.isCurrent(id) else { return }
            self.waitingKeyboardID = id
            func update() {
                guard self.attempt.isCurrent(id),self.waitingKeyboardID == id else { return }
                let now = ProcessInfo.processInfo.systemUptime
                self.continueKeyboard.isEnabled = self.keyboardStart.canApprove(id,now:now)
                let purpose = unlock ? "实际解锁测试：":"仅键盘检查（不会解锁）："
                let intro = authenticated ? purpose+"Mac 账户认证通过；请等待并关闭系统屏幕共享提示。同一连接，无需再次输入密码，暂勿锁屏。":"本机自检已准备；不使用密码、不连接 VNC、不锁屏。"
                self.status.stringValue = intro+(now < start+MacVNCKeyboardStart.settleSeconds
                    ? "稍后点击下方“继续键盘验证”。"
                    : "请点击下方“继续键盘验证”（剩余 \(max(0,Int(ceil(start+MacVNCKeyboardStart.waitSeconds-now)))) 秒）。")
            }
            update()
            self.countdown = Timer.scheduledTimer(withTimeInterval:0.25,repeats:true) { _ in update() }
        }
        defer {
            DispatchQueue.main.async { [weak self] in
                guard let self,self.waitingKeyboardID == id else { return }
                self.waitingKeyboardID = nil; self.continueKeyboard.isEnabled = false
                self.countdown?.invalidate(); self.countdown = nil
            }
        }
        while true {
            guard attempt.isCurrent(id) else { throw MacTab5LocalError.invalid("键盘验证已取消；未发送测试按键或登录输入。") }
            let now = ProcessInfo.processInfo.systemUptime
            guard now < start+MacVNCKeyboardStart.waitSeconds else {
                keyboardStart.cancel()
                throw MacTab5LocalError.invalid(authenticated
                    ? "等待“继续键盘验证”超时，连接已结束；未发送测试按键或登录输入，无需锁屏。"
                    : "本机自检等待“继续键盘验证”超时；未使用密码、未连接 VNC，无需锁屏。")
            }
            if keyboardStart.consume(id,now:now) { try keyboardProbe.requireStart(); return }
            Thread.sleep(forTimeInterval:0.05)
        }
    }
    private func continueKeyboardCheck() {
        guard busy,let id = waitingKeyboardID,attempt.isCurrent(id),NSApp.isActive,
              window?.isVisible == true,window?.isKeyWindow == true,
              keyboardStart.approve(id,now:ProcessInfo.processInfo.systemUptime) else { return }
        waitingKeyboardID = nil; continueKeyboard.isEnabled = false
        countdown?.invalidate(); countdown = nil
        status.stringValue = "请松开键盘、鼠标和触控板，保持 3 秒不操作；正在等待系统输入保护间隔结束，暂勿锁屏…"
    }
    private func test(delay:TimeInterval,unlock:Bool = false,keyboard:Bool = false,supplied:Data? = nil) {
        guard !busy else { return }
        guard !(unlock || keyboard) || foregroundReady else {
            password.stringValue = ""; loginPassword.stringValue = ""
            status.stringValue = "无法准备前台测试窗口，未开始账户认证或按键。"; return
        }
        var bytes = Data(password.stringValue.utf8); password.stringValue = ""
        var login = supplied ?? Data(loginPassword.stringValue.utf8); loginPassword.stringValue = ""
        defer { bytes.resetBytes(in:0..<bytes.count) }
        defer { login.resetBytes(in:0..<login.count) }
        if unlock || keyboard {
            do { try MacARDConnection.validate(username:Data(NSUserName().utf8),password:login); if unlock { try MacVNCUnlockTest.validate(login) } }
            catch { status.stringValue = error.localizedDescription; return }
            if unlock,!AXIsProcessTrusted() { status.stringValue = "实际解锁测试需要已有的辅助功能授权；未开始账户认证或倒计时。"; return }
        }
        else { guard (1...8).contains(bytes.count),bytes.allSatisfy({ (0x20...0x7e).contains($0) }) else { status.stringValue = "请输入 1–8 位英文字符的 VNC 专用密码。"; return } }
        busy = true; password.isEnabled = false; loginPassword.isEnabled = false
        let now = ProcessInfo.processInfo.systemUptime, due = now+delay
        let id = attempt.begin(password:unlock || keyboard ? login:bytes,delay:delay,now:now)
        if delay > 0 {
            status.stringValue = unlock ? "15 秒后先准备密码页，再尝试一次解锁。请现在锁屏，停留在指纹或密码界面；40 秒后若仍未解锁，请自行解锁查看原因。":"15 秒后仅检查连接。请现在锁屏，约 25 秒后自行解锁查看结果；本次不会解锁。"
            countdown = Timer.scheduledTimer(withTimeInterval:1,repeats:true) { [weak self] _ in
                guard let self, self.attempt.isCurrent(id) else { return }
                let remaining = max(0,Int(ceil(due-ProcessInfo.processInfo.systemUptime)))
                self.status.stringValue = remaining > 0 ? "\(remaining) 秒后\(unlock ? "准备密码页并尝试一次解锁，请保持锁屏。":"仅检查连接，本次不会解锁。")":"正在处理本次测试…"
            }
        }
        let work = DispatchWorkItem { [weak self] in self?.run(id,delayed:delay > 0,unlock:unlock,keyboard:keyboard) }
        pending = work; DispatchQueue.main.asyncAfter(deadline:.now()+delay,execute:work)
    }
    private func run(_ id:UUID,delayed:Bool,unlock:Bool,keyboard:Bool) {
        guard attempt.isCurrent(id) else { return }
        pending = nil; countdown?.invalidate(); countdown = nil
        guard var bytes = attempt.take(id,now:ProcessInfo.processInfo.systemUptime) else {
            _ = attempt.finish(id)
            busy = false; password.isEnabled = true; loginPassword.isEnabled = true; status.stringValue = "延时测试已过期；请重新输入密码后开始。"; return
        }
        let lockObserved = MacScreenLockObservation.shared.displayedValue == true
        status.stringValue = unlock ? "实际解锁测试：正在认证 Mac 账户，暂勿锁屏…":keyboard ? "仅键盘检查（不会解锁）：正在认证 Mac 账户…":"正在验证本机 VNC 连接…"
        let attempt = self.attempt, probe = keyboardProbe
        DispatchQueue.global(qos:.userInitiated).async { [weak self] in
            var result:String
            var verifiedPassword:Data?
            do {
                if unlock {
                    result = try MacVNCUnlockTest.run(loginPassword:bytes,probe:probe,isCancelled:{ !attempt.isCurrent(id) },startKeyboard:{ [weak self] in
                        guard let self else { throw MacTab5Error.unavailable }; try self.awaitKeyboardStart(id,unlock:true)
                    },progress:{ message in
                        DispatchQueue.main.async { [weak self] in guard attempt.isCurrent(id) else { return }; self?.status.stringValue = message }
                    })
                }
                else if keyboard {
                    result = try MacVNCUnlockTest.checkKeyboard(accountPassword:bytes,probe:probe,isCancelled:{ !attempt.isCurrent(id) },startKeyboard:{ [weak self] in
                        guard let self else { throw MacTab5Error.unavailable }; try self.awaitKeyboardStart(id)
                    })
                    if self?.enrollment == true { verifiedPassword = bytes }
                }
                else {
                    let connection = try MacVNCConnectionCheck.check(password:bytes,isCancelled:{ !attempt.isCurrent(id) })
                    result = delayed ? "仅检查连接：\(connection)\n\(lockObserved ? "开始连接时收到过锁屏通知。":"开始连接时未能确认锁屏状态。") 本次不会执行解锁。" : connection
                }
            }
            catch { result = (error as? MacTab5LocalError)?.localizedDescription ?? (unlock ? "解锁测试未完成，已停止且不重试。请自行检查密码框；可能有部分输入。":keyboard ? "Mac 账户键盘验证未完成；无需锁屏，不会自动重试。":"连接验证未完成；未重试，也未发送按键。") }
            bytes.resetBytes(in:0..<bytes.count)
            let savedResult = result, saveCandidate = verifiedPassword
            DispatchQueue.main.async { [weak self] in
                var verifiedPassword = saveCandidate, result = savedResult
                defer { if let size = verifiedPassword?.count { verifiedPassword?.resetBytes(in:0..<size) } }
                guard attempt.finish(id), let self else { return }
                if let verifiedPassword,let save = self.verified {
                    do { try save(verifiedPassword); result = "Mac 账户及公开键盘样本验证通过，密码已保存到本机钥匙串。请返回一键解锁设置，选择允许 Wi-Fi 解锁并保存。" }
                    catch { result = "验证通过，但密码未保存："+error.localizedDescription }
                    self.verified = nil
                }
                let labeled = (unlock ? "实际解锁测试结果：\n":keyboard ? "仅键盘检查结果（不会解锁）：\n":"")+result
                self.busy = false; self.password.isEnabled = true; self.loginPassword.isEnabled = true; Self.lastResult = labeled; self.status.stringValue = labeled
            }
        }
    }
    private func cancel() {
        verified = nil
        pending?.cancel(); pending = nil; countdown?.invalidate(); countdown = nil
        attempt.cancel(); keyboardStart.cancel(); waitingKeyboardID = nil; continueKeyboard.isEnabled = false
        busy = false; password.isEnabled = true; password.stringValue = ""; loginPassword.isEnabled = true; loginPassword.stringValue = ""
        status.stringValue = "本次测试已取消，不会重试。在途输入无法撤回；如已开始解锁测试，请自行检查密码框。"
    }
    func windowShouldClose(_ sender:NSWindow) -> Bool {
        if busy { cancel() }
        else { password.stringValue = ""; loginPassword.stringValue = "" }
        return true
    }
    func windowWillClose(_ notification:Notification) {
        if let previous = restoreActivation { _ = NSApp.setActivationPolicy(previous); restoreActivation = nil }
        foregroundReady = false
    }
    deinit { pending?.cancel(); countdown?.invalidate(); attempt.cancel(); keyboardStart.cancel(); if let sleepObserver { NSWorkspace.shared.notificationCenter.removeObserver(sleepObserver) } }
}
