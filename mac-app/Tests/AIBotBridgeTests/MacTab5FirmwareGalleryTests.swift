import CryptoKit
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
import XCTest
@testable import AIBotBridge

enum MacTab5TestData {
    // Synthetic ESP image structure, with no executable code or device data.
    static func firmware(version: String = "0.2.234-ui") -> Data {
        var bytes = Data(repeating: 0, count: 1024)
        bytes[0] = 0xe9; bytes[1] = 1; bytes[12] = 18; bytes[23] = 1
        let size = 944
        for index in 0..<4 { bytes[28 + index] = UInt8((size >> (8 * index)) & 255) }
        bytes.replaceSubrange(32..<36, with: [0x32, 0x54, 0xcd, 0xab])
        bytes.replaceSubrange(48..<48 + version.utf8.count, with: version.utf8)
        bytes.replaceSubrange(80..<90, with: "aibot_tab5".utf8)
        bytes[991] = bytes[32..<976].reduce(UInt8(0xef), ^)
        bytes.replaceSubrange(992..<1024, with: Data(SHA256.hash(data: bytes.prefix(992))))
        return bytes
    }
    static func jpeg() throws -> Data {
        let pixels = Data(repeating: 180, count: 1280 * 720 * 3)
        let image = try XCTUnwrap(CGImage(width: 1280, height: 720, bitsPerComponent: 8, bitsPerPixel: 24, bytesPerRow: 3840,
                                         space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: [], provider: CGDataProvider(data: pixels as CFData)!, decode: nil, shouldInterpolate: false, intent: .defaultIntent))
        let output = NSMutableData()
        let destination = try XCTUnwrap(CGImageDestinationCreateWithData(output, UTType.jpeg.identifier as CFString, 1, nil))
        CGImageDestinationAddImage(destination, image, nil); XCTAssertTrue(CGImageDestinationFinalize(destination)); return output as Data
    }
    static func galleryFiles() throws -> [String: Data] {
        let image = try jpeg()
        let work = MacTab5Artwork(id: "fixture", category: "painting", title: "合成测试", author: "测试", source: "fixture", license: "MIT", frames: ["landscape.jpg"], workId: "fixture", portraitFrames: ["portrait.jpg"])
        return ["catalog.json": try JSONEncoder().encode([work]), "landscape.jpg": image, "portrait.jpg": image,
                "provenance.json": Data("{}".utf8), "selection.json": Data("{}".utf8), "LICENSE": Data("synthetic fixture".utf8), "THIRD_PARTY_NOTICES.md": Data("No external artwork".utf8)]
    }
}

final class MacTab5FirmwareGalleryTests: XCTestCase {
    func testUpgradeZIPWithPublishedDependencyLicenseLayoutLoadsBoundNotes() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let bytes = MacTab5TestData.firmware(), expected = try MacTab5FirmwareImage(bytes: bytes)
        let license = "licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE"
        let licenseURL = root.appendingPathComponent(license)
        try FileManager.default.createDirectory(at: licenseURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("Synthetic license fixture".utf8).write(to: licenseURL)
        try bytes.write(to: root.appendingPathComponent("aibot_tab5.bin"))
        let notes = "## 修复\n- 合成测试说明"
        try MacTab5Protocol.json(["version": expected.version, "sha256": expected.sha256, "notes": notes]).write(to: root.appendingPathComponent("aibot_tab5.bin.notes.json"))
        let zip = root.appendingPathComponent("upgrade.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-q", zip.path, "aibot_tab5.bin", "aibot_tab5.bin.notes.json", license], directory: root)
        let image = try MacTab5FirmwareImage.load(zip)
        XCTAssertEqual(image.bytes, bytes); XCTAssertEqual(image.sha256, expected.sha256); XCTAssertEqual(image.notes, notes)
    }

    func testNestedArchivePathsRemainBoundedAndRejectTraversalAndPatterns() {
        XCTAssertTrue(MacTab5Archive.safePath("licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE"))
        XCTAssertTrue(MacTab5Archive.safePath(Array(repeating: "dir", count: 15).joined(separator: "/") + "/LICENSE"))
        for path in ["../LICENSE", "/LICENSE", "licenses//LICENSE", "licenses/./LICENSE", "licenses/../LICENSE", "licenses\\LICENSE", "licenses/*.txt", "licenses/[ab].txt", "-LICENSE", Array(repeating: "dir", count: 16).joined(separator: "/") + "/LICENSE", String(repeating: "a", count: 721)] {
            XCTAssertFalse(MacTab5Archive.safePath(path), path)
        }
    }

    func testFirmwareChecksChipProjectSegmentsDigestAndBoundNotes() throws {
        let bytes = MacTab5TestData.firmware()
        let image = try MacTab5FirmwareImage(bytes: bytes)
        XCTAssertEqual(image.version, "0.2.234-ui"); XCTAssertEqual(image.bytes.count, 1024)
        var bad = bytes; bad[12] = 0; XCTAssertThrowsError(try MacTab5FirmwareImage(bytes: bad))
        bad = bytes; bad[500] ^= 1; XCTAssertThrowsError(try MacTab5FirmwareImage(bytes: bad))
        bad = bytes; bad[28] = 255; XCTAssertThrowsError(try MacTab5FirmwareImage(bytes: bad))
        let notes = try MacTab5Protocol.json(["version": image.version, "sha256": image.sha256, "notes": "### 修复\n- 合成说明"])
        XCTAssertEqual(try MacTab5FirmwareImage(bytes: bytes, sidecar: notes).notes, "### 修复\n- 合成说明")
        XCTAssertThrowsError(try MacTab5FirmwareImage(bytes: bytes, sidecar: MacTab5Protocol.json(["version": "wrong", "sha256": image.sha256, "notes": "bad"])))
    }
    func testOTAIsAnExplicitOfferAndStartRequiresMatchingDeviceReceipt() throws {
        let control = MacTab5Firmware(), image = try MacTab5FirmwareImage(bytes: MacTab5TestData.firmware())
        try control.select(image); XCTAssertNil(control.projection().health["otaCommand"])
        try control.start(device: "001122334455")
        let command = control.projection().health["otaCommand"] as! [String: Any]
        var report = command; report["kind"] = "ota-control"; report["stage"] = "ready"
        XCTAssertEqual(control.handle(report, device: "ffffffffffff", transport: "USB", binary: false, bulk: false).status, 409)
        XCTAssertEqual(control.handle(report, device: "001122334455", transport: "USB", binary: false, bulk: false).body["commit"] as? Bool, true)
        XCTAssertThrowsError(try control.select(nil))
        report["stage"] = "started"
        XCTAssertEqual(control.handle(report, device: "001122334455", transport: "USB", binary: false, bulk: false).status, 200)
        XCTAssertFalse(control.summary.contains("核验通过"))
    }
    func testBulkFirmwareReadUsesAuthenticatedReplayProtectedRPC() throws {
        let key = Data(0..<32), device = "001122334455", nonce = MacTab5Protocol.identifier()
        let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.usbmodem-fixture", key: key, confirmed: true)
        let service = MacTab5Service(port: 8765, load: { pair }, save: { _ in })
        let image = try MacTab5FirmwareImage(bytes: MacTab5TestData.firmware()); try service.firmwareControl.select(image)
        let root: [String: Any] = ["kind": "ota", "session": service.session, "issuedAt": Int64(Date().timeIntervalSince1970 * 1000),
                                   "offerId": image.offerID, "sha256": image.sha256, "offset": 0, "count": 1024, "binaryReply": true, "bulkReply": true]
        let packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: key, challenge: nonce)
        let proof = MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
        let response = service.rpc(device: device, nonce: nonce, proof: proof, packet: packet, transport: "BLE")
        let (reply, raw) = try MacTab5Binary.open(MacTab5Protocol.decrypt(response.1, key: key, challenge: nonce), response: true)
        XCTAssertEqual(reply["status"] as? Int, 200); XCTAssertEqual(raw, image.bytes)
        let replay = service.rpc(device: device, nonce: nonce, proof: proof, packet: packet)
        XCTAssertEqual(try MacTab5Protocol.object(MacTab5Protocol.decrypt(replay.1, key: key, challenge: nonce))["status"] as? Int, 409)
        XCTAssertEqual(service.ota(device: device, nonce: nonce, proof: proof, sha: image.sha256).0, 401)
        let otaProof = MacTab5Protocol.proof(key: key, message: "GET|/tab5/v1/ota/\(image.sha256)|\(device)|\(nonce)")
        let server = HTTPStatusServer(port: 8765, token: { nil }, tab5: service, snapshot: { Data() })
        let request = Data("GET /tab5/v1/ota/\(image.sha256) HTTP/1.1\r\nX-AIBot-Device: \(device)\r\nX-AIBot-Nonce: \(nonce)\r\nX-AIBot-Proof: \(otaProof)\r\n\r\n".utf8)
        let served = server.response(for: request)
        XCTAssertTrue(served.starts(with: Data("HTTP/1.1 200 OK".utf8))); XCTAssertEqual(Data(served.suffix(image.bytes.count)), image.bytes)
    }
    func testBootAcceptanceRequiresExactImageValidPartitionAndAdvancingUI() throws {
        let image = try MacTab5FirmwareImage(bytes: MacTab5TestData.firmware()), device = "001122334455"
        let hello: [String: Any] = ["type": "tab5_hello", "version": 1, "deviceId": device, "firmware": image.version, "uptimeMs": 1000, "uiAgeMs": 100, "flushErrors": 0, "flushCount": 3]
        var diagnostic: [String: Any] = ["type": "tab5_ota_diagnostic", "firmware": image.version, "elfSha256": image.elfSHA256, "partition": "ota_1", "address": 0x700000, "stateError": 0, "state": 2]
        XCTAssertEqual(try MacTab5Firmware.validateBoot(hello: hello, diagnostic: diagnostic, image: image, device: device, previous: 999), 1000)
        XCTAssertThrowsError(try MacTab5Firmware.validateBoot(hello: hello, diagnostic: diagnostic, image: image, device: device, previous: 1000))
        diagnostic["state"] = 0
        XCTAssertThrowsError(try MacTab5Firmware.validateBoot(hello: hello, diagnostic: diagnostic, image: image, device: device, previous: 0))
    }
    func testGalleryZIPImportManifestAndExactBinaryRange() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let files = try MacTab5TestData.galleryFiles()
        let manifest = MacTab5GalleryManifest(schemaVersion: 1, version: "2026.10.19", category: "painting", works: 1, frames: 2, files: files.mapValues(MacTab5Protocol.sha256))
        for (name, bytes) in files { try bytes.write(to: root.appendingPathComponent(name)) }
        try JSONEncoder().encode(manifest).write(to: root.appendingPathComponent("PACK.json"))
        let zip = root.appendingPathComponent("fixture.zip")
        _ = try MacTab5LocalProcess.run("/usr/bin/zip", ["-j", zip.path] + (Array(files.keys) + ["PACK.json"]).map { root.appendingPathComponent($0).path })
        let gallery = MacTab5Gallery(directory: root.appendingPathComponent("installed"))
        XCTAssertEqual(try gallery.install(zip).works, 1)
        var request: [String: Any] = ["category": "painting", "date": "2026-10-19", "op": "manifest", "orientation": "portrait", "frame": 0]
        let reply = gallery.handle(request, binary: true, bulk: true)
        XCTAssertEqual(reply.status, 200); XCTAssertEqual(reply.body["sha256"] as? String, manifest.files["portrait.jpg"])
        request["op"] = "read"; request["sha256"] = reply.body["sha256"]; request["offset"] = 1; request["count"] = 30
        XCTAssertEqual(gallery.handle(request, binary: true, bulk: true).bytes, Data(files["portrait.jpg"]![1..<31]))
        request["date"] = "2026-02-30"; XCTAssertEqual(gallery.handle(request, binary: true, bulk: true).status, 400)
    }
    func testCorruptOrUnsafeGalleryCannotReplaceExistingPack() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let gallery = MacTab5Gallery(directory: root), files = try MacTab5TestData.galleryFiles()
        let manifest = MacTab5GalleryManifest(schemaVersion: 1, version: "2026.10.19", category: "painting", works: 1, frames: 2, files: files.mapValues(MacTab5Protocol.sha256))
        let metadata = try JSONEncoder().encode(manifest), names = Array(files.keys) + ["PACK.json"]
        _ = try gallery.install(manifest: manifest, manifestBytes: metadata, names: names) { files[$0]! }
        let before = gallery.revisions()
        XCTAssertThrowsError(try gallery.install(manifest: manifest, manifestBytes: metadata, names: names) { _ in Data([0]) })
        XCTAssertEqual(gallery.revisions(), before)
        XCTAssertThrowsError(try gallery.install(manifest: manifest, manifestBytes: metadata, names: names + ["../outside.jpg"]) { files[$0]! })
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.deletingLastPathComponent().appendingPathComponent("outside.jpg").path))
    }
    func testGuidePaginationPreservesUnicodeAndVerifiedTextOnly() throws {
        let text = String(repeating: "中文😀\n", count: 2000)
        let pages = try MacTab5ArtGuide.pages(text)
        XCTAssertGreaterThan(pages.count, 1); XCTAssertEqual(pages.joined(), text); XCTAssertTrue(pages.allSatisfy { $0.utf8.count <= 4095 })
        XCTAssertThrowsError(try MacTab5ArtGuide.pages(String(repeating: "文", count: 11000)))
        let projected = try MacTab5ArtGuide.projection(["transcription": text], version: 3)
        XCTAssertEqual(projected["transcriptionPages"] as? Int, pages.count)
        XCTAssertEqual(projected["transcriptionSha256"] as? String, MacTab5Protocol.sha256(Data(text.utf8)))
    }
    func testCachedGalleryRevalidatesChangedCatalogAndImageBeforeServing() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString); defer { try? FileManager.default.removeItem(at: root) }
        let gallery = MacTab5Gallery(directory: root), files = try MacTab5TestData.galleryFiles()
        let manifest = MacTab5GalleryManifest(schemaVersion: 1, version: "2026.10.19", category: "painting", works: 1, frames: 2, files: files.mapValues(MacTab5Protocol.sha256))
        _ = try gallery.install(manifest: manifest, manifestBytes: JSONEncoder().encode(manifest), names: Array(files.keys) + ["PACK.json"]) { files[$0]! }
        let request: [String: Any] = ["category": "painting", "op": "manifest", "date": "2026-10-19"]
        XCTAssertEqual(gallery.handle(request, binary: true, bulk: true).status, 200)
        try Data([0,1,2]).write(to: root.appendingPathComponent("painting/landscape.jpg"), options: .atomic)
        XCTAssertEqual(gallery.handle(request, binary: true, bulk: true).status, 503)
        try Data("[]".utf8).write(to: root.appendingPathComponent("painting/catalog.json"), options: .atomic)
        XCTAssertEqual(gallery.handle(["category": "painting", "op": "catalog"], binary: true, bulk: true).status, 503)
    }
}
