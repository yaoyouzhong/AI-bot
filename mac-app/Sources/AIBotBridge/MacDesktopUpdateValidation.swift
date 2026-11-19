import AppKit
import Darwin

// Runs from a signed App copied to <fixture>/installed, before the bridge's
// singleton or production AppDelegate. Only the fixture release and profile are read.
@MainActor enum MacDesktopUpdateValidation {
    private struct Fixture: Decodable {
        let version: String
        let archive: String
        let sha256: String
    }

    private struct Receipt: Codable {
        let oldVersion: String
        let oldPID: Int32
        let configSHA256: String
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
                              configSHA256: try MacTab5Protocol.sha256(Data(contentsOf: registry.file.url)))
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
                try await downloader.download(update, root: root.appendingPathComponent("updates"), progress: progress)
            })
        controller.open(component: "bridge")
        try waitFor("Catalog did not load.") { !controller.busy }
        func descendants(_ view: NSView) -> [NSView] { [view] + view.subviews.flatMap(descendants) }
        guard let content = controller.window?.contentView,
              let button = descendants(content).compactMap({ $0 as? NSButton }).first(where: { $0.title == "一键升级" }),
              button.isEnabled else { throw MacTab5LocalError.invalid("One-click update is unavailable.") }
        button.performClick(nil)
        try waitFor("Invalid checksum test timed out.") { !controller.busy }
        guard !installedCandidate, button.isEnabled,
              descendants(content).compactMap({ $0 as? NSTextField }).contains(where: { $0.stringValue.contains("未通过 SHA-256 校验") }),
              !FileManager.default.fileExists(atPath: root.appendingPathComponent("before.json").path) else {
            throw MacTab5LocalError.invalid("Invalid checksum did not leave the installed app available for retry.")
        }

        FixtureResponse.set([packageURL: asset, checksumURL: checksum(fixture.sha256)])
        button.performClick(nil)
        try waitFor("Verified package did not install.") { installedCandidate }
        print("MAC_DESKTOP_UPDATE_SWAP_OK invalid checksum blocked and retry installed verified bytes")
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
                      "predecessorExited": "yes", "isolatedConfigPreserved": "yes"]
        try JSONEncoder().encode(result).write(to: root.appendingPathComponent("boot.json"), options: .atomic)
    }
}

private final class FixtureResponse: URLProtocol {
    private static let lock = NSLock()
    private static var replies: [URL: Data] = [:]

    static func set(_ value: [URL: Data]) { lock.withLock { replies = value } }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url, let body = Self.lock.withLock({ Self.replies[url] }),
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
