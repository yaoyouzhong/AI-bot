import XCTest
@testable import AIBotBridge

final class MacTab5TaskAppendTests: XCTestCase {
    func testNativeTaskAppendJournalAndDraftProtection() throws {
        try MacTab5TaskAppendValidation.run()
    }
    func testEmptyAttachmentContainersDoNotBlockButUserContentDoes() {
        struct Node { var role: String; var text = ""; var children: [Node] = [] }
        func empty(_ node: Node) -> Bool {
            MacTab5DesktopAX.emptyAttachmentLayout(node, role: { $0.role }, children: { $0.children }, text: { $0.text })
        }
        XCTAssertTrue(empty(.init(role: "AXGroup")))
        XCTAssertTrue(empty(.init(role: "AXGroup", children: [.init(role: "AXScrollArea", children: [.init(role: "AXGroup")])])))
        for role in ["AXImage", "AXLink", "AXButton", "AXWebArea"] {
            XCTAssertFalse(empty(.init(role: "AXGroup", children: [.init(role: role)])))
        }
        XCTAssertFalse(empty(.init(role: "AXGroup", children: [.init(role: "AXStaticText", text: "quoted text")])))
        XCTAssertFalse(empty(.init(role: "AXGroup", children: Array(repeating: .init(role: "AXGroup"), count: 81))))
    }
}
