import AppKit
import XCTest
@testable import AIBotBridge

final class MacPermissionSetupWindowTests: XCTestCase {
    @MainActor func testInitialGuideOffersNotificationsAfterAccessibilityFollowUpWithoutWaiting() async throws {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        _ = NSApplication.shared
        let priorPolicy = NSApp.activationPolicy()
        if priorPolicy != .prohibited, !NSApp.setActivationPolicy(.prohibited) {
            throw XCTSkip("The test process cannot disable window activation")
        }
        defer { if priorPolicy != .prohibited { NSApp.setActivationPolicy(priorPolicy) } }

        var states: [Step: State] = [.notifications: .request, .systemEvents: .request, .accessibility: .settings]
        var sequence: [Step] = []
        var idleCount = 0
        var notificationDecision: CheckedContinuation<Void, Never>?
        let controller = MacPermissionSetupWindow(readState: { states[$0] ?? .unused }, request: { step, state in
            sequence.append(step)
            if step == .accessibility, state == .settings { return }
            if step == .notifications {
                await withCheckedContinuation { notificationDecision = $0 }
            }
            states[step] = .allowed
        })
        controller.onIdle = { idleCount += 1 }
        let window = try XCTUnwrap(controller.window)
        window.alphaValue = 0
        defer { notificationDecision?.resume(); window.close() }
        controller.open(initial: states, steps: [.notifications, .systemEvents, .accessibility], mode: .upgrade)
        for _ in 0..<100 where idleCount == 0 { try await Task.sleep(for: .milliseconds(10)) }
        XCTAssertEqual(sequence, [.systemEvents, .accessibility])
        XCTAssertEqual(idleCount, 1)
        XCTAssertNil(notificationDecision)

        states[.accessibility] = .request
        XCTAssertTrue(controller.resumeIfOpen())
        for _ in 0..<100 where notificationDecision == nil { try await Task.sleep(for: .milliseconds(10)) }
        XCTAssertEqual(sequence, [.systemEvents, .accessibility, .accessibility, .notifications])
        XCTAssertEqual(idleCount, 2)
        XCTAssertNotNil(notificationDecision)
        let finished = notificationDecision
        notificationDecision = nil
        finished?.resume()
    }

    @MainActor func testNotificationWaitDoesNotBlockSystemEvents() async throws {
        typealias Step = MacPermissionSetup.Step
        typealias State = MacPermissionSetup.State
        _ = NSApplication.shared
        let priorPolicy = NSApp.activationPolicy()
        if priorPolicy != .prohibited, !NSApp.setActivationPolicy(.prohibited) {
            throw XCTSkip("The test process cannot disable window activation")
        }
        defer { if priorPolicy != .prohibited { NSApp.setActivationPolicy(priorPolicy) } }

        var states: [Step: State] = [.notifications: .request, .systemEvents: .request]
        var notificationDecision: CheckedContinuation<Void, Never>?
        var requests: [Step] = []
        let controller = MacPermissionSetupWindow(readState: { states[$0] ?? .unused }, request: { step, _ in
            requests.append(step)
            if step == .notifications {
                await withCheckedContinuation { notificationDecision = $0 }
            }
            states[step] = .allowed
        })
        let window = try XCTUnwrap(controller.window)
        defer {
            notificationDecision?.resume()
            window.close()
        }
        func find(_ view: NSView, id: String) -> NSView? {
            if view.identifier?.rawValue == id { return view }
            return view.subviews.lazy.compactMap { find($0, id: id) }.first
        }
        let content = try XCTUnwrap(window.contentView)
        let notifications = try XCTUnwrap(find(content, id: "permission-notifications") as? NSButton)
        let settings = try XCTUnwrap(find(content, id: "permission-notification-settings") as? NSButton)
        let systemEvents = try XCTUnwrap(find(content, id: "permission-systemEvents") as? NSButton)
        let notificationStatus = try XCTUnwrap(find(content, id: "permission-status-notifications") as? NSTextField)
        controller.configurePresentation(mode: .upgrade, steps: [.notifications, .systemEvents])
        controller.display(states)
        window.alphaValue = 0
        window.orderFrontRegardless()
        notifications.performClick(nil)
        for _ in 0..<100 where notificationDecision == nil { try await Task.sleep(for: .milliseconds(10)) }
        XCTAssertEqual(requests, [.notifications])
        XCTAssertEqual(notificationStatus.stringValue, "请到右上角确认")
        XCTAssertTrue(settings.isEnabled && !settings.isHidden)
        XCTAssertTrue(systemEvents.isEnabled)

        systemEvents.performClick(nil)
        for _ in 0..<100 where !systemEvents.isHidden { try await Task.sleep(for: .milliseconds(10)) }
        XCTAssertEqual(requests, [.notifications, .systemEvents])
        XCTAssertNotNil(notificationDecision)
        XCTAssertEqual(notificationStatus.stringValue, "请到右上角确认")
        let finished = notificationDecision
        notificationDecision = nil
        finished?.resume()
    }

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
