import Foundation
import Darwin

enum MacLANIdentityValidation {
    private final class ResultBox {
        let lock = NSLock()
        private var missing = false
        func set(_ value: Bool) { lock.lock(); missing = value; lock.unlock() }
        var value: Bool { lock.lock(); defer { lock.unlock() }; return missing }
    }
    static func run(root: URL) throws -> Int {
        var checks = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("LAN identity: "+name) }; checks += 1
        }
        let up = UInt32(IFF_UP | IFF_RUNNING)
        func address(_ name:String,_ value:String,_ flags:UInt32? = nil) -> LocalNetworkIdentity.Address {
            .init(interface:name,value:value,flags:flags ?? up)
        }
        for invalid in ["127.0.0.1","169.254.1.2","198.18.1.2","8.8.8.8","::1","192.168.1.256","10..1.2","10.1.1.2.invalid"] {
            try check("non-LAN address excluded",LocalNetworkIdentity.select([address("en0",invalid)]) == nil)
        }
        for flags in [UInt32(0),UInt32(IFF_UP),up | UInt32(IFF_LOOPBACK),up | UInt32(IFF_POINTOPOINT)] {
            try check("inactive/loopback/tunnel excluded",LocalNetworkIdentity.select([address("utun1","10.1.2.3",flags)]) == nil)
        }
        try check("physical LAN preferred to VM bridge",LocalNetworkIdentity.select([address("bridge100","192.168.64.1"),address("en0","192.168.5.2")]) == "192.168.5.2")
        try check("private172 interface supported",LocalNetworkIdentity.select([address("en7","172.16.1.2")]) == "172.16.1.2")
        try check("private10 interface supported",LocalNetworkIdentity.select([address("en0","10.1.2.3")]) == "10.1.2.3")
        try check("local sharing bridge remains available",LocalNetworkIdentity.select([address("bridge100","192.168.64.1")]) == "192.168.64.1")
        // Exercise the actual numeric interface enumerator without depending on
        // this machine's addresses or saving any network configuration.
        for _ in 0..<20 { let value = LocalNetworkIdentity.privateIPv4(); try check("numeric enumeration returns private address or nil",value == nil || PrivateIPv4Address.isValid(value!)) }
        let entered = DispatchSemaphore(value:0), release = DispatchSemaphore(value:0), finished = DispatchSemaphore(value:0)
        let heartbeat = DispatchSemaphore(value:0), revoked = DispatchSemaphore(value:0), result = ResultBox()
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:9,count:32),confirmed:true)
        let codex = MacTab5Codex(store:.init(root:root,key:{pair.key}),enabled:{false},call:{_,_ in [:]})
        let computer = MacTab5Computer(defaults:UserDefaults(suiteName:"lan-identity-"+UUID().uuidString)!,backend:.init(read:{[:]},perform:{_,_,_ in false}))
        let voice = MacTab5Voice(enabled:{false},backend:.init(start:{_,_ in},feed:{_ in},finish:{},cancel:{}))
        let service = MacTab5Service(port:8765,host:{entered.signal();release.wait();return "192.168.5.2"},gallery:.init(directory:root),
            computer:computer,codex:codex,voice:voice,birthdays:.init(directory:root),agenda:.init(directory:root),load:{pair},save:{_ in})
        defer { service.stop() }
        service.setLANReady(true)
        DispatchQueue.global().async { result.set(service.lanConfiguration == nil); finished.signal() }
        let began = entered.wait(timeout:.now()+1) == .success
        DispatchQueue.global().async { service.usbState("fixture",confirmed:true); heartbeat.signal() }
        let progressed = heartbeat.wait(timeout:.now()+0.2) == .success
        DispatchQueue.global().async { service.setLANReady(false); revoked.signal() }
        let stopped = revoked.wait(timeout:.now()+0.2) == .success
        release.signal()
        let completed = finished.wait(timeout:.now()+1) == .success
        // Release a deliberately stalled lookup before asserting; old code must
        // fail deterministically, without leaving blocked validation workers.
        try check("fixture entered host lookup",began)
        try check("USB heartbeat progresses while address lookup stalls",progressed)
        try check("LAN revocation progresses while address lookup stalls",stopped)
        try check("lookup completes without publishing revoked endpoint",completed && result.value)
        return checks
    }
}
