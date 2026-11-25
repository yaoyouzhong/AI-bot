import AppKit
import XCTest
@testable import AIBotBridge

final class MacDeviceCenterAlignmentTests: XCTestCase {
    @MainActor private func withCenter(_ check: (MacTab5Window, () -> [String]) throws -> Void) throws {
        _ = NSApplication.shared
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let suite = "AI-bot-alignment-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }),
                                enabled: { false }, call: { _, _ in [:] })
        let computer = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in false }))
        let voice = MacTab5Voice(enabled: { false }, backend: .init(start: { _, _ in }, feed: { _ in }, finish: {}, cancel: {}))
        let service = MacTab5Service(port: 0, host: { nil }, gallery: .init(directory: root),
                                     computer: computer, codex: codex, voice: voice,
                                     birthdays: MacBirthdays(directory: root), agenda: MacAgenda(directory: root),
                                     load: { nil }, save: { _ in })
        var actions: [String] = []
        let center = MacTab5Window(service: service, usb: MacTab5USB(service: service),
                                   bridgeAction: { actions.append(String(describing: $0)) })
        defer { center.window?.close() }
        let content = try XCTUnwrap(center.window?.contentView)
        let tabs = try XCTUnwrap(content.subviews.compactMap { $0 as? NSTabView }.first)
        tabs.selectTabViewItem(at: 2)
        content.layoutSubtreeIfNeeded()
        try check(center, { actions })
    }

    private func descendants(_ view: NSView) -> [NSView] {
        [view] + view.subviews.flatMap(descendants)
    }

    @MainActor func testTrayOmitsPermissionsAndDeviceCenterStillRoutesToPermissions() async throws {
        _ = NSApplication.shared
        let devices = [MacRegisteredDevice(kind: "tab5", name: "TAB5"),
                       MacRegisteredDevice(kind: "esp8266", name: "小屏")]
        let menu = MacDeviceCenterMenu.build(devices: devices, deviceAction: { _, _ in }, common: { _ in })
        XCTAssertEqual(menu.items.map { $0.isSeparatorItem ? "|" : $0.title },
                       ["设备中心", "设备预览", "|", "小屏", "TAB5", "|", "退出"])
        try withCenter { center, actions in
            let content = try XCTUnwrap(center.window?.contentView)
            let permission = try XCTUnwrap(descendants(content).compactMap { $0 as? MacBridgeActionCard }
                .first { $0.identifier?.rawValue == "system-permissions" })
            XCTAssertEqual(permission.title, "系统授权")
            XCTAssertFalse(permission.isHiddenOrHasHiddenAncestor)
            permission.performClick(nil)
            XCTAssertEqual(actions(), ["permissions"])
        }
    }

    @MainActor func testUpgradeCardRendersCompactBadgeAndRetainsClickAcrossColumns() async throws {
        try withCenter { center, actions in
            let window = try XCTUnwrap(center.window)
            let content = try XCTUnwrap(window.contentView)
            let card = try XCTUnwrap(descendants(content).compactMap { $0 as? MacBridgeActionCard }
                .first { $0.identifier?.rawValue == "updates" })
            let grid = try XCTUnwrap(card.superview as? MacBridgeCardGrid)
            let tabs = try XCTUnwrap(content.subviews.compactMap { $0 as? NSTabView }.first)
            let page = try XCTUnwrap(tabs.selectedTabViewItem?.view as? MacBridgeSettingsPage)
            // The two-column grid is also used at narrow widths. Exercise the
            // same live shared page at both breakpoints without opening it.
            window.contentMinSize = NSSize(width: 520, height: 500)
            window.appearance = NSAppearance(named: .aqua)
            let preview = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-alignment-ui-" + UUID().uuidString)
            try FileManager.default.createDirectory(at: preview, withIntermediateDirectories: true)
            print("Alignment UI previews: \(preview.path)")

            func bitmap() throws -> NSBitmapImageRep {
                card.needsDisplay = true
                let rep = try XCTUnwrap(card.bitmapImageRepForCachingDisplay(in: card.bounds))
                card.effectiveAppearance.performAsCurrentDrawingAppearance { card.cacheDisplay(in: card.bounds, to: rep) }
                return rep
            }
            func color(_ image: NSBitmapImageRep, _ x: Int, _ y: Int) -> NSColor? {
                image.colorAt(x: x, y: y)?.usingColorSpace(.sRGB)
            }
            func greenBounds(_ image: NSBitmapImageRep) -> NSRect {
                var box = NSRect.null
                let sx = CGFloat(image.pixelsWide) / card.bounds.width
                let sy = CGFloat(image.pixelsHigh) / card.bounds.height
                for y in 0..<image.pixelsHigh {
                    for x in 0..<image.pixelsWide {
                        guard let pixel = color(image, x, y),
                              pixel.greenComponent > pixel.redComponent + 0.18,
                              pixel.greenComponent > pixel.blueComponent + 0.12 else { continue }
                        let point = NSRect(x: CGFloat(x) / sx, y: CGFloat(y) / sy, width: 1 / sx, height: 1 / sy)
                        box = box.union(point)
                    }
                }
                return box
            }
            func region(_ image: NSBitmapImageRep, y: Range<Int>) -> [NSColor] {
                let sx = CGFloat(image.pixelsWide) / card.bounds.width
                let sy = CGFloat(image.pixelsHigh) / card.bounds.height
                return (Int(CGFloat(y.lowerBound) * sy)..<Int(CGFloat(y.upperBound) * sy)).flatMap { row in
                    (Int(16 * sx)..<Int(100 * sx)).compactMap { color(image, $0, row) }
                }
            }
            for (columns, width) in [(2, CGFloat(560)), (3, CGFloat(920))] {
                window.setContentSize(NSSize(width: width, height: 620))
                content.layoutSubtreeIfNeeded()
                page.fitDocument()
                content.layoutSubtreeIfNeeded()
                XCTAssertEqual(grid.columns, columns)
                XCTAssertGreaterThan(card.bounds.width, 150)
                center.setAvailableUpdates(0)
                XCTAssertEqual(card.title, "软件固件")
                XCTAssertEqual(card.toolTip, "查看软件、固件和图库更新")
                let normal = try bitmap()
                XCTAssertTrue(greenBounds(normal).isNull)
                XCTAssertTrue(region(normal, y: 12..<34).contains {
                    $0.redComponent < 0.55 && $0.greenComponent < 0.55 && $0.blueComponent < 0.55
                }, "The title must actually be painted")
                card.performClick(nil)
                try MacTab5UIValidation.render(content, to: preview.appendingPathComponent("\(columns)-columns-clear.png"))
                center.setAvailableUpdates(2)
                XCTAssertEqual(card.toolTip, "2 项更新可用")
                XCTAssertTrue(card.showsUpgradeArrow)
                let updated = try bitmap()
                let badge = greenBounds(updated)
                XCTAssertFalse(badge.isNull)
                XCTAssertEqual(badge.width, 14, accuracy: 2)
                XCTAssertEqual(badge.height, 14, accuracy: 2)
                XCTAssertEqual(card.bounds.width - badge.maxX, 8, accuracy: 2)
                XCTAssertEqual(badge.minY, 8, accuracy: 2)
                XCTAssertTrue(region(normal, y: 12..<34) == region(updated, y: 12..<34), "Title rendering must not shrink or move")
                XCTAssertTrue(region(normal, y: 38..<58) != region(updated, y: 38..<58), "Update count must change the rendered detail")
                card.performClick(nil)
                try MacTab5UIValidation.render(content, to: preview.appendingPathComponent("\(columns)-columns-update.png"))
                center.setAvailableUpdates(0)
                XCTAssertFalse(card.showsUpgradeArrow)
                XCTAssertEqual(card.toolTip, "查看软件、固件和图库更新")
                let cleared = try bitmap()
                XCTAssertTrue(greenBounds(cleared).isNull)
                XCTAssertTrue(region(normal, y: 38..<58) == region(cleared, y: 38..<58))
                card.performClick(nil)
            }
            XCTAssertEqual(actions(), Array(repeating: "updates", count: 6))
        }
    }
}
