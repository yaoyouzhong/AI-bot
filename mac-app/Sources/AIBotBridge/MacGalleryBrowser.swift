import AppKit
import CryptoKit
import ImageIO

struct MacGalleryDesktopIndex: Codable {
    struct Source: Codable { var path: String, sha256: String; var width: Int, height: Int }
    struct Part: Codable { var source: String; var crop: [Double], dest: [Double]; var rotation: Int }
    struct Page: Codable { var category: String, id: String, workId: String, frame: String, previewSha256: String; var width: Int, height: Int; var parts: [Part] }
    var schema: Int, sources: [String: Source], pages: [Page]
    private static func bounds(_ v: [Double], _ width: Int, _ height: Int) -> Bool { v.count == 4 && v.allSatisfy(\.isFinite) && v[0] >= 0 && v[1] >= 0 && v[2] > 0 && v[3] > 0 && v[0] + v[2] <= Double(width) + 0.01 && v[1] + v[3] <= Double(height) + 0.01 }
    func validate(category: String, works: [MacTab5Artwork], hashes: [String:String], previewHashes: [String:String]) throws {
        guard schema == 1, (1...10000).contains(sources.count), pages.count == works.reduce(0, { $0 + $1.frames.count }), Set(pages.map { $0.id + "/" + $0.frame }).count == pages.count else { throw MacTab5LocalError.invalid("高清分页数量无效。") }
        for source in sources.values { guard MacTab5Archive.safeUnicodePath(source.path), hashes[source.path] == source.sha256, (1...200000).contains(source.width), (1...200000).contains(source.height) else { throw MacTab5LocalError.invalid("高清素材索引无效。") } }
        for page in pages {
            guard page.category == category, let work = works.first(where: { $0.id == page.id }), page.workId == (work.workId ?? work.source), work.frames.contains(page.frame), previewHashes[page.frame] == page.previewSha256, (1...200000).contains(page.width), (1...200000).contains(page.height), (1...12).contains(page.parts.count) else { throw MacTab5LocalError.invalid("高清页面与设备预览不匹配。") }
            for part in page.parts { guard let s = sources[part.source], [0,180].contains(part.rotation), Self.bounds(part.crop,s.width,s.height), Self.bounds(part.dest,page.width,page.height) else { throw MacTab5LocalError.invalid("高清裁切范围无效。") } }
        }
    }
    func image(root: URL, work: MacTab5Artwork, frame: Int, preview: Data) throws -> NSImage? {
        guard let page = pages.first(where: { $0.id == work.id && $0.category == work.category && $0.frame == work.frames[frame] && $0.workId == (work.workId ?? work.source) && $0.previewSha256 == MacTab5Protocol.sha256(preview) }) else { return nil }
        guard (1...200000).contains(page.width), (1...200000).contains(page.height), (1...12).contains(page.parts.count) else { return nil }
        let scale = min(1, min(8192 / Double(max(page.width,page.height)), sqrt(16000000 / (Double(page.width) * Double(page.height)))))
        let w = max(1,Int(Double(page.width) * scale)), h = max(1,Int(Double(page.height) * scale))
        guard let ctx = CGContext(data:nil,width:w,height:h,bitsPerComponent:8,bytesPerRow:0,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }
        ctx.setFillColor(NSColor(calibratedRed:0.91,green:0.88,blue:0.82,alpha:1).cgColor); ctx.fill(CGRect(x:0,y:0,width:w,height:h))
        for part in page.parts {
            guard let entry = sources[part.source], MacTab5Archive.safeUnicodePath(entry.path), Self.bounds(part.crop,entry.width,entry.height), Self.bounds(part.dest,page.width,page.height), [0,180].contains(part.rotation) else { return nil }
            let url = root.appendingPathComponent(entry.path)
            guard url.resolvingSymlinksInPath().path.hasPrefix(root.resolvingSymlinksInPath().path + "/"), let size = try url.resourceValues(forKeys:[.fileSizeKey]).fileSize, size > 0, size <= 2 * 1024 * 1024 * 1024 else { return nil }
            let file = try FileHandle(forReadingFrom:url); defer { try? file.close() }; var digest = SHA256()
            while let chunk = try file.read(upToCount:1024 * 1024), !chunk.isEmpty { digest.update(data:chunk) }
            guard digest.finalize().map({ String(format:"%02x",$0) }).joined() == entry.sha256,
                  let source = CGImageSourceCreateWithURL(url as CFURL, [kCGImageSourceShouldCache:false] as CFDictionary),
                  let properties = CGImageSourceCopyPropertiesAtIndex(source,0,nil) as? [CFString:Any],
                  (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue == entry.width, (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue == entry.height,
                  let image = CGImageSourceCreateThumbnailAtIndex(source,0,[kCGImageSourceCreateThumbnailFromImageAlways:true,kCGImageSourceThumbnailMaxPixelSize:8192,kCGImageSourceShouldCacheImmediately:true] as CFDictionary) else { return nil }
            let sx = Double(image.width) / Double(entry.width), sy = Double(image.height) / Double(entry.height)
            guard let crop = image.cropping(to:CGRect(x:part.crop[0]*sx,y:part.crop[1]*sy,width:part.crop[2]*sx,height:part.crop[3]*sy)) else { return nil }
            let dest = CGRect(x:part.dest[0]*scale,y:Double(h)-(part.dest[1]+part.dest[3])*scale,width:part.dest[2]*scale,height:part.dest[3]*scale)
            ctx.saveGState()
            if part.rotation == 180 { ctx.translateBy(x:dest.midX,y:dest.midY); ctx.rotate(by:.pi); ctx.translateBy(x:-dest.midX,y:-dest.midY) }
            ctx.interpolationQuality = .high; ctx.draw(crop,in:dest); ctx.restoreGState()
        }
        guard let image = ctx.makeImage() else { return nil }; return NSImage(cgImage:image,size:NSSize(width:w,height:h))
    }
}

final class MacGalleryBrowser: NSWindowController, NSTextFieldDelegate, NSTableViewDataSource, NSTableViewDelegate {
    private let gallery: MacTab5Gallery, list = NSTableView()
    private let category = MacSettingsControls.popup(["每日名画","每日书法"]), query = NSTextField(), date = NSDatePicker()
    let stage = MacGalleryStage(fullscreen:false)
    private let summary = MacBridgeSettingsStyle.note("")
    private var works: [MacTab5Artwork] = [], visible: [MacTab5Artwork] = [], frame = 0, generation = 0
    private var thumbnails: [String:NSImage] = [:]
    private let loader = OperationQueue()
    private var displayed: MacGalleryPage?, fullscreen: MacGalleryFullscreen?
    private let importPack: () -> Void
    init(gallery:MacTab5Gallery, importPack:@escaping ()->Void) {
        self.gallery = gallery; self.importPack = importPack; loader.maxConcurrentOperationCount = 1
        let window = MacGalleryWindow(contentRect:NSRect(x:0,y:0,width:1020,height:720),styleMask:[.titled,.closable,.resizable],backing:.buffered,defer:false)
        super.init(window:window); window.title = "AI-bot · 浏览作品"; window.contentMinSize = NSSize(width:960,height:580); window.isReleasedWhenClosed = false; window.center()
        let root = NSView(), filters = NSStackView(); filters.orientation = .vertical; filters.alignment = .leading; filters.spacing = 10
        category.target = self; category.action = #selector(reload)
        query.placeholderString = "作品名或作者"; query.delegate = self; query.setAccessibilityLabel("作品名或作者")
        date.datePickerElements = .yearMonthDay; date.dateValue = Date(); date.datePickerStyle = .textFieldAndStepper
        filters.addArrangedSubview(MacBridgeSettingsStyle.row([category,date,MacSettingsButton("按日期查找") { [weak self] in self?.locate() },MacSettingsButton("今日") { [weak self] in self?.date.dateValue = Date(); self?.locate() },query]))
        filters.addArrangedSubview(MacBridgeSettingsStyle.row([summary,NSView(),MacSettingsButton("刷新") { [weak self] in self?.reload() },MacSettingsButton("管理图库…",importPack)]))
        for row in filters.arrangedSubviews { row.widthAnchor.constraint(equalTo:filters.widthAnchor).isActive = true }
        query.widthAnchor.constraint(greaterThanOrEqualToConstant:140).isActive = true
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.autohidesScrollers = true; scroll.borderType = .bezelBorder
        let column = NSTableColumn(identifier:.init("work")); column.title = "作品"; column.width = 240; list.addTableColumn(column)
        list.headerView = nil; list.rowHeight = 66; list.delegate = self; list.dataSource = self; list.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle; list.usesAlternatingRowBackgroundColors = true; scroll.documentView = list
        for view in [filters,scroll,stage] { view.translatesAutoresizingMaskIntoConstraints = false; root.addSubview(view) }
        NSLayoutConstraint.activate([
            filters.topAnchor.constraint(equalTo:root.topAnchor,constant:20),filters.leadingAnchor.constraint(equalTo:root.leadingAnchor,constant:24),filters.trailingAnchor.constraint(equalTo:root.trailingAnchor,constant:-24),
            scroll.topAnchor.constraint(equalTo:filters.bottomAnchor,constant:16),scroll.leadingAnchor.constraint(equalTo:root.leadingAnchor,constant:24),scroll.bottomAnchor.constraint(equalTo:root.bottomAnchor,constant:-16),scroll.widthAnchor.constraint(equalToConstant:250),
            stage.leadingAnchor.constraint(equalTo:scroll.trailingAnchor,constant:4),stage.trailingAnchor.constraint(equalTo:root.trailingAnchor,constant:-8),stage.topAnchor.constraint(equalTo:scroll.topAnchor,constant:-16),stage.bottomAnchor.constraint(equalTo:root.bottomAnchor)
        ])
        window.contentView = root; window.keyHandler = { [weak self] in self?.stage.key($0) ?? false }
        stage.navigate = { [weak self] in self?.change($0) }; stage.toggleFullscreen = { [weak self] in self?.showFullscreen() }
        reload()
    }
    required init?(coder:NSCoder) { fatalError() }
    deinit { loader.cancelAllOperations() }
    func open() { reload(); MacSettingsControls.open(self) }
    @objc private func reload() {
        let selected = displayed?.work.id
        do { works = try gallery.browserWorks(category.indexOfSelectedItem == 0 ? "painting":"calligraphy"); filter(preferred:selected ?? (works.isEmpty ? nil:works[MacTab5Gallery.dayIndex(date.dateValue,count:works.count)].id)) }
        catch { works = []; visible = []; generation += 1; loader.cancelAllOperations(); list.reloadData(); displayed = nil; stage.update(nil); fullscreen?.stage.update(nil); summary.stringValue = "本机暂无可用图库，请先安装标准版或高清完整包。" }
    }
    func controlTextDidChange(_ obj:Notification) { filter() }
    private func filter(preferred:String? = nil) {
        generation += 1; loader.cancelAllOperations()
        let q = query.stringValue.trimmingCharacters(in:.whitespacesAndNewlines)
        visible = works.filter { q.isEmpty || ($0.title+" "+$0.author).localizedCaseInsensitiveContains(q) }
        list.reloadData(); summary.stringValue = "\(category.titleOfSelectedItem ?? "") · \(visible.count) / \(works.count) 件作品"
        if visible.isEmpty { displayed = nil; stage.update(nil); fullscreen?.stage.update(nil); stage.message.stringValue = "没有匹配的作品，请更换搜索词。"; return }
        let selected = preferred.flatMap { id in visible.firstIndex { $0.id == id } } ?? 0
        list.selectRowIndexes(IndexSet(integer:selected),byExtendingSelection:false); list.scrollRowToVisible(selected); frame = 0; load()
    }
    private func locate() { guard !works.isEmpty else { return }; let work = works[MacTab5Gallery.dayIndex(date.dateValue,count:works.count)]; query.stringValue = ""; filter(preferred:work.id); list.scrollRowToVisible(list.selectedRow) }
    func numberOfRows(in tableView:NSTableView) -> Int { visible.count }
    func tableView(_ tableView:NSTableView, viewFor tableColumn:NSTableColumn?, row:Int) -> NSView? {
        guard visible.indices.contains(row) else { return nil }; let work = visible[row]
        let thumb = NSImageView(); thumb.imageScaling = .scaleProportionallyUpOrDown; thumb.image = thumbnails[work.category+"/"+work.id]
        thumb.widthAnchor.constraint(equalToConstant:72).isActive = true; thumb.heightAnchor.constraint(equalToConstant:46).isActive = true
        let title = NSTextField(labelWithString:work.title), author = NSTextField(labelWithString:work.author)
        title.lineBreakMode = .byTruncatingTail; author.lineBreakMode = .byTruncatingTail; author.font = .systemFont(ofSize:11); author.textColor = .secondaryLabelColor
        let labels = NSStackView(views:[title,author]); labels.orientation = .vertical; labels.alignment = .leading; labels.spacing = 5
        for label in [title,author] { label.widthAnchor.constraint(equalTo:labels.widthAnchor).isActive = true }
        let rowView = MacBridgeSettingsStyle.row([thumb,labels]); rowView.toolTip = work.title+" · "+work.author
        if thumb.image == nil { let g = generation; loader.addOperation { [weak self] in
            guard let self, let bytes = try? self.gallery.browserPage(work,frame:0).0, let image = NSImage(data:bytes) else { return }
            let thumbnail = NSImage(size:NSSize(width:144,height:81)); thumbnail.lockFocus(); image.draw(in:NSRect(x:0,y:0,width:144,height:81)); thumbnail.unlockFocus()
            DispatchQueue.main.async { [weak self, weak thumb] in guard let self, self.generation == g else { return }; if self.thumbnails.count > 100 { self.thumbnails.removeAll() }; self.thumbnails[work.category+"/"+work.id] = thumbnail; thumb?.image = thumbnail }
        } }
        return rowView
    }
    func tableViewSelectionDidChange(_ notification:Notification) { frame = 0; load() }
    private func change(_ delta:Int) { guard visible.indices.contains(list.selectedRow) else { return }; let count = visible[list.selectedRow].frames.count; guard count > 1 else { return }; frame = (frame+delta % count+count)%count; load() }
    private func load() {
        guard visible.indices.contains(list.selectedRow) else { return }; let work = visible[list.selectedRow], requested = frame
        generation += 1; let g = generation; loader.cancelAllOperations()
        if displayed?.work.id != work.id || displayed?.work.category != work.category { displayed = nil; stage.update(nil); fullscreen?.stage.update(nil) }
        stage.message.stringValue = "正在读取作品…"; fullscreen?.stage.message.stringValue = "正在读取作品…"
        loader.addOperation { [weak self] in guard let self else { return }; do {
            let (bytes,guide) = try self.gallery.browserPage(work,frame:requested)
            guard var image = NSImage(data:bytes) else { throw MacTab5Error.invalidPacket }; var hd = false
            let root = self.gallery.directory.appendingPathComponent(work.category).appendingPathComponent("desktop"), indexURL = root.appendingPathComponent("desktop-index.relative.json")
            if let size = try? indexURL.resourceValues(forKeys:[.fileSizeKey]).fileSize, size <= 16*1024*1024,
               let data = try? Data(contentsOf:indexURL), let index = try? JSONDecoder().decode(MacGalleryDesktopIndex.self,from:data), let original = try? index.image(root:root,work:work,frame:requested,preview:bytes) { image = original; hd = true }
            let page = MacGalleryPage(work:work,frame:requested,image:image,hd:hd,guide:guide)
            DispatchQueue.main.async { guard self.generation == g else { return }; self.displayed = page; self.stage.update(page); self.fullscreen?.stage.update(page) }
        } catch { DispatchQueue.main.async { guard self.generation == g else { return }; self.frame = self.displayed?.frame ?? 0; self.stage.message.stringValue = "读取失败，保留上次成功画面。请刷新或重新安装图库。"; self.fullscreen?.stage.message.stringValue = self.stage.message.stringValue } } }
    }
    private func showFullscreen() {
        guard let displayed else { return }
        if let fullscreen { fullscreen.window?.makeKeyAndOrderFront(nil); return }
        let viewer = MacGalleryFullscreen(page:displayed,screen:window?.screen,navigate:{ [weak self] in self?.change($0) })
        viewer.didClose = { [weak self] in self?.fullscreen = nil; self?.window?.makeKeyAndOrderFront(nil) }
        fullscreen = viewer; viewer.open()
    }
}
