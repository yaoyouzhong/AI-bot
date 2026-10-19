import CryptoKit
import CoreFoundation
import Foundation

enum MacTab5Error: LocalizedError, Equatable {
    case invalidPacket, invalidSettings, unavailable, busy, staleRevision, unconfirmed, deviceBusy, saveFailed
    case identity, pairingRequired, keychain, serial, timeout, unsupported, invalidWiFi

    var errorDescription: String? {
        switch self {
        case .invalidPacket: return "TAB5 数据校验失败。"
        case .invalidSettings: return "设置无效：请检查数值、页面顺序，并至少启用一个页面。"
        case .unavailable: return "TAB5 暂不可用，请检查 USB 或 Wi-Fi 连接。"
        case .busy: return "正在等待 TAB5 回执，请稍后再试。"
        case .staleRevision: return "设备设置已改变，请重新读取后再保存。"
        case .unconfirmed: return "结果未确认，请重新读取设备设置；不会自动重复保存。"
        case .deviceBusy: return "TAB5 正在编辑本机设置，请退出设备设置页后重试。"
        case .saveFailed: return "设备未能完整保存，请重新读取确认实际设置。"
        case .identity: return "USB 设备身份不匹配；本次没有替换已保存的配对。"
        case .pairingRequired: return "请先通过 USB 配对 TAB5。"
        case .keychain: return "无法读取或保存 TAB5 钥匙串记录，请检查 Keychain 权限。"
        case .serial: return "串口连接中断或被其他操作占用。"
        case .timeout: return "未收到 TAB5 确认，请检查数据线和设备固件。"
        case .unsupported: return "当前设备或桥接不支持此操作。"
        case .invalidWiFi: return "Wi-Fi 名称应为 1–32 字节，密码最多 63 字节，不能包含空字符。"
        }
    }
}

enum MacTab5Protocol {
    static let prefix = "@AIBOT "
    static let maximumFrame = 32_768
    static let maximumRPC = 12_288

    static func isHex(_ value: String, count: Int) -> Bool {
        value.utf8.count == count && value.utf8.allSatisfy { (48...57).contains($0) || (97...102).contains($0) }
    }

    static func identifier() -> String { UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased() }
    static func sha256(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }
    static func proof(key: Data, message: String) -> String {
        HMAC<SHA256>.authenticationCode(for: Data(message.utf8), using: SymmetricKey(data: key))
            .map { String(format: "%02x", $0) }.joined()
    }
    static func verify(key: Data, message: String, proof: String) -> Bool {
        guard key.count == 32, isHex(proof, count: 64) else { return false }
        let expected = Self.proof(key: key, message: message)
        return zip(expected.utf8, proof.utf8).reduce(UInt8(0)) { $0 | ($1.0 ^ $1.1) } == 0
    }

    static func encrypt(_ clear: Data, key: Data, challenge: String, nonce: Data? = nil) throws -> Data {
        guard key.count == 32, isHex(challenge, count: 32), clear.count <= maximumFrame - 28 else { throw MacTab5Error.invalidPacket }
        let iv = try nonce.map { try AES.GCM.Nonce(data: $0) } ?? AES.GCM.Nonce()
        guard iv.withUnsafeBytes({ $0.count }) == 12 else { throw MacTab5Error.invalidPacket }
        let sealed = try AES.GCM.seal(clear, using: SymmetricKey(data: key), nonce: iv, authenticating: Data(challenge.utf8))
        // TAB5 uses nonce | tag | ciphertext, unlike CryptoKit's combined layout.
        var packet = iv.withUnsafeBytes { Data($0) }
        packet.append(sealed.tag); packet.append(sealed.ciphertext)
        return packet
    }

    static func decrypt(_ packet: Data, key: Data, challenge: String) throws -> Data {
        guard key.count == 32, isHex(challenge, count: 32), (28...maximumRPC).contains(packet.count) else { throw MacTab5Error.invalidPacket }
        let bytes = [UInt8](packet)
        let sealed = try AES.GCM.SealedBox(nonce: AES.GCM.Nonce(data: bytes[0..<12]),
                                         ciphertext: bytes[28...], tag: bytes[12..<28])
        return try AES.GCM.open(sealed, using: SymmetricKey(data: key), authenticating: Data(challenge.utf8))
    }

    static func json(_ value: [String: Any]) throws -> Data {
        try JSONSerialization.data(withJSONObject: value, options: [.sortedKeys, .withoutEscapingSlashes])
    }
    static func object(_ data: Data) throws -> [String: Any] {
        guard let value = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw MacTab5Error.invalidPacket }
        return value
    }
    static func line(_ value: [String: Any]) throws -> Data {
        var bytes = Data(prefix.utf8); bytes.append(try json(value)); bytes.append(10); return bytes
    }
    static func integer(_ value: Any?) -> Int64? {
        guard let n = value as? NSNumber, CFGetTypeID(n) != CFBooleanGetTypeID(),
              n.doubleValue.isFinite, abs(n.doubleValue) <= 9_007_199_254_740_991,
              n.doubleValue == Double(n.int64Value) else { return nil }
        return n.int64Value
    }

    static func snapshot(_ snapshot: MacStatusSnapshot, device: String, session: String, sequence: Int64,
                         command: [String: Any]?) throws -> Data {
        let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601
        var data = try object(encoder.encode(snapshot))
        // ESP8266 display preferences and unavailable capabilities are not TAB5 settings.
        data.removeValue(forKey: "displayPolicy")
        data.removeValue(forKey: "domesticQuotas")
        data["epochMilliseconds"] = Int64(snapshot.capturedAt.timeIntervalSince1970 * 1000)
        data["pcInput"] = ["session": session, "sequence": sequence]
        data["connectionHealth"] = ["displayCommand": command.map { $0 as Any } ?? NSNull()]
        if var quotas = data["quotas"] as? [String: Any], var codex = quotas["codex"] as? [String: Any],
           snapshot.quotas?.codex?.plan?.uppercased() != "PLUS" {
            codex.removeValue(forKey: "primaryPercent"); codex.removeValue(forKey: "primaryResetsAt")
            quotas["codex"] = codex; data["quotas"] = quotas
        }
        if var stocks = data["stocks"] as? [String: Any], let quotes = stocks["quotes"] as? [[String: Any]] {
            stocks["quotes"] = Array(quotes.prefix(20)); data["stocks"] = stocks
        }
        if var music = data["music"] as? [String: Any] { music["hasArtwork"] = false; data["music"] = music }
        let zone = TimeZone(secondsFromGMT: snapshot.utcOffsetSeconds) ?? .gmt
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = zone
        let date = Date(timeIntervalSince1970: TimeInterval(snapshot.epochUtc))
        let parts = calendar.dateComponents([.year, .month, .day, .weekday], from: date)
        data["calendar"] = ["date": String(format: "%04d-%02d-%02d", parts.year ?? 0, parts.month ?? 0, parts.day ?? 0),
                            "weekday": "星期" + ["日", "一", "二", "三", "四", "五", "六"][(parts.weekday ?? 1) - 1],
                            "lunar": LunarDate.format(epoch: snapshot.epochUtc, utcOffset: snapshot.utcOffsetSeconds)]
        data["quotaDetails"] = quotaDetails(snapshot)
        let frame = try json(["version": 1, "type": "tab5_status", "deviceId": device,
                              "session": session, "sequence": sequence, "data": data])
        guard frame.count <= maximumFrame - 28 else { throw MacTab5Error.invalidPacket }
        return frame
    }

    private static func quotaDetails(_ snapshot: MacStatusSnapshot) -> [[String: Any]] {
        [("claude", "Claude", snapshot.quotas?.claude, snapshot.claude.tokensToday),
         ("codex", "Codex", snapshot.quotas?.codex, snapshot.codex.tokensToday)].map { id, name, quota, tokens in
            var value: [String: Any] = ["id": id, "name": name, "stale": quota?.stale ?? false, "tokensToday": tokens]
            value["plan"] = quota?.plan
            let hasPrimary = id != "codex" || quota?.plan?.uppercased() == "PLUS"
            value["primary"] = hasPrimary ? quota?.primaryPercent : nil
            value["weekly"] = quota?.weeklyPercent
            value["primaryResetEpoch"] = hasPrimary ? quota?.primaryResetsAt.map { Int64($0.timeIntervalSince1970) } : nil
            value["weeklyResetEpoch"] = quota?.weeklyResetsAt.map { Int64($0.timeIntervalSince1970) }
            let formatter = DateFormatter(); formatter.dateFormat = "MM-dd HH:mm"
            formatter.locale = Locale(identifier: "en_US_POSIX"); formatter.timeZone = TimeZone(secondsFromGMT: snapshot.utcOffsetSeconds)
            value["primaryReset"] = hasPrimary ? quota?.primaryResetsAt.map { formatter.string(from: $0) } ?? "--" : "--"
            value["weeklyReset"] = quota?.weeklyResetsAt.map { formatter.string(from: $0) } ?? "--"
            value["planReset"] = "--"
            value["credits"] = quota?.resetCreditsAvailable
            return value
        }
    }
}
