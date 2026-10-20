import CoreLocation
import Foundation

// Created and driven on the main run loop required by CLLocationManager.
@MainActor
final class MacWeatherLocation: NSObject, CLLocationManagerDelegate {
    static let shared = MacWeatherLocation()
    private let manager = CLLocationManager()
    private var pending: CheckedContinuation<(Double, Double), Error>?
    private var timeout: Task<Void, Never>?
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
    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyKilometer
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
