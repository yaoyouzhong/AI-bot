import CoreGraphics
import Foundation
import ImageIO

struct MacPetAsset {
    let data: Data
    let licenseURL: URL
}

enum MacPetAssetError: Error, LocalizedError {
    case unsupportedType
    case invalidFile
    case missingLicense
    case invalidDimensions
    case decodeFailed
    case renderFailed

    var errorDescription: String? {
        switch self {
        case .unsupportedType: return "仅支持 PNG、JPEG、BMP 或 GIF。"
        case .invalidFile: return "图片不存在、为空、不是普通文件或超过 20 MiB。"
        case .missingLicense: return "同目录缺少有效的 LICENSE、LICENSE.txt 或同名 .license.txt。"
        case .invalidDimensions: return "图片尺寸必须在 1×1 到 4096×4096 之间。"
        case .decodeFailed: return "无法解码图片第一帧。"
        case .renderFailed: return "无法生成 112×112 RGB565 桌宠资源。"
        }
    }
}

enum MacPetAssetImporter {
    private static let maximumImageBytes = 20 * 1_024 * 1_024
    private static let maximumLicenseBytes = 64 * 1_024
    private static let supportedExtensions: Set<String> = ["png", "jpg", "jpeg", "bmp", "gif"]

    static func load(_ url: URL) throws -> MacPetAsset {
        guard supportedExtensions.contains(url.pathExtension.lowercased()) else {
            throw MacPetAssetError.unsupportedType
        }
        guard validRegularFile(url, maximumBytes: maximumImageBytes) else {
            throw MacPetAssetError.invalidFile
        }
        guard let license = licenseFile(for: url) else {
            throw MacPetAssetError.missingLicense
        }
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
              CGImageSourceGetCount(source) > 0 else { throw MacPetAssetError.decodeFailed }
        guard let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue,
              let height = (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue,
              validDimensions(width: width, height: height) else {
            throw MacPetAssetError.invalidDimensions
        }
        guard let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            throw MacPetAssetError.decodeFailed
        }
        guard let data = MacRgb565Renderer.renderImage(image, width: 112, height: 112),
              data.count == 112 * 112 * 2 else { throw MacPetAssetError.renderFailed }
        return MacPetAsset(data: data, licenseURL: license)
    }

    static func validDimensions(width: Int, height: Int) -> Bool {
        (1...4_096).contains(width) && (1...4_096).contains(height)
    }

    static func licenseFile(for image: URL) -> URL? {
        let directory = image.deletingLastPathComponent()
        let base = image.deletingPathExtension().lastPathComponent
        let candidates = [
            directory.appendingPathComponent("LICENSE", isDirectory: false),
            directory.appendingPathComponent("LICENSE.txt", isDirectory: false),
            directory.appendingPathComponent(base + ".license.txt", isDirectory: false)
        ]
        return candidates.first {
            validRegularFile($0, maximumBytes: maximumLicenseBytes)
        }
    }

    private static func validRegularFile(_ url: URL, maximumBytes: Int) -> Bool {
        guard let values = try? url.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey]),
              values.isRegularFile == true, let size = values.fileSize else { return false }
        return size > 0 && size <= maximumBytes
    }
}

extension MacRgb565Renderer {
    static func renderImage(_ image: CGImage, width: Int, height: Int) -> Data? {
        guard width > 0, height > 0 else { return nil }
        let bytesPerRow = width * 4
        var rgba = [UInt8](repeating: 0, count: bytesPerRow * height)
        return rgba.withUnsafeMutableBytes { buffer -> Data? in
            guard let context = CGContext(
                data: buffer.baseAddress, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: bytesPerRow,
                space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue |
                    CGBitmapInfo.byteOrder32Big.rawValue) else { return nil }
            let pixels = buffer.bindMemory(to: UInt8.self)
            context.setFillColor(red: 0, green: 0, blue: 0, alpha: 1)
            context.fill(CGRect(x: 0, y: 0, width: CGFloat(width), height: CGFloat(height)))
            context.interpolationQuality = .high
            let scale = min(CGFloat(width) / CGFloat(image.width),
                            CGFloat(height) / CGFloat(image.height))
            let targetWidth = CGFloat(image.width) * scale
            let targetHeight = CGFloat(image.height) * scale
            let target = CGRect(x: (CGFloat(width) - targetWidth) / 2,
                                y: (CGFloat(height) - targetHeight) / 2,
                                width: targetWidth, height: targetHeight)
            context.draw(image, in: target)

            var result = Data(capacity: width * height * 2)
            for outputY in 0..<height {
                let sourceY = height - 1 - outputY
                for x in 0..<width {
                    let offset = sourceY * bytesPerRow + x * 4
                    let value = rgb565(red: pixels[offset], green: pixels[offset + 1],
                                       blue: pixels[offset + 2])
                    result.append(UInt8(truncatingIfNeeded: value))
                    result.append(UInt8(truncatingIfNeeded: value >> 8))
                }
            }
            return result
        }
    }
}
