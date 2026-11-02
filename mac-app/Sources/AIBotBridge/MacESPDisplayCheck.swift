import Foundation

enum MacESPDisplayCheck {
    // Read only local bridge responses; only aggregate change counts leave this process.
    static func observe(samples: Int = 20) throws -> [String:Int] {
        guard let token = PairingTokenStore.read() else { throw DeviceAdminError.unavailable }
        let port = ProcessInfo.processInfo.environment["AIBOT_HTTP_PORT"].flatMap(UInt16.init) ?? 8765
        let config = URLSessionConfiguration.ephemeral
        config.connectionProxyDictionary = [:]; config.httpShouldSetCookies = false; config.httpCookieStorage = nil
        let session = URLSession(configuration:config); defer { session.invalidateAndCancel() }
        func get(_ path: String) throws -> [String:Any] {
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/\(path)")!,timeoutInterval:5)
            request.setValue(token,forHTTPHeaderField:"X-AIBot-Token")
            let done = DispatchSemaphore(value:0); var value: [String:Any]?
            session.dataTask(with:request) { data,response,_ in
                defer { done.signal() }
                guard (response as? HTTPURLResponse)?.statusCode == 200, let data, data.count <= 1_048_576 else { return }
                value = (try? JSONSerialization.jsonObject(with:data)) as? [String:Any]
            }.resume()
            guard done.wait(timeout:.now()+6) == .success, let value else { throw DeviceAdminError.unconfirmed }; return value
        }
        var prior: [String:Data] = [:], counts: [String:Int] = ["samples":0]
        for index in 0..<max(1,samples) {
            let status = try get("status"), resources = try get("resources")
            let domestic = status["domesticQuotas"] as? [String:Any] ?? [:]
            let activity = status["domesticActivity"] as? [String:Any] ?? [:]
            let quotas = status["quotas"] as? [String:Any] ?? [:]
            func fields(_ object: Any?, _ names: [String]) -> [String:Any] {
                let value = object as? [String:Any] ?? [:]
                return value.filter { names.contains($0.key) }
            }
            let financial = ["plan","currency","planPercent","primaryPercent","weeklyPercent","balance","usedCost","primaryResetsAt","weeklyResetsAt","planResetsAt","stale"]
            let inputs: [String:Any] = [
                "policy":status["displayPolicy"] ?? [:],
                "deepseek":fields(domestic["deepSeek"],financial), "glm":fields(domestic["zhipu"],financial),
                "domesticTokens":activity["tokensToday"] ?? NSNull(),
                "codexQuota":fields(quotas["codex"],["plan","primaryPercent","weeklyPercent","primaryResetsAt","weeklyResetsAt","resetCreditsAvailable","resetCreditExpiresAt"]),
                "codexState":fields(status["codex"],["state","needsInput","completionActive","completionSequence"]),
                "resources":resources["resources"] ?? []]
            for (key,value) in inputs {
                let bytes = try JSONSerialization.data(withJSONObject:value,options:[.sortedKeys,.fragmentsAllowed])
                if let old = prior[key], old != bytes { counts[key,default:0] += 1 }
                prior[key] = bytes
            }
            counts["samples",default:0] += 1
            if index+1 < samples { Thread.sleep(forTimeInterval:2) }
        }
        return counts
    }
    static func run() throws -> [String:Any] {
        guard let token = PairingTokenStore.read() else { throw DeviceAdminError.unavailable }
        let port = ProcessInfo.processInfo.environment["AIBOT_HTTP_PORT"].flatMap(UInt16.init) ?? 8765
        let config = URLSessionConfiguration.ephemeral
        config.connectionProxyDictionary = [:]; config.httpShouldSetCookies = false; config.httpCookieStorage = nil
        let session = URLSession(configuration:config); defer { session.invalidateAndCancel() }
        func get(_ path: String) throws -> [String:Any] {
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/\(path)")!,timeoutInterval:5)
            request.setValue(token,forHTTPHeaderField:"X-AIBot-Token")
            let done = DispatchSemaphore(value:0); var value: [String:Any]?
            let task = session.dataTask(with:request) { data,response,_ in
                defer { done.signal() }
                guard (response as? HTTPURLResponse)?.statusCode == 200, let data, data.count <= 1_048_576 else { return }
                value = (try? JSONSerialization.jsonObject(with:data)) as? [String:Any]
            }
            task.resume(); guard done.wait(timeout:.now()+6) == .success, let value else { task.cancel(); throw DeviceAdminError.unconfirmed }; return value
        }
        let status = try get("status"), resources = try get("resources")
        let raw = status["codex"] as? [String:Any] ?? [:], policy = status["displayPolicy"] as? [String:Any] ?? [:]
        var state = raw.filter { ["state","needsInput","completionActive","completionAt","completionSequence"].contains($0.key) }
        if let epoch = status["epochUtc"] as? Int64, let completed = raw["completionAt"] as? Int64 { state["completionAgeSeconds"] = epoch - completed }
        var result: [String:Any] = ["codex":state,"displayPolicy":policy.filter { ["selectedMode","cycleEnabled","intervalSeconds","pages"].contains($0.key) },"resources":(resources["resources"] as? [[String:Any]] ?? []).map { $0.filter { ["kind","length","crc"].contains($0.key) } }]
        result["localPets"] = ["claude","codex"].map { owner -> [String:Any] in
            guard let pet = MacPetCache.shared.selection(owner), let bytes = try? pet.encode(), let asset = try? MacTab5Asset.rgb565(slot:owner == "claude" ? 0:1,width:pet.width,height:pet.height,delays:pet.delays,raw:pet.frames.reduce(into:Data()) { $0.append($1) }) else { return ["owner":owner,"loaded":false] }
            return ["owner":owner,"loaded":true,"width":pet.width,"height":pet.height,"frames":pet.frames.count,"delays":pet.delays,"espCRC":MacBinaryResourceProtocol.crc32(Array(bytes)),"tab5ID":asset.id]
        }
        return result
    }
}
