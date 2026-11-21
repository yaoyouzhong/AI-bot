import AppKit
import CryptoKit
import UserNotifications

struct MacComponentRelease {
    struct Asset { let name: String, url: URL, size: Int64 }
    let component: String, version: String, notes: String, tag: String
    let asset: Asset, checksum: Asset?
}
enum MacReleaseCatalog {
    static func prefers(_ candidate: MacComponentRelease, over previous: MacComponentRelease) -> Bool {
        guard let next = version(candidate.version), let old = version(previous.version) else { return false }
        if next != old { return old.lexicographicallyPrecedes(next) }
        guard candidate.component == "esp8266" else { return false }
        func priority(_ asset: MacComponentRelease.Asset) -> Int {
            asset.name.hasSuffix("-flash.zip") ? 2 : asset.name.hasSuffix("-firmware.zip") ? 1 : 0
        }
        return priority(candidate.asset) > priority(previous.asset)
    }
    static func version(_ value: String) -> [Int]? {
        guard value.range(of:"^[0-9]+\\.[0-9]+\\.[0-9]+(?:-ui)?$",options:.regularExpression) != nil else { return nil }
        let parts = (value.hasSuffix("-ui") ? String(value.dropLast(3)):value).split(separator:".").compactMap { Int($0) }
        return parts.count == 3 && parts.allSatisfy { $0 >= 0 } ? parts:nil
    }
    static func parse(_ data: Data) throws -> ([MacComponentRelease], Int) {
        guard let rows = try JSONSerialization.jsonObject(with:data) as? [[String:Any]] else { throw MacTab5LocalError.invalid("发布目录无效。") }
        var releases: [MacComponentRelease] = []
        for row in rows where row["draft"] as? Bool == false && row["prerelease"] as? Bool == false {
            guard let tag = row["tag_name"] as? String, let assets = row["assets"] as? [[String:Any]] else { continue }
            func asset(_ value: [String:Any]) -> MacComponentRelease.Asset? {
                guard let name = value["name"] as? String, name == (name as NSString).lastPathComponent, let raw = value["browser_download_url"] as? String, let url = URL(string:raw), url.scheme == "https", url.host == "github.com", url.port == nil || url.port == 443, url.user == nil, url.password == nil, url.query == nil, url.fragment == nil, url.path == "/yaoyouzhong/AI-bot/releases/download/\(tag)/\(name)", let size = MacTab5Protocol.integer(value["size"]), size > 0 else { return nil }
                return .init(name:name,url:url,size:size)
            }
            let files = assets.compactMap(asset)
            for (component,prefix,suffix,tagPrefix) in [("bridge","AI-bot-bridge-v","-macos-arm64.zip","bridge-v"),("bridge","AIBotBridge-","-local-candidate-macos-arm64.zip","bridge-v"),("esp8266","AI-bot-esp8266-","-flash.zip","esp8266-v"),("esp8266","AI-bot-esp8266-","-firmware.zip","esp8266-v"),("esp8266","AI-bot-","-firmware-materials.zip","esp8266-v"),("tab5","AI-bot-tab5-","-upgrade.zip","tab5-v"),("tab5","TAB5-upgrade-",".zip","tab5-v")] {
                for file in files where file.name.hasPrefix(prefix) && file.name.hasSuffix(suffix) && file.name.count > prefix.count + suffix.count {
                    let text = String(file.name.dropFirst(prefix.count).dropLast(suffix.count))
                    guard version(text) != nil, tag == tagPrefix + text || tag.hasPrefix("v") && version(String(tag.dropFirst())) != nil else { continue }
                    let sidecar = files.first { $0.name == file.name + ".sha256" }
                    let compact = suffix == "-flash.zip"
                    if compact {
                        guard !text.hasSuffix("-ui"), file.size <= 256 * 1024 * 1024,
                              let sidecar, sidecar.size <= 256 * 1024 else { continue }
                    }
                    let checksum = sidecar ?? files.first { $0.name == "SHA256SUMS.txt" }
                    releases.append(.init(component:component,version:text,notes:row["body"] as? String ?? "暂无更新说明。",tag:tag,asset:file,checksum:checksum))
                }
            }
        }
        return (releases,rows.count)
    }
    static func checksum(_ bytes: Data, name: String) throws -> String {
        guard bytes.count <= 256 * 1024, let text = String(data:bytes,encoding:.utf8) else { throw MacTab5LocalError.invalid("校验清单无效。") }
        let matches = text.split(separator:"\n").compactMap { raw -> String? in
            let line = raw.trimmingCharacters(in:.whitespacesAndNewlines), hash = String(line.prefix(64)).lowercased()
            guard line.count > 65, MacTab5Protocol.isHex(hash,count:64) else { return nil }
            let filename = line.dropFirst(64).trimmingCharacters(in:.whitespaces).trimmingCharacters(in:CharacterSet(charactersIn:"*"))
            return filename == name ? hash:nil
        }
        guard matches.count == 1 else { throw MacTab5LocalError.invalid("校验清单没有唯一匹配文件。") }; return matches[0]
    }
}
final class MacUpdateRedirects: NSObject, URLSessionTaskDelegate {
    static func allowed(_ url: URL) -> Bool { url.scheme == "https" && (url.port == nil || url.port == 443) && url.user == nil && url.password == nil && ["api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com"].contains(url.host ?? "") }
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse, newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) { completionHandler(request.url.map(Self.allowed) == true ? request:nil) }
}

struct MacUpdateCheckCadence {
    enum Trigger { case manual, foreground, background }

    private var lastAttempt: (time: Date, succeeded: Bool)?

    func isDue(_ trigger: Trigger, at now: Date) -> Bool {
        guard trigger != .manual, let lastAttempt else { return true }
        let delay: TimeInterval
        if trigger == .foreground {
            delay = lastAttempt.succeeded ? 5 * 60 : 60
        } else {
            delay = lastAttempt.succeeded ? 60 * 60 : 15 * 60
        }
        return now.timeIntervalSince(lastAttempt.time) >= delay
    }

    mutating func record(at time: Date, succeeded: Bool) {
        lastAttempt = (time, succeeded)
    }
}

private final class MacUpdateTableHeader: NSTableHeaderView {
    override func draw(_ dirtyRect: NSRect) {
        NSColor.windowBackgroundColor.setFill()
        bounds.fill()
        guard let tableView else { return }
        let font = NSFont.systemFont(ofSize: 12, weight: .semibold)
        let style = NSMutableParagraphStyle(); style.lineBreakMode = .byTruncatingTail
        for (index, column) in tableView.tableColumns.enumerated() {
            let cell = headerRect(ofColumn: index)
            (column.title as NSString).draw(in: NSRect(x: cell.minX + 8, y: cell.midY - 8,
                width: max(0, cell.width - 16), height: 18), withAttributes: [
                    .font: font, .foregroundColor: NSColor.secondaryLabelColor, .paragraphStyle: style
                ])
        }
        NSColor.separatorColor.setFill()
        NSRect(x: 0, y: bounds.maxY - 1, width: bounds.width, height: 1).fill()
    }
}
final class MacUpdateWindow: NSWindowController, NSWindowDelegate {
    struct ESPConnection: Equatable {
        var port: String? = nil
        var id: String? = nil
        var firmware: String? = nil
    }
    private let tab5: MacTab5Service?, registry: MacDeviceRegistry
    private let espConnection: () -> ESPConnection
    private let upgradeTools: () throws -> Void
    private let espTools: (URL, String, String?) throws -> Void
    private let installDesktop: (URL, String) throws -> Void
    static let galleryID = "gallery"
    private let table = MacSettingsTable(["更新项","当前版本 / 安装","最新发布","状态"], minimumHeight: 162), detail = MacUpdateNotesView(), status = MacBridgeSettingsStyle.note("")
    private var releases: [String:MacComponentRelease] = [:], operation: Task<Void,Never>?
    private let components = ["bridge","esp8266","tab5", MacUpdateWindow.galleryID]
    private let componentNames = ["bridge":"Mac 桥接","esp8266":"ESP8266 小屏","tab5":"M5Stack TAB5"]
    private var displayedComponents: [String] = []
    private var checked = false, focusAvailableAfterCheck = false
    private var galleryReleases: [String: MacGalleryRelease] = [:]
    private var galleryManager: MacGalleryManager?, galleryBrowser: MacGalleryBrowser?
    var galleryBusy: Bool { galleryManager?.busy == true }

    func showActiveGalleryOperation() -> Bool {
        guard galleryBusy else { return false }
        galleryManager?.open()
        return true
    }

    private func isEnabled(_ key: String) -> Bool { key == "bridge" || registry.enabled(key) }
    private var availableComponents: [String] {
        components.filter { key in
            guard let release = releases[key], release.checksum != nil,
                  let installed = MacReleaseCatalog.version(current(key)),
                  let available = MacReleaseCatalog.version(release.version) else { return false }
            return installed.lexicographicallyPrecedes(available) && isEnabled(key)
        }
    }

    var availableUpdateCount: Int { availableComponents.count + (galleryReleases.isEmpty || galleryOverview().updates.isEmpty ? 0 : 1) }

    private func galleryOverview() -> (installed: Int, updates: [String]) {
        guard let gallery = tab5?.gallery else { return (0, []) }
        var installedCount = 0
        var updates: [String] = []
        for (category, name) in [("painting","每日名画"),("calligraphy","每日书法")] {
            guard let installed = try? gallery.installed(category) else { continue }
            installedCount += 1
            guard !gallery.fullCollection, installed.collection != "full", let release = galleryReleases[category],
                  let current = MacGalleryRelease.number(installed.version),
                  let latest = MacGalleryRelease.number(release.version), current.lexicographicallyPrecedes(latest) else { continue }
            let desktop = gallery.directory.appendingPathComponent(category + "/desktop/desktop-index.relative.json")
            if gallery.useHighQuality && FileManager.default.fileExists(atPath: desktop.path) { continue }
            updates.append("\(name)：\(installed.version) → \(release.version)")
        }
        return (installedCount, updates)
    }

    private func openGalleryManager() {
        guard let gallery = tab5?.gallery else { return }
        if galleryManager == nil {
            galleryManager = MacGalleryManager(gallery: gallery, browse: { [weak self] in
                guard let self else { return }
                if self.galleryBrowser == nil { self.galleryBrowser = MacGalleryBrowser(gallery: gallery) }
                self.galleryBrowser?.open()
            }, catalogChanged: { [weak self] releases in
                self?.galleryReleases = releases
                self?.refreshRows()
            })
        }
        galleryManager?.open(releases: checked ? galleryReleases : nil)
    }

    private var selectedComponent: String? {
        displayedComponents.indices.contains(table.selectedRow) ? displayedComponents[table.selectedRow] : nil
    }
    private let session: URLSession
    private let catalogPage: ((Int) async throws -> Data)?
    private let now: () -> Date
    private var checkCadence = MacUpdateCheckCadence()
    private(set) var busy = false
    private var canCancel = false
    static let automaticKey = "bridge_automatic_updates"
    private let automatic = NSButton(checkboxWithTitle:"自动检查并提醒",target:nil,action:nil), progress = NSProgressIndicator()
    private let downloadPackage: (MacGalleryRelease, @escaping (Double) -> Void) async throws -> URL
    private var automaticTimer: Timer?, rowTimer: Timer?
    private var downloadButton: MacSettingsButton!, cancelButton: MacSettingsButton!, checkButton: MacSettingsButton!
    private let requirement = MacBridgeSettingsStyle.note("")
    init(tab5: MacTab5Service?, registry: MacDeviceRegistry = .shared,
         espConnection: @escaping () -> ESPConnection = { .init() },
         upgradeTools: @escaping () throws -> Void, espTools: @escaping (URL, String, String?) throws -> Void,
         installDesktop: @escaping (URL, String) throws -> Void,
         now: @escaping () -> Date = Date.init,
         catalogPage: ((Int) async throws -> Data)? = nil,
         downloadPackage: ((MacGalleryRelease, @escaping (Double) -> Void) async throws -> URL)? = nil) {
        self.tab5 = tab5; self.registry = registry; self.espConnection = espConnection
        self.upgradeTools = upgradeTools; self.espTools = espTools; self.installDesktop = installDesktop; self.catalogPage = catalogPage
        self.now = now
        if let downloadPackage { self.downloadPackage = downloadPackage }
        else {
            let downloader = MacGalleryDownloader()
            self.downloadPackage = { release, progress in
                try await downloader.download(release, root: MacProfileFile<[MacBirthday]>.directory.appendingPathComponent("updates"), progress: progress)
            }
        }
        let configuration = URLSessionConfiguration.ephemeral; configuration.timeoutIntervalForRequest = 30; configuration.timeoutIntervalForResource = 600
        session = URLSession(configuration:configuration,delegate:MacUpdateRedirects(),delegateQueue:nil)
        super.init(window:MacBridgeSettingsStyle.window("软件固件",width:800,height:640,minimumHeight:570)); window?.delegate = self
        let page = MacBridgeSettingsPage(); page.stack.spacing = 10; page.heading("软件固件")
        automatic.state = UserDefaults.standard.bool(forKey:Self.automaticKey) ? .on:.off; automatic.target = self; automatic.action = #selector(changeAutomatic)
        checkButton = MacSettingsButton("检查更新") { [weak self] in self?.check(.manual) }
        page.add(MacBridgeSettingsStyle.row([checkButton,automatic])); page.add(table)
        table.table.rowHeight = 32
        table.table.headerView = MacUpdateTableHeader(frame: NSRect(x: 0, y: 0, width: 0, height: 26))
        table.heightAnchor.constraint(equalToConstant:162).isActive = true
        progress.isIndeterminate = false; progress.maxValue = 100; progress.isHidden = true; page.add(progress)
        page.section("所选更新说明"); page.add(detail); page.add(requirement)
        downloadButton = MacSettingsButton("一键升级") { [weak self] in self?.download() }
        downloadButton.bezelColor = .controlAccentColor
        cancelButton = MacSettingsButton("取消下载") { [weak self] in self?.operation?.cancel() }
        let downloads = MacSettingsButton("完整下载与安装说明") { NSWorkspace.shared.open(URL(string:"https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md")!) }
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[downloadButton,cancelButton,downloads])
        table.selected = { [weak self] _ in self?.selection() }; refreshRows()
        rowTimer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            guard let self, self.window?.isVisible == true, !self.busy else { return }
            self.refreshRows()
        }
    }
    deinit { automaticTimer?.invalidate(); rowTimer?.invalidate(); operation?.cancel(); session.invalidateAndCancel() }
    required init?(coder:NSCoder) { fatalError() }
    func open(component: String? = nil) {
        let requested = component.flatMap { components.contains($0) ? $0 : nil }
        focusAvailableAfterCheck = requested == nil && !checked
        refreshRows(select: requested ?? (busy ? selectedComponent : firstAvailableItem))
        MacSettingsControls.open(self)
        check(.foreground)
    }
    func startAutomaticChecks() {
        guard automaticTimer == nil else { return }
        automaticTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            if UserDefaults.standard.bool(forKey: Self.automaticKey) { self?.check(.background) }
        }
    }
    @objc private func changeAutomatic() {
        UserDefaults.standard.set(automatic.state == .on,forKey:Self.automaticKey)
        if automatic.state == .on { UNUserNotificationCenter.current().requestAuthorization(options:[.alert]) { _,_ in } }
    }
    private func controls() {
        let key = selectedComponent ?? ""
        let gallery = key == Self.galleryID
        let esp = espConnection()
        let legacy = key == "esp8266" && esp.port != nil && esp.firmware == nil
        let release = releases[key]
        let available = availableComponents.contains(key)
        downloadButton?.title = gallery ? "管理图库…" : legacy ? "手动准备小屏升级" : "一键升级"
        downloadButton?.isHidden = !gallery && !available && !legacy
        downloadButton?.isEnabled = !busy && (gallery ? tab5 != nil :
            release?.checksum != nil && isEnabled(key) && (key != "esp8266" || esp.port != nil) &&
            (available || legacy) && (key != "tab5" || tab5?.bootTransport != nil || tab5?.usbActive == true))
        cancelButton?.isHidden = !canCancel
        cancelButton?.title = progress.isHidden ? "取消检查" : "取消下载"
        automatic.isEnabled = !busy; checkButton?.isEnabled = !busy
        requirement.stringValue = gallery ? "" : legacy ? "无法比较旧固件版本。请在刷机窗口确认目标后开始。" :
            !available ? "" : key == "bridge" ? "自动校验、安装并重新打开 AI-bot。请先完成正在进行的操作。" :
            key == "tab5" ? "校验后向 TAB5 发送升级请求。请连接 USB 或 Wi-Fi，并保持供电。" :
            "通过 USB 升级，写入前自动备份。请保持连接和供电。"
        requirement.isHidden = requirement.stringValue.isEmpty
    }
    func windowShouldClose(_ sender:NSWindow) -> Bool { if busy { status.stringValue = canCancel ? "操作进行中，请先取消或等待完成。" : "正在准备安装，请等待完成。" }; return !busy }
    private func current(_ key: String) -> String {
        switch key {
        case "bridge": return Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "开发版"
        case "tab5": return tab5?.connectionStatus.firmware ?? "待连接"
        default:
            let esp = espConnection()
            if let version = esp.firmware { return MacReleaseCatalog.version(version) == nil ? "版本上报无效" : version }
            return esp.port != nil ? "旧固件未上报" : "连接设备后查看"
        }
    }
    func windowDidBecomeKey(_ notification: Notification) { if !busy { refreshRows() } }

    private var firstAvailableItem: String? {
        availableComponents.first ?? (galleryReleases.isEmpty || galleryOverview().updates.isEmpty ? nil : Self.galleryID)
    }

    private func refreshRows(select preferred: String? = nil) {
        let selected = preferred ?? selectedComponent
        let gallery = galleryOverview()
        let available = availableComponents + (gallery.updates.isEmpty ? [] : [Self.galleryID])
        let ordered = available + components.filter { !available.contains($0) }
        let rows = ordered.map { key in
            if key == Self.galleryID {
                return ["屏保图库", gallery.installed > 0 ? "已安装 \(gallery.installed) 套" : "未安装",
                        gallery.updates.isEmpty ? (checked ? "—" : "待查询") : "\(gallery.updates.count) 套新版",
                        gallery.updates.isEmpty ? (checked ? "可管理" : "待检查") : "有更新"]
            }
            return [componentNames[key] ?? key, current(key), releases[key]?.version ?? "待查询",
                    available.contains(key) ? "有更新" : releases[key] == nil ? (checked ? "暂无发布" : "待检查") : "—"]
        }
        let index = selected.flatMap { ordered.firstIndex(of: $0) } ?? 0
        if table.rows != rows || table.selectedRow != index {
            displayedComponents = ordered
            table.rows = rows
            table.select(index)
            table.table.scrollRowToVisible(index)
        }
        selection()
    }

    private func selection() {
        guard let key = selectedComponent else { return }
        if key == Self.galleryID {
            let updates = galleryOverview().updates
            detail.show(updates.isEmpty ? "查看名画和书法版本，或安装图库包。" : updates.joined(separator: "\n"))
        } else {
            if let release = releases[key] {
                detail.show(MacUpdateNotes.selected(release.notes, component: key, version: release.version))
            } else { detail.show(checked ? "暂无更新说明。" : "点击检查更新，读取正式发布的更新说明。") }
        }
        controls()
    }
    private func bytes(_ url: URL, maximum: Int, expected: Int64? = nil) async throws -> Data {
        guard MacUpdateRedirects.allowed(url) else { throw MacTab5LocalError.invalid("下载地址不受信任。") }
        var request = URLRequest(url:url); request.setValue("AI-bot-mac",forHTTPHeaderField:"User-Agent")
        let (stream,response) = try await session.bytes(for:request)
        guard let http = response as? HTTPURLResponse, http.statusCode == 200, response.expectedContentLength <= maximum else { throw MacTab5LocalError.invalid("下载响应无效或文件过大。") }
        var data = Data(); if let expected, expected <= maximum { data.reserveCapacity(Int(expected)) }
        for try await byte in stream { try Task.checkCancellation(); guard data.count < maximum else { throw MacTab5LocalError.invalid("下载文件超过限制。") }; data.append(byte) }
        guard expected == nil || Int64(data.count) == expected else { throw MacTab5LocalError.invalid("下载大小与发布记录不一致。") }; return data
    }
    private func check(_ trigger: MacUpdateCheckCadence.Trigger) {
        guard !busy, checkCadence.isDue(trigger, at: now()) else { return }
        busy = true; canCancel = true; controls(); status.stringValue = "正在读取正式发布记录…"
        operation = Task { @MainActor [weak self] in
            guard let self else { return }
            defer {
                busy = false
                canCancel = false
                controls()
            }
            do {
                var result: [String:MacComponentRelease] = [:], complete = false
                var galleryResult: [String:MacGalleryRelease] = [:]
                for page in 1...10 {
                    let data: Data
                    if let catalogPage { data = try await catalogPage(page) }
                    else { data = try await bytes(URL(string:"https://api.github.com/repos/yaoyouzhong/AI-bot/releases?per_page=100&page=\(page)")!,maximum:2*1024*1024) }
                    try Task.checkCancellation()
                    let (releases,count) = try MacReleaseCatalog.parse(data)
                    MacGalleryRelease.merge(try MacGalleryRelease.parse(data).0, into: &galleryResult)
                    for release in releases {
                        if let previous = result[release.component], !MacReleaseCatalog.prefers(release, over: previous) { continue }
                        result[release.component] = release
                    }
                    if count < 100 { complete = true; break }
                }
                guard complete else { throw MacTab5LocalError.invalid("发布记录超过查询范围。") }
                checkCadence.record(at: now(), succeeded: true)
                releases = result
                galleryReleases = galleryResult
                checked = true
                refreshRows(select: focusAvailableAfterCheck ? firstAvailableItem : nil)
                focusAvailableAfterCheck = false
                status.stringValue = "已检查更新。"
                if catalogPage == nil, UserDefaults.standard.bool(forKey: Self.automaticKey), Bundle.main.bundleIdentifier != nil {
                    for component in availableComponents {
                        guard let release = result[component] else { continue }
                        let key = "update_notified_" + component
                        guard UserDefaults.standard.string(forKey: key) != release.version else { continue }
                        let notice = UNMutableNotificationContent()
                        notice.title = "AI-bot 有可用更新"
                        notice.body = "\(componentNames[component] ?? component)：\(current(component)) → \(release.version)。点击查看更新。"
                        UNUserNotificationCenter.current().add(.init(identifier: key, content: notice, trigger: nil)) { error in
                            if error == nil { UserDefaults.standard.set(release.version, forKey: key) }
                        }
                    }
                }
            } catch {
                if Task.isCancelled || error is CancellationError {
                    status.stringValue = "已取消检查更新。"
                } else {
                    checkCadence.record(at: now(), succeeded: false)
                    let message = checked ? "检查未完成，保留上次结果：" : "检查未完成："
                    status.stringValue = message + error.localizedDescription
                }
            }
        }
    }
    private func download() {
        guard !busy, let key = selectedComponent else { return }
        if key == Self.galleryID { openGalleryManager(); return }
        guard let release = releases[key], let checksum = release.checksum, isEnabled(key) else { return }
        let version = current(key), esp = espConnection()
        let device = registry.device(key), pair = tab5?.currentPairing?.deviceId
        let legacy = key == "esp8266" && esp.firmware == nil && esp.port != nil
        guard availableComponents.contains(key) || legacy else { return }
        guard release.asset.size <= 256 * 1024 * 1024 else { status.stringValue = "发布包超过下载限制。"; return }
        if key == "esp8266" && esp.port == nil { return }
        if key == "tab5" && (pair == nil || (tab5?.bootTransport == nil && tab5?.usbActive != true)) { return }
        busy = true; canCancel = true; progress.isHidden = false; progress.doubleValue = 0; controls()
        status.stringValue = "正在下载并校验 \(componentNames[key] ?? key) 的更新…"
        operation = Task { @MainActor [weak self] in
            guard let self else { return }
            defer { busy = false; canCancel = false; progress.isHidden = true; refreshRows() }
            do {
                let request = MacGalleryRelease(category:key,version:release.version,asset:release.asset,checksum:checksum)
                let path = try await downloadPackage(request) { [weak self] value in
                    DispatchQueue.main.async { self?.progress.doubleValue = value*100 }
                }
                try Task.checkCancellation()
                canCancel = false; controls()
                status.stringValue = "下载校验通过，正在检查安装包…"
                if key == "bridge" {
                    let app = try await Task.detached { try MacDesktopUpdate.unpack(path, version: release.version) }.value
                    busy = false
                    do {
                        try installDesktop(app, release.version)
                        status.stringValue = "已替换应用，正在重新打开 AI-bot…"
                    } catch MacTab5Error.busy {
                        try? FileManager.default.removeItem(at: app.deletingLastPathComponent())
                        status.stringValue = "请先完成正在进行的设备操作，再点一键升级。"
                    } catch {
                        NSWorkspace.shared.activateFileViewerSelecting([app])
                        status.stringValue = error.localizedDescription + " 已在 Finder 中显示校验后的应用，可退出后手动替换。"
                    }
                } else if key == "tab5", let tab5 {
                    let image = try await Task.detached {
                        let archive = try MacTab5Archive(path, maximumSize: 32 * 1024 * 1024, allowNested: true)
                        guard !archive.names.contains("factory.bin") else { throw MacTab5LocalError.invalid("首次安装包不能用于固件升级。") }
                        try Self.validateCompatibility(archive, entry: "EDITION.json", bridgeVersion: Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String)
                        let image = try MacTab5FirmwareImage.load(path)
                        guard image.version == release.version else { throw MacTab5LocalError.invalid("包内固件版本不匹配。") }
                        return image
                    }.value
                    guard registry.device(key)?.id == device?.id, isEnabled(key), tab5.currentPairing?.deviceId == pair, current(key) == version,
                          tab5.bootTransport != nil || tab5.usbActive else { throw MacTab5LocalError.invalid("TAB5 或运行版本已改变，请重新检查更新。") }
                    try upgradeTools()
                    try tab5.firmwareControl.select(image)
                    if MacReleaseCatalog.version(version)?.lexicographicallyPrecedes([0, 2, 190]) == false {
                        try tab5.requestUpgrade()
                        status.stringValue = "已向 TAB5 发送升级请求，等待设备保存草稿并响应。"
                    } else { status.stringValue = "已提供固件。请在 TAB5 的「设置 → 固件升级」确认。" }
                } else if key == "esp8266", let port = esp.port {
                    try await Task.detached {
                        let archive = try MacTab5Archive(path, maximumSize: 256 * 1024 * 1024, allowNested: true)
                        let metadata = try Self.validateCompatibility(archive, entry: "COMPONENT.json", bridgeVersion: Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String)
                        let binary = try archive.read("firmware.bin", maximum: 4 * 1024 * 1024)
                        guard String(data: try archive.read("VERSION", maximum: 128), encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines) == release.version,
                              metadata == nil || (metadata?["component"] as? String == key && metadata?["version"] as? String == release.version),
                              !archive.names.contains("aibot_tab5.bin"), binary.count >= 1024, binary.first == 0xe9 else {
                            throw MacTab5LocalError.invalid("固件类型或包内版本不匹配。")
                        }
                    }.value
                    guard registry.device(key)?.id == device?.id, isEnabled(key), espConnection() == esp else {
                        throw MacTab5LocalError.invalid("USB 小屏或运行版本已改变，请重新检查更新。")
                    }
                    let automatic = !legacy && esp.id != nil
                    if !automatic {
                        let confirm = NSAlert()
                        confirm.messageText = legacy ? "小屏未上报当前版本" : "小屏未上报 USB 身份"
                        confirm.informativeText = "请确认这台小屏和目标版本 \(release.version)。升级前会自动备份。"
                        confirm.addButton(withTitle: "继续"); confirm.addButton(withTitle: "取消")
                        guard confirm.runModal() == .alertFirstButtonReturn else { throw CancellationError() }
                        guard registry.device(key)?.id == device?.id, isEnabled(key), espConnection() == esp else {
                            throw MacTab5LocalError.invalid("USB 小屏已改变，请重新检查更新。")
                        }
                    }
                    try espTools(path, port, automatic ? esp.id : nil)
                    status.stringValue = automatic ? "小屏升级已开始，正在准备完整备份。" : "固件已校验，请在刷机窗口开始。"
                }
            } catch {
                status.stringValue = Task.isCancelled || error is CancellationError ? "操作已取消。已下载的部分会保留，可重试。" : "升级未继续：" + error.localizedDescription
            }
        }
    }

    @discardableResult
    nonisolated private static func validateCompatibility(_ archive: MacTab5Archive, entry: String, bridgeVersion: String?) throws -> [String: Any]? {
        guard archive.names.contains(entry) else { return nil }
        let value = try MacTab5Protocol.object(archive.read(entry, maximum: 8192))
        if let protocolVersion = value["protocolVersion"], MacTab5Protocol.integer(protocolVersion) != 1 {
            throw MacTab5LocalError.invalid("固件协议不兼容，请先更新电脑端。")
        }
        if value["minimumBridgeVersion"] != nil {
            guard let minimum = value["minimumBridgeVersion"] as? String, let required = MacReleaseCatalog.version(minimum),
                  let bridgeVersion, let installed = MacReleaseCatalog.version(bridgeVersion), !installed.lexicographicallyPrecedes(required) else {
                throw MacTab5LocalError.invalid("请先更新电脑端 AI-bot，再升级设备固件。")
            }
        }
        return value
    }
}
