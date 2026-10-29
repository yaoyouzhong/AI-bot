import Foundation
import zlib
import MacDoubaoStore

/// Bounded settings interoperability. Writes use the upstream MMKV lock and
/// encoder; no settings UI or vendor private framework is invoked.
enum MacDoubaoBackground {
    static let path = "/Library/Input Methods/DoubaoIme.app"
    private static let limit = 1024 * 1024
    private static var invalid: Error { MacTab5LocalError.invalid("无法确认豆包原麦克风设置，未切换收音设备。") }

    static var root:URL { FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/DoubaoIme/.com.apple.xpc.activityd") }
    static func selection() throws -> String { try settings(root)["selectedMicrophoneId"] as! String }
    static func settings(_ root:URL) throws -> [String:Any] {
        let file = root.appendingPathComponent("com.apple.xpc.activity")
        func bounded(_ url: URL) throws -> Data {
            let handle = try FileHandle(forReadingFrom: url); defer { try? handle.close() }
            let data = try handle.read(upToCount: limit + 1) ?? Data()
            guard data.count <= limit else { throw invalid }; return data
        }
        for _ in 0..<3 {
            let before = try bounded(file.appendingPathExtension("crc")), data = try bounded(file)
            let after = try bounded(file.appendingPathExtension("crc"))
            if before == after, let selected = try? decodeSettings(data, metadata: after) { return selected }
            Thread.sleep(forTimeInterval: 0.03)
        }
        throw invalid
    }

    // Independent, bounded reader for the documented unencrypted MMKV map.
    // Only key "d" is decoded; secure Foundation decoding permits no vendor class.
    static func decode(_ data:Data,metadata:Data) throws -> String { try decodeSettings(data,metadata:metadata)["selectedMicrophoneId"] as! String }
    static func decodeSettings(_ data: Data, metadata: Data) throws -> [String:Any] {
        func word(_ bytes: Data, _ offset: Int) -> UInt32 {
            (0..<4).reduce(0) { $0 | UInt32(bytes[offset + $1]) << ($1 * 8) }
        }
        guard data.count >= 4, data.count <= limit, metadata.count >= 112,
              word(metadata, 4) == 4, metadata[104..<112].allSatisfy({ $0 == 0 }) else { throw invalid }
        let count = Int(word(metadata, 28))
        guard count > 0, count <= data.count - 4 else { throw invalid }
        let bytes = Data(data[4..<4 + count])
        let crc = bytes.withUnsafeBytes { crc32(0, $0.bindMemory(to: UInt8.self).baseAddress, uInt(bytes.count)) }
        guard crc == word(metadata, 0) else { throw invalid }
        var cursor = 0
        func length(maximum: Int = limit) throws -> Int {
            var value = 0
            for shift in stride(from: 0, to: 35, by: 7) {
                guard cursor < bytes.count else { throw invalid }
                let byte = bytes[cursor]; cursor += 1
                value |= Int(byte & 127) << shift
                if byte < 128 { guard value <= maximum else { throw invalid }; return value }
            }
            throw invalid
        }
        func field() throws -> Data {
            let count = try length()
            guard count <= bytes.count - cursor else { throw invalid }
            defer { cursor += count }; return Data(bytes[cursor..<cursor + count])
        }
        _ = try length(maximum: Int(UInt32.max)) // Historical root size, including the reserved 32-bit marker.
        var archive: Data?, entries = 0
        while cursor < bytes.count {
            entries += 1; guard entries <= 4096 else { throw invalid }
            let key = try field(), value = try field()
            if key == Data("d".utf8) { archive = value.isEmpty ? nil : value }
        }
        guard let archive, archive.starts(with: Data("bplist00".utf8)),
              let settings = try NSKeyedUnarchiver.unarchivedObject(ofClasses: [NSDictionary.self, NSArray.self, NSString.self, NSNumber.self, NSData.self], from: archive) as? [String: Any],
              let id = settings["selectedMicrophoneId"] as? String, !id.isEmpty, id.utf8.count <= 512,
              id.unicodeScalars.allSatisfy({ !CharacterSet.controlCharacters.contains($0) }) else { throw invalid }
        return settings
    }

    static func update(expected:String,next:String,at root:URL = root) throws {
        guard [expected,next].allSatisfy({ !$0.isEmpty && $0.utf8.count <= 512 && $0.unicodeScalars.allSatisfy({ !CharacterSet.controlCharacters.contains($0) }) }) else { throw invalid }
        // Opening MMKV must not create a missing store or recover damaged data.
        for name in ["com.apple.xpc.activity","com.apple.xpc.activity.crc"] {
            let url = root.appendingPathComponent(name)
            let a = try FileManager.default.attributesOfItem(atPath:url.path)
            guard a[.type] as? FileAttributeType == .typeRegular,
                  (a[.ownerAccountID] as? NSNumber)?.uint32Value == getuid(),
                  let size = a[.size] as? NSNumber,size.intValue >= 112,size.intValue <= limit else { throw invalid }
        }
        _ = try settings(root)
        guard let store = AIBDoubaoStore(root:root.path) else { throw invalid }
        defer { store.close() }
        guard store.tryLock() else { throw MacTab5LocalError.invalid("豆包正在保存设置，请稍后重试。") }
        defer { store.unlock() }
        var current = try settings(root)
        guard current["selectedMicrophoneId"] as? String == expected else { throw MacTab5LocalError.invalid("豆包麦克风选择已变化，未覆盖新的选择。") }
        current["selectedMicrophoneId"] = next
        guard store.writeSettings(current) else { throw invalid }
        store.sync()
        guard NSDictionary(dictionary:try settings(root)).isEqual(to:current) else { throw invalid }
    }

    static func select(_ id: String) {
        DistributedNotificationCenter.default().postNotificationName(
            Notification.Name("DoubaoImeSettings.selectedMicrophoneId"), object: nil,
            userInfo: ["selectedMicrophoneId": id], deliverImmediately: true)
    }
}
