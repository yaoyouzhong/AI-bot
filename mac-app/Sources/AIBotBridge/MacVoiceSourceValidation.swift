import Foundation
import CoreGraphics

enum MacVoiceSourceValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Voice source: " + name) }; count += 1
        }
        let suite = "AI-bot-voice-source-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        try check("unset preference preserves legacy routing", MacVoiceCaptureSource.saved(in: defaults) == nil)
        defaults.set("unknown", forKey: MacVoiceCaptureSource.preferenceKey)
        try check("unknown preference is ignored", MacVoiceCaptureSource.saved(in: defaults) == nil)
        for source in MacVoiceCaptureSource.allCases {
            defaults.set(source.rawValue, forKey: MacVoiceCaptureSource.preferenceKey)
            try check("saved choice survives reopening", MacVoiceCaptureSource.saved(in: UserDefaults(suiteName: suite)!) == source)
            for doubao in [false, true] {
                for requested: String? in [nil, "pc", "tab5"] {
                    var opened = [String](), samples = [Int16]()
                    let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { value, _ in opened.append(value) }, feed: { samples += $0 }, finish: {}, cancel: {}), usesDoubao: { doubao }, preferredSource: { MacVoiceCaptureSource.saved(in: defaults) })
                    var start: [String: Any] = ["op": "start", "taskId": MacTab5Voice.agendaOwner, "session": "fixture"]
                    start["source"] = requested
                    let reply = voice.handle(start, device: "fixture")
                    try check("saved source opens exactly one backend and reaches device", opened == [source.rawValue] && reply.body["source"] as? String == source.rawValue)
                    try check("provider remains independent of source", (reply.body["sourceName"] as? String)?.contains(doubao ? "豆包输入法" : "本地识别") == true)
                    let id = reply.body["voiceId"] as! String
                    let audio = voice.handle(["op": "audio", "voiceId": id, "session": "fixture", "seq": 0, "pcm": Data([7, 0]).base64EncodedString()], device: "fixture")
                    try check("only device capture forwards device PCM", source == .tab5 ? audio.status == 200 && samples == [7] : audio.status == 400 && samples.isEmpty)
                    voice.stop()
                }
            }
        }
        var preferred: MacVoiceCaptureSource? = .mac, opened = [String]()
        let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { value, _ in opened.append(value) }, feed: { _ in }, finish: {}, cancel: {}), preferredSource: { preferred })
        let start: [String: Any] = ["op": "start", "taskId": MacTab5Voice.agendaOwner, "session": "fixture"]
        let first = voice.handle(start, device: "fixture"), id = first.body["voiceId"] as! String
        preferred = .tab5
        let current = voice.handle(["op": "poll", "voiceId": id, "session": "fixture"], device: "fixture")
        try check("changing selection leaves active take untouched", current.body["source"] as? String == "pc" && opened == ["pc"])
        _ = voice.handle(["op": "cancel", "voiceId": id, "session": "fixture"], device: "fixture")
        let next = voice.handle(start, device: "fixture")
        try check("next take uses newly selected microphone", next.body["source"] as? String == "tab5" && opened == ["pc", "tab5"])
        voice.stop()
        for requested: String? in [nil, "tab5"] {
            var request = start; request["source"] = requested
            preferred = nil
            let legacy = voice.handle(request, device: "fixture")
            try check("unconfigured source keeps old device request behavior", legacy.body["source"] as? String == (requested ?? "pc"))
            voice.stop()
        }
        var attempts = [String]()
        let unavailable = MacTab5Voice(enabled: { true }, backend: .init(start: { value, _ in attempts.append(value); throw MacTab5Error.unavailable }, feed: { _ in }, finish: {}, cancel: {}), preferredSource: { .tab5 })
        let failed = unavailable.handle(start, device: "fixture")
        try check("unavailable selected microphone does not silently use Mac", failed.body["state"] as? String == "error" && failed.body["source"] as? String == "tab5" && attempts == ["tab5"])
        let djiName = MacAudioInputs.displayName(.init(id: 1, uid: "fixture", name: "DJI MIC MINI"))
        let macName = MacAudioInputs.displayName(.init(id: 2, uid: "fixture", name: "MacBook Pro 麦克风"))
        try check("DJI identity comes from opened endpoint", djiName.contains("DJI 大疆麦克风") && djiName.contains("MIC MINI"))
        try check("Mac endpoint remains identifiable", macName.contains("Mac 麦克风") && macName.contains("MacBook Pro"))
        var ready = false
        let named = MacTab5Voice(enabled: { true }, backend: .init(start: { _, _ in }, feed: { _ in }, finish: {}, cancel: {}, ready: { ready }, sourceName: { djiName }), usesDoubao: { true }, preferredSource: { .mac })
        let preparing = named.handle(start, device: "fixture"), namedID = preparing.body["voiceId"] as! String
        try check("unconfirmed endpoint is not advertised as DJI", !(preparing.body["sourceName"] as! String).contains("DJI"))
        ready = true
        let recording = named.handle(["op": "poll", "voiceId": namedID, "session": "fixture"], device: "fixture")
        try check("ready reply identifies actual microphone and recognizer", recording.body["sourceName"] as? String == djiName + " · 豆包输入法")
        named.stop()
        let visible:[String:Any] = [kCGWindowOwnerPID as String:42,kCGWindowIsOnscreen as String:true,kCGWindowLayer as String:0,kCGWindowAlpha as String:1]
        try check("preserve actually visible settings",MacDoubaoSettingsVisibility.visible(pid:42,windows:[visible]))
        try check("closed AX windows alone are not visible",!MacDoubaoSettingsVisibility.visible(pid:42,windows:[]))
        try check("other application cannot preserve temporary settings",!MacDoubaoSettingsVisibility.visible(pid:43,windows:[visible]))
        for (key,value) in [(kCGWindowIsOnscreen as String,false as Any),(kCGWindowLayer as String,25 as Any),(kCGWindowAlpha as String,0 as Any)] {
            var hidden = visible;hidden[key] = value
            try check("hidden/minimized/overlay settings must be cleaned",!MacDoubaoSettingsVisibility.visible(pid:42,windows:[hidden]))
        }
        try check("finish never reactivates leftover Doubao settings",!MacDoubaoSpeech.mayRestoreFocus(bundleIdentifier:"com.bytedance.inputmethod.doubaoime.settings",pid:42,ownPID:99))
        try check("finish never restores bridge to itself",!MacDoubaoSpeech.mayRestoreFocus(bundleIdentifier:"fixture",pid:99,ownPID:99))
        try check("finish may return to the user's app",MacDoubaoSpeech.mayRestoreFocus(bundleIdentifier:"fixture.editor",pid:42,ownPID:99))
        return count
    }
}
