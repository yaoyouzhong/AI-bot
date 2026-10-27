import AppKit
import ApplicationServices
import Security
import IOKit.pwr_mgt

/// Explicit local, one-use test. No credential storage or device capability.
/// Public AX must confirm the current console's signed loginwindow and focused
/// secure field. Before locking, the same RFB connection must echo a public
/// test string into this bridge's own field. Account authentication is separate
/// from the public sample; no password is typed into the probe.
enum MacVNCUnlockTest {
    private static let gate = NSLock()
    static let pending = "已提交一次登录输入，尚未确认解锁。若已自动回到桌面，请回复结果；否则请自行解锁查看。不会自动重试。"
    static func validate(_ password:Data) throws {
        guard (1...128).contains(password.count),password.allSatisfy({ (0x20...0x7e).contains($0) }) else {
            throw MacTab5LocalError.invalid("本次解锁测试仅支持 1–128 位英文、数字及符号的 Mac 登录密码；不支持的字符不会被截断或替换。")
        }
    }
    @inline(never)
    static func usesCounter(error:AXError,number:CFTypeRef?,replacingExistingInput:Bool = false) throws -> Bool {
        if error == .attributeUnsupported { return false }
        guard error == .success,let number,CFGetTypeID(number) == CFNumberGetTypeID(),
              let count = (number as? NSNumber)?.doubleValue,let characters = Int(exactly:count),characters >= 0,
              replacingExistingInput || characters == 0 else {
            throw MacTab5LocalError.invalid("密码框字符计数不可用或非空（AX \(error.rawValue)），未向登录密码框输入。")
        }
        return true
    }
    static let keyboardSample = "Ab9!@_-+"
    static let inputQuietSeconds:TimeInterval = 3, inputReadyTimeout:TimeInterval = 10
    /// Notifications may veto desktop confirmation, but neither their presence
    /// nor disappearance authorizes password input or proves success.
    static func lockedState() -> Bool? {
        guard MacScreenProtection.read().active == false else { return nil }
        let observedBefore = MacScreenLockObservation.shared.displayedValue
        let focus = focusedLockState()
        let observedAfter = MacScreenLockObservation.shared.displayedValue
        return MacScreenLockObservation.credentialState(focus:focus,
            observed:observedBefore == true || observedAfter == true ? true:observedAfter,
            protection:MacScreenProtection.read().active)
    }
    static func displayedLockState() -> Bool? {
        guard MacLoginPasswordTarget.onConsole(getuid()) else { return nil }
        let focus = focusedLockState(displayOnly:true)
        let windows = visibleLoginWindowObservation()
        // Cold start has no distributed unlock observation. Matching desktop
        // AX focus plus a complete, empty signed-loginwindow observation may
        // establish display-only state. Never used by lockedState().
        let desktopEvidence = windows.absent && focus == false
        let protection = MacScreenProtection.read()
        let displayFocus = focus == false && !windows.absent ? nil:focus
        let result = MacScreenLockObservation.displayState(focus:displayFocus,observed:MacScreenLockObservation.shared.displayedValue,protection:protection.active,desktopEvidence:desktopEvidence)
        // Window absence can support display-only desktop focus.
        // Credential checks never use it; a protection cover always vetoes.
        MacScreenLockObservation.shared.recordStatus(focus:focus,returned:result,windows:windows.summary+"；桌面辅助证据 \(desktopEvidence ? 1:0)",protection:protection.summary)
        return result
    }
    private static func visibleLoginWindowObservation() -> (summary:String,absent:Bool) {
        guard MacLoginPasswordTarget.onConsole(getuid()),
              let rows = CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]],
              rows.count <= 512 else { return ("登录屏上 ?",false) }
        let deadline = ProcessInfo.processInfo.systemUptime+0.5
        var verified:[pid_t:Bool] = [:], layers:[Int] = []
        for row in rows {
            guard ProcessInfo.processInfo.systemUptime < deadline else { return ("登录屏上 ?",false) }
            guard let owner = row[kCGWindowOwnerPID as String] as? NSNumber,
                  CFGetTypeID(owner) != CFBooleanGetTypeID(),owner.int64Value > 0,owner.int64Value <= Int64(Int32.max) else { return ("登录屏上 ?",false) }
            let pid = owner.int32Value
            if verified[pid] == nil { verified[pid] = MacLoginPasswordTarget.signedLoginWindow(pid) }
            guard verified[pid] == true else { continue }
            // Do not read names, titles, bounds, pixels or any other app's metadata.
            guard let visible = row[kCGWindowIsOnscreen as String],CFGetTypeID(visible as CFTypeRef) == CFBooleanGetTypeID(),visible as? Bool == true,
                  let alpha = row[kCGWindowAlpha as String] as? NSNumber,CFGetTypeID(alpha) != CFBooleanGetTypeID(),alpha.doubleValue.isFinite,
                  let layer = row[kCGWindowLayer as String] as? NSNumber,CFGetTypeID(layer) != CFBooleanGetTypeID() else { return ("登录屏上 ?",false) }
            if alpha.doubleValue > 0 { layers.append(layer.intValue) }
            guard layers.count <= 8 else { return ("登录屏上 >8",false) }
        }
        return ("登录屏上 \(layers.count)，层[\(layers.sorted().map(String.init).joined(separator:","))]",layers.isEmpty)
    }
    private static func focusedLockState(displayOnly:Bool = false) -> Bool? {
        guard AXIsProcessTrusted(),MacLoginPasswordTarget.onConsole(getuid()) else { return nil }
        let system = AXUIElementCreateSystemWide()
        _ = AXUIElementSetMessagingTimeout(system,0.15)
        guard let app = MacLoginPasswordTarget.ax(MacLoginPasswordTarget.attribute(system,kAXFocusedApplicationAttribute)) else { return nil }
        var pid:pid_t = 0
        guard AXUIElementGetPid(app,&pid) == .success,pid > 0 else { return nil }
        if MacLoginPasswordTarget.signedLoginWindow(pid) { return true }
        // A service popover or unknown focus is not an unlocked desktop.
        // Require the system AX recipient and regular foreground application
        // to agree in this user's still-current physical console.
        let front = NSWorkspace.shared.frontmostApplication
        let regular = front?.processIdentifier == pid && front?.activationPolicy == .regular && front?.isActive == true
        // Our menu-bar windows are accessory applications. They may describe
        // the desktop, but never become credential-input or unlock-success proof.
        return MacScreenLockObservation.desktopFocus(regularFront:regular,ownProcess:pid == getpid(),displayOnly:displayOnly) ? false:nil
    }
    /// Device entry uses only this request's public AX target. It deliberately
    /// opens no screen-sharing session: that service restores the prior lock
    /// state on disconnect. A verified secure field is written once; unsupported
    /// submission stops without replaying input or falling back to another path.
    static func unlockSaved(_ password:Data,isCancelled:@escaping () -> Bool,progress:@escaping (String) -> Void,stableDesktopSeconds:TimeInterval = 2) throws -> Bool {
        guard gate.try() else { throw MacTab5Error.busy }
        defer { gate.unlock() }
        try validate(password)
        guard [0.5,2].contains(stableDesktopSeconds) else { throw MacTab5Error.invalidPacket }
        guard !isCancelled() else { throw MacTab5Error.unconfirmed }
        progress("waking-display")
        return try withVerifiedDisplay(isCancelled:isCancelled) {
            try waitForLocked(isCancelled:isCancelled)
            progress("waiting-input")
            try waitForInputSource(snapshot:{
                guard lockedState() == true else { throw MacTab5Error.unconfirmed }
                return .init(source:"signed-loginwindow-ax",revision:0)
            },localIdleSeconds:{ CGEventSource.secondsSinceLastEventType(.combinedSessionState,eventType:CGEventType(rawValue:UInt32.max)!) },isCancelled:isCancelled,sourceStableSeconds:0.15)
            progress("preparing-field")
            let target = try MacLoginPasswordTarget.prepare(send:{ _ in },isCancelled:isCancelled,progress:{ _ in },replacingExistingInput:true)
            let submission = try target.directSubmission(isCancelled:isCancelled)
            progress("revealing-field")
            try prepareForDirectSubmission(ready:submission.isCurrent,observe:submission.preparation,
                send:{ try postPublicCharacter(target,isCancelled:isCancelled) },isCancelled:isCancelled)
            progress("sending")
            try submitDirectOnce(password,ready:submission.isCurrent,write:target.writeDirect,confirm:{
                progress("pressing-login-button")
                try submission.perform()
            },isCancelled:isCancelled)
            progress("confirming")
            return try confirmDirectDesktop(isCancelled:isCancelled,stableSeconds:stableDesktopSeconds)
        }
    }
    /// One explicitly armed diagnostic. No credential argument, AX value write,
    /// Return, deletion, screen-sharing session or automatic unlock follows.
    static func probePrintablePreparation(isCancelled:@escaping () -> Bool,progress:@escaping (String) -> Void) throws {
        guard gate.try() else { throw MacTab5Error.busy }
        defer { gate.unlock() }
        try withDisplayActivity {
            progress("waking-display")
            try waitForLocked(isCancelled:isCancelled)
            progress("waiting-input")
            try waitForInputSource(snapshot:{
                guard lockedState() == true else { throw MacTab5Error.unconfirmed }
                return .init(source:"signed-loginwindow-ax",revision:0)
            },localIdleSeconds:{ CGEventSource.secondsSinceLastEventType(.combinedSessionState,eventType:CGEventType(rawValue:UInt32.max)!) },isCancelled:isCancelled)
            // Bind the independently verified secure field before posting.
            // A hidden field may report focus; visibility is the human observation.
            let target = try MacLoginPasswordTarget.prepare(send:{ _ in },isCancelled:isCancelled,progress:{ _ in })
            progress("public-character-probe")
            try postPublicCharacter(target,isCancelled:isCancelled)
        }
    }
    private static func postPublicCharacter(_ target:MacLoginPasswordTarget,isCancelled:() -> Bool) throws {
        guard CGPreflightPostEventAccess(),
              let down = CGEvent(keyboardEventSource:nil,virtualKey:0,keyDown:true),
              let up = CGEvent(keyboardEventSource:nil,virtualKey:0,keyDown:false) else {
            throw MacTab5LocalError.invalid("未取得已有事件权限，未发送公开测试字符。")
        }
        var character:UniChar = 0x61 // Fixed public sample 'a', never a password.
        for event in [down,up] {
            event.flags = []
            event.keyboardSetUnicodeString(stringLength:1,unicodeString:&character)
        }
        let modifiers:CGEventFlags = [.maskCommand,.maskControl,.maskAlternate,.maskShift,.maskSecondaryFn,.maskAlphaShift]
        try publicCharacterOnce(ready:{
            target.isReady && lockedState() == true &&
                CGEventSource.flagsState(.combinedSessionState).intersection(modifiers).isEmpty &&
                !CGEventSource.keyState(.combinedSessionState,key:0)
        },isCancelled:isCancelled,down:{ down.post(tap:.cghidEventTap) },up:{ up.post(tap:.cghidEventTap) })
    }
    struct PreparationObservation {
        let characters:Int?
        let loginEnabled:Bool
    }
    /// An already enabled login page may retain input from a cancelled attempt.
    /// Revalidate its stable state without adding another preparation character.
    /// Otherwise require the public character's acknowledgement, not just posting.
    /// The caller always replaces the entire AXValue once, never appending.
    @inline(never)
    static func prepareForDirectSubmission(ready:() -> Bool,observe:() throws -> PreparationObservation,
                                           send:() throws -> Void,isCancelled:() -> Bool,
                                           now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                           pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws {
        let deadline = now()+6
        func check() throws {
            guard !isCancelled(),now()<deadline,ready(),!isCancelled(),now()<deadline else {
                throw MacTab5LocalError.invalid("密码页字符准备已取消、超时或目标变化；未填密码、未提交，也不重试。若有测试字符，请自行清空。")
            }
        }
        try check()
        let initial = try observe()
        try check()
        if initial.loginEnabled && (initial.characters.map({ $0 > 0 }) ?? true) {
            let readySince = now()
            repeat {
                pause()
                try check()
                let sample = try observe()
                try check()
                guard sample.loginEnabled,sample.characters == initial.characters else {
                    throw MacTab5LocalError.invalid("已有密码页的就绪状态发生变化，未输入或提交；请自行检查后重新发起。")
                }
            } while now()-readySince < 0.15
            return
        }
        guard initial.characters.map({ $0 == 0 }) ?? !initial.loginEnabled else {
            throw MacTab5LocalError.invalid("密码框可能已有输入，未发送准备字符或密码；请清空后重新发起。")
        }
        try send() // Reserve one balanced sample only; never replay on timeout.
        var readySince:TimeInterval?
        while true {
            try check()
            let sample = try observe()
            try check()
            guard (initial.characters == nil) == (sample.characters == nil),
                  sample.characters.map({ $0 == 0 || $0 == 1 }) ?? true else {
                throw MacTab5LocalError.invalid("准备字符的确认状态异常，未填密码或提交；请自行清空后查看。")
            }
            let acknowledged = sample.loginEnabled && (sample.characters.map({ $0 == 1 }) ?? true)
            if acknowledged {
                if let readySince,now()-readySince >= 0.15 { return }
                if readySince == nil { readySince = now() }
            } else if readySince != nil {
                throw MacTab5LocalError.invalid("密码页准备状态再次变化，未填密码或提交，也不重试。")
            }
            pause()
        }
    }
    static func publicCharacterOnce(ready:() -> Bool,isCancelled:() -> Bool,down:() -> Void,up:() -> Void,
                                     now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                     hold:() -> Void = { Thread.sleep(forTimeInterval:0.04) }) throws {
        let deadline = now()+2
        guard !isCancelled(),now()<deadline,ready(),!isCancelled(),now()<deadline else { throw MacTab5Error.unconfirmed }
        down()
        hold()
        up() // Balance even after cancellation/target change; never replay down.
        guard !isCancelled(),now()<deadline,ready(),!isCancelled(),now()<deadline else {
            throw MacTab5LocalError.invalid("公开字符诊断发送后目标变化或已取消；已释放按键，没有提交或重试。请自行清空密码框。")
        }
    }
    /// A paired, authenticated request may wake the display while focus is
    /// unknown. Only a fresh, independently verified loginwindow permits input.
    static func waitForLocked(state:() -> Bool? = lockedState,isCancelled:() -> Bool,
                              now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                              pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws {
        let deadline = now()+3
        repeat {
            guard !isCancelled(),now() < deadline else { throw MacTab5LocalError.invalid("显示器唤醒后仍无法确认登录窗口，未输入密码。") }
            let locked = state()
            guard !isCancelled(),now() < deadline else { throw MacTab5Error.unconfirmed }
            if locked == true { return }
            guard locked != false else { throw MacTab5LocalError.invalid("当前桌面未锁定，未输入密码。") }
            pause()
        } while true
    }
    /// Exact public button labels only, never substring matches, account
    /// titles, secure-field text, coordinates or a guessed first button.
    static func explicitLoginLabel(title:String?,description:String?) -> Bool {
        let title = title?.trimmingCharacters(in:.whitespacesAndNewlines) ?? ""
        let value = title.isEmpty ? (description?.trimmingCharacters(in:.whitespacesAndNewlines) ?? "") : title
        return ["log in","login","sign in","unlock","登录","登入","解锁"].contains(value.lowercased())
    }
    static func submitDirectOnce(_ password:Data,ready:() -> Bool,write:(Data) throws -> Void,
                                 confirm:() throws -> Void,isCancelled:() -> Bool,
                                 now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) throws {
        try validate(password)
        let deadline = now()+3
        func check() throws {
            guard !isCancelled(),now() < deadline,ready(),!isCancelled(),now() < deadline else { throw MacTab5Error.unconfirmed }
        }
        try check()
        try write(password) // One replacement; errors/uncertainty never retry.
        try check()
        try confirm() // One action; may immediately dismiss the original field.
    }
    static func confirmDirectDesktop(state:() -> Bool? = lockedState,isCancelled:() -> Bool,
                                     stableSeconds:TimeInterval = 2,
                                     now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                     pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws -> Bool {
        guard [0.5,2].contains(stableSeconds) else { throw MacTab5Error.invalidPacket }
        let deadline = now()+8
        var readySince:TimeInterval?, lastSample:TimeInterval?
        var readySamples = 0
        while !isCancelled(),now() < deadline {
            let locked = state(), time = now()
            guard !isCancelled(),time < deadline else { return false }
            if locked == false {
                if lastSample == nil || time > lastSample! { readySamples += 1; lastSample = time }
                // Local USB/BLE feedback requires three distinct fresh reads.
                // The accepted Wi-Fi two-second confirmation order is retained.
                if let readySince,time-readySince >= stableSeconds,readySamples >= (stableSeconds < 2 ? 3:1) { return true }
                if readySince == nil { readySince = time }
            } else {
                if readySince != nil,locked == true { throw MacTab5LocalError.invalid("桌面恢复后再次锁屏，本次未保持解锁，已停止且不重试。") }
                readySince = nil
                readySamples = 0; lastSample = nil
            }
            pause()
        }
        return false
    }
    static func withDisplayActivity<T>(create:() throws -> IOPMAssertionID = declareDisplayActivity,
                                      release:(IOPMAssertionID) -> Void = { _ = IOPMAssertionRelease($0) },
                                      operation:() throws -> T) throws -> T {
        let assertion = try create()
        defer { release(assertion) }
        return try operation()
    }
    /// A dark display can make protection geometry unavailable. Unknown permits
    /// display activity only; no keyboard event or credential operation runs
    /// until a fresh observation positively confirms the protection is absent.
    static func withVerifiedDisplay<T>(protection:() -> Bool? = { MacScreenProtection.read().active },
                                       isCancelled:() -> Bool,
                                       create:() throws -> IOPMAssertionID = declareDisplayActivity,
                                       release:(IOPMAssertionID) -> Void = { _ = IOPMAssertionRelease($0) },
                                       now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                       pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) },
                                       operation:() throws -> T) throws -> T {
        let deadline = now()+3
        func check() throws {
            guard !isCancelled(),now()<deadline else { throw MacTab5Error.unconfirmed }
        }
        try check()
        let initial = protection()
        try check()
        guard initial != true else {
            throw MacTab5LocalError.invalid("本机外层保护界面仍在显示；未输入密码。请先在 Mac 结束 Computer Use 并回到系统锁屏，再重新发起。")
        }
        return try withDisplayActivity(create:create,release:release) {
            while true {
                try check()
                let state = protection()
                try check()
                if state == false { return try operation() }
                guard state == nil else {
                    throw MacTab5LocalError.invalid("显示器唤醒后仍有本机外层保护；未输入密码。")
                }
                pause()
            }
        }
    }
    private static func declareDisplayActivity() throws -> IOPMAssertionID {
        var assertion = IOPMAssertionID(0)
        guard IOPMAssertionDeclareUserActivity("AI-bot user-requested unlock" as CFString,kIOPMUserActiveLocal,&assertion) == kIOReturnSuccess else {
            throw MacTab5LocalError.invalid("系统未接受本次显示器唤醒请求，未输入密码。")
        }
        // Bounded even if the worker stalls; never alter global sleep policy.
        guard IOPMAssertionSetProperty(assertion,kIOPMAssertionTimeoutKey as CFString,NSNumber(value:40)) == kIOReturnSuccess else {
            _ = IOPMAssertionRelease(assertion)
            throw MacTab5LocalError.invalid("无法限定显示器唤醒时限，未输入密码。")
        }
        return assertion
    }
    static func confirmDisconnectedDesktop(state:() -> Bool? = lockedState,isCancelled:() -> Bool,
                                            now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                            pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws -> Bool {
        let deadline = now()+3
        var readySince:TimeInterval?
        while !isCancelled(),now() < deadline {
            let locked = state(), time = now()
            guard !isCancelled(),time < deadline else { return false }
            if locked == true { throw MacTab5LocalError.invalid("屏幕共享连接结束后系统再次锁屏；本次未保持解锁，已停止且不重试。") }
            if locked == false {
                if let readySince,time-readySince >= 2 { return true }
                if readySince == nil { readySince = time }
            } else { readySince = nil }
            pause()
        }
        return false
    }
    static func checkKeyboard(accountPassword:Data,probe:MacVNCKeyboardProbe,isCancelled:@escaping () -> Bool,startKeyboard:() throws -> Void) throws -> String {
        guard gate.try() else { throw MacTab5LocalError.invalid("已有一次键盘／解锁测试在进行，本次未开始。") }
        defer { gate.unlock() }
        return try MacVNCConnectionCheck.withConnection(timeout:60,isCancelled:isCancelled) { read,send,headerNote in
            try probe.requireStart()
            try MacARDConnection.negotiate(password:accountPassword,read:read,send:send,isCancelled:isCancelled)
            try startKeyboard()
            try verifyProbe(probe,send:{ try pacedKey($0,send:send) },isCancelled:isCancelled,headerNote:headerNote)
            return "Mac 账户及当前桌面键盘检查通过，公开字符全部匹配。连接已结束，不会执行解锁，请勿锁屏。实际解锁请选择“开始实际解锁测试”。本次凭据未保存。"
        }
    }
    private static func verifyProbe(_ probe:MacVNCKeyboardProbe,send:(Data) throws -> Void,isCancelled:() -> Bool,headerNote:() -> String?) throws {
        defer { probe.end() }
        guard !isCancelled() else { throw MacTab5LocalError.invalid("键盘验证已取消，未向登录密码框输入。") }
        try probe.begin()
        do {
            try probe.waitForInputSource(isCancelled:isCancelled)
            try verifyKeyboard(value:{ try probe.read() },send:send,isCancelled:isCancelled)
        }
        catch let error as MacTab5LocalError { throw MacTab5LocalError.invalid(error.localizedDescription+"（本测试框收到按下 \(probe.keyCounts.down) 次、抬起 \(probe.keyCounts.up) 次。）\n"+probe.modifierNote+"\n焦点状态："+probe.diagnostic+"\n"+probe.preparationNote+(headerNote().map { "\n"+$0 } ?? "")) }
    }
    static func run(loginPassword:Data,probe:MacVNCKeyboardProbe,isCancelled:@escaping () -> Bool,startKeyboard:() throws -> Void,progress:@escaping (String) -> Void = { _ in }) throws -> String {
        guard gate.try() else { throw MacTab5LocalError.invalid("已有一次解锁测试在进行，本次未开始。") }
        defer { gate.unlock() }
        guard !isCancelled() else { throw MacTab5LocalError.invalid("解锁测试已取消，未向登录密码框输入。") }
        try validate(loginPassword)
        guard AXIsProcessTrusted() else { throw MacTab5LocalError.invalid("未取得已有辅助功能授权，未向登录密码框输入。") }
        progress("实际解锁测试：正在使用 Mac 账户认证本机 Apple 屏幕共享，暂勿锁屏…")
        return try MacVNCConnectionCheck.withConnection(timeout:90,isCancelled:isCancelled) { read,send,headerNote in
            try probe.requireStart()
            try MacARDConnection.negotiate(password:loginPassword,read:read,send:send,isCancelled:isCancelled)
            try startKeyboard()
            func input(_ packet:Data) throws { try pacedKey(packet,send:send) }
            progress("实际解锁测试：Mac 账户已认证。请松开键盘、鼠标和触控板，保持 3 秒不操作，等待键盘验证；暂勿锁屏…")
            try verifyProbe(probe,send:input,isCancelled:isCancelled,headerNote:headerNote)
            try waitForLock(isCancelled:isCancelled,progress:progress)
            progress("键盘通道已验证，正在准备登录密码框…")
            let target = try MacLoginPasswordTarget.prepare(send:input,isCancelled:isCancelled,progress:progress)
            if target.supportsCount {
                progress("密码框已聚焦，正在确认本机输入通道…")
                try sendOnce(loginPassword,count:{ try target.count() },send:input,isCancelled:isCancelled)
            } else {
                progress("同一连接的键盘已验证，登录密码框已聚焦，正在提交一次登录输入…")
                try sendFocused(loginPassword,ready:{ target.isReady },send:input,isCancelled:isCancelled)
            }
            return pending
        }
    }
    /// An explicit preparation can request at most one non-printing Tab.
    /// The caller must freshly verify the signed loginwindow before this call.
    static func prepareField<T>(find:() throws -> T?,stillLoginWindow:() -> Bool,send:(Data) throws -> Void,isCancelled:() -> Bool,
                                now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws -> T {
        let deadline = now()+4
        var tabSent = false
        repeat {
            guard !isCancelled(),now() < deadline else { throw MacTab5LocalError.invalid("密码页准备已取消或超时，未向登录密码框输入。") }
            guard stillLoginWindow() else { throw MacTab5LocalError.invalid("当前焦点已离开可确认的 Apple 登录窗口，未向登录密码框输入。") }
            if let field = try find() {
                guard !isCancelled(),now() < deadline,stillLoginWindow() else { throw MacTab5LocalError.invalid("密码页准备结果已过期或窗口变化，未向登录密码框输入。") }
                return field
            }
            if !tabSent {
                guard !isCancelled(),now() < deadline,stillLoginWindow() else { throw MacTab5LocalError.invalid("登录窗口发生变化，未向登录密码框输入。") }
                tabSent = true // Reserve before I/O. Uncertain delivery is never replayed.
                var tab = key(0xff09,down:true)+key(0xff09,down:false)
                defer { tab.resetBytes(in:0..<tab.count) }
                try send(tab)
            }
            pause()
        } while true
    }
    static func key(_ value:UInt32,down:Bool) -> Data {
        Data([4,down ? 1:0,0,0,UInt8(truncatingIfNeeded:value >> 24),UInt8(truncatingIfNeeded:value >> 16),UInt8(truncatingIfNeeded:value >> 8),UInt8(truncatingIfNeeded:value)])
    }
    /// Match the current Windows bridge's 40 ms key hold. Only a validated
    /// balanced KeyEvent pair enters this path, never authentication bytes.
    /// An uncertain edge is not replayed; unwinding closes the RFB connection.
    static func pacedKey(_ packet:Data,send:(Data) throws -> Void,pause:() -> Void = { Thread.sleep(forTimeInterval:0.04) }) throws {
        guard packet.count == 16,packet[0] == 4,packet[1] == 1,packet[8] == 4,packet[9] == 0,
              packet[2..<4].allSatisfy({ $0 == 0 }),packet[10..<12].allSatisfy({ $0 == 0 }),packet[4..<8] == packet[12..<16] else { throw MacTab5Error.invalidPacket }
        var down = Data(packet.prefix(8)), up = Data(packet.suffix(8))
        defer { down.resetBytes(in:0..<down.count); up.resetBytes(in:0..<up.count) }
        try send(down); pause(); try send(up)
    }
    /// Readiness is a bounded quiet interval, not a VNC delivery acknowledgement.
    /// The local Apple server suppresses remote key-down shortly after cursor
    /// activity. Respect that interval using public elapsed-input metadata;
    /// source stability alone does not establish that the mouse has stopped.
    /// Direct AX replacement has independent target checks and needs only a
    /// short fresh-focus interval. Its physical-input quiet requirement remains
    /// three seconds; keyboard enrollment and RFB retain full source stability.
    static func waitForInputSource(snapshot:() throws -> MacVNCInputSourceSnapshot?,localIdleSeconds:() -> TimeInterval?,isCancelled:() -> Bool,
                                   sourceStableSeconds:TimeInterval = inputQuietSeconds,
                                   now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.025) }) throws {
        guard sourceStableSeconds.isFinite,(0.15...inputQuietSeconds).contains(sourceStableSeconds) else { throw MacTab5Error.invalidPacket }
        let deadline = now()+inputReadyTimeout
        var previous:MacVNCInputSourceSnapshot?, since = now()
        while true {
            guard !isCancelled(),now() < deadline else { throw MacTab5LocalError.invalid("等待键鼠静止及输入源准备已取消或超时；未发送测试按键或登录输入，无需锁屏。") }
            let current = try snapshot()
            let idle = localIdleSeconds()
            let time = now()
            guard !isCancelled(),time < deadline else { throw MacTab5LocalError.invalid("等待键鼠静止及输入源准备已取消或超时；未发送测试按键或登录输入，无需锁屏。") }
            if current == nil || current != previous { previous = current; since = time }
            if current != nil,time-since >= sourceStableSeconds,
               let idle,idle.isFinite,idle >= inputQuietSeconds { return }
            pause()
        }
    }
    /// Only the bridge-owned public probe field is read. No login field text,
    /// clipboard, framebuffer or password-dependent probe is involved.
    static func verifyKeyboard(value:() throws -> String?,send:(Data) throws -> Void,isCancelled:() -> Bool,
                               now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.025) }) throws {
        let deadline = now()+6
        func check() throws {
            guard !isCancelled(),now() < deadline else { throw MacTab5LocalError.invalid("键盘验证取消或超时，未向登录密码框输入；无需锁屏。") }
        }
        func expect(_ expected:String,wait:Bool = false) throws {
            let until = min(deadline,now()+1.25)
            repeat {
                try check()
                guard let current = try value() else { throw MacTab5LocalError.invalid("键盘测试框失去焦点，未向登录密码框输入；请保持此窗口在前台，暂勿锁屏。") }
                if current == expected { return }
                guard wait,expected.hasPrefix(current),now() < until else {
                    let detail = current.isEmpty ? "未收到第 \(expected.count) 个测试字符":current.caseInsensitiveCompare(expected) == .orderedSame ? "第 \(expected.count) 个测试字符大小写不匹配":"第 \(expected.count) 个测试字符不匹配（已收到 \(current.count) 个字符）"
                    throw MacTab5LocalError.invalid("VNC 键盘验证：\(detail)。未向登录密码框输入；无需锁屏。")
                }
                pause()
            } while true
        }
        var expected = ""
        try expect(expected)
        for byte in keyboardSample.utf8 {
            try expect(expected)
            try send(key(UInt32(byte),down:true)+key(UInt32(byte),down:false))
            expected.append(Character(UnicodeScalar(byte)))
            try expect(expected,wait:true)
        }
    }
    static func waitForLock(isCancelled:() -> Bool,progress:(String) -> Void,
                            now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.05) }) throws {
        let due = now()+15
        var previous = -1
        while now() < due {
            guard !isCancelled() else { throw MacTab5LocalError.invalid("解锁测试已取消，未向登录密码框输入。") }
            let remaining = Int(ceil(due-now()))
            if remaining != previous { progress("实际解锁测试：键盘验证通过。\(remaining) 秒后尝试解锁，请现在按 Control-Command-Q 锁屏，勿再输入或触碰 Touch ID。"); previous = remaining }
            pause()
        }
        guard !isCancelled() else { throw MacTab5LocalError.invalid("解锁测试已取消，未向登录密码框输入。") }
    }
    /// Protected macOS password fields may deliberately omit character counts.
    /// This path is only used after the same connection passes the desktop probe
    /// and public AX confirms the console and signed loginwindow's secure field.
    /// It never pretends an unavailable count is zero, probes text, or retries.
    static func sendFocused(_ password:Data,ready:() -> Bool,send:(Data) throws -> Void,isCancelled:() -> Bool,
                            now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.04) }) throws {
        try validate(password)
        let deadline = now()+12
        func check() throws {
            guard !isCancelled(),now() < deadline,ready() else { throw MacTab5LocalError.invalid("登录密码框焦点、会话或测试状态发生变化，已停止且不重试；请自行检查密码框。") }
        }
        func press(_ value:UInt32) throws {
            try check()
            var bytes = key(value,down:true)+key(value,down:false)
            defer { bytes.resetBytes(in:0..<bytes.count) }
            try send(bytes); pause(); try check()
        }
        for byte in password { try press(UInt32(byte)) }
        try check()
        // Enter may immediately dismiss loginwindow. No post-Enter focus check
        // may be interpreted as failure/retry; actual unlock is still unconfirmed.
        var enter = key(0xff0d,down:true)+key(0xff0d,down:false)
        defer { enter.resetBytes(in:0..<enter.count) }
        try send(enter)
    }
    /// Count returns nil on unknown or changed focus/session, never field text.
    /// No retries of key-down, Return, a partial password or an uncertain write.
    static func sendOnce(_ password:Data,count:() throws -> Int?,send:(Data) throws -> Void,isCancelled:() -> Bool,
                         now:() -> TimeInterval = { ProcessInfo.processInfo.systemUptime },pause:() -> Void = { Thread.sleep(forTimeInterval:0.025) }) throws {
        try validate(password)
        let deadline = now()+12
        func check() throws {
            guard !isCancelled(),now() < deadline else { throw MacTab5LocalError.invalid("解锁测试已取消或超时，未自动重试；请自行检查登录页。") }
        }
        func expect(_ expected:Int,wait:Bool = false) throws {
            let until = min(deadline,now()+1)
            repeat {
                try check()
                guard let observed = try count() else { throw MacTab5LocalError.invalid("系统未提供可确认的登录密码框，已停止且不重试；请自行检查登录页。") }
                if observed == expected { return }
                guard wait,now() < until else { throw MacTab5LocalError.invalid("登录密码框发生变化，已停止且不重试；请自行清空后再试。") }
                pause()
            } while true
        }
        func press(_ value:UInt32) throws {
            try check()
            var bytes = key(value,down:true)+key(value,down:false)
            defer { bytes.resetBytes(in:0..<bytes.count) }
            // One logical press contains both edges; production spaces them
            // by 40 ms. Uncertainty closes the connection without replay.
            try send(bytes)
        }
        try expect(0)
        // One harmless marker, then Backspace, proves this authenticated RFB
        // connection reaches the same signed local login field, not another
        // desktop/session. A failure can leave a marker; never submit it.
        try press(0x76); try expect(1,wait:true)
        try press(0xff08); try expect(0,wait:true)
        var expected = 0
        for byte in password {
            try expect(expected)
            try press(UInt32(byte)); expected += 1
            try expect(expected,wait:true)
        }
        try expect(expected)
        try press(0xff0d)
    }
}

private final class MacLoginPasswordTarget {
    private let context:MacLoginWindowContext
    private let element:AXUIElement, pid:pid_t
    let supportsCount:Bool
    fileprivate static func attribute(_ element:AXUIElement,_ key:String) -> CFTypeRef? {
        var value:CFTypeRef?
        guard AXUIElementCopyAttributeValue(element,key as CFString,&value) == .success else { return nil }
        return value
    }
    fileprivate static func ax(_ value:CFTypeRef?) -> AXUIElement? {
        guard let value,CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        return unsafeBitCast(value,to:AXUIElement.self)
    }
    fileprivate static func onConsole(_ uid:uid_t) -> Bool {
        guard let session = CGSessionCopyCurrentDictionary() as? [String:Any],
              let on = session[kCGSessionOnConsoleKey as String],CFGetTypeID(on as CFTypeRef) == CFBooleanGetTypeID(),on as? Bool == true,
              let user = session[kCGSessionUserIDKey as String] as? NSNumber,CFGetTypeID(user) != CFBooleanGetTypeID() else { return false }
        return user.uint32Value == uid
    }
    fileprivate static func signedLoginWindow(_ pid:pid_t) -> Bool {
        guard let app = NSRunningApplication(processIdentifier:pid),
              app.bundleURL?.path == "/System/Library/CoreServices/loginwindow.app" else { return false }
        var code:SecCode?, requirement:SecRequirement?
        return SecCodeCopyGuestWithAttributes(nil,[kSecGuestAttributePid:pid] as CFDictionary,[],&code) == errSecSuccess &&
            SecRequirementCreateWithString("anchor apple and identifier \"com.apple.loginwindow\"" as CFString,[],&requirement) == errSecSuccess &&
            code != nil && requirement != nil && SecCodeCheckValidity(code!,[],requirement) == errSecSuccess
    }
    private init(context:MacLoginWindowContext,field:AXUIElement,replacingExistingInput:Bool) throws {
        self.context = context; element = field; pid = context.pid
        _ = AXUIElementSetMessagingTimeout(element,0.3)
        var raw:CFTypeRef?
        let error = AXUIElementCopyAttributeValue(element,kAXNumberOfCharactersAttribute as CFString,&raw)
        supportsCount = try MacVNCUnlockTest.usesCounter(error:error,number:raw,replacingExistingInput:replacingExistingInput)
        guard isReady else { throw MacTab5LocalError.invalid("登录密码框焦点已变化，未向登录密码框输入。") }
    }
    static func prepare(send:(Data) throws -> Void,isCancelled:() -> Bool,progress:(String) -> Void,replacingExistingInput:Bool = false) throws -> MacLoginPasswordTarget {
        let context = try MacLoginWindowContext()
        let field:AXUIElement = try MacVNCUnlockTest.prepareField(find:{ try context.findField(isCancelled:isCancelled) },stillLoginWindow:{ context.isCurrent },send:send,isCancelled:isCancelled)
        guard context.isCurrent,!isCancelled() else { throw MacTab5LocalError.invalid("登录窗口发生变化，未向登录密码框输入。") }
        if !context.isFocused(field) {
            progress("正在将焦点移到登录密码框…")
            var settable = DarwinBoolean(false)
            let supported = AXUIElementIsAttributeSettable(field,kAXFocusedAttribute as CFString,&settable)
            guard supported == .success,settable.boolValue else { throw MacTab5LocalError.invalid("系统不允许聚焦登录密码框（AX \(supported.rawValue)），未向登录密码框输入。") }
            guard context.isCurrent,!isCancelled() else { throw MacTab5LocalError.invalid("登录窗口发生变化，未向登录密码框输入。") }
            let result = AXUIElementSetAttributeValue(field,kAXFocusedAttribute as CFString,kCFBooleanTrue)
            guard result == .success else { throw MacTab5LocalError.invalid("登录密码框聚焦失败（AX \(result.rawValue)），未向登录密码框输入。") }
            let deadline = ProcessInfo.processInfo.systemUptime+0.75
            while !context.isFocused(field),context.isCurrent,!isCancelled(),ProcessInfo.processInfo.systemUptime < deadline { Thread.sleep(forTimeInterval:0.05) }
        }
        guard context.isFocused(field),!isCancelled() else { throw MacTab5LocalError.invalid("未能确认登录密码框已获得焦点，未向登录密码框输入。") }
        return try .init(context:context,field:field,replacingExistingInput:replacingExistingInput)
    }
    struct DirectSubmission {
        let isCurrent:() -> Bool
        let preparation:() throws -> MacVNCUnlockTest.PreparationObservation
        let perform:() throws -> Void
    }
    private static func supports(_ element:AXUIElement,_ action:String) -> Bool {
        var names:CFArray?
        return AXUIElementCopyActionNames(element,&names) == .success && (names as? [String])?.contains(action) == true
    }
    private var canWriteDirect:Bool {
        var settable = DarwinBoolean(false)
        return isReady && AXUIElementIsAttributeSettable(element,kAXValueAttribute as CFString,&settable) == .success && settable.boolValue
    }
    /// Bind the verified secure field/window before writing. The login button
    /// may be absent while empty. Its preparation acknowledgement and final
    /// submission are re-resolved in this same window; no input replay/fallback.
    func directSubmission(isCancelled:@escaping () -> Bool) throws -> DirectSubmission {
        guard canWriteDirect else { throw MacTab5LocalError.invalid("系统未开放登录密码框的公开写入接口；本次未输入密码，未连接屏幕共享。") }
        guard let window = try submissionWindow() else {
            throw MacTab5LocalError.invalid("未能确认密码框所属登录窗口，未输入密码。")
        }
        let current:() -> Bool = {
            guard !isCancelled(),self.isReady,
                  let actualWindow = try? self.submissionWindow(),CFEqual(actualWindow,window) else { return false }
            return !isCancelled()
        }
        return DirectSubmission(isCurrent:current,preparation:{
            guard current() else { throw MacTab5Error.unconfirmed }
            let characters = self.supportsCount ? try self.count():nil
            guard !self.supportsCount || characters != nil else { throw MacTab5Error.unconfirmed }
            var enabled = false
            if let button = try self.loginButton(in:window) {
                var owner:pid_t = 0
                guard AXUIElementGetPid(button,&owner) == .success,owner == self.pid,
                      Self.attribute(button,kAXRoleAttribute) as? String == kAXButtonRole,
                      Self.supports(button,kAXPressAction),
                      let value = Self.attribute(button,kAXEnabledAttribute),CFGetTypeID(value) == CFBooleanGetTypeID(),
                      let flag = value as? Bool else { throw MacTab5Error.unconfirmed }
                enabled = flag
            }
            guard current() else { throw MacTab5Error.unconfirmed }
            return .init(characters:characters,loginEnabled:enabled)
        },perform:{
            let deadline = ProcessInfo.processInfo.systemUptime+3
            guard current() else { throw MacTab5Error.unconfirmed }
            guard let button = try self.loginButton(in:window) else {
                throw MacTab5LocalError.invalid("登录窗口未提供默认或明确标注的登录按钮，未提交且不重试；请自行查看 Mac。")
            }
            _ = AXUIElementSetMessagingTimeout(button,0.3)
            var owner:pid_t = 0
            guard current(),AXUIElementGetPid(button,&owner) == .success,owner == self.pid,
                  Self.attribute(button,kAXRoleAttribute) as? String == kAXButtonRole,
                  Self.supports(button,kAXPressAction),
                  let actual = try? self.loginButton(in:window),CFEqual(actual,button),
                  Self.attribute(button,kAXEnabledAttribute) as? Bool == true,
                  current(),ProcessInfo.processInfo.systemUptime < deadline else {
                throw MacTab5LocalError.invalid("填入后登录按钮不可用或目标已变化，未按下按钮且不重试；请自行查看 Mac。")
            }
            let result = AXUIElementPerformAction(button,kAXPressAction as CFString)
            guard result == .success else { throw MacTab5LocalError.invalid("登录按钮按下未获确认（AX \(result.rawValue)），已停止且不重试；请自行查看 Mac。") }
        })
    }
    private func loginButton(in window:AXUIElement) throws -> AXUIElement? {
        if let button = Self.ax(Self.attribute(window,kAXDefaultButtonAttribute)) { return button }
        // The actual lock screen omits AXDefaultButton (r63). An explicitly
        // labelled descendant may still expose AXPress. Inspect buttons only;
        // never read secure values or text-field titles; other captions are discarded.
        let deadline = ProcessInfo.processInfo.systemUptime+1
        var queue:[(AXUIElement,Int)] = [(window,0)], cursor = 0
        var matches:[AXUIElement] = []
        while cursor < queue.count {
            guard isReady,ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.unconfirmed }
            let (node,depth) = queue[cursor]; cursor += 1
            _ = AXUIElementSetMessagingTimeout(node,0.1)
            var owner:pid_t = 0
            guard AXUIElementGetPid(node,&owner) == .success,owner == pid else { throw MacTab5Error.unconfirmed }
            let role = Self.attribute(node,kAXRoleAttribute) as? String
            if role == kAXButtonRole {
                if Self.supports(node,kAXPressAction) {
                    // Captions are compared transiently with a fixed vocabulary;
                    // raw labels/identifiers never enter diagnostics or logs.
                    if MacVNCUnlockTest.explicitLoginLabel(title:Self.attribute(node,kAXTitleAttribute) as? String,
                                                          description:Self.attribute(node,kAXDescriptionAttribute) as? String) {
                        if !matches.contains(where:{CFEqual($0,node)}) { matches.append(node) }
                        guard matches.count <= 1 else { throw MacTab5LocalError.invalid("登录窗口有多个登录标注按钮，未提交且不重试；请自行查看 Mac。") }
                    }
                }
            }
            if role == kAXTextFieldRole { continue }
            var raw:CFTypeRef?
            let result = AXUIElementCopyAttributeValue(node,kAXChildrenAttribute as CFString,&raw)
            if result == .attributeUnsupported || result == .noValue { continue }
            guard result == .success,let children = raw as? [AXUIElement],
                  (depth < 8 || children.isEmpty),queue.count+children.count <= 64 else {
                throw MacTab5LocalError.invalid("登录控件结构无法完整确认，未提交且不重试；请自行查看 Mac。")
            }
            queue.append(contentsOf:children.map { ($0,depth+1) })
        }
        guard isReady,ProcessInfo.processInfo.systemUptime < deadline else { throw MacTab5Error.unconfirmed }
        return matches.first
    }
    private func submissionWindow() throws -> AXUIElement? {
        guard isReady else { throw MacTab5Error.unconfirmed }
        if let window = Self.ax(Self.attribute(element,kAXWindowAttribute)) {
            var owner:pid_t = 0
            guard AXUIElementGetPid(window,&owner) == .success,owner == pid,
                  Self.attribute(window,kAXRoleAttribute) as? String == kAXWindowRole else { throw MacTab5Error.unconfirmed }
            _ = AXUIElementSetMessagingTimeout(window,0.1)
            return window
        }
        // Some AX fields omit AXWindow. Only their own bounded parent chain
        // can establish membership; a guessed foreground window is insufficient.
        let deadline = ProcessInfo.processInfo.systemUptime+1
        var node = element
        for _ in 0..<8 {
            guard isReady,ProcessInfo.processInfo.systemUptime < deadline,
                  let parent = Self.ax(Self.attribute(node,kAXParentAttribute)),!CFEqual(parent,node) else { return nil }
            _ = AXUIElementSetMessagingTimeout(parent,0.1)
            var owner:pid_t = 0
            guard AXUIElementGetPid(parent,&owner) == .success,owner == pid else { return nil }
            if Self.attribute(parent,kAXRoleAttribute) as? String == kAXWindowRole { return parent }
            node = parent
        }
        return nil
    }
    func writeDirect(_ password:Data) throws {
        guard canWriteDirect,let value = String(data:password,encoding:.utf8) else { throw MacTab5Error.unconfirmed }
        let result = AXUIElementSetAttributeValue(element,kAXValueAttribute as CFString,value as CFString)
        guard result == .success else { throw MacTab5LocalError.invalid("登录框写入未获确认（AX \(result.rawValue)），已停止且不重试；请自行查看 Mac。") }
    }
    func count() throws -> Int? {
        guard context.isCurrent,context.isFocused(element),context.pid == pid,
              let number = Self.attribute(element,kAXNumberOfCharactersAttribute),CFGetTypeID(number) == CFNumberGetTypeID() else { return nil }
        return (number as? NSNumber)?.intValue
    }
    var isReady:Bool {
        context.isCurrent && context.isFocused(element) &&
            Self.attribute(element,kAXRoleAttribute) as? String == kAXTextFieldRole &&
            Self.attribute(element,kAXSubroleAttribute) as? String == kAXSecureTextFieldSubrole
    }
}

/// Activity flags alone do not identify the recipient of an input event.
/// An inactive AppKit flag needs independent system proof of this exact probe.
struct MacVNCProbeFocusState {
    let active:Bool, visible:Bool, key:Bool, editor:Bool
    let front:Bool?, systemProbe:Bool?
    var permits:Bool { visible && key && editor && front == true && systemProbe != false && (active || systemProbe == true) }
    var summary:String {
        func flag(_ value:Bool?) -> String { value.map { $0 ? "1":"0" } ?? "?" }
        return "active=\(flag(active)), visible=\(flag(visible)), key=\(flag(key)), editor=\(flag(editor)), front=\(flag(front)), systemProbe=\(flag(systemProbe))"
    }
}

struct MacVNCInputSourceSnapshot: Equatable {
    let source:String
    let revision:UInt64
}

/// All view access is marshalled to the main thread, and is restricted to a field
/// owned by the initiating window. The public sample is cleared before locking.
final class MacVNCKeyboardProbe {
    // Own the editor rather than borrowing the window's shared password-field
    // editor. Roman-only input still uses AppKit text insertion and mapping.
    let field = NSTextView()
    private weak var window:NSWindow?
    private var monitor:Any?, sourceObserver:NSObjectProtocol?, down = 0, up = 0
    private var inputRevision:UInt64 = 0
    private var readinessNote = "输入准备尚未完成。"
    private var modifiers:NSEvent.ModifierFlags = []
    private var probeID = "", focusSummary = ""
    init() {
        field.isFieldEditor = false; field.isRichText = false; field.importsGraphics = false; field.allowsUndo = false
        field.allowedInputSourceLocales = [NSAllRomanInputSourcesLocaleIdentifier]
        field.isAutomaticQuoteSubstitutionEnabled = false; field.isAutomaticDashSubstitutionEnabled = false
        field.isAutomaticTextReplacementEnabled = false; field.isAutomaticSpellingCorrectionEnabled = false
        field.isContinuousSpellCheckingEnabled = false; field.isEditable = false; field.isSelectable = false
        field.font = .systemFont(ofSize:13); field.textContainerInset = NSSize(width:5,height:4)
        field.drawsBackground = true; field.backgroundColor = .textBackgroundColor
        field.wantsLayer = true; field.layer?.cornerRadius = 5
        field.layer?.borderWidth = 0.5; field.layer?.borderColor = NSColor.separatorColor.cgColor
        field.heightAnchor.constraint(equalToConstant:28).isActive = true
        field.setAccessibilityLabel("键盘测试框（仅使用公开测试字符）")
    }
    private func main<T>(_ body:() throws -> T) rethrows -> T {
        if Thread.isMainThread { return try body() }
        return try DispatchQueue.main.sync(execute:body)
    }
    func begin() throws {
        try main {
            guard NSApp.isActive,let window = field.window,window.isKeyWindow,window.isVisible else { throw MacTab5LocalError.invalid("请保持解锁设置窗口在前台再开始；未向登录密码框输入。") }
            self.window = window; field.string = ""; field.isSelectable = true; field.isEditable = true
            probeID = "aibot-public-probe-"+UUID().uuidString
            field.setAccessibilityIdentifier(probeID); focusSummary = ""; inputRevision = 0
            readinessNote = "输入准备尚未完成。"
            sourceObserver = NotificationCenter.default.addObserver(forName:NSTextInputContext.keyboardSelectionDidChangeNotification,object:nil,queue:.main) { [weak self] _ in
                guard let self,!self.probeID.isEmpty else { return }
                self.inputRevision &+= 1
            }
            window.contentView?.layoutSubtreeIfNeeded()
            field.scrollToVisible(field.bounds)
            guard window.makeFirstResponder(field) else { field.isEditable = false; throw MacTab5LocalError.invalid("无法聚焦本程序的键盘测试框；未向登录密码框输入。") }
            down = 0; up = 0; modifiers = []
            monitor = NSEvent.addLocalMonitorForEvents(matching:[.keyDown,.keyUp,.flagsChanged]) { [weak self] event in
                guard let self,let window = self.window,event.window === window,window.firstResponder === self.field else { return event }
                // Counts and modifier booleans only in this public-sample field.
                // Never read key codes, characters or either credential field.
                if event.type == .keyDown { self.down += 1 }
                else if event.type == .keyUp { self.up += 1 }
                self.inputRevision &+= 1
                self.modifiers.formUnion(event.modifierFlags.intersection([.command,.control,.option,.shift]))
                return event
            }
            guard monitor != nil else { throw MacTab5LocalError.invalid("无法建立本窗口的按键计数，未向登录密码框输入。") }
        }
    }
    func requireStart() throws {
        try main {
            guard NSApp.isActive,let window = field.window,window.isKeyWindow,window.isVisible else { throw MacTab5LocalError.invalid("请保持此设置窗口在前台再验证；未开始 Mac 账户认证。") }
        }
    }
    func waitForInputSource(isCancelled:() -> Bool) throws {
        try MacVNCUnlockTest.waitForInputSource(snapshot:{
            guard try self.read().isEmpty else { throw MacTab5LocalError.invalid("输入准备期间测试框已收到其他输入，已停止；未发送测试按键或登录输入。") }
            return self.main {
                guard let source = self.field.inputContext?.selectedKeyboardInputSource,!source.isEmpty else { return nil }
                return MacVNCInputSourceSnapshot(source:source,revision:self.inputRevision)
            }
        },localIdleSeconds:{
            // Public time since any keyboard/mouse/tablet event in this login
            // session. No event tap, event contents, other-app text or storage.
            CGEventSource.secondsSinceLastEventType(.combinedSessionState,eventType:CGEventType(rawValue:UInt32.max)!)
        },isCancelled:isCancelled)
        main { readinessNote = "输入准备：本机键鼠／触控板已静止至少 3 秒，测试框焦点与输入源也已稳定；这不代表 VNC 按键已送达。" }
    }
    func read() throws -> String {
        let initial = main { (state(systemProbe:nil),probeID) }
        // Never message our own AX server on its main thread. An untrusted or
        // unsupported interface is unknown, not permission or proof of focus.
        let proof = !Thread.isMainThread ? systemFocus(initial.1):nil
        return try main {
            let current = state(systemProbe:initial.1 == probeID ? proof:nil)
            focusSummary = current.summary
            guard current.permits else {
                let front = NSWorkspace.shared.frontmostApplication?.bundleIdentifier ?? "unknown"
                throw MacTab5LocalError.invalid("系统未确认当前键盘目标仍是本测试框，验证已停止；未向登录密码框输入，暂勿锁屏。\n焦点状态："+current.summary+"\n停止时前台程序标识："+String(front.prefix(160)))
            }
            return field.string
        }
    }
    private func state(systemProbe:Bool?) -> MacVNCProbeFocusState {
        .init(active:NSApp.isActive,visible:window?.isVisible == true,key:window?.isKeyWindow == true,
              editor:field.isEditable && window?.firstResponder === field,
              front:NSWorkspace.shared.frontmostApplication.map { $0.processIdentifier == getpid() },systemProbe:systemProbe)
    }
    /// Read only system focus identity, then the ID/role of our own app's
    /// focused element. Never inspect another app's field, text or AXValue.
    private func systemFocus(_ id:String) -> Bool? {
        guard !id.isEmpty,AXIsProcessTrusted() else { return nil }
        let system = AXUIElementCreateSystemWide()
        _ = AXUIElementSetMessagingTimeout(system,0.15)
        func attribute(_ element:AXUIElement,_ name:String) -> CFTypeRef? {
            var result:CFTypeRef?
            return AXUIElementCopyAttributeValue(element,name as CFString,&result) == .success ? result:nil
        }
        func element(_ raw:CFTypeRef?) -> AXUIElement? {
            guard let raw,CFGetTypeID(raw) == AXUIElementGetTypeID() else { return nil }
            return unsafeBitCast(raw,to:AXUIElement.self)
        }
        guard let app = element(attribute(system,kAXFocusedApplicationAttribute)) else { return nil }
        var pid:pid_t = 0
        guard AXUIElementGetPid(app,&pid) == .success else { return nil }
        guard pid == getpid() else { return false }
        _ = AXUIElementSetMessagingTimeout(app,0.15)
        guard let focused = element(attribute(app,kAXFocusedUIElementAttribute)),AXUIElementGetPid(focused,&pid) == .success else { return nil }
        guard pid == getpid() else { return false }
        _ = AXUIElementSetMessagingTimeout(focused,0.15)
        guard let identifier = attribute(focused,kAXIdentifierAttribute) as? String,
              let role = attribute(focused,kAXRoleAttribute) as? String,
              let value = attribute(focused,kAXFocusedAttribute),CFGetTypeID(value) == CFBooleanGetTypeID() else { return nil }
        return identifier == id && role == kAXTextAreaRole && value as? Bool == true
    }
    var diagnostic:String { main { focusSummary } }
    var preparationNote:String { main { readinessNote } }
    var modifierNote:String { main { "公开测试事件修饰键：command=\(modifiers.contains(.command) ? 1:0), control=\(modifiers.contains(.control) ? 1:0), option=\(modifiers.contains(.option) ? 1:0), shift=\(modifiers.contains(.shift) ? 1:0)。" } }
    var keyCounts:(down:Int,up:Int) { main { (down,up) } }
    func end() {
        main {
            if let monitor { NSEvent.removeMonitor(monitor); self.monitor = nil }
            if let sourceObserver { NotificationCenter.default.removeObserver(sourceObserver); self.sourceObserver = nil }
            if let window,window.firstResponder === field { _ = window.makeFirstResponder(nil) }
            field.string = ""; field.isEditable = false; field.isSelectable = false; window = nil
            probeID = ""; field.setAccessibilityIdentifier(nil)
        }
    }
}

/// Read-only process identity, role and child traversal; never reads AXValue,
/// text, account names or password contents. Focus writes are separate above.
private final class MacLoginWindowContext {
    private let system = AXUIElementCreateSystemWide()
    let app:AXUIElement, pid:pid_t
    private let uid = getuid()
    private typealias Target = MacLoginPasswordTarget
    init() throws {
        _ = AXUIElementSetMessagingTimeout(system,0.3)
        guard Target.onConsole(getuid()) else { throw MacTab5LocalError.invalid("无法确认当前账户仍在本机控制台，未向登录密码框输入。") }
        var raw:CFTypeRef?
        let error = AXUIElementCopyAttributeValue(system,kAXFocusedApplicationAttribute as CFString,&raw)
        guard error == .success,let value = Target.ax(raw) else { throw MacTab5LocalError.invalid("无法读取锁屏窗口焦点（AX \(error.rawValue)），未向登录密码框输入。") }
        var owner:pid_t = 0
        guard AXUIElementGetPid(value,&owner) == .success,Target.signedLoginWindow(owner) else {
            throw MacTab5LocalError.invalid("系统未提供可确认的 Apple 登录窗口焦点，未向登录密码框输入。")
        }
        app = value; pid = owner; _ = AXUIElementSetMessagingTimeout(app,0.3)
    }
    var isCurrent:Bool {
        guard Target.onConsole(uid),MacScreenProtection.read().active == false,
              let front = Target.ax(Target.attribute(system,kAXFocusedApplicationAttribute)) else { return false }
        var current:pid_t = 0
        return AXUIElementGetPid(front,&current) == .success && current == pid
    }
    func isFocused(_ field:AXUIElement) -> Bool {
        guard isCurrent,let current = focusedField else { return false }
        return CFEqual(current,field)
    }
    var focusedField:AXUIElement? { Target.ax(Target.attribute(app,kAXFocusedUIElementAttribute)) }
    private func secure(_ field:AXUIElement) -> Bool {
        var owner:pid_t = 0
        return AXUIElementGetPid(field,&owner) == .success && owner == pid &&
            Target.attribute(field,kAXRoleAttribute) as? String == kAXTextFieldRole &&
            Target.attribute(field,kAXSubroleAttribute) as? String == kAXSecureTextFieldSubrole
    }
    func findField(isCancelled:() -> Bool) throws -> AXUIElement? {
        guard isCurrent,!isCancelled() else { return nil }
        if let field = Target.ax(Target.attribute(app,kAXFocusedUIElementAttribute)),secure(field) { return field }
        let root = Target.ax(Target.attribute(app,kAXFocusedWindowAttribute)) ?? app
        let deadline = ProcessInfo.processInfo.systemUptime+0.75
        var queue:[(AXUIElement,Int)] = [(root,0)], cursor = 0, found:[AXUIElement] = []
        while cursor < queue.count,cursor < 64 {
            guard isCurrent,!isCancelled(),ProcessInfo.processInfo.systemUptime < deadline else { return nil }
            let (element,depth) = queue[cursor]; cursor += 1
            _ = AXUIElementSetMessagingTimeout(element,0.1)
            if secure(element) { found.append(element); if found.count > 1 { throw MacTab5LocalError.invalid("登录窗口存在多个密码框，无法确定目标；未向登录密码框输入。") }; continue }
            if let children = Target.attribute(element,kAXChildrenAttribute) as? [AXUIElement] {
                guard depth < 8 || children.isEmpty else { throw MacTab5LocalError.invalid("登录界面结构超出确认范围，未向登录密码框输入。") }
                let room = max(0,64-queue.count)
                guard children.count <= room else { throw MacTab5LocalError.invalid("登录界面结构超出确认范围，未向登录密码框输入。") }
                queue.append(contentsOf:children.map { ($0,depth+1) })
            }
        }
        return found.first
    }
}
