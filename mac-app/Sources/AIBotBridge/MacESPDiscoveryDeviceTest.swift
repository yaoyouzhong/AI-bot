import Darwin
import Foundation

// Hold an unused TCP port for the whole test. The device's saved endpoint then
// refuses connections and can recover only through authenticated UDP discovery.
private final class MacESPUnusedPort {
    let descriptor: Int32
    let port: UInt16
    init() throws {
        let fd = socket(AF_INET,SOCK_STREAM,0)
        guard fd >= 0 else { throw DeviceAdminError.unavailable }
        var address = sockaddr_in(); address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size); address.sin_family = sa_family_t(AF_INET)
        let bound = withUnsafePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { Darwin.bind(fd,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        var size = socklen_t(MemoryLayout<sockaddr_in>.size)
        let read = withUnsafeMutablePointer(to:&address) { $0.withMemoryRebound(to:sockaddr.self,capacity:1) { getsockname(fd,$0,&size) } }
        guard bound == 0, read == 0, address.sin_port != 0 else { Darwin.close(fd); throw DeviceAdminError.unavailable }
        descriptor = fd; port = UInt16(bigEndian:address.sin_port)
    }
    deinit { Darwin.close(descriptor) }
}

enum MacESPDiscoveryDeviceTest {
    static func run(serial: SerialBridge, service: DeviceAdminService, discovery: MacESPDiscovery) async throws -> String {
        let reserved = try MacESPUnusedPort()
        let initial = try await service.fetchInfo(), replies = discovery.responseCount
        guard initial.usbActive else { throw DeviceAdminError.unconfirmed }
        guard serial.provisionLan(port:reserved.port,pauseAfter:true) else { throw DeviceAdminError.connectionFailed }
        var restoreNeeded = true
        defer { serial.resumeTransmission(); if restoreNeeded { _ = serial.provisionLan() } }
        let before = try await service.fetchInfo()
        guard before.usbActive, let usb = before.usbStatusCount, let lan = before.lanStatusCount, let uptime = before.uptimeMs else { throw DeviceAdminError.unconfirmed }
        var recovered: DeviceInfo?
        for _ in 0..<20 {
            try await Task.sleep(nanoseconds:2_000_000_000)
            try serial.pauseTransmission()
            let now = try await service.fetchInfo()
            guard now.usbStatusCount == usb, (now.uptimeMs ?? 0) >= uptime else { throw DeviceAdminError.unconfirmed }
            if !now.usbActive, now.bridgeOnline, (now.lanStatusCount ?? 0) > lan, discovery.responseCount > replies { recovered = now; break }
        }
        serial.resumeTransmission()
        guard serial.provisionLan() else { throw DeviceAdminError.connectionFailed }
        restoreNeeded = false
        try await Task.sleep(nanoseconds:3_000_000_000)
        let after = try await service.fetchInfo()
        guard let recovered, after.usbActive, (after.usbStatusCount ?? 0) > usb, (after.uptimeMs ?? 0) >= (recovered.uptimeMs ?? UInt32.max) else { throw DeviceAdminError.unconfirmed }
        return "认证发现、Wi-Fi 回退和 USB 恢复均通过。\n发现回复增加 \(discovery.responseCount-replies)，Wi-Fi 状态增加 \((recovered.lanStatusCount ?? lan)-lan)。\n已恢复原电脑地址及端口；未改变网络、密钥和展示设置。\n本项模拟旧端口不可达，不等同于实际修改电脑 IP。"
    }
}
