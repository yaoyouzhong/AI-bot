import Foundation

final class MacTab5UIAutomation {
    struct Result { let request: String; let status: Int; let width: Int; let height: Int; let rotation: Int; let wifiTestActive: Bool }
    private let lock = NSLock()
    private var pending: [String:Any]?, result: Result?
    private var owner: MacTab5Pairing?
    private var wifiOwner: MacTab5Pairing?
    private var wifiSeen: TimeInterval = -.infinity
    func wifiAvailable(for pair: MacTab5Pairing, now: TimeInterval = ProcessInfo.processInfo.systemUptime) -> Bool {
        lock.lock(); defer { lock.unlock() }
        return wifiOwner == pair && now >= wifiSeen && now - wifiSeen < 8
    }
    func resetWiFi() {
        lock.lock(); defer { lock.unlock() }
        wifiOwner = nil; wifiSeen = -.infinity
    }
    func begin(_ action: [String:Any], transport: String = "USB", owner: MacTab5Pairing? = nil,
               now: Int64 = Int64(Date().timeIntervalSince1970*1000)) throws -> String {
        guard let op = action["op"] as? String, ["USB", "Wi-Fi"].contains(transport),
              (["probe","tap","swipe"].contains(op) || op == "wifi-test" && transport == "USB" ||
               op == "restore" && transport == "Wi-Fi"), transport != "Wi-Fi" || owner != nil else {
            throw MacTab5Error.invalidPacket
        }
        var value: [String:Any] = ["version":1,"requestId":MacTab5Protocol.identifier(),"transport":transport,"op":op,"expiresAt":now+5000]
        if op == "tap" || op == "swipe" {
            func number(_ key: String, _ range: ClosedRange<Int64>) throws -> Int64 {
                guard let v = MacTab5Protocol.integer(action[key]), range.contains(v) else { throw MacTab5Error.invalidPacket }; return v
            }
            let w = try number("width",1...1280), h = try number("height",1...1280)
            let x = try number("x",0...(w-1)), y = try number("y",0...(h-1))
            value["width"] = w; value["height"] = h; value["rotation"] = try number("rotation",0...3)
            value["x"] = x; value["y"] = y
            value["toX"] = op == "tap" ? x : try number("toX",0...(w-1))
            value["toY"] = op == "tap" ? y : try number("toY",0...(h-1))
            value["durationMs"] = op == "tap" ? 80 : try number("durationMs",100...1200)
        }
        lock.lock(); defer { lock.unlock() }
        guard pending == nil else { throw MacTab5Error.busy }
        pending = value; result = nil; self.owner = owner; return value["requestId"] as! String
    }
    func command(frameEpoch: Int64, owner: MacTab5Pairing? = nil, now: Int64 = Int64(Date().timeIntervalSince1970*1000)) -> [String:Any]? {
        lock.lock(); defer { lock.unlock() }
        guard self.owner == owner, var p = pending, let end = p["expiresAt"] as? Int64, now < end, frameEpoch > 0, frameEpoch <= Int64.max-5000 else { return nil }
        p["expiresAt"] = min(end,frameEpoch+5000); return p
    }
    func observe(_ value: Any?, owner: MacTab5Pairing? = nil,
                 now: Int64 = Int64(Date().timeIntervalSince1970*1000)) {
        guard let value = value as? String, value.utf8.count <= 96 else { return }
        lock.lock(); defer { lock.unlock() }
        _ = observeLocked(value.split(separator:",",omittingEmptySubsequences:false), transport: "USB",
                          wifiTest: false, owner: owner, now: now)
    }
    func observeWiFi(_ report: String, owner: MacTab5Pairing,
                     tick: TimeInterval = ProcessInfo.processInfo.systemUptime,
                     now: Int64 = Int64(Date().timeIntervalSince1970*1000)) {
        guard report.utf8.count <= 96 else { return }
        let parts = report.split(separator:",",omittingEmptySubsequences:false)
        guard parts.count == 7, parts[6] == "0" || parts[6] == "1" else { return }
        lock.lock(); defer { lock.unlock() }
        let idle = parts.prefix(6).elementsEqual(["1", "", "0", "0", "0", "0"])
        guard idle || observeLocked(Array(parts.prefix(6)), transport: "Wi-Fi", wifiTest: parts[6] == "1",
                                    owner: owner, now: now) else { return }
        wifiOwner = owner; wifiSeen = tick
    }
    private func observeLocked(_ parts: [Substring], transport: String, wifiTest: Bool,
                               owner: MacTab5Pairing?, now: Int64) -> Bool {
        guard parts.count == 6, parts[0] == "1", MacTab5Protocol.isHex(String(parts[1]), count: 32),
              let status = Int(parts[2]), (1...4).contains(status),
              let w = Int(parts[3]), let h = Int(parts[4]), (1...1280).contains(w), (1...1280).contains(h),
              let r = Int(parts[5]), (0...3).contains(r) else { return false }
        if self.owner == owner, pending?["transport"] as? String == transport,
           pending?["requestId"] as? String == String(parts[1]), let expiry = pending?["expiresAt"] as? Int64, now < expiry {
            result = .init(request:String(parts[1]),status:status,width:w,height:h,rotation:r,wifiTestActive:wifiTest)
            if status != 1 { pending = nil }
        }
        return true
    }
    func snapshot() -> Result? { lock.lock(); defer { lock.unlock() }; return result }
    func cancel() { lock.lock(); pending = nil; result = nil; owner = nil; lock.unlock() }
}
