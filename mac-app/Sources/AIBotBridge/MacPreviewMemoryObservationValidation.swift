import Foundation

enum MacPreviewMemoryObservationValidation {
    static func run() throws -> Int {
        var model = MacPreviewMemoryObservation(), count = 0
        func check(_ label: String, _ condition: Bool) throws {
            guard condition else { throw MacTab5Error.invalidPacket }; count += 1
        }
        func sample(_ job: Int, _ phase: Int, time: Int = 1000, firmware: String = "0.2.275-ui") -> [String: Any] {
            ["uptimeMs":time,"firmware":firmware,"displayDiag":"legacy,1;sp,1,0,\(job),11760,\(phase);op,1,0"]
        }
        try check("unknown before USB sample",model.summary(device:"device",host:"host") == nil)
        model.observe(sample(1,1),device:"device",host:"host")
        try check("phase 1 retained",model.summary(device:"device",host:"host")?.contains("阶段 1（原始像素）") == true)
        model.observe(sample(1,1),device:"device",host:"host")
        try check("repeated ACK deduplicated",model.summary(device:"device",host:"host")?.contains("采样 1 次") == true)
        model.observe(sample(2,0,time:1100),device:"device",host:"host")
        try check("success cannot erase failure",model.summary(device:"device",host:"host")?.contains("最近任务 1") == true)
        model.observe(sample(3,2,time:1200),device:"device",host:"host")
        try check("new output failure counted",model.summary(device:"device",host:"host")?.contains("采样 2 次，最近任务 3，阶段 2") == true)
        model.observe(sample(4,3,time:1300),device:"device",host:"host")
        try check("codec phase fixed label",model.summary(device:"device",host:"host")?.contains("阶段 3（JPEG 内部内存）") == true)
        for text in ["sp,1,0,5,1000,4", "sp,1,2,5,1000,1", "sp,1,0,5,1000,-1", "sp,1,0,5,1000,+1", "sp,1,0,4294967296,1000,1", "sp,1,0,5,1000,1,private", "sp,2,0,5,1000,1", String(repeating:"x",count:1025)] {
            model.observe(["uptimeMs":1400,"firmware":"0.2.275-ui","displayDiag":text],device:"device",host:"host")
            try check("malformed sample cannot replace evidence",model.summary(device:"device",host:"host")?.contains("最近任务 4") == true)
        }
        try check("foreign owner cannot display",model.summary(device:"device",host:"another") == nil)
        let body = model.summary(device:"device",host:"host")!
        try check("no caller identifiers",!body.contains("device") && !body.contains("host") && !body.contains("legacy") && !body.contains("private"))
        model.observe(sample(1,0,time:10),device:"device",host:"host")
        try check("USB reboot clears prior failure",model.summary(device:"device",host:"host") == nil)
        model.observe(sample(2,1,time:20),device:"device",host:"host")
        model.observe(sample(3,0,time:30,firmware:"0.2.276-ui"),device:"device",host:"host")
        try check("firmware change clears prior failure",model.summary(device:"device",host:"host") == nil)
        model.observe(sample(4,1,time:40),device:"device",host:"host")
        model.observe(sample(5,0,time:50),device:"other",host:"host")
        try check("device change clears prior evidence",model.summary(device:"other",host:"host") == nil)
        return count
    }
}
