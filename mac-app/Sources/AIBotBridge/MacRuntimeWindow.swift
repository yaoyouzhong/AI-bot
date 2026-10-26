import AppKit

struct MacRuntimeOptions: Codable, Equatable {
    var enabled = true, network = true, expectProxy = false, throughput = false
    var interval = 30, slow = 2500, cpu = 90, memory = 92, sustained = 30
    func validate() throws {
        guard (15...300).contains(interval), (500...15000).contains(slow), (50...100).contains(cpu), (50...100).contains(memory), (10...300).contains(sustained) else { throw MacTab5LocalError.invalid("检测间隔、持续时间或告警阈值超出范围。") }
    }
}
enum MacRuntimePreferences {
    static let file = MacProfileFile<MacRuntimeOptions>("runtime-health.json",initial:{ .init() },validate:{ try $0.validate() })
    static var current: MacRuntimeOptions { (try? file.read()) ?? MacRuntimeOptions(enabled:false,network:false) }
}
final class MacRuntimeWindow: NSWindowController {
    private let capture: () -> String, readout = MacBridgeSettingsStyle.label(""), status = MacBridgeSettingsStyle.note("")
    private var timer: Timer?
    private var overview: MacRuntimeOverview?, detailWindow: MacInformationWindow?
    private var readingDevice = false
    init(file: MacProfileFile<MacRuntimeOptions> = MacRuntimePreferences.file, capture: @escaping () -> String, reload: @escaping () -> Void,
         overviewCapture: @escaping () -> (SystemMetricsSnapshot?,[String:Any]?) = { (nil,nil) }, deviceCapture: @escaping () -> String = { "尚无设备诊断采样。" }, deviceImage: @escaping () -> NSImage? = { nil }, deviceImageCaption: @escaping () -> String = { "Mac 当前采样封面（仅显示内存中的图像）" }, deviceRefresh: @escaping (@escaping () -> Void) -> Void = { $0() }) {
        self.capture = capture; super.init(window:MacBridgeSettingsStyle.window("运行监控",width:800,height:650))
        let overview = MacBridgeSettingsPage(), settings = MacBridgeSettingsPage()
        overview.heading("运行监控",subtitle:"依据本机实际采样；无数据的指标保持未知。")
        let cards = MacRuntimeOverview(capture:overviewCapture,details:{ [weak self] group in
            let title = group == "system" ? "电脑详情" : group == "network" ? "网络详情":"连接与监控"
            let window = MacInformationWindow(title,live:true,capture:{
                let (metrics,value) = overviewCapture(), keys = group == "system" ? ["cpu","memory","queue","disk"] : group == "network" ? ["vpn","pac"]:["codex","bridge"]
                var lines = ((value?["checks"] as? [[String:Any]]) ?? []).filter { keys.contains($0["key"] as? String ?? "") }.map { "\($0["label"] as? String ?? "") · \($0["summary"] as? String ?? "")\n\($0["detail"] as? String ?? "")\n\($0["advice"] as? String ?? "")" }
                if group == "system",let metrics { lines.append(String(format:"整机流量 · 上传 %.1f KB/s · 下载 %.1f KB/s",Double(metrics.uploadBytesPerSecond)/1024,Double(metrics.downloadBytesPerSecond)/1024)) }
                if group == "network" { lines.append(capture()) }
                return lines.isEmpty ? "等待检测。":lines.joined(separator:"\n\n")
            }); self?.detailWindow = window; window.open()
        }); self.overview = cards; overview.add(cards)
        settings.heading("检测设置",subtitle:"只检测实际网络路径，不修改系统代理。测速需要主动启用。")
        var options: MacRuntimeOptions
        do { options = try file.read() } catch { options = .init(enabled:false,network:false); status.stringValue = "设置读取失败，原文件保留。" }
        func toggle(_ title: String, _ state: Bool) -> NSButton { let b = NSButton(checkboxWithTitle:title,target:nil,action:nil); b.state = state ? .on:.off; settings.add(b); return b }
        let enabled = toggle("启用监控",options.enabled), network = toggle("检测 VPN / PAC",options.network), proxy = toggle("关键域名预期走代理",options.expectProxy), speed = toggle("小流量测速（每 5 分钟最多 128 KiB）",options.throughput)
        let specs = [("检测间隔（15–300 秒）",options.interval),("慢响应阈值（500–15000 ms）",options.slow),("CPU 阈值（50–100%）",options.cpu),("内存阈值（50–100%）",options.memory),("持续时间（10–300 秒）",options.sustained)]
        let fields = specs.map { NSTextField(string:String($0.1)) }
        let grid = NSGridView(views:zip(specs,fields).map { [MacBridgeSettingsStyle.label($0.0.0),$0.1] }); grid.rowSpacing = 12; grid.columnSpacing = 20; settings.add(grid)
        for field in fields { field.widthAnchor.constraint(equalToConstant:110).isActive = true }
        settings.add(MacBridgeSettingsStyle.note("测速是系统网络路径上的小文件观测，不代表最大带宽。macOS 没有等价的 Windows 处理器等待队列指标，该项保持未知。"))
        let device = MacBridgeSettingsPage(), deviceReadout = MacBridgeSettingsStyle.label(deviceCapture())
        device.heading("TAB5 运行诊断",subtitle:"读取当前绑定设备的实际采样。USB 自动更新；无线诊断支持 Wi-Fi 和蓝牙，超时保留上次采样。")
        let refresh = MacSettingsButton("读取设备诊断") { [weak self] in
            guard let self, !self.readingDevice else { return }; self.readingDevice = true
            deviceRefresh { DispatchQueue.main.async { [weak self] in
                self?.readingDevice = false; deviceReadout.stringValue = deviceCapture(); self?.status.stringValue = "诊断读取已结束。"
            } }
            deviceReadout.stringValue = deviceCapture(); self.status.stringValue = "正在读取设备诊断…"
        }
        let artwork = NSImageView(); artwork.imageScaling = .scaleProportionallyUpOrDown
        artwork.widthAnchor.constraint(equalToConstant:150).isActive = true; artwork.heightAnchor.constraint(equalToConstant:150).isActive = true
        artwork.image = deviceImage()
        let caption = MacBridgeSettingsStyle.note(deviceImageCaption())
        device.add(refresh); device.add(caption); device.add(artwork); device.add(deviceReadout)
        let tabs = MacBridgeSettingsTabs([("监控状态",overview),("检测设置",settings),("设备诊断",device)])
        window?.contentView = MacBridgeSettingsShell(body:tabs,status:status,actions:[MacSettingsButton("刷新") { [weak self] in self?.refresh() },MacSettingsButton("保存设置") { [weak self] in
            guard let self else { return }; self.window?.makeFirstResponder(nil)
            let values = fields.compactMap { Int($0.stringValue) }
            guard values.count == fields.count else { self.status.stringValue = "请填写整数。"; return }
            let value = MacRuntimeOptions(enabled:enabled.state == .on,network:network.state == .on,expectProxy:proxy.state == .on,throughput:speed.state == .on,interval:values[0],slow:values[1],cpu:values[2],memory:values[3],sustained:values[4])
            do { try file.write(value); reload(); self.status.stringValue = "已保存，重新检测中。"; self.refresh() } catch { self.status.stringValue = error.localizedDescription }
        }])
        self.refresh(); timer = Timer.scheduledTimer(withTimeInterval:2,repeats:true) { [weak self] _ in if self?.window?.isVisible == true { self?.refresh(); deviceReadout.stringValue = deviceCapture(); artwork.image = deviceImage(); caption.stringValue = deviceImageCaption() } }
    }
    deinit { timer?.invalidate() }
    required init?(coder:NSCoder) { fatalError() }
    private func refresh() { overview?.refresh(); readout.stringValue = capture() }
    func open() { refresh(); MacSettingsControls.open(self) }
}
