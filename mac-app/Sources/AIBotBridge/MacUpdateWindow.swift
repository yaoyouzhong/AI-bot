import AppKit
import CryptoKit
import UserNotifications

struct MacComponentRelease {
    struct Asset { let name: String, url: URL, size: Int64 }
    let component: String, version: String, notes: String, tag: String
    let asset: Asset, checksum: Asset?
}
enum MacReleaseCatalog {
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
            for (component,prefix,suffix,tagPrefix) in [("bridge","AI-bot-bridge-v","-macos-arm64.zip","bridge-v"),("bridge","AIBotBridge-","-local-candidate-macos-arm64.zip","bridge-v"),("esp8266","AI-bot-esp8266-","-firmware.zip","esp8266-v"),("esp8266","AI-bot-","-firmware-materials.zip","esp8266-v"),("tab5","AI-bot-tab5-","-upgrade.zip","tab5-v"),("tab5","TAB5-upgrade-",".zip","tab5-v")] {
                for file in files where file.name.hasPrefix(prefix) && file.name.hasSuffix(suffix) && file.name.count > prefix.count + suffix.count {
                    let text = String(file.name.dropFirst(prefix.count).dropLast(suffix.count))
                    guard version(text) != nil, tag == tagPrefix + text || tag.hasPrefix("v") && version(String(tag.dropFirst())) != nil else { continue }
                    let checksum = files.first { $0.name == file.name + ".sha256" } ?? files.first { $0.name == "SHA256SUMS.txt" }
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
final class MacUpdateWindow: NSWindowController, NSWindowDelegate {
    private let tab5: MacTab5Service?, upgradeTools: () -> Void, espTools: () -> Void
    private let table = MacSettingsTable(["组件","当前版本","可用版本"]), detail = MacBridgeSettingsStyle.label(""), status = MacBridgeSettingsStyle.note("")
    private var releases: [String:MacComponentRelease] = [:], operation: Task<Void,Never>?, prepared: [String:URL] = [:]
    private let components = ["bridge","esp8266","tab5"]
    private let session: URLSession
    private(set) var busy = false
    static let automaticKey = "bridge_automatic_updates"
    private let automatic = NSButton(checkboxWithTitle:"自动检查并提醒",target:nil,action:nil), progress = NSProgressIndicator()
    private let downloader = MacGalleryDownloader()
    private var automaticTimer:Timer?, downloadButton:MacSettingsButton!, installButton:MacSettingsButton!, cancelButton:MacSettingsButton!
    init(tab5: MacTab5Service?, upgradeTools: @escaping () -> Void, espTools: @escaping () -> Void) {
        self.tab5 = tab5; self.upgradeTools = upgradeTools; self.espTools = espTools
        let configuration = URLSessionConfiguration.ephemeral; configuration.timeoutIntervalForRequest = 30; configuration.timeoutIntervalForResource = 600
        session = URLSession(configuration:configuration,delegate:MacUpdateRedirects(),delegateQueue:nil)
        super.init(window:MacBridgeSettingsStyle.window("软件固件",width:800,height:620)); window?.delegate = self
        let page = MacBridgeSettingsPage(); page.heading("软件固件",subtitle:"各组件独立比较版本；下载完成后校验 SHA-256，再打开安装或升级工具。")
        automatic.state = UserDefaults.standard.bool(forKey:Self.automaticKey) ? .on:.off; automatic.target = self; automatic.action = #selector(changeAutomatic)
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("检查更新") { [weak self] in self?.check() },automatic])); page.add(table); table.heightAnchor.constraint(equalToConstant:180).isActive = true
        progress.isIndeterminate = false; progress.maxValue = 100; progress.isHidden = true; page.add(progress)
        page.section("更新说明"); page.add(detail)
        downloadButton = MacSettingsButton("下载并准备升级") { [weak self] in self?.download() }; installButton = MacSettingsButton("打开安装／升级工具") { [weak self] in self?.install() }; cancelButton = MacSettingsButton("取消下载") { [weak self] in self?.operation?.cancel() }
        page.add(MacSettingsButton("完整下载与安装说明") { NSWorkspace.shared.open(URL(string:"https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md")!) })
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[downloadButton,installButton,cancelButton])
        table.selected = { [weak self] _ in self?.selection() }; refreshRows(); table.select(0)
    }
    deinit { automaticTimer?.invalidate(); operation?.cancel(); session.invalidateAndCancel() }
    required init?(coder:NSCoder) { fatalError() }
    func open() { refreshRows(); MacSettingsControls.open(self) }
    func startAutomaticChecks() {
        guard automaticTimer == nil else { return }
        automaticTimer = Timer.scheduledTimer(withTimeInterval:21600,repeats:true) { [weak self] _ in if UserDefaults.standard.bool(forKey:Self.automaticKey) { self?.check() } }
        if UserDefaults.standard.bool(forKey:Self.automaticKey) { check() }
    }
    @objc private func changeAutomatic() {
        UserDefaults.standard.set(automatic.state == .on,forKey:Self.automaticKey)
        if automatic.state == .on { UNUserNotificationCenter.current().requestAuthorization(options:[.alert]) { _,_ in }; check() }
    }
    private func controls() {
        let key = components.indices.contains(table.selectedRow) ? components[table.selectedRow]:""
        downloadButton?.isEnabled = !busy && releases[key]?.checksum != nil
        installButton?.isEnabled = !busy && prepared[key] != nil
        cancelButton?.isHidden = !busy; automatic.isEnabled = !busy
    }
    func windowShouldClose(_ sender:NSWindow) -> Bool { if busy { status.stringValue = "下载中；请先取消或等待完成。" }; return !busy }
    private func current(_ key: String) -> String { key == "bridge" ? Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "开发版" : key == "tab5" ? tab5?.connectionStatus.firmware ?? "待连接" : "连接设备后查看" }
    private func refreshRows() { let i = table.selectedRow; table.rows = components.map { [["bridge":"Mac 桥接","esp8266":"ESP8266","tab5":"M5Stack TAB5"][$0]!,current($0),releases[$0]?.version ?? "待检查"] }; if i >= 0 { table.select(i) } }
    private func selection() { guard components.indices.contains(table.selectedRow) else { return }; let key = components[table.selectedRow]; detail.stringValue = releases[key]?.notes ?? "点击检查更新，读取项目正式发布附件。"; controls() }
    private func bytes(_ url: URL, maximum: Int, expected: Int64? = nil) async throws -> Data {
        guard MacUpdateRedirects.allowed(url) else { throw MacTab5LocalError.invalid("下载地址不受信任。") }
        var request = URLRequest(url:url); request.setValue("AI-bot-mac",forHTTPHeaderField:"User-Agent")
        let (stream,response) = try await session.bytes(for:request)
        guard let http = response as? HTTPURLResponse, http.statusCode == 200, response.expectedContentLength <= maximum else { throw MacTab5LocalError.invalid("下载响应无效或文件过大。") }
        var data = Data(); if let expected, expected <= maximum { data.reserveCapacity(Int(expected)) }
        for try await byte in stream { try Task.checkCancellation(); guard data.count < maximum else { throw MacTab5LocalError.invalid("下载文件超过限制。") }; data.append(byte) }
        guard expected == nil || Int64(data.count) == expected else { throw MacTab5LocalError.invalid("下载大小与发布记录不一致。") }; return data
    }
    private func check() {
        guard !busy else { return }; busy = true; controls(); status.stringValue = "正在读取正式发布记录…"
        operation = Task { [weak self] in guard let self else { return }; do {
            var result: [String:MacComponentRelease] = [:], complete = false
            for page in 1...10 {
                let data = try await self.bytes(URL(string:"https://api.github.com/repos/yaoyouzhong/AI-bot/releases?per_page=100&page=\(page)")!,maximum:2*1024*1024)
                let (releases,count) = try MacReleaseCatalog.parse(data)
                for r in releases { if result[r.component] == nil || MacReleaseCatalog.version(result[r.component]!.version)!.lexicographicallyPrecedes(MacReleaseCatalog.version(r.version)!) { result[r.component] = r } }
                if count < 100 { complete = true; break }
            }
            guard complete else { throw MacTab5LocalError.invalid("发布记录超过查询范围。") }
            await MainActor.run { self.releases = result; self.refreshRows(); self.selection(); self.status.stringValue = "已检查；只显示实际发布的平台附件。"
                if UserDefaults.standard.bool(forKey:Self.automaticKey) {
                    for release in result.values {
                        guard let installed = MacReleaseCatalog.version(self.current(release.component)),let available = MacReleaseCatalog.version(release.version),installed.lexicographicallyPrecedes(available) else { continue }
                        let key = "update_notified_"+release.component
                        if UserDefaults.standard.string(forKey:key) != release.version {
                            let notice = UNMutableNotificationContent(); notice.title = "AI-bot 有可用更新"; notice.body = "\(release.component) · \(release.version)，可在软件固件中查看。"
                            UNUserNotificationCenter.current().add(.init(identifier:key,content:notice,trigger:nil)) { error in if error == nil { UserDefaults.standard.set(release.version,forKey:key) } }
                        }
                    }
                } }
        } catch { await MainActor.run { self.status.stringValue = "检查未完成，保留上次结果：" + error.localizedDescription } }; await MainActor.run { self.busy = false; self.controls(); self.progress.isHidden = true } }
    }
    private func download() {
        guard !busy, components.indices.contains(table.selectedRow), let release = releases[components[table.selectedRow]], let checksum = release.checksum else { status.stringValue = "请先检查更新并选择附带校验清单的组件。"; return }
        if let installed = MacReleaseCatalog.version(current(release.component)), let available = MacReleaseCatalog.version(release.version), !installed.lexicographicallyPrecedes(available) { status.stringValue = "当前版本相同或较新，保留现有版本。"; return }
        guard release.asset.size <= 256 * 1024 * 1024 else { status.stringValue = "发布包超过下载限制。"; return }
        busy = true; controls(); progress.isHidden = false; progress.doubleValue = 0; status.stringValue = "正在下载并校验 \(release.asset.name)…"
        operation = Task { [weak self] in guard let self else { return }; do {
            let request = MacGalleryRelease(category:release.component,version:release.version,asset:release.asset,checksum:checksum)
            let path = try await self.downloader.download(request,root:MacProfileFile<[MacBirthday]>.directory.appendingPathComponent("updates")) { [weak self] value in DispatchQueue.main.async { self?.progress.doubleValue = value*100 } }
            if release.component == "tab5" { let image = try MacTab5FirmwareImage.load(path); guard image.version == release.version else { throw MacTab5LocalError.invalid("包内固件版本不匹配。") }; try self.tab5?.firmwareControl.select(image) }
            await MainActor.run { self.prepared[release.component] = path; self.status.stringValue = "下载与校验完成。点击打开安装／升级工具；设备安装仍须明确确认。" }
        } catch { await MainActor.run { self.status.stringValue = error is CancellationError ? "下载已取消，已下载部分保留，下次可继续。":"未准备升级：" + error.localizedDescription } }; await MainActor.run { self.busy = false; self.controls(); self.progress.isHidden = true } }
    }
    private func install() {
        guard !busy, components.indices.contains(table.selectedRow) else { return }; let key = components[table.selectedRow]
        guard let path = prepared[key] else { status.stringValue = "请先下载并校验对应组件。"; return }
        if key == "bridge" { NSWorkspace.shared.activateFileViewerSelecting([path]); status.stringValue = "安装包已在 Finder 中显示，解压后按包内说明安装。" }
        else if key == "tab5" { upgradeTools() }
        else { NSWorkspace.shared.activateFileViewerSelecting([path]); espTools() }
    }
}
