import CryptoKit
import XCTest
import zlib
@testable import AIBotBridge

final class MacTab5GalleryBatchTests: XCTestCase {
    func testWindows321WireDigests() throws {
        // Golden values from the Windows handoff's .NET serializers and TextPages.
        let marker = "中文😀𠀀<>&+`'\"/\\\n\t\u{00A0}\u{2028}\u{200D}"
        let original = String(repeating: marker, count: 440)
        let guide: [String: Any] = ["workId": "fixture", "title": marker, "author": "作者", "overview": "说明", "transcription": original]
        let record = try MacTab5GalleryWire.record(workID: "fixture", guide: guide)
        XCTAssertEqual(MacTab5Protocol.sha256(record), "f6dccd337b223bcd44c80e08ef4e78f7505776c83856f1bce316350e83171151")
        XCTAssertEqual(MacTab5Protocol.sha256(try MacTab5GalleryWire.record(workID: "fixture", guide: nil)),
                       "e746b67ff84696c8fd7e10181f5d0c90779d5bd52ff7169b8234213d2b1b76b8")
        let projection = try XCTUnwrap(MacTab5Protocol.object(record)["guide"] as? [String: Any])
        XCTAssertEqual((projection["transcriptionPageTexts"] as? [String])?.joined(), original)
        let files = zip(["z.jpg", "guides.zh.json", "annual-index.json", "catalog.json", "a.jpg"], ["a","b","c","d","e"]).map {
            "\"\($0.0)\": \"\(String(repeating: $0.1, count: 64))\""
        }.joined(separator: ", ")
        let raw = Data(#"{"version":"2026.11.12.1", "schemaVersion":2,"category":"painting","works":1,"frames":2,"files":{\#(files)},"collection":"full"}"#.utf8)
        let manifest = try JSONDecoder().decode(MacTab5GalleryManifest.self, from: raw)
        XCTAssertEqual(try MacTab5GalleryWire.revision(manifest, bytes: raw, full: false), "2cd8f5c4826c0c2a68bb6b064616cb9dc99093f0fcfbed361ec88376c5f49c6c")
        XCTAssertEqual(try MacTab5GalleryWire.revision(manifest, bytes: raw, full: true), "9b8771f44572b36c4a8cd993ad2ab9bd0f91dc92a9e78f2ce942ed7fe1bf0e77")
        XCTAssertEqual(MacTab5GalleryWire.imageRevision(manifest, full: false), "de0a88efb94030aaca8bdc9d9d20f7822e484d31068e51a8dabe73756a396046")
        XCTAssertEqual(MacTab5GalleryWire.imageRevision(manifest, full: true), "c79ce71b8f49c7dce1623d0243fa3f234e8c252a20e5075bef27ed83b6d23032")
    }

    func testBatchMigratesLegacyMacAndAcceptsCheckedSummaryAcrossTransports() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let gallery = MacTab5Gallery(directory: root), images = try MacTab5TestData.galleryFiles()
        func install(_ version: String, overview: String, original: String) throws -> String {
            var files = images
            files["guides.zh.json"] = try MacTab5Protocol.json(["schemaVersion": 1, "language": "zh-CN", "works": [
                ["id": "fixture", "category": "painting", "workId": "fixture", "title": "合成测试", "author": "测试",
                 "overview": overview, "sources": ["synthetic"], "transcription": ["status": "reference_verified",
                  "text": "原文", "completeReferenceText": original]]
            ]])
            let manifest = MacTab5GalleryManifest(schemaVersion: 2, version: version, category: "painting", works: 1, frames: 2,
                                                 files: files.mapValues(MacTab5Protocol.sha256))
            let metadata = try JSONEncoder().encode(manifest)
            _ = try gallery.install(manifest: manifest, manifestBytes: metadata, names: Array(files.keys) + ["PACK.json"]) { files[$0]! }
            return MacTab5Protocol.sha256(metadata)
        }
        let legacy = try install("2026.11.03.1", overview: "旧导读", original: "旧原文")
        let first = try XCTUnwrap(gallery.revisions()["painting"])
        let original = String(repeating: "中文😀\n", count: 1300)
        _ = try install("2026.11.12.1", overview: "新导读", original: original)
        let ordinary = gallery.handle(["category": "painting", "op": "catalog"], binary: true, bulk: true)
        XCTAssertNil(ordinary.body["guideBatch"]); XCTAssertNil(ordinary.body["deltaSources"])
        let catalog = gallery.handle(["category": "painting", "op": "catalog", "guideBatch": "1"], binary: true, bulk: true)
        let revision = try XCTUnwrap(catalog.body["revision"] as? String)
        let imageRevision = try XCTUnwrap(catalog.body["imageRevision"] as? String)
        XCTAssertEqual(gallery.revisions()["painting"], revision); XCTAssertEqual(catalog.body["guideBatch"] as? Int, 1)
        let sources = try XCTUnwrap(catalog.body["deltaSources"] as? [String])
        XCTAssertTrue(sources.contains(legacy)); XCTAssertTrue(sources.contains(first))
        let delta = gallery.handle(["category": "painting", "op": "guideDelta", "from": legacy, "revision": revision], binary: true, bulk: true)
        XCTAssertEqual(delta.body["changed"] as? [Int], [0])
        let work = gallery.handle(["category": "painting", "op": "workGuide", "date": "2026-01-01", "revision": revision], binary: true, bulk: true)
        let fallbackGuide = try XCTUnwrap(work.body["guide"] as? [String: Any])
        let fallbackPage = gallery.handle(["category": "painting", "op": "guidePage", "date": "2026-01-01", "workId": "fixture",
                                          "sha256": try XCTUnwrap(fallbackGuide["transcriptionSha256"]), "page": 1], binary: true, bulk: true)
        XCTAssertEqual(fallbackPage.status, 200)
        XCTAssertEqual(fallbackPage.body["text"] as? String, try MacTab5ArtGuide.pages(original)[1])
        let key = Data(0..<32), device = "001122334455"
        let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.usbmodem-fixture", key: key, confirmed: true)
        let service = MacTab5Service(port: 8765, gallery: gallery, load: { pair }, save: { _ in })
        let server = HTTPStatusServer(port: 8765, token: { nil }, tab5: service, snapshot: { Data() })
        let common: [String: Any] = ["kind": "gallery", "category": "painting", "revision": revision, "from": legacy,
                                     "imageRevision": imageRevision, "binaryReply": true, "bulkReply": true]
        func rpc(_ extras: [String: Any], upload: Data = Data(), wifi: Bool = false, unauthorized: Bool = false) throws -> (Data, String) {
            let nonce = MacTab5Protocol.identifier()
            var body = common.merging(extras) { _, new in new }
            body["session"] = service.session; body["issuedAt"] = Int64(Date().timeIntervalSince1970 * 1000)
            let clear = try upload.isEmpty ? MacTab5Protocol.json(body) : MacTab5Binary.pack(body, bytes: upload, bulk: true, response: false)
            let packet = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce, maximum: MacTab5Binary.maximumRequest)
            let proof = unauthorized ? String(repeating: "0", count: 64)
                : MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
            if !wifi { return (service.rpc(device: device, nonce: nonce, proof: proof, packet: packet).1, nonce) }
            var request = Data("POST /tab5/v1/rpc HTTP/1.1\r\nX-AIBot-Device: \(device)\r\nX-AIBot-Nonce: \(nonce)\r\nX-AIBot-Proof: \(proof)\r\nContent-Length: \(packet.count)\r\n\r\n".utf8)
            request.append(packet)
            let response = server.response(for: request)
            XCTAssertTrue(response.starts(with: Data("HTTP/1.1 200 OK".utf8)))
            let header = try XCTUnwrap(response.range(of: Data("\r\n\r\n".utf8)))
            return (Data(response.suffix(from: header.upperBound)), nonce)
        }
        func reply(_ extras: [String: Any], upload: Data = Data(), wifi: Bool = false) throws -> [String: Any] {
            let (packet, nonce) = try rpc(extras, upload: upload, wifi: wifi)
            return try MacTab5Protocol.object(MacTab5Protocol.decrypt(packet, key: key, challenge: nonce))
        }
        let plan = try XCTUnwrap(reply(["op": "guideBatch"])["body"] as? [String: Any])
        XCTAssertEqual(plan["changed"] as? Int, 1)
        let size = try XCTUnwrap(plan["bytes"] as? Int), sha = try XCTUnwrap(plan["sha"] as? String)
        let request: [String: Any] = ["op": "guideBatchRead", "sha256": sha, "offset": 0, "count": size, "streamReply": true]
        XCTAssertEqual(try reply(request.merging(["count": size + 1]) { _, new in new })["status"] as? Int, 400)
        XCTAssertEqual(try reply(request.merging(["sha256": String(repeating: "a", count: 64)]) { _, new in new })["status"] as? Int, 409)
        let (packet, nonce) = try rpc(request, wifi: true)
        let compressed = try decodeStream(packet, key: key, nonce: nonce)
        let (usbPacket, usbNonce) = try rpc(request)
        let (usbReply, usbBytes) = try MacTab5Binary.open(MacTab5Protocol.decrypt(usbPacket, key: key, challenge: usbNonce), response: true)
        XCTAssertEqual(usbReply["status"] as? Int, 200)
        XCTAssertEqual(usbBytes, compressed)
        XCTAssertEqual(MacTab5Protocol.sha256(compressed), sha)
        var decodedSize = uLongf(try XCTUnwrap(plan["decoded"] as? Int)), decoded = Data(count: Int(decodedSize))
        let z = decoded.withUnsafeMutableBytes { out in
            compressed.withUnsafeBytes { input in
                uncompress(out.bindMemory(to: Bytef.self).baseAddress, &decodedSize, input.bindMemory(to: Bytef.self).baseAddress, uLong(compressed.count))
            }
        }
        XCTAssertEqual(z, Z_OK); XCTAssertEqual(Int(decodedSize), decoded.count)
        XCTAssertEqual(String(decoding: decoded.prefix(8), as: UTF8.self), "AIBGB002")
        XCTAssertEqual(MacTab5Mailbox.u32(decoded, 8), 1)
        XCTAssertEqual(String(decoding: decoded[12..<76], as: UTF8.self), legacy)
        XCTAssertEqual(String(decoding: decoded[76..<140], as: UTF8.self), imageRevision)
        XCTAssertEqual(MacTab5Mailbox.u32(decoded, 172), 180)
        XCTAssertEqual(Int(MacTab5Mailbox.u32(decoded, 176)), decoded.count - 180)
        let summary = Data(decoded[140..<172]), record = Data(decoded.dropFirst(180))
        XCTAssertEqual(summary, Data(SHA256.hash(data: record)))
        let guide = try XCTUnwrap(MacTab5Protocol.object(record)["guide"] as? [String: Any])
        XCTAssertEqual((guide["transcriptionPageTexts"] as? [String])?.joined(), original)
        let digest = MacTab5Protocol.sha256(summary)
        var upload: [String: Any] = ["op": "guideSummary", "sha256": digest, "offset": 0]
        let firstHalf = Data(summary.prefix(16)), secondHalf = Data(summary.suffix(16))
        XCTAssertTrue(try rpc(upload, upload: firstHalf, unauthorized: true).0.isEmpty)
        XCTAssertEqual(try reply(["op": "catalog"], upload: firstHalf)["status"] as? Int, 400)
        upload["offset"] = 16
        XCTAssertEqual(try reply(upload, upload: secondHalf)["status"] as? Int, 409)
        upload["offset"] = 0
        XCTAssertEqual(try reply(upload, upload: firstHalf)["status"] as? Int, 200)
        XCTAssertEqual(try reply(upload, upload: firstHalf, wifi: true)["status"] as? Int, 200)
        XCTAssertEqual(try reply(upload, upload: secondHalf)["status"] as? Int, 409)
        XCTAssertEqual(try reply(["op": "guideBatch", "summarySha": digest])["status"] as? Int, 409)
        upload["offset"] = 16
        XCTAssertEqual(try reply(upload, upload: secondHalf, wifi: true)["status"] as? Int, 200)
        let unchanged = try reply(["op": "guideBatch", "summarySha": digest])
        XCTAssertEqual((unchanged["body"] as? [String: Any])?["changed"] as? Int, 0)
        upload["sha256"] = String(repeating: "c", count: 64); upload["offset"] = 0
        XCTAssertEqual(try reply(upload, upload: summary)["status"] as? Int, 200)
        XCTAssertEqual(try reply(["op": "guideBatch", "summarySha": upload["sha256"]!])["status"] as? Int, 409)
        XCTAssertEqual(try reply(upload, upload: summary + Data([0]))["status"] as? Int, 409)
    }

    func testStreamBindsEachBlockAndRejectsCorruption() throws {
        let size = MacTab5Binary.bulk * 2 + 17
        let bytes = Data((0..<size).map { UInt8(truncatingIfNeeded: $0) })
        let key = Data(0..<32), nonce = MacTab5Protocol.identifier()
        let wire = try MacTab5GalleryStream.encode(.init(200, ["offset": 13, "count": size], bytes: bytes), key: key, nonce: nonce)
        XCTAssertEqual(try decodeStream(wire, key: key, nonce: nonce, expectedOffset: 13), bytes)
        var corrupt = wire; corrupt[55] ^= 1
        XCTAssertThrowsError(try decodeStream(corrupt, key: key, nonce: nonce, expectedOffset: 13))
        XCTAssertThrowsError(try decodeStream(Data(wire.dropLast()), key: key, nonce: nonce, expectedOffset: 13))
    }

    private func decodeStream(_ wire: Data, key: Data, nonce: String, expectedOffset: Int = 0) throws -> Data {
        guard wire.count >= 16, wire.starts(with: Data("T5S1".utf8)) else { throw MacTab5Error.invalidPacket }
        let offset = Int(MacTab5Mailbox.u32(wire, 4)), count = Int(MacTab5Mailbox.u32(wire, 8)), blocks = Int(MacTab5Mailbox.u32(wire, 12))
        XCTAssertEqual(offset, expectedOffset); XCTAssertEqual(blocks, (count + MacTab5Binary.bulk - 1) / MacTab5Binary.bulk)
        var result = Data(), cursor = 16
        for block in 0..<blocks {
            guard cursor + 4 <= wire.count else { throw MacTab5Error.invalidPacket }
            let length = Int(MacTab5Mailbox.u32(wire, cursor)); cursor += 4
            guard cursor + length <= wire.count else { throw MacTab5Error.invalidPacket }
            let packet = wire.subdata(in: cursor..<cursor + length); cursor += length
            let (metadata, raw) = try MacTab5Binary.open(MacTab5Protocol.decrypt(packet, key: key, challenge: nonce), response: true)
            XCTAssertEqual(metadata["status"] as? Int, 200)
            let body = try XCTUnwrap(metadata["body"] as? [String: Int])
            XCTAssertEqual(body, ["offset": offset + result.count, "count": raw.count, "streamOffset": offset,
                                  "streamBytes": count, "block": block, "blocks": blocks])
            result.append(raw)
        }
        XCTAssertEqual(cursor, wire.count); XCTAssertEqual(result.count, count)
        return result
    }
}
