import CoreFoundation
import Foundation

struct MacDataExtras {
    let weather: WeatherSnapshot?
    let stocks: StockSnapshot?
    let systemMetrics: SystemMetricsSnapshot?
    let quotas: QuotaSnapshot?
    let music: MusicSnapshot?
    let musicCover: Data?

    static let empty = MacDataExtras(weather: nil, stocks: nil, systemMetrics: nil,
                                     quotas: nil, music: MusicSnapshot.empty(), musicCover: nil)
}

struct MacDataPreferences {
    let city: String
    let latitude: Double?
    let longitude: Double?
    let symbols: [String]

    static func load(_ defaults: UserDefaults = .standard) -> MacDataPreferences {
        let city = defaults.string(forKey: "weather_city")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let latitude = defaults.object(forKey: "weather_latitude") == nil
            ? nil : defaults.double(forKey: "weather_latitude")
        let longitude = defaults.object(forKey: "weather_longitude") == nil
            ? nil : defaults.double(forKey: "weather_longitude")
        let configured = defaults.stringArray(forKey: "stock_symbols") ?? ["sh000001"]
        return MacDataPreferences(city: city, latitude: latitude, longitude: longitude,
                                  symbols: configured.compactMap { MacDataService.normalizeStock($0) })
    }

    func save(_ defaults: UserDefaults = .standard) {
        defaults.set(city, forKey: "weather_city")
        if let latitude { defaults.set(latitude, forKey: "weather_latitude") }
        else { defaults.removeObject(forKey: "weather_latitude") }
        if let longitude { defaults.set(longitude, forKey: "weather_longitude") }
        else { defaults.removeObject(forKey: "weather_longitude") }
        defaults.set(symbols, forKey: "stock_symbols")
    }
}

final class MacDataStore {
    private let lock = NSLock()
    private let defaults: UserDefaults
    private var weather: WeatherSnapshot?
    private var stocks: StockSnapshot?
    private var systemMetrics: SystemMetricsSnapshot?
    private var quotas: QuotaSnapshot?
    private var music: MusicSnapshot = .empty()
    private var musicCover: Data?
    private let decoder: JSONDecoder = {
        let value = JSONDecoder()
        value.dateDecodingStrategy = .iso8601
        return value
    }()

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if let data = defaults.data(forKey: "weather_cache"),
           let value = try? decoder.decode(WeatherSnapshot.self, from: data) {
            weather = WeatherSnapshot(city: value.city, condition: value.condition,
                                      temperature: value.temperature, high: value.high, low: value.low,
                                      humidity: value.humidity, weatherCode: value.weatherCode,
                                      pm25: value.pm25, airQualityIndex: value.airQualityIndex,
                                      source: value.source, updatedAt: value.updatedAt, stale: true)
        }
        if let data = defaults.data(forKey: "stock_cache"),
           let value = try? decoder.decode(StockSnapshot.self, from: data) {
            stocks = StockSnapshot(quotes: value.quotes, updatedAt: value.updatedAt, stale: true)
        }
        if let data = defaults.data(forKey: "quota_cache"),
           let value = try? decoder.decode(QuotaSnapshot.self, from: data) {
            quotas = QuotaSnapshot(claude: Self.stale(value.claude), codex: Self.stale(value.codex))
        }
    }

    func snapshot() -> MacDataExtras {
        lock.lock()
        defer { lock.unlock() }
        return MacDataExtras(weather: weather, stocks: stocks, systemMetrics: systemMetrics,
                             quotas: quotas, music: music, musicCover: musicCover)
    }

    func update(weather value: WeatherSnapshot) {
        lock.lock()
        weather = value
        lock.unlock()
        if let data = Self.encode(value) { defaults.set(data, forKey: "weather_cache") }
    }

    func update(stocks value: StockSnapshot) {
        lock.lock()
        stocks = value
        lock.unlock()
        if let data = Self.encode(value) { defaults.set(data, forKey: "stock_cache") }
    }

    func markWeatherStale() {
        lock.lock()
        if let value = weather {
            weather = WeatherSnapshot(city: value.city, condition: value.condition,
                                      temperature: value.temperature, high: value.high, low: value.low,
                                      humidity: value.humidity, weatherCode: value.weatherCode,
                                      pm25: value.pm25, airQualityIndex: value.airQualityIndex,
                                      source: value.source, updatedAt: value.updatedAt, stale: true)
        }
        lock.unlock()
    }

    func markStocksStale() {
        lock.lock()
        if let value = stocks { stocks = StockSnapshot(quotes: value.quotes, updatedAt: value.updatedAt, stale: true) }
        lock.unlock()
    }

    func update(systemMetrics value: SystemMetricsSnapshot) {
        lock.lock()
        systemMetrics = value
        lock.unlock()
    }

    func update(music value: MusicSnapshot) {
        lock.lock()
        if value.title.isEmpty {musicCover=nil}
        var next=value;next.hasArtwork = !value.title.isEmpty && musicCover?.isEmpty == false
        music = next
        lock.unlock()
    }

    func update(music value: MusicSnapshot, cover: Data) {
        lock.lock()
        var next=value;next.hasArtwork = !value.title.isEmpty && !cover.isEmpty
        music = next
        musicCover = cover
        lock.unlock()
    }

    func mergeQuotas(claude: ProviderQuotaSnapshot?, codex: ProviderQuotaSnapshot?) {
        lock.lock()
        let next = QuotaSnapshot(claude: claude ?? Self.stale(quotas?.claude),
                                 codex: Self.mergeCodex(codex, previous: quotas?.codex))
        guard next.claude != nil || next.codex != nil else { lock.unlock(); return }
        quotas = next
        lock.unlock()
        if claude?.stale == false || codex?.stale == false,
           let data = Self.encode(next) { defaults.set(data, forKey: "quota_cache") }
    }

    static func mergeCodex(_ fresh: ProviderQuotaSnapshot?, previous: ProviderQuotaSnapshot?) -> ProviderQuotaSnapshot? {
        guard let fresh else { return stale(previous) }
        guard let previous, (fresh.resetCreditsAvailable ?? 0) > 0,
              fresh.resetCreditsAvailable == previous.resetCreditsAvailable,
              fresh.resetCreditExpiresAt.isEmpty, !previous.resetCreditExpiresAt.isEmpty else { return fresh }
        return ProviderQuotaSnapshot(provider: fresh.provider, plan: fresh.plan,
                                     primaryPercent: fresh.primaryPercent, primaryResetsAt: fresh.primaryResetsAt,
                                     weeklyPercent: fresh.weeklyPercent, weeklyResetsAt: fresh.weeklyResetsAt,
                                     resetCreditsAvailable: fresh.resetCreditsAvailable,
                                     resetCreditExpiresAt: previous.resetCreditExpiresAt,
                                     updatedAt: fresh.updatedAt, stale: true)
    }

    private static func stale(_ value: ProviderQuotaSnapshot?) -> ProviderQuotaSnapshot? {
        guard let value else { return nil }
        return ProviderQuotaSnapshot(provider: value.provider, plan: value.plan,
                                     primaryPercent: value.primaryPercent,
                                     primaryResetsAt: value.primaryResetsAt,
                                     weeklyPercent: value.weeklyPercent,
                                     weeklyResetsAt: value.weeklyResetsAt,
                                     resetCreditsAvailable: value.resetCreditsAvailable,
                                     resetCreditExpiresAt: value.resetCreditExpiresAt,
                                     updatedAt: value.updatedAt, stale: true)
    }

    private static func encode<T: Encodable>(_ value: T) -> Data? {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return try? encoder.encode(value)
    }
}

actor MacDataService {
    private let store: MacDataStore
    private let session: URLSession
    private var lastWeatherAttempt = Date.distantPast
    private var lastStockAttempt = Date.distantPast

    init(store: MacDataStore) {
        self.store = store
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 8
        configuration.timeoutIntervalForResource = 12
        session = URLSession(configuration: configuration)
    }

    func refreshDue(force: Bool = false) async {
        let now = Date()
        if force || now.timeIntervalSince(lastWeatherAttempt) >= 900 {
            lastWeatherAttempt = now
            await refreshWeather()
        }
        if force || now.timeIntervalSince(lastStockAttempt) >= 5 {
            lastStockAttempt = now
            await refreshStocks()
        }
    }

    private func refreshWeather() async {
        do {
            let preferences = MacDataPreferences.load()
            guard let location = try await resolveLocation(preferences) else { return }
            let coordinate = String(format: "latitude=%.6f&longitude=%.6f", locale: Locale(identifier: "en_US_POSIX"),
                                    location.latitude, location.longitude)
            let forecast = URL(string: "https://api.open-meteo.com/v1/forecast?\(coordinate)&current=temperature_2m,relative_humidity_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto")!
            let air = URL(string: "https://air-quality-api.open-meteo.com/v1/air-quality?\(coordinate)&current=us_aqi,pm2_5")!
            let (forecastData, forecastResponse) = try await session.data(from: forecast)
            guard (forecastResponse as? HTTPURLResponse)?.statusCode == 200 else {
                throw DataError.invalidResponse
            }
            let (airData, airResponse) = try await session.data(from: air)
            guard (airResponse as? HTTPURLResponse)?.statusCode == 200 else {
                throw DataError.invalidResponse
            }
            let value = try Self.parseWeather(forecast: forecastData, air: airData, city: location.label)
            store.update(weather: value)
        } catch {
            store.markWeatherStale()
        }
    }

    private func refreshStocks() async {
        let symbols = MacDataPreferences.load().symbols.prefix(20)
        guard !symbols.isEmpty,
              let url = URL(string: "https://qt.gtimg.cn/q=" + symbols.joined(separator: ",")) else { return }
        do {
            var request = URLRequest(url: url)
            request.setValue("AI-bot/0.1", forHTTPHeaderField: "User-Agent")
            let (data, response) = try await session.data(for: request)
            guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw DataError.invalidResponse }
            let encoding = String.Encoding(rawValue:
                CFStringConvertEncodingToNSStringEncoding(
                    CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
            guard let text = String(data: data, encoding: encoding) else { throw DataError.invalidResponse }
            let quotes = Self.parseStocks(text, order: Array(symbols))
            guard !quotes.isEmpty else { throw DataError.invalidResponse }
            store.update(stocks: StockSnapshot(quotes: quotes, updatedAt: Date(), stale: false))
        } catch {
            store.markStocksStale()
        }
    }

    private func resolveLocation(_ preferences: MacDataPreferences) async throws
        -> (latitude: Double, longitude: Double, label: String)? {
        if let latitude = preferences.latitude, let longitude = preferences.longitude,
           (-90...90).contains(latitude), (-180...180).contains(longitude) {
            return (latitude, longitude, preferences.city.isEmpty ? "当前位置" : preferences.city)
        }
        guard !preferences.city.isEmpty else { return nil }
        var components = URLComponents(string: "https://geocoding-api.open-meteo.com/v1/search")!
        components.queryItems = [
            URLQueryItem(name: "count", value: "1"), URLQueryItem(name: "language", value: "zh"),
            URLQueryItem(name: "format", value: "json"), URLQueryItem(name: "name", value: preferences.city)
        ]
        let (data, response) = try await session.data(from: components.url!)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw DataError.invalidResponse }
        let decoded = try JSONDecoder().decode(GeocodingResponse.self, from: data)
        guard let result = decoded.results?.first else { return nil }
        return (result.latitude, result.longitude, result.name)
    }

    static func parseWeather(forecast: Data, air: Data, city: String, now: Date = Date()) throws
        -> WeatherSnapshot {
        let weather = try JSONDecoder().decode(ForecastResponse.self, from: forecast)
        let quality = try JSONDecoder().decode(AirResponse.self, from: air)
        guard let high = weather.daily.temperatureMax.first,
              let low = weather.daily.temperatureMin.first else { throw DataError.invalidResponse }
        return WeatherSnapshot(city: city, condition: condition(weather.current.code),
                               temperature: weather.current.temperature, high: high, low: low,
                               humidity: Int(weather.current.humidity.rounded()), weatherCode: weather.current.code,
                               pm25: quality.current?.pm25,
                               airQualityIndex: quality.current.map { Int($0.aqi.rounded()) },
                               source: "Open-Meteo", updatedAt: now, stale: false)
    }

    static func parseStocks(_ text: String, order: [String]) -> [StockQuote] {
        var values: [String: StockQuote] = [:]
        for rawLine in text.split(whereSeparator: \.isNewline) {
            let line = String(rawLine).trimmingCharacters(in: .whitespacesAndNewlines)
            guard line.hasPrefix("v_"), let equals = line.firstIndex(of: "=") else { continue }
            guard let symbol = normalizeStock(String(line[line.index(line.startIndex, offsetBy: 2)..<equals])) else { continue }
            let body = line[line.index(after: equals)...].trimmingCharacters(in: CharacterSet(charactersIn: "\";\r"))
            let fields = body.split(separator: "~", omittingEmptySubsequences: false).map(String.init)
            guard fields.count > 32, let price = Double(fields[3]), let change = Double(fields[31]),
                  let percent = Double(fields[32]) else { continue }
            let trend = change > 0 ? 1 : change < 0 ? -1 : 0
            let percentText = trend > 0 ? String(format: "+%.2f%%", percent)
                : String(format: "%.2f%%", percent)
            values[symbol] = StockQuote(symbol: symbol, code: fields[2].components(separatedBy: ".").first ?? fields[2],
                                        name: fields[1].trimmingCharacters(in: .whitespaces),
                                        price: String(format: abs(price) < 1 ? "%.3f" : "%.2f", price),
                                        changePercent: percentText, trend: trend)
        }
        return order.compactMap { normalizeStock($0) }.compactMap { values[$0] }.prefix(20).map { $0 }
    }

    static func normalizeStock(_ input: String) -> String? {
        var value = input.trimmingCharacters(in: .whitespacesAndNewlines)
        if value.count == 6 && value.allSatisfy(\.isNumber) {
            if value.first == "6" { value = "sh" + value }
            else if value.first == "0" || value.first == "3" { value = "sz" + value }
            else if value.first == "4" || value.first == "8" { value = "bj" + value }
        }
        guard value.count > 2 else { return nil }
        let market = value.prefix(2).lowercased()
        guard ["sh", "sz", "bj", "hk", "us"].contains(market) else { return nil }
        return market + value.dropFirst(2).uppercased()
    }

    private static func condition(_ code: Int) -> String {
        switch code {
        case 0: return "晴"
        case 1, 2: return "多云"
        case 3: return "阴"
        case 45, 48: return "雾"
        case 51, 53, 55, 56, 57: return "毛毛雨"
        case 61, 63, 65, 66, 67, 80, 81, 82: return "雨"
        case 71, 73, 75, 77, 85, 86: return "雪"
        case 95, 96, 99: return "雷雨"
        default: return "未知"
        }
    }

    private enum DataError: Error { case invalidResponse }
    private struct ForecastResponse: Decodable {
        let current: Current
        let daily: Daily
        struct Current: Decodable {
            let temperature: Double
            let humidity: Double
            let code: Int
            enum CodingKeys: String, CodingKey {
                case temperature = "temperature_2m", humidity = "relative_humidity_2m", code = "weather_code"
            }
        }
        struct Daily: Decodable {
            let temperatureMax: [Double]
            let temperatureMin: [Double]
            enum CodingKeys: String, CodingKey {
                case temperatureMax = "temperature_2m_max", temperatureMin = "temperature_2m_min"
            }
        }
    }
    private struct AirResponse: Decodable {
        let current: Current?
        struct Current: Decodable {
            let aqi: Double
            let pm25: Double
            enum CodingKeys: String, CodingKey { case aqi = "us_aqi", pm25 = "pm2_5" }
        }
    }
    private struct GeocodingResponse: Decodable {
        let results: [Result]?
        struct Result: Decodable { let name: String; let latitude: Double; let longitude: Double }
    }
}
