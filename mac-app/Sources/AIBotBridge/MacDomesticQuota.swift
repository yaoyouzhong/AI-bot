import CoreFoundation
import Foundation
import Security

enum MacDomesticQuotaParser {
    enum Failure: Error { case invalid }
    static func object(_ data: Data) throws -> [String: Any] {
        guard data.count <= 65_536,
              let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw Failure.invalid }
        return root
    }
    static func number(_ value: Any?) -> Double? {
        if let number = value as? NSNumber {
            guard CFGetTypeID(number) != CFBooleanGetTypeID(), number.doubleValue.isFinite else { return nil }
            return number.doubleValue
        }
        if let text = value as? String, let value = Double(text), value.isFinite { return value }
        return nil
    }
    static func deepSeek(_ data: Data, now: Date = Date()) throws -> DomesticProviderQuotaSnapshot {
        let root = try object(data)
        guard let rows = root["balance_infos"] as? [[String: Any]] else { throw Failure.invalid }
        var values: [String: Double] = [:]
        for row in rows {
            guard let currency = row["currency"] as? String, ["CNY","USD"].contains(currency) else { continue }
            guard values[currency] == nil, let amount = number(row["total_balance"]) else { throw Failure.invalid }
            values[currency] = amount
        }
        let currency = values["CNY"] != nil ? "CNY" : "USD"
        guard let amount = values[currency] else { throw Failure.invalid }
        return value("deepseek", balance: amount, used: nil, currency: currency, now: now)
    }
    static func zhipu(_ data: Data, now: Date = Date()) throws -> DomesticProviderQuotaSnapshot {
        let root = try object(data)
        guard number(root["code"]) == 200, let body = root["data"] as? [String: Any],
              let balance = number(body["availableBalance"]), abs(balance) < 1e15 else { throw Failure.invalid }
        let used = number(body["totalSpendAmount"]).flatMap { abs($0) < 1e15 ? $0 : nil }
        return value("zhipu", balance: (balance * 100).rounded(.towardZero) / 100,
                     used: used.map { ($0 * 100).rounded(.towardZero) / 100 }, currency: "CNY", now: now)
    }
    private static func value(_ provider: String, balance: Double, used: Double?, currency: String, now: Date) -> DomesticProviderQuotaSnapshot {
        DomesticProviderQuotaSnapshot(provider: provider, plan: "API PAYG", primaryPercent: nil,
            primaryResetsAt: nil, weeklyPercent: nil, weeklyResetsAt: nil, balance: balance,
            usedCost: used, currency: currency, updatedAt: now, stale: false)
    }
}

// Never send API credentials to a redirected origin.
private final class MacQuotaNoRedirect: NSObject, URLSessionTaskDelegate {
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) { completionHandler(nil) }
}

actor MacDomesticQuotaService {
    private let store: MacDataStore
    private let session: URLSession
    private var key: String?
    private var loadedKey = false
    private var refreshing = false
    private var revision = 0
    private var note = "DeepSeek：尚未授权"
    private static let account = "deepseek-api-key-v1"
    private var credentials: [String: MacDomesticCredential] = [:], sourceNotes: [String:String] = [:], revisions: [String:Int] = [:]
    private var refreshingSources = false
    private var attemptedCredentials = Set<String>()
    init(store: MacDataStore) {
        self.store = store
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 15; configuration.timeoutIntervalForResource = 20
        session = URLSession(configuration: configuration, delegate: MacQuotaNoRedirect(), delegateQueue: nil)
    }
    func diagnostic() -> String { (UserDefaults.standard.string(forKey:"domestic_source_deepseek") == nil ? [note]:[]) .joined(separator:"\n") + "\n" + MacDeviceRegistry.providers.compactMap { sourceNotes[$0.0].map { $0 } }.joined(separator:"\n") }
    func diagnostic(provider: String) -> String { sourceNotes[provider] ?? (provider == "deepseek" ? note : "") }
    private var legacyDeepSeek: Bool { UserDefaults.standard.string(forKey:"domestic_fingerprint_deepseek") == nil && UserDefaults.standard.bool(forKey:"deepseek_quota_enabled") }
    func useSeparateDeepSeek() -> Bool {
        guard legacyDeepSeek || UserDefaults.standard.string(forKey:"domestic_fingerprint_deepseek") != nil else { return false }
        if UserDefaults.standard.string(forKey:"domestic_source_deepseek") != "separate" {
            revision += 1; revisions["deepseek",default:0] += 1
            store.clearDomestic("deepseek")
            UserDefaults.standard.set("separate",forKey:"domestic_source_deepseek")
        }
        return true
    }
    func saveCredential(_ credential: MacDomesticCredential, provider: String) throws {
        try credential.validate(provider:provider)
        let encoder = JSONEncoder(); encoder.outputFormatting = [.sortedKeys]; let bytes = try encoder.encode(credential)
        try MacKeychainAccess.save("domestic-provider-v1-" + provider, bytes)
        let fingerprint = MacTab5Protocol.sha256(bytes), key = "domestic_fingerprint_" + provider
        if UserDefaults.standard.string(forKey:key) != fingerprint { store.clearDomestic(provider); UserDefaults.standard.set(fingerprint,forKey:key) }
        UserDefaults.standard.set(credential.mode,forKey:"domestic_source_" + provider)
        credentials[provider] = credential; revisions[provider,default:0] += 1
        if provider == "deepseek" { revision += 1 }
    }
    func useConsole(_ provider: String) {
        guard MacDomesticSources.modes(provider).contains(where:{$0.0 == "console"}) else { return }
        guard UserDefaults.standard.string(forKey:"domestic_source_"+provider) != "console" else { return }
        revisions[provider,default:0] += 1; UserDefaults.standard.set("console",forKey:"domestic_source_"+provider)
        if provider == "deepseek" { revision += 1; store.clearDomestic("deepseek") }
        store.markDomesticStale(provider)
    }
    func refresh() async {
        guard !refreshingSources else { return }; refreshingSources = true; defer { refreshingSources = false }
        let deepSeekMode = UserDefaults.standard.string(forKey:"domestic_source_deepseek")
        let useLegacy = deepSeekMode == nil || (deepSeekMode == "separate" && legacyDeepSeek)
        if useLegacy { await refreshDeepSeek() }
        for (provider,name) in MacDeviceRegistry.providers {
            guard let mode = UserDefaults.standard.string(forKey:"domestic_source_" + provider), mode != "console" else { continue }
            if provider == "deepseek" && useLegacy { continue }
            let generation = revisions[provider,default:0]
            do {
                let credential: MacDomesticCredential
                if let cached = credentials[provider] { credential = cached }
                else { guard attemptedCredentials.insert(provider).inserted, let bytes = try MacKeychainAccess.read("domestic-provider-v1-" + provider) else { throw MacDomesticQuotaParser.Failure.invalid }; credential = try JSONDecoder().decode(MacDomesticCredential.self,from:bytes); credentials[provider] = credential }
                let request = try MacDomesticSources.request(provider,credential)
                sourceNotes[provider] = "\(name)：正在读取…"
                let (stream,response) = try await session.bytes(for:request)
                guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw MacDomesticQuotaParser.Failure.invalid }
                var data = Data(); for try await byte in stream { guard data.count < 1048576 else { throw MacDomesticQuotaParser.Failure.invalid }; data.append(byte) }
                guard revisions[provider,default:0] == generation else { continue }
                store.updateDomestic(try MacDomesticSources.parse(provider,mode:credential.mode,bytes:data,package:credential.package),deepSeekAPI:provider == "deepseek"); sourceNotes[provider] = "\(name)：官方账户数据已更新"
            } catch {
                guard revisions[provider,default:0] == generation else { continue }; store.markDomesticStale(provider); sourceNotes[provider] = "\(name)：读取未成功，保留最近成功结果；请检查授权或网络"
            }
        }
    }
    func saveKey(_ value: String) throws {
        let value = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (8...512).contains(value.utf8.count), value.utf8.allSatisfy({ (33...126).contains($0) }) else {
            throw MacDomesticQuotaParser.Failure.invalid
        }
        try MacKeychainAccess.save(Self.account, Data(value.utf8))
        let fingerprint = MacTab5Protocol.sha256(Data(value.utf8))
        if UserDefaults.standard.string(forKey: "deepseek_quota_key_hash") != fingerprint {
            store.clearDeepSeekForNewAccount()
            UserDefaults.standard.set(fingerprint, forKey: "deepseek_quota_key_hash")
        }
        key = value; loadedKey = true; revision += 1
        UserDefaults.standard.set(true, forKey: "deepseek_quota_enabled")
    }
    private func refreshDeepSeek() async {
        guard UserDefaults.standard.bool(forKey: "deepseek_quota_enabled"), !refreshing else { return }
        refreshing = true
        let requestedRevision = revision
        defer {
            refreshing = false
            if revision != requestedRevision { Task { await self.refresh() } }
        }
        if !loadedKey {
            loadedKey = true
            if let data = try? MacKeychainAccess.read(Self.account) { key = String(data: data, encoding: .utf8) }
        }
        guard let key else { note = "DeepSeek：无法读取授权，请重新保存 API Key"; return }
        note = "DeepSeek：正在查询…"
        do {
            var request = URLRequest(url: URL(string: "https://api.deepseek.com/user/balance")!)
            request.setValue("Bearer " + key, forHTTPHeaderField: "Authorization")
            let (data, response) = try await session.data(for: request)
            guard revision == requestedRevision else { return }
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                store.markDomesticStale("deepseek")
                note = "DeepSeek：查询未成功（HTTP \((response as? HTTPURLResponse)?.statusCode ?? 0)），保留已有数据"
                return
            }
            store.updateDomestic(try MacDomesticQuotaParser.deepSeek(data),deepSeekAPI:true)
            note = "DeepSeek：余额已更新"
        } catch {
            guard revision == requestedRevision else { return }
            store.markDomesticStale("deepseek")
            note = "DeepSeek：网络或数据异常，保留已有数据"
        }
    }
}
