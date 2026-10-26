import Foundation
import Network

enum MacHTTPListenerRecoveryValidation {
    static func policy() throws -> Int {
        var count=0
        func check(_ label:String,_ value:Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("HTTP listener recovery: " + label) }; count += 1
        }
        var policy=MacHTTPListenerRecovery(); policy.begin(100)
        try check("first busy retry",policy.nextDelay(now:100,addressInUse:true)==0.5)
        try check("backoff",policy.nextDelay(now:100.5,addressInUse:true)==1)
        try check("permission/other errors never retry",policy.nextDelay(now:101,addressInUse:false)==nil)
        try check("clock rollback stops retry",policy.nextDelay(now:99,addressInUse:true)==nil)
        var now=101.0,attempts=0
        while let delay=policy.nextDelay(now:now,addressInUse:true) { now += delay; attempts += 1; guard attempts<20 else { throw MacTab5LocalError.invalid("Unbounded listener retry") } }
        try check("time and attempts bounded",now<=130 && attempts<12)
        policy.begin(200);try check("new explicit start resets policy",policy.nextDelay(now:200,addressInUse:true)==0.5)
        return count
    }
    // Deliberately collide on two ephemeral loopback-only ports. No device,
    // real token, user profile, public listener or system setting is accessed.
    static func loopback() throws -> Int {
        var count=try policy()
        func check(_ label:String,_ value:Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("Loopback listener: " + label) }; count += 1
        }
        func wait(_ predicate:() -> Bool) -> Bool {
            let until=ProcessInfo.processInfo.systemUptime+4
            while ProcessInfo.processInfo.systemUptime<until { if predicate(){return true}; Thread.sleep(forTimeInterval:0.01) }
            return false
        }
        func held() throws -> (NWListener,DispatchSemaphore) {
            let parameters=NWParameters.tcp
            parameters.requiredLocalEndpoint = .hostPort(host:.ipv4(.loopback),port:.any)
            let listener=try NWListener(using:parameters,on:.any),ready=DispatchSemaphore(value:0)
            listener.stateUpdateHandler={ if case .ready = $0 { ready.signal() } }
            listener.newConnectionHandler={ $0.cancel() };listener.start(queue:.global())
            return (listener,ready)
        }
        let (blocker,ready)=try held();defer{blocker.cancel()}
        try check("exclusive blocker ready",ready.wait(timeout:.now()+3) == .success)
        guard let port=blocker.port else { throw MacTab5LocalError.invalid("Missing loopback port") }
        let server=HTTPStatusServer(port:port.rawValue,token:{String(repeating:"fixture",count:8)},loopbackOnly:true,snapshot:{Data()})
        defer{server.stop()};try server.start()
        try check("busy start enters retry",wait{server.statusDiagnosticSummary.contains("正在重试")})
        try check("failed bind never reports ready",server.boundPort==nil)
        blocker.cancel()
        try check("recovers when previous owner releases port",wait{server.boundPort==port.rawValue})
        try server.start();try check("start idempotent",server.boundPort==port.rawValue)
        let config=URLSessionConfiguration.ephemeral;config.connectionProxyDictionary=[:]
        config.httpShouldSetCookies=false;config.httpCookieStorage=nil
        let session=URLSession(configuration:config);defer{session.invalidateAndCancel()}
        let done=DispatchSemaphore(value:0);var status=0
        session.dataTask(with:URL(string:"http://127.0.0.1:\(port.rawValue)/status")!){ _,response,_ in
            status=(response as? HTTPURLResponse)?.statusCode ?? 0;done.signal()
        }.resume()
        let replied=done.wait(timeout:.now()+3) == .success
        try check("authentication still required after recovery",replied && status==401)
        server.stop();try check("stop clears readiness",server.boundPort==nil)
        let (secondBlocker,secondReady)=try held();defer{secondBlocker.cancel()}
        try check("second blocker ready",secondReady.wait(timeout:.now()+3) == .success)
        guard let secondPort=secondBlocker.port else { throw MacTab5LocalError.invalid("Missing second loopback port") }
        let stopped=HTTPStatusServer(port:secondPort.rawValue,token:{nil},loopbackOnly:true,snapshot:{Data()})
        defer{stopped.stop()};try stopped.start()
        try check("second collision scheduled",wait{stopped.statusDiagnosticSummary.contains("正在重试")})
        stopped.stop();secondBlocker.cancel();Thread.sleep(forTimeInterval:1.1)
        try check("stopped generation cannot resurrect",stopped.boundPort==nil && stopped.statusDiagnosticSummary.contains("已停止"))
        return count
    }
}
