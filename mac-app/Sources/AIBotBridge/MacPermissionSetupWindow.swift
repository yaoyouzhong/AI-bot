import AppKit

@MainActor
final class MacPermissionSetupWindow: NSWindowController, NSWindowDelegate {
    typealias Step = MacPermissionSetup.Step
    typealias State = MacPermissionSetup.State

    private let readState: (Step) async -> State
    private let request: (Step, State) async -> Void
    private let page = MacBridgeSettingsPage()
    private let closeButton = NSButton(title: "稍后完成", target: nil, action: nil)
    private let revealAppButton = NSButton(title: "在 Finder 中显示当前 App", target: nil, action: nil)
    private let notificationSettingsButton = NSButton(title: "打开通知设置…", target: nil, action: nil)
    private let subtitle = MacBridgeSettingsStyle.note("")
    private let progress = MacBridgeSettingsStyle.note("")
    private var rows: [Step: (view: NSView, detail: NSTextField, status: NSTextField, button: NSButton)] = [:]
    private var visibleSteps = Step.allCases
    private var notificationRequestPending = false
    private var offerNotificationAfterGuide = false
    private var notificationTask: Task<Void, Never>?
    private var activeStep: Step?
    private var followUpAccessibilityRequest = false
    private var states: [Step: State] = [:]
    private var timer: Timer?
    private var refreshTask: Task<Void, Never>?
    private var requestTask: Task<Void, Never>?
    var onIdle: (() -> Void)?

    init(readState: @escaping (Step) async -> State, request: @escaping (Step, State) async -> Void) {
        self.readState = readState
        self.request = request
        let window = MacBridgeSettingsStyle.window("系统授权", width: 680, height: 650, minimumHeight: 470)
        super.init(window: window)
        window.delegate = self
        window.contentView = MacBridgeSettingsShell(body: page, status: progress,
                                                    actions: [closeButton, revealAppButton, notificationSettingsButton])
        progress.identifier = .init("permission-summary")
        page.heading("系统授权")
        page.add(subtitle)
        configurePresentation()
        for (index, step) in Step.allCases.enumerated() {
            let status = MacBridgeSettingsStyle.note("")
            status.identifier = .init("permission-status-\(step)")
            let detail = MacBridgeSettingsStyle.note(step.detail)
            let button = NSButton(title: "授权…", target: self, action: #selector(allow(_:)))
            button.tag = index
            button.identifier = .init("permission-\(step)")
            style(button)
            let labels = NSStackView(views: [MacBridgeSettingsStyle.label(step.title, weight: .semibold), detail])
            labels.orientation = .vertical
            labels.alignment = .leading
            labels.spacing = 3
            labels.setContentHuggingPriority(.defaultLow, for: .horizontal)
            status.setContentCompressionResistancePriority(.required, for: .horizontal)
            button.setContentCompressionResistancePriority(.required, for: .horizontal)
            let row = MacBridgeSettingsStyle.row([labels, status, button])
            page.add(row)
            labels.widthAnchor.constraint(equalTo: row.widthAnchor, multiplier: 0.53).isActive = true
            status.widthAnchor.constraint(equalToConstant: 112).isActive = true
            rows[step] = (row, detail, status, button)
        }
        style(closeButton)
        closeButton.target = self; closeButton.action = #selector(dismiss)
        closeButton.keyEquivalent = "\u{1b}"
        style(revealAppButton)
        revealAppButton.target = self; revealAppButton.action = #selector(revealCurrentApp)
        style(notificationSettingsButton)
        notificationSettingsButton.identifier = .init("permission-notification-settings")
        notificationSettingsButton.target = self; notificationSettingsButton.action = #selector(openNotificationSettings)
        display([:])
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate(); refreshTask?.cancel(); requestTask?.cancel(); notificationTask?.cancel() }

    private func style(_ button: NSButton) {
        button.controlSize = .large
        button.bezelStyle = .rounded
        button.heightAnchor.constraint(greaterThanOrEqualToConstant: 32).isActive = true
    }

    func configurePresentation(mode: MacPermissionSetup.LaunchMode? = nil, steps: [Step] = Step.allCases) {
        let upgrade = mode == .upgrade
        visibleSteps = upgrade ? steps : Step.allCases
        subtitle.stringValue = upgrade
            ? "这些权限需要在升级后重新确认。也可稍后从「设备中心 → 桥接设置」补办。"
            : "请依次确认 macOS 提示，并按指引在系统设置中开启辅助功能。也可稍后从「设备中心 → 桥接设置」补办。"
        for (step, row) in rows { row.view.isHidden = !visibleSteps.contains(step) }
    }

    func open(initial: [Step: State]? = nil, steps: [Step] = Step.allCases, mode: MacPermissionSetup.LaunchMode? = nil) {
        configurePresentation(mode: mode, steps: steps)
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        if let initial, let mode {
            offerNotificationAfterGuide = steps.contains(.notifications) && initial[.notifications] == .request
            display(initial)
            run { [weak self] in
                guard let self else { return }
                await MacPermissionSetup.runInitial(steps: steps, mode: mode, state: self.readState) { step, state in
                    await self.perform(step, state)
                }
            }
        } else { refresh() }
        if timer == nil {
            timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
                Task { @MainActor in self?.refresh() }
            }
        }
    }

    func windowWillClose(_ notification: Notification) {
        timer?.invalidate(); timer = nil
        refreshTask?.cancel()
        followUpAccessibilityRequest = false
        offerNotificationAfterGuide = false
        // Cancel remaining steps; the current macOS dialog belongs to the OS.
        requestTask?.cancel()
    }

    func windowDidBecomeKey(_ notification: Notification) {
        refresh()
    }

    func resumeIfOpen() -> Bool {
        guard let window, window.isVisible || window.isMiniaturized else { return false }
        if window.isMiniaturized { window.deminiaturize(nil) }
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        refresh()
        return true
    }

    private func refresh() {
        guard refreshTask == nil, requestTask == nil else { return }
        refreshTask = Task { [weak self] in
            guard let self else { return }
            var next: [Step: State] = [:]
            for step in self.visibleSteps {
                guard !Task.isCancelled else { break }
                next[step] = await self.readState(step)
            }
            let canApply = !Task.isCancelled
            if canApply { self.display(next) }
            self.refreshTask = nil
            if canApply, self.followUpAccessibilityRequest {
                if next[.accessibility] == .allowed { self.followUpAccessibilityRequest = false }
                if next[.accessibility] == .request {
                    // Accessibility is now trusted. Ask for event posting once
                    // if macOS still requires it, without waiting on Settings.
                    self.followUpAccessibilityRequest = false
                    self.run { [weak self] in await self?.perform(.accessibility, .request) }
                }
            }
            if canApply { self.offerNotificationIfReady() }
        }
    }

    func display(_ states: [Step: State], active: Step? = nil) {
        self.states = states
        activeStep = active
        for (step, row) in rows {
            let state = states[step] ?? .checking
            switch (step, state) {
            case (.accessibility, .conflictingCopy):
                row.status.stringValue = state.caption
                row.detail.stringValue = "「应用程序」中还有另一份桥接。请先安装要用的版本，从那里启动后再授权。"
            case (.accessibility, .settings):
                row.status.stringValue = "当前 App 未授权"
                row.detail.stringValue = "在系统设置的「辅助功能」中开启桥接。若已开启仍未生效，用「−」仅移除列表里的旧条目，再用「＋」添加下方 Finder 按钮显示的 App。返回此页可查看结果。"
                row.button.title = "授权当前 App…"
            case (.accessibility, .request):
                row.status.stringValue = "已授权，输入待确认"
                row.detail.stringValue = "确认输入权限后，设备即可操作 Mac 上的应用。"
                row.button.title = "确认输入权限…"
            case (.accessibility, _):
                row.status.stringValue = state.caption
                row.detail.stringValue = step.detail
                row.button.title = "授权…"
            case (.systemEvents, .openApp):
                row.status.stringValue = active == step ? "正在检查" : "尚未检查"
                row.detail.stringValue = step.detail + "尚未检查不代表未授权。"
                row.button.title = "检查状态…"
            case (.systemEvents, .request):
                row.status.stringValue = active == step ? "请确认系统提示" : state.caption
                row.detail.stringValue = step.detail
                row.button.title = "授权系统事件…"
            case (.notifications, .request):
                row.status.stringValue = notificationTask != nil ? "请到右上角确认" :
                    notificationRequestPending ? "尚未收到系统确认" : state.caption
                row.detail.stringValue = "允许后可接收任务和更新提醒。请在屏幕右上角的系统提示中点「允许」。"
                row.button.title = notificationRequestPending ? "重试通知授权…" : "允许通知…"
            case (.notifications, .settings):
                row.status.stringValue = state.caption
                row.detail.stringValue = step.detail
                row.button.title = "打开通知设置…"
            default:
                if step == .systemEvents || step == .notifications { row.detail.stringValue = step.detail }
                row.status.stringValue = active == step ? "请确认系统提示" :
                    step == .localNetwork && state == .settings ? "由系统管理" : state.caption
                row.button.title = state == .openApp ? "打开并授权…" : state == .settings ? "打开系统设置…" : "授权…"
            }
            row.button.setAccessibilityLabel(row.button.title + " " + step.title)
            row.button.isHidden = !state.needsPermission
            row.button.isEnabled = state.needsPermission && requestTask == nil && (step != .notifications || notificationTask == nil)
        }
        let systemEventsUnchecked = visibleSteps.contains(.systemEvents) && states[.systemEvents] == .openApp
        let notificationOptional = visibleSteps.contains(.notifications) && states[.notifications]?.needsAttention == true
        let pending = visibleSteps.filter {
            $0 != .localNetwork && $0 != .notifications && ($0 != .systemEvents || !systemEventsUnchecked) &&
                states[$0]?.needsAttention == true
        }.count
        let summary = pending > 0 ? "还有 \(pending) 项可处理。" :
            systemEventsUnchecked ? "系统事件尚未检查，可按需检查。" :
            notificationOptional ? "通知可单独处理，不影响其他权限。" : "已检查当前可用的权限。"
        progress.stringValue = active.map { "正在处理：\($0.title)" } ?? summary
        closeButton.title = pending > 0 || active != nil ? "稍后完成" : systemEventsUnchecked || notificationOptional ? "关闭" : "完成"
        revealAppButton.isHidden = !visibleSteps.contains(.accessibility) || (states[.accessibility] != .settings && states[.accessibility] != .conflictingCopy)
            || (active != nil && active != .accessibility)
        notificationSettingsButton.isHidden = !visibleSteps.contains(.notifications) || states[.notifications] != .request
        page.needsLayout = true
        if let active, let row = rows[active]?.view {
            page.layoutSubtreeIfNeeded()
            row.scrollToVisible(row.bounds)
        }
    }

    private func run(_ operation: @escaping () async -> Void) {
        guard requestTask == nil else { return }
        refreshTask?.cancel()
        requestTask = Task { [weak self] in
            await operation()
            guard let self else { return }
            if Task.isCancelled { self.offerNotificationAfterGuide = false }
            self.requestTask = nil
            self.onIdle?()
            if self.window?.isVisible == true {
                self.display(self.states)
                self.offerNotificationIfReady()
                self.refresh()
            }
        }
    }

    private func perform(_ step: Step, _ state: State) async {
        if step == .accessibility, state == .settings { followUpAccessibilityRequest = true }
        display(states, active: step)
        await request(step, state)
        guard !Task.isCancelled else { return }
        var next = states
        next[step] = await readState(step)
        display(next)
    }

    @objc private func allow(_ sender: NSButton) {
        let step = Step.allCases[sender.tag]
        if step == .notifications {
            requestNotificationSeparately()
            return
        }
        run { [weak self] in
            guard let self else { return }
            let state = await self.readState(step)
            guard !Task.isCancelled, state.needsPermission else { return }
            await self.perform(step, state)
        }
    }
    private func offerNotificationIfReady() {
        guard offerNotificationAfterGuide, requestTask == nil, !followUpAccessibilityRequest,
              window?.isVisible == true else { return }
        requestNotificationSeparately(manual: false)
    }

    private func requestNotificationSeparately(manual: Bool = true) {
        guard notificationTask == nil, requestTask == nil else { return }
        offerNotificationAfterGuide = false
        notificationTask = Task { [weak self] in
            guard let self else { return }
            let state = await self.readState(.notifications)
            if !Task.isCancelled, manual || self.window?.isVisible == true,
               state == .request || (manual && state == .settings) {
                await self.request(.notifications, state)
            }
            let result = await self.readState(.notifications)
            self.notificationTask = nil
            self.notificationRequestPending = state == .request && result == .request
            guard self.window?.isVisible == true else { return }
            var next = self.states
            next[.notifications] = result
            self.display(next, active: self.activeStep)
        }
        display(states, active: activeStep)
    }
    @objc private func openNotificationSettings() { MacPermissionSetup.openSettings(.notifications) }
    @objc private func revealCurrentApp() {
        NSWorkspace.shared.activateFileViewerSelecting([Bundle.main.bundleURL])
    }
    @objc private func dismiss() { close() }
}
