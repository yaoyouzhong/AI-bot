import Foundation

struct MacFlashDeviceSelection {
    private var previous: Set<String>?
    private var expected: String?
    private let bridgePort: String?
    private var recognizedBridge = false
    private var missingSince: Date?
    private(set) var selected: String?
    private(set) var message = "正在识别小屏…"
    init(bridgePort: String? = nil) { self.bridgePort = bridgePort }
    mutating func reset(_ devices: [String]) {
        previous = nil; expected = nil; selected = nil; recognizedBridge = false; missingSince = nil
        update(devices)
    }
    mutating func update(_ devices: [String]) {
        let now = Set(devices)
        let canSelectSingle = previous == nil || previous?.isEmpty == true
        if let previous {
            if expected == nil {
                let added = now.subtracting(previous)
                if added.count == 1 { expected = added.first }
            }
        } else if let bridgePort, now.contains(bridgePort) { expected = bridgePort; recognizedBridge = true }
        if expected == nil && canSelectSingle && devices.count == 1 { expected = devices[0] }
        selected = expected.flatMap { now.contains($0) ? $0 : nil }
        self.previous = now
        if selected != nil { missingSince = nil } else if missingSince == nil { missingSince = Date() }
        message = selected != nil ? (recognizedBridge ? "小屏已连接" : "USB 设备已连接")
            : expected != nil ? (Date().timeIntervalSince(missingSince ?? Date()) < 3 ? "正在确认连接…" : "等待小屏重新连接…")
            : devices.isEmpty ? "请连接小屏" : "发现多个 USB 设备，请拔插一次小屏"
    }
    mutating func scanFailed() { selected = nil; message = "正在重新检测连接…" }
}
