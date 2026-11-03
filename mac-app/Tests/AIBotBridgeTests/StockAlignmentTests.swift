import XCTest
import AppKit
@testable import AIBotBridge

final class StockAlignmentTests: XCTestCase {
    func testAcknowledgedResourceContentAndConnectionScope() {
        var cache = MacAcknowledgedResources()
        let a = Data([1, 2]), b = Data([1, 3])
        XCTAssertFalse(cache.contains(.stockLabels, a))
        cache.record(.stockLabels, a)
        XCTAssertTrue(cache.contains(.stockLabels, Data([1, 2])))
        XCTAssertFalse(cache.contains(.stockLabels, b))
        cache.invalidate(.stockLabels) // attempted change, even if transfer fails
        XCTAssertFalse(cache.contains(.stockLabels, a))
        cache.record(.stockLabels, b)
        cache.reset()
        XCTAssertFalse(cache.contains(.stockLabels, b))
    }
    func testConnectionStateTracksHandshakeAndDisconnect() {
        let state = MacSerialConnectionState()
        XCTAssertNil(state.snapshot().port)
        state.setDetail("握手已确认")
        state.setConnection(port: "fixture", host: "192.168.1.2")
        XCTAssertEqual(state.snapshot().port, "fixture")
        XCTAssertEqual(state.snapshot().detail, "握手已确认")
        state.setHost(nil)
        XCTAssertNil(state.snapshot().host)
        state.setConnection(port: nil, host: nil)
        XCTAssertNil(state.snapshot().port)
    }
    func testGlyphTableMatchesFirmwareAndTruncation() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let source = try String(contentsOf: root.appendingPathComponent("firmware/src/StockCodeGlyphs.h"), encoding: .utf8)
        let table = try XCTUnwrap(source.components(separatedBy: "stockCodeGlyphs[95][64]").last)
        let regex = try NSRegularExpression(pattern: "0x([0-9a-fA-F]{2})\\b")
        let bytes = regex.matches(in: table, range: NSRange(table.startIndex..., in: table)).map { UInt8(table[Range($0.range(at: 1), in: table)!], radix: 16)! }
        XCTAssertEqual(Data(bytes), MacStockCodeRaster.glyphs)
        XCTAssertEqual(MacStockCodeRaster.raster("ABCDEFGHmore"), MacStockCodeRaster.raster("ABCDEFGHmore-again"))
        XCTAssertNotEqual(MacStockCodeRaster.raster("ABCDEFGH"), MacStockCodeRaster.raster("ABCDEFGHmore"))
    }
    func testStockMirrorOffscreen() throws {
        let now = Date(), state = ToolState(state:"idle",ageSeconds:nil)
        let quotes = [("600519","贵州茅台","1523.88","+1.25%",1),("TSLA","特斯拉","243.12","-2.80%",-1),("ABCDEFGH","长股票名称示例","10.00","0.00%",0),("ABCDEFGHI","测试","0.00","+0.00%",1)].map { StockQuote(symbol:$0.0,code:$0.0,name:$0.1,price:$0.2,changePercent:$0.3,trend:$0.4) }
        let stocks=StockSnapshot(quotes:quotes,updatedAt:now,stale:false)
        var snapshot=MacStatusSnapshot(version:1,time:"12:00:00",epochUtc:0,utcOffsetSeconds:0,capturedAt:now,codex:state,claude:state,musicPlaying:nil,weather:nil,stocks:stocks,systemMetrics:nil,quotas:nil,music:nil)
        snapshot.displayPolicy=MacDisplayPolicy(selectedMode:"stocks",cycleEnabled:false,intervalSeconds:15,pages:["stocks"])
        let view=MacMirrorView(frame:NSRect(x:0,y:0,width:240,height:240))
        let resource=MacLocalizedTextResources().capture(weather:nil,stocks:stocks,music:nil,musicCover:nil)
        view.capture={snapshot}; view.resources={resource}
        let bitmap=try XCTUnwrap(view.bitmapImageRepForCachingDisplay(in:view.bounds))
        view.cacheDisplay(in:view.bounds,to:bitmap)
        let png=try XCTUnwrap(bitmap.representation(using:.png,properties:[:]))
        try png.write(to:URL(fileURLWithPath:"/tmp/aibot-mac-stock-preview.png"))
    }
}
