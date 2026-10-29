import Foundation

enum MacDoubaoValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name:String,_ value:@autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Doubao: "+name) }; count += 1
        }
        var silenceEvidence = MacDoubaoSilenceEvidence()
        try check("missing stream is not verified silenceEvidence", !silenceEvidence.verified)
        silenceEvidence.append([Int16](repeating:0,count:1600))
        try check("digital silenceEvidence verified", silenceEvidence.verified)
        try check("digital silenceEvidence exits after IO stop", MacDoubaoSilenceEvidence.noSpeech(capture:false,held:false,marked:false,text:"",elapsed:0.7,digitalSilence:silenceEvidence.verified))
        try check("marked composition cannot be discarded", !MacDoubaoSilenceEvidence.noSpeech(capture:false,held:false,marked:true,text:"",elapsed:6,digitalSilence:true))
        try check("unknown IO cannot end silenceEvidence", !MacDoubaoSilenceEvidence.noSpeech(capture:nil,held:false,marked:false,text:"",elapsed:6,digitalSilence:true))
        silenceEvidence.append([1])
        try check("even quiet audio retains ordinary recognition window", !silenceEvidence.verified && !MacDoubaoSilenceEvidence.noSpeech(capture:false,held:false,marked:false,text:"",elapsed:0.7,digitalSilence:silenceEvidence.verified))
        try check("noisy or physical audio retains timeout", MacDoubaoSilenceEvidence.noSpeech(capture:false,held:false,marked:false,text:"",elapsed:5,digitalSilence:false))
        var final = MacDoubaoCompletion()
        final.update("公开测试",committed:true,now:100)
        try check("committed text before stop cannot finalize",final.final(capture:false,marked:false,now:102) == nil)
        final.stopped = true
        try check("active capture cannot finalize",final.final(capture:true,marked:false,now:102) == nil)
        try check("unknown capture cannot finalize",final.final(capture:nil,marked:false,now:102) == nil)
        try check("marked input cannot finalize",final.final(capture:false,marked:true,now:102) == nil)
        try check("quiet period required",final.final(capture:false,marked:false,now:100.5) == nil)
        try check("stopped committed quiet text finalizes",final.final(capture:false,marked:false,now:101) == "公开测试")
        final.update("临时文字",committed:false,now:102)
        try check("new uncommitted input invalidates older commit",final.final(capture:false,marked:false,now:104) == nil)
        final.update(String(repeating:"a",count:2001),committed:true,now:105)
        try check("oversized committed text rejected",final.final(capture:false,marked:false,now:107) == nil)
        var drain = MacDoubaoDrain()
        try check("pending audio cannot release Fn",!drain.ready(drained:false,tail:0.3,now:1))
        try check("queue drain starts tail interval",!drain.ready(drained:true,tail:0.3,now:2))
        try check("output latency must elapse",!drain.ready(drained:true,tail:0.3,now:2.2))
        try check("drained tail may release Fn",drain.ready(drained:true,tail:0.3,now:2.4))
        _ = drain.ready(drained:false,tail:0.3,now:3)
        try check("new pending audio resets tail",!drain.ready(drained:true,tail:0.3,now:3.5))
        var callback: ((String,Bool,MacTab5SpeechFailure?) -> Void)?, now = 100.0, stopped = 0, captureReady = false
        let voice = MacTab5Voice(enabled:{ true },clock:{ now },backend:.init(start:{ _,value in callback = value },feed:{ _ in },finish:{ stopped += 1 },cancel:{},ready:{captureReady}),usesDoubao:{ true })
        let start = voice.handle(["op":"start","source":"pc","taskId":MacTab5Voice.agendaOwner,"session":"test-session"],device:"fixture")
        try check("shortcut dispatch does not claim audio readiness",start.body["ready"] as? Bool == false)
        let id = start.body["voiceId"] as! String
        func request(_ op:String) -> MacTab5RPCResult { voice.handle(["op":op,"voiceId":id,"session":"test-session"],device:"fixture") }
        captureReady = true
        try check("actual capture readiness reaches device",request("poll").body["ready"] as? Bool == true)
        callback?("测试草稿",false,nil)
        try check("partial draft cannot insert",request("poll").body["canInsert"] as? Bool == false)
        _ = request("stop")
        try check("stopped capture never remains ready",request("poll").body["ready"] as? Bool == false)
        try check("explicit stop releases capture once",stopped == 1)
        now += 20
        try check("confirmation wait exceeds native recognizer timeout",request("poll").body["state"] as? String == "recognizing")
        callback?("已确认的测试草稿",true,nil)
        try check("confirmed draft may be reviewed",request("poll").body["canInsert"] as? Bool == true)
        _ = request("cancel")
        callback?("迟到文字",true,nil)
        let cancelled = request("poll")
        try check("cancel discards late result",cancelled.body["text"] as? String == "" && cancelled.body["state"] as? String == "cancelled")
        _ = voice.handle(["op":"start","source":"pc","taskId":MacTab5Voice.agendaOwner,"session":"test-session"],device:"fixture")
        try check("next attempt retains previous diagnostics without transcript or owner",voice.diagnostics.contains("此前语音") && !voice.diagnostics.contains("测试草稿") && !voice.diagnostics.contains("test-session") && !voice.diagnostics.contains(id))
        voice.stop()
        var inputReady = false, forwarded:[[Int16]] = [], audioCancelled = 0, audioFinished = 0
        var audioNow = 100.0, audioCallback:((String,Bool,MacTab5SpeechFailure?) -> Void)?
        let audioVoice = MacTab5Voice(enabled:{true},clock:{audioNow},backend:.init(start:{_,callback in audioCallback = callback},feed:{forwarded.append($0)},finish:{audioFinished += 1},cancel:{audioCancelled += 1},ready:{inputReady}),usesDoubao:{true})
        func audioStart() -> String {
            audioVoice.handle(["op":"start","source":"tab5","taskId":MacTab5Voice.agendaOwner,"session":"audio-fixture"],device:"fixture").body["voiceId"] as! String
        }
        func audioRequest(_ id:String,_ op:String,seq:Int = 0,bytes:Data = Data([1,0])) -> MacTab5RPCResult {
            audioVoice.handle(["op":op,"voiceId":id,"session":"audio-fixture","seq":seq,"pcm":bytes.base64EncodedString()],device:"fixture")
        }
        let earlyID = audioStart(), early = audioRequest(earlyID,"audio")
        try check("confirmed device audio permits capture while recognizer prepares",early.status == 200 && early.body["ready"] as? Bool == true && early.body["silent"] as? Bool == false && forwarded.isEmpty)
        _ = audioRequest(earlyID,"audio")
        _ = audioRequest(earlyID,"audio",seq:1,bytes:Data([2,0]))
        inputReady = true
        try check("only verified IO receives retained preparation audio",audioRequest(earlyID,"poll").body["ready"] as? Bool == true && forwarded == [[1],[2]])
        _ = audioRequest(earlyID,"audio",seq:2,bytes:Data([3,0]))
        try check("first ready audio is forwarded intact",forwarded == [[1],[2],[3]])
        inputReady = false
        _ = audioRequest(earlyID,"audio",seq:3,bytes:Data([4,0]))
        _ = audioRequest(earlyID,"audio",seq:4,bytes:Data([5,0]))
        try check("readiness loss after recording retains audio",forwarded == [[1],[2],[3]])
        inputReady = true
        _ = audioRequest(earlyID,"poll")
        _ = audioRequest(earlyID,"audio",seq:4,bytes:Data([5,0]))
        try check("recovery and duplicate packets preserve each recording sample once",forwarded == [[1],[2],[3],[4],[5]])
        _ = audioRequest(earlyID,"cancel"); inputReady = false; forwarded = []
        let cancelledAudioID = audioStart(); _ = audioRequest(cancelledAudioID,"audio"); _ = audioRequest(cancelledAudioID,"cancel")
        inputReady = true; _ = audioRequest(cancelledAudioID,"poll")
        try check("cancelled preparation discards queued audio",forwarded.isEmpty)
        inputReady = false
        let stoppedAudioID = audioStart(); _ = audioRequest(stoppedAudioID,"audio")
        let earlyStop = audioRequest(stoppedAudioID,"stop")
        try check("early stop retains audio without claiming final text",earlyStop.body["state"] as? String == "recognizing" && earlyStop.body["canInsert"] as? Bool == false && forwarded.isEmpty && audioFinished == 0)
        inputReady = true; _ = audioRequest(stoppedAudioID,"poll")
        try check("early stop drains retained audio before finishing once",forwarded == [[1]] && audioFinished == 1)
        _ = audioRequest(stoppedAudioID,"poll")
        try check("recognition polls cannot finish twice",audioFinished == 1)
        audioCallback?("完整短句",true,nil)
        try check("early stop inserts only a confirmed complete result",audioRequest(stoppedAudioID,"poll").body["canInsert"] as? Bool == true)
        inputReady = false; forwarded = []
        let noAudioID = audioStart()
        try check("stop with no physical audio cannot claim recording",audioRequest(noAudioID,"stop").body["state"] as? String == "error")
        let timeoutID = audioStart(); _ = audioRequest(timeoutID,"audio"); _ = audioRequest(timeoutID,"stop")
        audioNow += 11
        try check("stopped preparation has a bounded deadline",audioRequest(timeoutID,"poll").body["state"] as? String == "error" && forwarded.isEmpty)
        inputReady = true; _ = audioRequest(timeoutID,"poll")
        try check("timed out audio is never replayed",forwarded.isEmpty)
        inputReady = false
        let prematureID = audioStart(); _ = audioRequest(prematureID,"audio")
        audioCallback?("不完整文字",true,nil)
        let premature = audioRequest(prematureID,"poll")
        try check("final before queued audio drains cannot insert",premature.body["state"] as? String == "error" && premature.body["canInsert"] as? Bool == false)
        let fullID = audioStart(), silence = Data(repeating:0,count:6400)
        for seq in 0..<40 { _ = audioRequest(fullID,"audio",seq:seq,bytes:silence) }
        try check("preparation retains at most eight seconds",audioRequest(fullID,"poll").body["state"] as? String == "recording" && forwarded.isEmpty)
        try check("initial preparation overflow fails without partial text",audioRequest(fullID,"audio",seq:40,bytes:silence).body["state"] as? String == "error" && forwarded.isEmpty)
        let recoveryID = audioStart(); inputReady = true; _ = audioRequest(recoveryID,"audio")
        forwarded = []; inputReady = false
        for seq in 1...40 { _ = audioRequest(recoveryID,"audio",seq:seq,bytes:silence) }
        try check("recording recovery buffer remains bounded",audioRequest(recoveryID,"audio",seq:41,bytes:silence).body["state"] as? String == "error" && forwarded.isEmpty && audioCancelled > 0)
        audioVoice.stop()
        // Independently generated with system OpenSSL DES-ECB, using synthetic data.
        let challenge = Data(0..<16), password = Data("password".utf8)
        let response = try MacVNCConnectionCheck.response(challenge:challenge,password:password)
        try check("VNC challenge known answer",response.map { String(format:"%02x",$0) }.joined() == "b866924125c8eebb9debc1db61c538e2")
        var header = Data([0x04,0,0x03,0]); header.append(Data(repeating:0,count:16)); header.append(contentsOf:[0,0,0,7])
        let initialization = header+Data("fixture".utf8)
        func negotiate(_ tail:Data,_ methods:Data = Data([1,2])) throws -> [Data] {
            let bytes = Data("RFB 003.889\n".utf8)+methods+tail
            var offset = 0, writes:[Data] = []
            _ = try MacVNCConnectionCheck.negotiate(password:password,read:{ count in
                guard offset+count <= bytes.count else { throw MacTab5Error.invalidPacket }
                defer { offset += count }; return bytes.subdata(in:offset..<offset+count)
            },send:{ writes.append($0) })
            return writes
        }
        let writes = try negotiate(challenge+Data(repeating:0,count:4)+initialization)
        try check("connection check sends authentication and shared-init only",writes == [Data("RFB 003.008\n".utf8),Data([2]),response,Data([1])])
        do { _ = try negotiate(Data(),Data([1,1])); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { count += 1 }
        do { _ = try negotiate(challenge+Data([0,0,0,1])); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { count += 1 }
        do { _ = try MacVNCConnectionCheck.response(challenge:challenge,password:Data("123456789".utf8)); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { count += 1 }
        let attempt = MacVNCCheckAttempt()
        let first = attempt.begin(password:password,delay:15,now:100)
        try check("delayed check cannot connect before countdown",attempt.take(first,now:114.9) == nil)
        try check("delayed check can consume password at deadline",attempt.take(first,now:115) == password)
        try check("delayed check cannot consume password twice",attempt.take(first,now:115) == nil)
        attempt.cancel()
        try check("cancelled connection cannot publish a late result",!attempt.finish(first))
        let cancelledID = attempt.begin(password:password,delay:15,now:100)
        attempt.cancel()
        try check("cancelled countdown never connects",attempt.take(cancelledID,now:115) == nil)
        let expired = attempt.begin(password:password,delay:15,now:100)
        try check("expired countdown discards pending secret",attempt.take(expired,now:126) == nil && !attempt.isCurrent(expired))
        let next = attempt.begin(password:password,delay:0,now:200)
        try check("old completion cannot finish replacement",!attempt.finish(expired) && attempt.isCurrent(next))
        try check("replacement retains only its own attempt",attempt.take(next,now:200) == password && attempt.finish(next))
        do { _ = try MacVNCConnectionCheck.check(password:password,isCancelled:{ true }); throw MacTab5Error.invalidPacket }
        catch is MacTab5LocalError { count += 1 }
        return count
    }
}
