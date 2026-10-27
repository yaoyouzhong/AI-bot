import Foundation
import Darwin

enum MacESPDiscoveryValidation {
    private static let token = "synthetic-pairing-token-for-discovery-test"
    private static let nonce = "0123456789abcdef"
    private static let request = Data("AIBOT_DISCOVER_V1|0123456789abcdef|ea3467b6f675486fbde26f6fe222a9e7c2676d018cb8a0695d22cc9ddc87d75d".utf8)
    static func policy() throws -> Int {
        var count = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("ESP discovery: " + name) }; count += 1
        }
        let up = UInt32(IFF_UP | IFF_RUNNING)
        func address(_ host: String, _ mask: String? = "255.255.255.0", flags: UInt32? = nil, name: String = "en0") -> LocalNetworkIdentity.Address {
            .init(interface:name,value:host,flags:flags ?? up,netmask:mask)
        }
        var addresses = [address("192.168.255.191")], enabled = true, secret: String? = token, port: UInt16? = 18765, lookups = 0
        let discovery = MacESPDiscovery(token:{secret},enabled:{enabled},port:{port},address:{ peer in
            lookups += 1; return LocalNetworkIdentity.select(addresses,matching:peer)
        })
        func response(_ bytes: Data = request, peer: String = "192.168.255.100") -> MacDiscoveryResponse? { discovery.response(bytes,peer:peer) }
        let expected = Data("AIBOT_BRIDGE_V1|0123456789abcdef|192.168.255.191|18765|27d7491e5a3f59af0f9464ba6f90dfb768998066d7d944edcd054025c5cc10d3".utf8)
        try check("Windows-compatible UTF-8 HMAC vector",response()?.bytes == expected)
        try check("explicit source matches advertised address",response()?.sourceAddress == "192.168.255.191")
        try check("no token in discovery datagram",!String(decoding:expected,as:UTF8.self).contains(token))
        for value in ["", "AIBOT_DISCOVER_V2|0123456789abcdef|" + String(repeating:"0",count:64),
                      String(decoding:request,as:UTF8.self).uppercased(), String(decoding:request,as:UTF8.self)+"|extra",
                      String(decoding:request,as:UTF8.self)+"\n", String(repeating:"x",count:161)] {
            let before = lookups
            try check("malformed request rejected before network lookup",response(Data(value.utf8)) == nil && lookups == before)
        }
        var corrupt = request; corrupt[corrupt.count-1] ^= 1
        try check("tampered signature rejected",response(corrupt) == nil)
        try check("non-ASCII rejected",response(Data([255])) == nil)
        secret = String(repeating:"wrong",count:8); try check("other pairing rejected",response() == nil)
        secret = nil; try check("unreadable key never regenerated",response() == nil)
        secret = token; enabled = false; try check("disabled or USB-only does not discover",response() == nil)
        enabled = true; port = nil; try check("HTTP unready does not advertise",response() == nil)
        port = 0; try check("zero port rejected",response() == nil); port = 18765
        for peer in ["127.0.0.1","8.8.8.8","::1","192.168.1.100","192.168.255.0","192.168.255.255","192.168.255.191","192.168.255.010"] {
            try check("off-subnet or invalid peer rejected",response(peer:peer) == nil)
        }
        for mask in [nil,"255.0.255.0","0.0.0.0","255.255.255.255","255.255.255.254","255.255.001.0"] {
            addresses = [address("192.168.255.191",mask)]; try check("missing invalid or host-only mask rejected",response() == nil)
        }
        for flags in [UInt32(0),UInt32(IFF_UP),up|UInt32(IFF_POINTOPOINT),up|UInt32(IFF_LOOPBACK)] {
            addresses = [address("192.168.255.191",flags:flags)];try check("inactive or tunnel excluded",response() == nil)
        }
        addresses = [address("10.3.0.2"),address("192.168.255.190",name:"bridge100"),address("192.168.255.191",name:"en7")]
        try check("select matching physical subnet",response()?.bytes == expected)
        addresses = [address("192.168.1.2","255.255.0.0"),address("192.168.255.191")]
        try check("prefer most specific matching subnet",response()?.bytes == expected)
        var tab5Calls = 0
        func tab5(_ packet: Data, _ host: String) -> Data? {
            tab5Calls += 1
            return packet == Data("TAB5_fixture".utf8) && host == "192.168.255.191" ? Data("TAB5_response".utf8) : nil
        }
        try check("shared listener routes ESP without TAB5 call",MacTab5Discovery.route(request,peer:"192.168.255.100",local:nil,esp:discovery,tab5:tab5)?.bytes == expected && tab5Calls == 0)
        let routed = MacTab5Discovery.route(Data("TAB5_fixture".utf8),peer:"192.168.255.100",local:"192.168.255.191",esp:discovery,tab5:tab5)
        try check("shared listener preserves TAB5 callback and source path",routed?.bytes == Data("TAB5_response".utf8) && routed?.sourceAddress == nil && tab5Calls == 1)
        try check("unpaired ESP never falls through to TAB5",MacTab5Discovery.route(request,peer:"192.168.255.100",local:"192.168.255.191",esp:nil,tab5:tab5) == nil && tab5Calls == 1)
        addresses = [address("192.168.255.192")]
        try check("host DHCP change reflected without USB",response()?.sourceAddress == "192.168.255.192")
        addresses = [address("10.5.0.9")]
        try check("both IPs change retains pairing",response(peer:"10.5.0.11")?.sourceAddress == "10.5.0.9" && secret == token)
        try check("stale subnet cannot move binding back",response() == nil && secret == token)
        let recheck = MacESPDiscovery(token:{token},enabled:{enabled},port:{port},address:{_ in enabled=false;return "192.168.255.191"})
        try check("disable during address lookup suppresses reply",recheck.response(request,peer:"192.168.255.100") == nil)
        return count
    }
    // Production UDP listener and HTTP authentication, synthetic key, ephemeral
    // loopback ports only. IP/subnet changes are separately tested above.
    static func loopback() throws -> Int {
        var count = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("Discovery loopback: " + name) }; count += 1
        }
        func wait(_ predicate: () -> Bool) -> Bool {
            let until = ProcessInfo.processInfo.systemUptime + 3
            while ProcessInfo.processInfo.systemUptime < until { if predicate() { return true }; Thread.sleep(forTimeInterval:0.01) }; return false
        }
        let http = HTTPStatusServer(port:0,token:{token},loopbackOnly:true,snapshot:{Data("{\"version\":1}".utf8)})
        defer { http.stop() }; try http.start(); try check("HTTP ready",wait { http.boundPort != nil })
        let esp = MacESPDiscovery(token:{token},enabled:{true},port:{http.boundPort},address:{ $0 == "127.0.0.1" ? "127.0.0.1" : nil })
        let udp = MacTab5Discovery(port:0,loopbackOnly:true,respond:{packet,peer,local in
            MacTab5Discovery.route(packet,peer:peer,local:local,esp:esp,tab5:{bytes,_ in bytes == Data("TAB5_fixture".utf8) ? Data("TAB5_response".utf8) : nil})
        })
        defer { udp.stop() }; try udp.start(); try check("UDP ready",wait { udp.boundPort != nil })
        guard let port = udp.boundPort else { throw MacTab5LocalError.invalid("Missing fixture port") }
        try udp.start(); try check("start idempotent",udp.boundPort == port)
        let fd = socket(AF_INET,SOCK_DGRAM,0); guard fd >= 0 else { throw MacTab5LocalError.invalid("UDP fixture socket") }
        defer { Darwin.close(fd) }
        func endpoint(_ port: UInt16) -> sockaddr_in {
            var value = sockaddr_in(); value.sin_len = UInt8(MemoryLayout<sockaddr_in>.size); value.sin_family = sa_family_t(AF_INET)
            value.sin_port = port.bigEndian; value.sin_addr.s_addr = inet_addr("127.0.0.1"); return value
        }
        var client = endpoint(0)
        let bound = withUnsafePointer(to:&client) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.bind(fd,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        try check("unconnected client bound",bound == 0)
        func exchange(_ packet: Data, port: UInt16) -> (Data,String)? {
            var target = endpoint(port)
            let sent = packet.withUnsafeBytes { bytes in withUnsafePointer(to:&target) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { sendto(fd,bytes.baseAddress,bytes.count,0,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } } }
            guard sent == packet.count else { return nil }
            var descriptor = pollfd(fd:fd,events:Int16(POLLIN),revents:0)
            guard poll(&descriptor,1,500) > 0 else { return nil }
            var bytes = [UInt8](repeating:0,count:512), source = sockaddr_in(), size = socklen_t(MemoryLayout<sockaddr_in>.size)
            let received = withUnsafeMutablePointer(to:&source) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { recvfrom(fd,&bytes,bytes.count,0,$0,&size) } }
            guard received > 0 else { return nil }
            var ip = source.sin_addr, buffer = [CChar](repeating:0,count:Int(INET_ADDRSTRLEN))
            guard inet_ntop(AF_INET,&ip,&buffer,socklen_t(INET_ADDRSTRLEN)) != nil else { return nil }
            return (Data(bytes.prefix(received)),String(cString:buffer))
        }
        let received = exchange(request,port:port)
        let body = "AIBOT_BRIDGE_V1|\(nonce)|127.0.0.1|\(http.boundPort!)"
        let expected = Data((body+"|"+MacTab5Protocol.proof(key:Data(token.utf8),message:body)).utf8)
        try check("UDP signed response and source address",received?.0 == expected && received?.1 == "127.0.0.1")
        try check("malformed request gets no reply",exchange(Data("invalid".utf8),port:port) == nil)
        // Same source/port must work again after cancellation of the old flow.
        try check("next request on same UDP socket works",exchange(request,port:port)?.0 == expected)
        try check("TAB5 reply uses same shared listener",exchange(Data("TAB5_fixture".utf8),port:port)?.0 == Data("TAB5_response".utf8))
        try check("ESP still works after TAB5 packet",exchange(request,port:port)?.0 == expected)
        for index in 0..<12 {
            try check("repeated TAB5 packet \(index)",exchange(Data("TAB5_fixture".utf8),port:port)?.0 == Data("TAB5_response".utf8))
            try check("repeated ESP packet \(index)",exchange(request,port:port)?.0 == expected)
        }
        let config = URLSessionConfiguration.ephemeral; config.connectionProxyDictionary = [:]; config.httpShouldSetCookies = false; config.httpCookieStorage = nil
        let session = URLSession(configuration:config); defer { session.invalidateAndCancel() }
        func get(_ key: String) -> Int {
            let done = DispatchSemaphore(value:0); var status = 0
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(http.boundPort!)/status")!);request.setValue(key,forHTTPHeaderField:"X-AIBot-Token")
            session.dataTask(with:request) { _,response,_ in status=(response as? HTTPURLResponse)?.statusCode ?? 0; done.signal() }.resume()
            _ = done.wait(timeout:.now()+3);return status
        }
        try check("discovered endpoint accepts paired token",get(token) == 200)
        try check("wrong token rejected by HTTP",get("wrong") == 401)
        http.stop(); try check("HTTP stopped does not advertise",exchange(request,port:port) == nil)
        udp.stop(); try check("stop clears UDP readiness",udp.boundPort == nil)
        try udp.start(); try check("restart creates current listener",wait { udp.boundPort != nil })
        return count
    }
}
