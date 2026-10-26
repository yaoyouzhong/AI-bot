import AppKit

/// One draft shared by the collection switches and provider table.
final class MacDeviceDataControls: NSObject {
    let sources: [(String, NSButton)]
    let providers: [(String, NSButton)]
    private let relation = MacBridgeSettingsStyle.note("")
    private var providerLabels: [NSButton] = []
    init(page: MacBridgeSettingsPage, device: MacRegisteredDevice) {
        sources = zip(MacDeviceRegistry.sources.map(\.0), ["会话动态", "模型额度", "天气预报", "股票行情", "音乐信息", "系统状态"]).map { id, title in
            let box = NSButton(checkboxWithTitle: title, target: nil, action: nil); box.state = device.sources.contains(id) ? .on : .off; return (id, box)
        }
        let entries = [("qwen","通义千问","Qwen","套餐额度"),("kimi","月之暗面","Kimi","套餐额度"),("minimax","稀宇科技","MiniMax","套餐额度"),("deepseek","深度求索","DeepSeek","账户余额"),("zhipu","智谱","GLM","额度 / 余额"),("stepfun","阶跃星辰","StepFun","账户余额"),("baidu","百度千帆","Qianfan","资源包余量"),("xiaomi","小米","MiMo","套餐额度")]
        providers = entries.map { id, title, _, _ in let box = NSButton(checkboxWithTitle: title, target: nil, action: nil); box.state = device.providers.contains(id) ? .on : .off; return (id, box) }
        super.init()
        page.section("采集内容")
        let sourceGrid = NSGridView(views: [Array(sources.prefix(3).map(\.1)), Array(sources.suffix(3).map(\.1))]); sourceGrid.rowSpacing = 12; sourceGrid.columnSpacing = 24; addGrid(sourceGrid, widths: [140,140,140], to:page)
        page.section("国产模型"); page.add(relation)
        var rows: [[NSView]] = [[MacBridgeSettingsStyle.note("模型 / 平台"),MacBridgeSettingsStyle.note("英文名称"),MacBridgeSettingsStyle.note("采集内容")]]
        for (index, entry) in entries.enumerated() {
            let box = providers[index].1
            let labels = [entry.2,entry.3].map { title -> NSButton in
                let button = MacSettingsButton(title) { box.state = box.state == .on ? .off : .on }; button.isBordered = false; button.alignment = .left; providerLabels.append(button); return button
            }
            rows.append([box] + labels)
        }
        let grid = NSGridView(views:rows); grid.rowSpacing = 10; grid.columnSpacing = 20; addGrid(grid, widths: [140,130,140], to:page)
        if device.kind == "esp8266" { page.add(MacBridgeSettingsStyle.note("小屏轮播中已选的模型也会采集。")) }
        sources.first { $0.0 == "quotas" }?.1.target = self
        sources.first { $0.0 == "quotas" }?.1.action = #selector(updateQuota)
        updateQuota()
    }
    private func addGrid(_ grid:NSGridView, widths:[CGFloat], to page:MacBridgeSettingsPage) {
        let holder = NSView(); grid.translatesAutoresizingMaskIntoConstraints = false; holder.addSubview(grid)
        for (index,width) in widths.enumerated() { grid.column(at:index).width = width }
        NSLayoutConstraint.activate([grid.leadingAnchor.constraint(equalTo:holder.leadingAnchor),grid.topAnchor.constraint(equalTo:holder.topAnchor),grid.bottomAnchor.constraint(equalTo:holder.bottomAnchor),grid.trailingAnchor.constraint(lessThanOrEqualTo:holder.trailingAnchor)])
        page.add(holder)
    }
    @objc private func updateQuota() {
        let enabled = sources.first { $0.0 == "quotas" }?.1.state == .on
        for button in providers.map(\.1) + providerLabels { button.isEnabled = enabled }
        relation.stringValue = enabled ? "Codex / Claude 额度根据本机官方客户端授权采集。" : "勾选“模型额度”后可选择；已选模型保留。"
    }
}
