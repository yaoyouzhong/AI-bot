import AppKit
import CommonCrypto
import MacARDKeyAgreement
import Security

/// Independently implemented RFB security type 30. Credentials travel only to
/// the verified local Apple service and are never persisted or passed to tools.
enum MacARDConnection {
    struct Parameters {
        let generator:UInt16, prime:Data, peer:Data
        func validate() throws {
            guard [256,384,512].contains(prime.count),peer.count == prime.count,generator >= 2,
                  prime[0]&128 != 0,prime.last!&1 == 1 else { throw MacTab5LocalError.invalid("Mac 账户认证的 DH 参数不受支持，未发送账户凭据。") }
        }
    }
    static func validate(username:Data,password:Data) throws {
        guard (1...63).contains(username.count),(1...63).contains(password.count),!username.contains(0),!password.contains(0),
              String(data:username,encoding:.utf8) != nil,String(data:password,encoding:.utf8) != nil else {
            throw MacTab5LocalError.invalid("Mac 账户认证要求账户名和密码各为 1–63 个 UTF-8 字节；不会截断或替换。")
        }
    }
    static func agree(_ parameters:Parameters) throws -> (publicValue:Data,shared:Data) {
        try parameters.validate()
        var publicValue = Data(repeating:0,count:parameters.prime.count), shared = publicValue
        let result = parameters.prime.withUnsafeBytes { prime in parameters.peer.withUnsafeBytes { peer in
            publicValue.withUnsafeMutableBytes { pub in shared.withUnsafeMutableBytes { secret in
                aibot_ard_agree(prime.bindMemory(to:UInt8.self).baseAddress,prime.count,parameters.generator,
                    peer.bindMemory(to:UInt8.self).baseAddress,pub.bindMemory(to:UInt8.self).baseAddress,secret.bindMemory(to:UInt8.self).baseAddress)
            } }
        } }
        guard result == 0 else { publicValue.resetBytes(in:0..<publicValue.count); shared.resetBytes(in:0..<shared.count); throw MacTab5LocalError.invalid("系统 DH 密钥协商未完成，未发送账户凭据。") }
        return (publicValue,shared)
    }
    static func response(_ parameters:Parameters,username:Data,password:Data,
                         exchange:(Parameters) throws -> (publicValue:Data,shared:Data) = agree,
                         random:() throws -> Data = randomBlock) throws -> Data {
        try validate(username:username,password:password); try parameters.validate()
        var keys = try exchange(parameters)
        defer { keys.publicValue.resetBytes(in:0..<keys.publicValue.count); keys.shared.resetBytes(in:0..<keys.shared.count) }
        guard keys.publicValue.count == parameters.prime.count,keys.shared.count == parameters.prime.count else { throw MacTab5Error.invalidPacket }
        var block = try random(), digest = Data(repeating:0,count:16), encrypted = Data(repeating:0,count:128)
        defer { block.resetBytes(in:0..<block.count); digest.resetBytes(in:0..<digest.count); encrypted.resetBytes(in:0..<encrypted.count) }
        guard block.count == 128 else { throw MacTab5Error.invalidPacket }
        block.replaceSubrange(0..<username.count,with:username); block[username.count] = 0
        block.replaceSubrange(64..<(64+password.count),with:password); block[64+password.count] = 0
        _ = keys.shared.withUnsafeBytes { source in digest.withUnsafeMutableBytes { target in
            CC_MD5(source.baseAddress,CC_LONG(source.count),target.bindMemory(to:UInt8.self).baseAddress)
        } }
        var written = 0
        let result = digest.withUnsafeBytes { key in block.withUnsafeBytes { input in encrypted.withUnsafeMutableBytes { output in
            CCCrypt(CCOperation(kCCEncrypt),CCAlgorithm(kCCAlgorithmAES),CCOptions(kCCOptionECBMode),key.baseAddress,16,nil,input.baseAddress,128,output.baseAddress,128,&written)
        } } }
        guard result == kCCSuccess,written == 128 else { throw MacTab5Error.unavailable }
        return encrypted+keys.publicValue
    }
    private static func randomBlock() throws -> Data {
        var data = Data(repeating:0,count:128)
        guard data.withUnsafeMutableBytes({ SecRandomCopyBytes(kSecRandomDefault,$0.count,$0.baseAddress!) }) == errSecSuccess else { data.resetBytes(in:0..<data.count); throw MacTab5Error.unavailable }
        return data
    }
    private static func begin(read:(Int) throws -> Data,send:(Data) throws -> Void,isCancelled:() -> Bool) throws -> (Parameters,pid_t) {
        guard try read(12) == Data("RFB 003.889\n".utf8) else { throw MacTab5LocalError.invalid("本机服务未提供预期的 Apple RFB 协议，未发送账户凭据。") }
        try send(Data("RFB 003.008\n".utf8))
        let count = Int(try read(1)[0]); guard count > 0 else { throw MacTab5Error.invalidPacket }
        guard try read(count).contains(30) else { throw MacTab5LocalError.invalid("本机服务未提供 Mac 账户认证，不会改用无认证或 VNC 密码。") }
        try send(Data([30]))
        let header = [UInt8](try read(4)), length = Int(header[2])*256+Int(header[3])
        guard [256,384,512].contains(length) else { throw MacTab5LocalError.invalid("本机 DH 参数长度不受支持，未发送账户凭据。") }
        let parameters = Parameters(generator:UInt16(header[0])*256+UInt16(header[1]),prime:try read(length),peer:try read(length))
        try parameters.validate()
        return (parameters,try MacARDServerIdentity.verify(isCancelled:isCancelled))
    }
    static func negotiate(password:Data,read:(Int) throws -> Data,send:(Data) throws -> Void,isCancelled:() -> Bool) throws {
        let username = Data(NSUserName().utf8)
        try validate(username:username,password:password)
        let (parameters,pid) = try begin(read:read,send:send,isCancelled:isCancelled)
        var packet = try response(parameters,username:username,password:password)
        defer { packet.resetBytes(in:0..<packet.count) }
        guard !isCancelled(),try MacARDServerIdentity.verify(isCancelled:isCancelled) == pid else { throw MacTab5LocalError.invalid("本机屏幕共享服务或测试状态已变化，未发送账户凭据。") }
        try send(packet)
        guard try read(4) == Data(repeating:0,count:4) else { throw MacTab5LocalError.invalid("Mac 账户认证未通过；未重试，也未发送测试按键或登录输入。") }
        _ = try MacVNCConnectionCheck.sharedSize(read:read,send:send)
    }
    /// Public parameters and ephemeral math only. Never sends an account name,
    /// credential ciphertext, public-key response, input, or framebuffer request.
    static func probe() throws {
        try MacVNCConnectionCheck.withConnection(timeout:12,isCancelled:{false}) { read,send,_ in
            let (parameters,_) = try begin(read:read,send:send,isCancelled:{false})
            var result = try agree(parameters)
            defer { result.shared.resetBytes(in:0..<result.shared.count); result.publicValue.resetBytes(in:0..<result.publicValue.count) }
            print("MAC_ARD_PUBLIC_CHECK_OK Apple service identity and DH \(parameters.prime.count*8); no account credentials or input sent")
        }
    }
}

private enum MacARDServerIdentity {
    private static let path = "/System/Library/CoreServices/RemoteManagement/screensharingd.bundle/Contents/MacOS/screensharingd"
    static func verify(isCancelled:() -> Bool) throws -> pid_t {
        guard !isCancelled() else { throw MacTab5LocalError.invalid("账户认证已取消，未发送账户凭据。") }
        let process = Process(), pipe = Pipe()
        process.executableURL = URL(fileURLWithPath:"/bin/launchctl")
        process.arguments = ["print","system/com.apple.screensharing"]
        process.environment = ["PATH":"/usr/bin:/bin:/usr/sbin:/sbin","LC_ALL":"C"]
        process.standardInput = FileHandle.nullDevice; process.standardOutput = pipe; process.standardError = FileHandle.nullDevice
        do { try process.run() } catch { throw MacTab5LocalError.invalid("无法核验系统屏幕共享服务，未发送账户凭据。") }
        let timeout = DispatchWorkItem { if process.isRunning { process.terminate() } }
        DispatchQueue.global().asyncAfter(deadline:.now()+2,execute:timeout)
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit(); timeout.cancel()
        guard !isCancelled(),process.terminationStatus == 0,data.count <= 32_768,let text = String(data:data,encoding:.utf8) else { throw MacTab5LocalError.invalid("系统屏幕共享服务核验未完成，未发送账户凭据。") }
        let lines = text.split(separator:"\n").map { $0.trimmingCharacters(in:.whitespaces) }
        func value(_ name:String) -> String? {
            let prefix = name+" = ", found = lines.filter { $0.hasPrefix(prefix) }
            return found.count == 1 ? String(found[0].dropFirst(prefix.count)):nil
        }
        guard value("program") == path,value("path") == "/System/Library/LaunchDaemons/com.apple.screensharing.plist",
              let raw = value("pid"),let pid = pid_t(raw),pid > 0 else { throw MacTab5LocalError.invalid("未能确认本机 Apple 屏幕共享服务，未发送账户凭据。") }
        var code:SecCode?, requirement:SecRequirement?, staticCode:SecStaticCode?, url:CFURL?
        guard SecCodeCopyGuestWithAttributes(nil,[kSecGuestAttributePid:pid] as CFDictionary,[],&code) == errSecSuccess,let code,
              SecRequirementCreateWithString("anchor apple and identifier \"com.apple.screensharing.daemon\"" as CFString,[],&requirement) == errSecSuccess,let requirement,
              SecCodeCheckValidity(code,[],requirement) == errSecSuccess,
              SecCodeCopyStaticCode(code,[],&staticCode) == errSecSuccess,let staticCode,
              SecCodeCopyPath(staticCode,[],&url) == errSecSuccess,let url,
              [path,"/System/Library/CoreServices/RemoteManagement/screensharingd.bundle"].contains((url as URL).path) else {
            throw MacTab5LocalError.invalid("Apple 屏幕共享进程签名或路径核验失败，未发送账户凭据。")
        }
        return pid
    }
}
