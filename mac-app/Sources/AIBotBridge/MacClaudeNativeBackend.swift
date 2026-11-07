import Foundation

final class MacClaudeNativeBackend {
    private struct Record: Codable {
        let task:String,operation:String,expected:String,baseline:MacClaudeNativeReceipt
        var confirmed:String?
        var submissionNotInvoked:Bool?
        var stopAcknowledged:Bool?
    }
    private let root:URL,store:MacTab5PrivateStore,key:()throws->Data
    private let harnesses: MacClaudeHarnesses, sources: MacClaudeSourceCache
    private let factory: ((UUID) -> (any MacClaudeNativeSession)?)?
    private let gate=NSLock(),recordsGate=NSLock(),generationGate=NSLock()
    private var generation=0
    var authorized:()->Bool={false}
    init(root: URL, store: MacTab5PrivateStore, key: @escaping () throws -> Data, harnesses: MacClaudeHarnesses,
         factory: ((UUID) -> (any MacClaudeNativeSession)?)?) {
        self.root = root; self.store = store; self.key = key; self.factory = factory; self.harnesses = harnesses
        sources = MacClaudeSourceCache(store: store)
    }
    func liveSessions() -> Set<UUID>? { factory == nil ? harnesses.liveSessions() : nil }
    func discover() -> [MacClaudeSessionObservation] { harnesses.discover() }
    private func owner(_ session: UUID) -> MacClaudeHarnesses.Owner {
        guard let factory else { return harnesses.owner(session) }
        guard let client = factory(session), client.session == session else { return .unavailable }
        return .verified(client)
    }
    private func history(_ entry:MacClaudeCatalog.Entry, client: (any MacClaudeNativeSession)?)throws->MacClaudeHistory.Value {
        let secret=try key();guard secret.count==32 else{throw MacTab5Error.keychain}
        return try MacClaudeHistory.read(root:root,session:entry.session,key:secret,backgroundImages:client is MacClaudeBackground || client is MacClaudeTerminalSession){
            client?.isVerifiedBlank() == true
        }
    }
    private func turn(_ entry:MacClaudeCatalog.Entry,_ user:String)throws->String{try MacClaudeCatalog.alias("claude:turn:"+entry.session.uuidString.lowercased()+":"+user,key:key())}
    private func records()throws->[String:Record]{guard let bytes=try store.read("native-requests") else{return [:]};return try JSONDecoder().decode([String:Record].self,from:bytes)}
    private func save(_ id:String,_ record:Record)throws{recordsGate.lock();defer{recordsGate.unlock()};var values=try records();guard values.count<512||values[id] != nil else{throw MacTab5Error.unavailable};values[id]=record;try store.write("native-requests",JSONEncoder().encode(values))}
    func stop(){generationGate.lock();generation += 1;generationGate.unlock()}
    private func currentGeneration()->Int{generationGate.lock();defer{generationGate.unlock()};return generation}
    func thread(_ entry: MacClaudeCatalog.Entry, includeHistory: Bool, live: Set<UUID>? = nil) throws -> [String: Any] {
        let ownership = live?.contains(entry.session) == false ? .unavailable : owner(entry.session)
        let client = ownership.client, state = (try? client?.state()) ?? "unknown"
        let running = ["busy", "working", "waiting"].contains(state), waiting = state == "waiting"
        let source: MacClaudeSessionSource
        switch ownership {
        case .verified(let client): source = sources.source(entry.alias, observed: client.source == .unknown ? nil : client.source)
        case .conflict: source = .multiple
        case .invalid: source = .unknown
        case .unavailable: source = sources.source(entry.alias)
        }
        var historyValue: MacClaudeHistory.Value?
        if includeHistory { historyValue = try history(entry, client: client) }
        else if client != nil, state != "unknown" { historyValue = try? history(entry, client: client) }
        var turns = historyValue?.turns ?? []
        let exactRunningTurn = running && turns.last?["status"] as? String == "unknown" &&
            (turns.last?["id"] as? String).map { alias in
                historyValue?.accepted.contains { id in UUID(uuidString: id) != nil && (try? turn(entry, id)) == alias } == true
            } == true
        if exactRunningTurn { turns[turns.count - 1]["status"] = "inProgress" }
        if includeHistory, !turns.isEmpty {
            let stopped = Set((try? records())?.values.compactMap { record -> String? in
                guard record.task == entry.alias, record.operation == "interrupt", record.baseline.session == entry.session,
                      record.stopAcknowledged == true, record.confirmed == record.expected else { return nil }
                return record.confirmed
            } ?? [])
            for index in turns.indices where stopped.contains(turns[index]["id"] as? String ?? "") {
                turns[index]["status"] = "interrupted"
            }
        }
        let status: [String: Any] = running ? ["type":"active", "activeFlags": waiting ? ["waitingOnApproval"] : [String]()]
            : state == "idle" ? ["type":"idle"] : ["type":"notLoaded"]
        var control: [String: Any]?
        if let client, historyValue?.limited == false, state == "idle" || running {
            let canSend = state == "idle" && client.allows("send", images: false)
            let canSteer = exactRunningTurn && !waiting && client.allows("steer", images: false)
            let canInterrupt = exactRunningTurn && !waiting && client.allows("interrupt", images: false)
            if waiting || canSend || canSteer || canInterrupt {
                control = ["taskId": entry.alias, "turnId": turns.last?["id"] as? String ?? "",
                           "running": running, "waiting": waiting, "canSend": canSend, "canSteer": canSteer, "canInterrupt": canInterrupt]
            }
        }
        return ["id": entry.alias, "turns": includeHistory ? turns : [], "historyLimited": historyValue?.limited ?? true,
                "status": status, "source": source.wire, "nativeControl": control as Any? ?? NSNull(), "controlUnavailable": control == nil]
    }
    func selection(_ entries:[MacClaudeCatalog.Entry],bound:MacClaudeIdentity.Selection?,now:Date)->MacClaudeIdentity.Selection? {
        let live=liveSessions()
        let observed=entries.filter{live?.contains($0.session) != false}.compactMap { entry -> MacClaudeIdentity.Selection? in
            guard let client=owner(entry.session).client,let state=try? client.state(),let value=try? history(entry, client: client),let latest=value.turns.last,let alias=latest["id"] as? String,
                  let raw=value.accepted.first(where:{(try? turn(entry,$0))==alias}),let prompt=UUID(uuidString:raw) else{return nil}
            let running=["busy","working","waiting"].contains(state),recorded=latest["status"] as? String ?? "unknown",outcome=running && recorded == "unknown" ? "inProgress":recorded
            let at=(latest["endedAt"] as? Int64 ?? 0)>0 ? latest["endedAt"] as? Int64 ?? 0:latest["startedAt"] as? Int64 ?? 0
            return MacClaudeIdentity.Selection(sessionID:entry.session,promptID:prompt,activity:running ? now:Date(timeIntervalSince1970:Double(at)),state:state=="waiting" ? "working":running ? "working":state=="idle" ? "idle":"unknown",outcome:outcome,needsInput:state=="waiting",completionAt:outcome=="completed" ? at:0,completionSequence:0,ownedTurn:alias)
        }
        return MacClaudeIdentity.resolve(bound:bound,owned:observed)
    }
    func call(_ method:String,entry:MacClaudeCatalog.Entry,params:[String:Any])throws->[String:Any] {
        gate.lock();defer{gate.unlock()}
        guard let id=MacTab5Codex.id(params["requestId"]),["turn/start","turn/steer","turn/interrupt"].contains(method),authorized() else{throw MacTab5Error.unconfirmed}
        let operation=method=="turn/start" ? "send":method=="turn/steer" ? "steer":"interrupt"
        let expected = params[operation == "interrupt" ? "turnId" : "expectedTurnId"] as? String ?? ""
        if let existing = try records()[id] {
            guard existing.task == entry.alias, existing.baseline.session == entry.session,
                  existing.operation == operation, existing.expected == expected else { throw MacTab5Error.unconfirmed }
            if existing.submissionNotInvoked == true { throw MacCodexDesktopError.rejected("composer_changed") }
            if let turn = receipt(entry,id) { return ["turnId":turn] }
            throw MacTab5Error.timeout
        }
        guard let client=owner(entry.session).client,client.session==entry.session else{throw MacCodexDesktopError.rejected("native_session_unavailable")}
        let inputs=params["input"] as? [[String:Any]] ?? [],text=inputs.filter{$0["type"] as? String=="text"}.compactMap{$0["text"] as? String}.joined(separator:"\n")
        let images = inputs.filter { $0["type"] as? String == "image" }
        guard images.count <= MacTab5Photos.maxCount, client.allows(operation, images: !images.isEmpty) else {
            throw MacCodexDesktopError.rejected("native_input_unavailable")
        }
        let fileImages = client is MacClaudeBackground || client is MacClaudeTerminalSession
        let photoDirectory = fileImages && !images.isEmpty ? try MacClaudeBackgroundPhotos.makeDirectory() : nil
        var photoDeliveryUnknown = false
        defer {
            if let photoDirectory { MacClaudeBackgroundPhotos.remove(photoDirectory, deliveryUnknown: photoDeliveryUnknown) }
        }
        var paths: [URL] = [], digests: [String] = []
        for input in images {
            guard let url = input["url"] as? String, let comma = url.firstIndex(of: ","),
                  ["data:image/png;base64", "data:image/jpeg;base64"].contains(String(url[..<comma])),
                  let bytes = Data(base64Encoded: String(url[url.index(after: comma)...])) else { throw MacTab5Error.invalidPacket }
            if let photoDirectory {
                paths.append(try MacClaudeBackgroundPhotos.stage(bytes, in: photoDirectory))
                digests.append(MacTab5Protocol.sha256(bytes))
            } else {
                digests.append(try MacClaudeNativeContent.image(bytes))
                paths.append(try store.materializeNativePhoto(bytes))
            }
        }
        let scope=try client.scope(),original=try history(entry, client: client),before=original.turns.last?["id"] as? String ?? "",epoch=currentGeneration()
        let state = try client.state()
        let currentUser = original.accepted.first { UUID(uuidString: $0) != nil && (try? turn(entry, $0)) == before }
        let exactTurn = !expected.isEmpty && expected == before && original.turns.last?["status"] as? String == "unknown"
            && currentUser != nil
        guard operation == "send" ? state == "idle" : exactTurn && ["busy","working"].contains(state) else {
            throw MacCodexDesktopError.rejected("turn_changed")
        }
        let request=MacClaudeNativeRequest(operation:operation,text:text,paths:paths,imageDigests:digests,
            authorized:{self.authorized()&&self.currentGeneration()==epoch},
            current:{
                guard let live = self.owner(entry.session).client, live.source == client.source,
                      try live.scope() == scope, try client.scope() == scope else { return false }
                return (try self.history(entry, client: client).turns.last?["id"] as? String ?? "") == before
            }, interrupted: {
                let latest = try self.history(entry, client: client).turns.last
                return latest?["id"] as? String == expected && latest?["status"] as? String == "interrupted"
            }, expectedUser: operation == "send" ? nil : currentUser)
        var saved:Record?
        let result=try MacClaudeNativeOperation.run(client:client,request:request,history:{try self.history(entry, client: client)},dispatch:{baseline in
            guard request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
            let record=Record(task:entry.alias,operation:operation,expected:expected,baseline:baseline,confirmed:nil);try self.save(id,record);saved=record
            photoDeliveryUnknown = photoDirectory != nil
        })
        if result.user != nil || result.submissionNotInvoked { photoDeliveryUnknown = false }
        if let user=result.user,var record=saved{let alias=try turn(entry,user);record.confirmed=alias;try save(id,record);return ["turnId":alias]}
        if result.stopped,var record=saved{record.confirmed=expected;record.stopAcknowledged=true;try save(id,record);return ["turnId":expected]}
        if result.submissionNotInvoked,var record=saved {
            record.submissionNotInvoked=true
            try save(id,record)
            throw MacCodexDesktopError.rejected(client is any MacClaudeDirectSession ? "native_session_changed" : "composer_changed")
        }
        throw MacTab5Error.timeout
    }
    func receipt(_ entry:MacClaudeCatalog.Entry,_ id:String)->String? {
        do {
            guard var record=try records()[id],record.task==entry.alias,record.baseline.session==entry.session,record.submissionNotInvoked != true else{return nil}
            if let confirmed=record.confirmed{return confirmed}
            // An idle client can also mean the task completed on its own. Only
            // the confirmed interrupt response above establishes a stop.
            if record.operation=="interrupt"{return nil}
            guard let client=owner(entry.session).client,client.session==entry.session else{return nil};let value=try history(entry, client: client),scope=try client.scope()
            let confirmed=try record.baseline.confirm(value,scope:scope).map{try turn(entry,$0)}
            if let confirmed{record.confirmed=confirmed;try save(id,record)};return confirmed
        }catch{return nil}
    }
}
