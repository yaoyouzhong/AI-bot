import Foundation

enum MacPreviewUSBPacingValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ name:String,_ ok:Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("Preview USB pacing: "+name) }; checks += 1
        }
        var now = 0.0, attempts = 0
        let fast = MacPreviewUSBPacing.run(active:true,clock:{now},mayContinue:{true}) {
            attempts += 1; now += 0.002; return true
        }
        try check("fast work yields by request count",fast == 4 && attempts == 4)
        attempts = 0; now = 0
        let slow = MacPreviewUSBPacing.run(active:true,clock:{now},mayContinue:{true}) {
            attempts += 1; now += 0.04; return true
        }
        try check("slow work yields by elapsed budget",slow == 2 && attempts == 2)
        attempts = 0
        let blocked = MacPreviewUSBPacing.run(active:true,clock:{now},mayContinue:{true}) {
            attempts += 1; now += 5; return true
        }
        try check("one stalled request cannot multiply",blocked == 1 && attempts == 1)
        attempts = 0
        let idle = MacPreviewUSBPacing.run(active:false,clock:{0},mayContinue:{true}) { attempts += 1; return true }
        try check("closed viewer keeps original single pump",idle == 1 && attempts == 1)
        attempts = 0
        let empty = MacPreviewUSBPacing.run(active:true,clock:{0},mayContinue:{true}) { attempts += 1; return false }
        try check("empty or completed ID stops without busy poll",empty == 0 && attempts == 1)
        attempts = 0
        let drained = MacPreviewUSBPacing.run(active:true,clock:{0},mayContinue:{true}) { attempts += 1; return attempts == 1 }
        try check("new empty mailbox stops burst",drained == 1 && attempts == 2)
        for reason in ["voice priority","status due","owner changed","disabled","configuration","viewer closed"] {
            attempts = 0
            let count = MacPreviewUSBPacing.run(active:true,clock:{0},mayContinue:{false}) { attempts += 1; return true }
            try check("yields after first exchange for "+reason,count == 1 && attempts == 1)
        }
        enum Failure:Error { case serial }
        attempts = 0
        do {
            _ = try MacPreviewUSBPacing.run(active:true,clock:{0},mayContinue:{true}) { attempts += 1; throw Failure.serial }
            throw MacTab5LocalError.invalid("Preview pacing swallowed error")
        } catch Failure.serial { try check("serial failure propagates without replay",attempts == 1) }

        let one:[String:Any] = ["transport":"USB","requestId":String(repeating:"1",count:32)]
        let two:[String:Any] = ["transport":"USB","requestId":String(repeating:"2",count:32)]
        func root(_ command:[String:Any]?) -> [String:Any] {
            ["data":["connectionHealth":["screenPreviewCommand":command.map{$0 as Any} ?? NSNull()]]]
        }
        var publication = MacPreviewUSBPublication()
        try check("no preview adds no urgent status",!publication.isDue(nil,voicePriority:false))
        try check("new request skips normal cadence",publication.isDue(one,voicePriority:false))
        try check("voice defers preview status",!publication.isDue(one,voicePriority:true))
        publication.acknowledge(root(one))
        try check("acknowledged request adds no status traffic",!publication.isDue(one,voicePriority:false))
        try check("newer request remains urgent during old ACK",publication.isDue(two,voicePriority:false))
        publication.acknowledge(root(one))
        try check("old actual frame ACK cannot acknowledge newer request",publication.isDue(two,voicePriority:false))
        publication.acknowledge(root(two))
        try check("new exact ACK completes urgency",!publication.isDue(two,voicePriority:false))
        try check("Wi-Fi preview does not accelerate USB",!publication.isDue(["transport":"Wi-Fi","requestId":String(repeating:"3",count:32)],voicePriority:false))
        try check("invalid IDs cannot accelerate status",!publication.isDue(["transport":"USB","requestId":"invalid"],voicePriority:false))
        publication = .init()
        try check("reconnection republishes current request",publication.isDue(two,voicePriority:false))
        publication.acknowledge(root(nil))
        try check("omitted command cannot mark a pending request published",publication.isDue(two,voicePriority:false))
        var cadence = MacPreviewUSBPollCadence()
        func interval(_ now:Double,_ active:Bool = true,_ voice:Bool = false,_ busy:Bool = false) -> Int {
            cadence.milliseconds(at:now,active:active,voicePriority:voice,foregroundBusy:busy)
        }
        try check("idle viewer never accelerates polling",interval(10) == 100)
        cadence.completed(at:10)
        try check("real completion enables next-range scheduling",interval(10) == 10)
        try check("short peer scheduling delay stays fast",interval(10.249) == 10)
        try check("empty polls cannot extend progress window",interval(10.25) == 100)
        try check("encoding uses ordinary cadence",interval(13) == 100)
        try check("closed viewer immediately returns idle cadence",interval(10.1,false) == 100)
        try check("voice immediately returns original cadence",interval(10.1,true,true) == 100)
        try check("foreground work returns original cadence",interval(10.1,true,false,true) == 100)
        try check("clock regression cannot prolong fast polling",interval(9.9) == 100)
        cadence = .init()
        try check("reconnect clears progress acceleration",interval(10.1) == 100)
        return checks
    }
}
