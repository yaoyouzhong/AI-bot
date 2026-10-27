import AppKit
import CoreText
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
            let date=formatter.string(from:Date()),air=MacWeatherAirStyle.category(label:weather.airQualityLabel,index:weather.airQualityIndex)
            let detailsKey=weather.city+"|"+weather.condition+"|"+air+"|"+date
            if detailsKey != weatherDetailsKey {
                let parts:[(MacBinaryResourceKind,Int,Int,String,CGFloat)]=[(.weatherHeader,122,26,weather.city,20),(.weatherDate,190,30,date,19),(.weatherAir,100,30,air+" "+weather.condition,12)]
                let rendered=parts.compactMap { kind,width,height,text,size -> MacResourcePayload? in
                    let data: Data?
                    if kind == .weatherAir { data = MacRgb565Renderer.renderWeatherAir(air:air,condition:weather.condition) }
                    else if kind == .weatherHeader { data = MacRgb565Renderer.renderFittedLines(width:width,height:height,lines:[text],fontPixels:size,rowHeight:height) }
                    else { data = MacRgb565Renderer.renderLines(width:width,height:height,lines:[text],fontPixels:size,rowHeight:height) }
                    guard let data else{return nil}
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
               let rendered = MacRgb565Renderer.renderFittedLines(
                width: 156, height: 400, lines: names, fontPixels: 21, rowHeight: 20, alignment: .right, weight: .regular, color:NSColor(srgbRed:210/255,green:210/255,blue:210/255,alpha:1), padding:0, truncate:true) {
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
    // Fit the actual glyph ink, not the font's line box: Chinese stock names
    // can occupy the existing 20-pixel device row without clipping descenders.
    static func renderFittedLines(width: Int, height: Int, lines: [String], fontPixels: CGFloat,
                                  rowHeight: Int, alignment: NSTextAlignment = .left,
                                  weight: NSFont.Weight = .bold, color: NSColor = .white, padding: CGFloat = 1, truncate: Bool = false) -> Data? {
        // Compose independent rows so CoreText's edge antialiasing and bitmap
        // coordinate precision cannot leak between quotes on older macOS.
        if width > 2, rowHeight > 2, height > rowHeight {
            var atlas = Data(count: width * height * 2)
            let stride = width * rowHeight * 2
            for (index, text) in lines.enumerated() where (index + 1) * rowHeight <= height {
                guard let row = renderFittedLines(width: width, height: rowHeight, lines: [text],
                    fontPixels: fontPixels, rowHeight: rowHeight, alignment: alignment,
                    weight: weight, color: color, padding: padding, truncate: truncate) else { return nil }
                atlas.replaceSubrange(index * stride..<(index + 1) * stride, with: row)
            }
            return atlas
        }
        guard width > 2, height > 2, rowHeight > 2,
              let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:width,pixelsHigh:height,
                bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,
                bytesPerRow:width*4,bitsPerPixel:32), let graphics = NSGraphicsContext(bitmapImageRep:bitmap) else { return nil }
        let context = graphics.cgContext
        context.setFillColor(NSColor.black.cgColor); context.fill(CGRect(x:0,y:0,width:width,height:height))
        for (index,text) in lines.enumerated() where (index+1)*rowHeight <= height {
            let font = NSFont.systemFont(ofSize:fontPixels,weight:weight)
            let attributes: [NSAttributedString.Key:Any] = [
                NSAttributedString.Key(kCTFontAttributeName as String):CTFontCreateWithName(font.fontName as CFString,fontPixels,nil),
                NSAttributedString.Key(kCTForegroundColorAttributeName as String):color.cgColor]
            var line = CTLineCreateWithAttributedString(NSAttributedString(string:text,attributes:attributes))
            if truncate {
                let token = CTLineCreateWithAttributedString(NSAttributedString(string:"…",attributes:attributes))
                line = CTLineCreateTruncatedLine(line,CGFloat(width)-2*padding,.end,token) ?? line
            }
            context.textMatrix = .identity; context.textPosition = .zero
            let ink = CTLineGetImageBounds(line,context)
            guard !ink.isNull, ink.width > 0, ink.height > 0 else { continue }
            let scale = min(1,(CGFloat(width)-2*padding)/ink.width,(CGFloat(rowHeight)-2*padding)/ink.height)
            let x = alignment == .right ? CGFloat(width)-padding-ink.width*scale : alignment == .center ? (CGFloat(width)-ink.width*scale)/2 : padding
            let y = CGFloat(height-(index+1)*rowHeight)+(CGFloat(rowHeight)-ink.height*scale)/2
            context.saveGState(); context.translateBy(x:x,y:y); context.scaleBy(x:scale,y:scale)
            context.textPosition = CGPoint(x:-ink.minX,y:-ink.minY); CTLineDraw(line,context); context.restoreGState()
        }
        graphics.flushGraphics()
        return encodeRgb565(bitmap,width:width,height:height)
    }
    static func renderWeatherAir(air: String, condition: String) -> Data? {
        let width = 100, height = 30
        guard let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:width,pixelsHigh:height,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:width*4,bitsPerPixel:32),
              let graphics = NSGraphicsContext(bitmapImageRep:bitmap) else { return nil }
        let context = graphics.cgContext
        context.setFillColor(NSColor.black.cgColor); context.fill(CGRect(x:0,y:0,width:width,height:height))
        let color = MacWeatherAirStyle.airColor(air)
        if !air.isEmpty && air != "--" {
            let wide = air.count > 1
            let rect = wide ? CGRect(x:1.5,y:3,width:39,height:24):CGRect(x:8.5,y:2.5,width:25,height:25)
            let path = CGPath(roundedRect:rect,cornerWidth:wide ? 8:12.5,cornerHeight:wide ? 8:12.5,transform:nil)
            context.addPath(path); context.setFillColor(NSColor(srgbRed:max(3/255,color.redComponent/9),green:max(3/255,color.greenComponent/9),blue:max(3/255,color.blueComponent/9),alpha:1).cgColor)
            context.setStrokeColor(color.cgColor); context.setLineWidth(1.4); context.drawPath(using:.fillStroke)
        }
        graphics.flushGraphics()
        var result = encodeRgb565(bitmap,width:width,height:height)
        func overlay(_ text: String, x: Int, width: Int, size: CGFloat, color: NSColor) {
            guard let pixels = renderFittedLines(width:width,height:height,lines:[text],fontPixels:size,rowHeight:height,alignment:.center,color:color) else { return }
            for y in 0..<height { for column in 0..<width {
                let source=(y*width+column)*2, target=(y*100+x+column)*2
                if pixels[source] != 0 || pixels[source+1] != 0 { result[target]=pixels[source]; result[target+1]=pixels[source+1] }
            } }
        }
        if !air.isEmpty && air != "--" { overlay(air,x:0,width:42,size:air.count>1 ? 12:18,color:color) }
        let summary = MacWeatherAirStyle.condition(condition)
        overlay(summary,x:48,width:52,size:summary.count>1 ? 19:22,color:MacWeatherAirStyle.conditionColor(summary))
        return result
    }
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
            // NSBitmapImageRep pixel rows are already top-down. Reversing them
            // inverts glyphs and moves the first stock names to the atlas bottom.
            let sourceY = outputY
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
