import CFNetwork
import CryptoKit
import Foundation

struct MacNetworkProbeResult {
    let status: Int?
    let milliseconds: Double?
    let proxy: Bool?
    let failure: String
    var responded: Bool { status.map { (100...599).contains($0) } == true }
    var restricted: Bool { status.map { [403, 407, 429].contains($0) || $0 >= 500 } == true }
}

struct MacNetworkDiagnosticSample {
    let observedAt: Date
    let checks: [[String: Any]]
    let history: [[String: Any]]
}

/// App-owned, bounded, unauthenticated header probes. No cookies, account token,
/// response body, proxy address or PAC URL enters the published diagnostics.
final class MacNetworkDiagnostics {
    static let targets = [URL(string: "https://chatgpt.com/backend-api/wham/usage")!, URL(string: "https://cdn.oaistatic.com/")!]
    private let lock = NSLock()
    private let queue = DispatchQueue(label: "AI-bot.network-diagnostics", qos: .utility)
    private var timer: DispatchSourceTimer?
    private var latest: MacNetworkDiagnosticSample?
    private var history: [[String: Any]] = []
    private var identity = ""
    private var lastSuccess: (Double, Date)?
    private var generation = 0

    func start() {
        lock.lock(); defer { lock.unlock() }
        guard timer == nil else { return }
        generation += 1; let current = generation
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now(), repeating: 30)
        timer.setEventHandler { [weak self] in self?.refresh(generation: current) }
        self.timer = timer; timer.resume()
    }
    func stop() {
        lock.lock(); generation += 1; timer?.cancel(); timer = nil; latest = nil; lock.unlock()
    }
    deinit { timer?.cancel() }
    func snapshot() -> MacNetworkDiagnosticSample? { lock.lock(); defer { lock.unlock() }; return latest }

    var summary: String {
        guard let value = snapshot(), value.observedAt <= Date(), Date().timeIntervalSince(value.observedAt) < 75 else { return "网络诊断：等待新采样" }
        return value.checks.map { check in
            let label = check["label"] as? String ?? "网络"
            let text = check["summary"] as? String ?? "待确认"
            let latency = (check["latencyMs"] as? Double).map { String(format: " · %.0f ms", $0) } ?? ""
            return "\(label)：\(text)\(latency)"
        }.joined(separator: "\n")
    }

    private func refresh(generation expected: Int) {
        let config = CFNetworkCopySystemProxySettings()?.takeRetainedValue() as? [String: Any]
        let before = Self.identity(config)
        let pac = config.map { Self.autoConfigured($0) }
        let routes = Self.targets.map { Self.routes(for: $0, configuration: config) }
        let results = Self.targets.map { HeaderProbe.run($0) }
        let after = Self.identity(CFNetworkCopySystemProxySettings()?.takeRetainedValue() as? [String: Any])
        lock.lock(); defer { lock.unlock() }
        guard generation == expected else { return }
        guard before == after else { latest = nil; history = []; lastSuccess = nil; identity = after; return }
        if identity != before { history = []; lastSuccess = nil; identity = before }
        let now = Date(), healthy = results.allSatisfy { $0.responded && !$0.restricted }
        let latency = Self.latency(results)
        if healthy, let latency { lastSuccess = (latency, now) }
        history.append(["epochUtc": Int64(now.timeIntervalSince1970), "milliseconds": latency as Any? ?? NSNull(), "reachable": results.allSatisfy(\.responded)])
        if history.count > 10 { history.removeFirst(history.count - 10) }
        latest = MacNetworkDiagnosticSample(observedAt: now,
            checks: Self.checks(results: results, pac: pac, routes: routes, now: now, lastSuccess: lastSuccess), history: history)
    }

    static func checks(results: [MacNetworkProbeResult], pac: Bool?, routes: [Set<Bool>?], now: Date,
                       lastSuccess: (Double, Date)? = nil) -> [[String: Any]] {
        guard results.count == 2, routes.count == 2 else { return [] }
        func route(_ value: Bool?) -> String { value.map { $0 ? "代理连接" : "无显式代理连接" } ?? "连接类型未知" }
        let names = ["ChatGPT", "资源服务"]
        let details = zip(names, results).map { name, result in
            "\(name)：\(result.status.map { "HTTP \($0)" } ?? result.failure)，\(route(result.proxy))"
        }.joined(separator: "；")
        let failed = results.contains { !$0.responded }, restricted = results.contains( where: { $0.restricted })
        let latency = Self.latency(results)
        let slow = (latency ?? 0) >= 2500
        var vpn: [String: Any] = ["key": "vpn", "label": "VPN / 代理", "state": failed ? "error" : restricted ? "unknown" : slow ? "observing" : "ok",
            "summary": failed ? "目标连接失败" : restricted ? "目标限制访问" : slow ? "响应偏慢，正在观察" : "出口响应正常",
            "detail": details + "。仅检测 HTTP 到达，不携带登录信息或验证模型请求；连接类型不判断 VPN 隧道是否启用。",
            "advice": failed ? "检查网络和系统代理；HTTP 拒绝与网络断开分别处理。" : "",
            "epochUtc": Int64(now.timeIntervalSince1970), "route": "系统网络路径"]
        if let latency { vpn["latencyMs"] = latency }
        if let lastSuccess { vpn["lastSuccessfulLatencyMs"] = lastSuccess.0; vpn["lastSuccessfulAt"] = ISO8601DateFormatter().string(from: lastSuccess.1) }
        let resolved = routes.allSatisfy { $0?.isEmpty == false }
        let observed = results.allSatisfy { $0.responded && $0.proxy != nil }
        let matches = resolved && observed && zip(routes, results).allSatisfy { $0.0!.contains($0.1.proxy!) }
        let state = pac == nil ? "unknown" : pac == false ? "disabled" : !resolved ? "error" : !observed ? "observing" : matches ? "ok" : "warning"
        let summary = pac == nil ? "系统代理配置不可读" : pac == false ? "未配置 PAC" : !resolved ? "PAC 选路失败" : !observed ? "PAC 已解析，连接待确认" : matches ? "PAC 连接类型已确认" : "实际连接与 PAC 结果不符"
        let pacCheck: [String: Any] = ["key": "pac", "label": "PAC 路由", "state": state, "summary": summary,
            "detail": pac == false ? "系统未开启自动代理脚本／自动发现；静态代理或隧道仍可能在使用。" : details + "。比较系统 PAC 结果与实际请求的代理／非代理类型，不证明具体节点或模型请求成功。",
            "advice": state == "error" || state == "warning" ? "检查系统自动代理配置和客户端；未修改任何代理设置。" : "",
            "epochUtc": Int64(now.timeIntervalSince1970), "route": zip(names, results).map { "\($0.0)：\(route($0.1.proxy))" }.joined(separator: "；")]
        return [vpn, pacCheck]
    }
    private static func latency(_ results: [MacNetworkProbeResult]) -> Double? {
        guard results.count == 2, results.allSatisfy({ $0.responded && $0.milliseconds.map { $0.isFinite && $0 >= 0 } == true }) else { return nil }
        return results.compactMap(\.milliseconds).max()
    }
    static func identity(_ value: [String: Any]?) -> String {
        guard let value, JSONSerialization.isValidJSONObject(value), let data = try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys]) else { return "unavailable" }
        return SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }
    private static func autoConfigured(_ value: [String: Any]) -> Bool {
        [kCFNetworkProxiesProxyAutoConfigEnable, kCFNetworkProxiesProxyAutoDiscoveryEnable].contains { (value[$0 as String] as? NSNumber)?.boolValue == true }
    }
    private final class PACResult { var done = false; var routes: Set<Bool>? }
    private static func types(_ proxies: [[String: Any]]) -> Set<Bool>? {
        var result = Set<Bool>()
        for proxy in proxies {
            guard let type = proxy[kCFProxyTypeKey as String] as? String else { return nil }
            if type == kCFProxyTypeNone as String { result.insert(false) }
            else if [kCFProxyTypeHTTP, kCFProxyTypeHTTPS, kCFProxyTypeSOCKS].contains(where: { type == $0 as String }) { result.insert(true) }
            else { return nil }
        }
        return result.isEmpty ? nil : result
    }
    static func routes(for target: URL, configuration: [String: Any]?) -> Set<Bool>? {
        guard let configuration else { return nil }
        let proxies = CFNetworkCopyProxiesForURL(target as CFURL, configuration as CFDictionary).takeRetainedValue() as? [[String: Any]] ?? []
        if let types = types(proxies) { return types }
        // CFNetwork appends a direct fallback after the primary PAC entry.
        // Resolve the script itself; its failure must not become a successful
        // PAC result merely because the outer fallback permits direct access.
        guard let first = proxies.first, proxies.count == 1 || types(Array(proxies.dropFirst())) != nil,
              first[kCFProxyTypeKey as String] as? String == kCFProxyTypeAutoConfigurationURL as String,
              let url = first[kCFProxyAutoConfigurationURLKey as String] as? URL,
              ["http", "https"].contains(url.scheme?.lowercased() ?? "") else { return nil }
        let result = PACResult()
        var context = CFStreamClientContext(version: 0, info: Unmanaged.passUnretained(result).toOpaque(), retain: nil, release: nil, copyDescription: nil)
        let source = ownedSource(CFNetworkExecuteProxyAutoConfigurationURL(url as CFURL, target as CFURL, { context, list, error in
            let result = Unmanaged<PACResult>.fromOpaque(context).takeUnretainedValue()
            if error == nil, let proxies = list as? [[String: Any]] { result.routes = MacNetworkDiagnostics.types(proxies) }
            result.done = true
        }, &context))
        let loop = CFRunLoopGetCurrent()
        CFRunLoopAddSource(loop, source, .defaultMode)
        defer { CFRunLoopSourceInvalidate(source); CFRunLoopRemoveSource(loop, source, .defaultMode) }
        let deadline = ProcessInfo.processInfo.systemUptime + 4
        while !result.done && ProcessInfo.processInfo.systemUptime < deadline { CFRunLoopRunInMode(.defaultMode, 0.1, true) }
        return result.done ? result.routes : nil
    }
    // CFNetwork's ownership annotation differs between the supported SDKs.
    private static func ownedSource(_ source: CFRunLoopSource) -> CFRunLoopSource { source }
    private static func ownedSource(_ source: Unmanaged<CFRunLoopSource>) -> CFRunLoopSource { source.takeRetainedValue() }

    private final class HeaderProbe: NSObject, URLSessionDataDelegate {
        private let finished = DispatchSemaphore(value: 0)
        private var status: Int?, milliseconds: Double?, proxy: Bool?
        private var failure = "连接失败"
        private let started = ProcessInfo.processInfo.systemUptime
        static func run(_ url: URL) -> MacNetworkProbeResult {
            let delegate = HeaderProbe(), config = URLSessionConfiguration.ephemeral
            config.httpCookieStorage = nil; config.httpShouldSetCookies = false; config.urlCredentialStorage = nil
            config.urlCache = nil; config.requestCachePolicy = .reloadIgnoringLocalCacheData
            config.timeoutIntervalForRequest = 6; config.timeoutIntervalForResource = 7; config.waitsForConnectivity = false
            let session = URLSession(configuration: config, delegate: delegate, delegateQueue: nil)
            var request = URLRequest(url: url); request.httpMethod = "HEAD"
            session.dataTask(with: request).resume()
            let completed = delegate.finished.wait(timeout: .now() + 8) == .success
            session.invalidateAndCancel()
            // No access to delegate fields after a timeout: callbacks may still run.
            guard completed else { return .init(status: nil, milliseconds: nil, proxy: nil, failure: "连接超时") }
            return .init(status: delegate.status, milliseconds: delegate.milliseconds, proxy: delegate.proxy, failure: delegate.failure)
        }
        func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive response: URLResponse, completionHandler: @escaping (URLSession.ResponseDisposition) -> Void) {
            status = (response as? HTTPURLResponse)?.statusCode
            milliseconds = (ProcessInfo.processInfo.systemUptime - started) * 1000
            completionHandler(.cancel) // Headers suffice; never retain a response body.
        }
        func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse, newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) { completionHandler(nil) }
        func urlSession(_ session: URLSession, task: URLSessionTask, didReceive challenge: URLAuthenticationChallenge, completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
            completionHandler(challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust ? .performDefaultHandling : .cancelAuthenticationChallenge, nil)
        }
        func urlSession(_ session: URLSession, task: URLSessionTask, didFinishCollecting metrics: URLSessionTaskMetrics) {
            proxy = metrics.transactionMetrics.last(where: { $0.response != nil })?.isProxyConnection
        }
        func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
            if let code = (error as? URLError)?.code {
                failure = code == .timedOut ? "连接超时" : code == .cannotFindHost || code == .dnsLookupFailed ? "域名解析失败" : code == .secureConnectionFailed || code == .serverCertificateUntrusted ? "TLS 验证失败" : "连接失败"
            }
            finished.signal()
        }
    }
}
