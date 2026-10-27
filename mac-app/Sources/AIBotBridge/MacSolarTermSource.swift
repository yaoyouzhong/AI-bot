import Foundation
import PDFKit

// Original reader of public date facts in Purple Mountain Observatory calendars.
enum MacSolarTermSource {
    static let catalog = URL(string: "https://pmo.cas.cn/xwdt2019/kpdt2019/202203/t20220309_6386774.html")!
    static func allowed(_ url: URL) -> Bool {
        url.scheme == "https" && ["pmo.cas.cn", "www.pmo.cas.cn"].contains(url.host ?? "")
    }
    private static func matches(_ pattern: String, _ text: String) throws -> [NSTextCheckingResult] {
        try NSRegularExpression(pattern: pattern, options: [.dotMatchesLineSeparators, .caseInsensitive])
            .matches(in: text, range: NSRange(text.startIndex..., in: text))
    }
    static func documents(_ data: Data) throws -> [Int: URL] {
        guard data.count <= 524288, let html = String(data: data, encoding: .utf8) else { throw MacTab5Error.invalidPacket }
        var result: [Int: URL] = [:]
        for match in try matches("<a\\b[^>]*href=[\"']([^\"']+)[\"'][^>]*>(.*?)</a>", html) {
            let link = (html as NSString).substring(with: match.range(at: 1))
            let body = (html as NSString).substring(with: match.range(at: 2))
            let label = body.replacingOccurrences(of: "<[^>]*>", with: "", options: .regularExpression).filter { !$0.isWhitespace }
            guard let yearMatch = try matches("^《(\\d{4})年日历资料》$", label).first,
                  let year = Int((label as NSString).substring(with: yearMatch.range(at: 1))) else { continue }
            guard (1901...2099).contains(year), let url = URL(string: link, relativeTo: catalog)?.absoluteURL,
                  allowed(url), url.path.lowercased().hasSuffix(".pdf"), result[year] == nil || result[year] == url else { throw MacTab5Error.invalidPacket }
            result[year] = url
        }
        guard !result.isEmpty else { throw MacTab5Error.invalidPacket }
        return result
    }
    static func table(_ data: Data, year: Int) throws -> Data {
        guard data.count <= 4194304, data.starts(with: Data("%PDF-".utf8)),
              let document = PDFDocument(data: data), !document.isLocked, (1...16).contains(document.pageCount),
              let text = document.string, text.count <= 131072 else { throw MacTab5Error.invalidPacket }
        return try table(text: text, year: year)
    }
    static func table(text: String, year: Int) throws -> Data {
        guard text.count <= 131072, (1901...2099).contains(year) else { throw MacTab5Error.invalidPacket }
        let compact = text.precomposedStringWithCompatibilityMapping.filter { !$0.isWhitespace }.replacingOccurrences(of: "○", with: "〇").replacingOccurrences(of: "零", with: "〇")
        let digits = Array("〇一二三四五六七八九")
        let yearText = String(String(year).compactMap { $0.wholeNumberValue.map { digits[$0] } })
        guard compact.hasPrefix(yearText + "年日历资料中国科学院紫金山天文台编制") else { throw MacTab5Error.invalidPacket }
        let headers = try matches("([〇一二三四五六七八九]{4})年([一二三四五六七八九十]{1,3})月[大小平]", compact)
        let months = ["一","二","三","四","五","六","七","八","九","十","十一","十二"]
        let names = ["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至",
                     "小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
        var dates: [Int] = [], seenMonths = 0
        let string = compact as NSString
        for (index, header) in headers.enumerated() where string.substring(with: header.range(at: 1)) == yearText {
            guard seenMonths < 12, string.substring(with: header.range(at: 2)) == months[seenMonths] else { throw MacTab5Error.invalidPacket }
            let start = NSMaxRange(header.range), end = index + 1 < headers.count ? headers[index + 1].range.location : string.length
            let section = string.substring(with: NSRange(location: start, length: end - start))
            let terms = try matches("【(" + names.joined(separator: "|") + ")】(\\d{1,2})日(\\d{1,2})时(\\d{1,2})分", section)
            guard terms.count == 2 else { throw MacTab5Error.invalidPacket }
            for term in terms {
                let value = section as NSString
                guard value.substring(with: term.range(at: 1)) == names[dates.count],
                      let day = Int(value.substring(with: term.range(at: 2))), (1...28).contains(day),
                      let hour = Int(value.substring(with: term.range(at: 3))), (0...23).contains(hour),
                      let minute = Int(value.substring(with: term.range(at: 4))), (0...59).contains(minute) else { throw MacTab5Error.invalidPacket }
                dates.append((seenMonths + 1) * 100 + day)
            }
            seenMonths += 1
        }
        guard seenMonths == 12, dates.count == 24, dates == dates.sorted(), Set(dates).count == 24 else { throw MacTab5Error.invalidPacket }
        let cacheNames = ["小寒","大寒","立春","雨水","驚蟄","春分","清明","穀雨","立夏","小滿","芒種","夏至",
                          "小暑","大暑","立秋","處暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
        return Data(("# AI-bot solar terms; source=PMO; UTC+8; year=\(year)\n" + zip(cacheNames, dates).map {
            "\(year)年\($0.1 / 100)月\($0.1 % 100)日 - - \($0.0)"
        }.joined(separator: "\n")).utf8)
    }
}
