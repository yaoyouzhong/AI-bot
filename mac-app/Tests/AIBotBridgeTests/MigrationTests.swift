import AppKit
import XCTest
@testable import AIBotBridge

final class MigrationTests: XCTestCase {
    func testMusicWithoutArtworkDoesNotPublishBlackOrStaleResources() {
        let renderer=MacLocalizedTextResources()
        var song=MusicSnapshot(title:"Song",artist:"Artist",album:"",playing:true,elapsedSeconds:0,durationSeconds:120,updatedAt:Date())
        song.hasArtwork=false
        XCTAssertFalse(renderer.capture(weather:nil,stocks:nil,music:song,musicCover:nil).contains{$0.kind == .musicCover})
        song.hasArtwork=true
        let cover=Data(repeating:120,count:112*112*2)
        XCTAssertTrue(renderer.capture(weather:nil,stocks:nil,music:song,musicCover:cover).contains{$0.kind == .musicCover && $0.data==cover})
        let empty=renderer.capture(weather:nil,stocks:nil,music:.empty(),musicCover:cover)
        XCTAssertFalse(empty.contains{$0.kind == .musicCover || $0.kind == .textBitmap})
        XCTAssertTrue(renderer.capture(weather:nil,stocks:nil,music:song,musicCover:cover).contains{$0.kind == .musicCover})
    }
    func testFollowUsesLastActualSwitchAndPreservesWireState() throws {
        func snapshot(_ claude:String,_ codex:String,_ mode:String="auto")->MacStatusSnapshot {
            var s=MacStatusSnapshot(version:1,time:"",epochUtc:0,utcOffsetSeconds:0,capturedAt:Date(timeIntervalSince1970:0),codex:ToolState(state:codex,ageSeconds:0),claude:ToolState(state:claude,ageSeconds:0),musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
            s.displayPolicy=MacDisplayPolicy(selectedMode:mode,cycleEnabled:false,intervalSeconds:15,pages:["weather"])
            return s
        }
        let tracker=MacAutoFollowTracker(),idle=snapshot("idle","idle"),both=snapshot("working","working")
        XCTAssertEqual(tracker.update(idle,milliseconds:100),"claude")
        XCTAssertEqual(tracker.update(idle,milliseconds:6099),"claude")
        XCTAssertEqual(tracker.update(idle,milliseconds:6100),"codex")
        XCTAssertEqual(tracker.update(both,milliseconds:8099),"codex")
        XCTAssertEqual(tracker.update(both,milliseconds:8100),"claude")
        XCTAssertEqual(tracker.update(snapshot("idle","working"),milliseconds:8500),"codex")
        XCTAssertEqual(tracker.update(both,milliseconds:10499),"codex")
        XCTAssertEqual(tracker.update(both,milliseconds:10500),"claude")
        XCTAssertEqual(tracker.update(snapshot("idle","idle","weather"),milliseconds:100000),"claude")
        XCTAssertEqual(tracker.update(idle,milliseconds:100001),"codex")
        var wire=idle;wire.followApp="codex"
        XCTAssertEqual(wire.displayPolicy!.resolve(wire),"codex")
        let frame=try XCTUnwrap(SerialBridge.statusFrame(wire))
        XCTAssertTrue(String(decoding:frame,as:UTF8.self).contains("\"followApp\":\"codex\""))
    }
    func testMetricsPreserveSubsecondSampleIdentity() throws {
        let sample=SystemMetricsSnapshot(cpuPercent:10,memoryPercent:20,uploadBytesPerSecond:30,downloadBytesPerSecond:40,updatedAt:Date(timeIntervalSince1970:1.25))
        let frame=try XCTUnwrap(SerialBridge.metricsFrame(sample))
        XCTAssertTrue(String(decoding:frame,as:UTF8.self).contains("01.250Z"))
        XCTAssertTrue(String(decoding:frame,as:UTF8.self).hasPrefix("@AIBOT "))
    }
    func testExplicitCycleAndSmartFollowAreSeparate() {
        var sample=MacStatusSnapshot(version:1,time:"00:00:00",epochUtc:1000,utcOffsetSeconds:0,capturedAt:Date(timeIntervalSince1970:1000),codex:ToolState(state:"working",ageSeconds:0),claude:ToolState(state:"idle",ageSeconds:0),musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
        let policy=MacDisplayPolicy(selectedMode:"auto",cycleEnabled:true,intervalSeconds:15,pages:["weather","stocks"],cycleStartedAt:sample.epochUtc)
        XCTAssertEqual(policy.resolve(sample),"weather")
        var advanced=policy;advanced.cycleStartedAt=sample.epochUtc-15
        XCTAssertEqual(advanced.resolve(sample),"stocks")
        var manual=policy;manual.selectedMode="music"
        XCTAssertEqual(manual.resolve(sample),"music")
        sample.displayPolicy=policy
        let encoder=JSONEncoder()
        XCTAssertNoThrow(try encoder.encode(sample))
    }
    func testDomesticPlanAndWeeklyRemainIndependent() throws {
        let decoder=JSONDecoder();decoder.dateDecodingStrategy = .iso8601
        let bytes=Data(#"{"provider":"alibaba","planPercent":25,"weeklyPercent":40,"updatedAt":"2026-09-09T00:00:00Z","stale":true}"#.utf8)
        let quota=try decoder.decode(DomesticProviderQuotaSnapshot.self,from:bytes)
        XCTAssertEqual(quota.planPercent,25);XCTAssertEqual(quota.weeklyPercent,40)
        let old=try decoder.decode(DomesticProviderQuotaSnapshot.self,from:Data(#"{"provider":"alibaba","weeklyPercent":40,"updatedAt":"2026-09-09T00:00:00Z","stale":true}"#.utf8))
        XCTAssertNil(old.planPercent);XCTAssertEqual(old.weeklyPercent,40)
    }
    func testPetDimensionsCacheAndRoleIsolation() throws {
        let directory=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer {try? FileManager.default.removeItem(at:directory)}
        let animation=MacPetAnimation(width:111,height:120,delays:[120,240],frames:[Data(repeating:0,count:111*120*2),Data(repeating:255,count:111*120*2)])
        let bytes=try animation.encode(),decoded=try MacPetAnimation(data:bytes)
        XCTAssertEqual(decoded.width,111);XCTAssertEqual(decoded.delays,[120,240]);XCTAssertEqual(decoded.frames,animation.frames)
        XCTAssertThrowsError(try MacPetAnimation(data:bytes.dropLast()))
        let cache=MacPetCache(directory:directory);try cache.select("claude",data:bytes)
        XCTAssertNil(cache.selection("codex"));XCTAssertEqual(MacPetCache(directory:directory).selection("claude")?.width,111)
        XCTAssertEqual(cache.resources().map(\.kind),[.claudePetAnimation]);XCTAssertThrowsError(try cache.restore("claude"))
        XCTAssertEqual(cache.selection("claude")?.width,111)
    }
    func testAttentionStopCompletionAndAcknowledgement() {
        let signals=MacActivitySignals(),now=Date(),idle=ToolState(state:"idle",ageSeconds:0)
        _=signals.apply("codex",raw:idle,now:now)
        XCTAssertTrue(signals.record(agent:"claude",event:"PermissionRequest",now:now))
        XCTAssertTrue(signals.apply("claude",raw:idle,now:now).needsInput)
        XCTAssertFalse(signals.apply("claude",raw:idle,now:now.addingTimeInterval(300)).needsInput)
        signals.record(agent:"codex",event:"Stop",now:now);XCTAssertFalse(signals.apply("codex",raw:idle,now:now).completionActive)
        signals.record(agent:"codex",event:"TaskComplete",now:now);XCTAssertTrue(signals.apply("codex",raw:idle,now:now).completionActive)
        signals.acknowledge();XCTAssertFalse(signals.apply("codex",raw:idle,now:now).completionActive)
    }
    func testTokenMetadataAndChildCompletionFiltering() {
        let now=Date(),stamp=ISO8601DateFormatter().string(from:now)
        let usage=MacActivityMetadata.parse(["{\"timestamp\":\"\(stamp)\",\"message\":{\"model\":\"qwen3\",\"usage\":{\"input_tokens\":10,\"output_tokens\":20,\"cache_read_input_tokens\":30}}}"],codex:false,now:now)
        XCTAssertEqual(usage.usage["alibaba"],60)
        let child=MacActivityMetadata.parse(["{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{}}}}","{\"timestamp\":\"\(stamp)\",\"payload\":{\"type\":\"task_complete\"}}"],codex:true,now:now)
        XCTAssertEqual(child.completed,0)
    }
    func testLanAuthenticationAndLoopbackEvents() {
        let token=String(repeating:"t",count:32),bytes=Data([0,1,128,255]);var events=0
        let server=HTTPStatusServer(port:12345,token:{token},resources:{[MacResourcePayload(kind:.claudePetAnimation,revision:1,data:bytes)]},event:{_,_,_ in events+=1;return true},snapshot:{Data("{}".utf8)})
        XCTAssertTrue(String(decoding:server.response(for:"GET /resources HTTP/1.1\r\n\r\n"),as:UTF8.self).contains("401"))
        let crc=MacBinaryResourceProtocol.crc32(Array(bytes))
        XCTAssertTrue(server.response(for:"GET /resources/10/\(crc) HTTP/1.1\r\nX-AIBot-Token: \(token)\r\n\r\n").suffix(bytes.count).elementsEqual(bytes))
        let event="POST /event HTTP/1.1\r\n\r\n{\"agent\":\"claude\",\"event\":\"PermissionRequest\"}"
        _=server.response(for:event);XCTAssertEqual(events,0)
        _=server.response(for:event,loopback:true);XCTAssertEqual(events,1)
        _=server.response(for:event.replacingOccurrences(of:"HTTP/1.1\r\n",with:"HTTP/1.1\r\nOrigin: https://example.invalid\r\n"),loopback:true);XCTAssertEqual(events,1)
    }
    func testGalleryBoundaries() {
        XCTAssertEqual(MacGalleryMotion.all.count,9)
        XCTAssertTrue(MacGalleryService.allowed(URL(string:"https://assets.petdex.dev/a.webp")!))
        XCTAssertFalse(MacGalleryService.allowed(URL(string:"http://assets.petdex.dev/a.webp")!))
        XCTAssertFalse(MacGalleryService.allowed(URL(string:"https://assets.petdex.dev.evil.invalid/a.webp")!))
    }
}
