import Foundation
import ImageIO
import CoreGraphics

/// Receives only explicitly leased, authenticated device screenshots. Images
/// live in memory and never enter photo attachments, caches or diagnostics.
final class MacTab5ScreenPreview {
    struct Context: Equatable {
        let device: String, host: String
        var transports: Set<String>
        var available = true
        var allowsCapture = true
        var highDefinition = false
        var responsiveCapture = false
    }
    struct Frame {
        let width: Int, height: Int, pixels: Data, received: TimeInterval
    }
    private struct Request {
        let id: String, transport: String, started: TimeInterval, expires: Int64, encoding: String, version: Int
        var width: Int, height: Int
        var bytes = Data(), size = 0, sha = ""
        var progress: TimeInterval?
        var firstBegin: TimeInterval?, firstByte: TimeInterval?
    }
    private let lock = NSLock()
    private let clock: () -> TimeInterval, epoch: () -> Int64
    private var context: Context?, stream = "", lease: TimeInterval = 0, next: TimeInterval = 0
    private var request: Request?, frame: Frame?, note = ""
    private var frameSHA = ""
    private var memoryLevel = 0, fallbackFrames = 0
    private var qualityRetry: TimeInterval = 0, qualityBackoff: TimeInterval = 30
    private var issued = 0, publications = 0, timedOut = 0, incoming = 0, begins = 0, chunks = 0, frames = 0, rejected = 0
    private var ttlFromSnapshot: Int64 = 0, ttlOverBound = 0
    private var lastReplyStatus = 0, lastOperation = "none"
    private var lastTiming: (wait: Int, prepare: Int, receive: Int, total: Int, interval: Int, bytes: Int)?
    private var previousCompletion: TimeInterval?, lastShown: TimeInterval?, displayDelay: Int?
    init(clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, epoch: @escaping () -> Int64 = { Int64(Date().timeIntervalSince1970 * 1000) }) {
        self.clock = clock; self.epoch = epoch
    }
    static func allowsCapture(firmware: String?) -> Bool {
        guard let firmware, firmware.hasSuffix("-ui") else { return false }
        let fields = firmware.dropLast(3).split(separator:".",omittingEmptySubsequences:false)
        let values = fields.compactMap { Int($0) }
        guard fields.count == 3, values.count == 3, values.allSatisfy({ $0 >= 0 }) else { return false }
        // .268's first hardware preview uses an invalid cache flag combination.
        // Older versions have no mirror support. Keep this path off until the
        // combined firmware contains the cache fix and has a newer version.
        return [0,2,268].lexicographicallyPrecedes(values)
    }
    static func supportsHD(firmware: String?) -> Bool {
        guard allowsCapture(firmware:firmware), let firmware else { return false }
        let values = firmware.dropLast(3).split(separator:".").compactMap{Int($0)}
        return [0,2,269].lexicographicallyPrecedes(values)
    }
    static func supportsResponsiveCapture(firmware: String?) -> Bool {
        guard allowsCapture(firmware:firmware), let firmware else { return false }
        let values = firmware.dropLast(3).split(separator:".").compactMap{Int($0)}
        return [0,2,276].lexicographicallyPrecedes(values)
    }
    @discardableResult func start(_ current: Context?) -> String {
        lock.lock(); defer { lock.unlock() }
        context = current; stream = MacTab5Protocol.identifier(); lease = clock() + 5
        request = nil; frame = nil; frameSHA = ""; memoryLevel = 0; fallbackFrames = 0; qualityRetry = 0; qualityBackoff = 30; next = 0; note = current?.allowsCapture == true ? "正在连接设备，等待首帧…" : "当前固件暂不支持设备预览。"
        issued = 0; publications = 0; timedOut = 0; incoming = 0; begins = 0; chunks = 0; frames = 0; rejected = 0
        ttlFromSnapshot = 0; ttlOverBound = 0; lastReplyStatus = 0; lastOperation = "none"
        lastTiming = nil; previousCompletion = nil; lastShown = nil; displayDelay = nil
        return stream
    }
    func stop(_ token: String? = nil) {
        lock.lock(); defer { lock.unlock() }
        guard token == nil || token == stream else { return }
        clear(); note = "预览已关闭。"
    }
    private func clear() { context = nil; stream = ""; request = nil; frame = nil; frameSHA = ""; lease = 0 }
    func renew(_ token: String, context current: Context?) {
        lock.lock(); defer { lock.unlock() }
        guard !stream.isEmpty, token == stream else { return }
        guard let old = context, let current, current.device == old.device, current.host == old.host else {
            clear(); note = "设备或电脑归属已变化，请重新打开设备预览。"; return
        }
        if clock() > lease { request = nil; frame = nil }
        lease = clock() + 5
        guard current.allowsCapture else {
            request = nil; frame = nil; context = current
            note = "当前固件的设备预览暂不可用，修复将随统一固件更新。"; return
        }
        guard current.available, !current.transports.isEmpty else {
            request = nil; note = "设备未连接，等待连接恢复。"; return
        }
        if let p = request, !current.transports.contains(p.transport) || clock() - p.started > 30 || clock() - (p.progress ?? p.started) > (p.encoding == "jpeg" && p.bytes.isEmpty ? 15:p.transport == "BLE" ? 10:5) {
            timedOut += 1
            request = nil; note = "读取超时或连接已变化，等待下一帧。"; next = clock() + 1
        }
        context = current
        if request == nil, clock() >= next {
            let transport = current.transports.contains("USB") ? "USB" : current.transports.contains("Wi-Fi") ? "Wi-Fi" : "BLE"
            if current.highDefinition && transport != "BLE" && memoryLevel > 0 && fallbackFrames >= 3 && clock() >= qualityRetry {
                memoryLevel = 0; fallbackFrames = 0
            }
            let hd = current.highDefinition && transport != "BLE" && memoryLevel == 0
            let small = transport == "BLE" || memoryLevel >= 2
            request = Request(id:MacTab5Protocol.identifier(), transport:transport, started:clock(), expires:epoch()+30_000,
                              encoding:hd ? "jpeg":"rgb565-rle1", version:current.highDefinition ? 2:1,
                              width:hd ? 1280:small ? 320:640, height:hd ? 720:small ? 180:360)
            issued += 1
        }
    }
    var command: [String: Any]? {
        lock.lock(); defer { lock.unlock() }
        return commandLocked()
    }
    private func commandLocked() -> [String: Any]? {
        if clock() > lease { request = nil; frame = nil }
        guard !stream.isEmpty, clock() <= lease, let p = request, clock() - p.started <= 30, epoch() <= p.expires else { return nil }
        return ["version":p.version,"streamId":stream,"requestId":p.id,"transport":p.transport,"expiresAt":p.expires,
                "width":p.width,"height":p.height,"encoding":p.encoding]
    }
    // Numeric publication/receiver observations only. Never retain frame bytes,
    // stream/request identifiers, device names or error descriptions here.
    func command(frameEpochMilliseconds: Int64) -> [String: Any]? {
        lock.lock(); defer { lock.unlock() }
        guard var value = commandLocked(), let p = request, frameEpochMilliseconds > 0, frameEpochMilliseconds <= Int64.max-30_000 else { return nil }
        // A status snapshot can predate this request. Keep the advertised lease
        // inside both the original deadline and the device snapshot's 30s bound.
        let expires = min(p.expires,frameEpochMilliseconds+30_000)
        guard expires > epoch() else { return nil }
        value["expiresAt"] = expires
        publications += 1; ttlFromSnapshot = expires-frameEpochMilliseconds
        if ttlFromSnapshot > 30_000 { ttlOverBound += 1 }
        return value
    }
    var diagnostic: String {
        lock.lock(); defer { lock.unlock() }
        let elapsed = request.map { Int(min(180,max(0,clock()-$0.started))*1000) } ?? 0
        let timing = lastTiming.map { t in
            "\n预览计时 ms：排队 \(t.wait)，首片前 \(t.prepare)，首片至完成 \(t.receive)，请求合计 \(t.total)，完整帧间隔 \(t.interval)，窗口呈现 \(displayDelay.map(String.init) ?? "—")；编码字节 \(t.bytes)"
        } ?? ""
        return "设备预览诊断：请求 \(issued)，发布 \(publications)，超时 \(timedOut)，接收 \(incoming)，开始 \(begins)，帧片 \(chunks)，完整帧 \(frames)，拒绝 \(rejected)\n" +
            "预览等待 \(elapsed) ms，状态时钟有效期差 \(ttlFromSnapshot) ms，超过30秒上界 \(ttlOverBound) 次，最近 \(lastOperation)/\(lastReplyStatus)" + timing
    }
    private static func ms(_ seconds: TimeInterval) -> Int { Int(min(180_000,max(0,seconds*1000))) }
    func observedInWindow(_ token: String, received: TimeInterval) {
        lock.lock(); defer { lock.unlock() }
        guard token == stream, frame?.received == received, lastShown != received else { return }
        lastShown = received; displayDelay = Self.ms(clock()-received)
    }
    func snapshot(_ token: String) -> (Frame?, String) {
        lock.lock(); defer { lock.unlock() }
        guard token == stream, clock() <= lease else { return (nil,note.isEmpty ? "预览已停止。":note) }
        let stale = frame.map { clock() - $0.received > 5 } ?? false
        return (frame, stale ? "画面未更新 · " + note : note)
    }
    func receive(_ root: [String: Any], bytes: Data, context current: Context?, device: String, transport: String) -> MacTab5RPCResult {
        lock.lock(); defer { lock.unlock() }
        incoming += 1
        let operation = root["op"] as? String ?? ""
        lastOperation = ["begin","error","frame","unchanged"].contains(operation) ? operation : "invalid"
        let result = receiveLocked(root,bytes:bytes,context:current,device:device,transport:transport)
        lastReplyStatus = result.status
        if result.status == 200 {
            if operation == "begin" { begins += 1 }
            if operation == "frame" { chunks += 1; if result.body["complete"] as? Bool == true { frames += 1 } }
        } else { rejected += 1 }
        return result
    }
    private func receiveLocked(_ root: [String: Any], bytes: Data, context current: Context?, device: String, transport: String) -> MacTab5RPCResult {
        guard !stream.isEmpty, clock() <= lease, let original = context, let current, current.available, current.allowsCapture,
              current.device == original.device, current.host == original.host, device == original.device,
              current.transports.contains(transport), root["streamId"] as? String == stream,
              var p = request, root["requestId"] as? String == p.id, p.transport == transport,
              clock() - p.started <= 30, epoch() <= p.expires else { return .error(409,"preview_expired") }
        switch root["op"] as? String {
        case "begin":
            guard bytes.isEmpty else { return .error(400,"preview_data") }
            if p.version == 2 {
                guard root["encoding"] as? String == p.encoding,
                      let w = MacTab5Protocol.integer(root["width"]), let h = MacTab5Protocol.integer(root["height"]),
                      (w == p.width && h == p.height) || (p.encoding == "jpeg" && w == p.height && h == p.width) else { return .error(400,"preview_dimensions") }
                p.width = Int(w); p.height = Int(h)
            }
            if p.firstBegin == nil { p.firstBegin = clock() }
            p.progress = clock(); request = p
            return .init(200,["accepted":true,"width":p.width,"height":p.height,"encoding":p.encoding,"chunkSize":4096])
        case "unchanged":
            guard p.version == 2, bytes.isEmpty, let old = frame, old.width == p.width, old.height == p.height,
                  root["encoding"] as? String == p.encoding, root["sha256"] as? String == frameSHA,
                  MacTab5Protocol.integer(root["width"]) == Int64(p.width), MacTab5Protocol.integer(root["height"]) == Int64(p.height) else { return .error(409,"preview_changed") }
            frame = Frame(width:old.width,height:old.height,pixels:old.pixels,received:clock())
            frameSucceeded(); request = nil; next = clock()+(current.responsiveCapture && transport != "BLE" ? 0.1:2); note = frameNote(width:p.width,height:p.height,transport:transport)
            return .init(200,["accepted":true,"complete":true])
        case "error":
            guard bytes.isEmpty, let value = root["error"] as? String,
                  ["busy","memory","capture","encoding","connection"].contains(value) else { return .error(400,"preview_error") }
            note = ["busy":"设备忙，稍后继续。","memory":"设备内存不足，暂时无法预览。","capture":"暂未取得完整画面。","encoding":"画面编码失败。","connection":"画面连接已中断。"][value]!
            if value == "memory", p.version == 2, p.width > 320 {
                memoryLevel = p.encoding == "jpeg" ? 1:2
                fallbackFrames = 0; qualityRetry = clock()+qualityBackoff; qualityBackoff = min(300,qualityBackoff*2)
                note = memoryLevel == 1 ? "内存受限，正在切换标准画质…":"内存受限，正在切换小图预览…"
            }
            request = nil; next = clock() + (value == "memory" ? 1:2)
            return .init(200,["accepted":true])
        case "frame":
            guard root["encoding"] as? String == p.encoding, MacTab5Protocol.integer(root["width"]) == Int64(p.width),
                  MacTab5Protocol.integer(root["height"]) == Int64(p.height),
                  let size = MacTab5Protocol.integer(root["size"]), (4...524288).contains(size),
                  let offset = MacTab5Protocol.integer(root["offset"]), offset >= 0,
                  let sha = root["sha256"] as? String, MacTab5Protocol.isHex(sha,count:64),
                  (1...4096).contains(bytes.count), offset <= size, bytes.count <= size-offset else { return .error(400,"preview_frame") }
            let total = Int(size), position = Int(offset)
            if p.size == 0 { guard offset == 0 else { return .error(409,"preview_offset") }; p.size = total; p.sha = sha; p.firstByte = clock() }
            guard p.size == total, p.sha == sha else { return .error(409,"preview_changed") }
            if position < p.bytes.count {
                guard position+bytes.count <= p.bytes.count,
                      p.bytes.subdata(in:position..<position+bytes.count) == bytes else { return .error(409,"preview_changed") }
            } else {
                guard position == p.bytes.count else { return .error(409,"preview_offset") }
                p.bytes.append(bytes); p.progress = clock()
            }
            if p.bytes.count == p.size {
                guard MacTab5Protocol.sha256(p.bytes) == p.sha,
                      let pixels = Self.decodeFrame(p.bytes,width:p.width,height:p.height,encoding:p.encoding) else {
                    request = nil; next = clock()+1; note = "画面校验失败，等待下一帧。"; return .error(400,"preview_corrupt")
                }
                let completed = clock(), began = p.firstBegin ?? p.started, firstByte = p.firstByte ?? completed
                lastTiming = (Self.ms(began-p.started),Self.ms(firstByte-began),Self.ms(completed-firstByte),
                              Self.ms(completed-p.started),previousCompletion.map { Self.ms(completed-$0) } ?? 0,p.size)
                previousCompletion = completed; displayDelay = nil
                frame = Frame(width:p.width,height:p.height,pixels:pixels,received:completed); frameSHA = p.sha
                frameSucceeded(); request = nil; next = clock()+(transport == "BLE" ? 1:current.responsiveCapture ? 0.1:0.25); note = frameNote(width:p.width,height:p.height,transport:transport)
                return .init(200,["accepted":true,"nextOffset":p.size,"complete":true])
            }
            request = p
            if frame == nil { note = "正在读取画面 · \(p.bytes.count*100/p.size)%" }
            return .init(200,["accepted":true,"nextOffset":p.bytes.count,"complete":false])
        default: return .error(400,"preview_operation")
        }
    }
    private func frameSucceeded() {
        if memoryLevel > 0 { fallbackFrames = min(3,fallbackFrames+1) }
        else { qualityBackoff = 30 }
    }
    private func frameNote(width:Int,height:Int,transport:String) -> String {
        let quality = memoryLevel == 1 ? " · 标准画质":memoryLevel >= 2 ? " · 小图预览":""
        return "实时镜像 · \(width) × \(height) · \(transport)"+quality
    }
    static func decodeFrame(_ data: Data, width: Int, height: Int, encoding: String) -> Data? {
        if encoding == "rgb565-rle1" { return decode(data,width:width,height:height) }
        guard encoding == "jpeg", width > 0, height > 0, width <= 1280, height <= 1280, width*height <= 1280*720,
              data.count <= 524288, let source = CGImageSourceCreateWithData(data as CFData,nil),
              CGImageSourceGetCount(source) == 1, CGImageSourceGetType(source) as String? == "public.jpeg",
              let properties = CGImageSourceCopyPropertiesAtIndex(source,0,nil) as? [CFString:Any],
              (properties[kCGImagePropertyPixelWidth] as? Int) == width, (properties[kCGImagePropertyPixelHeight] as? Int) == height,
              let image = CGImageSourceCreateImageAtIndex(source,0,nil),
              let context = CGContext(data:nil,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,
                  space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipLast.rawValue), let raw = context.data else { return nil }
        context.draw(image,in:CGRect(x:0,y:0,width:width,height:height))
        let rgba = raw.assumingMemoryBound(to:UInt8.self)
        var pixels = Data(count:width*height*2)
        pixels.withUnsafeMutableBytes { (out:UnsafeMutableRawBufferPointer) in
            for i in 0..<width*height {
                let word = UInt16(rgba[i*4]>>3)<<11 | UInt16(rgba[i*4+1]>>2)<<5 | UInt16(rgba[i*4+2]>>3)
                out[i*2] = UInt8(word&255); out[i*2+1] = UInt8(word>>8)
            }
        }
        return pixels
    }
    static func decode(_ data: Data, width: Int, height: Int) -> Data? {
        guard (1...640).contains(width), (1...360).contains(height), data.count <= 524288 else { return nil }
        let count = width*height, input = [UInt8](data)
        var output = Data(); output.reserveCapacity(count*2)
        var at = 0
        while at < input.count {
            guard at+2 <= input.count else { return nil }
            let control = Int(input[at]) | Int(input[at+1])<<8, n = control & 32767; at += 2
            guard n > 0, n <= count-output.count/2 else { return nil }
            if control & 32768 != 0 {
                guard at+2 <= input.count else { return nil }
                for _ in 0..<n { output.append(input[at]); output.append(input[at+1]) }; at += 2
            } else {
                guard n <= (input.count-at)/2 else { return nil }
                output.append(contentsOf:input[at..<at+n*2]); at += n*2
            }
        }
        return output.count == count*2 ? output:nil
    }
}
