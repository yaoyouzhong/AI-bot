import AppKit
import Foundation

struct MacResourcePayload {
    let kind: MacBinaryResourceKind
    let revision: Int
    let data: Data
}

final class MacLocalizedTextResources {
    private let lock = NSLock()
    private var weatherKey = ""
    private var stocksKey = ""
    private var weatherRevision = 0
    private var stocksRevision = 0
    private var musicRevision = 0
    private var musicCoverRevision = 0
    private var weatherData = Data()
    private var stocksData = Data()
    private var musicKey = ""
    private var musicData = Data()
    private var musicCoverData = Data()
    private var weatherDetailsKey = ""
    private var weatherDetails: [MacResourcePayload] = []

    func capture(weather: WeatherSnapshot?, stocks: StockSnapshot?,
                 music: MusicSnapshot?, musicCover: Data?) -> [MacResourcePayload] {
        lock.lock()
        defer { lock.unlock() }
        if let weather {
            let formatter=DateFormatter();formatter.locale=Locale(identifier:"zh_CN");formatter.dateFormat="M月d日 EEE"
            let date=formatter.string(from:Date()),air=weather.airQualityIndex.map{"AQI \($0)"} ?? ""
            let detailsKey=weather.city+"|"+weather.condition+"|"+air+"|"+date
            if detailsKey != weatherDetailsKey {
                let parts:[(MacBinaryResourceKind,Int,Int,String,CGFloat)]=[(.weatherHeader,122,26,weather.city,18),(.weatherDate,190,30,date,19),(.weatherAir,100,30,air+" "+weather.condition,12)]
                let rendered=parts.compactMap { kind,width,height,text,size -> MacResourcePayload? in
                    guard let data=MacRgb565Renderer.renderLines(width:width,height:height,lines:[text],fontPixels:size,rowHeight:height) else{return nil}
                    return MacResourcePayload(kind:kind,revision:Int(MacBinaryResourceProtocol.crc32(Array(data))),data:data)
                }
                if rendered.count==parts.count {weatherDetails=rendered;weatherDetailsKey=detailsKey}
            }
            let key = weather.city + "\n" + weather.condition
            if key != weatherKey,
               let rendered = MacRgb565Renderer.renderLines(
                width: 232, height: 24, lines: [weather.city + "  " + weather.condition],
                fontPixels: 20, rowHeight: 24) {
                weatherKey = key
                weatherData = rendered
                weatherRevision += 1
            }
        }
        if let stocks {
            let names = stocks.quotes.prefix(20).map(\.name)
            let key = names.joined(separator: "\n")
            if key != stocksKey,
               let rendered = MacRgb565Renderer.renderLines(
                width: 156, height: 400, lines: names, fontPixels: 12, rowHeight: 20, alignment: .right, weight: .regular) {
                stocksKey = key
                stocksData = rendered
                stocksRevision += 1
            }
        }
        if let music, !music.title.isEmpty {
            let key = music.title + "\n" + music.artist
            if key != musicKey,
               let rendered = MacRgb565Renderer.renderLines(
                width: 232, height: 44, lines: [music.title, music.artist],
                fontPixels: 17, rowHeight: 22, alignment: .center) {
                musicKey = key
                musicData = rendered
                musicRevision += 1
            }
            let nextCover = music.hasArtwork != false && musicCover?.count == 112 * 112 * 2 ? musicCover! : Data()
            if nextCover != musicCoverData {
                musicCoverData=nextCover
                musicCoverRevision += 1
            }
        } else {
            musicKey="";musicData=Data();musicCoverData=Data()
        }
        var result: [MacResourcePayload] = []
        if musicRevision > 0 && !musicData.isEmpty {
            result.append(MacResourcePayload(kind: .textBitmap,
                                             revision: musicRevision, data: musicData))
        }
        if musicCoverRevision > 0 && !musicCoverData.isEmpty {
            result.append(MacResourcePayload(kind: .musicCover,
                                             revision: musicCoverRevision, data: musicCoverData))
        }
        if weatherRevision > 0 {
            result.append(MacResourcePayload(kind: .weatherText,
                                             revision: weatherRevision, data: weatherData))
        }
        if stocksRevision > 0 {
            result.append(MacResourcePayload(kind: .stockLabels,
                                             revision: stocksRevision, data: stocksData))
        }
        return result+weatherDetails
    }
}

enum MacRgb565Renderer {
    static func renderLines(width: Int, height: Int, lines: [String],
                            fontPixels: CGFloat, rowHeight: Int,
                            alignment: NSTextAlignment = .left, weight: NSFont.Weight = .bold) -> Data? {
        guard width > 0, height > 0, rowHeight > 0,
              let bitmap = NSBitmapImageRep(
                bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height,
                bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                colorSpaceName: .deviceRGB, bytesPerRow: width * 4, bitsPerPixel: 32),
              let context = NSGraphicsContext(bitmapImageRep: bitmap) else { return nil }

        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = context
        NSColor.black.setFill()
        NSRect(x: 0, y: 0, width: CGFloat(width), height: CGFloat(height)).fill()
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = alignment
        paragraph.lineBreakMode = .byTruncatingTail
        let attributes: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: fontPixels, weight: weight),
            .foregroundColor: NSColor.white,
            .paragraphStyle: paragraph
        ]
        for (index, text) in lines.enumerated() where (index + 1) * rowHeight <= height {
            let y = height - (index + 1) * rowHeight
            let rect = NSRect(x: 0, y: CGFloat(y), width: CGFloat(width), height: CGFloat(rowHeight))
            (text as NSString).draw(with: rect,
                                    options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine],
                                    attributes: attributes)
        }
        context.flushGraphics()
        NSGraphicsContext.restoreGraphicsState()
        return encodeRgb565(bitmap, width: width, height: height)
    }

    static func rgb565(red: UInt8, green: UInt8, blue: UInt8) -> UInt16 {
        UInt16(red & 0xF8) << 8 | UInt16(green & 0xFC) << 3 | UInt16(blue) >> 3
    }

    private static func encodeRgb565(_ bitmap: NSBitmapImageRep, width: Int, height: Int) -> Data {
        var result = Data(capacity: width * height * 2)
        var pixel = [Int](repeating: 0, count: 4)
        for outputY in 0..<height {
            let sourceY = height - 1 - outputY
            for x in 0..<width {
                pixel.withUnsafeMutableBufferPointer {
                    bitmap.getPixel($0.baseAddress!, atX: x, y: sourceY)
                }
                let value = rgb565(red: UInt8(clamping: pixel[0]),
                                   green: UInt8(clamping: pixel[1]),
                                   blue: UInt8(clamping: pixel[2]))
                result.append(UInt8(truncatingIfNeeded: value))
                result.append(UInt8(truncatingIfNeeded: value >> 8))
            }
        }
        return result
    }
}
