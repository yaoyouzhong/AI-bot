import ApplicationServices
import AppKit
import Darwin

// Runs from a signed App copied to <fixture>/installed, before the bridge's
// singleton or production AppDelegate. Only the fixture release and profile are read.
@MainActor enum MacDesktopUpdateValidation {
    private struct Fixture: Decodable {
        let version: String
        let archive: String
        let sha256: String
        let expectsSigningMigration: Bool?
    }

    private struct Receipt: Codable {
        let oldVersion: String
        let oldPID: Int32
        let configSHA256: String
        let accessibility: Bool?
    }

    private static func context(_ path: URL) throws -> (root: URL, fixture: Fixture, installedVersion: String) {
        let root = path.resolvingSymlinksInPath()
        let expectedApp = root.appendingPathComponent("installed/AIBotBridge.app").path
        guard Bundle.main.bundleURL.resolvingSymlinksInPath().path == expectedApp,
              let installed = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String,
              MacReleaseCatalog.version(installed) != nil else {
            throw MacTab5LocalError.invalid("Start the isolated installed/AIBotBridge.app fixture.")
        }
        let manifest = try JSONDecoder().decode(Fixture.self, from: Data(contentsOf: root.appendingPathComponent("fixture.json")))
        guard MacReleaseCatalog.version(manifest.version) != nil,
              manifest.archive == "AI-bot-bridge-v\(manifest.version)-macos-arm64.zip",
              MacTab5Protocol.isHex(manifest.sha256, count: 64) else { throw MacTab5Error.invalidPacket }
        return (root, manifest, installed)
    }

    private static func waitFor(_ description: String, until ready: () -> Bool) throws {
        let deadline = Date().addingTimeInterval(45)
        while !ready(), Date() < deadline { RunLoop.current.run(until: Date().addingTimeInterval(0.025)) }
        guard ready() else { throw MacTab5LocalError.invalid(description) }
    }

    static func run(_ directory: URL) throws {
        let (root, fixture, installed) = try context(directory)
        guard MacReleaseCatalog.version(installed)!.lexicographicallyPrecedes(MacReleaseCatalog.version(fixture.version)!) else {
            throw MacTab5LocalError.invalid("Expected a newer fixture release.")
        }
        let registry = MacDeviceRegistry(directory: root.appendingPathComponent("profile"))
        try registry.file.write([.init(kind: "tab5", name: "验收设备", enabled: false, hardwareID: "001122334455")])
        let receipt = Receipt(oldVersion: installed, oldPID: ProcessInfo.processInfo.processIdentifier,
                              configSHA256: try MacTab5Protocol.sha256(Data(contentsOf: registry.file.url)),
                              accessibility: AXIsProcessTrusted())
        let asset = try Data(contentsOf: root.appendingPathComponent(fixture.archive), options: .mappedIfSafe)
        let base = "https://github.com/yaoyouzhong/AI-bot/releases/download/v\(fixture.version)/"
        let packageURL = URL(string: base + fixture.archive)!, checksumURL = URL(string: base + "SHA256SUMS.txt")!
        func checksum(_ hash: String) -> Data { Data("\(hash)  \(fixture.archive)\n".utf8) }
        let catalog = try JSONSerialization.data(withJSONObject: [["tag_name": "v" + fixture.version, "draft": false, "prerelease": false,
            "body": "## 桌面桥接 \(fixture.version)\n- 一键升级验收",
            "assets": [["name": fixture.archive, "size": asset.count, "browser_download_url": packageURL.absoluteString],
                       ["name": "SHA256SUMS.txt", "size": checksum(fixture.sha256).count, "browser_download_url": checksumURL.absoluteString]]]])
        FixtureResponse.set([packageURL: asset, checksumURL: checksum(String(repeating: "0", count: 64))])
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [FixtureResponse.self]
        let downloader = MacGalleryDownloader(configuration: configuration)
        let downloads = root.appendingPathComponent("updates")
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)
        var installedCandidate = false
        let controller = MacUpdateWindow(tab5: nil, registry: registry,
            upgradeTools: {}, espTools: { _, _, _ in }, installDesktop: { update, version in
                guard version == fixture.version, !installedCandidate else { throw MacTab5Error.invalidPacket }
                try JSONEncoder().encode(receipt).write(to: root.appendingPathComponent("before.json"), options: .atomic)
                try MacDesktopUpdate.install(update, replacing: Bundle.main.bundleURL, version: version) { target, staging, extracted in
                    try MacDesktopUpdate.launchAfterExit(target, staging, extracted,
                        arguments: ["--self-test-desktop-update-boot", root.path])
                }
                installedCandidate = true
            }, catalogPage: { _ in catalog },
            downloadPackage: { update, progress in
                try await downloader.download(update, root: downloads, progress: progress)
            })
        controller.open(component: "bridge")
        try waitFor("Catalog did not load.") { !controller.busy }
        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }
        guard let window = controller.window, let content = window.contentView,
              let button = descendants(content).compactMap({ $0 as? NSButton }).first(where: { $0.title == "一键升级" }),
              button.isEnabled else { throw MacTab5LocalError.invalid("One-click update is unavailable.") }
        func statusContains(_ message: String) -> Bool {
            descendants(content).compactMap { $0 as? NSTextField }.contains { $0.stringValue.contains(message) }
        }
        func notInstalled() -> Bool {
            !installedCandidate && !FileManager.default.fileExists(atPath: root.appendingPathComponent("before.json").path)
        }
        func rejectInvalidChecksum() throws {
            button.performClick(nil)
            try waitFor("Invalid checksum appeared to pass preparation.") { !controller.busy || window.attachedSheet != nil }
            guard notInstalled(), window.attachedSheet == nil, button.isEnabled, statusContains("未通过 SHA-256 校验") else {
                throw MacTab5LocalError.invalid("Invalid checksum did not leave the installed app available for retry.")
            }
        }
        func prepare(_ appearance: NSAppearance.Name? = nil, preview: String? = nil) throws -> (window: NSWindow, install: NSButton, later: NSButton) {
            window.appearance = appearance.flatMap(NSAppearance.init(named:))
            button.performClick(nil)
            try waitFor("Verified package did not reach confirmation.") { window.attachedSheet != nil || !controller.busy }
            guard notInstalled(), let sheet = window.attachedSheet, let view = sheet.contentView,
                  statusContains("等待确认安装"), !button.isEnabled else {
                throw MacTab5LocalError.invalid("The prepared update did not wait for an explicit choice.")
            }
            let buttons = descendants(view).compactMap { $0 as? NSButton }
            if let migration = fixture.expectsSigningMigration {
                let showsMigration = descendants(view).compactMap { $0 as? NSTextField }
                    .contains { $0.stringValue.contains("切换到固定签名") }
                guard showsMigration == migration else { throw MacTab5LocalError.invalid("Signing migration notice did not match the update.") }
            }
            guard let install = buttons.first(where: { $0.title == "安装并重启" }),
                  let later = buttons.first(where: { $0.title == "稍后" }), later.keyEquivalent == "\u{1b}" else {
                throw MacTab5LocalError.invalid("The upgrade confirmation actions were not available.")
            }
            if let preview { try MacTab5UIValidation.render(view, to: root.appendingPathComponent(preview)) }
            return (sheet, install, later)
        }

        try rejectInvalidChecksum()
        FixtureResponse.set([packageURL: asset, checksumURL: checksum(fixture.sha256)])
        let packageRequests = FixtureResponse.requestCount(packageURL)
        let cached = downloads.appendingPathComponent("downloads/\(fixture.sha256)/\(fixture.archive)")
        let light = try prepare(.aqua, preview: "confirmation-light.png")
        light.later.performClick(nil)
        try waitFor("Later did not return to the update window.") { !controller.busy }
        guard notInstalled(), button.isEnabled, statusContains("已保留安装包"),
              try MacTab5Protocol.sha256(Data(contentsOf: cached)) == fixture.sha256 else {
            throw MacTab5LocalError.invalid("Later did not preserve the verified package and installed app.")
        }
        let dark = try prepare(.darkAqua, preview: "confirmation-dark.png")
        guard let escape = NSEvent.keyEvent(with: .keyDown, location: .zero, modifierFlags: [], timestamp: 0,
                                           windowNumber: dark.window.windowNumber, context: nil, characters: "\u{1b}",
                                           charactersIgnoringModifiers: "\u{1b}", isARepeat: false, keyCode: 53) else {
            throw MacTab5Error.unavailable
        }
        dark.window.sendEvent(escape)
        try waitFor("Escape did not defer the installation.") { !controller.busy }
        guard notInstalled(), statusContains("已保留安装包"), FixtureResponse.requestCount(packageURL) == packageRequests + 1 else {
            throw MacTab5LocalError.invalid("Retry downloaded the same package again or installed without confirmation.")
        }

        // Damage the local copy without changing its length so retry must hash
        // the existing bytes, not infer validity from the completed download.
        let handle = try FileHandle(forWritingTo: cached)
        try handle.write(contentsOf: Data([(asset.first ?? 0) ^ 0xff]))
        try handle.close()
        try rejectInvalidChecksum()

        let final = try prepare()
        guard FixtureResponse.requestCount(packageURL) == packageRequests + 2 else {
            throw MacTab5LocalError.invalid("The invalid cache was not downloaded again on retry.")
        }
        final.install.performClick(nil)
        try waitFor("Verified package did not install.") { installedCandidate }
        print("MAC_DESKTOP_UPDATE_SWAP_OK checksum rejection, Later and Escape preserved the app, retry reused verified bytes, confirmation installed once")
        app.terminate(nil)
    }

    static func verifyBoot(_ directory: URL) throws {
        let (root, fixture, installed) = try context(directory)
        let before = try JSONDecoder().decode(Receipt.self, from: Data(contentsOf: root.appendingPathComponent("before.json")))
        let registry = MacDeviceRegistry(directory: root.appendingPathComponent("profile"))
        guard installed == fixture.version, getpid() != before.oldPID,
              kill(before.oldPID, 0) == -1, errno == ESRCH,
              try MacTab5Protocol.sha256(Data(contentsOf: registry.file.url)) == before.configSHA256,
              let device = try registry.file.read().first, device.name == "验收设备", !device.enabled else {
            throw MacTab5LocalError.invalid("Restart did not retain the isolated device configuration.")
        }
        let result = ["oldVersion": before.oldVersion, "runningVersion": installed, "packageSHA256": fixture.sha256,
                      "predecessorExited": "yes", "isolatedConfigPreserved": "yes",
                      "accessibilityBefore": before.accessibility.map(String.init) ?? "not recorded",
                      "accessibilityAfter": String(AXIsProcessTrusted())]
        try JSONEncoder().encode(result).write(to: root.appendingPathComponent("boot.json"), options: .atomic)
    }
}

private final class FixtureResponse: URLProtocol {
    private static let lock = NSLock()
    private static var replies: [URL: Data] = [:]
    private static var requests: [URL: Int] = [:]

    static func set(_ value: [URL: Data]) { lock.withLock { replies = value } }
    static func requestCount(_ url: URL) -> Int { lock.withLock { requests[url, default: 0] } }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url, let body = Self.lock.withLock({
            Self.requests[url, default: 0] += 1
            return Self.replies[url]
        }),
              let reply = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1",
                                          headerFields: ["Content-Length": String(body.count)]) else {
            client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable))
            return
        }
        client?.urlProtocol(self, didReceive: reply, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}
