import AppKit

/// Mirrors the Windows enable/credential/component layout. The test window is
/// a separate diagnostic, not the saved device feature's main entry point.
final class MacUnlockSettingsWindow:NSWindowController,NSWindowDelegate {
    private let service:MacTab5Service
    private let enabled = NSButton(checkboxWithTitle:"允许 TAB5 通过 Wi-Fi 一键解锁",target:nil,action:nil)
    private let localEnabled = NSButton(checkboxWithTitle:"允许 TAB5 通过 USB／蓝牙一键解锁",target:nil,action:nil)
    private let password = NSSecureTextField(), repeated = NSSecureTextField()
    private let status = MacBridgeSettingsStyle.note("")
    private let observations = MacBridgeSettingsStyle.note("")
    private var verification:MacUnlockConnectionWindow?
    private var draftGuard: MacSettingsDraft?
    init(service:MacTab5Service) {
        self.service = service
        super.init(window:MacBridgeSettingsStyle.window("一键解锁",width:620,height:620,minimumHeight:460))
        window?.delegate = self
        let page = MacBridgeSettingsPage(); page.heading("一键解锁",subtitle:"密码仅留在 Mac，TAB5 只发送解锁请求。")
        enabled.state = service.unlock.enabled ? .on:.off; page.add(enabled)
        localEnabled.state = service.unlock.localEnabled ? .on:.off; page.add(localEnabled)
        page.section("Mac 登录密码")
        page.field("新密码",password); page.field("再次输入",repeated)
        page.add(MacBridgeSettingsStyle.note("留空保留原密码。支持英文、数字、符号，最多 63 字节。系统认证和公开键盘验证通过后存入本机钥匙串，仅供当前账户及配对 TAB5 使用。"))
        page.add(MacBridgeSettingsStyle.row([MacSettingsButton("验证并保存密码") { [weak self] in self?.verify() },MacSettingsButton("删除密码") { [weak self] in self?.remove() }]))
        page.section("本机解锁组件")
        page.add(MacBridgeSettingsStyle.note("使用当前账户的本机解锁组件。受保护会话未确认结束时暂停解锁，不退出保护或更改系统设置。"))
        page.add(MacSettingsButton("打开屏幕共享设置") { _ = NSWorkspace.shared.open(URL(string:"x-apple.systempreferences:com.apple.Sharing-Settings.extension")!) })
        page.section("连接与测试")
        page.add(MacBridgeSettingsStyle.note("USB／蓝牙需支持 Mac 解锁的 TAB5 固件，不使用 Windows 数字 PIN。密码不下发；每次只尝试一次，取消、断连、停用或会话变化即停止，不切换通道重试。"))
        page.add(MacSettingsButton("临时解锁测试…") { [weak self] in
            guard let self else { return }; self.verification = MacUnlockConnectionWindow(); self.verification?.open()
        })
        page.add(MacSettingsButton("下一次仅测试字符触发…") { [weak self] in
            guard let self else { return }
            do { try self.service.unlock.armPreparationProbe(pair:self.service.currentPairing); self.refreshDiagnostic() }
            catch { self.status.stringValue = error.localizedDescription }
        })
        page.add(MacBridgeSettingsStyle.note("仅下一次 Wi-Fi 请求有效（2 分钟内）：输入公开字符 a，不使用密码、不按回车或删除。请在空密码框测试，完成后自行清空。"))
        page.add(MacSettingsButton("刷新解锁诊断") { [weak self] in self?.refreshDiagnostic() })
        page.add(observations)
        refreshDiagnostic()
        window?.contentView = MacBridgeSettingsShell(body:page,status:status,actions:[MacSettingsButton("检查配置") { [weak self] in self?.check() },MacSettingsButton("保存") { [weak self] in self?.save() },MacSettingsButton("取消") { [weak self] in self?.window?.performClose(nil) }])
        draftGuard = MacSettingsDraft(window) { [weak self] in
            guard let self else { return "" }; return MacSettingsDraft.values([self.enabled, self.localEnabled, self.password, self.repeated])
        }; window?.delegate = self
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool { draftGuard?.windowShouldClose(sender) ?? true }
    required init?(coder:NSCoder) { fatalError() }
    private func refreshDiagnostic() {
        status.stringValue = service.unlock.diagnostic + "\n" + service.unlock.powerDiagnostic
        observations.stringValue = MacScreenLockObservation.shared.diagnostic + "\n" + service.computer.diagnostic + "\n" + service.unlock.timingDiagnostic + "\n" + service.interactionDiagnostics.summary
    }
    func open() { refreshDiagnostic(); MacSettingsControls.open(self) }
    private func clear() { password.stringValue = ""; repeated.stringValue = "" }
    private func verify() {
        guard let pair = service.currentPairing,pair.confirmed else { clear(); status.stringValue = "请先配对 TAB5。"; return }
        guard !password.stringValue.isEmpty,password.stringValue == repeated.stringValue else { clear(); status.stringValue = "两次密码需一致且不能为空；请重新填写。"; return }
        var bytes = Data(password.stringValue.utf8); clear()
        defer { bytes.resetBytes(in:0..<bytes.count) }
        do { try MacARDConnection.validate(username:Data(NSUserName().utf8),password:bytes); try MacVNCUnlockTest.validate(bytes) }
        catch { status.stringValue = error.localizedDescription; return }
        verification = MacUnlockConnectionWindow(enrollment:true)
        verification?.beginEnrollment(bytes) { [weak self] secret in
            guard let self,self.service.currentPairing == pair else { throw MacTab5Error.identity }
            try self.service.unlock.saveVerified(secret,pair:pair)
            self.status.stringValue = "密码已验证并保存；选择允许的解锁通道后点保存。"
        }
    }
    private func save() {
        guard password.stringValue.isEmpty,repeated.stringValue.isEmpty else { clear(); status.stringValue = "新密码尚未验证，请重新填写并点击“验证并保存密码”。"; return }
        do { try service.unlock.setPermissions(wifi:enabled.state == .on,local:localEnabled.state == .on,pair:service.currentPairing); status.stringValue = service.unlock.diagnostic; draftGuard?.markSaved() }
        catch { status.stringValue = error.localizedDescription }
    }
    private func remove() {
        let alert = NSAlert(); alert.messageText = "删除本机保存的解锁密码？"; alert.informativeText = "同时关闭 Wi-Fi、USB 和蓝牙一键解锁，设备配对保留。"; alert.addButton(withTitle:"删除"); alert.addButton(withTitle:"取消")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        clear(); enabled.state = .off; localEnabled.state = .off
        do { try service.unlock.delete(); status.stringValue = service.unlock.diagnostic } catch { status.stringValue = "已停用；钥匙串删除未确认，请重试。" }
    }
    private func check() {
        status.stringValue = "正在检查本机钥匙串配置…"
        DispatchQueue.global(qos:.utility).async { [weak self] in
            self?.service.unlock.load(force:true)
            DispatchQueue.main.async { [weak self] in
                guard let self else { return }
                let configured = self.service.unlock.configured(self.service.currentPairing) || self.service.unlock.configured(self.service.currentPairing,transport:"USB")
                self.status.stringValue = configured ? "已配置 · "+self.service.unlock.diagnostic:self.service.unlock.diagnostic+"；需当前配对密码及开启开关。"
            }
        }
    }
    override func close() { clear(); super.close() }
    func windowWillClose(_ notification:Notification) { clear(); verification?.window?.performClose(nil) }
}
