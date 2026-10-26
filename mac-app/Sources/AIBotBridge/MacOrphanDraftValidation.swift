import Foundation

enum MacOrphanDraftValidation {
    static func run(root:URL) throws -> Int {
        var count = 0, calls = 0, listed = false, enabled = true
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("Orphan draft: " + name) }; count += 1
        }
        let task = UUID().uuidString.lowercased(), key = Data(repeating:9,count:32)
        let store = MacTab5PrivateStore(root:root,key:{key})
        let codex = MacTab5Codex(store:store,enabled:{enabled},sendEnabled:{true},call:{ _,_ in
            calls += 1
            return ["data":listed ? [["id":task,"cwd":"/fixture","name":"Fixture"]] : []]
        })
        func request(_ op:String,_ text:String = "kept fixture") -> [String:Any] {
            ["op":op,"taskId":task,"requestId":UUID().uuidString,"message":text]
        }
        func journal() throws -> [String:Any] { try MacTab5Protocol.object(store.read("journal")!) }
        try check("unknown draft saved before any catalog",codex.handle(request("draft-save"),device:"device-A").status == 200)
        try check("no IPC while saving",calls == 0)
        let saved = try journal()["drafts"] as! [String:Any]
        try check("saved under device and task",(saved["device-A|"+task] as? [String:Any])?["text"] as? String == "kept fixture")
        let disk = try FileManager.default.contentsOfDirectory(at:root,includingPropertiesForKeys:nil)
        try check("encrypted on disk",disk.count == 1 && (try Data(contentsOf:disk[0])).range(of:Data("kept fixture".utf8)) == nil)
        for op in ["draft-load","read","send","steer","interrupt"] {
            try check("unknown task cannot " + op,codex.handle(request(op),device:"device-A").status == 404)
        }
        try check("no implicit task selection or IPC",calls == 0 && !codex.contains(task))
        try check("oversized draft rejected",codex.handle(request("draft-save",String(repeating:"x",count:2001)),device:"device-A").status == 400)
        var malformed = request("draft-save");malformed["taskId"] = "not-a-task"
        try check("malformed task rejected",codex.handle(malformed,device:"device-A").status == 404)
        var image = request("draft-save");image["images"] = [UUID().uuidString]
        try check("unowned image not acknowledged",codex.handle(image,device:"device-A").status != 200)
        try check("failed saves preserve draft",((try journal())["drafts"] as! [String:Any])["device-A|"+task] as? NSDictionary == saved["device-A|"+task] as? NSDictionary)
        enabled = false
        try check("feature permission still required",codex.handle(request("draft-save"),device:"device-A").status == 403)
        enabled = true;listed = true;codex.refresh()
        try check("catalog later permits recovery",codex.handle(request("draft-load"),device:"device-A").status == 200 && (codex.snapshot()?["draftView"] as? [String:Any])?["text"] as? String == "kept fixture")
        _ = codex.handle(request("draft-load"),device:"device-B")
        try check("other device cannot load saved draft",(codex.snapshot()?["draftView"] as? [String:Any])?["text"] as? String == "")

        let full = MacTab5PrivateStore(root:root.appendingPathComponent("full"),key:{key})
        var entries:[String:Any] = [:]
        for i in 0..<128 { entries["device-A|" + (i == 0 ? task:UUID().uuidString.lowercased())] = ["text":"fixture","images":[String]()] }
        try full.write("journal",MacTab5Protocol.json(["drafts":entries,"receipts":[String:Any]()]))
        let bounded = MacTab5Codex(store:full,enabled:{true},call:{ _,_ in calls += 1;return [:] })
        var extra = request("draft-save");extra["taskId"] = UUID().uuidString
        try check("new orphan bounded",bounded.handle(extra,device:"device-A").status == 409)
        try check("existing draft can still save",bounded.handle(request("draft-save","updated fixture"),device:"device-A").status == 200)
        try check("capacity is device scoped",bounded.handle(extra,device:"device-B").status == 200)
        return count
    }
}
