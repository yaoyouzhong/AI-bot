import AppKit
import Carbon
import ApplicationServices
import Foundation
import SQLite3

// Reads native identity first; Accessibility and input are only used by an
// explicitly authorized operation. It never launches or resumes Claude.
final class MacClaudeNativeClient: MacClaudeNativeSession {
    let session: UUID
    private let pid: Int32, start: String, record: URL, host: NSRunningApplication, hostStart: String, entrypoint: String
    private let database: URL?
    var source: MacClaudeSessionSource { entrypoint == "cli" ? .terminal : .vscode }
    private struct UI { let window: AXUIElement, frame: AXUIElement, editor: AXUIElement, composer: AXUIElement, button: AXUIElement?, view: MacClaudeNativeView }
    private init(session:UUID,pid:Int32,start:String,record:URL,host:NSRunningApplication,hostStart:String,entrypoint:String,database:URL?) {
        self.session=session;self.pid=pid;self.start=start;self.record=record;self.host=host;self.hostStart=hostStart;self.entrypoint=entrypoint;self.database=database
    }
    static func ps(_ pid:Int32,_ field:String) throws -> String {
        guard pid>0,["lstart=","ppid=","comm=","tty=","pgid=,tpgid=","uid="].contains(field) else { throw MacTab5Error.unconfirmed }
        let process=Process(),pipe=Pipe();process.executableURL=URL(fileURLWithPath:"/bin/ps");process.arguments=["-o",field,"-p",String(pid)]
        var environment=ProcessInfo.processInfo.environment;environment["LC_ALL"]="C";environment["TZ"]="UTC";process.environment=environment
        process.standardOutput=pipe;process.standardError=FileHandle.nullDevice;try process.run()
        let deadline=DispatchWorkItem{if process.isRunning { process.terminate() }};DispatchQueue.global().asyncAfter(deadline:.now()+2,execute:deadline)
        let bytes=pipe.fileHandleForReading.readDataToEndOfFile();process.waitUntilExit();deadline.cancel()
        guard process.terminationStatus==0,bytes.count<8192 else { throw MacTab5Error.unavailable }
        return String(decoding:bytes,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines)
    }
    private static func metadata(_ file:URL) throws -> [String:Any] {
        let info=try file.resourceValues(forKeys:[.isRegularFileKey,.isSymbolicLinkKey,.fileSizeKey])
        guard info.isRegularFile==true,info.isSymbolicLink != true,(info.fileSize ?? 20000)<16384 else { throw MacTab5Error.unconfirmed }
        return try MacTab5Protocol.object(Data(contentsOf:file))
    }
    static func live(entrypoint: String) -> Set<UUID> {
        let root=FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/sessions")
        let files=(try? FileManager.default.contentsOfDirectory(at:root,includingPropertiesForKeys:nil)) ?? []
        return Set(files.prefix(2048).compactMap { file -> UUID? in
            guard let row=try? metadata(file),row["entrypoint"] as? String == entrypoint,let id=row["sessionId"] as? String else{return nil}
            return UUID(uuidString:id)
        })
    }
    static func find(_ session:UUID, entrypoint: String) -> (any MacClaudeNativeSession)? {
        do {
            let root=FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/sessions")
            let files=try FileManager.default.contentsOfDirectory(at:root,includingPropertiesForKeys:nil).filter { $0.pathExtension=="json" }
            guard files.count<=2048 else { return nil }
            let matches=files.compactMap { file -> (URL,[String:Any])? in guard let row=try? metadata(file),row["entrypoint"] as? String == entrypoint,UUID(uuidString:row["sessionId"] as? String ?? "")==session else { return nil };return(file,row) }
            guard matches.count==1,let (record,row)=matches.first,let rawPid=row["pid"] as? Int,let pid=Int32(exactly:rawPid),pid>0,
                  Int32(record.deletingPathExtension().lastPathComponent)==pid,row["kind"] as? String=="interactive",
                  let entry=row["entrypoint"] as? String,["cli","claude-vscode"].contains(entry),
                  ["2.1.314","2.1.318"].contains(row["version"] as? String ?? ""),let start=row["procStart"] as? String,
                  try ps(pid,"lstart=")==start, try ps(pid,"uid=")==String(getuid()),
                  URL(fileURLWithPath:try ps(pid,"comm=")).lastPathComponent=="claude" else { return nil }
            var parent=pid,app:NSRunningApplication?
            for _ in 0..<12 { guard let next=Int32(try ps(parent,"ppid=")),next>1 else { break };parent=next
                if let candidate=NSRunningApplication(processIdentifier:parent),["com.microsoft.VSCode","com.apple.Terminal","com.googlecode.iterm2"].contains(candidate.bundleIdentifier ?? "") {app=candidate;break} }
            guard let host=app else { return nil }
            var database:URL?
            if entry=="claude-vscode" {
                guard host.bundleIdentifier=="com.microsoft.VSCode" else { return nil }
                let storage=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("Code/User/workspaceStorage")
                let folders=try FileManager.default.contentsOfDirectory(at:storage,includingPropertiesForKeys:nil)
                guard folders.count<=2048 else { return nil }
                let found=folders.compactMap { folder -> URL? in let file=folder.appendingPathComponent("state.vscdb");guard let object=try? readDatabase(file,"Anthropic.claude-code"),let panels=object["panelTabSessions"] as? [[String:Any]],panels.contains(where:{ UUID(uuidString:$0["sessionId"] as? String ?? "")==session }) else { return nil };return file }
                guard found.count==1 else { return nil };database=found[0]
            } else if !["com.apple.Terminal","com.googlecode.iterm2"].contains(host.bundleIdentifier ?? "") { return nil }
            return MacClaudeNativeClient(session:session,pid:pid,start:start,record:record,host:host,hostStart:try ps(host.processIdentifier,"lstart="),entrypoint:entry,database:database)
        }catch{return nil}
    }
    func state() throws -> String {
        let row=try Self.metadata(record)
        guard !host.isTerminated,UUID(uuidString:row["sessionId"] as? String ?? "")==session,row["procStart"] as? String==start,row["entrypoint"] as? String==entrypoint,
              row["pid"] as? Int==Int(pid),row["kind"] as? String=="interactive",["2.1.314","2.1.318"].contains(row["version"] as? String ?? ""),
              try Self.ps(pid,"lstart=")==start,try Self.ps(host.processIdentifier,"lstart=")==hostStart else {throw MacTab5Error.unconfirmed}
        return row["status"] as? String ?? "unknown"
    }
    func scope() throws -> String { _ = try state();return MacClaudeNativeContent.hash(Data("\(session)|\(pid)|\(start)|\(host.processIdentifier)|\(hostStart)".utf8)) }
    private static func readDatabase(_ file:URL,_ key:String) throws -> [String:Any] {
        guard ["Anthropic.claude-code","memento/workbench.parts.editor"].contains(key),try file.resourceValues(forKeys:[.isSymbolicLinkKey]).isSymbolicLink != true else {throw MacTab5Error.unconfirmed}
        var db:OpaquePointer?,statement:OpaquePointer?;defer{sqlite3_finalize(statement);sqlite3_close(db)}
        guard sqlite3_open_v2(file.path,&db,SQLITE_OPEN_READONLY,nil)==SQLITE_OK else {throw MacTab5Error.unavailable};sqlite3_busy_timeout(db,100)
        guard sqlite3_prepare_v2(db,"SELECT value FROM ItemTable WHERE key='\(key)'",-1,&statement,nil)==SQLITE_OK,sqlite3_step(statement)==SQLITE_ROW,let ptr=sqlite3_column_blob(statement,0) else {throw MacTab5Error.unconfirmed}
        let size=Int(sqlite3_column_bytes(statement,0));guard size>0,size<=262144 else {throw MacTab5Error.unconfirmed}
        return try MacTab5Protocol.object(Data(bytes:ptr,count:size))
    }
    @discardableResult private func selectedCode() throws -> Bool {
        guard let database else {throw MacTab5Error.unconfirmed}
        let saved=try Self.readDatabase(database,"memento/workbench.parts.editor")
        guard let part=saved["editorpart.state"] as? [String:Any],let grid=part["serializedGrid"] as? [String:Any],let root=grid["root"] as? [String:Any],let active=part["activeGroup"] as? Int else {throw MacTab5Error.unconfirmed}
        var groups:[[String:Any]]=[]
        func visit(_ node:[String:Any]) {if node["type"] as? String=="leaf",let data=node["data"] as? [String:Any] {groups.append(data)} else {for child in node["data"] as? [[String:Any]] ?? [] {visit(child)}}}
        visit(root);let selected=groups.filter{$0["id"] as? Int==active}
        guard selected.count==1,let group=selected.first,let editors=group["editors"] as? [[String:Any]],let index=(group["mru"] as? [Int])?.first,editors.indices.contains(index),
              let encoded=editors[index]["value"] as? String,let tab=try? MacTab5Protocol.object(Data(encoded.utf8)),tab["extensionId"] as? String=="Anthropic.claude-code",tab["viewType"] as? String=="mainThreadWebview-claudeVSCodePanel",
              let state=tab["state"] as? String,let detail=try? MacTab5Protocol.object(Data(state.utf8)),UUID(uuidString:detail["sessionID"] as? String ?? "")==session else {throw MacTab5Error.unconfirmed}
        return UUID(uuidString:detail["sessionWithNoTranscript"] as? String ?? "") == session
    }
    func isVerifiedBlank() -> Bool {
        guard entrypoint=="claude-vscode",(try? state())=="idle" else{return false}
        return (try? selectedCode()) == true
    }
    static func newPanels() -> [MacClaudeSessionObservation] {
        live(entrypoint: "claude-vscode").compactMap { id in
            guard let client=find(id, entrypoint: "claude-vscode") as? MacClaudeNativeClient,client.isVerifiedBlank(),
                  let row=try? metadata(client.record),
                  let date=try? client.record.resourceValues(forKeys:[.creationDateKey]).creationDate else{return nil}
            return MacClaudeSessionObservation(sessionID:id,lastActivity:date,title:"Claude Code",directory:row["cwd"] as? String)
        }
    }
    private func terminalTTY() throws {
        let tty = try Self.ps(pid, "tty=")
        let groups = try Self.ps(pid, "pgid=,tpgid=").split(whereSeparator: \.isWhitespace).compactMap { Int($0) }
        guard groups.count == 2, groups[0] > 0, groups[0] == groups[1], tty.hasPrefix("ttys"),
              let bundle = host.bundleIdentifier else { throw MacTab5Error.unconfirmed }
        // Polling must never request Automation permission.
        let target = NSAppleEventDescriptor(processIdentifier: host.processIdentifier)
        guard AEDeterminePermissionToAutomateTarget(target.aeDesc, typeWildCard, typeWildCard, false) == noErr,
              !host.isTerminated else { throw MacTab5Error.unconfirmed }
        let script = bundle == "com.apple.Terminal"
            ? "tell application id \"com.apple.Terminal\" to get tty of selected tab of front window"
            : "tell application id \"com.googlecode.iterm2\" to get tty of current session of current window"
        // NSAppleScript is main-thread-only. Bound the worker's wait so a
        // shutdown or a temporarily busy UI cannot block the USB worker.
        func query() -> String? {
            guard !host.isTerminated, isFrontmost else { return nil }
            var error: NSDictionary?
            let value = NSAppleScript(source: "with timeout of 1 second\n" + script + "\nend timeout")?
                .executeAndReturnError(&error).stringValue
            return error == nil ? value : nil
        }
        let value: String?
        if Thread.isMainThread { value = query() }
        else {
            let completed = DispatchSemaphore(value: 0)
            var reply: String?
            let work = DispatchWorkItem { reply = query(); completed.signal() }
            DispatchQueue.main.async(execute: work)
            guard completed.wait(timeout: .now() + 1.25) == .success else {
                work.cancel()
                throw MacTab5Error.timeout
            }
            value = reply
        }
        guard value == "/dev/" + tty, !host.isTerminated else {
            throw MacCodexDesktopError.rejected("native_terminal_tab_unconfirmed")
        }
    }
    private var isFrontmost: Bool { NSWorkspace.shared.frontmostApplication?.processIdentifier == host.processIdentifier }
    private var hasDefaultTerminalBindings: Bool {
        !FileManager.default.fileExists(atPath: record.deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("keybindings.json").path)
    }
    func allows(_ operation: String, images: Bool) -> Bool {
        guard isFrontmost, CGPreflightPostEventAccess(), AXIsProcessTrusted() else { return false }
        if entrypoint == "cli" {
            guard !images, hasDefaultTerminalBindings, ["send", "steer", "interrupt"].contains(operation) else { return false }
        } else {
            // The ordinary extension's Stop button is not a queue interface.
            guard ["send", "interrupt"].contains(operation) else { return false }
        }
        guard let target = try? ui(operation), target.view.ready else { return false }
        // Until each host's stop behavior is accepted, do not risk its draft.
        if operation == "interrupt", !target.view.empty { return false }
        if operation == "interrupt", entrypoint != "cli" {
            return target.button.map { Self.label($0) == "Stop" && Self.value($0, "AXEnabled") as? Bool == true } ?? false
        }
        return true
    }
    private static func value(_ node:AXUIElement,_ key:String)->CFTypeRef?{var v:CFTypeRef?;return AXUIElementCopyAttributeValue(node,key as CFString,&v) == .success ? v:nil}
    private static func element(_ node:AXUIElement,_ key:String)->AXUIElement?{guard let raw=value(node,key),CFGetTypeID(raw)==AXUIElementGetTypeID() else{return nil};return unsafeBitCast(raw,to:AXUIElement.self)}
    private static func label(_ node:AXUIElement)->String{(value(node,"AXTitle") as? String).flatMap{$0.isEmpty ? nil:$0} ?? (value(node,"AXDescription") as? String) ?? ""}
    private static func descendants(_ node:AXUIElement)->[AXUIElement]{var out:[AXUIElement]=[],pending=[node];while let next=pending.popLast(),out.count<3000{out.append(next);pending.append(contentsOf:(value(next,"AXChildren") as? [AXUIElement] ?? []).reversed())};return out}
    private static func unique(_ values:[AXUIElement])throws->AXUIElement{guard values.count==1,let first=values.first else{throw MacTab5Error.unconfirmed};return first}
    private func ui(_ operation:String)throws->UI {
        guard AXIsProcessTrusted() else{throw MacCodexDesktopError.rejected("native_accessibility_required")}
        let state=try state(),ready=operation=="send" ? state=="idle" : operation=="observe" ? ["idle","busy","working"].contains(state):["busy","working"].contains(state)
        guard ["idle","busy","working"].contains(state) else{throw MacTab5Error.unconfirmed}
        let app=AXUIElementCreateApplication(host.processIdentifier);AXUIElementSetMessagingTimeout(app,0.2)
        guard let window=Self.element(app,"AXFocusedWindow"),(Self.value(window,"AXSheets") as? [AXUIElement] ?? []).isEmpty else{throw MacTab5Error.unconfirmed}
        if entrypoint=="cli" {
            try terminalTTY();let editor=try Self.unique(Self.descendants(window).filter{Self.value($0,"AXRole") as? String=="AXTextArea"})
            guard let output=Self.value(editor,"AXValue") as? String,output.utf8.count<=2_000_000 else{throw MacTab5Error.unconfirmed}
            guard let text = Self.terminalComposer(output),
                  Self.value(editor, "AXFocused") as? Bool == true else { throw MacTab5Error.unconfirmed }
            return UI(window:window,frame:editor,editor:editor,composer:editor,button:nil,view:MacClaudeNativeView(
                target:"\(host.processIdentifier):\(CFHash(editor))",text:text,images:[],empty:text.isEmpty,ready:ready,canSubmit:!text.isEmpty))
        }
        try selectedCode()
        // A saved editor UUID and a foreground window do not identify a
        // composer if the host also exposes another live Claude webview.
        guard let windows = Self.value(app, "AXWindows") as? [AXUIElement], windows.count <= 16 else { throw MacTab5Error.unconfirmed }
        func codeFrames(_ window: AXUIElement) -> [AXUIElement] {
            Self.descendants(window).filter {
                Self.value($0,"AXRole") as? String == "AXWebArea" &&
                    (Self.value($0,"AXIdentifier") as? String == "active-frame" || Self.value($0,"AXDOMIdentifier") as? String == "active-frame")
            }
        }
        let frame = try Self.unique(windows.filter { Self.value($0, "AXMinimized") as? Bool != true }.flatMap(codeFrames))
        guard codeFrames(window).contains(where: { CFEqual($0, frame) }) else { throw MacTab5Error.unconfirmed }
        let nodes=Self.descendants(frame),editor=try Self.unique(nodes.filter{["AXTextArea","AXTextField"].contains(Self.value($0,"AXRole") as? String ?? "")&&Self.label($0)=="Message input"})
        let button=try Self.unique(nodes.filter{Self.value($0,"AXRole") as? String=="AXButton"&&["Send message","Stop"].contains(Self.label($0))})
        var ancestor=Self.element(editor,"AXParent"),composer:AXUIElement?
        for _ in 0..<8 {guard let node=ancestor,!CFEqual(node,frame) else{break};if Self.descendants(node).filter({Self.value($0,"AXRole") as? String=="AXButton"&&Self.label($0)=="Add"}).count==1{composer=node;break};ancestor=Self.element(node,"AXParent")}
        guard let composer,let raw=Self.value(editor,"AXValue") as? String else{throw MacTab5Error.unconfirmed}
        let images=try Self.attachmentNames(composer)
        let enabled = Self.value(button,"AXEnabled") as? Bool == true
        let empty = images.isEmpty && raw.isEmpty && (!enabled || Self.label(button) == "Stop")
        return UI(window:window,frame:frame,editor:editor,composer:composer,button:button,view:MacClaudeNativeView(
            target:"\(host.processIdentifier):\(CFHash(frame))",text:raw,images:images,empty:empty,ready:ready,
            canSubmit:enabled && Self.label(button) == "Send message"))
    }
    static func terminalComposer(_ output: String) -> String? {
        let lines = output.components(separatedBy: "\n")
        func divider(_ line: String) -> Bool {
            let value = line.trimmingCharacters(in: .whitespaces)
            return value.count >= 8 && value.allSatisfy { $0 == "─" || $0 == "━" || $0 == " " }
        }
        guard let index = lines.lastIndex(where: { ["❯", ">"].contains(String($0.prefix(1))) }),
              index > 0, index + 1 < lines.count, divider(lines[index - 1]), divider(lines[index + 1]) else { return nil }
        let line = lines[index]
        if line.count == 1 { return "" }
        guard [" ", "\u{a0}"].contains(String(line.dropFirst().prefix(1))) else { return nil }
        // A suggestion and a real draft can look identical. Treat either as
        // occupied; never discard a continuation line or an existing image.
        return String(line.dropFirst(2))
    }
    func view(_ operation:String)throws->MacClaudeNativeView{try ui(operation).view}
    private func focus(_ request:MacClaudeNativeRequest,target first: String)throws {
        guard isFrontmost,request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
        let target=try ui(request.operation)
        guard target.view.target == first, target.view.empty else { throw MacTab5Error.unconfirmed }
        if entrypoint != "cli" {
            guard AXUIElementSetAttributeValue(target.editor,kAXFocusedAttribute as CFString,kCFBooleanTrue) == .success else{throw MacTab5Error.unconfirmed}
        }
    }
    private func key(_ code:CGKeyCode,flags:CGEventFlags=[])throws {
        guard NSWorkspace.shared.frontmostApplication?.processIdentifier==host.processIdentifier,let down=CGEvent(keyboardEventSource:nil,virtualKey:code,keyDown:true),let up=CGEvent(keyboardEventSource:nil,virtualKey:code,keyDown:false) else{throw MacTab5Error.unconfirmed}
        down.flags=flags;up.flags=flags;down.postToPid(host.processIdentifier);up.postToPid(host.processIdentifier)
    }
    private func press(_ element:AXUIElement)throws{guard AXUIElementPerformAction(element,kAXPressAction as CFString) == .success else{throw MacTab5Error.unconfirmed}}
    private static func attachmentNames(_ composer:AXUIElement)throws->[String]{
        // SVG toolbar graphics share AXImage with thumbnails. Draft attachment
        // pills instead have an explicit Remove attachment button and file label.
        let buttons=descendants(composer).filter{
            value($0,"AXRole") as? String=="AXButton" && label($0)=="Remove attachment"
        }
        guard buttons.count<=3 else{throw MacTab5Error.unconfirmed}
        return try buttons.map{button in
            var ancestor=element(button,"AXParent")
            for _ in 0..<6 {
                guard let pill=ancestor,!CFEqual(pill,composer) else{break}
                let nodes=descendants(pill)
                if value(pill,"AXRole") as? String=="AXButton",let name=attachmentName(label(pill)) {
                    guard nodes.filter({
                        value($0,"AXRole") as? String=="AXButton" && label($0)=="Remove attachment"
                    }).count==1 else{throw MacTab5Error.unconfirmed}
                    return name
                }
                ancestor=element(pill,"AXParent")
            }
            throw MacTab5Error.unconfirmed
        }
    }
    static func attachmentName(_ label:String)->String?{
        let suffix=" Remove attachment"
        guard label.count<=320,label.hasSuffix(suffix) else{return nil}
        let content=label.dropLast(suffix.count)
        guard let separator=content.lastIndex(of:" ") else{return nil}
        let name=content[..<separator]
        let dimensions=content[content.index(after:separator)...].split(separator:"×",omittingEmptySubsequences:false)
        guard !name.isEmpty,name.count<=255,dimensions.count==2,
              let width=Int(dimensions[0]),width>0,let height=Int(dimensions[1]),height>0 else{return nil}
        return String(name)
    }
    private func namedButton(_ node:AXUIElement,_ name:String)throws->AXUIElement{try Self.unique(Self.descendants(node).filter{Self.value($0,"AXRole") as? String=="AXButton"&&Self.label($0)==name})}
    func stage(_ request:MacClaudeNativeRequest)throws {
        guard !request.text.unicodeScalars.contains(where:CharacterSet.controlCharacters.contains),request.text.utf8.count<=2000,request.paths.count<=3,request.paths.count==request.imageDigests.count else{throw MacTab5Error.invalidPacket}
        let held=CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift,.maskControl,.maskAlternate,.maskCommand,.maskSecondaryFn])
        guard held.isEmpty else{throw MacTab5Error.unconfirmed}
        try terminalBindings(request)
        let first=try ui(request.operation);guard first.view.empty else{throw MacCodexDesktopError.rejected("desktop_draft_present")};try focus(request,target:first.view.target)
        for (index,path) in request.paths.enumerated() {
            guard request.authorized(),try request.current(),try MacClaudeNativeContent.image(Data(contentsOf:path))==request.imageDigests[index] else{throw MacTab5Error.unconfirmed}
            guard entrypoint != "cli", isFrontmost else { throw MacTab5Error.unconfirmed }
            try attach(path,request:request)
            try awaitImages(request,index:index,target:first.view.target)
        }
        if request.text.isEmpty{return};let target=try ui(request.operation);guard target.view.text.isEmpty else{throw MacTab5Error.unconfirmed}
        if entrypoint != "cli"{_ = AXUIElementSetAttributeValue(target.editor,kAXFocusedAttribute as CFString,kCFBooleanTrue)}
        let chars=Array(request.text.utf16);var offset=0
        while offset<chars.count {
            let current=try ui(request.operation)
            guard request.authorized(),current.view.target==first.view.target,
                  current.view.text==String(decoding:chars.prefix(offset),as:UTF16.self),
                  Self.value(current.editor,"AXFocused") as? Bool==true else{throw MacTab5Error.unconfirmed}
            var end=min(offset+20,chars.count);if end<chars.count,(0xD800...0xDBFF).contains(chars[end-1]){end-=1}
            let chunk=Array(chars[offset..<end]);guard let down=CGEvent(keyboardEventSource:nil,virtualKey:0,keyDown:true),let up=CGEvent(keyboardEventSource:nil,virtualKey:0,keyDown:false) else{throw MacTab5Error.unavailable}
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier==host.processIdentifier else{throw MacTab5Error.unconfirmed}
            chunk.withUnsafeBufferPointer{down.keyboardSetUnicodeString(stringLength:chunk.count,unicodeString:$0.baseAddress!);up.keyboardSetUnicodeString(stringLength:chunk.count,unicodeString:$0.baseAddress!)}
            down.postToPid(host.processIdentifier);up.postToPid(host.processIdentifier);offset=end;Thread.sleep(forTimeInterval:0.03)
        }
    }
    private func attach(_ path:URL,request:MacClaudeNativeRequest)throws {
        var target=try ui(request.operation);try press(namedButton(target.composer,"Add"));Thread.sleep(forTimeInterval:0.1)
        target=try ui(request.operation);try press(namedButton(target.frame,"Upload from computer"))
        let app=AXUIElementCreateApplication(host.processIdentifier);var sheet:AXUIElement?
        for _ in 0..<40{if let window=Self.element(app,"AXFocusedWindow"),let value=(Self.value(window,"AXSheets") as? [AXUIElement])?.first{sheet=value;break};Thread.sleep(forTimeInterval:0.05)}
        guard sheet != nil,request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
        try key(5,flags:[.maskCommand,.maskShift]);Thread.sleep(forTimeInterval:0.15)
        guard let window=Self.element(app,"AXFocusedWindow") else{throw MacTab5Error.unconfirmed}
        let edit=try Self.unique(Self.descendants(window).filter{["AXTextField","AXComboBox"].contains(Self.value($0,"AXRole") as? String ?? "")&&Self.value($0,"AXFocused") as? Bool==true})
        guard AXUIElementSetAttributeValue(edit,kAXValueAttribute as CFString,path.path as CFString) == .success,Self.value(edit,"AXValue") as? String==path.path else{throw MacTab5Error.unconfirmed}
        try key(36);Thread.sleep(forTimeInterval:0.15)
        guard request.authorized(),try request.current(),let panel=(Self.value(window,"AXSheets") as? [AXUIElement])?.first else{throw MacTab5Error.unconfirmed}
        let buttons=Self.descendants(panel).filter{Self.value($0,"AXRole") as? String=="AXButton"&&["Open","打开"].contains(Self.label($0))}
        try press(Self.unique(buttons))
    }
    func submit(_ request:MacClaudeNativeRequest)throws {
        try terminalBindings(request)
        guard isFrontmost,request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
        let target=try ui(request.operation);guard target.view.ready,target.view.text==request.text,target.view.images==request.paths.map(\.lastPathComponent),target.view.canSubmit else{throw MacTab5Error.unconfirmed}
        if entrypoint == "cli" {
            // queueSubmit also sends from idle without forcing older queued
            // messages to interrupt a turn that started during preflight.
            try key(7, flags: .maskControl)
            try terminalBindings(request)
            let current = try ui(request.operation)
            guard request.authorized(), try request.current(), current.view == target.view else { throw MacTab5Error.unconfirmed }
            try key(36)
        } else {
            guard let button = target.button, Self.label(button) == "Send message" else { throw MacTab5Error.unconfirmed }
            try press(button)
        }
    }
    func interrupt(_ request:MacClaudeNativeRequest)throws -> Bool {
        guard allows("interrupt", images: false), request.authorized(), try request.current() else { throw MacTab5Error.unconfirmed }
        let target = try ui("interrupt")
        guard target.view.empty else { throw MacTab5Error.unconfirmed }
        if entrypoint == "cli" {
            guard hasDefaultTerminalBindings else { throw MacTab5Error.unconfirmed }
            try key(8, flags: .maskControl)
        } else {
            guard isFrontmost, let button = target.button, Self.label(button) == "Stop",
                  Self.value(button, "AXEnabled") as? Bool == true else { throw MacTab5Error.unconfirmed }
            try press(button)
        }
        for _ in 0..<40 {
            guard request.authorized() else { return false }
            if try request.interrupted() { return try request.current() }
            Thread.sleep(forTimeInterval: 0.1)
        }
        return false
    }
    private func terminalBindings(_ request:MacClaudeNativeRequest)throws {
        if entrypoint == "cli" {
            let text = request.text.trimmingCharacters(in: .whitespaces)
            guard !text.hasPrefix("/"), !text.hasPrefix("!"), hasDefaultTerminalBindings,
                  request.paths.isEmpty else { throw MacTab5Error.unconfirmed }
        }
    }
    private func awaitImages(_ request:MacClaudeNativeRequest,index:Int,target:String,owned:()->Bool={true})throws {
        let expected=Array(request.paths.prefix(index+1)).map(\.lastPathComponent)
        let deadline=ProcessInfo.processInfo.systemUptime+20
        while ProcessInfo.processInfo.systemUptime<deadline {
            guard owned(),request.authorized(),try request.current() else{throw MacTab5Error.unconfirmed}
            if let current=try? view(request.operation) {
                guard current.target==target,current.ready,current.text.isEmpty,(index...expected.count).contains(current.images.count) else{throw MacTab5Error.unconfirmed}
                if current.images==expected {
                    guard owned() else{throw MacTab5Error.unconfirmed}
                    return
                }
            }
            Thread.sleep(forTimeInterval:0.08)
        }
        throw MacTab5Error.unconfirmed
    }
}
