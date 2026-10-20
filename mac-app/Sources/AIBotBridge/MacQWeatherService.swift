import Foundation
import Security

final class MacWeatherCredentialStore {
    static let shared = MacWeatherCredentialStore()
    private let lock = NSLock()
    private var cached: [String: String] = [:]
    private func query(_ host: String) -> [String: Any] {
        [kSecClass as String: kSecClassGenericPassword,
         kSecAttrService as String: "io.github.yaoyouzhong.AI-bot",
         kSecAttrAccount as String: "qweather-api-key-v1:" + host]
    }
    func save(_ raw: String, host: String) throws {
        guard MacQWeatherService.validHost(host) else { throw MacQWeatherService.Failure.configuration }
        let key = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (8...512).contains(key.utf8.count), key.utf8.allSatisfy({ (33...126).contains($0) }) else {
            throw MacQWeatherService.Failure.configuration
        }
        lock.lock(); defer { lock.unlock() }
        let q = query(host), data = Data(key.utf8)
        var status = SecItemUpdate(q as CFDictionary, [kSecValueData as String: data] as CFDictionary)
        if status == errSecItemNotFound {
            var item = q; item[kSecValueData as String] = data; status = SecItemAdd(item as CFDictionary, nil)
        }
        guard status == errSecSuccess else { throw MacQWeatherService.Failure.credential }
        cached[host] = key
    }
    func read(host: String) throws -> String {
        guard MacQWeatherService.validHost(host) else { throw MacQWeatherService.Failure.configuration }
        lock.lock(); defer { lock.unlock() }
        if let key = cached[host] { return key }
        var q = query(host); q[kSecReturnData as String] = true; q[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: CFTypeRef?
        guard SecItemCopyMatching(q as CFDictionary, &result) == errSecSuccess,
              let data = result as? Data, let key = String(data: data, encoding: .utf8), !key.isEmpty else {
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
        var request = URLRequest(url: c.url!)
        request.setValue(key, forHTTPHeaderField: "X-QW-Api-Key")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        return request
    }
    private func get(host: String, key: String, path: String, location: String) async throws -> Data {
        let request = try Self.request(host: host, key: key, path: path, location: location)
        let (data, response) = try await session.data(for: request)
        guard let status = (response as? HTTPURLResponse)?.statusCode else { throw Failure.response }
        guard status == 200 else { throw Failure.http(status) }
        guard data.count <= 2_000_000 else { throw Failure.response }
        return data
    }
    func resolveCity(_ city: String, host: String, key: String) async throws -> (Double, Double, String) {
        guard !city.isEmpty else { throw Failure.configuration }
        let data = try await get(host: host, key: key, path: "/geo/v2/city/lookup", location: city)
        let root = try MacWeatherForecast.qRoot(data)
        guard let rows = root["location"] as? [[String: Any]], let row = rows.first,
              let lat = MacWeatherForecast.number(row["lat"], range: -90...90),
              let lon = MacWeatherForecast.number(row["lon"], range: -180...180) else { throw Failure.response }
        return (lat, lon, String((row["name"] as? String ?? city).prefix(80)))
    }
    func fetch(host: String, key: String, latitude: Double, longitude: Double, label: String) async throws -> WeatherSnapshot {
        guard latitude.isFinite, longitude.isFinite, (-90...90).contains(latitude), (-180...180).contains(longitude) else {
            throw Failure.configuration
        }
        let location = String(format: "%.2f,%.2f", locale: Locale(identifier: "en_US_POSIX"), longitude, latitude)
        async let current = get(host: host, key: key, path: "/v7/weather/now", location: location)
        async let daily = get(host: host, key: key, path: "/v7/weather/7d", location: location)
        async let hourly = try? get(host: host, key: key, path: "/v7/weather/24h", location: location)
        return try await MacWeatherForecast.qweather(current: current, daily: daily, hourly: hourly, city: label)
    }
}
