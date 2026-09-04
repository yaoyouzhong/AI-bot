import Foundation

actor MacQuotaService {
    private let store: MacDataStore
    private let session: URLSession

    init(store: MacDataStore) {
        self.store = store
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 15
        configuration.timeoutIntervalForResource = 20
        session = URLSession(configuration: configuration)
    }

    func refresh() async {
        async let claude = fetchClaude()
        async let codex = fetchCodex()
        let values = await (claude, codex)
        store.mergeQuotas(claude: values.0, codex: values.1)
    }

    static func parseClaude(_ data: Data, credentialPlan: String? = nil,
                            now: Date = Date()) throws -> ProviderQuotaSnapshot {
        let root = try object(data)
        let primary = dictionary(root["five_hour"])
        let weekly = dictionary(root["seven_day"])
        return ProviderQuotaSnapshot(
            provider: "claude",
            plan: text(root["plan_type"]) ?? text(root["subscription_type"]) ?? credentialPlan,
            primaryPercent: number(primary?["utilization"]),
            primaryResetsAt: date(primary?["resets_at"]),
            weeklyPercent: number(weekly?["utilization"]),
            weeklyResetsAt: date(weekly?["resets_at"]),
            resetCreditsAvailable: nil,
            resetCreditExpiresAt: [],
            updatedAt: now,
            stale: false)
    }

    static func parseCodex(_ usage: Data, credits: Data? = nil, credentialPlan: String? = nil,
                           now: Date = Date()) throws -> ProviderQuotaSnapshot {
        let root = try object(usage)
        guard let rateLimit = dictionary(root["rate_limit"]) else { throw QuotaError.invalidResponse }
        var primaryPercent: Double?
        var primaryReset: Date?
        var weeklyPercent: Double?
        var weeklyReset: Date?
        for key in ["primary_window", "secondary_window"] {
            guard let window = dictionary(rateLimit[key]) else { continue }
            if (number(window["limit_window_seconds"]) ?? 0) >= 172_800 {
                weeklyPercent = number(window["used_percent"])
                weeklyReset = unixDate(window["reset_at"])
            } else {
                primaryPercent = number(window["used_percent"])
                primaryReset = unixDate(window["reset_at"])
            }
        }

        var available = integer(dictionary(root["rate_limit_reset_credits"])?["available_count"])
        var expirations: [Int64] = []
        if let credits {
            let creditRoot = try object(credits)
            available = integer(creditRoot["available_count"]) ?? available
            for row in creditRoot["credits"] as? [[String: Any]] ?? []
                where text(row["status"])?.lowercased() == "available" {
                if let expiration = date(row["expires_at"]) {
                    expirations.append(Int64(expiration.timeIntervalSince1970))
                }
            }
            expirations.sort()
        }
        return ProviderQuotaSnapshot(
            provider: "codex",
            plan: text(root["plan_type"]) ?? credentialPlan,
            primaryPercent: primaryPercent,
            primaryResetsAt: primaryReset,
            weeklyPercent: weeklyPercent,
            weeklyResetsAt: weeklyReset,
            resetCreditsAvailable: available,
            resetCreditExpiresAt: expirations,
            updatedAt: now,
            stale: false)
    }

    private func fetchClaude() async -> ProviderQuotaSnapshot? {
        guard let credential = Self.readClaudeCredential() else { return nil }
        var request = URLRequest(url: URL(string: "https://api.anthropic.com/api/oauth/usage")!)
        request.setValue("Bearer \(credential.token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("oauth-2025-04-20", forHTTPHeaderField: "anthropic-beta")
        request.setValue("claude-code/2.1.0", forHTTPHeaderField: "User-Agent")
        do {
            let (data, response) = try await session.data(for: request)
            guard (response as? HTTPURLResponse)?.statusCode == 200 else { return nil }
            return try Self.parseClaude(data, credentialPlan: credential.plan)
        } catch {
            return nil
        }
    }

    private func fetchCodex() async -> ProviderQuotaSnapshot? {
        guard let credential = Self.readCodexCredential() else { return nil }
        do {
            guard let usage = try await sendCodex(
                "https://chatgpt.com/backend-api/wham/usage", credential: credential) else { return nil }
            let credits = try await sendCodex(
                "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits", credential: credential)
            return try Self.parseCodex(usage, credits: credits, credentialPlan: credential.plan)
        } catch {
            return nil
        }
    }

    private func sendCodex(_ address: String, credential: CodexCredential) async throws -> Data? {
        var request = URLRequest(url: URL(string: address)!)
        request.setValue("Bearer \(credential.token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("AI-bot/0.1", forHTTPHeaderField: "User-Agent")
        if let accountID = credential.accountID {
            request.setValue(accountID, forHTTPHeaderField: "ChatGPT-Account-Id")
        }
        let (data, response) = try await session.data(for: request)
        return (response as? HTTPURLResponse)?.statusCode == 200 ? data : nil
    }

    private static func readClaudeCredential() -> (token: String, plan: String?)? {
        let path = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent(".claude/.credentials.json")
        guard let data = try? Data(contentsOf: path),
              let root = try? object(data),
              let oauth = dictionary(root["claudeAiOauth"]),
              let token = text(oauth["accessToken"]),
              !token.isEmpty else { return nil }
        return (token, text(oauth["subscriptionType"]) ?? text(oauth["subscription_type"]))
    }

    private static func readCodexCredential() -> CodexCredential? {
        let path = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex/auth.json")
        guard let data = try? Data(contentsOf: path),
              let root = try? object(data),
              let tokens = dictionary(root["tokens"]),
              let token = text(tokens["access_token"]),
              !token.isEmpty,
              !jwtExpired(token) else { return nil }
        var accountID = text(tokens["account_id"])
        var plan: String?
        if let idToken = text(tokens["id_token"]),
           let claims = jwtClaims(idToken),
           let auth = dictionary(claims["https://api.openai.com/auth"]) {
            accountID = accountID ?? text(auth["chatgpt_account_id"])
            plan = text(auth["chatgpt_plan_type"])
        }
        return CodexCredential(token: token, accountID: accountID, plan: plan)
    }

    private static func jwtExpired(_ token: String) -> Bool {
        guard let claims = jwtClaims(token), let expiration = number(claims["exp"]) else { return false }
        return expiration <= Date().timeIntervalSince1970 + 60
    }

    private static func jwtClaims(_ token: String) -> [String: Any]? {
        let parts = token.split(separator: ".")
        guard parts.count >= 2 else { return nil }
        var payload = String(parts[1]).replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        payload += String(repeating: "=", count: (4 - payload.count % 4) % 4)
        guard let data = Data(base64Encoded: payload) else { return nil }
        return try? object(data)
    }

    private static func object(_ data: Data) throws -> [String: Any] {
        guard let value = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw QuotaError.invalidResponse
        }
        return value
    }

    private static func dictionary(_ value: Any?) -> [String: Any]? { value as? [String: Any] }
    private static func text(_ value: Any?) -> String? { value as? String }

    private static func number(_ value: Any?) -> Double? {
        if let number = value as? NSNumber { return number.doubleValue }
        if let text = value as? String { return Double(text) }
        return nil
    }

    private static func integer(_ value: Any?) -> Int? {
        guard let number = number(value) else { return nil }
        return Int(number)
    }

    private static func date(_ value: Any?) -> Date? {
        guard let text = text(value) else { return nil }
        let formatter = ISO8601DateFormatter()
        if let parsed = formatter.date(from: text) { return parsed }
        formatter.formatOptions.insert(.withFractionalSeconds)
        return formatter.date(from: text)
    }

    private static func unixDate(_ value: Any?) -> Date? {
        guard let seconds = number(value) else { return nil }
        return Date(timeIntervalSince1970: seconds)
    }

    private struct CodexCredential {
        let token: String
        let accountID: String?
        let plan: String?
    }

    private enum QuotaError: Error { case invalidResponse }
}
