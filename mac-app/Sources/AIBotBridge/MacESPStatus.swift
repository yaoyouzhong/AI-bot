import Foundation

// ESP8266 has a bounded JSON line; TAB5-only series and bitmap label text
// must not consume its heartbeat budget. Keep the shared snapshot unchanged.
enum MacESPStatus {
    static func project(_ input: [String: Any]) -> [String: Any] {
        var data = input
        for key in ["capturedAt", "time", "musicPlaying", "quotaTrend"] { data.removeValue(forKey:key) }
        if var weather = data["weather"] as? [String:Any] {
            for key in ["hourly", "daily", "hourlyStale", "dailyStale"] { weather.removeValue(forKey:key) }
            data["weather"] = weather
        }
        if var metrics = data["systemMetrics"] as? [String:Any] { metrics.removeValue(forKey:"samples"); data["systemMetrics"] = metrics }
        if var activity = data["domesticActivity"] as? [String:Any] { activity.removeValue(forKey:"providers"); data["domesticActivity"] = activity }
        if var stocks = data["stocks"] as? [String:Any], let quotes = stocks["quotes"] as? [[String:Any]] {
            stocks["quotes"] = quotes.prefix(20).map { quote in
                var value = quote; value.removeValue(forKey:"name"); value.removeValue(forKey:"symbol"); return value
            }
            data["stocks"] = stocks
        }
        if var music = data["music"] as? [String:Any] {
            for key in ["album", "artworkWidth", "artworkHeight"] { music.removeValue(forKey:key) }
            data["music"] = music
        }
        return prune(data) as! [String:Any]
    }
    private static func prune(_ value: Any) -> Any {
        if let array = value as? [Any] { return array.map(prune) }
        guard var object = value as? [String:Any] else { return value }
        object.removeValue(forKey:"source"); object.removeValue(forKey:"provider")
        if object["cpuPercent"] == nil { object.removeValue(forKey:"updatedAt") }
        let limits = ["plan":24, "city":32, "condition":16, "title":48, "artist":48, "code":20, "price":20, "changePercent":20]
        for (key, item) in object {
            if let limit = limits[key], let text = item as? String { object[key] = String(String.UnicodeScalarView(text.unicodeScalars.prefix(limit))) }
            else { object[key] = prune(item) }
        }
        return object
    }
}
