import XCTest
@testable import AIBotBridge

final class AccountSpendParityTests: XCTestCase {
    func testGLMPairRequiresMatchingValidReport() throws {
        func parse(_ overview: String, _ report: String) throws -> DomesticProviderQuotaSnapshot {
            try MacDomesticQuotaParser.zhipuPair(Data("{\"code\":200,\"data\":{\"availableTotalBalance\":\(overview)},\"legacy\":\(report)}".utf8))
        }
        let report = #"{"code":200,"data":{"availableBalance":"9.999","totalSpendAmount":"2.239"}}"#
        let matched = try parse("10.004", report)
        XCTAssertEqual(matched.balance, 10); XCTAssertEqual(matched.usedCost, 2.23)
        XCTAssertNil(try parse("10.10", report).usedCost)
        for invalid in [#"{}"#, #"{"code":200,"data":{"availableBalance":10}}"#, #"{"code":200,"data":{"availableBalance":10,"totalSpendAmount":true}}"#, #"{"code":200,"data":{"availableBalance":10,"totalSpendAmount":-1}}"#] {
            XCTAssertNil(try parse("10", invalid).usedCost)
        }
        XCTAssertEqual(try parse("0", #"{"code":200,"data":{"availableBalance":0,"totalSpendAmount":0}}"#).usedCost, 0)
        XCTAssertThrowsError(try parse("true", report))
    }
    func testDeepSeekWebProjectionIsolationAndExpiry() throws {
        let name = "AccountSpendParityTests-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        defer { defaults.removePersistentDomain(forName: name) }
        let store = MacDataStore(defaults: defaults), now = Date()
        let api = try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":90}]}"#.utf8), now: now)
        store.updateDomestic(api, deepSeekAPI: true)
        XCTAssertNil(store.deviceSnapshot(now: now).domesticQuotas?.deepSeek)
        func web(_ amount: String, _ cost: String) throws -> DomesticProviderQuotaSnapshot {
            try MacDomesticSources.parse("deepseek", mode:"console", bytes:Data("{\"data\":{\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\(amount)}],\"total_costs\":[\(cost)]}}".utf8), now:now)
        }
        let good = try web("10", #"{"currency":"CNY","amount":0}"#)
        store.updateDeepSeekWeb(good)
        let glm = try MacDomesticQuotaParser.zhipu(Data(#"{"code":200,"data":{"availableBalance":10,"totalSpendAmount":2}}"#.utf8),now:now)
        store.updateDomestic(glm)
        let restored = MacDataStore(defaults:defaults)
        XCTAssertNil(restored.snapshot(now:now).domesticQuotas?.zhipu?.usedCost)
        XCTAssertEqual(restored.deepSeekWebSnapshot(now:now)?.usedCost, 0)
        store.updateDomestic(glm,validatedGLMPair:true)
        XCTAssertEqual(MacDataStore(defaults:defaults).snapshot(now:now).domesticQuotas?.zhipu?.usedCost, 2)
        XCTAssertEqual(store.snapshot(now:now).domesticQuotas?.deepSeek?.balance, 90)
        XCTAssertEqual(store.deviceSnapshot(now:now).domesticQuotas?.deepSeek?.balance, 10)
        XCTAssertEqual(store.deviceSnapshot(now:now).domesticQuotas?.deepSeek?.usedCost, 0)
        let reader = SessionActivityReader(metadata: MacActivitySampler(scan: { _ in MacActivityTotals() }))
        let device = reader.capture(extras:store.deviceSnapshot(now:now))
        let esp = try JSONSerialization.jsonObject(with: JSONEncoder().encode(device)) as! [String:Any]
        let quotas = esp["domesticQuotas"] as! [String:Any]
        let deep = quotas["deepSeek"] as! [String:Any]
        XCTAssertEqual(deep["balance"] as? Double, 10)
        XCTAssertEqual(deep["usedCost"] as? Double, 0)
        let packet = try MacTab5Protocol.snapshot(device,device:"001122334455",session:"fixture",sequence:1,command:nil)
        let data = try MacTab5Protocol.object(packet)["data"] as! [String:Any]
        let rows = data["quotaDetails"] as! [[String:Any]]
        XCTAssertEqual(rows[5]["balance"] as? Double, 10)
        XCTAssertEqual(rows[5]["cost"] as? Double, 0)
        store.markDeepSeekWebStale()
        XCTAssertEqual(store.deviceSnapshot(now:now).domesticQuotas?.deepSeek?.stale, true)
        XCTAssertNil(store.deviceSnapshot(now:now.addingTimeInterval(86400)).domesticQuotas?.deepSeek?.usedCost)
        store.updateDeepSeekWeb(try web("8", #"{"currency":"USD","amount":2}"#))
        XCTAssertEqual(store.deviceSnapshot(now:now).domesticQuotas?.deepSeek?.balance, 8)
        XCTAssertNil(store.deviceSnapshot(now:now).domesticQuotas?.deepSeek?.usedCost)
    }
}
