import AppKit

// ESP8266's existing 100x30 resource: a 42px air badge, 6px gap, 52px condition.
// This presentation does not change the source AQI value or its standard.
enum MacWeatherAirStyle {
    static func category(label: String?, index: Int?) -> String {
        let label = label ?? ""
        if label.contains("优") || label.lowercased() == "excellent" { return "优" }
        if label.contains("良") || label.lowercased() == "good" { return "良" }
        if label.contains("轻") { return "轻度" }
        if label.contains("中") { return "中度" }
        if label.contains("污染") { return "污染" }
        guard let index, index >= 0 else { return "--" }
        switch index { case ...50:return "优";case ...100:return "良";case ...150:return "轻度";case ...200:return "中度";default:return "污染" }
    }
    static func condition(_ value: String) -> String {
        if value == "毛毛雨" { return "细雨" }
        if value.count <= 2 { return value }
        for (match,label) in [("雷","雷雨"),("阵雨","阵雨"),("暴雨","暴雨"),("大雨","大雨"),("中雨","中雨"),("雨","小雨"),("暴雪","暴雪"),("大雪","大雪"),("中雪","中雪"),("雪","小雪"),("霾","霾"),("雾","雾"),("云","多云")] {
            if value.contains(match) { return label }
        }
        return String(value.prefix(2))
    }
    private static func color(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat) -> NSColor { NSColor(srgbRed:r/255,green:g/255,blue:b/255,alpha:1) }
    static func airColor(_ category: String) -> NSColor {
        switch category { case "优":return color(45,227,123);case "良":return color(255,200,61);case "轻度":return color(255,154,61);case "中度":return color(255,91,71);default:return color(255,72,92) }
    }
    static func conditionColor(_ value: String) -> NSColor {
        if value.contains("晴") { return color(255,215,0) }
        if value.contains("雨") || value.contains("雷") { return color(0,191,255) }
        if value.contains("雪") { return color(224,255,255) }
        if value.contains("沙") || value.contains("尘") { return color(255,165,0) }
        if value.contains("雾") || value.contains("霾") { return color(211,211,211) }
        return color(220,220,220)
    }
}
