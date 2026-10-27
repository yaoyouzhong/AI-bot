import AppKit

final class MacESPDisplayWindow: NSWindowController {
    private let mode = NSPopUpButton(), interval = MacSettingsControls.popup(["10 秒","15 秒","30 秒","60 秒"])
    private let brightness = NSSlider(value: 50, minValue: 0, maxValue: 100, target: nil, action: nil)
    private let lightLabel = MacBridgeSettingsStyle.note("尚未读取亮度"), status = MacBridgeSettingsStyle.note("")
    private let rows = NSStackView()
    private var order: [String], checks: [String:NSButton] = [:]
    private let apply: (MacDisplayPolicy) -> Void, sendBrightness: (Int) -> Bool
    private var policy: MacDisplayPolicy
    private static let selections: [(String,String,Bool)] = [("自动轮播","auto",true),("智能跟随","auto",false)] + MacDisplayPolicy.pageLabels.map { ($0.1,$0.0,false) } + [("综合动态","activity",false),("双模型额度","quotas",false),("国产模型","domestic",false),("屏保","screensaver",false)]
    init(policy: MacDisplayPolicy, apply: @escaping (MacDisplayPolicy) -> Void, brightness: @escaping (Int) -> Bool, read: @escaping (@escaping (Result<Int,Error>) -> Void) -> Void) {
        self.policy = policy; self.apply = apply; sendBrightness = brightness; order = policy.pages + MacDisplayPolicy.modes.filter { !policy.pages.contains($0) }
        super.init(window: MacBridgeSettingsStyle.window("ESP8266 · 显示设置",width:580,height:640))
        window?.contentMinSize = NSSize(width:520,height:480)
        let page = MacBridgeSettingsPage(); page.heading("显示设置",subtitle:"调整自动生效；轮播页面和顺序会在切换显示方式后保留。")
        self.brightness.isEnabled = false; self.brightness.isContinuous = false; self.brightness.target = self; self.brightness.action = #selector(changeBrightness)
        page.field("屏幕亮度",self.brightness); page.add(lightLabel)
        page.add(MacSettingsButton("读取亮度") { [weak self] in self?.readBrightness(read) })
        mode.addItems(withTitles:Self.selections.map(\.0)); mode.selectItem(at:Self.selections.firstIndex { $0.1 == policy.selectedMode && ($0.1 != "auto" || $0.2 == policy.cycleEnabled) } ?? 0)
        interval.selectItem(at:[10,15,30,60].firstIndex(of:policy.intervalSeconds) ?? 1)
        mode.target = self; mode.action = #selector(change); interval.target = self; interval.action = #selector(change)
        page.field("显示方式",mode); page.field("轮播间隔",interval)
        for id in order { let b = NSButton(checkboxWithTitle:MacDisplayPolicy.pageLabels.first { $0.0 == id }?.1 ?? id,target:self,action:#selector(change)); b.state = policy.pages.contains(id) ? .on:.off; checks[id] = b }
        rows.orientation = .vertical; rows.alignment = .leading; rows.spacing = 6; page.add(rows)
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[MacSettingsButton("关闭") { [weak self] in self?.close() }]); rebuild()
        readBrightness(read)
    }
    required init?(coder:NSCoder) { fatalError() }
    func open() { MacSettingsControls.open(self) }
    private func readBrightness(_ read: (@escaping (Result<Int,Error>) -> Void) -> Void) {
        lightLabel.stringValue = "正在读取亮度…"
        read { [weak self] result in DispatchQueue.main.async {
            guard let self else { return }; switch result {
            case .success(let level): self.brightness.doubleValue = Double(level); self.brightness.isEnabled = true; self.lightLabel.stringValue = "\(level)%"
            case .failure(let error): self.brightness.isEnabled = false; self.lightLabel.stringValue = error.localizedDescription
            }
        } }
    }
    @objc private func changeBrightness() { let value = Int(brightness.doubleValue.rounded()); lightLabel.stringValue = sendBrightness(value) ? "已发送 \(value)% · 读取可核对设备值":"USB 未连接，未发送" }
    @objc private func change(_ sender:NSControl) {
        let chosen = Self.selections[mode.indexOfSelectedItem], pages = order.filter { checks[$0]?.state == .on }
        guard !pages.isEmpty else { (sender as? NSButton)?.state = .on; status.stringValue = "至少保留一个轮播页面。"; return }
        policy = .init(selectedMode:chosen.1,cycleEnabled:chosen.2,intervalSeconds:[10,15,30,60][interval.indexOfSelectedItem],pages:pages)
        apply(policy); rebuild()
    }
    private func rebuild() {
        rows.arrangedSubviews.forEach { rows.removeArrangedSubview($0); $0.removeFromSuperview() }
        let cycling = mode.indexOfSelectedItem == 0; interval.isEnabled = cycling
        for (i,id) in order.enumerated() {
            guard let check = checks[id] else { continue }; check.removeFromSuperview(); check.isEnabled = cycling
            check.setContentHuggingPriority(.defaultLow,for:.horizontal)
            let up = MacSettingsButton("↑") { [weak self] in self?.move(id,-1) }, down = MacSettingsButton("↓") { [weak self] in self?.move(id,1) }
            up.setAccessibilityLabel("上移"+check.title); down.setAccessibilityLabel("下移"+check.title); up.isEnabled = cycling && i > 0; down.isEnabled = cycling && i + 1 < order.count
            let row = MacBridgeSettingsStyle.row([check,up,down]); rows.addArrangedSubview(row); row.widthAnchor.constraint(equalTo:rows.widthAnchor).isActive = true
        }
        status.stringValue = cycling ? "轮播已选 \(policy.pages.count) 页 · 自动保存":"轮播设置已保留，选择“自动轮播”后启用。"
    }
    private func move(_ id:String,_ delta:Int) { guard let i = order.firstIndex(of:id),order.indices.contains(i+delta) else { return }; order.swapAt(i,i+delta); change(mode) }
}
