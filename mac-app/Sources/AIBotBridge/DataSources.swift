import CoreFoundation
import Foundation

struct MacDataExtras {
    let weather: WeatherSnapshot?
    let stocks: StockSnapshot?
    let systemMetrics: SystemMetricsSnapshot?
    let quotas: QuotaSnapshot?
    let music: MusicSnapshot?
    let musicCover: Data?
    var quotaTrend: MacQuotaTrend? = nil
    var domesticQuotas: DomesticQuotaSnapshot? = nil

    static let empty = MacDataExtras(weather: nil, stocks: nil, systemMetrics: nil,
                                     quotas: nil, music: MusicSnapshot.empty(), musicCover: nil)
}

struct MacDataPreferences: Equatable {
    let city: String
    let latitude: Double?
    let longitude: Double?
    let symbols: [String]
    var automaticLocation = false
    var weatherProvider = "open-meteo"
    var qweatherHost = ""

    static func load(_ defaults: UserDefaults = .standard) -> MacDataPreferences {
        let city = defaults.string(forKey: "weather_city")?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let latitude = defaults.object(forKey: "weather_latitude") == nil
            ? nil : defaults.double(forKey: "weather_latitude")
        let longitude = defaults.object(forKey: "weather_longitude") == nil
            ? nil : defaults.double(forKey: "weather_longitude")
        let configured = defaults.stringArray(forKey: "stock_symbols") ?? ["sh000001"]
        return MacDataPreferences(city: city, latitude: latitude, longitude: longitude,
                                  symbols: configured.compactMap { MacDataService.normalizeStock($0) },
                                  automaticLocation: defaults.bool(forKey: "weather_automatic_location"),
                                  weatherProvider: defaults.string(forKey: "weather_provider") == "qweather" ? "qweather" : "open-meteo",
                                  qweatherHost: defaults.string(forKey: "qweather_api_host") ?? "")
    }

    func save(_ defaults: UserDefaults = .standard) {
        defaults.set(city, forKey: "weather_city")
        if let latitude { defaults.set(latitude, forKey: "weather_latitude") }
        else { defaults.removeObject(forKey: "weather_latitude") }
        if let longitude { defaults.set(longitude, forKey: "weather_longitude") }
        else { defaults.removeObject(forKey: "weather_longitude") }
        defaults.set(symbols, forKey: "stock_symbols")
        defaults.set(automaticLocation, forKey: "weather_automatic_location")
        defaults.set(weatherProvider, forKey: "weather_provider")
        defaults.set(qweatherHost, forKey: "qweather_api_host")
    }
}

final class MacDataStore {
    private let lock = NSLock()
    private let defaults: UserDefaults
    private let quotaHistory: MacQuotaHistory?
    private var weather: WeatherSnapshot?
    private var weatherNote = "天气：尚未刷新"
    var weatherDiagnostic: String { lock.lock(); defer { lock.unlock() }; return weatherNote }
    func weatherDiagnostic(_ note: String) { lock.lock(); weatherNote = note; lock.unlock() }
    private var stocks: StockSnapshot?
    private var systemMetrics: SystemMetricsSnapshot?
    var systemDiagnostic: String {
        lock.lock(); defer { lock.unlock() }
        guard let count = systemMetrics?.samples?.count, let sequence = systemMetrics?.sampleSequence else { return "网络曲线：等待采样" }
        return "网络曲线：\(count) 个样本 · 序号 \(sequence)"
    }
    private var quotas: QuotaSnapshot?
    private var domesticQuotas: DomesticQuotaSnapshot?
    private var music: MusicSnapshot = .empty()
    private var musicSampled = false
    private var musicCover: Data?
    private let decoder: JSONDecoder = {
        let value = JSONDecoder()
        value.dateDecodingStrategy = .iso8601
        return value
    }()

    init(defaults: UserDefaults = .standard, quotaHistory: MacQuotaHistory? = nil) {
        self.defaults = defaults
        self.quotaHistory = quotaHistory
        if let data = defaults.data(forKey: "weather_cache"),
           let value = try? decoder.decode(WeatherSnapshot.self, from: data) {
            weather = value
            weather?.stale = true
        }
        if let data = defaults.data(forKey: "stock_cache"),
           let value = try? decoder.decode(StockSnapshot.self, from: data) {
            stocks = StockSnapshot(quotes: value.quotes, updatedAt: value.updatedAt, stale: true)
        }
        if let data = defaults.data(forKey: "quota_cache"),
           let value = try? decoder.decode(QuotaSnapshot.self, from: data) {
            quotas = QuotaSnapshot(claude: Self.stale(value.claude), codex: Self.stale(value.codex))
        }
        if let data = defaults.data(forKey: "domestic_quota_cache"),
           let value = try? decoder.decode(DomesticQuotaSnapshot.self, from: data) {
            domesticQuotas = value
            domesticQuotas?.deepSeek = value.deepSeek.map(Self.staleDomestic)
            domesticQuotas?.zhipu = value.zhipu.map(Self.staleDomestic)
        }
    }

    func snapshot() -> MacDataExtras {
        lock.lock()
        defer { lock.unlock() }
        return MacDataExtras(weather: weather, stocks: stocks, systemMetrics: systemMetrics,
                             quotas: quotas, music: music, musicCover: musicCover, quotaTrend: quotaHistory?.snapshot(),
                             domesticQuotas: domesticQuotas)
    }
    func updateDomestic(_ value: DomesticProviderQuotaSnapshot) {
        lock.lock(); defer { lock.unlock() }
        var current = domesticQuotas ?? DomesticQuotaSnapshot(alibaba: nil, kimi: nil, miniMax: nil, deepSeek: nil)
        if value.provider == "deepseek" { current.deepSeek = value }
        else if value.provider == "zhipu" { current.zhipu = value }
        else { return }
        domesticQuotas = current
        if let data = Self.encode(current) { defaults.set(data, forKey: "domestic_quota_cache") }
    }
    func markDomesticStale(_ provider: String) {
        lock.lock(); defer { lock.unlock() }
        guard var current = domesticQuotas else { return }
        if provider == "deepseek" { current.deepSeek = current.deepSeek.map(Self.staleDomestic) }
        if provider == "zhipu" { current.zhipu = current.zhipu.map(Self.staleDomestic) }
        domesticQuotas = current
    }
    func clearDeepSeekForNewAccount() {
        lock.lock(); defer { lock.unlock() }
        domesticQuotas?.deepSeek = nil
        if let current = domesticQuotas, let data = Self.encode(current) { defaults.set(data, forKey: "domestic_quota_cache") }
    }
    private static func staleDomestic(_ value: DomesticProviderQuotaSnapshot) -> DomesticProviderQuotaSnapshot {
        var value = value; value.stale = true; return value
    }

    func update(weather value: WeatherSnapshot) {
        lock.lock()
        var value = value
        if let old = weather, old.city == value.city, old.source == value.source,
           old.utcOffsetSeconds == value.utcOffsetSeconds {
            if value.hourly?.isEmpty != false, old.hourly?.isEmpty == false {
                value.hourly = old.hourly; value.hourlyStale = true
            }
            if value.daily?.isEmpty != false, old.daily?.isEmpty == false {
                value.daily = old.daily; value.dailyStale = true
            }
        }
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
            weather = value
            weather?.stale = true
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
        defer { lock.unlock() }
        guard !musicSampled || value.updatedAt >= music.updatedAt else { return }
        musicSampled = true
        if value.title.isEmpty || value.title != music.title || value.artist != music.artist || value.album != music.album { musicCover = nil }
        var next=value;next.hasArtwork = !value.title.isEmpty && musicCover?.isEmpty == false
        music = next
    }

    func update(music value: MusicSnapshot, cover: Data) {
        lock.lock()
        defer { lock.unlock() }
        // Artwork may finish after a newer poll or a confirmed control readback.
        // Retain that playback sample, and never attach an old track's cover.
        if musicSampled, value.updatedAt < music.updatedAt {
            guard music.title == value.title, music.artist == value.artist,
                  music.album == value.album else { return }
        }
        var next = musicSampled && value.updatedAt < music.updatedAt ? music : value
        musicSampled = true
        next.hasArtwork = !next.title.isEmpty && !cover.isEmpty
        music = next
        musicCover = cover
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

    func recordCodexQuota(_ value: ProviderQuotaSnapshot, account: String?) {
        lock.lock(); defer { lock.unlock() }
        quotaHistory?.record(value, account: account)
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
                                     updatedAt: fresh.updatedAt, stale: true,
                                     primaryWindowSeconds: fresh.primaryWindowSeconds,
                                     weeklyWindowSeconds: fresh.weeklyWindowSeconds)
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
                                     updatedAt: value.updatedAt, stale: true,
                                     primaryWindowSeconds: value.primaryWindowSeconds,
                                     weeklyWindowSeconds: value.weeklyWindowSeconds)
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
    private var refreshingWeather = false
    private let qweather = MacQWeatherService()

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
        guard !refreshingWeather else { return }
        refreshingWeather = true
        let preferences = MacDataPreferences.load()
        defer {
            refreshingWeather = false
            if MacDataPreferences.load() != preferences { Task { await self.refreshDue(force: true) } }
        }
        do {
            store.weatherDiagnostic(preferences.automaticLocation ? "天气：正在获取大致位置…" : "天气：正在查询…")
            if preferences.weatherProvider == "qweather" {
                let key = try MacWeatherCredentialStore.shared.read(host: preferences.qweatherHost)
                let location: (Double, Double, String)
                if preferences.automaticLocation {
                    let grid = try await MacWeatherLocation.shared.locate()
                    location = (grid.0, grid.1, "当前位置")
                } else if let lat = preferences.latitude, let lon = preferences.longitude {
                    location = (lat, lon, preferences.city.isEmpty ? "当前位置" : preferences.city)
                } else {
                    location = try await qweather.resolveCity(preferences.city, host: preferences.qweatherHost, key: key)
                }
                let value = try await qweather.fetch(host: preferences.qweatherHost, key: key,
                    latitude: location.0, longitude: location.1, label: location.2)
                guard MacDataPreferences.load() == preferences else { return }
                store.update(weather: value)
                store.weatherDiagnostic(Self.forecastNote(store.snapshot().weather!))
                return
            }
            guard let location = try await resolveLocation(preferences) else {
                store.weatherDiagnostic("天气：请选择自动定位或填写城市。"); return
            }
            let coordinate = String(format: "latitude=%.6f&longitude=%.6f", locale: Locale(identifier: "en_US_POSIX"),
                                    location.latitude, location.longitude)
            let forecast = URL(string: "https://api.open-meteo.com/v1/forecast?\(coordinate)&current=temperature_2m,relative_humidity_2m,weather_code&hourly=temperature_2m,weather_code,precipitation_probability&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,precipitation_sum,sunrise,sunset,daylight_duration&forecast_days=7&timezone=auto")!
            let air = URL(string: "https://air-quality-api.open-meteo.com/v1/air-quality?\(coordinate)&current=us_aqi,pm2_5")!
            let (forecastData, forecastResponse) = try await session.data(from: forecast)
            guard (forecastResponse as? HTTPURLResponse)?.statusCode == 200 else {
                throw DataError.invalidResponse
            }
            var airData: Data?
            if let (data, response) = try? await session.data(from: air),
               (response as? HTTPURLResponse)?.statusCode == 200 {
                airData = data
            }
            let value = try Self.parseWeather(forecast: forecastData, air: airData, city: location.label)
            guard MacDataPreferences.load() == preferences else { return }
            store.update(weather: value)
            store.weatherDiagnostic(Self.forecastNote(store.snapshot().weather!))
        } catch {
            guard MacDataPreferences.load() == preferences else { return }
            store.markWeatherStale()
            store.weatherDiagnostic((error as? MacWeatherLocation.Failure)?.errorDescription ??
                                    (error as? MacQWeatherService.Failure)?.errorDescription ??
                                    "天气：查询失败，已保留上次成功数据。")
        }
    }

    private static func forecastNote(_ value: WeatherSnapshot) -> String {
        "天气：已更新 · \(value.hourly?.count ?? 0) 小时 / \(value.daily?.count ?? 0) 天" +
        ((value.hourlyStale == true || value.dailyStale == true) ? " · 部分预报为缓存" : "")
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
        if preferences.automaticLocation {
            let coordinate = try await MacWeatherLocation.shared.locate()
            return (coordinate.0, coordinate.1, "当前位置")
        }
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

    static func parseWeather(forecast: Data, air: Data?, city: String, now: Date = Date()) throws
        -> WeatherSnapshot {
        let weather = try JSONDecoder().decode(ForecastResponse.self, from: forecast)
        let quality = air.flatMap { try? JSONDecoder().decode(AirResponse.self, from: $0) }
        guard let high = weather.daily.temperatureMax.first,
              let low = weather.daily.temperatureMin.first else { throw DataError.invalidResponse }
        var value = WeatherSnapshot(city: city, condition: condition(weather.current.code),
                               temperature: weather.current.temperature, high: high, low: low,
                               humidity: Int(weather.current.humidity.rounded()), weatherCode: weather.current.code,
                               pm25: quality?.current?.pm25,
                               airQualityIndex: quality?.current.map { Int($0.aqi.rounded()) },
                               source: "open-meteo", updatedAt: now, stale: false)
        try MacWeatherForecast.addMeteo(forecast, to: &value, now: now)
        return value
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

    static func condition(_ code: Int) -> String {
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
