import XCTest
import zlib
@testable import AIBotBridge

final class MacClientCompanionAlignmentTests: XCTestCase {
    func testLegacyClaudeStopAndSubagentDoNotInventCompletionOrDismissAttention() {
        let signals=MacActivitySignals(), now=Date(), raw=ToolState(state:"working",ageSeconds:0)
        XCTAssertTrue(signals.record(agent:"claude",event:"PermissionRequest",now:now))
        XCTAssertTrue(signals.record(agent:"claude",event:"SubagentStop",now:now.addingTimeInterval(1)))
        XCTAssertTrue(signals.record(agent:"claude",event:"Stop",now:now.addingTimeInterval(2)))
        let waiting=signals.apply("claude",raw:raw,now:now.addingTimeInterval(2))
        XCTAssertTrue(waiting.needsInput)
        XCTAssertEqual(waiting.state,"unknown")
        XCTAssertFalse(waiting.completionActive)
        XCTAssertTrue(signals.record(agent:"claude",event:"UserPromptSubmit",now:now.addingTimeInterval(3)))
        XCTAssertFalse(signals.apply("claude",raw:raw,now:now.addingTimeInterval(3)).needsInput)
    }

    func testClaudeClientTimeIsIndependentOfModelAndExcludesSidechain() {
        let now = Date(), stamp = ISO8601DateFormatter().string(from: now.addingTimeInterval(-10))
        func row(_ model: String, sidechain: Bool = false, type: String = "assistant") -> String {
            """
            {"timestamp":"\(stamp)","type":"\(type)","isSidechain":\(sidechain),"message":{"model":"\(model)","usage":{"input_tokens":1}}}
            """
        }
        for model in ["claude-sonnet", "deepseek-chat", "vendor/custom-private-model"] {
            XCTAssertGreaterThan(MacActivityMetadata.parse([row(model)],codex:false,now:now).clientAt.timeIntervalSince1970, 0)
        }
        XCTAssertEqual(MacActivityMetadata.parse([row("custom",sidechain:true)],codex:false,now:now).clientAt, .distantPast)
        XCTAssertEqual(MacActivityMetadata.parse([row("custom",type:"user")],codex:false,now:now).clientAt, .distantPast)
    }

    func testMainSessionCapturePreservesActualTimesAndUnknownCustomModel() throws {
        let home=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let directory=home.appendingPathComponent(".claude/projects/test")
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:home) }
        let now=Date(), old=now.addingTimeInterval(-45), fresh=now.addingTimeInterval(-5)
        func row(_ date:Date,_ model:String) throws -> String {
            let stamp=ISO8601DateFormatter().string(from:date)
            let bytes=try JSONSerialization.data(withJSONObject:["type":"assistant","timestamp":stamp,"message":["model":model,"usage":["input_tokens":1]]])
            return String(decoding:bytes,as:UTF8.self)+"\n"
        }
        let text=try row(old,"deepseek-chat") + row(fresh,"vendor/custom-model")
        try text.write(to:directory.appendingPathComponent("session.jsonl"),atomically:true,encoding:.utf8)
        let captured=MacActivityMetadata(home:home).capture(now:now)
        XCTAssertEqual(captured.claude.state,"working")
        XCTAssertLessThanOrEqual(captured.claude.ageSeconds ?? 999,6)
        XCTAssertEqual(captured.domestic?.activeProvider,"deepseek")
        XCTAssertEqual(captured.domestic?.providers?["deepseek"]?.lastActivity,Int64(old.timeIntervalSince1970))
        XCTAssertGreaterThan(captured.claude.ageSeconds.map { Int64(now.timeIntervalSince1970)-$0 } ?? 0, captured.domestic?.providers?["deepseek"]?.lastActivity ?? 0)
    }

    func testRealProviderTimestampSurvivesTab5WireAndESPCompactExcludesIt() throws {
        let now = Date(), earlier = Int64(now.timeIntervalSince1970) - 13
        let state = ToolState(state:"working",ageSeconds:13)
        var snapshot = MacStatusSnapshot(version:1,time:"12:00:00",epochUtc:Int64(now.timeIntervalSince1970),utcOffsetSeconds:0,capturedAt:now,codex:state,claude:state,musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
        snapshot.domesticActivity = MacDomesticActivity(activeProvider:"deepseek",state:"working",needsInput:false,tokensToday:12,providers:["deepseek":MacProviderActivity(tokensToday:12,lastActivity:earlier)])
        let frame = try MacTab5Protocol.snapshot(snapshot,device:"001122334455",session:String(repeating:"a",count:32),sequence:1,command:nil)
        func verify(_ data: Data) throws {
            let root=try MacTab5Protocol.object(data), payload=try XCTUnwrap(root["data"] as? [String:Any])
            let domestic=try XCTUnwrap(payload["domesticActivity"] as? [String:Any])
            let providers=try XCTUnwrap(domestic["providers"] as? [String:[String:Any]])
            XCTAssertEqual(providers["deepseek"]?["lastActivity"] as? Int64,earlier)
            XCTAssertNil(providers["deepseek"]?["model"])
        }
        try verify(frame) // Wi-Fi uses the full status frame.
        var baseline=MacTab5BLEDelta()
        try verify(MacTab5USBStatus.wire(frame,baseline:&baseline,packed:false,delta:false,ownershipRefresh:false,deltaRenewsOwnership:false))
        XCTAssertEqual(try MacTab5BLEStatus.wire(frame,supported:false),frame)
        let packed=try MacTab5BLEStatus.wire(frame,supported:true)
        let envelope=try MacTab5Protocol.object(packed)
        if envelope["type"] as? String == "tab5_packed" {
            let input=try XCTUnwrap(Data(base64Encoded:try XCTUnwrap(envelope["payload"] as? String)))
            let size=try XCTUnwrap(envelope["size"] as? Int)
            var output=[UInt8](repeating:0,count:size), length=uLongf(size)
            let status=input.withUnsafeBytes { source in output.withUnsafeMutableBufferPointer { target in
                uncompress(target.baseAddress,&length,source.bindMemory(to:UInt8.self).baseAddress,uLong(input.count))
            }}
            XCTAssertEqual(status,Z_OK); XCTAssertEqual(Int(length),size)
            try verify(Data(output))
        } else { try verify(packed) }
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: JSONEncoder().encode(snapshot)) as? [String:Any])
        let compact = MacESPStatus.project(encoded)
        let domestic = try XCTUnwrap(compact["domesticActivity"] as? [String:Any])
        XCTAssertNil(domestic["providers"])
    }

    func testProviderTimestampSurvivesUSBAndBLEDeltaAndActivityFiltering() throws {
        let now=Date(), state=ToolState(state:"working",ageSeconds:3)
        var snapshot=MacStatusSnapshot(version:1,time:"12:00:00",epochUtc:Int64(now.timeIntervalSince1970),utcOffsetSeconds:0,capturedAt:now,codex:state,claude:state,musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
        snapshot.domesticActivity=MacDomesticActivity(activeProvider:"deepseek",state:"working",needsInput:false,tokensToday:2,providers:["deepseek":MacProviderActivity(tokensToday:2,lastActivity:100)])
        let device="001122334455", session=String(repeating:"a",count:32)
        func frame(_ sequence:Int64) throws -> Data { try MacTab5Protocol.snapshot(snapshot,device:device,session:session,sequence:sequence,command:nil,additions:["fixture":String(repeating:"x",count:4096)]) }
        let before=try frame(1)
        snapshot.domesticActivity?.providers?["deepseek"]?.lastActivity=101
        let after=try frame(2)
        func check(_ wire:Data) throws {
            let root=try MacTab5Protocol.object(wire)
            if root["type"] as? String == "tab5_delta" {
                let changes=try XCTUnwrap(root["changes"] as? [[String:Any]])
                XCTAssertTrue(changes.contains { ($0["path"] as? [String]) == ["domesticActivity","providers","deepseek","lastActivity"] && ($0["value"] as? Int) == 101 })
            } else { XCTAssertEqual(wire,after) }
        }
        var ble=MacTab5BLEDelta(); ble.acknowledge(before,sequence:1)
        try check(ble.wire(after,packed:false,supported:true))
        var usb=MacTab5BLEDelta(); usb.acknowledge(before,sequence:1)
        try check(MacTab5USBStatus.wire(after,baseline:&usb,packed:false,delta:true,ownershipRefresh:false,deltaRenewsOwnership:false))
        let dir=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at:dir) }
        let registry=MacDeviceRegistry(directory:dir)
        var entry=MacRegisteredDevice(kind:"tab5",name:"fixture")
        entry.sources=["activity"]
        try registry.file.write([entry])
        XCTAssertEqual(registry.filter(snapshot,kind:"tab5").domesticActivity?.providers?["deepseek"]?.lastActivity,101)
        entry.sources=[]; try registry.file.write([entry])
        XCTAssertNil(registry.filter(snapshot,kind:"tab5").domesticActivity)
    }

    func testRecentClaudeSignalDoesNotGetHiddenByOldDomesticProvider() {
        let now=Date(), queue=DispatchQueue(label:"companion-test")
        let observed=Int64(now.timeIntervalSince1970)-60
        let domestic=MacDomesticActivity(activeProvider:"deepseek",state:"working",needsInput:false,tokensToday:2,providers:["deepseek":MacProviderActivity(tokensToday:2,lastActivity:observed)])
        let sampler=MacActivitySampler(queue:queue) { _ in MacActivityTotals(claude:ToolState(state:"idle",ageSeconds:30),domestic:domestic) }
        let reader=SessionActivityReader(metadata:sampler)
        _=reader.capture(); queue.sync {}
        XCTAssertTrue(reader.signals.record(agent:"claude",event:"PermissionRequest",now:now))
        let value=reader.capture()
        XCTAssertTrue(value.claude.needsInput)
        XCTAssertEqual(value.domesticActivity?.needsInput,true)
        XCTAssertEqual(value.domesticActivity?.providers?["deepseek"]?.lastActivity,observed)
    }
}
