import Foundation
import ImageIO

final class MacTab5Photos {
    private struct Upload { let binding: String, sha: String; let size: Int; var bytes: Data; var touched: TimeInterval }
    private let lock = NSLock()
    private let store: MacTab5PrivateStore
    private var uploads: [String: Upload] = [:]
    init(store: MacTab5PrivateStore) { self.store = store }
    static func imageType(_ bytes: Data) throws -> String {
        guard (32...2 * 1024 * 1024).contains(bytes.count), let source = CGImageSourceCreateWithData(bytes as CFData, nil), CGImageSourceGetCount(source) == 1,
              let type = CGImageSourceGetType(source) as String?, ["public.jpeg", "public.png"].contains(type),
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [String: Any],
              let width = properties[kCGImagePropertyPixelWidth as String] as? Int, let height = properties[kCGImagePropertyPixelHeight as String] as? Int,
            width > 0, height > 0, width <= 4096, height <= 4096, width * height <= 4_194_304,
              CGImageSourceCreateImageAtIndex(source, 0, [kCGImageSourceShouldCacheImmediately: true] as CFDictionary) != nil else { throw MacTab5Error.invalidPacket }
        return type == "public.jpeg" ? "image/jpeg" : "image/png"
    }
    private func name(_ id: String) -> String { "image|" + id }
    private func saved(_ id: String) throws -> [String: Any]? { try store.read(name(id)).map(MacTab5Protocol.object) }
    func upload(_ root: [String: Any], bytes: Data, device: String) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        guard let task = MacTab5Codex.id(root["taskId"]), let id = MacTab5Codex.id(root["requestId"]), !bytes.isEmpty else { return .error(400, "invalid_image") }
        let binding = device + "|" + task, chunked = root["kind"] as? String == "image-chunk"
        let size = chunked ? Int(MacTab5Protocol.integer(root["size"]) ?? -1) : bytes.count
        let sha = chunked ? root["sha256"] as? String ?? "" : MacTab5Protocol.sha256(bytes)
        let offset = chunked ? Int(MacTab5Protocol.integer(root["offset"]) ?? -1) : 0
        guard (32...2 * 1024 * 1024).contains(size), offset >= 0, offset <= size, bytes.count <= size - offset,
              !chunked || bytes.count <= MacTab5Binary.bulk, MacTab5Protocol.isHex(sha, count: 64) else { return .error(400, "invalid_range") }
        do {
            if let previous = try saved(id) {
                guard previous["binding"] as? String == binding, previous["sha256"] as? String == sha, previous["size"] as? Int == size else { return .error(409, "image_id_conflict") }
                return .init(200, ["status": "uploaded", "imageId": id, "sha256": sha, "offset": size])
            }
            let now = ProcessInfo.processInfo.systemUptime; uploads = uploads.filter { now - $0.value.touched < 90 }
            if uploads[id] == nil {
                guard offset == 0, uploads.count < 2 else { return .error(409, "image_offset_or_capacity") }
                uploads[id] = Upload(binding: binding, sha: sha, size: size, bytes: Data(), touched: now)
            }
            var upload = uploads[id]!
            guard upload.binding == binding, upload.sha == sha, upload.size == size else { return .error(409, "image_id_conflict") }
            if offset < upload.bytes.count {
                guard offset + bytes.count <= upload.bytes.count, Data(upload.bytes[offset..<offset + bytes.count]) == bytes else { return .error(409, "image_chunk_conflict") }
            } else {
                guard offset == upload.bytes.count else { return .error(409, "image_offset") }; upload.bytes.append(bytes)
            }
            upload.touched = now; uploads[id] = upload
            if upload.bytes.count < size { return .init(200, ["offset": upload.bytes.count]) }
            guard MacTab5Protocol.sha256(upload.bytes) == sha else { uploads.removeValue(forKey: id); return .error(400, "image_checksum") }
            let mime = try Self.imageType(upload.bytes)
            var index = try store.read("photo-index").map(MacTab5Protocol.object) ?? ["ids": [String]()]
            var ids = index["ids"] as? [String] ?? []
            if !ids.contains(id) { guard ids.count < 512 else { return .error(429, "image_storage_full") }; ids.append(id); index["ids"] = ids; try store.write("photo-index", MacTab5Protocol.json(index)) }
            try store.write(name(id), MacTab5Protocol.json(["binding": binding, "sha256": sha, "size": size, "mime": mime, "bytes": upload.bytes.base64EncodedString(), "createdAt": Date().timeIntervalSince1970]))
            uploads.removeValue(forKey: id)
            return .init(200, ["status": "uploaded", "imageId": id, "sha256": sha, "offset": size])
        } catch { uploads.removeValue(forKey: id); return .error(400, "image_or_storage_unavailable") }
    }
    func inputs(_ ids: [String], device: String, task: String) throws -> [[String: Any]] {
        lock.lock(); defer { lock.unlock() }
        guard ids.count <= 3, Set(ids).count == ids.count else { throw MacTab5Error.invalidPacket }
        return try ids.map { id in
            guard let normalized = MacTab5Codex.id(id), let root = try saved(normalized), root["binding"] as? String == device + "|" + task,
                  let created = root["createdAt"] as? Double, Date().timeIntervalSince1970 - created < 7 * 86400,
                  let base64 = root["bytes"] as? String, let bytes = Data(base64Encoded: base64), MacTab5Protocol.sha256(bytes) == root["sha256"] as? String else { throw MacTab5Error.invalidPacket }
            let mime = try Self.imageType(bytes)
            return ["type": "image", "url": "data:\(mime);base64,\(base64)"]
        }
    }
}
