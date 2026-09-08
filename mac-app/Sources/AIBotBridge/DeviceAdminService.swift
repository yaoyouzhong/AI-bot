import Foundation

enum PrivateIPv4Address {
    static func isValid(_ value: String) -> Bool {
        let parts = value.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 4 else { return false }
        let octets = parts.compactMap { part -> UInt8? in
            guard let value = UInt8(part), String(value) == String(part) else { return nil }
            return value
        }
        guard octets.count == 4 else { return false }
        return octets[0] == 10 || octets[0] == 192 && octets[1] == 168 ||
            octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31
    }
}

struct DeviceInfo: Decodable {
    static func fallbackPassed(before: DeviceInfo, during: DeviceInfo, after: DeviceInfo) -> Bool {
        guard let beforeUsb = before.usbStatusCount, let duringUsb = during.usbStatusCount,
              let afterUsb = after.usbStatusCount, let beforeLan = before.lanStatusCount,
              let duringLan = during.lanStatusCount, let beforeTime = before.uptimeMs,
              let duringTime = during.uptimeMs, let afterTime = after.uptimeMs else { return false }
        return before.usbActive && !during.usbActive && during.bridgeOnline &&
            beforeUsb == duringUsb && duringLan > beforeLan && duringTime >= beforeTime &&
            after.usbActive && afterUsb > duringUsb && afterTime >= duringTime
    }

    let device: String
    let version: Int
    let ip: String
    let usbActive: Bool
    let bridgeOnline: Bool
    let mode: String
    let brightness: Int
    let uptimeMs: UInt32?
    let usbStatusCount: UInt32?
    let lanStatusCount: UInt32?

    enum CodingKeys: String, CodingKey {
        case device, version, ip, mode, brightness
        case usbActive = "usb_active"
        case bridgeOnline = "bridge_online"
        case uptimeMs = "uptime_ms"
        case usbStatusCount = "usb_status_count"
        case lanStatusCount = "lan_status_count"
    }
}

final class DeviceAdminService {
    private let serial: SerialBridge?
    private let host: () -> String?
    private let token: () -> String?
    private let session: URLSession

    init(host: @escaping () -> String?, token: @escaping () -> String?,
         session: URLSession? = nil, serial: SerialBridge? = nil) {
        self.serial = serial
        self.host = host
        self.token = token
        self.session = session ?? Self.makeSession()
    }

    func fetchInfo() async throws -> DeviceInfo {
        if let serial {
            let reply = try await serial.requestDevice(type: "device_info_request", replyType: "device_info")
            guard reply.ok, let info = reply.data, info.device == "AI-bot", info.version == 1,
                  (0...100).contains(info.brightness), SerialBridge.displayModes.contains(info.mode) else {
                throw DeviceAdminError.invalidResponse
            }
            return info
        }
        let response = try await perform(path: "/api/info", method: "GET")
        guard let info = try? JSONDecoder().decode(DeviceInfo.self, from: response.data),
              info.device == "AI-bot", info.version == 1, info.ip == response.host,
              PrivateIPv4Address.isValid(info.ip), (0...100).contains(info.brightness),
              SerialBridge.displayModes.contains(info.mode) else {
            throw DeviceAdminError.invalidResponse
        }
        return info
    }

    func resetWiFi() async throws {
        if let serial {
            let reply = try await serial.requestDevice(type: "reset_wifi", replyType: "reset_wifi_ack", confirm: true)
            guard reply.ok else { throw DeviceAdminError.invalidResponse }
            return  // Never retry a destructive request over HTTP after a missing USB ACK.
        }
        let result = try await perform(path: "/reset-wifi", method: "POST")
        guard let response = try? JSONDecoder().decode(ResetResponse.self, from: result.data),
              response.ok, response.restarting else { throw DeviceAdminError.invalidResponse }
    }

    static func makeRequest(host: String, token: String, path: String,
                            method: String) throws -> URLRequest {
        let allowedEndpoint = path == "/api/info" && method == "GET" ||
            path == "/reset-wifi" && method == "POST"
        guard PrivateIPv4Address.isValid(host), token.utf8.count >= 32,
              allowedEndpoint else {
            throw DeviceAdminError.invalidTarget
        }
        var components = URLComponents()
        components.scheme = "http"
        components.host = host
        components.port = 80
        components.path = path
        guard let url = components.url, url.host == host else { throw DeviceAdminError.invalidTarget }
        var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData,
                                 timeoutInterval: 3)
        request.httpMethod = method
        request.setValue(token, forHTTPHeaderField: "X-AIBot-Token")
        return request
    }

    private func perform(path: String, method: String) async throws -> (data: Data, host: String) {
        guard let host = host(), let token = token() else { throw DeviceAdminError.unavailable }
        let request = try Self.makeRequest(host: host, token: token, path: path, method: method)
        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200,
                  data.count <= 32_768 else { throw DeviceAdminError.invalidResponse }
            return (data, host)
        } catch let error as DeviceAdminError {
            throw error
        } catch {
            throw DeviceAdminError.connectionFailed
        }
    }

    private static func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 3
        configuration.timeoutIntervalForResource = 5
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        return URLSession(configuration: configuration,
                          delegate: DeviceAdminSessionDelegate(), delegateQueue: nil)
    }

    private struct ResetResponse: Decodable {
        let ok: Bool
        let restarting: Bool
    }
}

private final class DeviceAdminSessionDelegate: NSObject, URLSessionTaskDelegate {
    func urlSession(_ session: URLSession, task: URLSessionTask,
                    willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest,
                    completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(nil)
    }
}

enum DeviceAdminError: LocalizedError {
    case unavailable
    case invalidTarget
    case connectionFailed
    case invalidResponse
    case unconfirmed

    var errorDescription: String? {
        switch self {
        case .unavailable: return "设备尚未通过 USB 握手，或配对令牌不可用。"
        case .invalidTarget: return "设备没有提供有效的私有局域网地址。"
        case .connectionFailed: return "无法连接设备管理接口。"
        case .invalidResponse: return "设备管理接口返回了无效响应。"
        case .unconfirmed: return "USB 请求未获确认，请检查固件版本；若为重置，请先观察设备，不会自动重试。"
        }
    }
}
