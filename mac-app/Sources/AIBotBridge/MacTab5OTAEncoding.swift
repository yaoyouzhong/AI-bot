import Foundation
import zlib

/// Existing TAB5 block format; every block decompresses independently and the
/// device still validates the complete original image before selecting a boot slot.
enum MacTab5OTAEncoding {
    static let name = "aibot-zlib-blocks-v1"
    static func supportsRanges(_ firmware:String?, minimumPatch:Int = 257) -> Bool {
        guard let firmware, firmware.hasSuffix("-ui") else { return false }
        let parts = firmware.dropLast(3).split(separator:".",omittingEmptySubsequences:false)
        guard parts.count == 3, parts[0] == "0", parts[1] == "2",
              !parts[2].isEmpty, parts[2].allSatisfy({ $0.isASCII && $0.isNumber }),
              let patch = Int(parts[2]) else { return false }
        return patch >= minimumPatch
    }
    static func negotiated(_ headers:[String:String]) -> Bool {
        headers["x-aibot-ota-flow"] == "tcp-v2" && headers["x-aibot-ota-encoding"] == name
    }
    static func range(_ raw:Data, offset:Int, enabled:Bool) -> MacTab5RPCResult {
        if enabled, !raw.isEmpty, raw.count <= MacTab5Binary.bulk {
            var packed = [UInt8](repeating:0,count:Int(compressBound(uLong(raw.count))))
            var size = uLongf(packed.count)
            let result = raw.withUnsafeBytes { source in
                packed.withUnsafeMutableBufferPointer { target in
                    compress2(target.baseAddress,&size,source.bindMemory(to:UInt8.self).baseAddress,uLong(raw.count),Z_DEFAULT_COMPRESSION)
                }
            }
            if result == Z_OK, Int(size)+96 < raw.count {
                return .init(200,["offset":offset,"encoding":"zlib","decodedSize":raw.count],bytes:Data(packed.prefix(Int(size))))
            }
        }
        return .init(200,["offset":offset],bytes:raw)
    }
    static func stream(_ image:Data) -> Data {
        var result = Data(); result.reserveCapacity(image.count)
        for offset in stride(from:0,to:image.count,by:MacTab5Binary.bulk) {
            let raw = Data(image[offset..<min(offset+MacTab5Binary.bulk,image.count)])
            let block = range(raw,offset:offset,enabled:true), bytes = block.bytes
            for value in [offset,raw.count,bytes.count,block.body["encoding"] as? String == "zlib" ? 1:0] {
                MacTab5Mailbox.append32(UInt32(value),to:&result)
            }
            result.append(bytes)
        }
        return result
    }
}
