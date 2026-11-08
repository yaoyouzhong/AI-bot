import XCTest
@testable import AIBotBridge

final class MacTab5UIAutomationTests: XCTestCase {
    func testFixtureWakeUsesOneActivityTimestampAndKeepsNewerRealInput() {
        let input = MacTab5AutomationInput()
        XCTAssertEqual(input.sample(now: 100, idle: 20).idle, 20)
        input.wake(now: 100)
        XCTAssertEqual(input.sample(now: 101, idle: 20).idle, 1)
        XCTAssertEqual(input.sample(now: 102, idle: 20).idle, 2)
        XCTAssertEqual(input.sample(now: 102, idle: 0.5).idle, 0.5)
        XCTAssertEqual(input.sample(now: 99, idle: 10).idle, 10)
        var activity = MacTab5InputActivity()
        XCTAssertEqual(activity.sample(uptime: 99, idle: 20), 79_000)
        let first = input.sample(now: 101, idle: 20)
        let later = input.sample(now: 102, idle: 20)
        XCTAssertEqual(activity.sample(uptime: first.uptime, idle: first.idle), 100_000)
        XCTAssertEqual(activity.sample(uptime: later.uptime, idle: later.idle), 100_000)
    }
    func testRadioAutomationKeepsUSBUnlessIsolationIsExplicit() throws {
        for radio in ["Wi-Fi", "BLE"] {
            let shared = try MacTab5AutomationRoute(radio, isolated: false)
            XCTAssertTrue(shared.usb)
            XCTAssertEqual(shared.captureTransport, "USB")
            XCTAssertEqual(shared.requestedRPC, "auto")
            XCTAssertEqual(shared.wifi, radio == "Wi-Fi")
            XCTAssertEqual(shared.bluetooth, radio == "BLE")
            let isolated = try MacTab5AutomationRoute(radio, isolated: true)
            XCTAssertFalse(isolated.usb)
            XCTAssertEqual(isolated.captureTransport, radio)
            XCTAssertEqual(isolated.requestedRPC, radio)
        }
        let usb = try MacTab5AutomationRoute("USB", isolated: true)
        XCTAssertTrue(usb.usb)
        XCTAssertEqual(usb.requestedRPC, "USB")
        XCTAssertThrowsError(try MacTab5AutomationRoute("unknown", isolated: false))
    }
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
