import AppKit
import ApplicationServices
import CoreAudio
import Foundation

struct MacTab5Launcher: Codable {
    let id: String
    let name: String
    let path: String
}
final class MacTab5Launchers {
    private let defaults: UserDefaults
    private let lock = NSLock()
    init(defaults: UserDefaults = .standard) { self.defaults = defaults }
    func load() throws -> [MacTab5Launcher] {
        lock.lock(); defer { lock.unlock() }
        guard let bytes = defaults.data(forKey: "tab5_launchers") else { return [] }
        guard bytes.count <= 32768 else { throw MacTab5Error.invalidSettings }
        let values = try JSONDecoder().decode([MacTab5Launcher].self, from: bytes); try Self.validate(values, exists: false); return values
    }
    func save(_ values: [MacTab5Launcher]) throws {
        try Self.validate(values, exists: true)
        lock.lock(); defer { lock.unlock() }; defaults.set(try JSONEncoder().encode(values), forKey: "tab5_launchers")
    }
    static func validate(_ values: [MacTab5Launcher], exists: Bool) throws {
        guard values.count <= 4, Set(values.map { $0.id.lowercased() }).count == values.count else { throw MacTab5Error.invalidSettings }
        for value in values {
            guard UUID(uuidString: value.id) != nil, !value.name.isEmpty, value.name.count <= 16,
                  !value.name.unicodeScalars.contains(where: CharacterSet.controlCharacters.contains), value.path.hasPrefix("/"),
                  value.path.utf8.count <= 4096, !value.path.utf8.contains(0),
                  !exists || FileManager.default.fileExists(atPath: value.path) else { throw MacTab5Error.invalidSettings }
        }
    }
}

struct MacTab5ComputerBackend {
    var read: () throws -> [String: Any]
    var perform: (String, [String: Any], MacTab5Launcher?) throws -> Bool
}

final class MacTab5Computer {
    static let enabledKey = "tab5_computer_control_enabled"
    private let lock = NSLock()
    private let enabled: () -> Bool
    private let musicEnabled: () -> Bool
    let launchers: MacTab5Launchers
    private let backend: MacTab5ComputerBackend
    private var receipts: [String: (String, MacTab5RPCResult, TimeInterval)] = [:]
    init(defaults: UserDefaults = .standard, backend: MacTab5ComputerBackend? = nil, music: @escaping () -> MusicSnapshot? = { nil }) {
        enabled = { defaults.bool(forKey: Self.enabledKey) }
        musicEnabled = { defaults.bool(forKey: MacMusicService.enabledKey) }
        launchers = MacTab5Launchers(defaults: defaults)
        self.backend = backend ?? MacTab5ComputerBackend(read: { try MacTab5ComputerPlatform.read(music: music()) }, perform: { try MacTab5ComputerPlatform.perform($0, request: $1, launcher: $2, music: music()) })
    }
    var isEnabled: Bool { enabled() }
    var hasCapabilities: Bool { enabled() || musicEnabled() }
    private func permits(_ op: String) -> Bool {
        op == "media" ? musicEnabled() : enabled()
    }
    private func state() throws -> [String: Any] {
        var state = try backend.read(); state["version"] = 1; state["oneClickUnlock"] = false
        state["launchers"] = enabled() ? try launchers.load().map { ["id": $0.id, "name": $0.name, "kind": $0.path.hasSuffix(".app") ? "app" : "folder"] } : []
        if !enabled() {
            state["volumeAvailable"] = false; state["canLock"] = false
            state.removeValue(forKey: "volume"); state.removeValue(forKey: "muted")
        }
        if !musicEnabled() {
            state["mediaAvailable"] = false
            for key in ["canPlayPause", "canPrevious", "canNext", "playing"] { state.removeValue(forKey: key) }
        }
        return state
    }
    func handle(_ root: [String: Any]) -> MacTab5RPCResult {
        guard hasCapabilities else { return .error(403, "computer_control_disabled") }
        let op = root["op"] as? String ?? ""
        let fields: [String: Set<String>] = ["status": [], "lock-screen": [], "volume": ["volume"], "mute": ["muted"], "lock": [], "launch": ["launcherId"], "media": ["action"]]
        guard let parameters = fields[op], Set(root.keys).isSubset(of: parameters.union(["kind", "op", "requestId", "session", "issuedAt", "binaryReply", "bulkReply", "iconRevision", "unlockHistory"])) else { return .error(400, "invalid_parameters") }
        if op == "volume", !(MacTab5Protocol.integer(root["volume"]).map { (0...100).contains($0) } ?? false) { return .error(400, "invalid_volume") }
        if op == "mute", !MacTab5Protocol.isBoolean(root["muted"]) { return .error(400, "invalid_muted") }
        if op == "media", !["play-pause", "previous", "next"].contains(root["action"] as? String ?? "") { return .error(400, "invalid_media_action") }
        guard op == "status" || op == "lock-screen" || permits(op) else { return .error(403, op == "media" ? "music_control_disabled" : "computer_control_disabled") }
        lock.lock(); defer { lock.unlock() }
        do {
            if op == "status" { return .init(200, try state()) }
            if op == "lock-screen" { return .init(200, ["locked": NSNull(), "lockScreenCover": NSNull(), "displayState": NSNull()]) }
            guard let suppliedID = root["requestId"] as? String, let uuid = UUID(uuidString: suppliedID) else { return .error(400, "invalid_request_id") }
            let id = uuid.uuidString.lowercased()
            var command: [String: Any] = ["op": op]
            for key in parameters { command[key] = root[key] }
            let fingerprint = MacTab5Protocol.sha256(try MacTab5Protocol.json(command)), now = ProcessInfo.processInfo.systemUptime
            receipts = receipts.filter { now - $0.value.2 < 120 }
            if let receipt = receipts[id] { return receipt.0 == fingerprint ? receipt.1 : .error(409, "request_id_conflict") }
            guard receipts.count < 256 else { return .error(429, "command_capacity_reached") }
            let launcher: MacTab5Launcher?
            if op == "launch" {
                guard let selected = root["launcherId"] as? String, UUID(uuidString: selected) != nil,
                      let value = try launchers.load().first(where: { $0.id.lowercased() == selected.lowercased() }) else { return .error(404, "launcher_not_found") }
                launcher = value
            } else { launcher = nil }
            // Reserve before the effect. Failure/uncertainty is retained too;
            // the same request cannot repeat a key, track skip or application launch.
            receipts[id] = (fingerprint, .error(503, "command_unconfirmed"), now)
            guard permits(op) else { return .error(403, op == "media" ? "music_control_disabled" : "computer_control_disabled") }
            let accepted = try backend.perform(op, root, launcher)
            let actual = try state()
            var confirmed = accepted
            if op == "volume" { confirmed = accepted && MacTab5Protocol.integer(actual["volume"]) == MacTab5Protocol.integer(root["volume"]) }
            if op == "mute" { confirmed = accepted && actual["muted"] as? Bool == root["muted"] as? Bool }
            let result: MacTab5RPCResult = confirmed ? .init(200, ["ok": true, "state": actual]) : .init(503, ["error": "command_unconfirmed", "state": actual])
            receipts[id] = (fingerprint, result, now); return result
        } catch { return .error(503, op == "status" ? "computer_control_unavailable" : "command_unconfirmed") }
    }
}

enum MacTab5ComputerPlatform {
    static func main<T>(_ body: () -> T) -> T { Thread.isMainThread ? body() : DispatchQueue.main.sync(execute: body) }
    private static func output() -> AudioDeviceID? {
        var address = AudioObjectPropertyAddress(mSelector: kAudioHardwarePropertyDefaultOutputDevice, mScope: kAudioObjectPropertyScopeGlobal, mElement: kAudioObjectPropertyElementMain)
        var device = AudioDeviceID(0), size = UInt32(MemoryLayout<AudioDeviceID>.size)
        return AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size, &device) == noErr && device != 0 ? device : nil
    }
    private static func channels(_ device: AudioDeviceID) -> [AudioObjectPropertyElement] {
        [AudioObjectPropertyElement(0), 1, 2].filter { element in
            var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyVolumeScalar, mScope: kAudioDevicePropertyScopeOutput, mElement: element)
            return AudioObjectHasProperty(device, &address)
        }.reduce(into: []) { result, element in if result.isEmpty || result.first != 0 { result.append(element) } }
    }
    private static func volume(_ device: AudioDeviceID) -> (Int, Bool)? {
        let channels = channels(device)
        guard !channels.isEmpty else { return nil }
        var sum: Float32 = 0
        for element in channels {
            var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyVolumeScalar, mScope: kAudioDevicePropertyScopeOutput, mElement: element)
            var value: Float32 = 0, size = UInt32(MemoryLayout<Float32>.size)
            guard AudioObjectGetPropertyData(device, &address, 0, nil, &size, &value) == noErr, value.isFinite, (0...1).contains(value) else { return nil }; sum += value
        }
        var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyMute, mScope: kAudioDevicePropertyScopeOutput, mElement: kAudioObjectPropertyElementMain)
        var muted: UInt32 = 0, size = UInt32(MemoryLayout<UInt32>.size)
        guard AudioObjectGetPropertyData(device, &address, 0, nil, &size, &muted) == noErr else { return nil }
        return (Int((sum / Float32(channels.count) * 100).rounded()), muted != 0)
    }
    private static func player(_ expected: MusicSnapshot?) -> (MacMusicProvider, MusicSnapshot)? {
        guard UserDefaults.standard.bool(forKey: MacMusicService.enabledKey), let expected, !expected.title.isEmpty else { return nil }
        let matches: [(MacMusicProvider, MusicSnapshot)] = [MacMusicProvider.appleMusic, .spotify].compactMap { provider in
            guard MacMusicService.isRunning(provider), let actual = MacMusicService.read(provider), actual.title == expected.title, actual.artist == expected.artist else { return nil }; return (provider, actual)
        }
        return matches.count == 1 ? matches[0] : nil
    }
    static func read(music: MusicSnapshot?) throws -> [String: Any] {
        var state: [String: Any] = ["volumeAvailable": false, "canLock": AXIsProcessTrusted(), "locked": NSNull(), "displayState": NSNull(), "mediaAvailable": false]
        if let device = output(), let (volume, muted) = volume(device) { state["volumeAvailable"] = true; state["volume"] = volume; state["muted"] = muted }
        if UserDefaults.standard.bool(forKey: MacMusicService.enabledKey), let track = music, !track.title.isEmpty { state["mediaAvailable"] = true; state["canPlayPause"] = true; state["canPrevious"] = true; state["canNext"] = true; state["playing"] = track.playing }
        return state
    }
    static func perform(_ op: String, request: [String: Any], launcher: MacTab5Launcher?, music: MusicSnapshot?) throws -> Bool {
        if op == "volume" || op == "mute" {
            guard let device = output(), volume(device) != nil else { return false }
            if op == "volume", let level = MacTab5Protocol.integer(request["volume"]) {
                for element in channels(device) {
                    var value = Float32(level) / 100
                    var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyVolumeScalar, mScope: kAudioDevicePropertyScopeOutput, mElement: element)
                    guard AudioObjectSetPropertyData(device, &address, 0, nil, UInt32(MemoryLayout<Float32>.size), &value) == noErr else { return false }
                }; return true
            }
            var value: UInt32 = request["muted"] as? Bool == true ? 1 : 0
            var address = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyMute, mScope: kAudioDevicePropertyScopeOutput, mElement: kAudioObjectPropertyElementMain)
            return AudioObjectSetPropertyData(device, &address, 0, nil, UInt32(MemoryLayout<UInt32>.size), &value) == noErr
        }
        if op == "launch", let launcher {
            try MacTab5Launchers.validate([launcher], exists: true)
            return main { NSWorkspace.shared.open(URL(fileURLWithPath: launcher.path)) }
        }
        if op == "lock" {
            guard AXIsProcessTrusted() else { return false }
            return main { script("with timeout of 2 seconds\n tell application \"System Events\" to keystroke \"q\" using {control down, command down}\nend timeout") }
        }
        if op == "media" {
            guard let (provider, before) = main({ player(music) }), let action = request["action"] as? String,
                  let verb = ["play-pause": "playpause", "previous": "previous track", "next": "next track"][action] else { return false }
            guard main({ script("with timeout of 2 seconds\n tell application id \"\(provider.bundleIdentifier)\" to \(verb)\nend timeout") }) else { return false }
            for attempt in 0..<5 {
                if let actual = main({ MacMusicService.read(provider) }), action == "play-pause" ? actual.playing != before.playing : actual.title != before.title || actual.artist != before.artist { return true }
                if attempt < 4 { Thread.sleep(forTimeInterval: 0.12) }
            }
            return false
        }
        return false
    }
    private static func script(_ source: String) -> Bool {
        guard let script = NSAppleScript(source: source) else { return false }
        var error: NSDictionary?; _ = script.executeAndReturnError(&error); return error == nil
    }
}
