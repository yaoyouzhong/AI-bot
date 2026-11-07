import Foundation

// Explicit diagnostic fixture only. A temporary directory, fixed dummy key,
// loopback endpoint and independently installed CLI are all mandatory. The
// normal bridge never constructs this adapter or accepts fixture operations.
final class MacTab5WorkspaceAcceptance {
    enum Failure: String, Error { case temporaryRoot, fixtureFiles, environment, cliUnavailable }
    let root: URL
    let claude: MacClaudeWorkspace
    let codex: MacTab5Codex
    private let terminalSession: UUID?
    static let codexID = "11111111-1111-4111-8111-111111111111"
    init(root: URL) throws {
        let resolved = root.resolvingSymlinksInPath().standardizedFileURL
        let temporary = FileManager.default.temporaryDirectory.resolvingSymlinksInPath().standardizedFileURL.path+"/"
        guard resolved.path.hasPrefix(temporary) else { throw Failure.temporaryRoot }
        guard let mode = try? String(contentsOf:resolved.appendingPathComponent(".aibot-synthetic-claude"),encoding:.utf8),
              ["1\n", "terminal\n"].contains(mode),
              let bytes = try? Data(contentsOf:resolved.appendingPathComponent("environment.json")),
              let environment = try JSONSerialization.jsonObject(with:bytes) as? [String:String] else { throw Failure.fixtureFiles }
        func sameDirectory(_ value: String?) -> Bool {
            value.map { URL(fileURLWithPath:$0,isDirectory:true).resolvingSymlinksInPath().standardizedFileURL == resolved } ?? false
        }
        guard sameDirectory(environment["HOME"]), sameDirectory(environment["TMPDIR"]),
              environment["ANTHROPIC_API_KEY"] == "synthetic-key",
              Set(environment.keys).isSubset(of:["PATH","HOME","TMPDIR","ANTHROPIC_BASE_URL","ANTHROPIC_API_KEY","CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC","DISABLE_TELEMETRY","DISABLE_ERROR_REPORTING"]),
              let endpoint = environment["ANTHROPIC_BASE_URL"].flatMap(URL.init(string:)),endpoint.scheme == "http",endpoint.host == "127.0.0.1",endpoint.port != nil else { throw Failure.environment }
        guard let executable = MacClaudeWorkspaceBackend.locate() else { throw Failure.cliUnavailable }
        self.root = resolved
        let key = Data(0..<32), store = MacTab5PrivateStore(root:resolved.appendingPathComponent("sealed"),key:{key})
        let harnesses: MacClaudeHarnesses?
        if mode == "terminal\n" {
            let config = resolved.appendingPathComponent(".claude",isDirectory:true)
            guard config.resolvingSymlinksInPath().path == config.path,
                  let text = try? String(contentsOf:resolved.appendingPathComponent("terminal-session"),encoding:.utf8),
                  let session = UUID(uuidString:text.trimmingCharacters(in:.whitespacesAndNewlines)),
                  MacClaudeTerminalSession.find(session,config:config) != nil else { throw Failure.fixtureFiles }
            terminalSession = session
            harnesses = try MacClaudeHarnesses([
                .init(source:.terminal,discover:{[]},live:{[session]},resolve:{
                    $0 == session ? MacClaudeTerminalSession.find($0,config:config) : nil
                })
            ])
        } else { terminalSession = nil; harnesses = nil }
        let backend = MacClaudeWorkspaceBackend(root:resolved.appendingPathComponent(".claude/projects"),store:store,key:{key},executable:{executable},environment:environment,
            arguments:["--model","claude-sonnet-4-6","--setting-sources","user","--strict-mcp-config","--tools","","--disable-slash-commands","--max-budget-usd","0.05"],
            legacyFixture:harnesses == nil,harnesses:harnesses)
        // Capture a standalone locked set, avoiding a self/let initialization cycle.
        let visibility = Visibility(); self.visibility = visibility
        claude = MacClaudeWorkspace(store:store,backend:backend,enabled:{visibility.has("claude")})
        codex = MacTab5Codex(store:MacTab5PrivateStore(root:resolved.appendingPathComponent("codex-sealed"),key:{key}),enabled:{visibility.has("codex")},sendEnabled:{false},call:{ method,params in
            if method == "thread/list" { return ["data":[["id":Self.codexID,"name":"自动验收 Codex 会话","cwd":"/synthetic/AI-bot","updatedAt":Int64(Date().timeIntervalSince1970),"status":["type":"idle"]]]] }
            if method == "thread/read", params["threadId"] as? String == Self.codexID {
                return ["thread":["id":Self.codexID,"status":["type":"idle"],"turns":[["id":"11111111-1111-4111-8111-111111111112","status":"completed","items":[["type":"agentMessage","text":"Codex 合成公开回复，用于双客户端隔离验收。"]]]]]]
            }
            throw MacTab5Error.invalidPacket
        })
    }
    private final class Visibility {
        let lock = NSLock(); var clients: Set<String> = ["claude"]
        func has(_ name: String) -> Bool { lock.lock(); defer { lock.unlock() }; return clients.contains(name) }
    }
    private let visibility: Visibility
    func setClients(_ clients: [String]) throws {
        guard !clients.isEmpty, Set(clients).isSubset(of:["codex","claude"]) else { throw MacTab5Error.invalidPacket }
        visibility.lock.lock(); visibility.clients = Set(clients); visibility.lock.unlock()
        // Catalog refresh has no UI side effects. Requests still use each
        // client's own encrypted storage and the production device RPC path.
        claude.tasks.refresh(); codex.refresh()
    }
    func stageTerminalDraft(_ text: String, photo: Bool, device: String) throws {
        guard let terminalSession, let task = claude.backend.alias(session:terminalSession),
              text.utf8.count <= 2000 else { throw Failure.fixtureFiles }
        var images: [String] = []
        if photo {
            let file = root.appendingPathComponent("terminal-photo.jpg")
            guard file.resolvingSymlinksInPath().path == file.path,
                  let size = try file.resourceValues(forKeys:[.fileSizeKey]).fileSize,
                  (32...MacTab5Photos.maxBytes).contains(size) else { throw Failure.fixtureFiles }
            let id = UUID().uuidString.lowercased()
            let result = claude.tasks.photos.upload(["taskId":task,"requestId":id],bytes:try Data(contentsOf:file),device:device)
            guard result.status == 200 else { throw Failure.fixtureFiles }
            images = [id]
        }
        let result = claude.tasks.handle(["op":"draft-save","taskId":task,"requestId":UUID().uuidString.lowercased(),
                                          "message":text,"images":images],device:device)
        guard result.status == 200 else { throw Failure.fixtureFiles }
    }
    var projects: URL { root.appendingPathComponent(".claude/projects") }
    func voice() -> MacTab5Voice {
        final class Callback { var value: ((String,Bool,MacTab5SpeechFailure?) -> Void)? }
        let callback = Callback()
        return MacTab5Voice(enabled:{true},backend:.init(start:{ _, result in callback.value = result },feed:{ _ in },finish:{ callback.value?("自动验收语音文字",true,nil) },cancel:{callback.value = nil}))
    }
    func snapshot() -> MacStatusSnapshot {
        let now = Date(), format = DateFormatter(); format.dateFormat = "HH:mm:ss"
        return MacStatusSnapshot(version:1,time:format.string(from:now),epochUtc:Int64(now.timeIntervalSince1970),utcOffsetSeconds:TimeZone.current.secondsFromGMT(),capturedAt:now,
            codex:ToolState(state:"idle",ageSeconds:60),claude:ToolState(state:"idle",ageSeconds:0),musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
    }
}
