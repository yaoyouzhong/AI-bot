import Foundation

enum MacClaudeEventValidation {
    // Isolated loopback endpoint for exercising production hook parsing and
    // lifecycle code. No account, serial, pairing or foreground UI is opened.
    static func serve(port: UInt16) throws {
        let identity = MacClaudeIdentity()
        let server = HTTPStatusServer(port:port,token:{String(repeating:"t",count:40)},
            claudeEvent:{identity.record($0)},loopbackOnly:true) {
                let selected = identity.selection()
                return (try? JSONSerialization.data(withJSONObject:[
                    "state":selected?.state ?? "none", "outcome":selected?.outcome ?? "none",
                    "completionActive":selected?.tool.completionActive ?? false,
                    "completionSequence":selected?.completionSequence ?? 0])) ?? Data()
            }
        try server.start()
        RunLoop.current.run()
        server.stop()
    }
}
