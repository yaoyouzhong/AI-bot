import Foundation

/// USB ACKs arrive on one serial queue. Retain only numeric allocation evidence
/// before a successful preview job clears the device's last-task diagnostic.
struct MacPreviewMemoryObservation {
    private var owner = "", firmware = ""
    private var uptime: Int64 = -1
    private var job: UInt32 = 0
    private var failures: UInt32 = 0
    private var last: (job: UInt32, phase: UInt32, uptime: Int64)?

    mutating func observe(_ value: [String: Any], device: String, host: String) {
        guard let time = MacTab5Protocol.integer(value["uptimeMs"]), time >= 0,
              let version = value["firmware"] as? String, version.utf8.count <= 40,
              let text = value["displayDiag"] as? String, text.utf8.count <= 1024,
              let suffix = text.split(separator: ";").first(where: { $0.hasPrefix("sp,") }) else { return }
        let parts = suffix.split(separator: ",", omittingEmptySubsequences: false)
        guard parts.count == 6, parts[0] == "sp", parts[1] == "1" else { return }
        func number(_ part: Substring) -> UInt32? {
            guard !part.isEmpty, part.utf8.allSatisfy({ (48...57).contains($0) }) else { return nil }
            return UInt32(part)
        }
        guard let busy = number(parts[2]), busy <= 1, let finished = number(parts[3]),
              number(parts[4]) != nil, let phase = number(parts[5]), phase <= 3 else { return }
        let key = device + ":" + host
        if key != owner || version != firmware || time < uptime || finished < job {
            owner = key; firmware = version; failures = 0; last = nil; job = 0
        }
        if phase > 0, finished > 0, last?.job != finished {
            failures = failures == .max ? failures : failures + 1
            last = (finished, phase, time)
        }
        uptime = time; job = finished
    }

    func summary(device: String, host: String) -> String? {
        guard owner == device + ":" + host, let last else { return nil }
        let names = [1: "原始像素", 2: "编码输出", 3: "JPEG 内部内存"]
        return "USB 预览内存失败：采样 \(failures) 次，最近任务 \(last.job)，阶段 \(last.phase)（\(names[Int(last.phase)]!)），设备运行 \(last.uptime) ms"
    }
}
