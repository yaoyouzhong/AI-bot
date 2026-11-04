import XCTest
@testable import AIBotBridge

final class MacTab5UIAutomationTests: XCTestCase {
    func testLeaseBoundedCommandAndExactRequestAcknowledgement() throws {
        let control = MacTab5UIAutomation(), now: Int64 = 100_000
        XCTAssertNil(control.command(frameEpoch:now,now:now))
        let id = try control.begin(["op":"tap","width":1280,"height":720,"rotation":1,"x":100,"y":200],now:now)
        let command = try XCTUnwrap(control.command(frameEpoch:now-2000,now:now))
        XCTAssertEqual(command["expiresAt"] as? Int64,now+3000)
        XCTAssertEqual(command["toX"] as? Int64,100)
        XCTAssertThrowsError(try control.begin(["op":"probe"],now:now))
        control.observe("1,\(String(repeating:"0",count:32)),2,1280,720,1")
        XCTAssertNil(control.snapshot())
        control.observe("1,\(id),1,1280,720,1")
        XCTAssertEqual(control.snapshot()?.status,1)
        control.observe("1,\(id),2,1280,720,1")
        XCTAssertEqual(control.snapshot()?.status,2)
        XCTAssertNil(control.command(frameEpoch:now,now:now))
        let next = try control.begin(["op":"probe"],now:now)
        control.observe("1,\(id),2,1280,720,1")
        XCTAssertNil(control.snapshot())
        XCTAssertNotEqual(id,next)
        XCTAssertNil(control.command(frameEpoch:now,now:now+5000))
        control.cancel()
    }
    func testInvalidCoordinatesAndDurationsNeverCreateCommands() throws {
        let control = MacTab5UIAutomation()
        for action: [String:Any] in [
            ["op":"send"],
            ["op":"tap","width":1280,"height":720,"rotation":1,"x":1280,"y":0],
            ["op":"tap","width":1280,"height":720,"rotation":true,"x":1,"y":0],
            ["op":"swipe","width":1280,"height":720,"rotation":1,"x":1,"y":1,"toX":1,"toY":10,"durationMs":1201]
        ] { XCTAssertThrowsError(try control.begin(action)) }
    }
}
