import AppKit
import AVFoundation
import CoreAudio
import Security

/// Audio goes only to the explicitly identified vendor virtual device. Never
/// changes system default input/output or falls back to the physical speakers.
final class MacDoubaoAudio {
    private let lock = NSLock()
    private var engine:AVAudioEngine?, player:AVAudioPlayerNode?
    private var queued = 0, generation = UUID()
    private(set) var tailSeconds:TimeInterval = 0.25
    static func device() throws -> MacAudioInput {
        let path = URL(fileURLWithPath:"/Library/Audio/Plug-Ins/HAL/VBCable.driver")
        var code:SecStaticCode?, requirement:SecRequirement?
        guard SecStaticCodeCreateWithPath(path as CFURL,[],&code) == errSecSuccess,
              SecRequirementCreateWithString("anchor apple generic and identifier \"com.vbaudio.vbcable\" and certificate leaf[subject.OU] = \"6K8JQXLBSY\"" as CFString,[],&requirement) == errSecSuccess,
              let code,let requirement,SecStaticCodeCheckValidity(code,[],requirement) == errSecSuccess else { throw MacTab5LocalError.invalid("TAB5 音频转送需要安装官方 VB-CABLE Mac 驱动，请在语音设置中查看组件。") }
        let devices = MacAudioInputs.devices().filter { $0.uid.hasPrefix("com.vbaudio.vbcable:") }
        guard devices.count == 1,let device = devices.first else { throw MacTab5LocalError.invalid("没有找到唯一的 VB-CABLE 音频设备；请完成安装或重新启动 Mac。") }
        var property = AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyStreams,mScope:kAudioDevicePropertyScopeOutput,mElement:kAudioObjectPropertyElementMain), size:UInt32 = 0
        guard AudioObjectGetPropertyDataSize(device.id,&property,0,nil,&size) == noErr,size > 0 else { throw MacTab5Error.unavailable }
        return device
    }
    func start(device:MacAudioInput) throws {
        stop()
        let engine = AVAudioEngine(), player = AVAudioPlayerNode()
        guard let unit = engine.outputNode.audioUnit else { throw MacTab5Error.unavailable }
        var id = device.id
        guard AudioUnitSetProperty(unit,kAudioOutputUnitProperty_CurrentDevice,kAudioUnitScope_Global,0,&id,UInt32(MemoryLayout<AudioDeviceID>.size)) == noErr else { throw MacTab5LocalError.invalid("无法打开 VB-CABLE 输出；未改用扬声器。") }
        var actual = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        guard AudioUnitGetProperty(unit,kAudioOutputUnitProperty_CurrentDevice,kAudioUnitScope_Global,0,&actual,&size) == noErr,actual == device.id,
              let format = AVAudioFormat(standardFormatWithSampleRate:16000,channels:2) else { throw MacTab5Error.unavailable }
        var rate = Float64(48000), rateSize = UInt32(MemoryLayout<Float64>.size)
        var property = AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyNominalSampleRate,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        _ = AudioObjectGetPropertyData(device.id,&property,0,nil,&rateSize,&rate)
        guard rate.isFinite,rate >= 8000 else { throw MacTab5Error.unavailable }
        var frames:UInt32 = 0
        for selector in [kAudioDevicePropertyLatency,kAudioDevicePropertySafetyOffset,kAudioDevicePropertyBufferFrameSize] {
            property.mSelector = selector; property.mScope = selector == kAudioDevicePropertyBufferFrameSize ? kAudioObjectPropertyScopeGlobal:kAudioObjectPropertyScopeOutput
            var value:UInt32 = 0, size:UInt32 = 4
            if AudioObjectGetPropertyData(device.id,&property,0,nil,&size,&value) == noErr { frames = min(96000,frames+min(value,96000)) }
        }
        tailSeconds = min(1,max(0.25,Double(frames)/rate+0.05))
        engine.attach(player); engine.connect(player,to:engine.mainMixerNode,format:format)
        engine.prepare(); try engine.start(); player.play()
        lock.lock(); self.engine = engine; self.player = player; lock.unlock()
    }
    func feed(_ pcm:[Int16]) throws {
        guard !pcm.isEmpty,pcm.count <= 16000,let format = AVAudioFormat(standardFormatWithSampleRate:16000,channels:2),
              let buffer = AVAudioPCMBuffer(pcmFormat:format,frameCapacity:AVAudioFrameCount(pcm.count)),let channels = buffer.floatChannelData else { throw MacTab5Error.invalidPacket }
        buffer.frameLength = AVAudioFrameCount(pcm.count)
        for i in pcm.indices { let value = Float(pcm[i])/32768; channels[0][i] = value; channels[1][i] = value }
        lock.lock(); defer { lock.unlock() }
        guard let player,engine?.isRunning == true,queued+pcm.count <= 128000 else { throw MacTab5LocalError.invalid("VB-CABLE 音频队列未就绪或积压过多，本次收音停止。") }
        queued += pcm.count; let owner = generation, size = pcm.count
        player.scheduleBuffer(buffer,completionCallbackType:.dataPlayedBack) { [weak self] _ in
            guard let self else { return }; self.lock.lock(); defer { self.lock.unlock() }
            if self.generation == owner { self.queued = max(0,self.queued-size) }
        }
    }
    var drained:Bool { lock.lock(); defer { lock.unlock() }; return queued == 0 }
    var pendingSeconds:TimeInterval { lock.lock(); defer { lock.unlock() }; return Double(queued) / 16000 }
    func stop() {
        lock.lock(); generation = UUID(); let oldPlayer = player, oldEngine = engine
        player = nil; engine = nil; queued = 0; lock.unlock()
        oldPlayer?.stop(); oldEngine?.stop()
    }
}

struct MacDoubaoDrain {
    private var since:TimeInterval?
    mutating func ready(drained:Bool,tail:TimeInterval,now:TimeInterval) -> Bool {
        guard drained else { since = nil; return false }
        if since == nil { since = now }
        return tail.isFinite && tail >= 0 && now-(since ?? now) >= tail
    }
}

/// Public CoreAudio process metadata proves that the signed input method is
/// actually using the intended input device. No audio tap or sample is read.
final class MacDoubaoCapture {
    private let app:NSRunningApplication, device:AudioDeviceID
    init(device:AudioDeviceID) throws {
        guard #available(macOS 14.2,*) else { throw MacTab5LocalError.invalid("自动确认豆包收音需要 macOS 14.2 或更新版本。") }
        let apps = NSRunningApplication.runningApplications(withBundleIdentifier:"com.bytedance.inputmethod.doubaoime")
        guard apps.count == 1,let app = apps.first,app.bundleURL?.path == "/Library/Input Methods/DoubaoIme.app" else { throw MacTab5LocalError.invalid("豆包输入法尚未运行，请先切换到豆包输入法。") }
        var code:SecCode?, requirement:SecRequirement?
        guard SecCodeCopyGuestWithAttributes(nil,[kSecGuestAttributePid:app.processIdentifier] as CFDictionary,[],&code) == errSecSuccess,
              SecRequirementCreateWithString("anchor apple generic and identifier \"com.bytedance.inputmethod.doubaoime\" and certificate leaf[subject.OU] = \"96L78H6LMH\"" as CFString,[],&requirement) == errSecSuccess,
              let code,let requirement,SecCodeCheckValidity(code,[],requirement) == errSecSuccess else { throw MacTab5Error.identity }
        self.app = app; self.device = device
    }
    func running() -> Bool? {
        guard !app.isTerminated else { return nil }
        var pid = app.processIdentifier, object = AudioObjectID(0), size = UInt32(MemoryLayout<AudioObjectID>.size)
        var property = AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyTranslatePIDToProcessObject,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        let status = AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&property,UInt32(MemoryLayout<pid_t>.size),&pid,&size,&object)
        guard status == noErr else { return nil }; if object == 0 { return false }
        var running:UInt32 = 0; size = 4; property.mSelector = kAudioProcessPropertyIsRunningInput
        guard AudioObjectGetPropertyData(object,&property,0,nil,&size,&running) == noErr else { return nil }
        if running == 0 { return false }
        property.mSelector = kAudioProcessPropertyDevices; property.mScope = kAudioObjectPropertyScopeInput
        guard AudioObjectGetPropertyDataSize(object,&property,0,nil,&size) == noErr,size <= 1024 else { return nil }
        var devices = [AudioDeviceID](repeating:0,count:Int(size)/MemoryLayout<AudioDeviceID>.size)
        guard AudioObjectGetPropertyData(object,&property,0,nil,&size,&devices) == noErr else { return nil }
        return devices.contains(device) ? true:nil
    }
}

/// Only a committed IME result after explicit stop, stopped capture and a quiet
/// unmarked editor may become final. Time passing alone cannot confirm text.
struct MacDoubaoCompletion {
    var stopped = false, committed = false, text = "", changedAt:TimeInterval = 0
    mutating func update(_ value:String,committed:Bool,now:TimeInterval) { text = value; self.committed = committed; changedAt = now }
    func final(capture:Bool?,marked:Bool,now:TimeInterval) -> String? {
        guard stopped,committed,capture == false,!marked,!text.isEmpty,text.utf8.count <= 2000,now-changedAt >= 0.6 else { return nil }
        return text
    }
}
