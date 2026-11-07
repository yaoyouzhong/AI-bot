import AppKit

// Explicit development session. JSON lines on stdin; one response per action.
// It never creates a window or sends an action without a caller request.
enum MacTab5AutomationRunner {
    private final class Input: @unchecked Sendable {
        let lock = NSLock(); var lines: [String] = []; var ended = false
        init() {
            Thread.detachNewThread { [self] in
                while let line = readLine() { lock.lock(); if lines.count < 64 { lines.append(line) }; lock.unlock() }
                lock.lock(); ended = true; lock.unlock()
            }
        }
        func pop() -> (String?, Bool) { lock.lock(); defer { lock.unlock() }; return (lines.isEmpty ? nil:lines.removeFirst(),ended) }
    }
    static func run(directory: URL, fixture: URL? = nil) throws {
        // Real voice backends use AppKit even when this diagnostic entry has
        // no application delegate. Initialize without activating a window.
        _ = NSApplication.shared
        NSApp.setActivationPolicy(.accessory)
        guard !FileManager.default.fileExists(atPath:directory.path) else { throw MacTab5Error.invalidPacket }
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
        let registry = MacDeviceRegistry()
        guard registry.enabled("tab5") else { throw MacTab5Error.unavailable }
        let stored = UserDefaults.standard.integer(forKey:"status_port"), port = UInt16((1...65535).contains(stored) ? stored:8765)
        guard let address = LocalNetworkIdentity.privateIPv4() else { throw MacTab5Error.unavailable }
        let identity = MacClaudeIdentity()
        let acceptance = try fixture.map { try MacTab5WorkspaceAcceptance(root:$0) }
        let service = MacTab5Service(port:port,codex:acceptance?.codex ?? MacTab5Codex(),voice:acceptance?.voice() ?? MacTab5Voice(),enabled:{registry.enabled("tab5")},sourceEnabled:{registry.source($0,kind:"tab5")},
            claudeSessions:MacClaudeSessionSampler(root:acceptance?.projects ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects")),claudeIdentity:identity,
            claudeWorkspace:acceptance?.claude)
        guard service.currentPairing?.confirmed == true else { throw MacTab5Error.pairingRequired }
        let usb = MacTab5USB(service:service,authenticationEndpoint:{(address,port)})
        let server = HTTPStatusServer(port:port,token:{nil},claudeEvent:{identity.record($0)},espEnabled:{false},loopbackOnly:true,snapshot:{Data()})
        try server.start(); usb.start()
        if acceptance == nil { service.codex.start() }
        service.claude.start()
        if acceptance != nil { service.voice.beforeStart = nil }
        defer { service.uiAutomation.cancel(); service.screenPreview.stop(); usb.stopAndWait(); server.stop(); service.stop() }
        let reader = SessionActivityReader()
        var lastPublish = -Double.infinity
        func pump() {
            let now = ProcessInfo.processInfo.systemUptime
            if now-lastPublish >= 0.4 { service.publish(registry.filter(acceptance?.snapshot() ?? reader.capture(),kind:"tab5")); lastPublish = now }
            RunLoop.current.run(until:Date(timeIntervalSinceNow:0.05))
        }
        func action(_ value:[String:Any]) throws -> MacTab5UIAutomation.Result {
            let request = try service.uiAutomation.begin(value)
            defer { service.uiAutomation.cancel() }
            let deadline = ProcessInfo.processInfo.systemUptime+8
            while ProcessInfo.processInfo.systemUptime < deadline {
                pump()
                if let result = service.uiAutomation.snapshot(), result.request == request, result.status != 1 {
                    guard result.status == 2 else { throw MacTab5Error.unconfirmed }; return result
                }
            }
            throw MacTab5Error.timeout
        }
        func emit(_ value:[String:Any]) throws {
            let data = try JSONSerialization.data(withJSONObject:value,options:[.sortedKeys])
            try FileHandle.standardOutput.write(contentsOf:data+Data("\n".utf8))
        }
        func settings() throws -> MacTab5DisplaySettings {
            let lock = NSLock(); var outcome: Result<MacTab5DisplaySettings,Error>?
            service.displaySettings(nil) { value in lock.lock(); outcome = value; lock.unlock() }
            let deadline = ProcessInfo.processInfo.systemUptime+15
            while ProcessInfo.processInfo.systemUptime < deadline {
                lock.lock(); let value = outcome; lock.unlock()
                if let value { return try value.get() }; pump()
            }
            throw MacTab5Error.timeout
        }
        let connectDeadline = ProcessInfo.processInfo.systemUptime+45
        while !service.usbActive, ProcessInfo.processInfo.systemUptime < connectDeadline { pump() }
        guard service.usbActive else { throw MacTab5Error.unavailable }
        let before = try settings()
        var viewport = try action(["op":"probe"])
        try emit(["status":"ready","width":viewport.width,"height":viewport.height,"rotation":viewport.rotation])
        let input = Input(), until = ProcessInfo.processInfo.systemUptime+600
        while ProcessInfo.processInfo.systemUptime < until {
            pump(); let next = input.pop()
            guard let line = next.0 else { if next.1 { break }; continue }
            do {
                guard line.utf8.count <= 4096, let value = try JSONSerialization.jsonObject(with:Data(line.utf8)) as? [String:Any],
                      let op = value["op"] as? String else { throw MacTab5Error.invalidPacket }
                if op == "stop" {
                    let preserved = try settings() == before
                    try emit(["status":"stopped","settingsPreserved":preserved])
                    guard preserved else { throw MacTab5Error.unconfirmed }; return
                }
                if op == "fixture-clients", let acceptance, let clients = value["clients"] as? [String] {
                    try acceptance.setClients(clients)
                    try emit(["status":"fixture-clients-set"]); continue
                }
                if op == "capture" {
                    guard let file = value["file"] as? String, file.hasSuffix(".png"), file.utf8.count <= 80,
                          file.unicodeScalars.allSatisfy({ CharacterSet.alphanumerics.contains($0) || "-_.".unicodeScalars.contains($0) }),
                          !file.hasPrefix(".") else { throw MacTab5Error.invalidPacket }
                    let token = service.screenPreview.start(service.screenPreviewContext)
                    defer { service.screenPreview.stop(token) }
                    let deadline = ProcessInfo.processInfo.systemUptime+35
                    var saved = false
                    while ProcessInfo.processInfo.systemUptime < deadline {
                        service.screenPreview.renew(token,context:service.screenPreviewContext); pump()
                        guard let frame = service.screenPreview.snapshot(token).0 else { continue }
                        guard let bitmap = MacTab5ScreenPreviewWindow.image(frame)?.representations.first as? NSBitmapImageRep,
                              let png = bitmap.representation(using:.png,properties:[:]) else { throw MacTab5Error.unconfirmed }
                        try png.write(to:directory.appendingPathComponent(file),options:.withoutOverwriting)
                        try emit(["status":"captured","file":file,"width":frame.width,"height":frame.height]); saved = true; break
                    }
                    if !saved { throw MacTab5Error.timeout }
                } else {
                    var command = value
                    if command["width"] == nil { command["width"] = viewport.width }
                    if command["height"] == nil { command["height"] = viewport.height }
                    if command["rotation"] == nil { command["rotation"] = viewport.rotation }
                    viewport = try action(command)
                    let settle = ProcessInfo.processInfo.systemUptime+0.4
                    while ProcessInfo.processInfo.systemUptime < settle { pump() }
                    try emit(["status":"done","op":op,"width":viewport.width,"height":viewport.height,"rotation":viewport.rotation])
                }
            } catch { try emit(["status":"error","reason":"action_unconfirmed"]) }
        }
        try emit(["status":"stopped","settingsPreserved":try settings() == before])
    }
}
