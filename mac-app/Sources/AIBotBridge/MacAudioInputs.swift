import AppKit
import AVFoundation
import AudioToolbox
import CoreAudio
import Speech

struct MacAudioInput { let id:AudioDeviceID, uid:String, name:String }
enum MacAudioInputs {
    static let preference = "tab5_voice_input_uid"
    static func devices() -> [MacAudioInput] {
        var address = AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyDevices,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain), size:UInt32 = 0
        guard AudioObjectGetPropertyDataSize(AudioObjectID(kAudioObjectSystemObject),&address,0,nil,&size) == noErr else { return [] }
        var ids = [AudioDeviceID](repeating:0,count:Int(size)/MemoryLayout<AudioDeviceID>.size)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&address,0,nil,&size,&ids) == noErr else { return [] }
        func text(_ id:AudioDeviceID,_ selector:AudioObjectPropertySelector) -> String? {
            var value:Unmanaged<CFString>?, count = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
            var property = AudioObjectPropertyAddress(mSelector:selector,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
            guard AudioObjectGetPropertyData(id,&property,0,nil,&count,&value) == noErr, let value else { return nil }; return value.takeRetainedValue() as String
        }
        return ids.compactMap { id in
            var streams = AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyStreams,mScope:kAudioDevicePropertyScopeInput,mElement:kAudioObjectPropertyElementMain), bytes:UInt32 = 0
            guard AudioObjectGetPropertyDataSize(id,&streams,0,nil,&bytes) == noErr,bytes > 0,let uid = text(id,kAudioDevicePropertyDeviceUID),let name = text(id,kAudioObjectPropertyName) else { return nil }
            return .init(id:id,uid:uid,name:name)
        }
    }
    static func selected() -> MacAudioInput? { let uid = UserDefaults.standard.string(forKey:preference) ?? ""; return devices().first { $0.uid == uid } }
    static func displayName(_ device: MacAudioInput) -> String {
        let kind = device.name.localizedCaseInsensitiveContains("DJI") || device.name.contains("大疆") ? "DJI 大疆麦克风" : "Mac 麦克风"
        return kind + "（" + String(device.name.prefix(48)) + "）"
    }
    static func activeInput(of input: AVAudioInputNode) -> MacAudioInput? {
        guard let unit = input.audioUnit else { return nil }
        var id = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        guard AudioUnitGetProperty(unit, kAudioOutputUnitProperty_CurrentDevice, kAudioUnitScope_Global, 0, &id, &size) == noErr else { return nil }
        return devices().first { $0.id == id }
    }
    static func apply(to input:AVAudioInputNode) throws {
        guard let uid = UserDefaults.standard.string(forKey:preference),!uid.isEmpty else { return }
        guard let device = devices().first(where:{$0.uid == uid}),let unit = input.audioUnit else { throw MacTab5LocalError.invalid("所选麦克风未连接，请在语音设置重新选择。") }
        var id = device.id
        guard AudioUnitSetProperty(unit,kAudioOutputUnitProperty_CurrentDevice,kAudioUnitScope_Global,0,&id,UInt32(MemoryLayout<AudioDeviceID>.size)) == noErr else { throw MacTab5LocalError.invalid("无法启用所选麦克风。") }
    }
}

final class MacVoiceInputSettings: NSObject {
    private weak var page:MacBridgeSettingsPage?
    private var generation = UUID()
    private let input = NSPopUpButton(), status = MacBridgeSettingsStyle.note(""), text = MacBridgeSettingsStyle.label("")
    private let testButton = NSButton(title: "测试 Mac 本地识别", target: nil, action: nil)
    private var entries:[MacAudioInput] = [], tester:MacTab5SpeechBackend?, timeout:Timer?
    init(page:MacBridgeSettingsPage) {
        self.page = page; super.init(); input.target = self; input.action = #selector(selectInput)
        input.identifier = .init("voice-mac-input")
        testButton.target = self; testButton.action = #selector(test)
        page.field("Mac 优先麦克风",input)
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("检查音频") { [weak self] in self?.reload() },testButton,MacSettingsButton("停止测试") { [weak self] in self?.stop() }]))
        text.isSelectable = true; page.add(status); page.add(text); reload()
    }
    deinit { timeout?.invalidate(); tester?.cancel() }
    func setMacInputEnabled(_ enabled: Bool) {
        input.isEnabled = enabled; testButton.isEnabled = enabled
        if !enabled { cancelTest() }
    }
    @objc private func selectInput() {
        stop(); let i = input.indexOfSelectedItem
        guard i == 0 || entries.indices.contains(i-1) else { return }
        UserDefaults.standard.set(i == 0 ? "":entries[i-1].uid,forKey:MacAudioInputs.preference); status.stringValue = "麦克风选择已保存，下次收音使用。"
    }
    private func reload() {
        entries = MacAudioInputs.devices(); input.removeAllItems(); input.addItems(withTitles:["Mac 默认麦克风"]+entries.map(\.name))
        let uid = UserDefaults.standard.string(forKey:MacAudioInputs.preference) ?? ""
        if let i = entries.firstIndex(where:{$0.uid == uid}) { input.selectItem(at:i+1) }
        else if !uid.isEmpty { input.addItem(withTitle:"之前选择的麦克风（未连接）"); input.selectItem(at:input.numberOfItems-1) }
        status.stringValue = "检测到 \(entries.count) 个输入设备 · " + (SFSpeechRecognizer.authorizationStatus() == .authorized ? "语音识别已授权":"语音识别待授权")
    }
    @objc private func test() {
        guard tester == nil else { return }; status.stringValue = "检查语音与麦克风权限…"
        let owner = generation
        MacTab5Speech.authorize { [weak self] allowed in
            guard let self,self.generation == owner,self.page?.window?.isVisible == true,self.tester == nil else { return }; guard allowed else { self.status.stringValue = "请在系统设置允许语音识别与麦克风。"; return }
            let backend = MacTab5Speech().backend(); self.tester = backend; self.text.stringValue = ""
            do { try backend.start("pc") { [weak self] value,final,error in DispatchQueue.main.async {
                guard let self,self.generation == owner,self.tester != nil else { return }; self.text.stringValue = value
                if final || error != nil { self.status.stringValue = error?.message ?? "本地识别完成；测试文字不会发送。"; self.timeout?.invalidate(); self.tester?.cancel(); self.tester = nil }
            } }; self.status.stringValue = "请说话，15 秒后停止；测试文字不会发送。"; self.timeout = Timer.scheduledTimer(withTimeInterval:15,repeats:false) { [weak self] _ in self?.stop() } }
            catch { backend.cancel(); self.tester = nil; self.status.stringValue = error.localizedDescription }
        }
    }
    func cancelTest() { generation = UUID(); timeout?.invalidate(); tester?.cancel(); tester = nil }
    private func stop() { timeout?.invalidate(); tester?.finish(); if tester != nil { status.stringValue = "正在完成识别…"; timeout = Timer.scheduledTimer(withTimeInterval:15,repeats:false) { [weak self] _ in self?.tester?.cancel(); self?.tester = nil; self?.status.stringValue = "测试已结束。" } } }
}
