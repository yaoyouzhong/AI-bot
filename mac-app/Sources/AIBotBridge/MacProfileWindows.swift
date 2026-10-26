import AppKit

final class MacBirthdayWindow: NSWindowController {
    private let store: MacBirthdays
    private let list = MacSettingsTable(["姓名", "生日与提醒"])
    private let apply = MacSettingsButton("添加到列表") {}
    private let name = NSTextField(), month = MacSettingsControls.popup((1...12).map { "\($0) 月" })
    private let day = MacSettingsControls.popup((1...31).map { "\($0) 日" }), calendar = MacSettingsControls.popup(["公历", "农历"])
    private let leap = NSButton(checkboxWithTitle: "闰月（当年无闰月时按普通月）", target: nil, action: nil)
    private let remind = MacSettingsControls.popup((0...30).map { $0 == 0 ? "当天提醒" : "提前 \($0) 天" })
    private let status = MacBridgeSettingsStyle.note("")
    private var draft: [MacBirthday] = [], original: [MacBirthday] = []
    private var editing: Int?
    private var draftGuard:MacSettingsDraft?
    init(store: MacBirthdays = .shared) {
        self.store = store
        super.init(window: MacBridgeSettingsStyle.window("日历与生日", width: 780, height: 600))
        window?.contentMinSize = NSSize(width: 720, height: 480)
        let left = MacBridgeSettingsPage(), right = MacBridgeSettingsPage()
        left.heading("日历生日", subtitle: "最多 32 位；保存后同步至 TAB5。")
        left.add(list); list.heightAnchor.constraint(equalToConstant: 330).isActive = true
        left.add(MacBridgeSettingsStyle.row([MacSettingsButton("新增") { [weak self] in self?.beginNew() }, MacSettingsButton("移除") { [weak self] in self?.remove() }]))
        right.heading("生日信息"); right.field("姓名", name); right.field("历法", calendar)
        right.add(MacBridgeSettingsStyle.row([month, day])); right.add(leap); right.field("提醒", remind)
        apply.target = self; apply.action = #selector(applyDraft); right.add(apply)
        calendar.target = self; calendar.action = #selector(calendarChanged)
        right.add(MacBridgeSettingsStyle.note("日期与姓名只在本机保存，并通过已配对的加密连接同步。公历 2 月 29 日和农历小月末日按该月最后一天显示。"))
        window?.contentView = MacBridgeSettingsShell(body: MacSettingsControls.columns(left, right), status: status, actions: [MacSettingsButton("保存并同步") { [weak self] in self?.save() }, MacSettingsButton("取消") { [weak self] in self?.close() }])
        list.selected = { [weak self] index in self?.select(index) }
        reload()
        name.identifier = .init("birthday-name"); calendar.identifier = .init("birthday-calendar"); remind.identifier = .init("birthday-remind")
        draftGuard = MacSettingsDraft(window) { [weak self] in guard let self else { return "" }; let value = MacBirthday(name:self.name.stringValue.trimmingCharacters(in:.whitespacesAndNewlines),month:self.month.indexOfSelectedItem+1,day:self.day.indexOfSelectedItem+1,lunar:self.calendar.indexOfSelectedItem == 1,leap:self.leap.state == .on,remind:self.remind.indexOfSelectedItem)
            let pending = !value.name.isEmpty && (self.editing.flatMap { self.draft.indices.contains($0) ? self.draft[$0]:nil } != value)
            return String(describing:self.draft) + String(pending) }
    }
    required init?(coder: NSCoder) { fatalError() }
    func open() { if window?.isVisible != true { reload() }; MacSettingsControls.open(self) }
    private func reload() { do { original = try store.file.read(); draft = original; refresh(); beginNew(); status.stringValue = ""; draftGuard?.markSaved() } catch { status.stringValue = error.localizedDescription } }
    private func refresh() { list.rows = draft.map { [$0.name, "\($0.lunar ? "农历" : "公历")\($0.leap ? "闰" : "") \($0.month)/\($0.day) · 提前 \($0.remind) 天"] } }
    private func beginNew() { editing = nil; list.select(-1); name.stringValue = ""; calendar.selectItem(at: 0); month.selectItem(at: 0); day.selectItem(at: 0); remind.selectItem(at: 3); leap.state = .off; calendarChanged(); apply.title = "添加到列表" }
    private func select(_ index: Int) { guard draft.indices.contains(index) else { return }; editing = index; let v = draft[index]; name.stringValue = v.name; calendar.selectItem(at: v.lunar ? 1 : 0); month.selectItem(at: v.month - 1); day.selectItem(at: v.day - 1); remind.selectItem(at: v.remind); leap.state = v.leap ? .on : .off; calendarChanged(); apply.title = "更新所选" }
    @objc private func calendarChanged() { leap.isEnabled = calendar.indexOfSelectedItem == 1; if !leap.isEnabled { leap.state = .off } }
    @objc private func applyDraft() { updateDraft() }
    @discardableResult private func updateDraft() -> Bool {
        window?.makeFirstResponder(nil)
        let value = MacBirthday(name: name.stringValue.trimmingCharacters(in: .whitespacesAndNewlines), month: month.indexOfSelectedItem + 1, day: day.indexOfSelectedItem + 1, lunar: calendar.indexOfSelectedItem == 1, leap: leap.state == .on, remind: remind.indexOfSelectedItem)
        do { var values = draft; if let editing, values.indices.contains(editing) { values[editing] = value } else { values.append(value) }; try MacBirthdays.validate(values); draft = values; let row = editing ?? values.count - 1; refresh(); list.select(row); select(row); status.stringValue = "列表已更新，点击保存并同步后生效。"; return true }
        catch { status.stringValue = error.localizedDescription; return false }
    }
    private func remove() { guard draft.indices.contains(list.selectedRow) else { return }; draft.remove(at: list.selectedRow); refresh(); beginNew() }
    private func save() {
        if !name.stringValue.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !updateDraft() { return }
        do { try store.file.update { values in guard values == original else { throw MacTab5LocalError.invalid("生日列表已变化，请重新打开后编辑。") }; values = draft }; close() }
        catch { status.stringValue = error.localizedDescription }
    }
}

final class MacAgendaWindow: NSWindowController {
    private let store: MacAgenda
    private let list = MacSettingsTable(["事项", "状态"])
    private let filter = MacSettingsControls.popup(["今日待办", "全部事项"])
    private let name = NSTextField(), timed = NSButton(checkboxWithTitle: "设置时间", target: nil, action: nil)
    private let start = MacSettingsControls.datePicker(), end = MacSettingsControls.datePicker()
    private let repeats = MacSettingsControls.popup(["不重复", "每天", "周一至周五", "每周", "工作日（含调休）"])
    private let lead = MacSettingsControls.popup(["准时提醒", "提前 1 分钟", "提前 3 分钟", "提前 5 分钟", "提前 10 分钟", "提前 15 分钟", "提前 30 分钟", "提前 60 分钟"])
    private let leads = [0,1,3,5,10,15,30,60]
    private let status = MacBridgeSettingsStyle.note("")
    private var draftGuard:MacSettingsDraft?
    private var items: [MacAgendaItem] = [], editing: MacAgendaItem?, timer: Timer?
    init(store: MacAgenda = .shared) {
        self.store = store; super.init(window: MacBridgeSettingsStyle.window("日程待办", width: 760, height: 590))
        window?.contentMinSize = NSSize(width: 700, height: 500)
        let left = MacBridgeSettingsPage(), right = MacBridgeSettingsPage()
        left.heading("日程待办", subtitle: "电脑与 TAB5 双向同步，离线修改会在重连后合并。")
        filter.selectItem(at: 1); left.add(filter); filter.target = self; filter.action = #selector(refresh)
        left.add(list); list.heightAnchor.constraint(equalToConstant: 340).isActive = true
        left.add(MacBridgeSettingsStyle.row([MacSettingsButton("移除") { [weak self] in self?.remove() }, MacSettingsButton("稍后 10 分钟") { [weak self] in self?.snooze() }]))
        right.heading("事项信息"); right.add(MacSettingsButton("新建事项") { [weak self] in self?.beginNew() })
        right.field("事项名称", name); right.add(timed); timed.target = self; timed.action = #selector(updateTimed)
        right.field("开始", start); right.field("结束", end); right.field("重复", repeats); right.field("提醒", lead)
        right.add(MacSettingsButton("保存事项") { [weak self] in self?.save() })
        start.target = self; start.action = #selector(startChanged)
        window?.contentView = MacBridgeSettingsShell(body: MacSettingsControls.columns(left, right, leftWidth: 310), status: status, actions: [MacSettingsButton("刷新") { [weak self] in self?.refresh() }, MacSettingsButton("关闭") { [weak self] in self?.close() }])
        list.selected = { [weak self] index in self?.select(index) }
        refresh(); beginNew()
        draftGuard = MacSettingsDraft(window) { [weak self] in guard let self else { return "" }; return MacSettingsDraft.values([self.name,self.timed,self.start,self.end,self.repeats,self.lead]) }
        timer = Timer.scheduledTimer(withTimeInterval: 10, repeats: true) { [weak self] _ in self?.refresh() }
    }
    required init?(coder: NSCoder) { fatalError() }
    deinit { timer?.invalidate() }
    func open() { if window?.isVisible != true { beginNew(); status.stringValue = "" }; refresh(); MacSettingsControls.open(self) }
    @objc private func startChanged() { if end.dateValue <= start.dateValue { end.dateValue = start.dateValue.addingTimeInterval(3600) } }
    @objc private func updateTimed() { for control in [start, end, repeats, lead] as [NSControl] { control.isEnabled = timed.state == .on } }
    @objc private func refresh() {
        do {
            let now = MacAgendaRules.civil(Date())
            items = try store.file.read().items.filter { item in let v = MacAgendaRules.view(item, now: now); return filter.indexOfSelectedItem == 1 || !v.ended && (item.due == 0 || v.occurrence / 86400 <= now / 86400) }.sorted { a, b in
                let av = MacAgendaRules.view(a, now: now), bv = MacAgendaRules.view(b, now: now)
                if av.ended != bv.ended { return !av.ended }
                let ak = a.due == 0 ? Int64.max : av.ended ? -av.occurrence : av.occurrence
                let bk = b.due == 0 ? Int64.max : bv.ended ? -bv.occurrence : bv.occurrence
                return ak == bk ? a.id < b.id : ak < bk
            }
            let previous = editing
            list.rows = items.map { i in
                let v = MacAgendaRules.view(i, now: now)
                let date = i.due == 0 ? "无时间" : (MacAgendaRules.localDate(v.occurrence).map { DateFormatter.localizedString(from: $0, dateStyle: .short, timeStyle: .short) } ?? "")
                return [i.title, v.calendarPending ? "等待节假日安排" : (v.ended ? "已结束 · " : "") + date]
            }
            if let previous, let row = items.firstIndex(where: { $0.id == previous.id && $0.edit == previous.edit }) {
                // Refreshing the clock never replaces an unsaved editor draft.
                let handler = list.selected; list.selected = nil; list.select(row); list.selected = handler
            }
        } catch { status.stringValue = error.localizedDescription }
    }
    private func beginNew() { editing = nil; list.select(-1); name.stringValue = ""; timed.state = .on; start.dateValue = Date().addingTimeInterval(3600); end.dateValue = start.dateValue.addingTimeInterval(3600); repeats.selectItem(at: 0); lead.selectItem(at: 0); updateTimed(); draftGuard?.markSaved() }
    private func select(_ index: Int) { guard items.indices.contains(index) else { return }; let i = items[index]; editing = i; name.stringValue = i.title; timed.state = i.due == 0 ? .off : .on; start.dateValue = MacAgendaRules.localDate(i.due) ?? Date(); end.dateValue = MacAgendaRules.localDate(i.end > 0 ? i.end : i.due + 3600) ?? Date(); repeats.selectItem(at: i.repeat); lead.selectItem(at: leads.firstIndex(of: i.lead) ?? 0); updateTimed(); draftGuard?.markSaved() }
    private func save() {
        window?.makeFirstResponder(nil)
        do {
            let due = timed.state == .on ? MacAgendaRules.civil(start.dateValue) / 60 * 60 : 0
            let ends = timed.state == .on ? MacAgendaRules.civil(end.dateValue) / 60 * 60 : 0
            if timed.state == .on && ends <= due { throw MacTab5LocalError.invalid("结束时间须晚于开始时间，最长 24 小时。") }
            let item = MacAgendaItem(id: editing?.id ?? MacProfileValidation.id(), title: name.stringValue.trimmingCharacters(in: .whitespacesAndNewlines), due: due, end: ends, repeat: due == 0 ? 0 : repeats.indexOfSelectedItem, lead: due == 0 ? 0 : leads[lead.indexOfSelectedItem])
            try store.save(item, expected: editing?.edit); beginNew(); refresh(); status.stringValue = "已保存；TAB5 连接后自动同步。满 16 条新增时清理最早结束的过期事项。"
        } catch { status.stringValue = error.localizedDescription }
    }
    private func remove() { guard let editing else { return }; let alert = NSAlert(); alert.messageText = "删除事项"; alert.informativeText = "删除“\(editing.title)”？"; alert.addButton(withTitle: "删除"); alert.addButton(withTitle: "取消"); guard alert.runModal() == .alertFirstButtonReturn else { return }; do { try store.remove(id: editing.id, edit: editing.edit); beginNew(); refresh(); status.stringValue = "已删除，重连后同步。" } catch { status.stringValue = error.localizedDescription } }
    private func snooze() {
        guard let editing else { return }; let now = MacAgendaRules.civil(Date()), v = MacAgendaRules.view(editing, now: MacAgendaRules.civil(Date()))
        guard editing.due > 0, !v.ended, !v.calendarPending, v.alertAt <= now, now + 600 < editing.ends(at: v.occurrence) else { status.stringValue = "当前事项尚未提醒、已结束，或剩余时间不足 10 分钟。"; return }
        let result = store.handle(["op": "sync", "workdaySupport": 1, "actions": [["id": editing.id, "edit": editing.edit, "occurrence": v.occurrence, "op": "snooze", "until": now + 600]]])
        status.stringValue = result.status == 200 ? "已设为 10 分钟后再提醒。" : "事项已变化，请刷新后重试。"; refresh()
    }
}

final class MacTaskPinsWindow: NSWindowController {
    private let store: MacTaskPins, codex: MacTab5Codex
    private let page = MacBridgeSettingsPage(), status = MacBridgeSettingsStyle.note("")
    private var choices: [(String, NSButton)] = [], original: [String] = []
    init(codex: MacTab5Codex, store: MacTaskPins = .shared, permissions: @escaping () -> Void) {
        self.codex = codex; self.store = store
        super.init(window: MacBridgeSettingsStyle.window("常用任务", width: 600, height: 470))
        page.heading("常用任务", subtitle: "最多固定 32 个 Codex 任务；固定任务优先出现在 TAB5 列表。")
        do {
            original = try store.read(); var seen = Set<String>()
            for task in codex.taskCatalog() {
                guard let id = task["id"] as? String, seen.insert(id).inserted else { continue }
                let title = (task["folder"] as? String ?? "Codex") + " · " + (task["title"] as? String ?? id)
                let button = NSButton(checkboxWithTitle: title, target: nil, action: nil); button.state = original.contains(id) ? .on : .off
                page.add(button); choices.append((id, button))
            }
            for id in original where !seen.contains(id) {
                let button = NSButton(checkboxWithTitle: "暂未载入 · \(id)", target: nil, action: nil); button.state = .on
                page.add(button); choices.append((id, button))
            }
            if choices.isEmpty { page.add(MacBridgeSettingsStyle.note("暂无本机任务。启用 Codex 会话读取并等待目录加载后重新打开。")) }
        } catch { status.stringValue = error.localizedDescription }
        window?.contentView = MacBridgeSettingsShell(body: page, status: status, actions: [MacSettingsButton("保存") { [weak self] in self?.save() }, MacSettingsButton("会话与权限…", permissions), MacSettingsButton("取消") { [weak self] in self?.close() }])
    }
    required init?(coder: NSCoder) { fatalError() }
    func open() { MacSettingsControls.open(self) }
    private func save() { do { guard try store.read() == original else { throw MacTab5LocalError.invalid("常用任务已改变，请重新打开。") }; try store.save(choices.filter { $0.1.state == .on }.map(\.0)); close() } catch { status.stringValue = error.localizedDescription } }
}
