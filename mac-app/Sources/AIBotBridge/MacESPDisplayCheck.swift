import Foundation

enum MacESPDisplayCheck {
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
