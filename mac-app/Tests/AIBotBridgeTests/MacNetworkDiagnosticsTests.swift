import XCTest
import CFNetwork
import Network
@testable import AIBotBridge

final class MacNetworkDiagnosticsTests: XCTestCase {
    func testActualPACResolutionWithSystemFallbackAndMalformedScript() throws {
        let params = NWParameters.tcp
        params.requiredLocalEndpoint = .hostPort(host: .ipv4(.loopback), port: .any)
        let listener = try NWListener(using: params, on: .any)
        let ready = DispatchSemaphore(value: 0)
        listener.stateUpdateHandler = { if case .ready = $0 { ready.signal() } }
        listener.newConnectionHandler = { connection in
            connection.start(queue: .global())
            connection.receive(minimumIncompleteLength: 1, maximumLength: 8192) { data, _, _, _ in
                let request = String(decoding: data ?? Data(), as: UTF8.self)
                let path = request.split(separator: " ").dropFirst().first.map(String.init) ?? ""
                let routes = ["/direct": "DIRECT", "/proxy": "PROXY 127.0.0.1:9876", "/fallback": "PROXY 127.0.0.1:9876; DIRECT"]
                let script = routes[path].map { "function FindProxyForURL(url, host) { return \"\($0)\"; }" } ?? "invalid javascript("
                let bytes = Data("HTTP/1.1 200 OK\r\nContent-Type: application/x-ns-proxy-autoconfig\r\nContent-Length: \(script.utf8.count)\r\nConnection: close\r\n\r\n\(script)".utf8)
                connection.send(content: bytes, completion: .contentProcessed { _ in connection.cancel() })
            }
        }
        listener.start(queue: .global()); defer { listener.cancel() }
        XCTAssertEqual(ready.wait(timeout: .now() + 3), .success)
        let port = try XCTUnwrap(listener.port)
        let target = URL(string: "https://chatgpt.com/backend-api/wham/usage")!
        for (name, expected) in [("direct", Set([false])), ("proxy", Set([true])), ("fallback", Set([true, false])), ("invalid", nil)] {
            let config: [String: Any] = [kCFNetworkProxiesProxyAutoConfigEnable as String: 1,
                kCFNetworkProxiesProxyAutoConfigURLString as String: "http://127.0.0.1:\(port.rawValue)/\(name)"]
            XCTAssertEqual(MacNetworkDiagnostics.routes(for: target, configuration: config), expected, name)
        }
    }
    func testProxyIdentityIsStableAcrossDictionaryOrderAndDetectsChanges() {
        let a: [String: Any] = ["HTTPEnable": 1, "HTTPPort": 8080, "__SCOPED__": ["en0": ["ExceptionsList": ["localhost"]]]]
        let b: [String: Any] = ["__SCOPED__": ["en0": ["ExceptionsList": ["localhost"]]], "HTTPPort": 8080, "HTTPEnable": 1]
        XCTAssertEqual(MacNetworkDiagnostics.identity(a), MacNetworkDiagnostics.identity(b))
        var changed = b; changed["HTTPPort"] = 8081
        XCTAssertNotEqual(MacNetworkDiagnostics.identity(a), MacNetworkDiagnostics.identity(changed))
    }
    private let now = Date(timeIntervalSince1970: 2000)
    private func result(_ status: Int? = 200, ms: Double? = 120, proxy: Bool? = true) -> MacNetworkProbeResult {
        .init(status: status, milliseconds: ms, proxy: proxy, failure: "连接超时")
    }
    private func checks(_ results: [MacNetworkProbeResult], pac: Bool? = true, routes: [Set<Bool>?] = [[true], [true]]) -> [[String: Any]] {
        MacNetworkDiagnostics.checks(results: results, pac: pac, routes: routes, now: now)
    }
    func testUnauthenticatedResponseAndMissingPACAreNotConnectionFailures() {
        let values = checks([result(401), result()], pac: false)
        XCTAssertEqual(values[0]["state"] as? String, "ok")
        XCTAssertEqual(values[0]["latencyMs"] as? Double, 120)
        XCTAssertEqual(values[1]["state"] as? String, "disabled")
        XCTAssertNil(checks([result(), result()], pac: nil)[1]["latencyMs"])
        XCTAssertEqual(checks([result(), result()], pac: nil)[1]["state"] as? String, "unknown")
    }
    func testRestrictionAndTransportFailureRemainDistinct() {
        for status in [403, 407, 429, 500, 503] {
            let value = checks([result(status), result()])
            XCTAssertEqual(value[0]["state"] as? String, "unknown")
            XCTAssertEqual(value[0]["summary"] as? String, "目标限制访问")
        }
        let failed = checks([result(nil, ms: nil, proxy: nil), result(nil, ms: nil, proxy: nil)])
        XCTAssertEqual(failed[0]["state"] as? String, "error")
        XCTAssertNil(failed[0]["latencyMs"])
        XCTAssertEqual(failed[1]["state"] as? String, "observing")
        XCTAssertNil(checks([result(), result(nil, ms: nil)])[0]["latencyMs"])
        XCTAssertNil(checks([result(ms: .nan), result()])[0]["latencyMs"])
    }
    func testResolvedPACRequiresActualConnectionEvidence() {
        XCTAssertEqual(checks([result(), result()])[1]["state"] as? String, "ok")
        XCTAssertEqual(checks([result(), result()], routes: [nil, [true]])[1]["state"] as? String, "error")
        XCTAssertEqual(checks([result(proxy: nil), result()])[1]["state"] as? String, "observing")
        XCTAssertEqual(checks([result(proxy: false), result()])[1]["state"] as? String, "warning")
        XCTAssertEqual(checks([result(proxy: false), result()], routes: [[true, false], [true]])[1]["state"] as? String, "ok")
    }
    func testFailureRetainsLastSuccessWithoutClaimingCurrentSuccess() {
        let values = MacNetworkDiagnostics.checks(results: [result(nil, ms: nil, proxy: nil), result(nil, ms: nil, proxy: nil)],
            pac: true, routes: [nil, nil], now: now, lastSuccess: (180, now.addingTimeInterval(-30)))
        XCTAssertEqual(values[0]["state"] as? String, "error")
        XCTAssertNil(values[0]["latencyMs"])
        XCTAssertEqual(values[0]["lastSuccessfulLatencyMs"] as? Double, 180)
        XCTAssertNotNil(values[0]["lastSuccessfulAt"])
    }
    func testNetworkFreshnessIndependentOfRepeatedCPUFrames() throws {
        let monitor = MacRuntimeHealth()
        let sample = MacNetworkDiagnosticSample(observedAt: now, checks: checks([result(), result()]),
            history: [["epochUtc": Int64(2000), "milliseconds": 120, "reachable": true]])
        func frame(_ time: Date, network: MacNetworkDiagnosticSample) -> [String: Any] {
            let metrics = SystemMetricsSnapshot(cpuPercent: 20, memoryPercent: 30, uploadBytesPerSecond: 1, downloadBytesPerSecond: 1,
                updatedAt: time, pressureObservedAt: time)
            return monitor.snapshot(metrics: metrics, now: time, logicalProcessors: 8, codexEnabled: true, desktopObserved: true, network: network)!
        }
        let fresh = frame(now.addingTimeInterval(20), network: sample)
        let freshChecks = try XCTUnwrap(fresh["checks"] as? [[String: Any]])
        XCTAssertEqual(freshChecks.first { $0["key"] as? String == "vpn" }?["epochUtc"] as? Int64, 2000)
        let expired = frame(now.addingTimeInterval(75), network: sample)
        let expiredChecks = try XCTUnwrap(expired["checks"] as? [[String: Any]])
        XCTAssertEqual(expiredChecks.first { $0["key"] as? String == "vpn" }?["state"] as? String, "unknown")
        XCTAssertEqual((expired["latencyHistory"] as? [Any])?.count, 0)
        let future = frame(now.addingTimeInterval(-1), network: sample)
        XCTAssertEqual((future["latencyHistory"] as? [Any])?.count, 0)
        XCTAssertNil((fresh["pressure"] as? [String: Any])?["processorQueue"])
    }
}
