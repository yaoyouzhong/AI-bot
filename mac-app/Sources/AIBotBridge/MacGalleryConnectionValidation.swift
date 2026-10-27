import AppKit
import ImageIO

enum MacGalleryConnectionValidation {
    static func fixture(_ directory:URL) throws -> MacTab5Gallery {
        let gallery = MacTab5Gallery(directory:directory)
        let context = CGContext(data:nil,width:1280,height:720,bitsPerComponent:8,bytesPerRow:0,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
        context.setFillColor(CGColor(red:0.88,green:0.83,blue:0.72,alpha:1)); context.fill(CGRect(x:0,y:0,width:1280,height:720))
        context.setFillColor(CGColor(red:0.13,green:0.35,blue:0.35,alpha:1)); context.fill(CGRect(x:100,y:100,width:1080,height:520))
        context.setFillColor(CGColor(red:0.86,green:0.55,blue:0.24,alpha:1)); context.fillEllipse(in:CGRect(x:500,y:220,width:280,height:280))
        let output = NSMutableData(), destination = CGImageDestinationCreateWithData(output,"public.jpeg" as CFString,1,nil)!
        CGImageDestinationAddImage(destination,context.makeImage()!,nil); guard CGImageDestinationFinalize(destination) else { throw MacTab5Error.invalidPacket }
        for category in ["painting","calligraphy"] {
            let work = MacTab5Artwork(id:"display-id",category:category,title:"构图与阅读测试",author:"AI-bot 原创测试图",source:"https://example.com/fixture",license:"本项目生成的几何测试图",frames:["a.jpg","b.jpg"],workId:"guide-id",portraitFrames:["pa.jpg","pb.jpg"])
            var files: [String:Data] = ["catalog.json":try JSONEncoder().encode([work]),"a.jpg":output as Data,"b.jpg":output as Data,"pa.jpg":output as Data,"pb.jpg":output as Data,"provenance.json":Data("{}".utf8),"selection.json":Data("{}".utf8),"LICENSE":Data("fixture".utf8),"THIRD_PARTY_NOTICES.md":Data("fixture".utf8)]
            files["guides.zh.json"] = try MacTab5Protocol.json(["schemaVersion":1,"language":"zh-CN","works":[["id":work.id,"category":category,"workId":"guide-id","title":work.title,"author":work.author,"brief":"用于验证图片与导读同时显示。","overview":"宽屏左右对照，窄屏上下对照；图片保持可缩放、拖动。","looking":"检查边缘、色块与间距。","sources":["fixture"],"transcription":["status":"source_verified","text":"这是测试原文。","completeReferenceText":String(repeating:"可选择、复制和滚动的完整原文。\n",count:120)]]]])
            let manifest = MacTab5GalleryManifest(schemaVersion:2,version:"2026.10.22",category:category,works:1,frames:4,files:files.mapValues(MacTab5Protocol.sha256))
            _ = try gallery.install(manifest:manifest,manifestBytes:JSONEncoder().encode(manifest),names:Array(files.keys)+["PACK.json"],read:{ files[$0]! })
        }
        return gallery
    }
    static func run(root:URL) throws -> Int {
        var checks = 0
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws { guard try value() else { throw MacTab5LocalError.invalid("Gallery/connection: "+name) }; checks += 1 }
        func rejects(_ name:String,_ body:() throws -> Void) throws { do { try body() } catch { checks += 1; return }; throw MacTab5LocalError.invalid("Expected failure: "+name) }
        let network:[String:Any] = ["ssid":"测试网络","label":"书房","connected":true,"selected":true]
        let reply:[String:Any] = ["version":1,"deviceId":"test","networks":[network]]
        try check("Wi-Fi metadata parsed",MacSavedWiFi.parse(reply,device:"test").first?.label == "书房")
        try rejects("Wi-Fi device mismatch") { _ = try MacSavedWiFi.parse(reply,device:"wrong") }
        try rejects("Wi-Fi duplicate SSID") { _ = try MacSavedWiFi.parse(["version":1,"deviceId":"test","networks":[network,network]],device:"test") }
        try rejects("Wi-Fi newline label") { try MacSavedWiFi.validate(ssid:"test",label:"bad\nlabel") }
        try rejects("Wi-Fi UTF8 length") { try MacSavedWiFi.validate(ssid:String(repeating:"网",count:11),label:"") }
        let legacyBenchmark:[String:Any] = ["type":"tab5_benchmark","deviceId":"test","result":"complete;USB,up,1,262144,1000,0"]
        func acceptsManagement(_ value:[String:Any],type:String = "tab5_benchmark",device:String = "test") -> Bool {
            do { try MacTab5USBManagementReply.validate(value,type:type,device:device); return true } catch { return false }
        }
        try check("legacy benchmark reply without version",acceptsManagement(legacyBenchmark))
        var versionedBenchmark = legacyBenchmark; versionedBenchmark["version"] = 1
        try check("versioned benchmark reply",acceptsManagement(versionedBenchmark))
        try check("benchmark wrong identity rejected",!acceptsManagement(legacyBenchmark,device:"other"))
        try check("benchmark wrong type rejected",!acceptsManagement(legacyBenchmark,type:"tab5_wifi_list"))
        var missingDevice = legacyBenchmark; missingDevice.removeValue(forKey:"deviceId")
        try check("benchmark missing identity rejected",!acceptsManagement(missingDevice))
        for invalid in [2,"1",true,NSNull()] as [Any] {
            var value = legacyBenchmark; value["version"] = invalid
            try check("explicit invalid benchmark version rejected",!acceptsManagement(value))
        }
        var invalidResult = legacyBenchmark; invalidResult["result"] = NSNull()
        try check("legacy benchmark missing result rejected",!acceptsManagement(invalidResult))
        invalidResult["result"] = String(repeating:"a",count:8193)
        try check("legacy benchmark oversized result rejected",!acceptsManagement(invalidResult))
        var wifiManagement = legacyBenchmark; wifiManagement["type"] = "tab5_wifi_list"
        try check("other management still requires version",!acceptsManagement(wifiManagement,type:"tab5_wifi_list"))
        wifiManagement["version"] = 1
        try check("versioned other management preserved",acceptsManagement(wifiManagement,type:"tab5_wifi_list"))
        let pattern = MacTab5Benchmark.pattern(offset:8192,count:49152)
        let request:[String:Any] = ["offset":8192,"count":49152]
        try check("benchmark pattern",pattern.first == 17 && pattern[1] == 48)
        try check("benchmark negotiated bulk",MacTab5Benchmark.handle(request,raw:Data(),binary:true,bulk:true).bytes == pattern)
        try check("benchmark unnegotiated bulk",MacTab5Benchmark.handle(request,raw:Data(),binary:false,bulk:true).status == 400)
        try check("benchmark upload validated",MacTab5Benchmark.handle(request,raw:pattern,binary:true,bulk:true).status == 200)
        try check("benchmark corruption rejected",MacTab5Benchmark.handle(request,raw:Data(repeating:0,count:49152),binary:true,bulk:true).status == 400)
        try check("zero time safe",!MacTab5Benchmark.format("running;gallery,USB,0,0,0,0,0,0,0",details:false).contains("nan"))
        try check("gallery numeric date",MacGalleryRelease.number("2026.10.22.3") == [2026,10,22,3] && MacGalleryRelease.number("2026.02.30") == nil)
        let archiveRoot = root.appendingPathComponent("unicode-archive"), art = archiveRoot.appendingPathComponent("作品")
        try FileManager.default.createDirectory(at:art,withIntermediateDirectories:true)
        let literal = "作品/图[12]—高清.jpg", expected = Data([1,2,3,4])
        try expected.write(to:archiveRoot.appendingPathComponent(literal)); try Data([9,9]).write(to:art.appendingPathComponent("图1—高清.jpg"))
        let zip = root.appendingPathComponent("unicode.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/zip",["-q","-r",zip.path,"作品"],directory:archiveRoot)
        let archive = try MacTab5Archive(zip,allowNested:true,allowUnicode:true)
        try check("literal Unicode bracket ZIP read",archive.read(literal,maximum:100) == expected)
        let copied = root.appendingPathComponent("copied.jpg")
        try archive.copy(literal,to:copied,maximum:100,sha256:MacTab5Protocol.sha256(expected))
        try check("literal Unicode bracket ZIP copy",Data(contentsOf:copied) == expected)
        try check("Unicode traversal still rejected",!MacTab5Archive.safeUnicodePath("作品/../outside.jpg"))
        let gallery = try fixture(root), work = try gallery.browserWorks("painting")[0], page = try gallery.browserPage(work,frame:0)
        try check("guide looked up by workId",page.1["overview"] as? String == "宽屏左右对照，窄屏上下对照；图片保持可缩放、拖动。")
        try check("full transcription retained",(page.1["transcription"] as? String)?.count ?? 0 > 1000)
        let cancelled = MacGalleryProgress(); cancelled.cancel()
        try rejects("cancellation token") { try cancelled.check() }
        let current = gallery.revisions(), manifest = try gallery.installed("painting")
        try rejects("cancelled import preserves active library") { _ = try gallery.install(manifest:manifest,manifestBytes:JSONEncoder().encode(manifest),names:Array(manifest.files.keys)+["PACK.json"],progress:cancelled,read:{ _ in Data() }) }
        try check("original library retained",gallery.revisions() == current)
        let canvas = MacGalleryCanvas(frame:NSRect(x:0,y:0,width:800,height:600))
        canvas.setImage(NSImage(size:NSSize(width:1280,height:720)))
        canvas.scrollZoom(deltaY:10000,precise:false)
        try check("accelerated wheel advances one small step",canvas.zoom > 1 && canvas.zoom <= 1.13)
        canvas.scrollZoom(deltaY:-10000,precise:false)
        try check("wheel reversal returns to fit",abs(canvas.zoom-1) < 0.000001)
        canvas.scrollZoom(deltaY:1,precise:true)
        try check("precise trackpad motion remains fine grained",canvas.zoom > 1 && canvas.zoom < 1.01)
        let fine = canvas.zoom
        canvas.scrollZoom(deltaY:0,precise:true)
        canvas.scrollZoom(deltaY:.nan,precise:true)
        try check("empty or invalid scroll does not change zoom",canvas.zoom == fine)
        canvas.scrollZoom(deltaY:10000,precise:true)
        try check("coalesced precise scroll remains bounded",canvas.zoom > fine && canvas.zoom <= fine*1.13)
        canvas.zoom(to:2)
        let anchor = NSPoint(x:500,y:330), before = canvas.imageBounds
        let fx = (anchor.x-before.minX)/before.width, fy = (anchor.y-before.minY)/before.height
        canvas.scrollZoom(deltaY:3,precise:false,at:anchor)
        let after = canvas.imageBounds
        try check("wheel zoom preserves pointer anchor",abs(after.minX+fx*after.width-anchor.x) < 0.001 && abs(after.minY+fy*after.height-anchor.y) < 0.001)
        canvas.resetView()
        for _ in 0..<5 { let previous = canvas.zoom; canvas.scrollZoom(deltaY:3,precise:false); try check("successive wheel notches progress gradually",canvas.zoom > previous && canvas.zoom <= previous*1.13) }
        for _ in 0..<100 { canvas.scrollZoom(deltaY:10000,precise:false) }
        try check("repeated scrolling respects maximum",canvas.zoom == 16)
        canvas.resetView()
        try check("fit resets zoom and pan",canvas.zoom == 1 && canvas.pan == .zero)
        let clipped = MacGalleryCanvas(frame:NSRect(x:0,y:0,width:120,height:80))
        func staysInsideViewport(_ size:NSSize, pan:NSPoint = .zero) throws -> Bool {
            clipped.setImage(NSImage(size:size,flipped:false,drawingHandler:{ rect in NSColor.red.setFill(); rect.fill(); return true }))
            clipped.zoom(to:16); clipped.move(by:pan)
            guard let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:200,pixelsHigh:160,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0),
                  let context = NSGraphicsContext(bitmapImageRep:bitmap) else { throw MacTab5Error.unavailable }
            NSGraphicsContext.saveGraphicsState()
            defer { NSGraphicsContext.restoreGraphicsState() }
            NSGraphicsContext.current = context
            context.cgContext.clear(CGRect(x:0,y:0,width:200,height:160))
            context.cgContext.translateBy(x:40,y:40)
            let clipBefore = context.cgContext.boundingBoxOfClipPath
            clipped.draw(clipped.bounds)
            let outside = [(10,80),(190,80),(100,10),(100,150)]
            let clear = outside.allSatisfy { (bitmap.colorAt(x:$0.0,y:$0.1)?.alphaComponent ?? 1) < 0.01 }
            let inside = bitmap.colorAt(x:100,y:80)?.usingColorSpace(.deviceRGB)
            return clear && (inside?.redComponent ?? 0) > 0.9 && (inside?.greenComponent ?? 1) < 0.1 && context.cgContext.boundingBoxOfClipPath == clipBefore
        }
        try check("zoomed portrait drawing stays within viewport",staysInsideViewport(NSSize(width:200,height:400)))
        try check("zoomed landscape drawing stays within viewport",staysInsideViewport(NSSize(width:400,height:200)))
        try check("dragged drawing stays within viewport",staysInsideViewport(NSSize(width:400,height:200),pan:NSPoint(x:10000,y:-10000)))
        return checks
    }
}
