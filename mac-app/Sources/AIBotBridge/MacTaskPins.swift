import Foundation

final class MacTaskPins {
    static let shared = MacTaskPins()
    private let file: MacProfileFile<[String]>
    init(directory: URL? = nil) {
        file = MacProfileFile("tab5-task-pins.json", directory: directory, initial: { [] }) { ids in
            guard ids.count <= 32, Set(ids).count == ids.count, ids.allSatisfy({ UUID(uuidString: $0) != nil }) else {
                throw MacTab5LocalError.invalid("最多选择 32 个常用任务，任务 ID 必须有效且不重复。")
            }
        }
    }
    func read() throws -> [String] { try file.read() }
    func save(_ ids: [String]) throws { try file.write(ids.map { $0.lowercased() }) }
}
