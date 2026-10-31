import Foundation
import ImageIO

struct MacTab5Asset {
    let slot: Int
    let width: Int
    let height: Int
    let delays: [Int]
    let bytes: Data
    let id: String
    var encoding = "rle565"
    var frames: Int { delays.count }
    static func rgb565(slot: Int, width: Int, height: Int, delays: [Int], raw: Data) throws -> Self {
        guard (0...2).contains(slot), (1...336).contains(width), (1...336).contains(height),
              slot == 2 || width <= 120 && height <= 120,
              (1...8).contains(delays.count), delays.allSatisfy({ (20...60000).contains($0) }),
              raw.count == width * height * delays.count * 2 else { throw MacTab5Error.invalidPacket }
        var packed = Data(), offset = 0
        while offset < raw.count {
            let color = MacTab5Mailbox.u16(raw, offset)
            var count = 1
            while count < 65535 && offset + (count + 1) * 2 <= raw.count && MacTab5Mailbox.u16(raw, offset + count * 2) == color { count += 1 }
            MacTab5Mailbox.append16(count, to: &packed); MacTab5Mailbox.append16(Int(color), to: &packed); offset += count * 2
        }
        guard packed.count <= 524288 else { throw MacTab5Error.invalidPacket }
        return Self(slot: slot, width: width, height: height, delays: delays, bytes: packed,
                    id: String(format: "%08x", MacBinaryResourceProtocol.crc32(Array(packed))))
    }
    static func jpeg(_ bytes: Data) throws -> Self {
        guard !bytes.isEmpty, bytes.count <= 524288,
              let source = CGImageSourceCreateWithData(bytes as CFData,nil),
              CGImageSourceGetType(source) as String? == "public.jpeg",
              CGImageSourceGetCount(source) == 1,
              let properties = CGImageSourceCopyPropertiesAtIndex(source,0,nil) as? [CFString:Any],
              let width = (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue,
              let height = (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue,
              (1...560).contains(width), (1...336).contains(height),
              ((properties[kCGImagePropertyOrientation] as? NSNumber)?.intValue ?? 1) == 1 else { throw MacTab5Error.invalidPacket }
        return Self(slot:2,width:width,height:height,delays:[1000],bytes:bytes,
                    id:String(format:"%08x",MacBinaryResourceProtocol.crc32(Array(bytes))),encoding:"jpeg")
    }
    static func supportsJPEG(_ version: String?) -> Bool {
        guard let version, let text = version.split(separator:"-").first else { return false }
        let components = text.split(separator:".",omittingEmptySubsequences:false)
        let parts = components.compactMap { Int($0) }
        guard components.count == 3, parts.count == 3, parts.allSatisfy({ $0 >= 0 }) else { return false }
        return (parts[0],parts[1],parts[2]) >= (0,2,80)
    }
    func chunk(_ offset: Int) -> [String: Any] {
        ["slot": slot, "id": id, "width": width, "height": height, "frames": frames, "delays": delays,
         "encoding": encoding, "total": bytes.count, "offset": offset,
         "bytes": Data(bytes[offset..<min(offset + 1024, bytes.count)]).base64EncodedString()]
    }
}

final class MacTab5Assets {
    private let lock = NSLock()
    private var assets: [MacTab5Asset?] = [nil, nil, nil]
    private var sources: [Data?] = [nil, nil, nil]
    private var acknowledged = ["-", "-", "-"]
    private var slot = 0
    private var offsets = [0, 0, 0]
    private var coverFallbackAttempted = false
    private var coverGeneration = 0, coverAcknowledgements = 0
    private var coverPasses = 0, coverRetryAt: TimeInterval = 0
    private let tick: () -> TimeInterval
    var diagnostics: (([String: Int]) -> Void)?
    private var lastDiagnostic: TimeInterval = -.infinity
    init(tick: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) { self.tick = tick }
    // An unacknowledged image must not monopolize transport and device decode.
    // All transports share this budget; status publication never resets it.
    private func coverMaySend() -> Bool { coverPasses < 3 && tick() >= coverRetryAt }
    private func advanceCoverLocked() {
        guard coverPasses >= 3, tick() >= coverRetryAt, !coverFallbackAttempted, let jpeg = assets[2],
              jpeg.encoding == "jpeg", acknowledged[2] != jpeg.id else { return }
        coverFallbackAttempted = true
        guard jpeg.width <= 336,
              let source = CGImageSourceCreateWithData(jpeg.bytes as CFData, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
              let pixels = MacRgb565Renderer.renderImage(image, width: jpeg.width, height: jpeg.height, topDown: true),
              let fallback = try? MacTab5Asset.rgb565(slot: 2, width: jpeg.width, height: jpeg.height,
                    delays: [1000], raw: pixels) else { return }
        // Existing wire format; preserve every pixel dimension. Decode once on
        // the host if device JPEG confirmation failed, never loop formats.
        assets[2] = fallback; offsets[2] = 0; coverPasses = 0; coverRetryAt = 0
    }
    private func recordCoverLocked() {
        let now = tick()
        guard now - lastDiagnostic >= 1, let diagnostics else { return }
        lastDiagnostic = now
        diagnostics(["width":assets[2]?.width ?? 0,"height":assets[2]?.height ?? 0,
            "bytes":assets[2]?.bytes.count ?? 0,"offset":offsets[2],"passes":coverPasses,
            "jpeg":assets[2]?.encoding == "jpeg" ? 1:0,
            "confirmed":assets[2].map { acknowledged[2] == $0.id ? 1:0 } ?? 0,
            "limited":coverPasses >= 3 ? 1:0,"usbChunks":coverUSBChunks,
            "generation":coverGeneration,"acknowledgements":coverAcknowledgements])
    }
    private var coverReadyAt: TimeInterval = 0, coverFirstChosenAt: TimeInterval = 0, coverConfirmedAt: TimeInterval = 0
    private var coverUSBChunks = 0, coverUSBBytes = 0
    private var coverUSBMilliseconds = 0.0, coverUSBPeakMilliseconds = 0.0
    private var coverUSBSteps = 0, coverUSBMaxBurst = 0, coverStatusBytes = 0, coverWireBytes = 0
    private var coverStatusMilliseconds = 0.0, coverGenerationMilliseconds = 0.0, coverRPCMilliseconds = 0.0
    func update(pets: [MacPetAnimation?], cover: Data?) {
        lock.lock(); defer { lock.unlock() }
        updatePetsLocked(pets); updateCoverLocked(cover)
    }
    func updatePets(_ pets: [MacPetAnimation?]) {
        lock.lock(); defer { lock.unlock() }; updatePetsLocked(pets)
    }
    private func updatePetsLocked(_ pets: [MacPetAnimation?]) {
        for index in 0..<2 {
            let selected = index < pets.count ? pets[index] : nil
            // A transient unreadable selection must not clear a last-good asset.
            if selected == nil, assets[index] != nil { continue }
            guard let pet = selected ?? MacByteSprout.animation, let source = try? pet.encode() else { continue }
            guard source != sources[index],
                  let asset = try? MacTab5Asset.rgb565(slot: index, width: pet.width, height: pet.height, delays: pet.delays, raw: pet.frames.reduce(into: Data()) { $0.append($1) }) else { continue }
            sources[index] = source; offsets[index] = 0; assets[index] = asset
        }
    }
    func updateCover(_ cover: Data?, jpegSupported: Bool = true) {
        let selected: Data?
        if !jpegSupported, let cover, (try? MacTab5Asset.jpeg(cover)) != nil {
            selected = MacMusicService.renderArtwork(cover)
        } else { selected = cover }
        lock.lock(); defer { lock.unlock() }; updateCoverLocked(selected)
    }
    private func updateCoverLocked(_ cover: Data?) {
        guard cover != sources[2] else { return }
        sources[2] = cover; offsets[2] = 0; coverPasses = 0; coverRetryAt = 0
        coverGeneration += 1; coverAcknowledgements = 0; coverFallbackAttempted = false
        coverReadyAt = ProcessInfo.processInfo.systemUptime; coverFirstChosenAt = 0; coverConfirmedAt = 0
        coverUSBChunks = 0; coverUSBBytes = 0; coverUSBMilliseconds = 0; coverUSBPeakMilliseconds = 0
        coverUSBSteps = 0; coverUSBMaxBurst = 0; coverStatusBytes = 0; coverWireBytes = 0
        coverStatusMilliseconds = 0; coverGenerationMilliseconds = 0; coverRPCMilliseconds = 0
        if let cover, let jpeg = try? MacTab5Asset.jpeg(cover) { assets[2] = jpeg }
        else if let cover, cover.count == 112 * 112 * 2 { assets[2] = try? .rgb565(slot: 2, width: 112, height: 112, delays: [1000], raw: cover) }
        else { assets[2] = nil }
    }
    static func validIDs(_ text: String) -> Bool {
        let ids = text.split(separator: ",", omittingEmptySubsequences: false)
        return ids.count == 3 && ids.allSatisfy { $0 == "-" || MacTab5Protocol.isHex(String($0), count: 8) }
    }
    func acknowledge(_ ids: String) {
        guard Self.validIDs(ids) else { return }
        lock.lock(); defer { lock.unlock() }
        acknowledged = ids.components(separatedBy: ",")
        coverAcknowledgements += 1
        recordCoverLocked()
        if coverConfirmedAt == 0, let cover = assets[2], acknowledged[2] == cover.id {
            coverConfirmedAt = ProcessInfo.processInfo.systemUptime
        }
    }
    func projection(includeResource: Bool = true) -> [String: Any] {
        lock.lock(); defer { lock.unlock() }
        advanceCoverLocked(); recordCoverLocked()
        var result: [String: Any] = ["assetIds": assets.map { $0.map { $0.id as Any } ?? NSNull() }]
        // BLE sends images separately, so status/voice traffic never advances
        // or consumes an image chunk merely by publishing current asset IDs.
        guard includeResource else { return result }
        for _ in 0..<3 {
            slot = (slot + 1) % 3
            guard let asset = assets[slot], acknowledged[slot] != asset.id, slot != 2 || coverMaySend() else { continue }
            if slot == 2, coverFirstChosenAt == 0 { coverFirstChosenAt = ProcessInfo.processInfo.systemUptime }
            if offsets[slot] >= asset.bytes.count { offsets[slot] = 0 }
            result["resource"] = asset.chunk(offsets[slot]); offsets[slot] += 1024
            if slot == 2, offsets[slot] >= asset.bytes.count {
                coverPasses += 1; coverRetryAt = tick() + 2
            }
            break
        }
        return result
    }
    var hasCover: Bool { lock.lock(); defer { lock.unlock() }; return assets[2] != nil }
    func recordUSBStep(started:TimeInterval,status:TimeInterval,rpc:TimeInterval,generation:TimeInterval,rawBytes:Int,wireBytes:Int,chunks:Int) {
        lock.lock(); defer { lock.unlock() }
        guard assets[2] != nil, coverConfirmedAt == 0 || coverConfirmedAt >= started else { return }
        coverUSBSteps += 1; coverUSBMaxBurst = max(coverUSBMaxBurst,chunks)
        coverStatusBytes += rawBytes; coverWireBytes += wireBytes
        coverStatusMilliseconds += status*1000; coverGenerationMilliseconds += generation*1000; coverRPCMilliseconds += rpc*1000
    }
    func recordUSBChunk(_ chunk: [String:Any], elapsed: TimeInterval) {
        lock.lock(); defer { lock.unlock() }
        guard chunk["slot"] as? Int == 2, chunk["id"] as? String == assets[2]?.id,
              let offset = chunk["offset"] as? Int, let total = chunk["total"] as? Int,
              offset >= 0, offset < total, elapsed.isFinite, elapsed >= 0 else { return }
        coverUSBChunks += 1; coverUSBBytes += min(1024,total-offset)
        coverUSBMilliseconds += elapsed*1000; coverUSBPeakMilliseconds = max(coverUSBPeakMilliseconds,elapsed*1000)
    }
    var coverDiagnostic: String {
        lock.lock(); defer { lock.unlock() }
        let format = assets[2].map { "\($0.width)×\($0.height) \($0.encoding) · " } ?? ""
        let total = coverConfirmedAt > 0 ? String(format:"%.0f ms",(coverConfirmedAt-coverReadyAt)*1000) : "等待确认"
        let first = coverFirstChosenAt > 0 ? String(format:"%.0f ms",(coverFirstChosenAt-coverReadyAt)*1000) : "未选择"
        let timing = String(format:" · 就绪至确认 %@ · 首片 %@ · USB %d 片/%dB · 收发累计 %.0f ms/峰值 %.0f ms",total,first,coverUSBChunks,coverUSBBytes,coverUSBMilliseconds,coverUSBPeakMilliseconds)
        let work = String(format:" · USB周期 %d/最大批 %d · 状态 %.0f ms（生成/认证 %.0f）/%d→%dB · 邮箱 %.0f ms",coverUSBSteps,coverUSBMaxBurst,coverStatusMilliseconds,coverGenerationMilliseconds,coverStatusBytes,coverWireBytes,coverRPCMilliseconds)
        let pets = (0..<2).map { i in "\(i == 0 ? "Claude":"Codex") 桌宠：目标 \(assets[i]?.id ?? "-") · 设备确认 \(acknowledged[i]) · \(assets[i]?.width ?? 0)×\(assets[i]?.height ?? 0) · \(assets[i]?.frames ?? 0) 帧" }.joined(separator:"\n")
        return "封面资源：\(format)目标 \(assets[2]?.id ?? "-") · 设备确认 \(acknowledged[2]) · 分块位置 \(offsets[2])/\(assets[2]?.bytes.count ?? 0)B" + timing + work + "\n" + pets
    }
    var hasPending: Bool {
        lock.lock(); defer { lock.unlock() }
        advanceCoverLocked()
        return assets.enumerated().contains { index, asset in asset != nil && acknowledged[index] != asset?.id && (index != 2 || coverMaySend()) }
    }
    func batch(_ maximum: Int) -> [[String: Any]] {
        (0..<min(maximum, 8)).compactMap { _ in projection()["resource"] as? [String: Any] }
    }
}
