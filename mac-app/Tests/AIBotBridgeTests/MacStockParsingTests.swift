import XCTest
@testable import AIBotBridge

final class MacStockParsingTests: XCTestCase {
    private func quote(_ symbol: String, providerCode: String) -> String {
        var fields = Array(repeating: "", count: 33)
        fields[1] = "Fixture"; fields[2] = providerCode
        fields[3] = "20"; fields[31] = "1"; fields[32] = "5"
        return "v_\(symbol)=\"\(fields.joined(separator: "~"))\";"
    }
    func testLeadingDotIndexAndShareClassRemainCompleteDisplayCodes() {
        let payload = [quote("us.IXIC", providerCode: ".IXIC"), quote("usBRK.B", providerCode: "BRK.B.N")].joined(separator: "\n")
        let parsed = MacDataService.parseStocks(payload, order: ["us.IXIC", "usBRK.B"])
        XCTAssertEqual(parsed.map(\.code), [".IXIC", "BRK.B"])
        XCTAssertEqual(parsed.map(\.symbol), ["us.IXIC", "usBRK.B"])
    }
    func testProviderSuffixOrEmptyCodeCannotEraseConfiguredIdentity() {
        let payload = [quote("usAAPL", providerCode: "AAPL.OQ"), quote("usLI", providerCode: "")].joined(separator: "\n")
        let parsed = MacDataService.parseStocks(payload, order: ["usLI", "usAAPL"])
        XCTAssertEqual(parsed.map(\.code), ["LI", "AAPL"])
        XCTAssertTrue(parsed.allSatisfy { !$0.name.isEmpty && !$0.price.isEmpty && !$0.changePercent.isEmpty })
    }
    func testMainlandAndHongKongCodesKeepLeadingZerosAndRequestedOrder() {
        let payload = [quote("hk00700", providerCode: "700"), quote("sh000001", providerCode: "000001")].joined(separator: "\n")
        let parsed = MacDataService.parseStocks(payload, order: ["sh000001", "hk00700"])
        XCTAssertEqual(parsed.map(\.code), ["000001", "00700"])
        XCTAssertEqual(parsed.map(\.trend), [1, 1])
        XCTAssertEqual(parsed.map(\.changePercent), ["+5.00%", "+5.00%"])
    }
}
