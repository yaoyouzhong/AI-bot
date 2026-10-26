import AppKit

enum MacLocalizedTextValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ name: String, _ ok: Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("Localized text: " + name) }; checked += 1
        }
        // An upright F has a wide top bar and a narrow lower stem. This checks
        // glyph direction independently of another call to the same renderer.
        guard let glyph = MacRgb565Renderer.renderLines(width:60,height:48,lines:["F"],fontPixels:36,rowHeight:48) else { throw MacPetAssetError.renderFailed }
        var ink = [Int]()
        for y in 0..<48 {
            var count = 0
            for x in 0..<60 { let offset = (y * 60 + x) * 2; if glyph[offset] != 0 || glyph[offset+1] != 0 { count += 1 } }
            ink.append(count)
        }
        let occupied = ink.filter { $0 > 0 }, span = max(1,occupied.count / 4)
        try check("glyph is upright",occupied.prefix(span).reduce(0,+) > occupied.suffix(span).reduce(0,+) * 2)
        let names = (1...20).map { "第\($0)行" }
        func render(_ names: [String], height: Int) throws -> Data {
            guard let pixels = MacRgb565Renderer.renderFittedLines(width:156,height:height,lines:names,fontPixels:21,rowHeight:20,alignment:.right,weight:.regular,color:NSColor(srgbRed:210/255,green:210/255,blue:210/255,alpha:1),padding:0,truncate:true) else { throw MacPetAssetError.renderFailed }; return pixels
        }
        let full = try render(names,height:400), stride = 156 * 20 * 2
        for index in 0..<20 {
            let row = try render([names[index]],height:20)
            try check("firmware stock row \(index) matches quote",Data(full[index*stride..<(index+1)*stride]) == row && row.contains { $0 != 0 })
        }
        let sparse = try render(Array(names.prefix(3)),height:400)
        try check("unused stock rows stay at bottom",sparse.dropFirst(3*stride).allSatisfy { $0 == 0 })
        try check("first quote remains in first row",Data(sparse.prefix(stride)) == render([names[0]],height:20))
        try check("missing AQI stays unknown",MacWeatherAirStyle.category(label:nil,index:nil) == "--")
        try check("air categories match existing Windows presentation",[50,100,150,200,300].map { MacWeatherAirStyle.category(label:nil,index:$0) } == ["优","良","轻度","中度","污染"])
        try check("source air label takes priority",MacWeatherAirStyle.category(label:"轻度污染",index:50) == "轻度")
        try check("compact condition",MacWeatherAirStyle.condition("雷阵雨") == "雷雨" && MacWeatherAirStyle.condition("毛毛雨") == "细雨")
        for air in ["优","良","轻度","中度","污染","--"] {
            guard let pixels = MacRgb565Renderer.renderWeatherAir(air:air,condition:"多云") else { throw MacPetAssetError.renderFailed }
            func region(_ start: Int, _ end: Int) -> [UInt8] { (0..<30).flatMap { y in Array(pixels[(y*100+start)*2..<(y*100+end)*2]) } }
            try check("weather resource dimensions",pixels.count == 100*30*2)
            try check("air and weather have separate gap",region(42,48).allSatisfy { $0 == 0 })
            try check("weather remains visible",region(48,100).contains { $0 != 0 })
            try check("unknown air badge omitted",region(0,42).contains { $0 != 0 } == (air != "--"))
        }
        return checked
    }
}
