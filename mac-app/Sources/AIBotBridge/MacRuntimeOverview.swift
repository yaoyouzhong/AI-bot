import AppKit

final class MacRuntimeOverview: NSStackView {
    private let network = MacBridgeSettingsStyle.label("等待检测"), system = MacBridgeSettingsStyle.label("等待采样")
    private let cpu = NSProgressIndicator(), memory = NSProgressIndicator(), notice = MacBridgeSettingsStyle.note("")
    private let capture: () -> (SystemMetricsSnapshot?,[String:Any]?), details: (String) -> Void
    init(capture:@escaping () -> (SystemMetricsSnapshot?,[String:Any]?), details:@escaping (String) -> Void) {
        self.capture = capture; self.details = details
        super.init(frame:.zero); orientation = .vertical; alignment = .leading; spacing = 16
        addArrangedSubview(notice); notice.widthAnchor.constraint(equalTo:widthAnchor).isActive = true
        let cards = NSStackView(); cards.spacing = 16; cards.distribution = .fillEqually; cards.alignment = .top
        for (title,readout,key) in [("网络状态",network,"network"),("电脑负载",system,"system")] {
            let box = NSBox(); box.title = title; box.contentViewMargins = NSSize(width:14,height:14)
            let body = NSStackView(); body.orientation = .vertical; body.alignment = .leading; body.spacing = 14
            body.addArrangedSubview(readout); readout.widthAnchor.constraint(equalTo:body.widthAnchor).isActive = true
            if key == "system" { for (label,bar) in [("CPU",cpu),("内存",memory)] { body.addArrangedSubview(MacBridgeSettingsStyle.note(label)); bar.isIndeterminate = false; bar.minValue = 0; bar.maxValue = 100; body.addArrangedSubview(bar); bar.widthAnchor.constraint(equalTo:body.widthAnchor).isActive = true } }
            body.addArrangedSubview(MacSettingsButton(key == "system" ? "电脑详情":"网络详情") { details(key) })
            box.contentView = body; cards.addArrangedSubview(box)
        }
        addArrangedSubview(cards); cards.widthAnchor.constraint(equalTo:widthAnchor).isActive = true
        addArrangedSubview(MacSettingsButton("连接与监控详情") { details("connection") }); refresh()
    }
    required init?(coder:NSCoder) { fatalError() }
    func refresh() {
        let (metrics,value) = capture(), now = Date().timeIntervalSince1970
        let fresh = (value?["epochUtc"] as? NSNumber).map { (0..<10).contains(now - $0.doubleValue) } ?? false
        let checks = fresh ? value?["checks"] as? [[String:Any]] ?? []:[]
        func check(_ key:String) -> [String:Any]? { checks.first { $0["key"] as? String == key } }
        func summary(_ key:String) -> String { check(key)?["summary"] as? String ?? "等待检测" }
        let latency = (check("vpn")?["latencyMs"] as? Double).map { String(format:"%.0f ms",$0) } ?? "— ms"
        network.stringValue = "VPN 响应：\(summary("vpn"))\n\(latency)\n\nPAC 路由：\(summary("pac"))"
        let measured = fresh && metrics.map { (0..<10).contains(now - ($0.pressureObservedAt ?? $0.updatedAt).timeIntervalSince1970) } == true
        cpu.doubleValue = measured ? metrics!.cpuPercent:0; memory.doubleValue = measured ? metrics!.memoryPercent:0
        system.stringValue = measured ? String(format:"CPU %.0f%% · %@\n内存 %.0f%% · %@\n\n磁盘：%@",metrics!.cpuPercent,summary("cpu"),metrics!.memoryPercent,summary("memory"),summary("disk")) : "CPU —\n内存 —\n\n等待电脑采样"
        notice.stringValue = !fresh ? (value == nil ? "等待检测":"监控样本已过期，等待恢复") : (value?["alerts"] as? [[String:Any]])?.first?["title"] as? String ?? "监控数据已更新"
    }
}
