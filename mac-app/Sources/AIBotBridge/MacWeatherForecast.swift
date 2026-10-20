import Foundation

// Project the providers' public fields into the existing TAB5 forecast schema.
enum MacWeatherForecast {
    static func number(_ value: Any?, range: ClosedRange<Double> = -1e6...1e6) -> Double? {
        guard let n = MacDomesticQuotaParser.number(value), range.contains(n) else { return nil }
        return n
    }
    static func formatter(_ format: String, offset: Int = 0) -> DateFormatter {
        let f = DateFormatter(); f.locale = Locale(identifier: "en_US_POSIX")
        f.calendar = Calendar(identifier: .gregorian); f.timeZone = TimeZone(secondsFromGMT: offset)
        f.dateFormat = format; f.isLenient = false; return f
    }
    static func date(_ text: String, format: String, offset: Int = 0) -> Date? {
        let f = formatter(format, offset: offset)
        guard let value = f.date(from: text), f.string(from: value) == text else { return nil }
        return value
    }
    static func solar(_ value: Any?, day: String) -> String? {
        guard let text = value as? String else { return nil }
        if date(text, format: "HH:mm") != nil { return text }
        if text.hasPrefix(day + "T"), date(text, format: "yyyy-MM-dd'T'HH:mm") != nil {
            return String(text.suffix(5))
        }
        return nil
    }
    static func root(_ data: Data) throws -> [String: Any] {
        guard data.count <= 2_000_000, let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw MacQWeatherService.Failure.response
        }
        return root
    }
    static func addMeteo(_ data: Data, to value: inout WeatherSnapshot, now: Date) throws {
        let root = try root(data)
        let current = root["current"] as? [String: Any] ?? [:]
        value.pressureHpa = number(current["pressure_msl"], range: 1...1200)
        value.pressureKind = value.pressureHpa == nil ? nil : "sea_level"
        let offset = number(root["utc_offset_seconds"], range: -43200...50400).map(Int.init)
        value.utcOffsetSeconds = offset
        // Without an explicit city timezone, do not guess which hours are future.
        guard let offset else { return }
        let day = formatter("yyyy-MM-dd", offset: offset).string(from: now)
        let hours = root["hourly"] as? [String: Any] ?? [:]
        let days = root["daily"] as? [String: Any] ?? [:]
        func at(_ object: [String: Any], _ key: String, _ i: Int) -> Any? {
            guard let list = object[key] as? [Any], list.indices.contains(i) else { return nil }
            return list[i]
        }
        let startHour = floor((now.timeIntervalSince1970 + Double(offset)) / 3600) * 3600 - Double(offset)
        var seen = Set<String>()
        value.hourly = (hours["time"] as? [String] ?? []).enumerated().compactMap { i, text -> MacWeatherHour? in
            guard let stamp = date(text, format: "yyyy-MM-dd'T'HH:mm", offset: offset),
                  stamp.timeIntervalSince1970 >= startHour, seen.insert(text).inserted else { return nil }
            let code = number(at(hours, "weather_code", i), range: 0...999).map(Int.init)
            return MacWeatherHour(time: text, condition: code.map(MacDataService.condition) ?? "--",
                temperature: number(at(hours, "temperature_2m", i), range: -100...100),
                rainProbability: number(at(hours, "precipitation_probability", i), range: 0...100))
        }.sorted { $0.time < $1.time }.prefix(24).map { $0 }
        seen.removeAll()
        value.daily = (days["time"] as? [String] ?? []).enumerated().compactMap { i, text -> MacWeatherDay? in
            guard date(text, format: "yyyy-MM-dd") != nil, text >= day, seen.insert(text).inserted else { return nil }
            let code = number(at(days, "weather_code", i), range: 0...999).map(Int.init)
            return MacWeatherDay(date: text, condition: code.map(MacDataService.condition) ?? "--",
                low: number(at(days, "temperature_2m_min", i), range: -100...100),
                high: number(at(days, "temperature_2m_max", i), range: -100...100),
                rainProbability: number(at(days, "precipitation_probability_max", i), range: 0...100),
                precipitationMm: number(at(days, "precipitation_sum", i), range: 0...10000),
                sunrise: solar(at(days, "sunrise", i), day: text), sunset: solar(at(days, "sunset", i), day: text),
                daylightSeconds: number(at(days, "daylight_duration", i), range: 0...86400))
        }.sorted { $0.date < $1.date }.prefix(7).map { $0 }
        value.hourlyStale = false; value.dailyStale = false
    }
    static func qRoot(_ data: Data) throws -> [String: Any] {
        let r = try root(data)
        guard r["code"] as? String == "200" else { throw MacQWeatherService.Failure.response }
        return r
    }
    // Open-Meteo supplies the US EPA scale, not the Chinese AQI scale.
    static func usAirLabel(_ index: Int) -> String {
        switch index {
        case ...50: return "优"
        case ...100: return "良"
        case ...150: return "敏感人群不健康"
        case ...200: return "不健康"
        case ...300: return "非常不健康"
        default: return "危险"
        }
    }
    static func addMeteoAir(_ data: Data?, to value: inout WeatherSnapshot) {
        guard let data, let r = try? root(data), let c = r["current"] as? [String: Any] else { return }
        if let aqi = number(c["us_aqi"], range: 0...10000) {
            value.airQualityIndex = Int(aqi.rounded())
            value.airQualityLabel = usAirLabel(value.airQualityIndex!)
            value.airQualityStandard = "us-epa"
        }
        value.pm25 = number(c["pm2_5"], range: 0...100000)
    }
    static func addQAir(_ data: Data?, to value: inout WeatherSnapshot) {
        guard let data, let r = try? root(data) else { return }
        // The existing device field uses integer AQI. Never round fractional QAQI into that scale.
        let indexes = r["indexes"] as? [[String: Any]] ?? []
        if let item = indexes.first(where: { $0["code"] as? String == "us-epa" }),
           let aqi = number(item["aqi"], range: 0...10000) {
            value.airQualityIndex = Int(aqi.rounded())
            value.airQualityStandard = "us-epa"
            value.airQualityLabel = usAirLabel(value.airQualityIndex!)
        }
        let pollutants = r["pollutants"] as? [[String: Any]] ?? []
        if let pm = pollutants.first(where: { $0["code"] as? String == "pm2p5" }),
           let concentration = pm["concentration"] as? [String: Any],
           ["μg/m3", "µg/m3", "μg/m³", "µg/m³"].contains(concentration["unit"] as? String ?? "") {
            value.pm25 = number(concentration["value"], range: 0...100000)
        }
    }
    static func qweather(current: Data, daily: Data, hourly: Data?, city: String, now: Date = Date()) throws -> WeatherSnapshot {
        let currentRoot = try qRoot(current), dailyRoot = try qRoot(daily)
        guard let c = currentRoot["now"] as? [String: Any], let temp = number(c["temp"], range: -100...100),
              let humidity = number(c["humidity"], range: 0...100), let condition = c["text"] as? String,
              let rows = dailyRoot["daily"] as? [[String: Any]], !rows.isEmpty else { throw MacQWeatherService.Failure.response }
        let observed = c["obsTime"] as? String ?? ""
        let offsetText = String(observed.suffix(6))
        let offset = observed.hasSuffix("Z") ? 0 : timeOffset(offsetText)
        guard let offset, formatter("yyyy-MM-dd'T'HH:mmXXXXX").date(from: observed) != nil else { throw MacQWeatherService.Failure.response }
        let today = formatter("yyyy-MM-dd", offset: offset).string(from: now)
        var seen = Set<String>()
        let days = rows.compactMap { row -> MacWeatherDay? in
            guard let d = row["fxDate"] as? String, date(d, format: "yyyy-MM-dd") != nil,
                  d >= today, seen.insert(d).inserted else { return nil }
            return MacWeatherDay(date: d, condition: row["textDay"] as? String ?? "--",
                low: number(row["tempMin"], range: -100...100), high: number(row["tempMax"], range: -100...100),
                rainProbability: nil, precipitationMm: number(row["precip"], range: 0...10000),
                sunrise: solar(row["sunrise"], day: d), sunset: solar(row["sunset"], day: d), daylightSeconds: nil)
        }.sorted { $0.date < $1.date }.prefix(7).map { $0 }
        guard let first = days.first, first.date == today, let high = first.high, let low = first.low else {
            throw MacQWeatherService.Failure.response
        }
        var value = WeatherSnapshot(city: city, condition: String(condition.prefix(80)), temperature: temp,
            high: high, low: low, humidity: Int(humidity), weatherCode: qCode(c["icon"] as? String),
            pm25: nil, airQualityIndex: nil, source: "qweather", updatedAt: now, stale: false,
            utcOffsetSeconds: offset, daily: days, dailyStale: false)
        value.pressureHpa = number(c["pressure"], range: 1...1200)
        value.pressureKind = value.pressureHpa == nil ? nil : "station"
        seen.removeAll()
        if let hourly, let h = try? qRoot(hourly), let rows = h["hourly"] as? [[String: Any]] {
            value.hourly = rows.compactMap { row -> MacWeatherHour? in
                guard let t = row["fxTime"] as? String,
                      let stamp = formatter("yyyy-MM-dd'T'HH:mmXXXXX").date(from: t),
                      stamp >= now.addingTimeInterval(-3600), seen.insert(t).inserted else { return nil }
                return MacWeatherHour(time: t, condition: String((row["text"] as? String ?? "--").prefix(80)),
                    temperature: number(row["temp"], range: -100...100), rainProbability: number(row["pop"], range: 0...100))
            }.sorted { $0.time < $1.time }.prefix(24).map { $0 }
            value.hourlyStale = false
        }
        return value
    }
    static func timeOffset(_ text: String) -> Int? {
        let chars = Array(text)
        guard chars.count == 6, chars[0] == "+" || chars[0] == "-", chars[3] == ":",
              let h = Int(String(chars[1...2])), let m = Int(String(chars[4...5])), h <= 14, m < 60 else { return nil }
        let result = (h * 3600 + m * 60) * (chars[0] == "-" ? -1 : 1)
        return (-43200...50400).contains(result) ? result : nil
    }
    private static func qCode(_ text: String?) -> Int {
        guard let code = text.flatMap(Int.init) else { return -1 }
        switch code {
        case 100, 150: return 0
        case 101...103, 151...153: return 2
        case 104: return 3
        case 302...304: return 95
        case 300...399: return 61
        case 400...499: return 71
        case 500...515: return 45
        default: return -1
        }
    }
}
