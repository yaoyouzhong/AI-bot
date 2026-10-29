import Foundation
import zlib
import MacDoubaoStore

enum MacDoubaoBackgroundValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ value: Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("Doubao microphone preference reader regression") }; checks += 1
        }
        func varint(_ value: Int) -> Data {
            var value = value, result = Data()
            repeat { var byte = UInt8(value & 127); value >>= 7; if value > 0 { byte |= 128 }; result.append(byte) } while value > 0
            return result
        }
        func record(_ key: String, _ data: Data) -> Data { let key = Data(key.utf8); return varint(key.count) + key + varint(data.count) + data }
        func archive(_ value: Any) throws -> Data {
            try NSKeyedArchiver.archivedData(withRootObject: ["selectedMicrophoneId": value, "unrelated": "preserved"], requiringSecureCoding: true)
        }
        func fixture(_ records: Data, root: Data = Data([0])) -> (Data, Data) {
            let payload = root + records
            var metadata = Data(repeating: 0, count: 112)
            func word(_ offset: Int, _ value: UInt32) { for n in 0..<4 { metadata[offset + n] = UInt8(truncatingIfNeeded: value >> (n * 8)) } }
            word(0, UInt32(payload.withUnsafeBytes { crc32(0, $0.bindMemory(to: UInt8.self).baseAddress, uInt(payload.count)) }))
            word(4, 4); word(28, UInt32(payload.count))
            return (Data(repeating: 0, count: 4) + payload, metadata)
        }
        func rejects(_ pair: (Data, Data)) throws { try check((try? MacDoubaoBackground.decode(pair.0, metadata: pair.1)) == nil) }
        let builtIn = try archive("BuiltInMicrophoneDevice"), automatic = try archive("ime_auto_detect_microphone_id")
        for (id, value) in [("BuiltInMicrophoneDevice", builtIn), ("ime_auto_detect_microphone_id", automatic)] {
            let pair = fixture(record("d", value))
            try check(try MacDoubaoBackground.decode(pair.0, metadata: pair.1) == id)
        }
        let newest = fixture(record("d", builtIn) + record("unrelated", Data([1, 2, 3])) + record("d", automatic))
        try check(try MacDoubaoBackground.decode(newest.0, metadata: newest.1) == "ime_auto_detect_microphone_id")
        let marker = fixture(record("d", builtIn), root: varint(Int(UInt32.max)))
        try check(try MacDoubaoBackground.decode(marker.0, metadata: marker.1) == "BuiltInMicrophoneDevice")
        try rejects(fixture(record("d", builtIn) + record("d", Data())))
        try rejects(fixture(record("other", builtIn)))
        for value: Any in ["", "a\u{0}b", String(repeating: "x", count: 513), 42] { try rejects(fixture(record("d", try archive(value)))) }
        try rejects(fixture(record("d", Data("bplist00bad".utf8))))
        try rejects(fixture(Data([255, 255, 255, 255, 255])))
        try rejects(fixture(Data([10, 1])))
        let valid = fixture(record("d", builtIn))
        for offset in [0, 4, 28, 104] {
            var metadata = valid.1; metadata[offset] ^= 1; try rejects((valid.0, metadata))
        }
        var changed = valid.0; changed[changed.count - 1] ^= 1; try rejects((changed, valid.1))
        try rejects((Data(), valid.1)); try rejects((valid.0, Data()))
        try rejects((Data(repeating: 0, count: 1024 * 1024 + 1), valid.1))
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-store-test-" + UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:false)
        defer { try? FileManager.default.removeItem(at:root) }
        let original: [String:Any] = ["selectedMicrophoneId":"original", "nested":["keep":42], "bytes":Data([1,2,3]), "boolean":true]
        guard let store = AIBDoubaoStore(root:root.path) else { throw MacTab5Error.unavailable }
        try check(store.writeSettings(original)); store.sync(); store.close()
        try MacDoubaoBackground.update(expected:"original",next:"temporary",at:root)
        var expected = original; expected["selectedMicrophoneId"] = "temporary"
        try check(NSDictionary(dictionary:try MacDoubaoBackground.settings(root)).isEqual(to:expected))
        do { try MacDoubaoBackground.update(expected:"original",next:"overwritten",at:root); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { checks += 1 }
        try check(try MacDoubaoBackground.settings(root)["selectedMicrophoneId"] as? String == "temporary")
        try MacDoubaoBackground.update(expected:"temporary",next:"original",at:root)
        try check(NSDictionary(dictionary:try MacDoubaoBackground.settings(root)).isEqual(to:original))
        return checks
    }
}
