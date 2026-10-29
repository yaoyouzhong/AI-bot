import Foundation

enum MacVoiceFinalPollValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ name:String,_ value:Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("Final poll: " + name) }; checks += 1
        }
        var callback:((String,Bool,MacTab5SpeechFailure?) -> Void)?
        let voice = MacTab5Voice(enabled:{true},backend:.init(start:{_,cb in callback=cb},feed:{_ in},finish:{},cancel:{},ready:{true}),usesDoubao:{true})
        func start() -> String {
            let id = voice.handle(["op":"start","source":"pc","taskId":MacTab5Voice.agendaOwner,"session":"poll-fixture"],device:"fixture").body["voiceId"] as! String
            _ = voice.handle(["op":"stop","voiceId":id,"session":"poll-fixture"],device:"fixture")
            return id
        }
        func request(_ op:String,_ id:String,_ wait:Bool = false) -> MacTab5RPCResult {
            voice.handle(["op":op,"voiceId":id,"session":"poll-fixture"],device:"fixture",waitForFinal:wait)
        }
        let first = start(), complete = callback!
        DispatchQueue.global().asyncAfter(deadline:.now()+0.02) { complete("完整结果",true,nil) }
        let result = request("poll",first,true)
        try check("committed final wakes current device poll",result.body["state"] as? String == "review" && result.body["canInsert"] as? Bool == true)
        let second = start()
        DispatchQueue.global().asyncAfter(deadline:.now()+0.02) {
            _ = voice.handle(["op":"cancel","voiceId":second,"session":"poll-fixture"],device:"fixture")
        }
        let cancelled = request("poll",second,true)
        try check("cancel wakes poll without inserting",cancelled.body["state"] as? String == "cancelled" && cancelled.body["canInsert"] as? Bool == false)
        let third = start()
        DispatchQueue.global().asyncAfter(deadline:.now()+0.02) { voice.stop() }
        try check("replaced owner cannot receive an old poll",request("poll",third,true).status == 409)
        voice.stop()
        return checks
    }
}
