import XCTest
@testable import AIBotBridge

final class MacTab5BenchmarkStopTests: XCTestCase {
    @MainActor func testStopOnceAndPollUntilDeviceAcknowledges() async throws {
        var actions:[String] = [], tick = 0.0
        let result = try await MacTab5BenchmarkStop.run(gallery:false,request:{ action in
            actions.append(action)
            return ["result":actions.count < 3 ? "running;USB,up,1,8192/262144":"cancelled;USB,up,1,8192,100,0"]
        },now:{ tick },wait:{ tick += 1 })
        XCTAssertEqual(actions,["cancel","status","status"])
        XCTAssertTrue(result.hasPrefix("cancelled;"))
    }
    @MainActor func testGalleryAndCompletedResultsKeepTheirActualState() async throws {
        var actions:[String] = []
        let result = try await MacTab5BenchmarkStop.run(gallery:true,request:{ action in
            actions.append(action); return ["result":actions.count == 1 ? "running;starting":"complete;gallery,USB,8,100,100,0,0,0,0"]
        },wait:{})
        XCTAssertEqual(actions,["gallery_cancel","gallery_status"])
        XCTAssertTrue(result.hasPrefix("complete;"))
    }
    @MainActor func testRunningResultCannotBeReportedAsStoppedAfterDeadline() async throws {
        var tick = 0.0
        do {
            _ = try await MacTab5BenchmarkStop.run(gallery:false,request:{ _ in ["result":"running;starting"] },now:{ tick },wait:{ tick += 8 })
            XCTFail("Unconfirmed running device was accepted")
        } catch { XCTAssertEqual(error as? MacTab5Error,.timeout) }
    }
}
