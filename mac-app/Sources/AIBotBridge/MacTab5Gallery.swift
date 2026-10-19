import Foundation
import ImageIO

struct MacTab5GalleryManifest: Codable {
    let schemaVersion: Int
    let version: String
    let category: String
    let works: Int
    let frames: Int
    let files: [String: String]
}
struct MacTab5Artwork: Codable {
    let id: String
    let category: String
    let title: String
    let author: String
    let source: String
    let license: String
    let frames: [String]
    let workId: String?
    let portraitFrames: [String]
}

final class MacTab5Gallery {
    private let lock = NSLock()
    private struct Cache { let stamp: String; let bytes: Data; let manifest: MacTab5GalleryManifest; let works: [MacTab5Artwork]; let guides: [String: [String: Any]] }
    private var catalogs: [String: Cache] = [:]
    private var lastImage: (path: String, stamp: String, sha: String, bytes: Data)?
    let directory: URL
    init(directory: URL? = nil) {
        self.directory = directory ?? FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("AI-bot/Tab5/Galleries", isDirectory: true)
    }
    func install(_ url: URL) throws -> MacTab5GalleryManifest {
        let archive = try MacTab5Archive(url, maximumSize: 16 * 1024 * 1024 * 1024, allowNested: true, allowUnicode: true, maximumEntries: 20000)
        if !archive.names.contains("PACK.json") { return try installHD(archive) }
        return try installStandard(archive)
    }
    private func installStandard(_ archive: MacTab5Archive) throws -> MacTab5GalleryManifest {
        let bytes = try archive.read("PACK.json", maximum: 2 * 1024 * 1024)
        let manifest = try JSONDecoder().decode(MacTab5GalleryManifest.self, from: bytes)
        return try install(manifest: manifest, manifestBytes: bytes, names: archive.names) { try archive.read($0, maximum: $0.hasSuffix(".jpg") ? 1048576 : 16 * 1024 * 1024) }
    }
    func install(manifest: MacTab5GalleryManifest, manifestBytes: Data, names: [String], read: (String) throws -> Data) throws -> MacTab5GalleryManifest {
        guard [1, 2].contains(manifest.schemaVersion), ["painting", "calligraphy"].contains(manifest.category),
              manifest.version.range(of: "^\\d{4}\\.\\d{2}\\.\\d{2}(?:\\.\\d+)?$", options: .regularExpression) != nil,
              (1...2000).contains(manifest.works), (1...8000).contains(manifest.frames), names.allSatisfy(MacTab5Archive.safeName),
              Set(names.map { $0.lowercased() }).count == names.count,
              Set(names) == Set(manifest.files.keys).union(["PACK.json"]),
              manifest.files.values.allSatisfy({ MacTab5Protocol.isHex($0, count: 64) }),
              ["catalog.json", "provenance.json", "selection.json", "LICENSE", "THIRD_PARTY_NOTICES.md"].allSatisfy({ manifest.files[$0] != nil }),
              manifest.schemaVersion != 2 || manifest.files["guides.zh.json"] != nil else { throw MacTab5LocalError.invalid("图库清单与内容不一致。") }
        let fm = FileManager.default
        try fm.createDirectory(at: directory, withIntermediateDirectories: true)
        let staging = directory.appendingPathComponent(".import-" + MacTab5Protocol.identifier(), isDirectory: true)
        try fm.createDirectory(at: staging, withIntermediateDirectories: false)
        defer { if fm.fileExists(atPath: staging.path) { try? fm.removeItem(at: staging) } }
        var total = 0
        for name in manifest.files.keys.sorted() {
            let bytes = try read(name); total += bytes.count
            guard !bytes.isEmpty, total <= 2 * 1024 * 1024 * 1024,
                  bytes.count <= (name.hasSuffix(".jpg") ? 1048576 : 16 * 1024 * 1024),
                  MacTab5Protocol.sha256(bytes) == manifest.files[name] else { throw MacTab5LocalError.invalid("图库文件校验失败，原图库保持不变。") }
            if name.hasSuffix(".jpg") { try Self.validateJPEG(bytes) }
            try bytes.write(to: staging.appendingPathComponent(name), options: .withoutOverwriting)
        }
        let works = try JSONDecoder().decode([MacTab5Artwork].self, from: Data(contentsOf: staging.appendingPathComponent("catalog.json")))
        let images = works.flatMap { $0.frames + $0.portraitFrames }
        guard works.count == manifest.works, Set(works.map(\.id)).count == works.count,
              works.allSatisfy({ $0.category == manifest.category && !$0.id.isEmpty && !$0.title.isEmpty && !$0.author.isEmpty && (1...64).contains($0.frames.count) && $0.frames.count == $0.portraitFrames.count }),
              images.count == manifest.frames, Set(images).count == images.count,
              Set(images) == Set(manifest.files.keys.filter { $0.hasSuffix(".jpg") }) else { throw MacTab5LocalError.invalid("图库作品、分页或图片清单无效。") }
        if manifest.files["guides.zh.json"] != nil { _ = try MacTab5ArtGuide.read(Data(contentsOf: staging.appendingPathComponent("guides.zh.json")), works: works) }
        try manifestBytes.write(to: staging.appendingPathComponent("PACK.json"), options: .withoutOverwriting)
        try activate(staging, category: manifest.category)
        return manifest
    }
    private func activate(_ staging: URL, category: String) throws {
        lock.lock(); defer { lock.unlock() }
        let fm = FileManager.default, target = directory.appendingPathComponent(category, isDirectory: true)
        let backup = directory.appendingPathComponent(category + ".backup-" + MacTab5Protocol.identifier())
        let previous = fm.fileExists(atPath: target.path)
        if previous { try fm.moveItem(at: target, to: backup) }
        do { try fm.moveItem(at: staging, to: target) }
        catch { if previous { try? fm.moveItem(at: backup, to: target) }; throw error }
        // Previous installed artwork remains in a backup until separately removed.
        catalogs.removeValue(forKey: category); lastImage = nil
    }
    private func installHD(_ archive: MacTab5Archive) throws -> MacTab5GalleryManifest {
        let files = archive.names.filter { !$0.hasSuffix("/") }
        guard let first = files.first, first.contains("/") else { throw MacTab5LocalError.invalid("高清完整包缺少外层目录。") }
        let prefix = String(first.split(separator: "/")[0]) + "/"
        guard files.allSatisfy({ $0.hasPrefix(prefix) }), files.contains(prefix + "standard.zip"), files.contains(prefix + "desktop-index.relative.json") else { throw MacTab5LocalError.invalid("请选择包含 TAB5 展示图库的高清完整包。") }
        let listing = try archive.read(prefix + "SHA256SUMS.txt", maximum: 4 * 1024 * 1024)
        guard let text = String(data: listing, encoding: .utf8) else { throw MacTab5LocalError.invalid("高清校验清单不是 UTF-8。") }
        var hashes: [String: String] = [:]
        for raw in text.split(separator: "\n") {
            let line = String(raw).trimmingCharacters(in: .newlines), hash = String(line.prefix(64)).lowercased(), name = String(line.dropFirst(66))
            guard line.count >= 67, line.dropFirst(64).prefix(2) == "  ", MacTab5Protocol.isHex(hash, count: 64), MacTab5Archive.safeUnicodePath(name), hashes[name] == nil else { throw MacTab5LocalError.invalid("高清校验清单格式或文件名无效。") }
            hashes[name] = hash
        }
        guard Set(hashes.keys.map { prefix + $0 }) == Set(files).subtracting([prefix + "SHA256SUMS.txt"]), let expected = hashes["standard.zip"], let catalogHash = hashes["catalog.json"] else { throw MacTab5LocalError.invalid("高清 ZIP 文件集合与校验清单不一致。") }
        let catalogBytes = try archive.read(prefix + "catalog.json", maximum: 16 * 1024 * 1024)
        guard MacTab5Protocol.sha256(catalogBytes) == catalogHash else { throw MacTab5LocalError.invalid("高清目录 SHA-256 不一致。") }
        let catalog = try MacTab5Protocol.object(catalogBytes)
        guard MacTab5Protocol.integer(catalog["schema"]) == 1, let category = catalog["category"] as? String, ["painting", "calligraphy"].contains(category), let works = catalog["works"] as? [[String: Any]], (1...2000).contains(works.count) else { throw MacTab5LocalError.invalid("高清目录 schema、类别或作品数量无效。") }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let stage = directory.appendingPathComponent(".hd-import-" + MacTab5Protocol.identifier())
        try FileManager.default.createDirectory(at: stage, withIntermediateDirectories: false)
        defer { try? FileManager.default.removeItem(at: stage) }
        let zip = stage.appendingPathComponent("standard.zip")
        try archive.copy(prefix + "standard.zip", to: zip, maximum: 2 * 1024 * 1024 * 1024, sha256: expected)
        let prepared = MacTab5Gallery(directory: stage.appendingPathComponent("prepared")), manifest: MacTab5GalleryManifest
        do { manifest = try prepared.installStandard(MacTab5Archive(zip)) }
        catch { throw MacTab5LocalError.invalid("内置 TAB5 图库未通过校验：\(error.localizedDescription)") }
        guard manifest.category == category, manifest.works == works.count, manifest.schemaVersion == 2 else { throw MacTab5LocalError.invalid("高清包与内置 TAB5 图库不一致，原图库保持不变。") }
        try activate(prepared.directory.appendingPathComponent(category), category: category)
        // Only the complete, verified TAB5 subset is imported. Desktop originals
        // are never extracted or advertised as a Mac high-resolution browser.
        return manifest
    }
    static func validateJPEG(_ bytes: Data) throws {
        guard bytes.starts(with: [0xff, 0xd8]), let source = CGImageSourceCreateWithData(bytes as CFData, nil),
              CGImageSourceGetCount(source) == 1,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue == 1280,
              (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue == 720,
              CGImageSourceCreateImageAtIndex(source, 0, nil) != nil else { throw MacTab5LocalError.invalid("图库图片必须为 1280×720 JPEG。") }
    }
    static func dayIndex(_ date: Date, count: Int) -> Int {
        let calendar = Calendar(identifier: .gregorian)
        let base = calendar.date(from: DateComponents(year: 2026, month: 1, day: 1))!
        return (((calendar.dateComponents([.day], from: base, to: date).day ?? 0) % count) + count) % count
    }
    func revisions() -> [String: String] {
        lock.lock(); defer { lock.unlock() }
        var revisions: [String: String] = [:]
        for category in ["painting", "calligraphy"] {
            if let cache = try? load(category) { revisions[category] = MacTab5Protocol.sha256(cache.bytes) }
        }
        return revisions
    }
    private func stamp(_ url: URL, maximum: Int) throws -> String {
        let values = try url.resourceValues(forKeys: [.fileSizeKey, .contentModificationDateKey, .fileResourceIdentifierKey])
        guard let size = values.fileSize, size > 0, size <= maximum else { throw MacTab5Error.invalidPacket }
        return "\(size)|\(values.contentModificationDate?.timeIntervalSince1970 ?? 0)|\(String(describing: values.fileResourceIdentifier))"
    }
    private func load(_ category: String) throws -> Cache {
        let root = directory.appendingPathComponent(category), pack = root.appendingPathComponent("PACK.json"), catalog = root.appendingPathComponent("catalog.json")
        let bytes: Data, manifest: MacTab5GalleryManifest
        let packStamp = try stamp(pack, maximum: 2 * 1024 * 1024), catalogStamp = try stamp(catalog, maximum: 16 * 1024 * 1024)
        if let previous = catalogs[category], previous.stamp.hasPrefix(packStamp + "|") { bytes = previous.bytes; manifest = previous.manifest }
        else { bytes = try Data(contentsOf: pack); manifest = try JSONDecoder().decode(MacTab5GalleryManifest.self, from: bytes) }
        let guideURL = root.appendingPathComponent("guides.zh.json"), guideStamp = manifest.files["guides.zh.json"] == nil ? "" : try stamp(guideURL, maximum: 16 * 1024 * 1024)
        let identity = packStamp + "|" + catalogStamp + "|" + guideStamp
        if let previous = catalogs[category], previous.stamp == identity { return previous }
        let workBytes = try Data(contentsOf: catalog), works = try JSONDecoder().decode([MacTab5Artwork].self, from: workBytes)
        guard manifest.category == category, [1,2].contains(manifest.schemaVersion), MacTab5Protocol.sha256(workBytes) == manifest.files["catalog.json"],
              !works.isEmpty, works.count <= 2000, works.count == manifest.works,
              works.allSatisfy({ $0.category == category && (1...64).contains($0.frames.count) && $0.frames.count == $0.portraitFrames.count && ($0.frames + $0.portraitFrames).allSatisfy(MacTab5Archive.safeName) }) else { throw MacTab5Error.invalidPacket }
        var guides: [String: [String: Any]] = [:]
        if !guideStamp.isEmpty { let guideBytes = try Data(contentsOf: guideURL); guard MacTab5Protocol.sha256(guideBytes) == manifest.files["guides.zh.json"] else { throw MacTab5Error.invalidPacket }; guides = try MacTab5ArtGuide.read(guideBytes, works: works) }
        let cache = Cache(stamp: identity, bytes: bytes, manifest: manifest, works: works, guides: guides); catalogs[category] = cache; return cache
    }
    func handle(_ request: [String: Any], binary: Bool, bulk: Bool) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        do {
            guard let category = request["category"] as? String, ["painting", "calligraphy"].contains(category) else { return .error(400, "invalid_gallery_category") }
            let root = directory.appendingPathComponent(category)
            let cache = try load(category), manifestBytes = cache.bytes, manifest = cache.manifest, works = cache.works
            if request["op"] as? String == "catalog" { return .init(200, ["syncVersion": 1, "works": works.count, "revision": MacTab5Protocol.sha256(manifestBytes), "baseDate": "2026-01-01"]) }
            let formatter = DateFormatter(); formatter.locale = Locale(identifier: "en_US_POSIX"); formatter.dateFormat = "yyyy-MM-dd"; formatter.isLenient = false
            guard let text = request["date"] as? String, let date = formatter.date(from: text), formatter.string(from: date) == text else { return .error(400, "invalid_gallery_date") }
            let work = works[Self.dayIndex(date, count: works.count)]
            let guide = cache.guides[work.workId ?? work.source]
            if request["op"] as? String == "guidePage" {
                guard let guide, let text = guide["transcription"] as? String,
                      request["workId"] as? String == guide["workId"] as? String,
                      request["sha256"] as? String == MacTab5Protocol.sha256(Data(text.utf8)),
                      let page = MacTab5Protocol.integer(request["page"]) else { return .error(409, "gallery_guide_changed") }
                let pages = try MacTab5ArtGuide.pages(text)
                guard page >= 0, page < pages.count else { return .error(400, "invalid_gallery_guide_page") }
                return .init(200, ["workId": guide["workId"]!, "page": page, "pages": pages.count, "sha256": request["sha256"]!, "pageSha256": MacTab5Protocol.sha256(Data(pages[Int(page)].utf8)), "text": pages[Int(page)]])
            }
            let frame = Int(MacTab5Protocol.integer(request["frame"]) ?? 0)
            let orientation = request["orientation"] as? String ?? "landscape"
            guard ["landscape", "portrait", ""].contains(orientation), frame >= 0, frame < work.frames.count else { return .error(400, "invalid_gallery_frame") }
            let file = orientation == "portrait" ? work.portraitFrames[frame] : work.frames[frame]
            guard MacTab5Archive.safeName(file), let expected = manifest.files[file] else { return .error(503, "invalid_gallery_catalog") }
            let imageURL = root.appendingPathComponent(file), imageStamp = try stamp(imageURL, maximum: 1048576), bytes: Data
            if let previous = lastImage, previous.path == imageURL.path, previous.stamp == imageStamp, previous.sha == expected { bytes = previous.bytes }
            else { bytes = try Data(contentsOf: imageURL); guard MacTab5Protocol.sha256(bytes) == expected else { return .error(503, "gallery_pack_incomplete") }; lastImage = (imageURL.path, imageStamp, expected, bytes) }
            if request["op"] as? String == "manifest" {
                let version = Int(MacTab5Protocol.integer(request["guideVersion"]) ?? 1)
                var body: [String: Any] = ["id": work.id, "title": work.title, "author": work.author, "source": work.source, "license": work.license,
                                          "date": text, "size": bytes.count, "sha256": expected, "frames": work.frames.count, "width": 1280, "height": 720, "orientation": orientation,
                                          "guideVersion": version, "guideUnchanged": false]
                if request["includeGuide"] as? Bool == true, let guide { body["guide"] = try MacTab5ArtGuide.projection(guide, version: version); body["workId"] = guide["workId"] }
                return .init(200, body)
            }
            guard request["op"] as? String == "read", binary, request["sha256"] as? String == expected,
                  let offset = MacTab5Protocol.integer(request["offset"]), let count = MacTab5Protocol.integer(request["count"]),
                  offset >= 0, offset < bytes.count, count > 0, count <= (bulk ? MacTab5Binary.bulk : 8192) else { return .error(400, "invalid_gallery_range") }
            let end = min(Int(offset + count), bytes.count)
            return .init(200, ["offset": offset, "count": end - Int(offset)], bytes: Data(bytes[Int(offset)..<end]))
        } catch { return .error(503, "gallery_pack_unavailable") }
    }
}
