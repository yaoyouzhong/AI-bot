import Foundation

final class MacClaudeNativeBackend {
    private struct Record: Codable {
        let task:String,operation:String,expected:String,baseline:MacClaudeNativeReceipt
        var confirmed:String?
        var submissionNotInvoked:Bool?
        var stopAcknowledged:Bool?
    }
    private let root:URL,store:MacTab5PrivateStore,key:()throws->Data
    private let factory:(UUID)->(any MacClaudeNativeSession)?
    private let gate=NSLock(),recordsGate=NSLock(),generationGate=NSLock()
    private var generation=0
    var authorized:()->Bool={false}
    init(root:URL,store:MacTab5PrivateStore,key:@escaping ()throws->Data,factory:@escaping (UUID)->(any MacClaudeNativeSession)?=MacClaudeNativeClient.find) {
        self.root=root;self.store=store;self.key=key;self.factory=factory
    }
    private func history(_ entry:MacClaudeCatalog.Entry)throws->MacClaudeHistory.Value {
        let secret=try key();guard secret.count==32 else{throw MacTab5Error.keychain}
        return try MacClaudeHistory.read(root:root,session:entry.session,key:secret){
            (self.factory(entry.session) as? MacClaudeNativeClient)?.isNewPanel() == true
        }
    }
    private func turn(_ entry:MacClaudeCatalog.Entry,_ user:String)throws->String{try MacClaudeCatalog.alias("claude:turn:"+entry.session.uuidString.lowercased()+":"+user,key:key())}
    private func records()throws->[String:Record]{guard let bytes=try store.read("native-requests") else{return [:]};return try JSONDecoder().decode([String:Record].self,from:bytes)}
    private func save(_ id:String,_ record:Record)throws{recordsGate.lock();defer{recordsGate.unlock()};var values=try records();guard values.count<512||values[id] != nil else{throw MacTab5Error.unavailable};values[id]=record;try store.write("native-requests",JSONEncoder().encode(values))}
    func stop(){generationGate.lock();generation += 1;generationGate.unlock()}
    private func currentGeneration()->Int{generationGate.lock();defer{generationGate.unlock()};return generation}
    func thread(_ entry:MacClaudeCatalog.Entry,includeHistory:Bool)throws->[String:Any] {
        var turns:[[String:Any]]=[];var limited=false
        if includeHistory{let value=try history(entry);turns=value.turns;limited=value.limited}
        let client=factory(entry.session),state=(try? client?.state()) ?? "unknown",running=["busy","working","waiting"].contains(state),waiting=state=="waiting"
        if running,!turns.isEmpty{turns[turns.count-1]["status"]="inProgress"}
        if !turns.isEmpty {
            let stopped=Set(try records().values.compactMap { record -> String? in
                guard record.task==entry.alias,record.operation=="interrupt",record.baseline.session==entry.session,
                      record.stopAcknowledged==true,record.confirmed==record.expected else{return nil}
                return record.confirmed
            })
            for index in turns.indices where stopped.contains(turns[index]["id"] as? String ?? "") {
                turns[index]["status"]="interrupted"
            }
        }
        let status:[String:Any]=running ? ["type":"active","activeFlags":waiting ? ["waitingOnApproval"]:[String]()] : state=="idle" ? ["type":"idle"]:["type":"notLoaded"]
        return ["id":entry.alias,"turns":turns,"historyLimited":limited,"status":status,"controlUnavailable":client?.hasSessionAPI != true || !["idle","busy","working","waiting"].contains(state)]
    }
    func selection(_ entries:[MacClaudeCatalog.Entry],bound:MacClaudeIdentity.Selection?,now:Date)->MacClaudeIdentity.Selection? {
        let live=MacClaudeNativeClient.live()
        let observed=entries.filter{live.contains($0.session)}.compactMap { entry -> MacClaudeIdentity.Selection? in
            guard let client=factory(entry.session),let state=try? client.state(),let value=try? history(entry),let latest=value.turns.last,let alias=latest["id"] as? String,
                  let raw=value.accepted.first(where:{(try? turn(entry,$0))==alias}),let prompt=UUID(uuidString:raw) else{return nil}
            let running=["busy","working","waiting"].contains(state),outcome=running ? "inProgress":latest["status"] as? String ?? "unknown"
            let at=(latest["endedAt"] as? Int64 ?? 0)>0 ? latest["endedAt"] as? Int64 ?? 0:latest["startedAt"] as? Int64 ?? 0
            return MacClaudeIdentity.Selection(sessionID:entry.session,promptID:prompt,activity:running ? now:Date(timeIntervalSince1970:Double(at)),state:state=="waiting" ? "working":running ? "working":state=="idle" ? "idle":"unknown",outcome:outcome,needsInput:state=="waiting",completionAt:outcome=="completed" ? at:0,completionSequence:0,ownedTurn:alias)
        }
        return MacClaudeIdentity.resolve(bound:bound,owned:observed)
    }
    func call(_ method:String,entry:MacClaudeCatalog.Entry,params:[String:Any])throws->[String:Any] {
        gate.lock();defer{gate.unlock()}
        guard let id=MacTab5Codex.id(params["requestId"]),["turn/start","turn/steer","turn/interrupt"].contains(method),authorized() else{throw MacTab5Error.unconfirmed}
        if let existing=try records()[id] {guard existing.task==entry.alias else{throw MacTab5Error.unconfirmed};if existing.submissionNotInvoked==true{throw MacCodexDesktopError.rejected("composer_changed")};if let turn=receipt(entry,id){return ["turnId":turn]};throw MacTab5Error.timeout}
        guard let client=factory(entry.session),client.session==entry.session else{throw MacCodexDesktopError.rejected("native_session_unavailable")}
        guard client.hasSessionAPI else{throw MacCodexDesktopError.rejected("native_session_api_unavailable")}
        let operation=method=="turn/start" ? "send":method=="turn/steer" ? "steer":"interrupt"
        let inputs=params["input"] as? [[String:Any]] ?? [],text=inputs.filter{$0["type"] as? String=="text"}.compactMap{$0["text"] as? String}.joined(separator:"\n")
        var paths:[URL]=[],digests:[String]=[]
        for input in inputs where input["type"] as? String=="image" {
            guard paths.count<3,let url=input["url"] as? String,let comma=url.firstIndex(of:","),["data:image/png;base64","data:image/jpeg;base64"].contains(String(url[..<comma])),let bytes=Data(base64Encoded:String(url[url.index(after:comma)...])) else{throw MacTab5Error.invalidPacket}
            digests.append(try MacClaudeNativeContent.image(bytes));paths.append(try store.materializeNativePhoto(bytes))
        }
        let scope=try client.scope(),original=try history(entry),before=original.turns.last?["id"] as? String ?? "",expected=params[operation=="interrupt" ? "turnId":"expectedTurnId"] as? String ?? "",epoch=currentGeneration()
        guard operation=="send" || (!expected.isEmpty && expected==before && ["busy","working"].contains(try client.state())) else{throw MacCodexDesktopError.rejected("turn_changed")}
        let request=MacClaudeNativeRequest(operation:operation,text:text,paths:paths,imageDigests:digests,
            authorized:{self.authorized()&&self.currentGeneration()==epoch},
            current:{
                guard try client.scope()==scope else{return false}
                return (try self.history(entry).turns.last?["id"] as? String ?? "")==before
            })
        var saved:Record?
        let result=try MacClaudeNativeOperation.run(client:client,request:request,history:{try self.history(entry)},dispatch:{baseline in
            guard request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
            let record=Record(task:entry.alias,operation:operation,expected:expected,baseline:baseline,confirmed:nil);try self.save(id,record);saved=record
        })
        if let user=result.user,var record=saved{let alias=try turn(entry,user);record.confirmed=alias;try save(id,record);return ["turnId":alias]}
        if result.stopped,var record=saved{record.confirmed=expected;record.stopAcknowledged=true;try save(id,record);return ["turnId":expected]}
        if result.submissionNotInvoked,var record=saved {
            record.submissionNotInvoked=true
            try save(id,record)
            throw MacCodexDesktopError.rejected("composer_changed")
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
            guard let client=factory(entry.session),client.session==entry.session else{return nil};let value=try history(entry),scope=try client.scope()
            let confirmed=try record.baseline.confirm(value,scope:scope).map{try turn(entry,$0)}
            if let confirmed{record.confirmed=confirmed;try save(id,record)};return confirmed
        }catch{return nil}
    }
}
