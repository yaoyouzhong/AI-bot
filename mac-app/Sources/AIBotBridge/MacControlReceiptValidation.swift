import Foundation

enum MacControlReceiptValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name:String,_ ok:@autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("Control receipt: "+name) };count += 1
        }
        func row(_ sequence:UInt32 = 1,_ flags:UInt32 = 0x00C01B49) -> String {
            [sequence,flags,100,4100,UInt32.max,UInt32.max,UInt32.max,0x000100C8,300].map { String(format:"%08X",$0) }.joined()
        }
        let wire = "1;"+row()
        let parsed = MacDeviceUnlockHistory.decode(wire)
        try check("existing device history decoded without changing protocol",parsed?.count == 1 && parsed![0].contains("蓝牙") && parsed![0].contains("4100 ms") && parsed![0].contains("状态 RPC 1/200"))
        var invalidRows:[String] = ["", "1;", "2;"+row(), wire+";", "1;"+row().lowercased()]
        invalidRows.append("1;"+row(0));invalidRows.append("1;"+row(1,3))
        invalidRows.append("1;"+String(repeating:"A",count:71));invalidRows.append("1;"+String(repeating:"A",count:73))
        invalidRows.append("1;"+Array(repeating:row(),count:9).joined(separator:";"))
        for invalid in invalidRows {
            try check("malformed history rejected",MacDeviceUnlockHistory.decode(invalid) == nil)
        }
        try check("bounded full history accepted",MacDeviceUnlockHistory.decode("1;"+Array(repeating:row(),count:8).joined(separator:";"))?.count == 8)
        let computer = MacTab5Computer(backend:.init(read:{[:]},perform:{_,_,_ in false}))
        let started = ProcessInfo.processInfo.systemUptime
        computer.observe(["kind":"computer-control","op":"lock","requestId":"secret-request"],result:.init(503,["error":"command_unconfirmed","state":["locked":false]]),transport:"BLE",started:started,queued:nil)
        for _ in 0..<12 { computer.observe(["kind":"computer-control","op":"status","unlockHistory":wire],result:.init(200,["locked":false]),transport:"BLE",started:started,queued:nil) }
        try check("polls cannot erase lock command evidence",computer.diagnostic.contains("BLE · lock · 503 · command_unconfirmed") && computer.diagnostic.contains("设备 #1 蓝牙"))
        try check("request identities and raw history not retained",!computer.diagnostic.contains("secret-request") && !computer.diagnostic.contains(wire))
        computer.observe(["kind":"computer-control","op":"media","action":"play-pause","requestId":"private-request","title":"private-track"],result:.init(200,["ok":true]),transport:"USB",started:started,queued:0.02)
        try check("media action has content-free result and timing",computer.mediaDiagnostic.contains("USB · media/play-pause · 200 · ok") && computer.mediaDiagnostic.contains("排队 20"))
        computer.observe(["kind":"computer-control","op":"media","action":"private-command"],result:.error(400,"invalid_media_action"),transport:"USB",started:started,queued:nil)
        try check("media identity and invalid input never retained",!computer.mediaDiagnostic.contains("private-request") && !computer.mediaDiagnostic.contains("private-track") && !computer.mediaDiagnostic.contains("private-command") && computer.mediaDiagnostic.contains("media/invalid"))
        let diagnostics = MacTab5InteractionDiagnostics(clock:{100})
        for (body,expected) in [(["ok":true,"unlocked":true,"stage":"unlocked"] as [String:Any],"已确认解锁"),(["ok":true,"cancelled":true],"已结束"),(["ok":true,"stage":"preparing"],"preparing"),(["ok":false,"error":"unlock_offer_changed"],"unlock_offer_changed"),(["error":"secret-payload","offer":"secret-offer"],"未确认")] {
            let id = diagnostics.begin(kind:"host-unlock",operation:"poll",transport:"BLE")
            diagnostics.finish(id,status:200,body:body)
            try check("HTTP 200 retains fixed logical result",diagnostics.summary.contains("回执 "+expected))
        }
        try check("unrecognized peer text never enters diagnostics",!diagnostics.summary.contains("secret-payload") && !diagnostics.summary.contains("secret-offer"))
        let pacing:[(Double,Double,Double,Bool)] = [(10.5,10,7,true),(11,10,7,false),(10.5,10,6.5,false),
            (10.5,11,7,false),(10.5,10,11,false),(.nan,10,7,false),(10.5,-.infinity,7,false),(10.5,10,-.infinity,false)]
        for (now,control,frame,expected) in pacing {
            try check("control burst bounded by one second and four-second heartbeat fairness",MacTab5BLEControlPacing.active(now:now,lastControl:control,lastFrame:frame) == expected)
        }
        return count
    }
}
