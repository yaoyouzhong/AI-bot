import Foundation
import XCTest
@testable import AIBotBridge

final class GalleryDeliveryImportTests: XCTestCase {
    func testPublishedPackageImport() throws {
        let environment = ProcessInfo.processInfo.environment
        let archive = try XCTUnwrap(environment["GALLERY_TEST_PACKAGE"])
        let category = try XCTUnwrap(environment["GALLERY_TEST_CATEGORY"])
        let kind = try XCTUnwrap(environment["GALLERY_TEST_KIND"])
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("gallery-import-" + UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        let gallery = MacTab5Gallery(directory: directory)
        let installed = try gallery.install(URL(fileURLWithPath: archive))
        XCTAssertEqual(installed.category, category)
        XCTAssertEqual(installed.works, 366)
        XCTAssertEqual(try gallery.installed(category).works, 366)
        if kind == "hd" {
            let work = try XCTUnwrap(gallery.browserWorks(category).first)
            let preview = try gallery.browserPage(work, frame: 0).0
            let root = gallery.directory.appendingPathComponent(category).appendingPathComponent("desktop")
            let bytes = try Data(contentsOf: root.appendingPathComponent("desktop-index.relative.json"))
            let index = try JSONDecoder().decode(MacGalleryDesktopIndex.self, from: bytes)
            XCTAssertNotNil(try index.image(root: root, work: work, frame: 0, preview: preview))
            XCTAssertNotNil(gallery.hdEdition(category))
        }
    }
}
