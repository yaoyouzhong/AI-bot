import AppKit
import XCTest
@testable import AIBotBridge

final class MacPermissionSetupWindowTests: XCTestCase {
    @MainActor func testReturnsToSameWindowAndRefreshesAfterAccessibilityApproval() async throws {
        typealias State = MacPermissionSetup.State
        _ = NSApplication.shared
        let priorPolicy = NSApp.activationPolicy()
        if priorPolicy != .prohibited {
            guard NSApp.setActivationPolicy(.prohibited) else {
                throw XCTSkip("The test process cannot disable window activation")
            }
        }
        defer {
            if priorPolicy != .prohibited { NSApp.setActivationPolicy(priorPolicy) }
        }

        var accessibility = State.settings
        var requests: [State] = []
        let controller = MacPermissionSetupWindow(readState: {
            $0 == .accessibility ? accessibility : .allowed
        }, request: { step, state in
            if step == .accessibility { requests.append(state) }
        })
        let window = try XCTUnwrap(controller.window)
        let content = try XCTUnwrap(window.contentView)
        func descendants(_ view: NSView) -> [NSView] {
            [view] + view.subviews.flatMap(descendants)
        }
        let views = descendants(content)
        let button = try XCTUnwrap(views.first { $0.identifier?.rawValue == "permission-accessibility" } as? NSButton)
        let status = try XCTUnwrap(views.first { $0.identifier?.rawValue == "permission-status-accessibility" } as? NSTextField)
        func waitUntil(_ condition: () -> Bool) async throws {
            for _ in 0..<100 {
                if condition() { return }
                try await Task.sleep(for: .milliseconds(20))
            }
            XCTFail("Expected the permission window to update")
        }

        controller.configurePresentation(mode: .upgrade, steps: [.accessibility])
        controller.display([.accessibility: .settings])
        XCTAssertFalse(controller.resumeIfOpen())
        window.alphaValue = 0
        window.orderFrontRegardless()
        defer { if window.isVisible { window.close() } }
        XCTAssertTrue(window.isVisible)
        button.performClick(nil)
        try await waitUntil { requests == [.settings] && button.isEnabled }
        XCTAssertEqual(status.stringValue, "当前 App 未授权")

        // Keep the same checklist pending while the simulated system sheet is in use.
        // Native acceptance can exercise a real-time wait with this isolated fixture.
        if ProcessInfo.processInfo.environment["AIBOT_TEST_SLOW_PERMISSION_APPROVAL"] == "1" {
            try await Task.sleep(for: .seconds(35))
        }
        XCTAssertTrue(window.isVisible)
        XCTAssertEqual(requests, [.settings])

        accessibility = .allowed
        XCTAssertTrue(controller.resumeIfOpen())
        XCTAssertTrue(controller.window === window)
        try await waitUntil { status.stringValue == "已授权" }
        XCTAssertTrue(button.isHidden)
        XCTAssertEqual(requests, [.settings])
        window.close()
        XCTAssertFalse(controller.resumeIfOpen())
    }
}
