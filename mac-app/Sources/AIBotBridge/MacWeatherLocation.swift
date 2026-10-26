import CoreLocation
import Foundation

// Created and driven on the main run loop required by CLLocationManager.
@MainActor
final class MacWeatherLocation: NSObject, CLLocationManagerDelegate {
    static let shared = MacWeatherLocation()
    private let manager = CLLocationManager()
    private var pending: CheckedContinuation<(Double, Double), Error>?
    private var timeout: Task<Void, Never>?
    private let reverseLookup: (Double, Double) async -> String?
    private var place: (latitude: Double, longitude: Double, name: String, updatedAt: Date)?
    enum Failure: LocalizedError {
        case denied, unavailable, busy
        var errorDescription: String? {
            switch self {
            case .denied: return "定位权限未允许，请在系统设置的定位服务中允许 AI-bot Bridge。"
            case .unavailable: return "暂时无法定位，已保留上次天气；请检查系统定位服务。"
            case .busy: return "正在获取位置，请稍后重试。"
            }
        }
    }
    init(reverseLookup: ((Double, Double) async -> String?)? = nil) {
        self.reverseLookup = reverseLookup ?? Self.reverseGeocode
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyKilometer
    }
    func namedLocation() async throws -> (Double, Double, String) {
        let grid = try await locate()
        return (grid.0, grid.1, await placeName(latitude: grid.0, longitude: grid.1))
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
        return city + " · " + district
    }
    func locate() async throws -> (Double, Double) {
        guard pending == nil else { throw Failure.busy }
        return try await withCheckedThrowingContinuation { continuation in
            pending = continuation
            timeout = Task { [weak self] in
                do { try await Task.sleep(nanoseconds: 30_000_000_000) }
                catch { return }
                self?.finish(.failure(Failure.unavailable))
            }
            authorizeAndStart()
        }
    }
    private func authorizeAndStart() {
        guard pending != nil else { return }
        switch manager.authorizationStatus {
        case .notDetermined: manager.requestWhenInUseAuthorization()
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
        // Weather needs an approximate grid cell; never persist precise coordinates.
        let grid = Self.weatherGrid(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
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
        guard let pending else { return }
        self.pending = nil; timeout?.cancel(); timeout = nil
        manager.stopUpdatingLocation()
        pending.resume(with: result)
    }
}
