import AppKit
import ApplicationServices
import CoreAudio
import Security

/// Public AX interoperability with the installed, signed settings app. Only
/// microphone buttons are inspected. No vendor private API/preferences are
/// patched. A small non-secret journal permits restoration after a crash.
final class MacDoubaoMicrophone {
    private static let queue = DispatchQueue(label:"AI-bot.doubao-microphone")
    private static let journal = "doubao_microphone_restore_v1"
    private static let path = "/Library/Input Methods/DoubaoIme.app/Contents/Applications/DoubaoImeSettings.app"
    private static let identifier = "com.bytedance.inputmethod.doubaoime.settings"
    private static let statusLock = NSLock()
    private static var status = ""
    static var restorationStatus:String {
        statusLock.lock(); let value = status; statusLock.unlock()
        if !value.isEmpty { return value }
        return UserDefaults.standard.dictionary(forKey:journal) == nil ? "":"豆包麦克风存在待恢复记录，请点击“恢复豆包麦克风”。"
    }
    private static func record(_ value:String) { statusLock.lock(); status = value; statusLock.unlock() }
    static func physical() throws -> MacAudioInput {
        let devices = MacAudioInputs.devices().filter { !$0.uid.hasPrefix("com.vbaudio.vbcable:") && $0.uid != "BlackHole2ch_UID" }
        if let uid = UserDefaults.standard.string(forKey:MacAudioInputs.preference),!uid.isEmpty {
            guard let selected = devices.first(where:{$0.uid == uid}) else { throw MacTab5LocalError.invalid("所选麦克风未连接，未改用其他麦克风。") }; return selected
        }
        let dji = devices.filter { $0.name.localizedCaseInsensitiveContains("DJI") || $0.name.contains("大疆") }
        if dji.count == 1 { return dji[0] }
        var property = AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyDefaultInputDevice,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        var id = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&property,0,nil,&size,&id) == noErr,let selected = devices.first(where:{$0.id == id}) else { throw MacTab5LocalError.invalid("未找到可用的实体麦克风。") }
        return selected
    }
    func acquire(_ device:MacAudioInput,cancelled:@escaping () -> Bool,completion:@escaping (Result<Void,Error>) -> Void) {
        Self.queue.async {
            do {
                try Self.restore()
                guard !cancelled() else { throw MacTab5Error.unconfirmed }
                let app = try Self.settings()
                let button = try Self.selectedButton(app), old = Self.label(button)
                if old != device.name {
                    UserDefaults.standard.set(["previous":old,"temporary":device.name],forKey:Self.journal)
                    try Self.select(device.name,app:app,cancelled:cancelled)
                }
                guard !cancelled() else { try Self.restore(); throw MacTab5Error.unconfirmed }
                DispatchQueue.main.async { completion(.success(())) }
            } catch { DispatchQueue.main.async { completion(.failure(error)) } }
        }
    }
    func release(completion:((Result<Void,Error>) -> Void)? = nil) {
        Self.queue.async {
            if UserDefaults.standard.dictionary(forKey:Self.journal) != nil { Self.record("正在恢复豆包麦克风…") }
            var outcome:Result<Void,Error> = .success(())
            for attempt in 0..<2 {
                do { try Self.restore(); outcome = .success(()); break }
                catch {
                    outcome = .failure(error)
                    if attempt == 0 { Thread.sleep(forTimeInterval:0.25) }
                }
            }
            if case .failure(let error) = outcome {
                Self.record("豆包麦克风恢复未完成，请点击“恢复豆包麦克风”。" + ((error as? MacTab5LocalError)?.localizedDescription ?? ""))
            }
            let result = outcome
            DispatchQueue.main.async { completion?(result) }
        }
    }
    private static func attribute(_ element:AXUIElement,_ name:String) -> CFTypeRef? {
        var value:CFTypeRef?
        return AXUIElementCopyAttributeValue(element,name as CFString,&value) == .success ? value:nil
    }
    private static func label(_ element:AXUIElement) -> String {
        (attribute(element,kAXTitleAttribute) as? String).flatMap { $0.isEmpty ? nil:$0 } ?? (attribute(element,kAXDescriptionAttribute) as? String) ?? ""
    }
    private static func buttons(_ app:AXUIElement) throws -> [AXUIElement] {
        var raw:CFTypeRef?
        let status = AXUIElementCopyAttributeValue(app,kAXWindowsAttribute as CFString,&raw)
        guard status == .success,let windows = raw as? [AXUIElement],!windows.isEmpty,windows.count <= 4 else {
            let windows = raw as? [AXUIElement]
            throw MacTab5LocalError.invalid("豆包设置窗口不可读（AX \(status.rawValue)，窗口 \(windows?.count ?? -1)）。")
        }
        var pending = windows, cursor = 0, result:[AXUIElement] = []
        while cursor < pending.count,cursor < 192 {
            let element = pending[cursor]; cursor += 1; _ = AXUIElementSetMessagingTimeout(element,0.1)
            if attribute(element,kAXRoleAttribute) as? String == kAXButtonRole { result.append(element) }
            if let children = attribute(element,kAXChildrenAttribute) as? [AXUIElement] {
                guard pending.count+children.count <= 192 else { throw MacTab5LocalError.invalid("豆包设置控件超过读取上限，未操作麦克风。") }; pending += children
            }
        }
        return result
    }
    private static func settings() throws -> AXUIElement {
        guard AXIsProcessTrusted() else { throw MacTab5LocalError.invalid("自动选择豆包麦克风需要已有辅助功能权限。") }
        var staticCode:SecStaticCode?, requirement:SecRequirement?
        guard SecStaticCodeCreateWithPath(URL(fileURLWithPath:path) as CFURL,[],&staticCode) == errSecSuccess,
              SecRequirementCreateWithString("anchor apple generic and identifier \"\(identifier)\" and certificate leaf[subject.OU] = \"96L78H6LMH\"" as CFString,[],&requirement) == errSecSuccess,
              let staticCode,let requirement,SecStaticCodeCheckValidity(staticCode,[],requirement) == errSecSuccess else { throw MacTab5LocalError.invalid("豆包设置应用签名未通过核验，未开始收音。") }
        let wait = DispatchSemaphore(value:0)
        DispatchQueue.main.async {
            // This input-method settings app exposes no AX windows while
            // opened in the background. The explicit voice action opens it
            // visibly before selecting the microphone and our own editor.
            let configuration = NSWorkspace.OpenConfiguration(); configuration.activates = true
            NSWorkspace.shared.openApplication(at:URL(fileURLWithPath:path),configuration:configuration) { _,_ in wait.signal() }
        }
        guard wait.wait(timeout:.now()+4) == .success else { throw MacTab5LocalError.invalid("豆包设置启动超时，请打开豆包设置后重试。") }
        guard let app = NSRunningApplication.runningApplications(withBundleIdentifier:identifier).first,app.bundleURL?.path == path else { throw MacTab5LocalError.invalid("未确认豆包设置进程及安装位置，未开始收音。") }
        var live:SecCode?
        guard SecCodeCopyGuestWithAttributes(nil,[kSecGuestAttributePid:app.processIdentifier] as CFDictionary,[],&live) == errSecSuccess,
              let live,SecCodeCheckValidity(live,[],requirement) == errSecSuccess else { throw MacTab5LocalError.invalid("豆包设置运行身份未通过核验，未开始收音。") }
        let element = AXUIElementCreateApplication(app.processIdentifier); _ = AXUIElementSetMessagingTimeout(element,0.2)
        // Workspace completion may precede SwiftUI's first window layout.
        let deadline = ProcessInfo.processInfo.systemUptime+2
        var hint = "语音输入入口尚未出现。"
        while ProcessInfo.processInfo.systemUptime < deadline {
            do {
                let values = try buttons(element)
                if let voice = values.first(where:{label($0) == "语音输入"}) {
                    guard AXUIElementPerformAction(voice,kAXPressAction as CFString) == .success else { throw MacTab5LocalError.invalid("豆包语音输入入口未响应。") }
                    return element
                }
                hint = "已读取 \(values.count) 个按钮，未找到语音输入入口。"
            } catch let error as MacTab5LocalError { hint = error.localizedDescription }
            catch { hint = "豆包设置控件未能读取。" }
            Thread.sleep(forTimeInterval:0.05)
        }
        throw MacTab5LocalError.invalid(hint+"请打开豆包设置并检查辅助功能授权后重试。")
    }
    private static func selectedButton(_ app:AXUIElement) throws -> AXUIElement {
        let names = Set(MacAudioInputs.devices().map(\.name))
        let values = try buttons(app).filter { let name = label($0); return names.contains(name) || name.hasPrefix("自动检测（") }
        guard values.count == 1,let value = values.first else { throw MacTab5LocalError.invalid("未能唯一确认豆包麦克风选择控件；请关闭豆包的麦克风列表后重试。") }
        return value
    }
    private static func select(_ name:String,app:AXUIElement,cancelled:() -> Bool = { false }) throws {
        func same(_ actual:String) -> Bool { actual == name || (name.hasPrefix("自动检测（") && actual.hasPrefix("自动检测（")) }
        guard !cancelled() else { throw MacTab5Error.unconfirmed }
        let current = try selectedButton(app)
        if same(label(current)) { return }
        guard AXUIElementPerformAction(current,kAXPressAction as CFString) == .success else { throw MacTab5Error.unconfirmed }
        // Cancellation can arrive immediately after opening the picker. Always
        // close this picker, including selection/readback failure paths.
        defer { try? closePicker(app) }
        let deadline = ProcessInfo.processInfo.systemUptime+2
        while !cancelled(),ProcessInfo.processInfo.systemUptime < deadline {
            let candidates = try buttons(app).filter { same(label($0)) }
            if candidates.count == 1,AXUIElementPerformAction(candidates[0],kAXPressAction as CFString) == .success {
                // The installed Mac picker remains open after selection. Close
                // only the known picker that this operation just opened.
                try closePicker(app)
                let until = ProcessInfo.processInfo.systemUptime+1
                while !cancelled(),ProcessInfo.processInfo.systemUptime < until {
                    if let selected = try? selectedButton(app),same(label(selected)) { return }
                    Thread.sleep(forTimeInterval:0.05)
                }
                throw MacTab5Error.unconfirmed
            }
            Thread.sleep(forTimeInterval:0.05)
        }
        throw MacTab5LocalError.invalid("豆包未确认麦克风切换，本次收音未开始。")
    }
    private static func closePicker(_ app:AXUIElement) throws {
        let close = try buttons(app).filter { label($0) == "onboarding_close_icon" }
        guard !close.isEmpty else { return }
        guard close.count == 1,AXUIElementPerformAction(close[0],kAXPressAction as CFString) == .success else { throw MacTab5Error.unconfirmed }
        let deadline = ProcessInfo.processInfo.systemUptime+1
        while ProcessInfo.processInfo.systemUptime < deadline {
            if try !buttons(app).contains(where:{label($0) == "onboarding_close_icon"}) { return }
            Thread.sleep(forTimeInterval:0.05)
        }
        throw MacTab5LocalError.invalid("豆包麦克风列表尚未关闭。")
    }
    private static func restore() throws {
        guard let journal = UserDefaults.standard.dictionary(forKey:journal),let previous = journal["previous"] as? String,let temporary = journal["temporary"] as? String else { return }
        let app = try settings()
        // A previous interrupted acquisition may have left its picker open.
        try closePicker(app)
        // A manual microphone selection made meanwhile takes precedence.
        let restorePrevious = label(try selectedButton(app)) == temporary
        if restorePrevious { try select(previous,app:app) }
        UserDefaults.standard.removeObject(forKey:Self.journal)
        record(restorePrevious ? "豆包麦克风已恢复为原选择。":"豆包麦克风恢复已完成，保留当前手动选择。")
    }
}
