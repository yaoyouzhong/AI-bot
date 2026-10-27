import Foundation

struct MacSavedWiFi {
    let ssid: String, label: String
    let connected: Bool, selected: Bool
    static func validate(ssid:String, label:String) throws {
        guard (1...32).contains(ssid.utf8.count), !ssid.utf8.contains(0), label.utf8.count <= 48,
              !label.unicodeScalars.contains(where:CharacterSet.controlCharacters.contains) else { throw MacTab5LocalError.invalid("Wi-Fi 名称或备注无效；备注最多 48 字节，不能包含换行。") }
    }
    static func parse(_ reply:[String:Any], device:String) throws -> [MacSavedWiFi] {
        guard MacTab5Protocol.integer(reply["version"]) == 1, reply["deviceId"] as? String == device,
              let rows = reply["networks"] as? [[String:Any]], rows.count <= 10 else { throw MacTab5Error.invalidPacket }
        var names = Set<String>()
        return try rows.map { row in
            guard let ssid = row["ssid"] as? String, let label = row["label"] as? String,
                  let connected = row["connected"] as? Bool, let selected = row["selected"] as? Bool, names.insert(ssid).inserted else { throw MacTab5Error.invalidPacket }
            try validate(ssid:ssid,label:label); return .init(ssid:ssid,label:label,connected:connected,selected:selected)
        }
    }
}

enum MacTab5USBManagementReply {
    static func validate(_ response:[String:Any], type:String, device:String) throws {
        guard response["deviceId"] as? String == device else { throw MacTab5Error.identity }
        guard response["type"] as? String == type else { throw MacTab5Error.invalidPacket }
        if MacTab5Protocol.integer(response["version"]) == 1 { return }
        // Existing firmware omits version only in this management reply. The
        // serial identity and host proof are still checked before each command.
        guard type == "tab5_benchmark", response["version"] == nil,
              let result = response["result"] as? String, result.utf8.count <= 8192 else { throw MacTab5Error.invalidPacket }
    }
}

enum MacTab5Benchmark {
    static func handle(_ root:[String:Any], raw:Data, binary:Bool, bulk:Bool) -> MacTab5RPCResult {
        guard let offset = MacTab5Protocol.integer(root["offset"]), (0...1048576).contains(offset),
              let count = MacTab5Protocol.integer(root["count"]), (1...(binary && bulk ? 49152:8192)).contains(count) else { return .error(400,"invalid_range") }
        let expected = pattern(offset:Int(offset),count:Int(count)), legacy = root["data"] != nil
        let upload = legacy || !raw.isEmpty
        if legacy && (!raw.isEmpty || Data(base64Encoded:root["data"] as? String ?? "") != expected) || !raw.isEmpty && raw != expected { return .error(400,"benchmark_bytes_mismatch") }
        var body: [String:Any] = ["offset":offset,"count":count,"sha256":MacTab5Protocol.sha256(expected)]
        if !upload { if binary { return .init(200,body,bytes:expected) }; body["data"] = expected.base64EncodedString() }
        return .init(200,body)
    }
    static func pattern(offset:Int,count:Int) -> Data { Data((0..<count).map { UInt8(truncatingIfNeeded:(offset+$0)*31+17) }) }
    static func format(_ value:String, details:Bool) -> String {
        let rows = value.split(separator:";").map(String.init)
        var lines = [["running":"测速中…","complete":"测速完成","failed":"测速未全部完成","cancelled":"测速已停止","unavailable":"当前模式没有可测速的连接"][rows.first ?? ""] ?? (rows.first ?? "尚未测速")]
        var totals: [String:(bytes:Double,ms:Double)] = [:]
        for row in rows.dropFirst() {
            let f = row.split(separator:",",omittingEmptySubsequences:false).map(String.init)
            if f.count == 9, f[0] == "gallery", let samples = Int(f[2]), (0...8).contains(samples), let bytes = Double(f[3]), let ms = Double(f[4]), bytes >= 0, ms >= 0, bytes.isFinite, ms.isFinite {
                lines.append(String(format:"图库写卡 · %@ · %d/8 个样本 · %.1f KiB/s",f[1],samples,ms > 0 ? bytes*1000/ms/1024:0))
                if details { lines.append("接收 \(f[5]) ms · 写卡 \(f[6]) ms · 并行接收 \(f[7]) 次 · 错误状态 \(f[8])") }
            } else if f.count >= 6, ["USB","Wi-Fi","BLE"].contains(f[0]), ["up","down"].contains(f[1]), let bytes = Double(f[3]), let ms = Double(f[4]), bytes >= 0, ms >= 0, bytes.isFinite, ms.isFinite {
                let key = f[0] + (f[1] == "up" ? " 上传":" 下载")
                if f[5] == "0", ms > 0 { let previous = totals[key] ?? (0,0); totals[key] = (previous.bytes+bytes,previous.ms+ms) }
                if details { lines.append(String(format:"%@ 第 %@ 次：%.1f KiB · %.2f 秒 · 错误状态 %@",key,f[2],bytes/1024,ms/1000,f[5])) }
                else if f[5] != "0" { lines.append(key+" 样本失败："+f[5]) }
            } else if details { lines.append(row) }
        }
        if !details { for key in totals.keys.sorted() { let t = totals[key]!; lines.append(String(format:"%@ %.1f KiB/s",key,t.bytes*1000/t.ms/1024)) } }
        return lines.joined(separator:"\n")
    }
}
