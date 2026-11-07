import Foundation
import CryptoKit
import ImageIO
import CoreGraphics

enum MacClaudeNativeContent {
    static func hash(_ data: Data) -> String { SHA256.hash(data:data).map { String(format:"%02X",$0) }.joined() }
    static func digest(_ text: String, images: [String] = []) -> String {
        hash(Data((images.isEmpty ? text : "claude-native-images-v1\n"+hash(Data(text.utf8))+"\n"+images.joined(separator:"\n")).utf8))
    }
    static func image(_ data: Data) throws -> String {
        _ = try MacTab5Photos.imageType(data)
        guard let source = CGImageSourceCreateWithData(data as CFData,nil), let image = CGImageSourceCreateImageAtIndex(source,0,nil) else { throw MacTab5Error.invalidPacket }
        let width = image.width, height = image.height
        var pixels = Data(count:width*height*4)
        let rendered = pixels.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data:buffer.baseAddress,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,
                space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo.byteOrder32Little.rawValue|CGImageAlphaInfo.premultipliedFirst.rawValue) else { return false }
            context.setBlendMode(.copy); context.draw(image,in:CGRect(x:0,y:0,width:width,height:height)); return true
        }
        guard rendered else { throw MacTab5Error.invalidPacket }
        var bytes = Data(); for dimension in [UInt32(width),UInt32(height)] { var value = dimension.littleEndian; withUnsafeBytes(of:&value) { bytes.append(contentsOf:$0) } }
        bytes.append(pixels); return hash(bytes)
    }
    static func message(_ content: Any?) -> String? {
        if let text = content as? String { return digest(text) }
        guard let blocks = content as? [[String:Any]] else { return nil }
        var text = "", images: [String] = []
        for block in blocks {
            if block["type"] as? String == "text" { text += block["text"] as? String ?? "" }
            else if block["type"] as? String == "image", images.count < 3,
                    let source = block["source"] as? [String:Any], source["type"] as? String == "base64",
                    ["image/jpeg","image/png"].contains(source["media_type"] as? String ?? ""),
                    let encoded = source["data"] as? String, encoded.count <= 2_796_204,
                    let bytes = Data(base64Encoded:encoded), let digest = try? image(bytes) { images.append(digest) }
            else { return nil }
        }
        return digest(text,images:images)
    }
}

struct MacClaudeNativeReceipt: Codable {
    let session: UUID, scope: String, digest: String, known: Set<String>, limited: Bool
    static func capture(_ history: MacClaudeHistory.Value, session: UUID, scope: String, text: String, images: [String]) -> Self {
        Self(session:session,scope:scope,digest:MacClaudeNativeContent.digest(text,images:images),known:history.accepted,limited:history.limited)
    }
    func confirm(_ history: MacClaudeHistory.Value, scope current: String) -> String? {
        guard scope == current, !limited, !history.limited, known.isSubset(of:history.accepted) else { return nil }
        let added = history.accepted.subtracting(known)
        guard added.count == 1, let id = added.first, UUID(uuidString:id) != nil, history.inputDigests[id] == digest else { return nil }
        return id
    }
}

struct MacClaudeNativeView: Equatable {
    let target: String, text: String, images: [String], empty: Bool, ready: Bool, canSubmit: Bool
}
struct MacClaudeNativeRequest {
    let operation: String, text: String, paths: [URL], imageDigests: [String]
    let authorized: () -> Bool, current: () throws -> Bool
}
protocol MacClaudeNativeSession {
    var hasSessionAPI: Bool { get }
    var session: UUID { get }
    func scope() throws -> String
    func state() throws -> String
    func view(_ operation: String) throws -> MacClaudeNativeView
    func stage(_ request: MacClaudeNativeRequest) throws
    func submit(_ request: MacClaudeNativeRequest) throws
    /// True only after the client confirms interruption of the requested turn.
    func interrupt(_ request: MacClaudeNativeRequest) throws -> Bool
}
extension MacClaudeNativeSession { var hasSessionAPI: Bool { false } }

enum MacClaudeNativeOperation {
    struct Result {
        let attempted: Bool, user: String?, stopped: Bool
        var submissionNotInvoked = false
    }
    static func run(client: any MacClaudeNativeSession, request: MacClaudeNativeRequest,
                    history: () throws -> MacClaudeHistory.Value, dispatch: (MacClaudeNativeReceipt) throws -> Void,
                    wait: () -> Void = { Thread.sleep(forTimeInterval:0.1) }) throws -> Result {
        guard request.authorized(), try request.current() else { throw MacCodexDesktopError.rejected("native_session_changed") }
        guard client.hasSessionAPI else { throw MacCodexDesktopError.rejected("native_session_api_unavailable") }
        let scope = try client.scope(), first = try client.view(request.operation)
        let baseline = MacClaudeNativeReceipt.capture(try history(),session:client.session,scope:scope,text:request.text,images:request.imageDigests)
        guard !baseline.limited, first.ready else { throw MacCodexDesktopError.rejected("native_history_or_state_unconfirmed") }
        func same(_ view: MacClaudeNativeView) throws -> Bool {
            guard view.target == first.target, view.ready, try client.scope() == scope else { return false }
            return try request.current()
        }
        if request.operation == "interrupt" {
            guard request.authorized(), try same(client.view("interrupt")) else { throw MacCodexDesktopError.rejected("turn_changed") }
            try dispatch(baseline)
            do {
                guard try client.interrupt(request), try client.scope() == scope, try request.current() else {
                    return Result(attempted:true,user:nil,stopped:false)
                }
                let after = try client.view("observe")
                let preserved = after.target == first.target && after.text == first.text && after.images == first.images
                return Result(attempted:true,user:nil,stopped:preserved)
            } catch { return Result(attempted:true,user:nil,stopped:false) }
        }
        guard first.empty, first.text.isEmpty else { throw MacCodexDesktopError.rejected("desktop_draft_present") }
        guard request.authorized(), try same(client.view(request.operation)) else { throw MacCodexDesktopError.rejected("native_session_changed") }
        try dispatch(baseline)
        let notSubmitted = Result(attempted:true,user:nil,stopped:false,submissionNotInvoked:true)
        do {
            try client.stage(request)
            var staged = false
            for _ in 0..<30 {
                let view = try client.view(request.operation)
                guard try same(view) else { return notSubmitted }
                if view.text == request.text, view.images == request.paths.map(\.lastPathComponent), view.canSubmit { staged = true; break }; wait()
            }
            guard staged, request.authorized() else { return notSubmitted }
            let final = try client.view(request.operation)
            guard try same(final), final.text == request.text, final.images == request.paths.map(\.lastPathComponent), final.canSubmit else { return notSubmitted }
        } catch { return notSubmitted }
        do {
            do { try client.submit(request) } catch { /* Invocation may already have reached the original client. */ }
            for _ in 0..<50 {
                if let id = baseline.confirm(try history(),scope:try client.scope()) { return Result(attempted:true,user:id,stopped:false) }; wait()
            }
        } catch { return Result(attempted:true,user:nil,stopped:false) }
        return Result(attempted:true,user:nil,stopped:false)
    }
}
