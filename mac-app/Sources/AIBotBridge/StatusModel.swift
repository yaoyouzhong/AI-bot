import Foundation

struct ToolState: Codable, Equatable {
    let state: String
    let ageSeconds: Int64?
}

struct WeatherSnapshot: Codable, Equatable {
    let city: String
    let condition: String
    let temperature: Double
    let high: Double
    let low: Double
    let humidity: Int
    let weatherCode: Int
    let pm25: Double?
    let airQualityIndex: Int?
    let source: String
    let updatedAt: Date
    let stale: Bool
}

struct StockQuote: Codable, Equatable {
    let symbol: String
    let code: String
    let name: String
    let price: String
    let changePercent: String
    let trend: Int
}

struct StockSnapshot: Codable, Equatable {
    let quotes: [StockQuote]
    let updatedAt: Date
    let stale: Bool
}

struct SystemMetricsSnapshot: Codable, Equatable {
    let cpuPercent: Double
    let memoryPercent: Double
    let uploadBytesPerSecond: Int64
    let downloadBytesPerSecond: Int64
    let updatedAt: Date
}

struct MacStatusSnapshot: Codable {
    let version: Int
    let time: String
    let epochUtc: Int64
    let utcOffsetSeconds: Int
    let capturedAt: Date
    let codex: ToolState
    let claude: ToolState
    let musicPlaying: Bool?
    let weather: WeatherSnapshot?
    let stocks: StockSnapshot?
    let systemMetrics: SystemMetricsSnapshot?
}

final class SessionActivityReader {
    private let fileManager = FileManager.default
    private let encoder: JSONEncoder = {
        let value = JSONEncoder()
        value.dateEncodingStrategy = .iso8601
        return value
    }()

    func capture(extras: MacDataExtras = .empty) -> MacStatusSnapshot {
        let now = Date()
        return MacStatusSnapshot(
            version: 1,
            time: Self.clockString(now),
            epochUtc: Int64(now.timeIntervalSince1970),
            utcOffsetSeconds: TimeZone.current.secondsFromGMT(for: now),
            capturedAt: now,
            codex: state(in: home(".codex/sessions"), now: now),
            claude: state(in: home(".claude/projects"), now: now),
            musicPlaying: nil,
            weather: extras.weather,
            stocks: extras.stocks,
            systemMetrics: extras.systemMetrics
        )
    }

    func json(extras: MacDataExtras = .empty) -> Data {
        (try? encoder.encode(capture(extras: extras))) ?? Data("{\"version\":1}".utf8)
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

    private static func clockString(_ date: Date) -> String {
        let values = Calendar.current.dateComponents([.hour, .minute, .second], from: date)
        return String(format: "%02d:%02d:%02d", values.hour ?? 0, values.minute ?? 0, values.second ?? 0)
    }
}
