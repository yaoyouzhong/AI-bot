import Foundation

enum MacESPStatusValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name: String, _ ok: Bool) throws { guard ok else { throw MacTab5LocalError.invalid("ESP status: " + name) }; count += 1 }
        let now = Date(timeIntervalSince1970:1_800_000_000)
        let hours = (0..<48).map { MacWeatherHour(time:"2026-10-26T\($0):00",condition:"多云转小雨",temperature:20,rainProbability:40) }
        let days = (0..<14).map { MacWeatherDay(date:"2026-10-\($0)",condition:"多云",low:15,high:25,rainProbability:40,precipitationMm:3,sunrise:"06:00",sunset:"18:00",daylightSeconds:43200) }
        let weather = WeatherSnapshot(city:"中文城市",condition:"多云",temperature:20,high:25,low:15,humidity:60,weatherCode:3,source:"fixture",updatedAt:now,stale:false,hourly:hours,daily:days)
        let quotes = (0..<20).map { StockQuote(symbol:"sh\($0)",code:"600\($0)",name:"中文股票名称\($0)",price:"123.45",changePercent:"0.00",trend:0) }
        let policy = MacDisplayPolicy(selectedMode:"stocks",cycleEnabled:false,intervalSeconds:15,pages:["codex","claude","weather","stocks"])
        let snapshot = MacStatusSnapshot(version:1,time:"12:34:56",epochUtc:1_800_000_000,utcOffsetSeconds:28800,capturedAt:now,codex:.init(state:"working",ageSeconds:0),claude:.init(state:"idle",ageSeconds:0),musicPlaying:false,weather:weather,stocks:.init(quotes:quotes,updatedAt:now,stale:false),systemMetrics:.init(cpuPercent:0,memoryPercent:25,uploadBytesPerSecond:0,downloadBytesPerSecond:0,updatedAt:now,samples:Array(repeating:.init(upload:10,download:20),count:120)),quotas:nil,music:nil,displayPolicy:policy)
        let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601; encoder.outputFormatting = [.sortedKeys]
        let original = try encoder.encode(snapshot)
        try check("oversize regression fixture",original.count > 6144)
        guard let frame = SerialBridge.statusFrame(snapshot), let root = try JSONSerialization.jsonObject(with:Data(frame.dropFirst(7).dropLast())) as? [String:Any], let data = root["data"] as? [String:Any] else { throw MacTab5LocalError.invalid("ESP status missing frame") }
        try check("complete bounded heartbeat",frame.count <= 6144 && root["version"] as? Int == 1 && root["type"] as? String == "status")
        let w = data["weather"] as? [String:Any], m = data["systemMetrics"] as? [String:Any]
        try check("forecast omitted only in projection",w?["hourly"] == nil && w?["daily"] == nil && w?["city"] as? String == "中文城市")
        try check("metrics timestamp and zero retained",m?["samples"] == nil && m?["updatedAt"] != nil && m?["cpuPercent"] as? Int == 0)
        let q = (data["stocks"] as? [String:Any])?["quotes"] as? [[String:Any]]
        try check("all stock values retained",q?.count == 20 && q?.first?["price"] as? String == "123.45" && q?.first?["trend"] as? Int == 0 && q?.first?["name"] == nil)
        try check("display choices retained",(data["displayPolicy"] as? [String:Any])?["pages"] as? [String] == policy.pages && (data["displayPolicy"] as? [String:Any])?["cycleEnabled"] as? Bool == false)
        try check("shared snapshot unmodified",try encoder.encode(snapshot) == original && snapshot.stocks?.quotes.first?.name == "中文股票名称0")
        let text = String(repeating:"中文🙂",count:40)
        let projected = MacESPStatus.project(["music":["title":text,"artist":"歌手","album":"专辑"],"codex":["tokensToday":0]])
        try check("scalar boundary",(projected["music"] as? [String:Any])?["title"] as? String == String(String.UnicodeScalarView(text.unicodeScalars.prefix(48))))
        try check("music metadata removed",(projected["music"] as? [String:Any])?["album"] == nil)
        let suite = "AI-bot.esp-restore-test." + UUID().uuidString
        let defaults = UserDefaults(suiteName:suite)!; defer { defaults.removePersistentDomain(forName:suite) }
        defaults.set("stocks",forKey:"display_mode"); defaults.set(false,forKey:"display_cycle_enabled")
        defaults.set(policy.pages,forKey:"display_cycle_pages"); defaults.set(30,forKey:"display_cycle_interval_seconds"); defaults.set(79,forKey:"fixture_tab5_brightness")
        MacDisplayPolicy.restoreCycleDefault(defaults:defaults)
        try check("restore auto cycle",defaults.string(forKey:"display_mode") == "auto" && defaults.bool(forKey:"display_cycle_enabled"))
        try check("restore preserves order interval and other settings",defaults.stringArray(forKey:"display_cycle_pages") == policy.pages && defaults.integer(forKey:"display_cycle_interval_seconds") == 30 && defaults.integer(forKey:"fixture_tab5_brightness") == 79)
        let completion = ToolState(state:"idle",ageSeconds:0,completionActive:true,completionSequence:7,completionAt:100)
        for age: Int64 in [0,11,12,60] {
            let display = MacDisplayPolicy.displayCodexState(completion,epoch:100+age)
            try check("completion interruption age \(age)",display.completionActive == (age < 12) && display.completionSequence == 7 && completion.completionActive)
        }
        var waiting = completion; waiting.needsInput = true
        try check("display expiry does not dismiss permission request",MacDisplayPolicy.displayCodexState(waiting,epoch:160).needsInput)
        return count
    }
}
