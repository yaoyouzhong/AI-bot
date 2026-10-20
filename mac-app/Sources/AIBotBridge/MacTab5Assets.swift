import Foundation

struct MacTab5Asset {
    let slot: Int
    let width: Int
    let height: Int
    let delays: [Int]
    let bytes: Data
    let id: String
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
    func chunk(_ offset: Int) -> [String: Any] {
        ["slot": slot, "id": id, "width": width, "height": height, "frames": frames, "delays": delays,
         "encoding": "rle565", "total": bytes.count, "offset": offset,
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
    func update(pets: [MacPetAnimation?], cover: Data?) {
        lock.lock(); defer { lock.unlock() }
        updatePetsLocked(pets); updateCoverLocked(cover)
    }
    func updatePets(_ pets: [MacPetAnimation?]) {
        lock.lock(); defer { lock.unlock() }; updatePetsLocked(pets)
    }
    private func updatePetsLocked(_ pets: [MacPetAnimation?]) {
        for index in 0..<2 {
            let pet = index < pets.count ? pets[index] : nil
            let source = pet.flatMap { try? $0.encode() }
            guard source != sources[index] else { continue }; sources[index] = source; offsets[index] = 0
            if let pet { assets[index] = try? .rgb565(slot: index, width: pet.width, height: pet.height, delays: pet.delays, raw: pet.frames.reduce(into: Data()) { $0.append($1) }) }
            else { assets[index] = nil }
        }
    }
    func updateCover(_ cover: Data?) {
        lock.lock(); defer { lock.unlock() }; updateCoverLocked(cover)
    }
    private func updateCoverLocked(_ cover: Data?) {
        guard cover != sources[2] else { return }
        sources[2] = cover; offsets[2] = 0
        if let cover, cover.count == 112 * 112 * 2 { assets[2] = try? .rgb565(slot: 2, width: 112, height: 112, delays: [1000], raw: cover) }
        else { assets[2] = nil }
    }
    static func validIDs(_ text: String) -> Bool {
        let ids = text.split(separator: ",", omittingEmptySubsequences: false)
        return ids.count == 3 && ids.allSatisfy { $0 == "-" || MacTab5Protocol.isHex(String($0), count: 8) }
    }
    func acknowledge(_ ids: String) {
        guard Self.validIDs(ids) else { return }
        lock.lock(); acknowledged = ids.components(separatedBy: ","); lock.unlock()
    }
    func projection() -> [String: Any] {
        lock.lock(); defer { lock.unlock() }
        var result: [String: Any] = ["assetIds": assets.map { $0.map { $0.id as Any } ?? NSNull() }]
        for _ in 0..<3 {
            slot = (slot + 1) % 3
            guard let asset = assets[slot], acknowledged[slot] != asset.id else { continue }
            if offsets[slot] >= asset.bytes.count { offsets[slot] = 0 }
            result["resource"] = asset.chunk(offsets[slot]); offsets[slot] += 1024; break
        }
        return result
    }
    var hasCover: Bool { lock.lock(); defer { lock.unlock() }; return assets[2] != nil }
    var hasPending: Bool {
        lock.lock(); defer { lock.unlock() }
        return assets.enumerated().contains { index, asset in asset != nil && acknowledged[index] != asset?.id }
    }
    func batch(_ maximum: Int) -> [[String: Any]] {
        (0..<min(maximum, 8)).compactMap { _ in projection()["resource"] as? [String: Any] }
    }
}
