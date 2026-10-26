import AppKit
import CryptoKit
import UniformTypeIdentifiers

final class MacGalleryProgress: @unchecked Sendable {
    private let lock = NSLock()
    private var stopped = false
    let update: (Double)->Void
    init(update:@escaping (Double)->Void = { _ in }) { self.update = update }
    func cancel() { lock.lock(); stopped = true; lock.unlock() }
    func check() throws { lock.lock(); let cancelled = stopped; lock.unlock(); if cancelled { throw CancellationError() } }
}

struct MacGalleryRelease {
    let category:String, version:String, asset:MacComponentRelease.Asset, checksum:MacComponentRelease.Asset
    static func number(_ text:String) -> [Int]? {
        guard text.range(of:"^\\d{4}\\.\\d{2}\\.\\d{2}(?:\\.\\d+)?$",options:.regularExpression) != nil else { return nil }
        let p = text.split(separator:".").compactMap { Int($0) }; guard (3...4).contains(p.count) else { return nil }
        let date = DateComponents(year:p[0],month:p[1],day:p[2]); let calendar = Calendar(identifier:.gregorian)
        guard let value = calendar.date(from:date), calendar.dateComponents([.year,.month,.day],from:value) == date else { return nil }
        return p.count == 3 ? p+[0]:p
    }
    static func parse(_ data:Data) throws -> ([MacGalleryRelease],Int) {
        guard let rows = try JSONSerialization.jsonObject(with:data) as? [[String:Any]] else { throw MacTab5Error.invalidPacket }
        var releases: [MacGalleryRelease] = []
        for row in rows where row["draft"] as? Bool == false && row["prerelease"] as? Bool == false {
            guard let tag = row["tag_name"] as? String, let assets = row["assets"] as? [[String:Any]] else { continue }
            let files: [MacComponentRelease.Asset] = assets.compactMap { value in
                guard let name = value["name"] as? String, name == (name as NSString).lastPathComponent,
                      let raw = value["browser_download_url"] as? String, let url = URL(string:raw), MacUpdateRedirects.allowed(url), url.host == "github.com", url.query == nil, url.fragment == nil,
                      url.path == "/yaoyouzhong/AI-bot/releases/download/\(tag)/\(name)", let size = MacTab5Protocol.integer(value["size"]), size > 0 else { return nil }
                return .init(name:name,url:url,size:size)
            }
            for file in files where file.size <= 2*1024*1024*1024 {
                for (category,prefix) in [("painting","AI-bot-gallery-painting-standard-"),("calligraphy","AI-bot-gallery-calligraphy-standard-"),("painting","AI-bot-DailyPainting-Curated-"),("calligraphy","AI-bot-DailyCalligraphy-Curated-")] {
                    guard file.name.hasPrefix(prefix), file.name.hasSuffix(".zip") else { continue }
                    let version = String(file.name.dropFirst(prefix.count).dropLast(4))
                    guard number(version) != nil, let checksum = files.first(where:{$0.name == file.name+".sha256"}) ?? files.first(where:{$0.name == "SHA256SUMS.txt"}) else { continue }
                    releases.append(.init(category:category,version:version,asset:file,checksum:checksum))
                }
            }
        }
        return (releases,rows.count)
    }
}

/// Downloads into a hash-specific cache; only a complete verified ZIP is installed.
final class MacGalleryDownloader {
    private let session:URLSession
    init() { let config = URLSessionConfiguration.ephemeral; config.timeoutIntervalForRequest = 30; config.timeoutIntervalForResource = 3600; session = URLSession(configuration:config,delegate:MacUpdateRedirects(),delegateQueue:nil) }
    deinit { session.invalidateAndCancel() }
    func metadata(_ url:URL, maximum:Int) async throws -> Data {
        guard MacUpdateRedirects.allowed(url) else { throw MacTab5Error.invalidPacket }
        var request = URLRequest(url:url); request.setValue("AI-bot-mac",forHTTPHeaderField:"User-Agent")
        let (bytes,response) = try await session.bytes(for:request)
        guard (response as? HTTPURLResponse)?.statusCode == 200, response.expectedContentLength <= maximum else { throw MacTab5Error.invalidPacket }
        var result = Data(); for try await byte in bytes { try Task.checkCancellation(); guard result.count < maximum else { throw MacTab5Error.invalidPacket }; result.append(byte) }; return result
    }
    func download(_ release:MacGalleryRelease, root:URL, progress:@escaping (Double)->Void) async throws -> URL {
        let hash = try MacReleaseCatalog.checksum(await metadata(release.checksum.url,maximum:256*1024),name:release.asset.name)
        let directory = root.appendingPathComponent("downloads").appendingPathComponent(hash)
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        let file = directory.appendingPathComponent(release.asset.name)
        if !FileManager.default.fileExists(atPath:file.path) { FileManager.default.createFile(atPath:file.path,contents:nil,attributes:[.posixPermissions:0o600]) }
        let handle = try FileHandle(forUpdating:file); defer { try? handle.close() }
        var received = Int64(try handle.seekToEnd())
        if received > release.asset.size { try handle.truncate(atOffset:0); try handle.seek(toOffset:0); received = 0 }
        if received < release.asset.size {
            var request = URLRequest(url:release.asset.url); request.setValue("AI-bot-mac",forHTTPHeaderField:"User-Agent"); request.setValue("identity",forHTTPHeaderField:"Accept-Encoding")
            if received > 0 { request.setValue("bytes=\(received)-",forHTTPHeaderField:"Range") }
            let (bytes,response) = try await session.bytes(for:request)
            guard let http = response as? HTTPURLResponse else { throw MacTab5Error.invalidPacket }
            if http.statusCode == 200 { try handle.truncate(atOffset:0); try handle.seek(toOffset:0); received = 0 }
            else if http.statusCode == 206 { guard http.value(forHTTPHeaderField:"Content-Range") == "bytes \(received)-\(release.asset.size-1)/\(release.asset.size)" else { throw MacTab5Error.invalidPacket } }
            else { throw MacTab5LocalError.invalid("图库下载失败（HTTP \(http.statusCode)）。") }
            var buffer = Data(); buffer.reserveCapacity(256*1024)
            for try await byte in bytes {
                try Task.checkCancellation(); guard received+Int64(buffer.count) < release.asset.size else { throw MacTab5Error.invalidPacket }; buffer.append(byte)
                if buffer.count == 256*1024 { try handle.write(contentsOf:buffer); received += Int64(buffer.count); buffer.removeAll(keepingCapacity:true); progress(Double(received)/Double(release.asset.size)) }
            }
            try handle.write(contentsOf:buffer); received += Int64(buffer.count)
        }
        guard received == release.asset.size else { throw MacTab5LocalError.invalid("下载未完成，可重试继续下载。") }
        try handle.synchronize(); try handle.seek(toOffset:0); var digest = SHA256()
        while let chunk = try handle.read(upToCount:1024*1024), !chunk.isEmpty { try Task.checkCancellation(); digest.update(data:chunk) }
        guard digest.finalize().map({String(format:"%02x",$0)}).joined() == hash else { try handle.truncate(atOffset:0); throw MacTab5LocalError.invalid("下载校验失败，原图库保持不变。") }
        return file
    }
}

final class MacGalleryManager: NSWindowController, NSWindowDelegate {
    private let gallery:MacTab5Gallery, browse:()->Void, downloader = MacGalleryDownloader()
    private let status = MacBridgeSettingsStyle.note("")
    private var current: [NSTextField] = [], latest: [NSTextField] = [], buttons: [NSButton] = [], controls: [NSButton] = []
    private let progress = NSProgressIndicator(), cancel = NSButton()
    private var operation:Task<Void,Never>?, installation:MacGalleryProgress?, releases:[String:MacGalleryRelease] = [:], checked = false
    private(set) var busy = false
    init(gallery:MacTab5Gallery,browse:@escaping ()->Void) {
        self.gallery = gallery; self.browse = browse
        super.init(window:MacBridgeSettingsStyle.window("屏保图库",width:680,height:600,minimumHeight:520)); window?.delegate = self
        let page = MacBridgeSettingsPage(); page.heading("屏保图库",subtitle:"选择喜欢的作品集，在电脑欣赏，也可用于 TAB5 屏保。")
        for (i,name) in ["每日名画","每日书法"].enumerated() {
            page.section(name); let installed = MacBridgeSettingsStyle.label(""), online = MacBridgeSettingsStyle.note(""); current.append(installed); latest.append(online)
            page.add(installed); let button = MacSettingsButton("下载并安装") { [weak self] in self?.download(i) }; buttons.append(button)
            page.add(MacBridgeSettingsStyle.row([online,NSView(),button]))
        }
        page.section("选择版本"); page.add(MacBridgeSettingsStyle.label("标准版：电脑使用适配后的展示图。\n高清版：电脑使用高清图片，可放大查看更多细节。")); page.add(MacBridgeSettingsStyle.note("两版均含作品和导读，TAB5 显示相同，任选一版即可。高清完整包可通过“安装图库包”导入。"))
        progress.isIndeterminate = false; progress.minValue = 0; progress.maxValue = 100; progress.isHidden = true; page.add(progress)
        cancel.title = "取消操作"; cancel.target = self; cancel.action = #selector(cancelGalleryOperation); cancel.isHidden = true; page.add(cancel)
        controls = [MacSettingsButton("浏览作品",browse),MacSettingsButton("安装图库包…") { [weak self] in self?.importPack() },MacSettingsButton("检查更新") { [weak self] in self?.check() }]
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("打开图库目录") { [weak self] in guard let self else { return }; do { try FileManager.default.createDirectory(at:self.gallery.directory,withIntermediateDirectories:true); NSWorkspace.shared.open(self.gallery.directory) } catch { self.status.stringValue = error.localizedDescription } },MacSettingsButton("下载与安装说明") { NSWorkspace.shared.open(URL(string:"https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md")!) }]))
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:controls); refresh()
    }
    required init?(coder:NSCoder) { fatalError() }
    deinit { operation?.cancel(); installation?.cancel() }
    func open() { refresh(); MacSettingsControls.open(self); if !checked { check() } }
    func windowShouldClose(_ sender:NSWindow) -> Bool { if busy { status.stringValue = "请先取消操作或等待安装结束。" }; return !busy }
    private let categories = ["painting","calligraphy"]
    private func installed(_ category:String) -> MacTab5GalleryManifest? { try? gallery.installed(category) }
    private func refresh() {
        for (i,category) in categories.enumerated() {
            let pack = installed(category), hd = FileManager.default.fileExists(atPath:gallery.directory.appendingPathComponent(category+"/desktop/desktop-index.relative.json").path)
            current[i].stringValue = pack.map { "\(hd ? "高清版":"标准版") \(hd ? gallery.hdEdition(category) ?? "已安装" : $0.version) · \($0.works) 件作品" } ?? "尚未安装"
            let release = releases[category]; latest[i].stringValue = release.map { "线上标准版：\($0.version) · \($0.asset.size/1048576) MiB" } ?? (checked ? "线上标准版：暂无正式发布":"线上标准版：尚未检查")
            let newer = release.map { r in pack.flatMap { MacGalleryRelease.number($0.version) }.map { $0.lexicographicallyPrecedes(MacGalleryRelease.number(r.version)!) } ?? true } ?? false
            buttons[i].isEnabled = !busy && !hd && newer; buttons[i].title = hd ? "已安装高清版" : pack == nil ? "下载并安装":newer ? "下载并升级":"已安装"
        }
        controls.forEach { $0.isEnabled = !busy }; cancel.isHidden = !busy; progress.isHidden = !busy; window?.contentView?.layoutSubtreeIfNeeded()
    }
    @objc private func cancelGalleryOperation() { operation?.cancel(); installation?.cancel(); status.stringValue = "正在取消，保留原图库…" }
    private func run(_ message:String,_ body:@escaping () async throws -> String) {
        guard !busy else { return }; busy = true; progress.doubleValue = 0; status.stringValue = message; refresh()
        operation = Task { [weak self] in guard let self else { return }; do { let text = try await body(); await MainActor.run { self.status.stringValue = text } }
            catch { await MainActor.run { self.status.stringValue = error is CancellationError ? "操作已取消，原图库保留；下载可继续。":"操作未完成，保留原图库："+error.localizedDescription } }
            await MainActor.run { self.busy = false; self.operation = nil; self.installation = nil; self.refresh() }
        }
    }
    private func check() { run("正在检查名画与书法的正式精选集…") { [self] in
        var found:[String:MacGalleryRelease] = [:], complete = false
        for page in 1...10 {
            let (rows,count) = try MacGalleryRelease.parse(await downloader.metadata(URL(string:"https://api.github.com/repos/yaoyouzhong/AI-bot/releases?per_page=100&page=\(page)")!,maximum:2*1024*1024))
            for r in rows { if found[r.category] == nil || MacGalleryRelease.number(found[r.category]!.version)!.lexicographicallyPrecedes(MacGalleryRelease.number(r.version)!) { found[r.category] = r } }
            if count < 100 { complete = true; break }
        }
        guard complete else { throw MacTab5LocalError.invalid("发布目录超过查询范围。") }
        await MainActor.run { self.releases = found; self.checked = true }; return "已检查正式发布的图库。"
    } }
    private func install(_ url:URL, expected:MacGalleryRelease? = nil) async throws -> String {
        let state = MacGalleryProgress { [weak self] value in DispatchQueue.main.async { self?.progress.doubleValue = 70+value*30 } }; await MainActor.run { self.installation = state; self.status.stringValue = "正在验证与安装，旧图库保留到新包校验完成…" }
        let result = try await withTaskCancellationHandler(operation:{ try await Task.detached { [gallery] in
            if let expected { let archive = try MacTab5Archive(url); let bytes = try archive.read("PACK.json",maximum:2*1024*1024); let pack = try JSONDecoder().decode(MacTab5GalleryManifest.self,from:bytes); guard pack.category == expected.category, pack.version == expected.version else { throw MacTab5Error.invalidPacket } }
            return try gallery.install(url,progress:state)
        }.value },onCancel:{ state.cancel() })
        return "\(result.category == "painting" ? "每日名画":"每日书法")已安装 · \(gallery.hdEdition(result.category).map { "高清版 " + $0 } ?? result.version) · \(result.works) 件作品。旧版保留为备份。"
    }
    private func importPack() { guard !busy else { return }; let panel = NSOpenPanel(); panel.allowedContentTypes = [.zip]; panel.allowsMultipleSelection = false; panel.title = "安装标准版或高清完整图库"; guard panel.runModal() == .OK, let url = panel.url else { return }; run("正在检查图库包…") { [self] in try await install(url) } }
    private func download(_ index:Int) { guard let release = releases[categories[index]] else { return }; run("正在下载并校验图库（支持断点续传）…") { [self] in
        let file = try await downloader.download(release,root:gallery.directory) { [weak self] value in DispatchQueue.main.async { self?.progress.doubleValue = value*70 } }
        try Task.checkCancellation(); let result = try await install(file,expected:release); try? FileManager.default.removeItem(at:file); return result
    } }
}
