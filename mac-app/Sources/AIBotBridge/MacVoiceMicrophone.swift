import Foundation
import CoreAudio

enum MacVoiceMicrophone: Equatable {
    case automatic, tab5, systemDefault, device(String), legacy, invalid
    static let preferenceKey = "tab5_voice_microphone"
    init(stored: String) {
        switch stored {
        case "auto": self = .automatic
        case "tab5": self = .tab5
        case "system": self = .systemDefault
        default: self = stored.hasPrefix("device:") && stored.count > 7 ? .device(String(stored.dropFirst(7))) : .invalid
        }
    }
    var stored: String? {
        switch self {
        case .automatic: return "auto"
        case .tab5: return "tab5"
        case .systemDefault: return "system"
        case .device(let uid): return "device:" + uid
        case .legacy, .invalid: return nil
        }
    }
    static func isPhysical(_ input: MacAudioInput) -> Bool {
        !input.uid.hasPrefix("com.vbaudio.vbcable:") && input.uid != "BlackHole2ch_UID"
    }
    static func isDJI(_ input: MacAudioInput) -> Bool {
        input.name.localizedCaseInsensitiveContains("DJI") || input.name.contains("大疆") || input.name.localizedCaseInsensitiveContains("Wireless Mic Rx")
    }
}

struct MacVoiceRoute {
    let source: MacVoiceCaptureSource, input: MacAudioInput?
    var name: String { source == .tab5 ? "TAB5 内置麦克风" : input.map(MacAudioInputs.displayName) ?? "Mac 麦克风" }
}

extension MacVoiceSettings {
    // Equivalent old TAB5/device choices display normally without migrating
    // preferences. Ambiguous legacy auto/entry routing gets one explicit item.
    var displayedMicrophone: MacVoiceMicrophone {
        if let microphone { return microphone }
        if source == .tab5 { return .tab5 }
        if source == .mac, !input.isEmpty { return .device(input) }
        return .legacy
    }
    func selecting(_ choice: MacVoiceMicrophone) -> Self {
        var value = self
        guard choice != displayedMicrophone else { return value }
        value.microphone = choice
        switch choice {
        case .automatic, .tab5: value.source = .tab5; value.input = ""
        case .systemDefault: value.source = .mac; value.input = ""
        case .device(let uid): value.source = .mac; value.input = uid
        case .legacy, .invalid: break
        }
        return value
    }
    func route(requested: String, inputs: [MacAudioInput], defaultInput: AudioDeviceID?) throws -> MacVoiceRoute {
        func unavailable(_ message: String) throws -> MacVoiceRoute { throw MacTab5LocalError.invalid(message) }
        func device(_ uid: String, physical: Bool = true) throws -> MacVoiceRoute {
            guard !uid.isEmpty, uid.utf8.count <= 512, !uid.unicodeScalars.contains(where: CharacterSet.controlCharacters.contains),
                  let selected = inputs.first(where: { $0.uid == uid && (!physical || MacVoiceMicrophone.isPhysical($0)) }) else {
                return try unavailable("所选麦克风已断开或不适用，请重新选择；未改用其他麦克风。")
            }
            return .init(source: .mac, input: selected)
        }
        func system() throws -> MacVoiceRoute {
            guard let selected = inputs.first(where: { $0.id == defaultInput && MacVoiceMicrophone.isPhysical($0) }) else {
                return try unavailable("Mac 系统默认麦克风不可用，请连接设备或重新选择。")
            }
            return .init(source: .mac, input: selected)
        }
        let dji = inputs.filter { MacVoiceMicrophone.isPhysical($0) && MacVoiceMicrophone.isDJI($0) }
        switch microphone ?? .legacy {
        case .automatic:
            if dji.count > 1 { return try unavailable("已连接多个 DJI 麦克风，请明确选择一个。") }
            return dji.first.map { .init(source: .mac, input: $0) } ?? .init(source: .tab5, input: nil)
        case .tab5: return .init(source: .tab5, input: nil)
        case .systemDefault: return try system()
        case .device(let uid): return try device(uid)
        case .invalid: return try unavailable("原麦克风设置无法识别，请重新选择后保存。")
        case .legacy:
            if MacVoiceCaptureSource.resolve(requested: requested, preferred: source) == .tab5 { return .init(source: .tab5, input: nil) }
            if !input.isEmpty { return try device(input, physical: provider == "doubao") }
            if provider == "doubao", dji.count == 1 { return .init(source: .mac, input: dji[0]) }
            return try system()
        }
    }
}
