import Foundation

struct MacAgendaItem: Codable, Equatable {
    var id: String, edit: String, title: String
    var due: Int64, end: Int64
    var `repeat`: Int, lead: Int
    var done: Int64, snoozeOccurrence: Int64, snoozeUntil: Int64
    init(id: String = MacProfileValidation.id(), edit: String = MacProfileValidation.id(), title: String,
         due: Int64 = 0, end: Int64 = 0, repeat rule: Int = 0, lead: Int = 0,
         done: Int64 = 0, snoozeOccurrence: Int64 = 0, snoozeUntil: Int64 = 0) {
        self.id = id; self.edit = edit; self.title = title; self.due = due; self.end = end; self.repeat = rule
        self.lead = lead; self.done = done; self.snoozeOccurrence = snoozeOccurrence; self.snoozeUntil = snoozeUntil
    }
    enum CodingKeys: String, CodingKey { case id, edit, title, due, end, `repeat`, lead, done, snoozeOccurrence, snoozeUntil }
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(String.self, forKey: .id); edit = try c.decode(String.self, forKey: .edit)
        title = try c.decode(String.self, forKey: .title); due = try c.decode(Int64.self, forKey: .due)
        end = try c.decodeIfPresent(Int64.self, forKey: .end) ?? 0
        self.repeat = try c.decode(Int.self, forKey: .repeat); lead = try c.decode(Int.self, forKey: .lead)
        done = try c.decodeIfPresent(Int64.self, forKey: .done) ?? 0
        snoozeOccurrence = try c.decodeIfPresent(Int64.self, forKey: .snoozeOccurrence) ?? 0
        snoozeUntil = try c.decodeIfPresent(Int64.self, forKey: .snoozeUntil) ?? 0
    }
    func ends(at occurrence: Int64) -> Int64 { occurrence + (end == 0 ? 0 : end - due) }
    func expired(at now: Int64) -> Bool { self.repeat == 0 && due > 0 && now >= ends(at: due) }
}
struct MacAgendaDocument: Codable, Equatable {
    var version = 1
    var revision = MacProfileValidation.id()
    var items: [MacAgendaItem] = []
}
struct MacAgendaAction: Codable { var id: String, edit: String; var occurrence: Int64; var op: String; var until: Int64? }
struct MacAgendaEdit: Codable { var baseEdit: String?; var item: MacAgendaItem }
struct MacAgendaDeletion: Codable { var id: String, edit: String; var baseEdit: String? }
struct MacAgendaView { var occurrence: Int64, alertAt: Int64; var ended: Bool, calendarPending: Bool }

enum MacAgendaRules {
    static var calendar: Calendar { var c = Calendar(identifier: .gregorian); c.timeZone = .gmt; return c }
    static func civil(_ date: Date, zone: TimeZone = .current) -> Int64 { Int64(date.timeIntervalSince1970) + Int64(zone.secondsFromGMT(for: date)) }
    static func localDate(_ civil: Int64, zone: TimeZone = .current) -> Date? {
        let parts = calendar.dateComponents([.year, .month, .day, .hour, .minute, .second], from: Date(timeIntervalSince1970: Double(civil)))
        var local = calendar; local.timeZone = zone; return local.date(from: parts)
    }
    static func validOccurrence(_ i: MacAgendaItem, _ value: Int64) -> Bool {
        if i.due == 0 { return value == 1 }
        guard value >= i.due, (value - i.due) % 86400 == 0 else { return false }
        switch i.repeat {
        case 0: return value == i.due
        case 1: return true
        case 2: return ![1,7].contains(calendar.component(.weekday, from: Date(timeIntervalSince1970: Double(value))))
        case 3: return (value - i.due) % (7 * 86400) == 0
        case 4: return MacTab5Calendar.isWorkday(civil: value) == true
        default: return false
        }
    }
    static func validate(_ items: [MacAgendaItem]) throws {
        guard items.count <= 16, Set(items.map(\.id)).count == items.count else { throw MacTab5LocalError.invalid("最多保存 16 项，事项标识不能重复。") }
        for i in items {
            guard MacTab5Protocol.isHex(i.id, count: 32), MacTab5Protocol.isHex(i.edit, count: 32),
                  MacProfileValidation.text(i.title, limit: 32), i.title.utf8.count <= 96,
                  (0...4).contains(i.repeat), (0...60).contains(i.lead),
                  i.due == 0 || (946684800...4102444799).contains(i.due),
                  i.due != 0 || (i.repeat == 0 && i.lead == 0 && i.end == 0),
                  i.end == 0 || (i.due > 0 && i.end > i.due && i.end - i.due <= 86400 && i.end <= 4102531199),
                  i.snoozeUntil >= 0 && i.snoozeUntil <= 4105123200 else { throw MacTab5LocalError.invalid("请检查事项名称、开始与结束时间、重复方式和提醒分钟数。") }
            for occurrence in [i.done, i.snoozeOccurrence] where occurrence != 0 {
                let valid = i.repeat == 4 ? occurrence >= i.due && (occurrence - i.due) % 86400 == 0 : validOccurrence(i, occurrence)
                guard valid else { throw MacTab5LocalError.invalid("事项处理状态无效。") }
            }
        }
    }
    static func view(_ i: MacAgendaItem, now: Int64) -> MacAgendaView {
        if i.due == 0 { return .init(occurrence: 1, alertAt: 0, ended: i.done >= 1, calendarPending: false) }
        var occurrence = i.due
        if i.repeat != 0 {
            let end = i.ends(at: i.due)
            occurrence += (now >= end ? (now - end) / 86400 + 1 : 0) * 86400
            occurrence = max(occurrence, i.done > 0 ? i.done + 86400 : 0)
            var found = false
            for _ in 0..<370 {
                if i.repeat == 4 && MacTab5Calendar.isWorkday(civil: occurrence) == nil { break }
                if validOccurrence(i, occurrence) && occurrence > i.done { found = true; break }
                occurrence += 86400
            }
            if !found { return .init(occurrence: i.due, alertAt: .max, ended: false, calendarPending: true) }
        }
        if i.repeat != 0 && i.snoozeOccurrence > i.done && i.snoozeUntil > 0 && i.ends(at: i.snoozeOccurrence) > now { occurrence = i.snoozeOccurrence }
        return .init(occurrence: occurrence, alertAt: i.snoozeOccurrence == occurrence && i.snoozeUntil > 0 ? i.snoozeUntil : occurrence - Int64(i.lead * 60),
                     ended: i.repeat == 0 && (i.done >= occurrence || now >= i.ends(at: occurrence)), calendarPending: false)
    }
}

final class MacAgenda {
    static let shared = MacAgenda()
    let file: MacProfileFile<MacAgendaDocument>
    private let now: () -> Int64
    init(directory: URL? = nil, now: @escaping () -> Int64 = { MacAgendaRules.civil(Date()) }) {
        self.now = now
        file = .init("agenda.json", directory: directory, initial: { .init() }, validate: {
            guard $0.version == 1, MacTab5Protocol.isHex($0.revision, count: 32) else { throw MacTab5LocalError.invalid("日程文件版本无效，原文件已保留。") }
            try MacAgendaRules.validate($0.items)
        })
    }
    func summary() throws -> [String: Any] { let d = try file.read(); return ["version": 1, "revision": d.revision, "count": d.items.count] }
    private func makeRoom(_ items: inout [MacAgendaItem], protecting: Set<String>) -> Bool {
        let excess = items.count - 16; if excess <= 0 { return true }
        let victims = items.filter { !protecting.contains($0.id) && $0.expired(at: now()) }.sorted {
            $0.ends(at: $0.due) == $1.ends(at: $1.due) ? $0.id < $1.id : $0.ends(at: $0.due) < $1.ends(at: $1.due)
        }.prefix(excess)
        guard victims.count == excess else { return false }
        let ids = Set(victims.map(\.id)); items.removeAll { ids.contains($0.id) }; return true
    }
    func save(_ proposed: MacAgendaItem, expected: String?) throws {
        try file.update { d in
            let old = d.items.first { $0.id == proposed.id }
            guard old?.edit == expected else { throw MacTab5LocalError.invalid("事项已在其他窗口修改，请重新读取后编辑。") }
            var item = proposed; item.edit = MacProfileValidation.id(); item.done = 0; item.snoozeOccurrence = 0; item.snoozeUntil = 0
            try MacAgendaRules.validate([item])
            guard old != nil || !item.expired(at: now()) else { throw MacTab5LocalError.invalid("结束时间已过，请选择未来时间。") }
            d.items.removeAll { $0.id == item.id }; d.items.append(item)
            guard makeRoom(&d.items, protecting: [item.id]) else { throw MacTab5LocalError.invalid("已满 16 条，没有可清理的过期事项。") }
            d.revision = MacProfileValidation.id()
        }
    }
    func remove(id: String, edit: String) throws {
        try file.update { d in
            guard d.items.contains(where: { $0.id == id && $0.edit == edit }) else { throw MacTab5LocalError.invalid("事项已变化，请刷新后重试。") }
            d.items.removeAll { $0.id == id }; d.revision = MacProfileValidation.id()
        }
    }
    private func apply(_ action: MacAgendaAction, items: inout [MacAgendaItem]) -> Bool {
        guard let index = items.firstIndex(where: { $0.id == action.id && $0.edit == action.edit }), MacAgendaRules.validOccurrence(items[index], action.occurrence) else { return false }
        var i = items[index]
        if action.op == "complete" {
            if action.occurrence > i.done {
                i.done = action.occurrence
                if i.snoozeOccurrence <= action.occurrence { i.snoozeOccurrence = 0; i.snoozeUntil = 0 }
            }
        } else if action.op == "snooze" {
            if i.done >= action.occurrence || i.due != 0 && now() >= i.ends(at: action.occurrence) { return true }
            guard i.due != 0, let until = action.until, until > 0, until <= now() + 86400,
                  until >= action.occurrence - Int64(i.lead * 60), until < i.ends(at: action.occurrence) else { return false }
            i.snoozeUntil = i.snoozeOccurrence == action.occurrence ? max(i.snoozeUntil, until) : until; i.snoozeOccurrence = action.occurrence
        } else { return false }
        items[index] = i; return true
    }
    func handle(_ root: [String: Any]) -> MacTab5RPCResult {
        guard let op = root["op"] as? String, ["sync", "delete"].contains(op) else { return .error(400, "invalid_agenda_operation") }
        if op == "delete" {
            guard let id = root["id"] as? String, let edit = root["edit"] as? String, MacTab5Protocol.isHex(id, count: 32), MacTab5Protocol.isHex(edit, count: 32) else { return .error(400, "invalid_agenda_delete") }
            do { return try file.update { d in
                if let old = d.items.first(where: { $0.id == id }) {
                    guard old.edit == edit else { return .error(409, "agenda_changed_keep_local") }
                    d.items.removeAll { $0.id == id }; d.revision = MacProfileValidation.id()
                }
                return MacTab5RPCResult(200, ["version": 1, "removed": ["id": id, "edit": edit]])
            } } catch { return .error(503, "agenda_unavailable") }
        }
        let edits: [MacAgendaEdit], deletions: [MacAgendaDeletion], actions: [MacAgendaAction]
        func array<T: Decodable>(_ key: String, limit: Int) throws -> [T] {
            guard let raw = root[key] else { return [] }
            guard let a = raw as? [Any], a.count <= limit else { throw MacTab5Error.invalidPacket }
            return try JSONDecoder().decode([T].self, from: JSONSerialization.data(withJSONObject: a))
        }
        do {
            edits = try array("edits", limit: 16); deletions = try array("deletions", limit: 16); actions = try array("actions", limit: 32)
            try MacAgendaRules.validate(edits.map(\.item))
            func baseValid(_ id: String?) -> Bool { id == nil || id == "" || MacTab5Protocol.isHex(id!, count: 32) }
            guard edits.allSatisfy({ baseValid($0.baseEdit) }), deletions.allSatisfy({ MacTab5Protocol.isHex($0.id, count: 32) && MacTab5Protocol.isHex($0.edit, count: 32) && baseValid($0.baseEdit) }),
                  Set(deletions.map(\.id)).count == deletions.count, actions.allSatisfy({ ["complete", "snooze"].contains($0.op) }),
                  !edits.contains(where: { edit in deletions.contains { $0.id == edit.item.id } }),
                  !actions.contains(where: { action in deletions.contains { $0.id == action.id } }) else { return .error(400, "invalid_agenda_envelope") }
        } catch { return .error(400, "invalid_agenda_envelope") }
        do { return try file.update { document in
            if MacTab5Protocol.integer(root["workdaySupport"]) != 1 && (document.items.contains { $0.repeat == 4 } || edits.contains { $0.item.repeat == 4 }) { return .error(409, "agenda_workdays_requires_update_keep_local") }
            var items = document.items, forks: [[String: String]] = [], conflicts: [String] = []
            func definition(_ a: MacAgendaItem, _ b: MacAgendaItem) -> Bool {
                a.id == b.id && a.edit == b.edit && a.title == b.title && a.due == b.due && a.end == b.end && a.repeat == b.repeat && a.lead == b.lead
            }
            for deletion in deletions {
                if let old = items.first(where: { $0.id == deletion.id }) {
                    items.removeAll { $0.id == old.id }
                    if old.edit != deletion.edit && old.edit != deletion.baseEdit {
                        var copy = old; copy.id = old.edit
                        if let existing = items.first(where: { $0.id == copy.id }), existing != copy { return .error(409, "agenda_delete_conflict_keep_local") }
                        if !items.contains(where: { $0.id == copy.id }) { items.append(copy) }
                        forks.append(["id": old.id, "newId": copy.id])
                    }
                }
                items.removeAll { $0.id == deletion.edit && $0.edit == deletion.edit }
            }
            for change in edits {
                var proposed = change.item; proposed.done = 0; proposed.snoozeOccurrence = 0; proposed.snoozeUntil = 0
                let old = items.first { $0.id == proposed.id }
                if let old, definition(old, proposed) { continue }
                if old?.edit == (change.baseEdit == "" ? nil : change.baseEdit) {
                    items.removeAll { $0.id == proposed.id }; items.append(proposed)
                } else {
                    var copy = proposed; copy.id = proposed.edit
                    if let existing = items.first(where: { $0.id == copy.id }), !definition(existing, copy) { return .error(409, "agenda_copy_conflict_keep_local") }
                    if !items.contains(where: { $0.id == copy.id }) { items.append(copy) }
                    forks.append(["id": proposed.id, "newId": copy.id])
                }
            }
            let protected = Set(edits.flatMap { [$0.item.id, $0.item.edit] } + forks.compactMap { $0["newId"] })
            guard makeRoom(&items, protecting: protected) else { return .error(409, "agenda_full_keep_local") }
            for raw in actions {
                var action = raw
                if let fork = forks.first(where: { $0["id"] == raw.id && $0["newId"] == raw.edit }) { action.id = fork["newId"]! }
                if !apply(action, items: &items) { conflicts.append(action.id) }
            }
            if items != document.items { document.items = items; document.revision = MacProfileValidation.id() }
            let body: [String: Any] = ["version": 1, "editSupport": 1, "deleteSupport": 1, "retentionSupport": 2, "workdaySupport": 1,
                "revision": document.revision, "items": try document.items.map { try MacProfileValidation.object($0) },
                "conflicts": conflicts, "forks": forks, "deleted": deletions.map { ["id": $0.id, "edit": $0.edit] }, "workdayYears": MacTab5Calendar.workdayYears]
            return MacTab5RPCResult(200, body)
        } } catch { return .error(503, "agenda_unavailable") }
    }
}
