import Foundation

enum MacVoiceMicrophoneValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ label: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Microphone selection: " + label) }; checks += 1
        }
        let suite = "AI-bot-microphone-" + UUID().uuidString, defaults: UserDefaults
        defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let builtIn = MacAudioInput(id: 1, uid: "built-in", name: "Built-in"), dji = MacAudioInput(id: 2, uid: "dji", name: "DJI Mini2")
        let cable = MacAudioInput(id: 3, uid: "com.vbaudio.vbcable:1", name: "VB-CABLE")
        let snapshot = [builtIn, dji, cable]
        let initial = MacVoiceSettings.read(defaults)
        try check("fresh configuration defaults to defined automatic policy", initial.microphone == .automatic)
        try check("reading defaults does not persist preferences", defaults.persistentDomain(forName: suite)?.isEmpty != false)
        func route(_ settings: MacVoiceSettings, _ inputs: [MacAudioInput]? = nil, requested: String = "tab5") throws -> MacVoiceRoute {
            try settings.route(requested: requested, inputs: inputs ?? snapshot, defaultInput: 1)
        }
        var chosen = try route(initial)
        try check("automatic selects DJI even when device requests TAB5", chosen.source == .mac && chosen.input?.uid == "dji")
        chosen = try route(initial, [builtIn, cable], requested: "pc")
        try check("no DJI falls back to TAB5 rather than default Mac microphone", chosen.source == .tab5 && chosen.input == nil)
        chosen = try route(initial, [cable])
        try check("virtual cable cannot be selected as physical input", chosen.source == .tab5)
        let system = initial.selecting(.systemDefault)
        chosen = try route(system)
        try check("system default remains default despite connected DJI", chosen.source == .mac && chosen.input?.uid == "built-in")
        let fixed = initial.selecting(.device("built-in"))
        chosen = try route(fixed)
        try check("specific input selected exactly", chosen.input?.uid == "built-in")
        func rejected(_ settings: MacVoiceSettings, _ inputs: [MacAudioInput]) -> Bool { (try? route(settings, inputs)) == nil }
        try check("removed fixed microphone cannot silently fall back", rejected(fixed, [dji, cable]))
        try check("virtual device explicitly rejected", rejected(initial.selecting(.device(cable.uid)), snapshot))
        try check("missing system default cannot select DJI instead", rejected(system, [dji, cable]))
        let secondDJI = MacAudioInput(id: 4, uid: "dji-2", name: "Wireless Mic Rx")
        try check("multiple DJI requires explicit choice", rejected(initial, snapshot + [secondDJI]))
        try check("specific DJI removes ambiguity", tryUID(initial.selecting(.device("dji-2")), snapshot + [secondDJI]) == "dji-2")
        func persisted() -> [String: Any] { defaults.persistentDomain(forName: suite) ?? [:] }
        defaults.set("doubao", forKey: MacSpeechProvider.key)
        defaults.set("tab5", forKey: MacVoiceCaptureSource.preferenceKey)
        defaults.set("previous-unused-input", forKey: MacAudioInputs.preference)
        var old = MacVoiceSettings.read(defaults), before = NSDictionary(dictionary: persisted())
        try check("existing TAB5 selection displays normally without migration", old.microphone == nil && old.displayedMicrophone == .tab5)
        let same = old.selecting(.tab5)
        try check("unchanged display preserves unused old preference", same == old && NSDictionary(dictionary: persisted()) == before)
        old.save(defaults)
        try check("provider-only save preserves old audio settings", NSDictionary(dictionary: persisted()) == before)
        defaults.set("pc", forKey: MacVoiceCaptureSource.preferenceKey); defaults.set("", forKey: MacAudioInputs.preference)
        old = MacVoiceSettings.read(defaults); before = NSDictionary(dictionary: persisted())
        try check("old Mac automatic uses an explicit compatibility entry", old.displayedMicrophone == .legacy)
        chosen = try route(old, [builtIn], requested: "pc")
        try check("old Mac automatic retains system default fallback", chosen.source == .mac && chosen.input?.uid == "built-in")
        old.save(defaults)
        try check("opening or saving legacy policy does not convert to automatic", NSDictionary(dictionary: persisted()) == before)
        defaults.removeObject(forKey: MacVoiceCaptureSource.preferenceKey)
        old = MacVoiceSettings.read(defaults)
        try check("old entry-driven TAB5 remains TAB5", (try? route(old, requested: "tab5").source) == .tab5)
        try check("old entry-driven Mac still chooses DJI", (try? route(old, requested: "pc").input?.uid) == "dji")
        let updated = old.selecting(.systemDefault); updated.save(defaults)
        try check("explicit new choice round trips", MacVoiceSettings.read(defaults) == updated)
        try check("new setting retains compatible old source keys", MacVoiceCaptureSource.saved(in: defaults) == .mac && defaults.string(forKey: MacAudioInputs.preference) == "")
        defaults.set("future-unknown", forKey: MacVoiceMicrophone.preferenceKey)
        try check("unknown persisted policy fails closed", rejected(MacVoiceSettings.read(defaults), snapshot))
        // Production voice state must return the resolved wire source, keep it
        // frozen through polling, and resolve again only on the next take.
        var inputs = snapshot, selection = initial, planned: MacVoiceRoute?, opened: [(String,String?)] = [], resolves = 0
        let backend = MacTab5SpeechBackend(start: { source, _ in opened.append((source, planned?.input?.uid)) }, feed: { _ in }, finish: {}, cancel: {},
            sourceName: { planned?.name ?? "" }, resolveSource: { source, force in
                resolves += 1
                planned = try force ? .init(source: .tab5, input: nil) : selection.route(requested: source, inputs: inputs, defaultInput: 1)
                return planned!.source.rawValue
            })
        let voice = MacTab5Voice(enabled: { true }, backend: backend, usesDoubao: { true })
        func start(force: Bool = false) -> MacTab5RPCResult {
            var request: [String:Any] = ["op":"start","taskId":MacTab5Voice.agendaOwner,"session":"fixture"]
            if force { request["source"] = "tab5" }
            return voice.handle(request, device:"fixture")
        }
        var started = start(), id = started.body["voiceId"] as! String
        func request(_ op: String, _ id: String) -> MacTab5RPCResult { voice.handle(["op":op,"voiceId":id,"session":"fixture"],device:"fixture") }
        try check("automatic routing reaches the real voice wire reply", started.body["source"] as? String == "pc" && opened.count == 1 && opened[0].1 == "dji")
        try check("actual microphone name reaches device and UI", (started.body["sourceName"] as? String)?.contains("DJI Mini2") == true && voice.summary.contains("DJI Mini2"))
        inputs = [builtIn]; selection = system
        try check("device changes and preference edits cannot reroute active capture", request("poll", id).body["source"] as? String == "pc" && resolves == 1 && planned?.input?.uid == "dji")
        _ = request("cancel", id); selection = initial
        started = start(); id = started.body["voiceId"] as! String
        try check("next take re-evaluates missing DJI and reports TAB5", started.body["source"] as? String == "tab5" && opened.count == 2 && resolves == 2)
        _ = request("cancel", id); inputs = snapshot
        started = start(); id = started.body["voiceId"] as! String
        try check("explicit switch to TAB5 overrides automatic DJI for this take", request("tab5", id).body["source"] as? String == "tab5" && opened.last?.0 == "tab5" && planned?.input == nil)
        _ = request("cancel", id)
        started = start(force: true); id = started.body["voiceId"] as! String
        try check("device explicit built-in recording overrides automatic policy", started.body["source"] as? String == "tab5" && opened.count == 5)
        _ = request("cancel", id); selection = fixed; inputs = [dji]
        let unavailable = start()
        try check("missing chosen microphone cannot open another backend", unavailable.body["state"] as? String == "error" && opened.count == 5)
        voice.stop()
        let localVoice = MacTab5Voice(enabled: { true }, backend: backend, usesDoubao: { false })
        let localError = localVoice.handle(["op":"start","taskId":MacTab5Voice.agendaOwner,"session":"fixture"], device:"fixture")
        try check("local recognizer preserves the concrete disconnected device reason", localError.body["state"] as? String == "error" && (localError.body["message"] as? String)?.contains("断开") == true && opened.count == 5)
        localVoice.stop()
        return checks
    }
    private static func tryUID(_ value: MacVoiceSettings, _ inputs: [MacAudioInput]) -> String? {
        try? value.route(requested: "tab5", inputs: inputs, defaultInput: 1).input?.uid
    }
}
