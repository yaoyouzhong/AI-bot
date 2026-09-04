import Foundation

struct ToolState: Codable, Equatable {
    let state: String
    let ageSeconds: Int64?
}

struct MacStatusSnapshot: Codable {
    let version: Int
    let time: String
    let epochUtc: Int64
    let utcOffsetSeconds: Int
    let capturedAt: Date
    let codex: ToolState
    let claude: ToolState
    let musicPlaying: Bool
}

final class SessionActivityReader {
    private let fileManager = FileManager.default
    private let encoder: JSONEncoder = {
        let value = JSONEncoder()
        value.dateEncodingStrategy = .iso8601
        return value
    }()

    func capture() -> MacStatusSnapshot {
        let now = Date()
        return MacStatusSnapshot(
            version: 1,
            time: Self.clock.string(from: now),
            epochUtc: Int64(now.timeIntervalSince1970),
            utcOffsetSeconds: TimeZone.current.secondsFromGMT(for: now),
            capturedAt: now,
            codex: state(in: home(".codex/sessions"), now: now),
            claude: state(in: home(".claude/projects"), now: now),
            musicPlaying: false
        )
    }

    func json() -> Data {
        (try? encoder.encode(capture())) ?? Data("{\"version\":1}".utf8)
    }

    private func state(in root: URL, now: Date) -> ToolState {
        guard let enumerator = fileManager.enumerator(
            at: root,
            includingPropertiesForKeys: [.contentModificationDateKey, .isRegularFileKey],
            options: [.skipsHiddenFiles]
        ) else { return ToolState(state: "offline", ageSeconds: nil) }

        var newest: Date?
        for case let file as URL in enumerator where file.pathExtension == "jsonl" {
            guard let values = try? file.resourceValues(forKeys: [.contentModificationDateKey, .isRegularFileKey]),
                  values.isRegularFile == true,
                  let modified = values.contentModificationDate else { continue }
            if newest == nil || modified > newest! { newest = modified }
        }
        guard let newest else { return ToolState(state: "offline", ageSeconds: nil) }
        let age = max(0, Int64(now.timeIntervalSince(newest)))
        return ToolState(state: Self.classify(ageSeconds: age), ageSeconds: age)
    }

    static func classify(ageSeconds: Int64) -> String {
        if ageSeconds < 90 { return "working" }
        if ageSeconds <= 900 { return "idle" }
        return "offline"
    }

    private func home(_ relativePath: String) -> URL {
        fileManager.homeDirectoryForCurrentUser.appendingPathComponent(relativePath)
    }

    private static let clock: DateFormatter = {
        let value = DateFormatter()
        value.locale = Locale(identifier: "en_US_POSIX")
        value.dateFormat = "HH:mm:ss"
        return value
    }()
}
