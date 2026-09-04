import AppKit
import Foundation

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let reader = SessionActivityReader()
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private var server: HTTPStatusServer?
    private var timer: Timer?
    private var port: UInt16 = 8765

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem.button?.title = "AI-bot"
        statusItem.menu = buildMenu()
        let storedPort = UserDefaults.standard.integer(forKey: "status_port")
        port = UInt16((1...65_535).contains(storedPort) ? storedPort : 8765)
        server = HTTPStatusServer(port: port, token: PairingTokenStore.read, snapshot: reader.json)
        do { try server?.start() } catch { show("LAN 服务启动失败", error.localizedDescription) }
        updateTitle()
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.updateTitle() }
    }

    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
        server?.stop()
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("查看本机状态", #selector(showStatus)))
        menu.addItem(item("设置配对令牌…", #selector(setPairingToken)))
        menu.addItem(item("复制 LAN 服务地址", #selector(copyAddress)))
        menu.addItem(.separator())
        menu.addItem(item("退出", #selector(quit), key: "q"))
        return menu
    }

    private func item(_ title: String, _ action: Selector, key: String = "") -> NSMenuItem {
        let value = NSMenuItem(title: title, action: action, keyEquivalent: key)
        value.target = self
        return value
    }

    private func updateTitle() {
        let snapshot = reader.capture()
        statusItem.button?.title = "C:\(short(snapshot.codex.state)) A:\(short(snapshot.claude.state))"
    }

    private func short(_ state: String) -> String {
        state == "working" ? "W" : state == "idle" ? "I" : "-"
    }

    @objc private func showStatus() {
        let snapshot = reader.capture()
        show("AI-bot 状态", "Codex: \(snapshot.codex.state)\nClaude: \(snapshot.claude.state)\n端口: \(port)")
    }

    @objc private func setPairingToken() {
        let alert = NSAlert()
        alert.messageText = "设置设备配对令牌"
        alert.informativeText = "令牌至少 32 个字符，仅保存到当前用户 Keychain。"
        alert.addButton(withTitle: "保存")
        alert.addButton(withTitle: "取消")
        let field = NSSecureTextField(frame: NSRect(x: 0, y: 0, width: 360, height: 24))
        alert.accessoryView = field
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        let token = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard token.utf8.count >= 32 else { show("未保存", "令牌必须至少 32 个字符。"); return }
        do { try PairingTokenStore.save(token); show("已保存", "配对令牌已写入 Keychain。") }
        catch { show("保存失败", "Keychain 返回错误。") }
    }

    @objc private func copyAddress() {
        let address = Host.current().addresses.first { value in
            let parts = value.split(separator: ".").compactMap { Int($0) }
            return parts.count == 4 && (parts[0] == 10 || parts[0] == 192 && parts[1] == 168 ||
                parts[0] == 172 && parts[1] >= 16 && parts[1] <= 31)
        } ?? "127.0.0.1"
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString("http://\(address):\(port)/status", forType: .string)
    }

    @objc private func quit() { NSApp.terminate(nil) }

    private func show(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.runModal()
    }
}
