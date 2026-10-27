import Foundation
import AppKit
import ImageIO
import MacKeychainSupport

/// Deterministic checks can also run on machines whose Command Line Tools omit XCTest.
/// All mutable profiles use a temporary directory; no network, Keychain or device access.
enum MacParityValidation {
    static func run() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-parity-" + UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:false)
        defer { try? FileManager.default.removeItem(at:root) }
        var checked = 0
        func check(_ name: String, _ condition: @autoclosure () throws -> Bool) throws { guard try condition() else { throw MacTab5LocalError.invalid("Parity check failed: " + name) }; checked += 1 }
        func rejects(_ name: String, _ action: () throws -> Void) throws { do { try action() } catch { checked += 1; return }; throw MacTab5LocalError.invalid("Expected rejection: " + name) }
        func json(_ value: Any) throws -> Data { try JSONSerialization.data(withJSONObject:value,options:[.sortedKeys]) }
        func epoch(_ value: String) -> Int64 { Int64(ISO8601DateFormatter().date(from:value)!.timeIntervalSince1970) }
        let now = epoch("2026-10-22T08:00:00Z")
        let birthdays = MacBirthdays(directory:root)
        let leap = MacBirthday(name:"示例",month:2,day:29,lunar:false,leap:false,remind:3)
        try birthdays.file.write([leap]); try check("birthday round trip",birthdays.file.read() == [leap])
        try check("leap birthday last day",MacBirthdays.occurrence(leap,year:2027) == ISO8601DateFormatter().date(from:"2027-02-28T00:00:00Z"))
        try rejects("invalid solar date") { try MacBirthdays.validate([.init(name:"示例",month:4,day:31,lunar:false,leap:false,remind:0)]) }
        try rejects("birthday count") { try MacBirthdays.validate(Array(repeating:leap,count:33)) }
        let corrupt = root.appendingPathComponent("corrupt"); try FileManager.default.createDirectory(at:corrupt,withIntermediateDirectories:false)
        let bad = Data("broken".utf8); try bad.write(to:corrupt.appendingPathComponent("birthdays.json"))
        try rejects("preserve corrupt profile") { try MacBirthdays(directory:corrupt).file.write([]) }
        try check("corrupt bytes untouched",Data(contentsOf:corrupt.appendingPathComponent("birthdays.json")) == bad)
        try check("official make-up workday",MacTab5Calendar.isWorkday(civil:epoch("2026-10-10T00:00:00Z")) == true)
        try check("holiday",MacTab5Calendar.isWorkday(civil:epoch("2026-10-01T00:00:00Z")) == false)
        try check("unpublished year unknown",MacTab5Calendar.isWorkday(civil:epoch("2027-01-01T00:00:00Z")) == nil)
        try check("workday calendar names required by device",(MacTab5Calendar.workdayYears[0]["holidays"] as? [[String:Any]])?.allSatisfy { $0["name"] is String } == true)
        let agenda = MacAgenda(directory:root,now:{now})
        let item = MacAgendaItem(title:"同步检查",due:now+3600,end:now+7200,repeat:1,lead:5)
        try agenda.save(item,expected:nil); let saved = try agenda.file.read().items[0]
        let before = try Data(contentsOf:agenda.file.url), reply = agenda.handle(["op":"sync","workdaySupport":1])
        try check("agenda sync capability",reply.status == 200 && reply.body["retentionSupport"] as? Int == 2)
        try check("reading does not write",Data(contentsOf:agenda.file.url) == before)
        var remote = saved; remote.edit = MacProfileValidation.id(); remote.title = "离线编辑"
        let edit: [String:Any] = ["op":"sync","workdaySupport":1,"edits":[["baseEdit":saved.edit,"item":try MacProfileValidation.object(remote)]]]
        try check("matching edit replaces",agenda.handle(edit).status == 200 && agenda.file.read().items.count == 1)
        let revision = try agenda.file.read().revision
        try check("same edit retry idempotent",agenda.handle(edit).status == 200 && agenda.file.read().revision == revision)
        var conflicting = saved; conflicting.edit = MacProfileValidation.id(); conflicting.title = "另一份编辑"
        let conflict: [String:Any] = ["op":"sync","workdaySupport":1,"edits":[["baseEdit":saved.edit,"item":try MacProfileValidation.object(conflicting)]]]
        try check("concurrent edit preserves both",agenda.handle(conflict).status == 200 && agenda.file.read().items.count == 2)
        try check("fork id stable",agenda.file.read().items.contains { $0.id == conflicting.edit && $0.title == conflicting.title })
        try check("fork retry idempotent",agenda.handle(conflict).status == 200 && agenda.file.read().items.count == 2)
        let delete: [String:Any] = ["op":"sync","workdaySupport":1,"deletions":[["id":saved.id,"edit":saved.edit,"baseEdit":saved.edit]]]
        try check("delete conflict retains newer host edit",agenda.handle(delete).status == 200 && agenda.file.read().items.contains { $0.id == remote.edit })
        let unchanged = try agenda.file.read()
        try check("invalid envelope atomic",agenda.handle(["op":"sync","actions":[["op":"invalid"]]]).status == 400 && agenda.file.read() == unchanged)
        let capacity = MacAgenda(directory:root.appendingPathComponent("full"),now:{now})
        for i in 0..<16 { try capacity.save(.init(title:"条目 \(i)"),expected:nil) }
        try rejects("full queue preserves entries") { try capacity.save(.init(title:"超出上限"),expected:nil) }
        try check("full count unchanged",capacity.file.read().items.count == 16)
        let dated = MacAgendaItem(title:"结束规则",due:now-100,end:now+100)
        try check("end keeps active interval",!MacAgendaRules.view(dated,now:now).ended)
        try check("end expires at boundary",MacAgendaRules.view(dated,now:now+100).ended)
        let futureWorkday = MacAgendaItem(title:"未来工作日",due:epoch("2027-01-01T08:00:00Z"),end:epoch("2027-01-01T09:00:00Z"),repeat:4)
        try check("unpublished repeat pauses",MacAgendaRules.view(futureWorkday,now:now).calendarPending)
        let pins = MacTaskPins(directory:root), id = UUID().uuidString.lowercased(); try pins.save([id])
        try check("pin persistence",MacTaskPins(directory:root).read() == [id])
        try rejects("pin duplicates") { try pins.save([id,id]) }; try rejects("pin cap") { try pins.save((0..<33).map { _ in UUID().uuidString }) }
        let registry = MacDeviceRegistry(directory:root); try registry.add("tab5",hardwareID:"fixture")
        try rejects("one device per kind") { try registry.add("tab5") }; var device = registry.device("tab5")!; device.name = "我的设备"; device.enabled = false; try registry.update(device)
        try check("disable persists",MacDeviceRegistry(directory:root).device("tab5")?.enabled == false)
        try registry.remove("tab5"); try check("remove registry only",registry.device("tab5") == nil && birthdays.file.read() == [leap])
        let stocks = try MacStockSearch.parse(#"v_hint="sh~600000~浦发银行~pfyh~GP-A^us~AAPL.oq~Apple~apple~GP^sh~600000~浦发银行~pfyh~GP-A";"#)
        try check("stock search markets dedup",stocks.map(\.symbol) == ["sh600000","usAAPL"])
        try rejects("never execute search JS") { _ = try MacStockSearch.parse("alert('bad')") }
        let rows = MacQuotaHistory.build([],now:Date(timeIntervalSince1970:Double(now)),days:30)
        try check("30 days missing stays missing",rows.days.count == 30 && rows.days.allSatisfy { $0.used == nil && $0.partial })
        let apiFixtures: [(String,String,[String:Any],Double?)] = [
            ("deepseek","api",["balance_infos":[["currency":"CNY","total_balance":"12.5"]]],12.5),
            ("stepfun","api",["object":"account","type":"prepaid","balance":20],20),
            ("qwen","balance",["Success":true,"Code":"200","Data":["AvailableCashAmount":"1,234.50","Currency":"CNY"]],1234.5),
            ("kimi","balance",["code":0,"status":true,"data":["available_balance":30]],30),
            ("baidu","balance",["cashBalance":8],8),
            ("zhipu","console",["code":200,"data":["availableBalance":18.25,"totalSpendAmount":1]],18.25)
        ]
        for (provider,mode,response,balance) in apiFixtures { let parsed = try MacDomesticSources.parse(provider,mode:mode,bytes:json(response)); try check("official balance \(provider)",parsed.balance == balance && parsed.primaryPercent == nil) }
        let mini = try MacDomesticSources.parse("minimax",mode:"api",bytes:json(["data":["current_weekly_used_percent":32,"current_interval_remaining_percent":80,"weekly_remains_time":10000]]))
        try check("minimax used vs remaining",mini.weeklyPercent == 32 && mini.primaryPercent == 20)
        let kimi = try MacDomesticSources.parse("kimi",mode:"local",bytes:json(["data":["kind":"ok","quota":["usages":["limit5h":["usedRatio":0.2],"limit7d":["usedRatio":0.4]]]]]))
        try check("Kimi subscription ratios",kimi.primaryPercent == 20 && kimi.weeklyPercent == 40 && kimi.balance == nil)
        let kimiWindows = try MacDomesticSources.parse("kimi",mode:"local",bytes:json(["data":["kind":"ok","summary":["window":["duration":1,"unit":"week"],"used":40,"limit":100],"limits":[["window":["duration":5,"unit":"hour"],"used":20,"limit":100]]]]))
        try check("Kimi explicit window units",kimiWindows.primaryWindowSeconds == 18000 && kimiWindows.weeklyPercent == 40)
        let glm = try MacDomesticSources.parse("zhipu",mode:"plan",bytes:json(["success":true,"data":["limits":[["type":"TOKENS_LIMIT","percentage":36,"unit":3,"number":5],["type":"TIMES_LIMIT","percentage":99]]]]))
        try check("GLM token budget excludes MCP",glm.primaryPercent == 36 && glm.primaryWindowSeconds == 18000)
        let mimo = try MacDomesticSources.parse("xiaomi",mode:"console",bytes:json(["data":["usagePercent":20,"totalCredits":100,"usedCredits":20]]))
        try check("MiMo plan",mimo.planPercent == 20 && mimo.balance == nil)
        try rejects("MiMo conflicting counters") { _ = try MacDomesticSources.parse("xiaomi",mode:"console",bytes:json(["data":["usagePercent":20,"usedRate":0.9]])) }
        try rejects("GLM failure cannot replace cache") { _ = try MacDomesticSources.parse("zhipu",mode:"plan",bytes:json(["success":false,"data":["limits":[]]])) }
        let local = try MacDomesticSources.request("kimi",.init(mode:"local",key:"fixture",port:5494))
        try check("local token only loopback",local.url?.host == "127.0.0.1" && local.url?.port == 5494)
        let glmRequest = try MacDomesticSources.request("zhipu",.init(mode:"plan",key:"fixture"))
        try check("GLM raw API key",glmRequest.value(forHTTPHeaderField:"Authorization") == "fixture")
        for provider in MacDeviceRegistry.providers.map(\.0) { try BridgeKeychain.Request(operation:"save",account:"domestic-provider-v1-"+provider,value:Data([1])).validate(); checked += 1 }
        try rejects("keychain unknown provider") { try BridgeKeychain.Request(operation:"read",account:"domestic-provider-v1-other").validate() }
        try check("finance endpoint rejects suffix",!MacDomesticBrowser.endpoint(provider:"xiaomi",url:URL(string:"https://platform.xiaomimimo.com.evil/api/v1/tokenPlan/usage")!))
        for id in ["deepseek","stepfun","baidu"] {
            let url = MacDomesticSources.externalConsoleURL(id)
            try check("API provider has HTTPS console without changing auth mode",url?.scheme == "https" && url?.query == nil && MacDomesticSources.modes(id).first?.0 != "console")
        }
        try check("embedded console providers and unknown IDs do not fall back to an unrelated site",["qwen","kimi","minimax","zhipu","xiaomi","unknown"].allSatisfy { MacDomesticSources.externalConsoleURL($0) == nil })
        let archive = MacConfigurationArchive(settings:["display_mode":"auto","stock_symbols":"sh000001,usAAPL"],birthdays:[leap]), path = root.appendingPathComponent("backup.json")
        try archive.data().write(to:path); try check("Windows migration archive roundtrip",MacConfigurationArchive.read(path).birthdays == [leap])
        try rejects("credentials excluded from backup") { try MacConfigurationArchive(settings:["api_key":"secret"],birthdays:[]).validate() }
        let hash = String(repeating:"a",count:64)
        try check("checksum exact filename",MacReleaseCatalog.checksum(Data((hash+"  file.zip\n").utf8),name:"file.zip") == hash)
        try rejects("duplicate checksum") { _ = try MacReleaseCatalog.checksum(Data((hash+"  file.zip\n"+hash+"  file.zip\n").utf8),name:"file.zip") }
        let options = MacNotificationOptions(quiet:true,quietStart:1320,quietEnd:480)
        let midnight = Calendar.current.date(from:DateComponents(year:2026,month:10,day:22,hour:1))!
        try check("quiet hours span midnight",options.isQuiet(midnight)); try rejects("invalid quiet range") { try MacNotificationOptions(quiet:true,quietStart:480,quietEnd:480).validate() }
        let actions = MacAgenda(directory:root.appendingPathComponent("actions"),now:{now})
        try actions.save(.init(title:"完成与稍后",due:now+60,end:now+3600,lead:10),expected:nil)
        let actionItem = try actions.file.read().items[0]
        let snooze: [String:Any] = ["op":"snooze","id":actionItem.id,"edit":actionItem.edit,"occurrence":actionItem.due,"until":now+600]
        try check("snooze persists",actions.handle(["op":"sync","actions":[snooze]]).status == 200 && actions.file.read().items[0].snoozeUntil == now+600)
        let complete: [String:Any] = ["op":"complete","id":actionItem.id,"edit":actionItem.edit,"occurrence":actionItem.due]
        try check("complete clears snooze",actions.handle(["op":"sync","actions":[complete]]).status == 200 && actions.file.read().items[0].snoozeUntil == 0)
        let actionRevision = try actions.file.read().revision
        try check("late snooze cannot undo completion",actions.handle(["op":"sync","actions":[snooze,complete]]).status == 200 && actions.file.read().revision == actionRevision)
        let historyPath = root.appendingPathComponent("hourly.json"), history = MacQuotaHistory(file:historyPath)
        let q = ProviderQuotaSnapshot(provider:"codex",plan:"fixture",primaryPercent:23,primaryResetsAt:Date(timeIntervalSince1970:Double(now+3600)),weeklyPercent:40,weeklyResetsAt:nil,resetCreditsAvailable:nil,resetCreditExpiresAt:[],updatedAt:Date(timeIntervalSince1970:Double(now)),stale:false)
        history.record(q,account:String(repeating:"a",count:64))
        try check("five-hour history survives reload",MacQuotaHistory(file:historyPath).hourly(days:7,now:q.updatedAt).first?.primary == 23)
        try rejects("monitor thresholds bounded") { try MacRuntimeOptions(cpu:101).validate() }
        let direct = MacNetworkProbeResult(status:200,milliseconds:100,proxy:false,failure:"")
        try check("explicit proxy expectation checked",MacNetworkDiagnostics.checks(results:[direct,direct],pac:false,routes:[Set([false]),Set([false])],now:Date(),expectProxy:true).first?["state"] as? String == "warning")
        try check("version parser strict",MacReleaseCatalog.version("0.2.237-ui") == [0,2,237] && MacReleaseCatalog.version("1.x.2.3") == nil)
        let canvas = CGContext(data:nil,width:16,height:8,bitsPerComponent:8,bytesPerRow:0,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
        canvas.setFillColor(CGColor(red:0.8,green:0.1,blue:0.2,alpha:1)); canvas.fill(CGRect(x:0,y:0,width:16,height:8))
        let imageBytes = NSMutableData(), destination = CGImageDestinationCreateWithData(imageBytes,"public.png" as CFString,1,nil)!
        CGImageDestinationAddImage(destination,canvas.makeImage()!,nil); try check("HD fixture encoded",CGImageDestinationFinalize(destination))
        let original = imageBytes as Data, preview = Data("test preview".utf8), sourceHash = MacTab5Protocol.sha256(original)
        try original.write(to:root.appendingPathComponent("original.png"))
        let work = MacTab5Artwork(id:"fixture",category:"painting",title:"测试",author:"AI-bot",source:"https://example.com/art",license:"test",frames:["frame.jpg"],workId:"fixture",portraitFrames:["portrait.jpg"])
        var hd = MacGalleryDesktopIndex(schema:1,sources:["original":.init(path:"original.png",sha256:sourceHash,width:16,height:8)],pages:[.init(category:"painting",id:work.id,workId:"fixture",frame:"frame.jpg",previewSha256:MacTab5Protocol.sha256(preview),width:16,height:8,parts:[.init(source:"original",crop:[0,0,16,8],dest:[0,0,16,8],rotation:0)])])
        try hd.validate(category:"painting",works:[work],hashes:["original.png":sourceHash],previewHashes:["frame.jpg":MacTab5Protocol.sha256(preview)])
        try check("HD original decodes and composites",hd.image(root:root,work:work,frame:0,preview:preview)?.size == NSSize(width:16,height:8))
        hd.pages[0].parts[0].crop = [0,0,17,8]
        try rejects("HD out-of-bounds rejected") { try hd.validate(category:"painting",works:[work],hashes:["original.png":sourceHash],previewHashes:["frame.jpg":MacTab5Protocol.sha256(preview)]) }
        let noticeAt = Date(timeIntervalSince1970:10000), noQuiet = MacNotificationOptions()
        try check("notification completion stays on device",MacNotifications.outcome(kind:"completion",options:noQuiet,at:noticeAt,last:nil,active:true,onDevice:true) == "由 TAB5 提醒")
        try check("completion uses backup sound",MacNotifications.outcome(kind:"completion",options:noQuiet,at:noticeAt,last:nil,active:true,onDevice:false) == "已请求备用音")
        try check("quota coalesces five minutes",MacNotifications.outcome(kind:"quota",options:noQuiet,at:noticeAt,last:noticeAt.addingTimeInterval(-299),active:true,onDevice:false) == "合并频繁提醒")
        try check("attention permits after ten seconds",MacNotifications.outcome(kind:"attention",options:noQuiet,at:noticeAt,last:noticeAt.addingTimeInterval(-10),active:true,onDevice:false) == "已请求通知")
        try check("disabled notices retained as disabled",MacNotifications.outcome(kind:"health",options:.init(health:false),at:noticeAt,last:nil,active:true,onDevice:false) == "已关闭")
        try check("ESP full Windows rotation catalog",MacDisplayPolicy.modes.count == 16 && Set(MacDisplayPolicy.modes).count == 16 && MacDisplayPolicy.modes.allSatisfy(SerialBridge.displayModes.contains))
        for mode in MacDisplayPolicy.modes + ["activity","domestic","quotas"] { try MacConfigurationArchive(settings:["display_mode":mode],birthdays:[]).validate() }
        try check("all ESP pages restore",MacConfigurationArchive(settings:["display_cycle_pages":MacDisplayPolicy.modes.joined(separator:",")],birthdays:[]).data().count > 0)
        let lanFixture = HTTPStatusServer(port:8765,token:{ String(repeating:"x",count:32) },snapshot:{Data("{}".utf8)})
        _ = lanFixture.response(for:"GET /status HTTP/1.1\r\nX-AIBot-Token: wrong\r\n\r\n")
        try check("unauthorized request not a heartbeat",lanFixture.lastESPRequest == nil)
        _ = lanFixture.response(for:"GET /status HTTP/1.1\r\nX-AIBot-Token: \(String(repeating:"x",count:32))\r\n\r\n",loopback:true)
        try check("local diagnostic not a device heartbeat",lanFixture.lastESPRequest == nil)
        _ = lanFixture.response(for:"GET /status HTTP/1.1\r\nX-AIBot-Token: \(String(repeating:"x",count:32))\r\n\r\n")
        try check("authenticated device heartbeat recorded",lanFixture.lastESPRequest != nil)
        checked += try MacLatestParityValidation.run(root:root.appendingPathComponent("latest"))
        checked += try MacGalleryConnectionValidation.run(root:root.appendingPathComponent("gallery-connection"))
        checked += try MacDoubaoValidation.run()
        checked += try MacVNCUnlockValidation.run()
        checked += try MacHostUnlockValidation.run()
        checked += try MacControlReceiptValidation.run()
        checked += try MacOrphanDraftValidation.run(root:root.appendingPathComponent("orphan-drafts"))
        checked += try MacARDValidation.run()
        checked += try MacDeviceCenterMenuValidation.run()
        checked += try MacTab5ScreenPreviewValidation.run()
        checked += try MacPreviewMemoryObservationValidation.run()
        checked += try MacMusicArtworkValidation.run()
        checked += try MacTab5USBResourceBurstValidation.run()
        checked += try MacPreviewUSBPacingValidation.run()
        checked += try MacTab5WiFiStatusValidation.run()
        checked += try MacFirmwareCommandValidation.run()
        checked += try MacCodexPublicHistoryValidation.run(root: root.appendingPathComponent("public-history"))
        checked += try MacTab5USBStatusValidation.run()
        checked += try MacTab5USBLeaseValidation.run()
        checked += try MacHTTPListenerRecoveryValidation.policy()
        checked += try MacApplicationInstanceValidation.run()
        checked += try MacTab5USBOperationWaitValidation.run()
        checked += try MacSolarTermSourceValidation.run(root: root.appendingPathComponent("solar-source"))
        checked += try MacVoiceSchedulingValidation.run(root: root.appendingPathComponent("voice-scheduling"))
        checked += try MacPreviewHDValidation.run()
        checked += try MacLANIdentityValidation.run(root:root.appendingPathComponent("lan-identity"))
        checked += try MacESPDiscoveryValidation.policy()
        checked += try MacESPStatusValidation.run()
        checked += try MacPetImportValidation.run()
        checked += try MacLocalizedTextValidation.run()
        checked += try MacModelAccountValidation.run()
        checked += try MacTab5USBVoicePriorityValidation.run()
        checked += try MacVoiceTerminalPriorityValidation.run(root:root.appendingPathComponent("voice-terminal"))
        print("MAC_PARITY_SELF_TEST_OK \(checked) checks; isolated storage, no credentials/network/hardware")
    }
}
