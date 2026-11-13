import CryptoKit
import Foundation
import zlib

// Access is serialized by the gallery lock. Keep one bounded transfer per category.
final class MacTab5GalleryBatch {
    static let maximumWire = 1024 * 1024
    private static let maximumDecoded = 3 * 1024 * 1024
    private struct Summary {
        let revision: String, from: String, sha: String
        var bytes = Data()
    }
    private struct Batch {
        let from: String, sha: String, compressed: Data, decoded: Int, changed: Int
    }
    private var summaries: [String: Summary] = [:]
    private var batches: [String: Batch] = [:]

    func reset(_ category: String? = nil) {
        if let category { summaries.removeValue(forKey: category); batches.removeValue(forKey: category) }
        else { summaries.removeAll(); batches.removeAll() }
    }

    func handle(_ request: [String: Any], category: String, revision: String, images: String, works: [MacTab5Artwork],
                guides: [String: [String: Any]], binary: Bool, bulk: Bool, stream: Bool, upload: Data,
                previous: (String) throws -> [String: [String: Any]]) -> MacTab5RPCResult {
        func text(_ key: String) -> String { request[key] as? String ?? "" }
        func number(_ key: String) -> Int { Int(MacTab5Protocol.integer(request[key]) ?? -1) }
        let unavailable = MacTab5RPCResult.error(409, "gallery_delta_unavailable")
        let from = text("from")
        guard text("revision") == revision, MacTab5Protocol.isHex(from, count: 64), from != revision else { return unavailable }
        if text("op") == "guideBatchRead" {
            let offset = number("offset"), count = number("count")
            guard let batch = batches[category], from == batch.from, text("sha256") == batch.sha else { return unavailable }
            guard binary, offset >= 0, (1...(stream ? Self.maximumWire : bulk ? MacTab5Binary.bulk : 8192)).contains(count),
                  offset <= batch.compressed.count - count else { return .error(400, "invalid_gallery_range") }
            return .init(200, ["offset": offset, "count": count], bytes: batch.compressed.subdata(in: offset..<offset + count))
        }
        let total = works.count * 32
        if text("op") == "guideSummary" {
            let sha = text("sha256"), offset = number("offset")
            guard binary, bulk, text("imageRevision") == images, MacTab5Protocol.isHex(sha, count: 64),
                  !upload.isEmpty, upload.count <= MacTab5Binary.bulk, offset >= 0, offset <= total - upload.count else { return unavailable }
            var summary = summaries[category]
            if summary?.revision != revision || summary?.from != from || summary?.sha != sha {
                guard offset == 0 else { return unavailable }
                summary = Summary(revision: revision, from: from, sha: sha)
            }
            guard var current = summary, offset <= current.bytes.count else { return unavailable }
            if offset == current.bytes.count { current.bytes.append(upload) }
            else if offset + upload.count > current.bytes.count || current.bytes.subdata(in: offset..<offset + upload.count) != upload {
                return unavailable
            }
            summaries[category] = current
            return .init(200, ["offset": offset, "count": upload.count])
        }
        do {
            let baseline: Data?
            let old: [String: [String: Any]]
            if text("summarySha").isEmpty {
                baseline = nil; old = try previous(from)
            } else {
                guard let summary = summaries[category], summary.revision == revision, summary.from == from,
                      summary.sha == text("summarySha"), summary.bytes.count == total,
                      MacTab5Protocol.sha256(summary.bytes) == summary.sha else { return unavailable }
                baseline = summary.bytes; old = [:]
            }
            let batch = try Self.build(works: works, guides: guides, old: old, baseline: baseline, from: from, images: images)
            batches[category] = batch
            return .init(200, ["revision": revision, "from": from, "works": works.count, "changed": batch.changed,
                               "sha": batch.sha, "bytes": batch.compressed.count, "decoded": batch.decoded])
        } catch { return unavailable }
    }

    private static func build(works: [MacTab5Artwork], guides: [String: [String: Any]], old: [String: [String: Any]],
                              baseline: Data?, from: String, images: String) throws -> Batch {
        var header = Data("AIBGB002".utf8), records = Data()
        MacTab5Mailbox.append32(UInt32(works.count), to: &header)
        header.append(contentsOf: from.utf8); header.append(contentsOf: images.utf8)
        let indexSize = header.count + works.count * 40
        var changed = 0
        for (i, work) in works.enumerated() {
            let id = work.workId ?? work.source
            let record = try MacTab5GalleryWire.record(workID: id, guide: guides[id])
            let digest = Data(SHA256.hash(data: record))
            let unchanged: Bool
            if let baseline { unchanged = baseline.subdata(in: i * 32..<(i + 1) * 32) == digest }
            else { unchanged = try MacTab5GalleryWire.record(workID: id, guide: old[id]) == record }
            header.append(digest)
            if unchanged {
                MacTab5Mailbox.append32(0, to: &header); MacTab5Mailbox.append32(0, to: &header)
            } else {
                guard record.count <= 64 * 1024, indexSize + records.count + record.count <= maximumDecoded else { throw MacTab5Error.invalidPacket }
                MacTab5Mailbox.append32(UInt32(indexSize + records.count), to: &header)
                MacTab5Mailbox.append32(UInt32(record.count), to: &header)
                records.append(record); changed += 1
            }
        }
        header.append(records)
        var length = uLongf(maximumWire), compressed = Data(count: maximumWire)
        let status = compressed.withUnsafeMutableBytes { dst in
            header.withUnsafeBytes { src in
                compress2(dst.bindMemory(to: Bytef.self).baseAddress, &length,
                          src.bindMemory(to: Bytef.self).baseAddress, uLong(header.count), Z_BEST_SPEED)
            }
        }
        guard status == Z_OK else { throw MacTab5Error.invalidPacket }
        compressed.count = Int(length)
        return Batch(from: from, sha: MacTab5Protocol.sha256(compressed), compressed: compressed, decoded: header.count, changed: changed)
    }
}
