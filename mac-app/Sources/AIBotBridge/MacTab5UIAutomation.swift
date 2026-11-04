import Foundation

final class MacTab5UIAutomation {
    struct Result { let request: String; let status: Int; let width: Int; let height: Int; let rotation: Int }
    private let lock = NSLock()
    private var pending: [String:Any]?, result: Result?
    func begin(_ action: [String:Any], now: Int64 = Int64(Date().timeIntervalSince1970*1000)) throws -> String {
        guard let op = action["op"] as? String, ["probe","tap","swipe"].contains(op) else { throw MacTab5Error.invalidPacket }
        var value: [String:Any] = ["version":1,"requestId":MacTab5Protocol.identifier(),"transport":"USB","op":op,"expiresAt":now+5000]
        if op != "probe" {
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
        pending = value; result = nil; return value["requestId"] as! String
    }
    func command(frameEpoch: Int64, now: Int64 = Int64(Date().timeIntervalSince1970*1000)) -> [String:Any]? {
        lock.lock(); defer { lock.unlock() }
        guard var p = pending, let end = p["expiresAt"] as? Int64, now < end, frameEpoch > 0, frameEpoch <= Int64.max-5000 else { return nil }
        p["expiresAt"] = min(end,frameEpoch+5000); return p
    }
    func observe(_ value: Any?) {
        guard let value = value as? String else { return }
        let parts = value.split(separator:",",omittingEmptySubsequences:false)
        guard parts.count == 6, parts[0] == "1", let status = Int(parts[2]), (1...4).contains(status),
              let w = Int(parts[3]), let h = Int(parts[4]), (1...1280).contains(w), (1...1280).contains(h),
              let r = Int(parts[5]), (0...3).contains(r) else { return }
        lock.lock(); defer { lock.unlock() }
        guard pending?["requestId"] as? String == String(parts[1]) else { return }
        result = .init(request:String(parts[1]),status:status,width:w,height:h,rotation:r)
        if status != 1 { pending = nil }
    }
    func snapshot() -> Result? { lock.lock(); defer { lock.unlock() }; return result }
    func cancel() { lock.lock(); pending = nil; result = nil; lock.unlock() }
}
