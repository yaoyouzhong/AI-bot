import Foundation

struct ToolState: Codable, Equatable {
    let state: String
    let ageSeconds: Int64?
    var needsInput: Bool = false
    var completionActive: Bool = false
    var completionSequence: Int64 = 0
    var completionAt: Int64 = 0
    var tokensToday: Int64 = 0
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

struct ProviderQuotaSnapshot: Codable, Equatable {
    let provider: String
    let plan: String?
    let primaryPercent: Double?
    let primaryResetsAt: Date?
    let weeklyPercent: Double?
    let weeklyResetsAt: Date?
    let resetCreditsAvailable: Int?
    let resetCreditExpiresAt: [Int64]
    let updatedAt: Date
    let stale: Bool
}

struct QuotaSnapshot: Codable, Equatable {
    let claude: ProviderQuotaSnapshot?
    let codex: ProviderQuotaSnapshot?
}

// Optional display contract; macOS does not acquire vendor quotas yet.
struct DomesticProviderQuotaSnapshot: Codable, Equatable {
    let provider: String
    let plan: String?
    let primaryPercent: Double?
    let primaryResetsAt: Date?
    let weeklyPercent: Double?
    let weeklyResetsAt: Date?
    let balance: Double?
    let usedCost: Double?
    let currency: String?
    let updatedAt: Date
    let stale: Bool
    var planPercent: Double? = nil
    var planResetsAt: Date? = nil
}

struct DomesticQuotaSnapshot: Codable, Equatable {
    let alibaba: DomesticProviderQuotaSnapshot?
    let kimi: DomesticProviderQuotaSnapshot?
    let miniMax: DomesticProviderQuotaSnapshot?
    let deepSeek: DomesticProviderQuotaSnapshot?
    var zhipu: DomesticProviderQuotaSnapshot? = nil
}

struct MusicSnapshot: Codable, Equatable {
    var hasArtwork: Bool? = nil
    let title: String
    let artist: String
    let album: String
    let playing: Bool
    let elapsedSeconds: Double
    let durationSeconds: Double
    let updatedAt: Date

    static func empty(at date: Date = Date()) -> MusicSnapshot {
        MusicSnapshot(title: "", artist: "", album: "", playing: false,
                      elapsedSeconds: 0, durationSeconds: 0, updatedAt: date)
    }
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
    let quotas: QuotaSnapshot?
    let music: MusicSnapshot?
    var displayPolicy: MacDisplayPolicy? = nil
    var domesticActivity: MacDomesticActivity? = nil
    var domesticQuotas: DomesticQuotaSnapshot? = nil
    var followApp: String? = nil
}

final class SessionActivityReader {
    let signals = MacActivitySignals()
    private let metadata = MacActivityMetadata()
    private let lock = NSLock()
    private var selectedMode: String?
    private let follow=MacAutoFollowTracker()
    func select(_ mode: String) { lock.lock(); selectedMode=mode; lock.unlock() }
    private let fileManager = FileManager.default
    private let encoder: JSONEncoder = {
        let value = JSONEncoder()
        value.dateEncodingStrategy = .iso8601
        return value
    }()

    func capture(extras: MacDataExtras = .empty) -> MacStatusSnapshot {
        lock.lock(); defer { lock.unlock() }
        let now = Date()
        let activity = metadata.capture(now: now)
        var domestic=activity.domestic
        var claude=signals.apply("claude",raw:activity.claude,now:now)
        if domestic != nil {
            let routed=signals.apply("claude",raw:ToolState(state:domestic!.state,ageSeconds:nil),now:now)
            domestic?.state=routed.state;domestic?.needsInput=routed.needsInput
            claude=activity.claude
        }
        var snapshot = MacStatusSnapshot(
            version: 1,
            time: Self.clockString(now),
            epochUtc: Int64(now.timeIntervalSince1970),
            utcOffsetSeconds: TimeZone.current.secondsFromGMT(for: now),
            capturedAt: now,
            codex: signals.apply("codex", raw: activity.codex, now: now),
            claude: claude,
            musicPlaying: extras.music?.playing ?? false,
            weather: extras.weather,
            stocks: extras.stocks,
            systemMetrics: extras.systemMetrics,
            quotas: extras.quotas,
            music: extras.music,
            displayPolicy: MacDisplayPolicy.load(selected: selectedMode),
            domesticActivity: domestic
        )
        snapshot.followApp=follow.update(snapshot,milliseconds:Int64(ProcessInfo.processInfo.systemUptime*1000))
        return snapshot
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
