import CryptoKit
import Foundation

enum MacFirmwareCommandValidation {
    static func run() throws -> Int {
        var count = 0, now:Int64 = 1_900_000_000_000, tick = 100.0
        func check(_ name:String,_ ok:Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("Firmware command: "+name) };count += 1
        }
        // A valid synthetic ESP image, never offered to or flashed on hardware.
        var bytes = Data(repeating:0,count:1072)
        bytes[0] = 0xe9;bytes[1] = 1;bytes[12] = 18;bytes[23] = 1
        func word(_ at:Int,_ value:UInt32) { for i in 0..<4 { bytes[at+i] = UInt8(truncatingIfNeeded:value>>(i*8)) } }
        word(28,1024);word(32,0xabcd5432)
        for (i,b) in "0.2.277-ui".utf8.enumerated() { bytes[48+i] = b }
        for (i,b) in "aibot_tab5".utf8.enumerated() { bytes[80+i] = b }
        bytes[1071] = bytes[32..<1056].reduce(UInt8(0xef),^)
        bytes.append(Data(SHA256.hash(data:bytes)))
        let image = try MacTab5FirmwareImage(bytes:bytes), device = "001122334455"
        func model() throws -> MacTab5Firmware {
            let value = MacTab5Firmware(epoch:{now},tick:{tick});try value.select(image);return value
        }
        func command(_ value:MacTab5Firmware,_ key:String = "otaCommand",_ frame:Int64? = nil) -> [String:Any]? {
            value.projection(frameEpochMilliseconds:frame).health[key] as? [String:Any]
        }
        var value = try model();try value.start(device:device)
        let original = command(value)!, deadline = original["expiresAt"] as! Int64
        try check("fresh frame keeps original30s deadline",command(value,"otaCommand",now)?["expiresAt"] as? Int64 == deadline)
        let capped = command(value,"otaCommand",now-20_000)!["expiresAt"] as! Int64
        try check("stale snapshot stays inside receiver45s bound",capped == now+25_000 && capped <= deadline)
        for bad in [Int64(0),-1,Int64.max,now-45_001,now+40_000] {
            try check("invalid or unusable frame clock cannot publish",command(value,"otaCommand",bad) == nil)
        }
        now += 5000;tick += 5
        try check("new status does not extend original request",command(value,"otaCommand",now)?["expiresAt"] as? Int64 == deadline)
        now = deadline+1;tick += 26
        try check("expired request is withdrawn",command(value) == nil && value.summary.contains("未确认"))
        try check("projection never creates an automatic retry",command(value) == nil)

        value = try model();try value.start(device:device)
        var ready = command(value)!;ready["kind"] = "ota-control";ready["stage"] = "ready"
        try check("ready still requires matching request",value.handle(ready,device:device,transport:"USB",binary:false,bulk:false).body["commit"] as? Bool == true)
        ready["stage"] = "started"
        try check("started acknowledgment accepted",value.handle(ready,device:device,transport:"USB",binary:false,bulk:false).status == 200)
        now += 31_000;tick += 31
        try check("started command expires without false unconfirmed message",command(value) == nil && value.summary.contains("已确认开始") && !value.summary.contains("请求未确认"))

        value = try model();try value.start(device:device)
        try check("real authenticated image handler retains existing bytes",value.bytes(sha:image.sha256) == image.bytes)
        let provided = value.summary;now += 31_000;tick += 31
        try check("download cannot be overwritten by old command expiry",command(value) == nil && value.summary == provided && value.busy)
        do { try value.select(image);throw MacTab5LocalError.invalid("Image changed during download") }
        catch MacTab5Error.busy { count += 1 }
        value.observeVersion("0.2.276-ui")
        try check("old version cannot clear active download",value.busy)
        value.observeVersion(image.version)
        try check("new version clears stale command without claiming boot verification",!value.busy && value.summary.contains("已运行") && !value.summary.contains("通过"))

        try value.beginBootCheck(device:device,transport:"USB")
        let boot = command(value,"bootVerifyCommand",now-7000)!
        try check("boot request stays inside receiver15s bound",boot["expiresAt"] as? Int64 == now+8000)
        now += 10_001;tick += 10.001
        try check("boot request deadline never extends",command(value,"bootVerifyCommand",now) == nil && value.summary.contains("超时"))
        try value.beginBootCheck(device:device,transport:"USB")
        let hellos:[[String:Any]] = [100,110,120].map { uptime in
            ["type":"tab5_hello","version":1,"deviceId":device,"firmware":image.version,"uptimeMs":uptime,"uiAgeMs":10,"flushCount":3,"flushErrors":0]
        }
        let diagnostic:[String:Any] = ["type":"tab5_ota_diagnostic","firmware":image.version,"elfSha256":image.elfSHA256,"stateError":0,"state":2,"partition":"ota_0","address":0x20000]
        try value.completeUSBCheck(hellos:hellos,diagnostic:diagnostic,device:device)
        let verified = value.summary;value.observeVersion(image.version)
        try check("later version sample preserves exact boot result",verified.contains("核验通过") && value.summary == verified)
        value = try model();try value.start(device:device,reinstall:true)
        let reinstall = command(value)!, offer = value.projection().offer!
        try check("explicit reinstall renews offer identity",offer["offerId"] as? String != image.offerID && reinstall["offerId"] as? String == offer["offerId"] as? String)
        value.observeVersion(image.version)
        try check("unchanged version cannot consume reinstall command",command(value)?["requestId"] as? String == reinstall["requestId"] as? String && value.busy)
        try check("same bytes still served",value.beginRanges(sha:image.sha256))
        value.observeDeviceOTA("1,1,0,1");value.observeVersion(image.version)
        try check("unchanged version cannot finish transfer",value.busy && !value.summary.contains("已运行"))
        now += 31_000;tick += 31;_ = value.projection()
        value.observeDeviceOTA("1,1,0,0")
        try check("range idle receipt after command expiry clears lease without success",!value.busy && value.summary.contains("尚未确认"))
        try value.start(device:device,reinstall:true)
        try check("next deliberate reinstall has new IDs",command(value)?["requestId"] as? String != reinstall["requestId"] as? String && value.projection().offer?["offerId"] as? String != offer["offerId"] as? String)
        var stale = reinstall;stale["kind"] = "ota-control";stale["stage"] = "ready"
        try check("previous reinstall cannot commit again",value.handle(stale,device:device,transport:"USB",binary:false,bulk:false).status == 409)
        for version in ["0.2.285-ui","0.2.286","286","0.3.286-ui","bad"] { try check("old or unknown reinstall capability",!MacTab5Service.supportsReinstall(version)) }
        try check("capable reinstall peer",MacTab5Service.supportsReinstall("0.2.286-ui"))
        for transport in ["Wi-Fi", "BLE", "USB"] {
            value = try model(); try value.start(device: device, reinstall: true)
            try check("no automatic check before delivery", !value.beginAutomaticBootCheck(device: device, transport: transport))
            let selected = value.selected!
            let range: [String: Any] = ["kind":"ota", "offerId":selected.offerID,"sha256":image.sha256,"offset":0,"count":image.bytes.count]
            try check("entire image served for automatic check", value.handle(range,device:device,transport:transport,binary:true,bulk:true).status == 200)
            tick += 39; now += 39000
            try check("healthy startup wait does not report success", !value.beginAutomaticBootCheck(device:device,transport:transport) && !value.summary.contains("通过"))
            tick += 1; now += 1000
            try check("automatic check uses available transport", value.beginAutomaticBootCheck(device:device,transport:transport))
            try check("in-flight check is not duplicated", !value.beginAutomaticBootCheck(device:device,transport:transport))
            do { try value.start(device:device,reinstall:true); throw MacTab5LocalError.invalid("Upgrade overlapped boot check") }
            catch MacTab5Error.busy { count += 1 }
            var old = hellos[0]; old["uptimeMs"] = 999999999
            if transport == "USB" {
                do { try value.completeUSBCheck(hellos:[old,old,old],diagnostic:diagnostic,device:device); throw MacTab5LocalError.invalid("Old boot passed") }
                catch MacTab5Error.unconfirmed { count += 1 }
            } else {
                var reply = command(value,"bootVerifyCommand")!;reply["kind"]="boot-verify";reply["hello"]=old;reply["diagnostic"]=diagnostic
                try check("same image before restart cannot pass", value.handle(reply,device:device,transport:transport,binary:false,bulk:false).status == 400)
            }
            tick += 15; now += 15000
            try check("read-only boot retry after failed sample", value.beginAutomaticBootCheck(device:device,transport:transport))
            if transport == "USB" { try value.completeUSBCheck(hellos:hellos,diagnostic:diagnostic,device:device) }
            else {
                for (index,hello) in hellos.enumerated() {
                    var reply = command(value,"bootVerifyCommand")!;reply["kind"]="boot-verify";reply["hello"]=hello;reply["diagnostic"]=diagnostic
                    try check("wireless sample accepted", value.handle(reply,device:device,transport:transport,binary:false,bulk:false).status == 200)
                    try check("three fresh samples required", value.summary.contains("核验通过") == (index == 2))
                }
            }
            tick += 30; now += 30000
            try check("verified check ends automatic polling", value.summary.contains("核验通过") && !value.beginAutomaticBootCheck(device:device,transport:transport) && !value.busy)
        }
        value = try model();try value.start(device:device,reinstall:true)
        let pendingImage = value.selected!
        _ = value.handle(["kind":"ota","offerId":pendingImage.offerID,"sha256":image.sha256,"offset":0,"count":image.bytes.count],device:device,transport:"Wi-Fi",binary:true,bulk:true)
        tick += 40;now += 40000
        for _ in 0..<6 {
            try check("bounded automatic boot attempt",value.beginAutomaticBootCheck(device:device,transport:"Wi-Fi"))
            tick += 15;now += 15000
        }
        try check("unreachable device never claims success or retries upgrade",!value.beginAutomaticBootCheck(device:device,transport:"Wi-Fi") && value.summary.contains("未完成") && command(value)==nil)
        for error in ["device_busy","draft_pending","draft_save_failed","no_link","offer_changed","unknown-private-content"] {
            let rejected = try model();try rejected.start(device:device)
            var root = command(rejected)!;root["kind"] = "ota-control";root["stage"] = "rejected";root["error"] = error
            try check("bounded rejection receipt accepted",rejected.handle(root,device:device,transport:"USB",binary:false,bulk:false).status == 200 && command(rejected) == nil)
            try check("raw remote error never appears in UI",!rejected.summary.contains(error))
        }
        return count
    }
}
