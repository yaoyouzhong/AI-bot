import AppKit
import ApplicationServices
import Carbon
import Security

enum MacSpeechProvider {
    static let key = "tab5_voice_provider"
    static var doubao: Bool { UserDefaults.standard.string(forKey:key) == "doubao" }
    static var name: String { doubao ? "豆包输入法":"Mac 本地识别" }
}

private final class MacDoubaoEditor:NSTextView {
    var committed:(() -> Void)?
    override func insertText(_ insertString:Any,replacementRange:NSRange) {
        super.insertText(insertString,replacementRange:replacementRange)
        committed?()
    }
    override func unmarkText() { super.unmarkText(); committed?() }
}

/// Public input-source/AX/CoreAudio interoperability with installed Doubao.
/// Audio routing and actual input IO are confirmed before device audio is ready;
/// committed text is read only from this application's dedicated editor.
final class MacDoubaoSpeech:NSObject,NSTextViewDelegate,NSWindowDelegate {
    private static let bundleID = "com.bytedance.inputmethod.doubaoime", sourceID = bundleID+".pinyin"
    private let publication = NSLock(), microphone = MacDoubaoMicrophone()
    private var publishedReady = false, publishedAudio:MacDoubaoAudio?
    private var observations: [String] = []
    private var observationStarted:TimeInterval = 0
    private var lastCaptureObservation: String?
    private var window:NSWindow?, editor:MacDoubaoEditor?, status:NSTextField?
    private var previousSource:TISInputSource?, previousPolicy:NSApplication.ActivationPolicy?
    private var callback:((String,Bool,MacTab5SpeechFailure?) -> Void)?
    private var token = UUID(), held = false, stopping = false, startedCapture = false
    private var watchdog:Timer?, capture:MacDoubaoCapture?, audio:MacDoubaoAudio?
    private var completion = MacDoubaoCompletion(), started:TimeInterval = 0, stopped:TimeInterval = 0
    private var drain = MacDoubaoDrain()
    private let callbacks = DispatchQueue(label:"AI-bot.doubao-results")

    static func installed() -> Bool {
        var code:SecStaticCode?, requirement:SecRequirement?
        let rule = "anchor apple generic and identifier \"\(bundleID)\" and certificate leaf[subject.OU] = \"96L78H6LMH\""
        return SecStaticCodeCreateWithPath(URL(fileURLWithPath:"/Library/Input Methods/DoubaoIme.app") as CFURL,[],&code) == errSecSuccess &&
            SecRequirementCreateWithString(rule as CFString,[],&requirement) == errSecSuccess && code != nil && requirement != nil && SecStaticCodeCheckValidity(code!,[],requirement) == errSecSuccess
    }
    private static func id(_ source:TISInputSource) -> String? {
        guard let raw = TISGetInputSourceProperty(source,kTISPropertyInputSourceID) else { return nil }
        return Unmanaged<CFString>.fromOpaque(raw).takeUnretainedValue() as String
    }
    private static func source() -> TISInputSource? {
        let values = TISCreateInputSourceList([kTISPropertyInputSourceID:sourceID] as CFDictionary,false).takeRetainedValue()
        guard CFArrayGetCount(values) > 0,let raw = CFArrayGetValueAtIndex(values,0) else { return nil }
        return Unmanaged<TISInputSource>.fromOpaque(raw).takeUnretainedValue()
    }
    private static func current() -> TISInputSource? { TISCopyCurrentKeyboardInputSource()?.takeRetainedValue() }
    static func readiness() -> String {
        guard installed(),source() != nil else { return "请先安装官方豆包输入法并在系统键盘设置中启用。" }
        guard AXIsProcessTrusted(),CGPreflightPostEventAccess() else { return "豆包已启用；调用及麦克风选择需要辅助功能权限。" }
        let route = (try? MacDoubaoAudio.device()) == nil ? "TAB5 音频：待安装 VB-CABLE Mac":"TAB5 音频：VB-CABLE 设备可用"
        return "豆包已安装并启用。请保持长按模式快捷键为 Fn。\(route)。实际录音状态与最终文字仍需测试。"
    }
    private func onMain(_ work:@escaping () -> Void) { if Thread.isMainThread { work() } else { DispatchQueue.main.async(execute:work) } }
    private func publish(ready:Bool,audio:MacDoubaoAudio? = nil) { publication.lock(); publishedReady = ready; publishedAudio = audio; publication.unlock() }
    private func ready() -> Bool { publication.lock(); defer { publication.unlock() }; return publishedReady }
    private func observe(_ event: String) {
        publication.lock(); defer { publication.unlock() }
        let elapsed = Int(max(0, ProcessInfo.processInfo.systemUptime - observationStarted) * 1000)
        observations.append("\(elapsed) ms \(event)"); observations = Array(observations.suffix(10))
    }
    private func diagnostics() -> String {
        publication.lock(); defer { publication.unlock() }
        return (publishedReady ? "豆包实际输入 IO 已确认" : "豆包等待实际收音确认") + (observations.isEmpty ? "" : "；" + observations.joined(separator: " → "))
    }
    func backend() -> MacTab5SpeechBackend {
        .init(start:{ [self] source,callback in
            guard ["pc","tab5"].contains(source),Self.installed() else { throw MacTab5LocalError.invalid("请先安装并启用官方豆包输入法。") }
            guard AXIsProcessTrusted(),CGPreflightPostEventAccess() else { throw MacTab5LocalError.invalid("豆包调用需要辅助功能权限。") }
            let input = try source == "tab5" ? MacDoubaoAudio.device():MacDoubaoMicrophone.physical()
            // USB may start again before the previous main-queue cancellation
            // runs. Never publish that older session's readiness to this one.
            publish(ready:false)
            onMain { [self] in begin(input:input,virtual:source == "tab5",callback:callback) }
        },feed:{ [self] pcm in
            publication.lock(); let route = publishedReady ? publishedAudio:nil; publication.unlock()
            guard let route else { throw MacTab5LocalError.invalid("豆包尚未确认从 VB-CABLE 收音，未发送音频。") }; try route.feed(pcm)
        },finish:{ [self] in onMain { [self] in finish() } },cancel:{ [self] in onMain { [self] in cancel() } },
        diagnostics:{ [self] in diagnostics() },ready:{ [self] in ready() })
    }
    private func emit(_ text:String,final:Bool = false,failure:MacTab5SpeechFailure? = nil) {
        let callback = callback; callbacks.async { callback?(text,final,failure) }
    }
    private var safeFocus:Bool {
        guard let window,let editor else { return false }
        return NSApp.isActive && window.isKeyWindow && window.firstResponder === editor &&
            MacScreenLockObservation.shared.displayedValue != true && !IsSecureEventInputEnabled() && Self.current().flatMap(Self.id) == Self.sourceID
    }
    private func fn(_ down:Bool) -> Bool {
        guard let event = CGEvent(keyboardEventSource:nil,virtualKey:63,keyDown:down) else { return false }
        let flags = CGEventSource.flagsState(.combinedSessionState)
        event.type = .flagsChanged; event.flags = down ? flags.union(.maskSecondaryFn):flags.subtracting(.maskSecondaryFn)
        event.post(tap:.cghidEventTap); return true
    }
    private func release() { if held { held = false; _ = fn(false) } }
    private func begin(input:MacAudioInput,virtual:Bool,callback:@escaping (String,Bool,MacTab5SpeechFailure?) -> Void) {
        cancel(); self.callback = callback; completion = .init(); drain = .init()
        publication.lock(); observations = []; observationStarted = ProcessInfo.processInfo.systemUptime; publication.unlock(); lastCaptureObservation = nil
        observe("准备麦克风")
        // Device starts arrive on USB/LAN/BLE queues. HIToolbox input-source
        // enumeration must stay on the main queue along with the editor.
        guard Self.source() != nil else { abort("请先在系统键盘设置中启用官方豆包输入法。"); return }
        let owner = token
        microphone.acquire(input,cancelled:{ [weak self] in self?.isCurrent(owner) != true }) { [weak self] result in
            guard let self,self.token == owner else { return }
            do { try result.get(); try self.open(input:input,virtual:virtual) }
            catch { self.abort((error as? MacTab5LocalError)?.localizedDescription ?? "豆包麦克风准备未完成，未开始收音。") }
        }
    }
    private func isCurrent(_ owner:UUID) -> Bool {
        // A serial queue may query cancellation without accessing AppKit state.
        publication.lock(); defer { publication.unlock() }; return token == owner
    }
    private func open(input:MacAudioInput,virtual:Bool) throws {
        guard let source = Self.source() else { throw MacTab5Error.unavailable }
        let previous = NSApp.activationPolicy()
        guard previous == .regular || NSApp.setActivationPolicy(.regular) else { throw MacTab5Error.unavailable }
        if previous != .regular { previousPolicy = previous }
        let window = MacBridgeSettingsStyle.window("豆包语音",width:620,height:460)
        self.window = window; window.delegate = self
        let page = MacBridgeSettingsPage(); page.heading("豆包语音",subtitle:virtual ? "TAB5 麦克风 → VB-CABLE → 豆包":"所选 Mac／DJI 麦克风 → 豆包")
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.borderType = .bezelBorder
        let editor = MacDoubaoEditor(); editor.isRichText = false; editor.font = .systemFont(ofSize:16); editor.delegate = self
        editor.isVerticallyResizable = true; editor.autoresizingMask = [.width]; editor.textContainer?.widthTracksTextView = true
        editor.committed = { [weak self] in self?.changed(committed:true) }
        scroll.documentView = editor; scroll.heightAnchor.constraint(equalToConstant:180).isActive = true; page.add(scroll); self.editor = editor
        page.add(MacBridgeSettingsStyle.note("停止后等待豆包提交完整文字，自动返回 TAB5 预览；不会自动发送任务。请保持此窗口在前台。"))
        let status = MacBridgeSettingsStyle.note("正在准备豆包收音…"); self.status = status
        window.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[MacSettingsButton("停止收音") { [weak self] in self?.finish() },MacSettingsButton("取消") { [weak self] in self?.abort("已取消豆包语音。") }])
        previousSource = Self.current()
        guard TISSelectInputSource(source) == noErr else { throw MacTab5Error.unavailable }
        window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps:true)
        guard window.makeFirstResponder(editor) else { throw MacTab5Error.unavailable }
        let owner = token
        DispatchQueue.main.asyncAfter(deadline:.now()+0.4) { [weak self] in
            guard let self,self.token == owner else { return }
            do {
                self.capture = try MacDoubaoCapture(device:input.id)
                self.trigger(owner,input:input,virtual:virtual,idleSince:nil,deadline:ProcessInfo.processInfo.systemUptime+3)
            } catch { self.abort((error as? MacTab5LocalError)?.localizedDescription ?? "豆包收音未能启动。") }
        }
    }
    private func trigger(_ owner:UUID,input:MacAudioInput,virtual:Bool,idleSince:TimeInterval?,deadline:TimeInterval) {
        guard token == owner else { return }
        let now = ProcessInfo.processInfo.systemUptime
        guard !stopping,safeFocus,now < deadline else { abort("豆包录音状态或草稿焦点未就绪，未开始新的收音。"); return }
        let idle = capture?.running() == false
        if idle,let idleSince,now-idleSince >= 0.3 {
            do {
                if virtual { let audio = MacDoubaoAudio(); try audio.start(device:input); self.audio = audio }
                guard fn(true) else { throw MacTab5Error.unavailable }
                held = true; started = now
                observe("Fn 已按下")
                watchdog = Timer.scheduledTimer(withTimeInterval:0.1,repeats:true) { [weak self] _ in self?.tick() }
            } catch { abort((error as? MacTab5LocalError)?.localizedDescription ?? "豆包音频通道未能启动。") }
            return
        }
        DispatchQueue.main.asyncAfter(deadline:.now()+0.1) { [weak self] in
            self?.trigger(owner,input:input,virtual:virtual,idleSince:idle ? idleSince ?? now:nil,deadline:deadline)
        }
    }
    func textDidChange(_ notification:Notification) { guard notification.object as AnyObject? === editor else { return }; changed(committed:false) }
    private func changed(committed:Bool) {
        guard let editor else { return }
        guard editor.string.utf8.count <= 2000 else { abort("豆包文字超过长度限制，请分段录入。"); return }
        completion.update(editor.string,committed:committed,now:ProcessInfo.processInfo.systemUptime); emit(editor.string)
        if committed,!editor.string.isEmpty { observe("输入法提交文字") }
    }
    private func tick() {
        guard safeFocus else { abort("输入焦点已离开，已停止本次豆包语音。"); return }
        let now = ProcessInfo.processInfo.systemUptime, running = capture?.running()
        let observed = running.map { $0 ? "输入 IO 开启" : "输入 IO 停止" } ?? "输入 IO 未确认"
        if observed != lastCaptureObservation { lastCaptureObservation = observed; observe(observed) }
        if running == nil { publish(ready:false) }
        if !stopping {
            if running == true { startedCapture = true; publish(ready:true,audio:audio); status?.stringValue = "已确认豆包从所选麦克风收音，请开始说话。" }
            if !startedCapture,now-started > 4 { abort("豆包未确认实际收音，已释放 Fn；请检查长按 Fn 快捷键及麦克风权限。"); return }
            if startedCapture,running == false { observe("输入 IO 提前结束"); finish() }
            if now-started >= 60 { observe("一分钟上限"); finish() }
        }
        if stopping {
            if held,drain.ready(drained:audio?.drained ?? true,tail:audio?.tailSeconds ?? 0,now:now) { release(); observe("Fn 已释放"); completion.stopped = true; stopped = now }
            if let value = completion.final(capture:running,marked:editor?.hasMarkedText() ?? true,now:now) {
                observe("最终文字已确认"); emit(value,final:true); callback = nil; cancel(); return
            }
            if !held,running == false,completion.text.isEmpty,now-stopped >= 5 { emit("",failure:.noSpeech); callback = nil; cancel(); return }
            if now-stopped > 15 { abort("豆包未确认最终文字，本次不插入；请检查输入法状态。"); return }
        }
    }
    private func finish() {
        guard callback != nil,!stopping else { return }
        if window == nil { emit("",failure:.noSpeech); callback = nil; cancel(); return }
        stopping = true; stopped = ProcessInfo.processInfo.systemUptime; publish(ready:false)
        observe("请求结束收音（待播 \(Int((audio?.pendingSeconds ?? 0) * 1000)) ms）")
        status?.stringValue = "正在排空音频并等待豆包提交最终文字…"
    }
    private func abort(_ message:String) { emit(editor?.string ?? "",failure:.external(message)); callback = nil; cancel() }
    private func cancel() {
        publication.lock(); token = UUID(); publishedReady = false; publishedAudio = nil; publication.unlock()
        release(); watchdog?.invalidate(); watchdog = nil; audio?.stop(); audio = nil; capture = nil; microphone.release()
        if let previousSource,Self.current().flatMap(Self.id) == Self.sourceID { _ = TISSelectInputSource(previousSource) }
        previousSource = nil; callback = nil; stopping = false; startedCapture = false
        editor?.committed = nil; editor?.string = ""; window?.orderOut(nil); window?.delegate = nil; window = nil; editor = nil; status = nil
        if let previousPolicy { _ = NSApp.setActivationPolicy(previousPolicy); self.previousPolicy = nil }
    }
    func windowShouldClose(_ sender:NSWindow) -> Bool { abort("已关闭豆包语音。"); return true }
}

final class MacSpeechRouter {
    private let local = MacTab5Speech().backend(), doubao = MacDoubaoSpeech().backend()
    private var active:MacTab5SpeechBackend?
    private var last:MacTab5SpeechBackend?
    func backend() -> MacTab5SpeechBackend {
        .init(start:{ [self] source,callback in active?.cancel(); let chosen = MacSpeechProvider.doubao ? doubao:local; active = chosen; last = chosen; try chosen.start(source,callback) },
              feed:{ [self] in guard let active else { throw MacTab5Error.unavailable }; try active.feed($0) },finish:{ [self] in active?.finish() },
              cancel:{ [self] in active?.cancel(); active = nil },diagnostics:{ [self] in (active ?? last)?.diagnostics() ?? "" },ready:{ [self] in active?.ready() ?? false })
    }
}
