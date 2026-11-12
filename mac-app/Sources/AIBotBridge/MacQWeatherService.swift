import Foundation
import Security

final class MacWeatherCredentialStore {
    static let shared = MacWeatherCredentialStore()
    private let lock = NSLock()
    private var cached: [String: String] = [:]
    func save(_ raw: String, host: String) throws {
        guard MacQWeatherService.validHost(host) else { throw MacQWeatherService.Failure.configuration }
        let key = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (8...512).contains(key.utf8.count), key.utf8.allSatisfy({ (33...126).contains($0) }) else {
            throw MacQWeatherService.Failure.configuration
        }
        lock.lock(); defer { lock.unlock() }
        try MacKeychainAccess.save("qweather-api-key-v1:" + host, Data(key.utf8))
        cached[host] = key
    }
    func read(host: String) throws -> String {
        guard MacQWeatherService.validHost(host) else { throw MacQWeatherService.Failure.configuration }
        lock.lock(); defer { lock.unlock() }
        if let key = cached[host] { return key }
        guard let data = try MacKeychainAccess.read("qweather-api-key-v1:" + host),
              let key = String(data: data, encoding: .utf8), !key.isEmpty else {
            throw MacQWeatherService.Failure.credential
        }
        cached[host] = key; return key
    }
}

private final class MacWeatherNoRedirect: NSObject, URLSessionTaskDelegate {
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(nil)
    }
}

actor MacQWeatherService {
    enum Failure: LocalizedError {
        case configuration, credential, response, http(Int)
        var errorDescription: String? {
            switch self {
            case .configuration: return "和风天气：请填写控制台分配的 API Host 和 API Key。"
            case .credential: return "和风天气：请在设置中保存该 Host 的 API Key，或允许钥匙串访问。"
            case .response: return "和风天气：数据暂不可用，已保留上次成功天气。"
            case .http(let code): return "和风天气：请求失败（HTTP \(code)），已保留上次成功天气。"
            }
        }
    }
    private let session: URLSession
    init() {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 10; configuration.timeoutIntervalForResource = 15
        session = URLSession(configuration: configuration, delegate: MacWeatherNoRedirect(), delegateQueue: nil)
    }
    nonisolated static func validHost(_ host: String) -> Bool {
        let parts = host.split(separator: ".", omittingEmptySubsequences: false)
        guard host.utf8.count <= 253, parts.count >= 3,
              parts.suffix(2) == ["qweatherapi", "com"] else { return false }
        return parts.dropLast(2).allSatisfy { part in
            (1...63).contains(part.count) && part.first != "-" && part.last != "-" &&
            part.utf8.allSatisfy { (97...122).contains($0) || (48...57).contains($0) || $0 == 45 }
        }
    }
    nonisolated static func request(host: String, key: String, path: String, location: String) throws -> URLRequest {
        guard validHost(host), ["/v7/weather/now", "/v7/weather/7d", "/v7/weather/24h", "/geo/v2/city/lookup"].contains(path),
              !key.isEmpty, key.utf8.allSatisfy({ (33...126).contains($0) }) else { throw Failure.configuration }
        var c = URLComponents(); c.scheme = "https"; c.host = host; c.path = path
        c.queryItems = [URLQueryItem(name: "location", value: location), URLQueryItem(name: "lang", value: "zh")]
        if path == "/geo/v2/city/lookup" { c.queryItems?.append(URLQueryItem(name: "number", value: "1")) }
        var request = URLRequest(url: c.url!)
        request.setValue(key, forHTTPHeaderField: "X-QW-Api-Key")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        return request
    }
    nonisolated static func airRequest(host: String, key: String, latitude: Double, longitude: Double) throws -> URLRequest {
        guard latitude.isFinite, longitude.isFinite, (-90...90).contains(latitude), (-180...180).contains(longitude) else {
            throw Failure.configuration
        }
        var request = try request(host: host, key: key, path: "/v7/weather/now", location: "")
        var c = URLComponents(url: request.url!, resolvingAgainstBaseURL: false)!
        c.path = String(format: "/airquality/v1/current/%.2f/%.2f", locale: Locale(identifier: "en_US_POSIX"), latitude, longitude)
        c.queryItems = [URLQueryItem(name: "lang", value: "zh")]
        request.url = c.url!
        return request
    }
    private func get(host: String, key: String, path: String, location: String) async throws -> Data {
        let request = try Self.request(host: host, key: key, path: path, location: location)
        return try await get(request)
    }
    private func get(_ request: URLRequest) async throws -> Data {
        let (data, response) = try await session.data(for: request)
        guard let status = (response as? HTTPURLResponse)?.statusCode else { throw Failure.response }
        guard status == 200 else { throw Failure.http(status) }
        guard data.count <= 2_000_000 else { throw Failure.response }
        return data
    }
    struct City {
        let latitude: Double, longitude: Double
        let name: String, parentCity: String?, id: String?, mainland: Bool

        var automaticLabel: String {
            MacWeatherLocation.placeLabel(city: parentCity, district: name, region: nil) ?? "当前位置"
        }
    }

    func resolveCity(_ city: String, host: String, key: String) async throws -> City {
        guard !city.isEmpty else { throw Failure.configuration }
        let data = try await get(host: host, key: key, path: "/geo/v2/city/lookup", location: city)
        return try Self.parseCity(data)
    }

    nonisolated static func parseCity(_ data: Data) throws -> City {
        let root = try MacWeatherForecast.qRoot(data)
        guard let rows = root["location"] as? [[String: Any]], let row = rows.first,
              let lat = MacWeatherForecast.number(row["lat"], range: -90...90),
              let lon = MacWeatherForecast.number(row["lon"], range: -180...180) else { throw Failure.response }
        return City(latitude: lat, longitude: lon, name: String((row["name"] as? String ?? "").prefix(80)),
                    parentCity: row["adm2"] as? String,
                    id: (row["id"] as? String).flatMap { $0.isEmpty ? nil : $0 },
                    mainland: Self.isMainland(country: row["country"] as? String, region: row["adm1"] as? String))
    }

    nonisolated static func isMainland(country: String?, region: String?) -> Bool {
        guard country == "中国" || country == "China", let region, !region.isEmpty else { return false }
        return !["香港", "澳门", "台湾", "Hong Kong", "Macao", "Macau", "Taiwan"].contains {
            region.lowercased().hasPrefix($0.lowercased())
        }
    }

    // Provider boundary only: callers and Open-Meteo retain WGS-84.
    // Same transform and regional guard as the Windows implementation.
    nonisolated static func mainlandCoordinate(_ latitude: Double, _ longitude: Double) -> (Double, Double) {
        guard (0.8293...55.8271).contains(latitude), (72.004...137.8347).contains(longitude) else {
            return (latitude, longitude)
        }
        let x = longitude - 105, y = latitude - 35
        func wave(_ angle: Double) -> Double { sin(angle * .pi) }
        let shared = (20 * wave(6*x) + 20 * wave(2*x)) * 2/3
        let northBase = -100 + 2*x + 3*y + 0.2*y*y + 0.1*x*y + 0.2*sqrt(abs(x))
        let eastBase = 300 + x + 2*y + 0.1*x*x + 0.1*x*y + 0.1*sqrt(abs(x))
        let north = northBase + shared + (20*wave(y) + 40*wave(y/3))*2/3 + (160*wave(y/12) + 320*wave(y/30))*2/3
        let east = eastBase + shared + (20*wave(x) + 40*wave(x/3))*2/3 + (150*wave(x/12) + 300*wave(x/30))*2/3
        let axis = 6378245.0, eccentricity = 0.006693421622965943
        let radians = latitude * .pi / 180
        let scale = 1 - eccentricity * pow(sin(radians), 2), root = sqrt(scale)
        return (latitude + north*180/(axis*(1-eccentricity)/(scale*root) * .pi),
                longitude + east*180/(axis/root*cos(radians) * .pi))
    }

    func fetchAutomatic(host: String, key: String, latitude: Double, longitude: Double) async throws -> WeatherSnapshot {
        func lookup(_ lat: Double, _ lon: Double) async throws -> City {
            try await resolveCity(String(format: "%.4f,%.4f", locale: Locale(identifier: "en_US_POSIX"), lon, lat),
                                  host: host, key: key)
        }
        var city = try await lookup(latitude, longitude)
        var lat = latitude, lon = longitude
        if city.mainland {
            let (shiftedLat, shiftedLon) = Self.mainlandCoordinate(latitude, longitude)
            if shiftedLat != latitude || shiftedLon != longitude {
                let shiftedCity = try await lookup(shiftedLat, shiftedLon)
                if shiftedCity.mainland { city = shiftedCity; lat = shiftedLat; lon = shiftedLon }
            }
        }
        // Forecast by district ID avoids rounding back across a district edge.
        // The separate AQI endpoint takes latitude/longitude to two decimals.
        return try await fetch(host: host, key: key, latitude: lat, longitude: lon, label: city.automaticLabel, locationID: city.id)
    }

    func fetch(host: String, key: String, latitude: Double, longitude: Double, label: String, locationID: String? = nil) async throws -> WeatherSnapshot {
        guard latitude.isFinite, longitude.isFinite, (-90...90).contains(latitude), (-180...180).contains(longitude) else {
            throw Failure.configuration
        }
        let location = locationID ?? String(format: "%.2f,%.2f", locale: Locale(identifier: "en_US_POSIX"), longitude, latitude)
        async let current = get(host: host, key: key, path: "/v7/weather/now", location: location)
        async let daily = get(host: host, key: key, path: "/v7/weather/7d", location: location)
        async let hourly = try? get(host: host, key: key, path: "/v7/weather/24h", location: location)
        async let air = try? get(Self.airRequest(host: host, key: key, latitude: latitude, longitude: longitude))
        var value = try await MacWeatherForecast.qweather(current: current, daily: daily, hourly: hourly, city: label)
        MacWeatherForecast.addQAir(await air, to: &value)
        return value
    }
}
