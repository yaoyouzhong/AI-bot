import Foundation

/// Existing device status metadata only. Never used to authorize input or
/// confirm success; raw wire text and identities are not retained.
enum MacDeviceUnlockHistory {
    static func decode(_ text:String) -> [String]? {
        guard (2...586).contains(text.utf8.count),text.hasPrefix("1;") else { return nil }
        let parts = text.dropFirst(2).split(separator:";",omittingEmptySubsequences:false)
        guard (1...8).contains(parts.count) else { return nil }
        var result:[String] = []
        for part in parts {
            let bytes = Array(part.utf8)
            guard bytes.count == 72,bytes.allSatisfy({ (48...57).contains($0) || (65...70).contains($0) }) else { return nil }
            let words = stride(from:0,to:72,by:8).compactMap { UInt32(String(decoding:bytes[$0..<$0+8],as:UTF8.self),radix:16) }
            guard words.count == 9,words[0] > 0 else { return nil }
            let flags = words[1], link = flags&3, outcome = (flags>>2)&3
            guard link < 3,outcome > 0 else { return nil }
            let transport = ["USB","蓝牙","Wi-Fi"][Int(link)]
            let state = ["","成功","未完成","已关闭"][Int(outcome)]
            func ms(_ value:UInt32) -> String { value == UInt32.max ? "未知":String(min(120_000,value))+" ms" }
            result.append("设备 #\(words[0]) \(transport) · \(state) · 原因 \((flags>>4)&31) · 阶段 \((flags>>9)&7) · 缺失位 \(flags>>16) · 总计 \(ms(words[3])) · 锁样本 \(ms(words[4])) · 状态 RPC \(words[7]>>16)/\(words[7]&65535) · RPC 年龄 \(ms(words[8]))")
        }
        return result
    }
}
