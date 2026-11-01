import AppKit
import AVFoundation
import AudioToolbox
import CoreAudio
import Speech

struct MacAudioInput { let id:AudioDeviceID, uid:String, name:String }
enum MacAudioInputs {
    static let preference = "tab5_voice_input_uid"
    static func defaultInputID() -> AudioDeviceID? {
        var property = AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyDefaultInputDevice,mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        var id = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        return AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&property,0,nil,&size,&id) == noErr ? id : nil
    }
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
    static func apply(to input:AVAudioInputNode, selected: MacAudioInput? = nil) throws {
        let uid = selected?.uid ?? UserDefaults.standard.string(forKey:preference) ?? ""
        guard !uid.isEmpty else { return }
        guard let device = devices().first(where:{$0.uid == uid && (selected == nil || $0.id == selected?.id)}),let unit = input.audioUnit else { throw MacTab5LocalError.invalid("所选麦克风未连接，请在语音设置重新选择。") }
        var id = device.id
        guard AudioUnitSetProperty(unit,kAudioOutputUnitProperty_CurrentDevice,kAudioUnitScope_Global,0,&id,UInt32(MemoryLayout<AudioDeviceID>.size)) == noErr else { throw MacTab5LocalError.invalid("无法启用所选麦克风。") }
    }
}
