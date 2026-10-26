import XCTest
@testable import AIBotBridge

final class MacTab5HDGalleryTests: XCTestCase {
    func testHDOuterZIPImportsVerifiedOriginalsAndCorruptionKeepsPrevious() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString), standard = root.appendingPathComponent("standard"), bundle = root.appendingPathComponent("bundle")
        try FileManager.default.createDirectory(at: standard, withIntermediateDirectories: true); try FileManager.default.createDirectory(at: bundle.appendingPathComponent("作品"), withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        var files = try MacTab5TestData.galleryFiles()
        files["guides.zh.json"] = try MacTab5Protocol.json(["schemaVersion": 1, "language": "zh-CN", "works": [["id": "fixture", "category": "painting", "workId": "fixture", "title": "测试", "author": "测试", "overview": "测试导读", "sources": ["fixture"], "transcription": ["status": "not_available"]]]])
        let manifest = MacTab5GalleryManifest(schemaVersion: 2, version: "2026.10.19", category: "painting", works: 1, frames: 2, files: files.mapValues(MacTab5Protocol.sha256))
        files["PACK.json"] = try JSONEncoder().encode(manifest)
        for (name, bytes) in files { try bytes.write(to: standard.appendingPathComponent(name)) }
        let zip = bundle.appendingPathComponent("standard.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-q", "-j", zip.path] + files.keys.sorted().map { standard.appendingPathComponent($0).path })
        let original = files["landscape.jpg"]!, sourcePath = "作品/原作 [测试]—高清.jpg"
        let index = MacGalleryDesktopIndex(schema:1,sources:["fixture":.init(path:sourcePath,sha256:MacTab5Protocol.sha256(original),width:1280,height:720)],pages:[.init(category:"painting",id:"fixture",workId:"fixture",frame:"landscape.jpg",previewSha256:MacTab5Protocol.sha256(original),width:1280,height:720,parts:[.init(source:"fixture",crop:[0,0,1280,720],dest:[0,0,1280,720],rotation:0)])])
        var outer = ["standard.zip": try Data(contentsOf: zip), "catalog.json": try MacTab5Protocol.json(["schema": 1, "category": "painting", "edition": "fixture", "works": [["id": "fixture"]]]), "desktop-index.relative.json": try JSONEncoder().encode(index), sourcePath: original]
        for (name, bytes) in outer { try bytes.write(to: bundle.appendingPathComponent(name)) }
        let sums = outer.keys.sorted().map { MacTab5Protocol.sha256(outer[$0]!) + "  " + $0 }.joined(separator: "\n")
        try sums.write(to: bundle.appendingPathComponent("SHA256SUMS.txt"), atomically: true, encoding: .utf8)
        let archive = root.appendingPathComponent("full-hd.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-q", "-r", archive.path, "bundle"], directory: root)
        let gallery = MacTab5Gallery(directory: root.appendingPathComponent("installed"))
        XCTAssertEqual(try gallery.install(archive).schemaVersion, 2)
        XCTAssertEqual(gallery.handle(["category": "painting", "op": "manifest", "date": "2026-10-19"], binary: true, bulk: true).status, 200)
        XCTAssertTrue(FileManager.default.fileExists(atPath: gallery.directory.appendingPathComponent("painting/desktop/" + sourcePath).path))
        let previous = gallery.revisions(); outer["standard.zip"] = Data([1,2,3]); try outer["standard.zip"]!.write(to: zip)
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-q", archive.path, "bundle/standard.zip"], directory: root)
        XCTAssertThrowsError(try gallery.install(archive)); XCTAssertEqual(gallery.revisions(), previous)
        XCTAssertFalse(MacTab5Archive.safeUnicodePath("bundle/../outside.jpg"))
    }
}
