import Darwin
import Foundation
import MacKeychainSupport
import Security

// One request over inherited anonymous pipes, then exit. No socket, launchd,
// command-line credential, log, environment credential or persistent process.
func run() throws {
    guard getuid() == geteuid(), getppid() > 1,
          fcntl(STDIN_FILENO, F_GETFD) >= 0, fcntl(STDOUT_FILENO, F_GETFD) >= 0 else { throw BridgeKeychain.Failure.identity }
    var inputInfo = stat(), outputInfo = stat()
    guard fstat(STDIN_FILENO, &inputInfo) == 0, fstat(STDOUT_FILENO, &outputInfo) == 0,
          inputInfo.st_mode & S_IFMT == S_IFIFO, outputInfo.st_mode & S_IFMT == S_IFIFO else { throw BridgeKeychain.Failure.identity }
    let ownPath = URL(fileURLWithPath: CommandLine.arguments[0]).standardizedFileURL.resolvingSymlinksInPath()
    guard ownPath.lastPathComponent == "AIBotKeychainHelper", ownPath.deletingLastPathComponent().lastPathComponent == "Helpers" else { throw BridgeKeychain.Failure.identity }
    let app = ownPath.deletingLastPathComponent().deletingLastPathComponent()
    guard app.lastPathComponent == "Contents" else { throw BridgeKeychain.Failure.identity }
    let expectedParent = app.appendingPathComponent("MacOS/AIBotBridge").path
    var parentPath = [CChar](repeating: 0, count: 4 * Int(MAXPATHLEN))
    let parent = getppid()
    guard proc_pidpath(parent, &parentPath, UInt32(parentPath.count)) > 0,
          URL(fileURLWithPath: String(cString: parentPath)).standardizedFileURL.resolvingSymlinksInPath().path == expectedParent else { throw BridgeKeychain.Failure.identity }
    let own = try BridgeKeychain.staticCode(ownPath)
    guard let cert = BridgeKeychain.certificate(own) else { throw BridgeKeychain.Failure.identity }
    try BridgeKeychain.verify(own, identifier: BridgeKeychain.helperID, certificate: cert)
    let requirement = try BridgeKeychain.requirement(identifier: BridgeKeychain.appID, certificate: cert)
    var caller: SecCode?
    guard SecCodeCopyGuestWithAttributes(nil, [kSecGuestAttributePid as String: parent] as CFDictionary, [], &caller) == errSecSuccess,
          let caller, SecCodeCheckValidity(caller, [], requirement) == errSecSuccess, getppid() == parent else { throw BridgeKeychain.Failure.identity }
    // Limit input even when the parent sends malformed or unterminated data.
    let bytes = try BridgeKeychain.readBounded(.standardInput)
    guard !bytes.isEmpty, bytes.count <= BridgeKeychain.limit else { throw BridgeKeychain.Failure.invalid }
    let request = try JSONDecoder().decode(BridgeKeychain.Request.self, from: bytes)
    let result = try BridgeKeychain.perform(request)
    let encoded = try JSONEncoder().encode(result)
    guard encoded.count <= BridgeKeychain.limit else { throw BridgeKeychain.Failure.invalid }
    try FileHandle.standardOutput.write(contentsOf: encoded)
}
do { try run() } catch { exit(1) }
