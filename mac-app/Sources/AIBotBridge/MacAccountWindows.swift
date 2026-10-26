import AppKit
import CoreFoundation

struct MacStockSearchResult: Equatable {
    let symbol: String, name: String, pinyin: String
    var market: String { ["sh":"沪市", "sz":"深市", "bj":"北交所", "hk":"港股", "us":"美股"][String(symbol.prefix(2))] ?? "" }
}
enum MacStockSearch {
    static func parse(_ text: String) throws -> [MacStockSearchResult] {
        let expression = try NSRegularExpression(pattern: #"^\s*v_hint\s*=\s*("(?:[^"\\]|\\.)*")\s*;?\s*$"#, options: [.dotMatchesLineSeparators])
        guard let m = expression.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)), let range = Range(m.range(at: 1), in: text), let encoded = String(text[range]).data(using: .utf8), let payload = try? JSONDecoder().decode(String.self, from: encoded) else { throw MacTab5LocalError.invalid("无法识别搜索服务的响应。") }
        var results: [MacStockSearchResult] = [], seen = Set<String>()
        for entry in payload.split(separator: "^") {
            let fields = entry.components(separatedBy: "~")
            guard fields.count >= 5, ["GP", "GP-A", "ZS", "ETF", "FJ", "LOF"].contains(fields[4]), MacProfileValidation.text(fields[2], limit: 100) else { continue }
            let code = fields[0] == "us" ? fields[1].replacingOccurrences(of: #"\.(oq|n|ps|am)$"#, with: "", options: [.regularExpression, .caseInsensitive]) : fields[1]
            guard let symbol = MacDataService.normalizeStock(fields[0] + code), seen.insert(symbol).inserted else { continue }
            results.append(.init(symbol: symbol, name: fields[2], pinyin: fields[3])); if results.count == 20 { break }
        }
        return results
    }
    static func search(_ query: String) async throws -> [MacStockSearchResult] {
        let query = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard MacProfileValidation.text(query, limit: 40) else { throw MacTab5LocalError.invalid("请输入不超过 40 个字符的代码、名称或拼音。") }
        var url = URLComponents(string: "https://smartbox.gtimg.cn/s3/")!; url.queryItems = [.init(name: "q", value: query), .init(name: "t", value: "all")]
        var request = URLRequest(url: url.url!); request.timeoutInterval = 8
        let (bytes, response) = try await URLSession.shared.bytes(for: request)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw MacTab5LocalError.invalid("股票搜索暂不可用。") }
        var data = Data(); for try await byte in bytes { guard data.count < 65_536 else { throw MacTab5LocalError.invalid("搜索响应过大，请缩小查询范围。") }; data.append(byte) }
        let encoding = String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
        guard let text = String(data: data, encoding: encoding) else { throw MacTab5LocalError.invalid("搜索响应编码无效。") }
        return try parse(text)
    }
}

final class MacStocksWindow: NSWindowController, NSTextFieldDelegate {
    private let query = NSTextField(), batch = NSTextField()
    private let market = MacSettingsControls.popup(["全部市场", "沪市", "深市", "北交所", "港股", "美股"])
    private let results = MacSettingsTable(["名称", "市场", "代码"]), selected = MacSettingsTable(["名称", "市场", "代码"])
    private let status = MacBridgeSettingsStyle.note("")
    private var matches: [MacStockSearchResult] = [], shown: [MacStockSearchResult] = [], symbols: [String] = []
    private var search: Task<Void, Never>?, generation = 0
    private var debounce: Timer?, names: [String:String] = [:]
    private var draftGuard:MacSettingsDraft?
    private let didSave: () -> Void
    init(quotes:[StockQuote] = [], didSave: @escaping () -> Void) {
        names = Dictionary(quotes.map { ($0.symbol,$0.name) },uniquingKeysWith:{ first,_ in first })
        self.didSave = didSave; symbols = MacDataPreferences.load().symbols
        super.init(window: MacBridgeSettingsStyle.window("自选股票", width: 720, height: 680))
        window?.contentMinSize = NSSize(width: 620, height: 520)
        let page = MacBridgeSettingsPage(); page.heading("自选股票",subtitle:"搜索代码、名称、拼音及指数、ETF；所有设备共用此列表。")
        query.placeholderString = "代码 / 中文 / 拼音"; query.delegate = self; query.target = self; query.action = #selector(searchNow)
        market.target = self; market.action = #selector(filter)
        let entry = MacBridgeSettingsStyle.row([market,query,MacSettingsButton("搜索") { [weak self] in self?.performSearch() }]); query.setContentHuggingPriority(.defaultLow,for:.horizontal); page.add(entry)
        page.add(results); results.heightAnchor.constraint(equalToConstant:180).isActive = true
        results.table.target = self; results.table.doubleAction = #selector(pick)
        page.add(MacSettingsButton("加入自选") { [weak self] in self?.addSelected() })
        page.section("自选列表"); page.add(selected); selected.heightAnchor.constraint(equalToConstant:220).isActive = true
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("移除所选") { [weak self] in self?.remove() },MacSettingsButton("上移") { [weak self] in self?.move(-1) },MacSettingsButton("下移") { [weak self] in self?.move(1) },MacSettingsButton("批量添加代码…") { [weak self] in self?.showBatch() }]))
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[MacSettingsButton("保存") { [weak self] in self?.save() },MacSettingsButton("取消") { [weak self] in self?.close() }])
        refreshSelection()
        draftGuard = MacSettingsDraft(window) { [weak self] in self?.symbols.joined(separator:",") ?? "" }
    }
    required init?(coder: NSCoder) { fatalError() }
    deinit { search?.cancel(); debounce?.invalidate() }
    func open() { MacSettingsControls.open(self) }
    func controlTextDidChange(_ notification:Notification) {
        debounce?.invalidate(); search?.cancel(); generation += 1
        guard !query.stringValue.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty else { matches = []; filter(); return }
        debounce = Timer.scheduledTimer(withTimeInterval:0.35,repeats:false) { [weak self] _ in self?.performSearch() }
    }
    @objc private func searchNow() { performSearch() }
    @objc private func pick() { addSelected() }
    private func showBatch() {
        let form = MacBridgeSettingsForm("批量添加代码",subtitle:"用逗号、分号或空格分隔，最多 20 个。",height:380)
        batch.stringValue = ""; batch.placeholderString = "sh000001, hk00700, usAAPL"; form.page.field("股票代码",batch)
        if form.run() { addBatch() }
    }
    private func performSearch() {
        debounce?.invalidate(); search?.cancel(); generation += 1; let g = generation, text = query.stringValue
        status.stringValue = "正在搜索…"
        search = Task { [weak self] in
            do { let result = try await MacStockSearch.search(text); guard !Task.isCancelled else { return }; await MainActor.run { guard let self, self.generation == g else { return }; self.matches = result; for item in result { self.names[item.symbol] = item.name }; self.filter(); self.status.stringValue = "找到 \(result.count) 项，可选择添加。" } }
            catch { guard !Task.isCancelled else { return }; await MainActor.run { guard let self, self.generation == g else { return }; self.status.stringValue = "搜索失败，已有自选列表保留。" } }
        }
    }
    @objc private func filter() { shown = matches.filter { market.indexOfSelectedItem == 0 || $0.market == market.titleOfSelectedItem }; results.rows = shown.map { [$0.name, $0.market, $0.symbol] } }
    private func refreshSelection() { selected.rows = symbols.map { symbol in [names[symbol] ?? symbol,MacStockSearchResult(symbol:symbol,name:"",pinyin:"").market,symbol] }; status.stringValue = "已选 \(symbols.count) / 20 项。" }
    private func add(_ values: [String]) { var draft = symbols; for value in values where !draft.contains(value) { draft.append(value) }; guard draft.count <= 20 else { status.stringValue = "最多选择 20 项。"; return }; symbols = draft; refreshSelection() }
    private func addSelected() { guard shown.indices.contains(results.selectedRow) else { return }; add([shown[results.selectedRow].symbol]); query.stringValue = "" }
    private func addBatch() { let parts = batch.stringValue.components(separatedBy: CharacterSet(charactersIn: ",，;； \n\t")).filter { !$0.isEmpty }; let values = parts.compactMap(MacDataService.normalizeStock); guard values.count == parts.count else { status.stringValue = "代码需要 sh、sz、bj、hk 或 us 市场前缀。"; return }; add(values) }
    private func remove() { guard symbols.indices.contains(selected.selectedRow) else { return }; symbols.remove(at: selected.selectedRow); refreshSelection() }
    private func move(_ direction: Int) { let from = selected.selectedRow, to = from + direction; guard symbols.indices.contains(from), symbols.indices.contains(to) else { return }; symbols.swapAt(from, to); refreshSelection(); selected.select(to) }
    private func save() { UserDefaults.standard.set(symbols, forKey: "stock_symbols"); didSave(); close() }
}

private final class MacQuotaChart: NSView {
    var days: [MacQuotaTrend.Day] = [] { didSet { needsDisplay = true } }
    var points = false
    override func draw(_ dirtyRect: NSRect) {
        NSColor.controlBackgroundColor.setFill(); bounds.fill()
        let maxValue = points ? 100 : max(1, days.compactMap(\.used).max() ?? 1), width = (bounds.width - 24) / CGFloat(max(1, days.count))
        let line = NSBezierPath(); var started = false
        for (index, day) in days.enumerated() {
            let x = 12 + CGFloat(index) * width
            if let value = day.used {
                if points {
                    let p = NSPoint(x:x + width/2,y:30 + CGFloat(value / maxValue) * (bounds.height - 62))
                    if started { line.line(to:p) } else { line.move(to:p); started = true }
                } else {
                    (day.partial ? NSColor.systemOrange : NSColor.controlAccentColor).setFill()
                    NSBezierPath(rect: NSRect(x: x + 2, y: 30, width: max(1, width - 4), height: max(1, CGFloat(value / maxValue) * (bounds.height - 62)))).fill()
                }
            } else { NSColor.separatorColor.setFill(); NSRect(x: x + 2, y: 30, width: max(1, width - 4), height: 2).fill() }
            if days.count <= 7 || index % max(1,days.count/6) == 0 { (day.label as NSString).draw(at: NSPoint(x: x, y: 10), withAttributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]) }
        }
        if points { NSColor.controlAccentColor.setStroke(); line.lineWidth = 2; line.stroke() }
    }
}
final class MacQuotaHistoryWindow: NSWindowController {
    private let store: MacDataStore
    private let days = MacSettingsControls.popup(["最近 7 天", "最近 30 天"]), chart = MacQuotaChart()
    private let metric = MacSettingsControls.popup(["每日已记录用量（周额度）","五小时已用比例"])
    private var timer: Timer?
    private var displayedDays: [MacQuotaTrend.Day] = []
    private let detail = MacBridgeSettingsStyle.note("选择日期查看采样范围。")
    private let table = MacSettingsTable(["日期（北京时间）", "观测额度增量", "完整性"]), status = MacBridgeSettingsStyle.note("")
    init(store: MacDataStore) {
        self.store = store; super.init(window: MacBridgeSettingsStyle.window("额度历史", width: 820, height: 700))
        let page = MacBridgeSettingsPage(); page.heading("额度历史", subtitle: "Codex 账户额度历史。缺失值不记作零；橙色表示采样不完整。")
        page.add(MacBridgeSettingsStyle.row([metric,days])); days.target = self; days.action = #selector(refresh); metric.target = self; metric.action = #selector(refresh)
        page.add(chart); chart.heightAnchor.constraint(equalToConstant: 200).isActive = true
        page.add(table); table.heightAnchor.constraint(equalToConstant: 300).isActive = true
        page.add(detail); table.selected = { [weak self] index in self?.showDetail(index) }
        window?.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [MacSettingsButton("刷新") { [weak self] in self?.refresh() }]); refresh()
        timer = Timer.scheduledTimer(withTimeInterval:30,repeats:true) { [weak self] _ in if self?.window?.isVisible == true { self?.refresh() } }
    }
    deinit { timer?.invalidate() }
    required init?(coder: NSCoder) { fatalError() }
    func open() { refresh(); MacSettingsControls.open(self) }
    @objc private func refresh() {
        let count = days.indexOfSelectedItem == 0 ? 7 : 30
        chart.points = metric.indexOfSelectedItem == 1
        if chart.points {
            displayedDays = []; detail.stringValue = "每小时最后一个有效采样。"
            let values = store.hourlyHistory(days:count), formatter = DateFormatter(); formatter.dateFormat = "MM-dd HH:mm"; formatter.timeZone = TimeZone(identifier:"Asia/Shanghai")
            chart.days = values.map { .init(date:formatter.string(from:$0.at),label:formatter.string(from:$0.at),used:$0.primary,partial:false) }
            for (column,title) in zip(table.table.tableColumns,["采样时间（北京时间）","五小时已用（%）","重置时间"]) { column.title = title }
            table.rows = values.reversed().map { [formatter.string(from:$0.at),$0.primary.map { String(format:"%.2f",$0) } ?? "—",$0.primaryReset.map(formatter.string(from:)) ?? "—"] }
            status.stringValue = "每小时最后一个已用比例快照，非每日消耗。新版开始记录；旧历史缺少此字段时保持未知。"; return
        }
        guard let value = store.history(days:count) else { status.stringValue = "尚无可用历史。"; return }
        for (column,title) in zip(table.table.tableColumns,["日期（北京时间）","观测额度增量","完整性"]) { column.title = title }
        chart.days = value.days; displayedDays = value.days.reversed()
        table.rows = displayedDays.map { [$0.date, $0.used.map { String(format: "%.2f", $0) } ?? "—", ($0.carryOver ?? 0) > 0 ? String(format:"含跨日补记 %.2f",$0.carryOver!) : ($0.partial ? "部分采样" : "完整采样")] }
        showDetail(table.selectedRow)
        let complete = value.days.filter { !$0.partial && $0.used != nil }
        let average = complete.isEmpty ? "暂无完整统计日" : String(format:"完整日均 %.2f（%d 天）",complete.compactMap(\.used).reduce(0,+)/Double(complete.count),complete.count)
        status.stringValue = value.storageWarning ? "历史文件读取或保存异常，原文件已保留。" : "单位：周额度百分点；\(average)。本机日志 token 数不参与统计。"
    }
    private func showDetail(_ index: Int) {
        guard displayedDays.indices.contains(index) else { detail.stringValue = "选择日期查看首末采样、重置和采样间断。跨日补记计入下一次观测日，并标记为部分采样。"; return }
        let day = displayedDays[index]
        detail.stringValue = "采样 \(day.samples) 次；重置 \(day.resets) 次；未确认变化 \(day.uncertainResets) 次；间断 \(day.gaps) 次。\n" + (day.coverage ?? "采样范围未知")
    }
}
