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
    private let subtitle = MacBridgeSettingsStyle.note("")
    private let progress = MacBridgeSettingsStyle.note("")
    private var rows: [Step: (view: NSView, status: NSTextField, button: NSButton)] = [:]
    private var visibleSteps = Step.allCases
    private var notificationRequestPending = false
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
        window.contentView = MacBridgeSettingsShell(body: page, status: progress, actions: [closeButton, revealAppButton])
        page.heading("系统授权")
        page.add(subtitle)
        configurePresentation()
        for (index, step) in Step.allCases.enumerated() {
            let status = MacBridgeSettingsStyle.note("")
            let button = NSButton(title: "授权…", target: self, action: #selector(allow(_:)))
            button.tag = index
            button.identifier = .init("permission-\(step)")
            style(button)
            let labels = NSStackView(views: [MacBridgeSettingsStyle.label(step.title, weight: .semibold),
                                            MacBridgeSettingsStyle.note(step.detail)])
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
            rows[step] = (row, status, button)
        }
        style(closeButton)
        closeButton.target = self; closeButton.action = #selector(dismiss)
        closeButton.keyEquivalent = "\u{1b}"
        style(revealAppButton)
        revealAppButton.target = self; revealAppButton.action = #selector(revealCurrentApp)
        display([:])
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { timer?.invalidate(); refreshTask?.cancel(); requestTask?.cancel() }

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
            : "请依次确认 macOS 提示，最后在系统设置中开启辅助功能。也可稍后从「设备中心 → 桥接设置」补办。"
        for (step, row) in rows { row.view.isHidden = !visibleSteps.contains(step) }
    }

    func open(initial: [Step: State]? = nil, steps: [Step] = Step.allCases, mode: MacPermissionSetup.LaunchMode? = nil) {
        configurePresentation(mode: mode, steps: steps)
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        if let initial, let mode {
            display(initial)
            run { [weak self] in
                guard let self else { return }
                await MacPermissionSetup.runInitial(steps: steps, mode: mode, state: self.readState) { step, state in
                    // On upgrade, go straight to Settings. The OS's separate
                    // invitation to open Settings can remain after access is granted.
                    await self.perform(step, state, promptForAccessibility: mode == .firstLaunch)
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
        // Cancel remaining steps; the current macOS dialog belongs to the OS.
        requestTask?.cancel()
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
        }
    }

    func display(_ states: [Step: State], active: Step? = nil) {
        self.states = states
        for (step, row) in rows {
            let state = states[step] ?? .checking
            let retry = step == .notifications && state == .request && notificationRequestPending
            row.status.stringValue = active == step ? (step == .accessibility ? "请在系统设置中开启" : "请确认系统提示") :
                (retry ? "尚未收到系统确认" :
                    step == .localNetwork && state == .settings ? "由系统管理" :
                    step == .accessibility && state.needsPermission ? "权限未生效" : state.caption)
            row.button.title = retry ? "重试通知授权…" :
                state == .openApp ? "打开并授权…" : state == .settings ? "打开系统设置…" : "授权…"
            row.button.setAccessibilityLabel(row.button.title + " " + step.title)
            row.button.isHidden = !state.needsPermission
            row.button.isEnabled = state.needsPermission && requestTask == nil
        }
        let pending = visibleSteps.filter { $0 != .localNetwork && states[$0]?.needsPermission == true }.count
        progress.stringValue = active.map { "正在处理：\($0.title)" } ?? (pending == 0 ? "已检查当前可用的权限。" : "还有 \(pending) 项可处理。")
        closeButton.title = pending == 0 && active == nil ? "完成" : "稍后完成"
        revealAppButton.isHidden = !visibleSteps.contains(.accessibility) || states[.accessibility] != .settings
            || (active != nil && active != .accessibility)
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
            self.requestTask = nil
            self.onIdle?()
            if self.window?.isVisible == true {
                self.display(self.states)
                self.refresh()
            }
        }
    }

    private func perform(_ step: Step, _ state: State, promptForAccessibility: Bool = true) async {
        if step == .accessibility, state == .settings { followUpAccessibilityRequest = true }
        display(states, active: step)
        if step == .accessibility, state == .settings, !promptForAccessibility {
            MacPermissionSetup.openSettings(step)
        } else {
            await request(step, state)
        }
        guard !Task.isCancelled else { return }
        var next = states
        next[step] = await readState(step)
        if step == .notifications { notificationRequestPending = next[step] == .request }
        display(next)
    }

    @objc private func allow(_ sender: NSButton) {
        let step = Step.allCases[sender.tag]
        run { [weak self] in
            guard let self else { return }
            let state = await self.readState(step)
            guard !Task.isCancelled, state.needsPermission else { return }
            // A manual click opens Settings in one step, even if macOS no longer
            // shows its Accessibility prompt for this app.
            if state == .settings, step == .accessibility {
                self.followUpAccessibilityRequest = true
                MacPermissionSetup.openSettings(step)
            } else { await self.perform(step, state) }
        }
    }
    @objc private func revealCurrentApp() {
        NSWorkspace.shared.activateFileViewerSelecting([Bundle.main.bundleURL])
    }
    @objc private func dismiss() { close() }
}
