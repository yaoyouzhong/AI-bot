import CoreLocation
import Foundation

// Created and driven on the main run loop required by CLLocationManager.
@MainActor
final class MacWeatherLocation: NSObject, CLLocationManagerDelegate {
    static let shared = MacWeatherLocation()
    private let manager = CLLocationManager()
    private var pending: [CheckedContinuation<(Double, Double), Error>] = []
    private var canPrompt = false
    private var timeout: Task<Void, Never>?
    private let reverseLookup: (Double, Double) async -> String?
    private var place: (latitude: Double, longitude: Double, name: String, updatedAt: Date)?
    enum Failure: LocalizedError {
        case denied, needsAuthorization, unavailable
        var errorDescription: String? {
            switch self {
            case .denied: return "定位未授权。请在系统设置允许 AI-bot Bridge 后重试。"
            case .needsAuthorization: return "请先点击「获取当前位置」授权定位。"
            case .unavailable: return "暂时无法定位，已保留上次天气；请检查系统定位服务。"
            }
        }
    }
    init(reverseLookup: ((Double, Double) async -> String?)? = nil) {
        self.reverseLookup = reverseLookup ?? Self.reverseGeocode
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters
    }
    func namedLocation() async throws -> (Double, Double, String) {
        let point = try await locate()
        let grid = Self.weatherGrid(latitude: point.0, longitude: point.1)
        return (grid.0, grid.1, await placeName(latitude: point.0, longitude: point.1))
    }
    func placeName(latitude: Double, longitude: Double, now: Date = Date()) async -> String {
        let sameGrid = place?.latitude == latitude && place?.longitude == longitude
        if sameGrid, let place, (0..<21_600).contains(now.timeIntervalSince(place.updatedAt)) { return place.name }
        if let name = await reverseLookup(latitude, longitude), !name.isEmpty {
            place = (latitude, longitude, name, now); return name
        }
        // A naming failure must neither erase a known name for this grid nor
        // label a different location with the previous city's name.
        return sameGrid ? place?.name ?? "当前位置" : "当前位置"
    }
    private static func reverseGeocode(latitude: Double, longitude: Double) async -> String? {
        let geocoder = CLGeocoder()
        let deadline = Task {
            do { try await Task.sleep(nanoseconds: 5_000_000_000) } catch { return }
            geocoder.cancelGeocode()
        }
        defer { deadline.cancel() }
        guard let mark = try? await geocoder.reverseGeocodeLocation(
            CLLocation(latitude: latitude, longitude: longitude), preferredLocale: Locale(identifier: "zh_CN")).first else { return nil }
        return placeLabel(city: mark.locality ?? mark.subAdministrativeArea, district: mark.subLocality, region: mark.administrativeArea)
    }
    nonisolated static func placeLabel(city: String?, district: String?, region: String?) -> String? {
        func clean(_ value: String?) -> String? {
            guard let value, !value.contains("\0") else { return nil }
            let result = value.components(separatedBy: .whitespacesAndNewlines).filter { !$0.isEmpty }.joined(separator: " ")
            return result.isEmpty || result.utf8.count > 160 ? nil : result
        }
        let city = clean(city) ?? clean(region), district = clean(district)
        guard let city else { return district }
        guard let district, district != city else { return city }
        if district.hasPrefix(city) { return district }
        if city.hasSuffix(district) { return city }
        return city + " " + district
    }
    func requireAuthorization() throws {
        switch manager.authorizationStatus {
        case .authorizedAlways, .authorizedWhenInUse: return
        case .notDetermined: throw Failure.needsAuthorization
        case .denied, .restricted: throw Failure.denied
        @unknown default: throw Failure.unavailable
        }
    }
    func locate(prompt: Bool = true) async throws -> (Double, Double) {
        if !prompt { try requireAuthorization() }
        return try await withCheckedThrowingContinuation { continuation in
            pending.append(continuation)
            guard pending.count == 1 else { return }
            canPrompt = prompt
            timeout = Task { [weak self] in
                do { try await Task.sleep(nanoseconds: 30_000_000_000) }
                catch { return }
                self?.finish(.failure(Failure.unavailable))
            }
            authorizeAndStart()
        }
    }
    private func authorizeAndStart() {
        guard !pending.isEmpty else { return }
        switch manager.authorizationStatus {
        case .notDetermined:
            if canPrompt { manager.requestWhenInUseAuthorization() }
            else { finish(.failure(Failure.needsAuthorization)) }
        case .authorizedAlways, .authorizedWhenInUse: manager.startUpdatingLocation()
        case .denied, .restricted: finish(.failure(Failure.denied))
        @unknown default: finish(.failure(Failure.unavailable))
        }
    }
    nonisolated func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        Task { @MainActor in self.authorizeAndStart() }
    }
    nonisolated func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last, location.horizontalAccuracy >= 0,
              abs(location.timestamp.timeIntervalSinceNow) < 120,
              CLLocationCoordinate2DIsValid(location.coordinate) else { return }
        // Use an approximate 100 m grid in memory for district lookup. The
        // editor displays and saves only its existing coarser grid.
        let grid = ((location.coordinate.latitude * 1000).rounded() / 1000,
                    (location.coordinate.longitude * 1000).rounded() / 1000)
        Task { @MainActor in self.finish(.success(grid)) }
    }
    nonisolated static func weatherGrid(latitude: Double, longitude: Double) -> (Double, Double) {
        ((latitude * 100).rounded() / 100, (longitude * 100).rounded() / 100)
    }
    nonisolated func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        if (error as? CLError)?.code == .locationUnknown { return }
        let failure = (error as? CLError)?.code == .denied ? Failure.denied : Failure.unavailable
        Task { @MainActor in self.finish(.failure(failure)) }
    }
    private func finish(_ result: Result<(Double, Double), Error>) {
        let requests = pending
        guard !requests.isEmpty else { return }
        pending.removeAll(); canPrompt = false; timeout?.cancel(); timeout = nil
        manager.stopUpdatingLocation()
        requests.forEach { $0.resume(with: result) }
    }
}
