# macOS bridge rules

- `Sources/AIBotBridge/` contains the independently implemented menu-bar bridge. Do not copy Swift source or image assets from the earlier project.
- Use public macOS frameworks only. The minimum deployment target is macOS 13.
- The LAN status listener must require the same `X-AIBot-Token` used by firmware and must not expose tokens in status bodies, logs, defaults, or diagnostics.
- Persist pairing secrets only in Keychain. Non-secret preferences may use `UserDefaults`.
- Background Claude/Codex activity remains file-metadata-only. The separately opt-in TAB5 Codex feature may read public assistant replies and exchange encrypted device-bound drafts/photos after explicit user actions through the local Codex App Server. Sending has a second opt-in. Never export raw rollouts, reasoning, credentials or tool output, or auto-approve requests. This scope follows the user's authorization to implement all five Mac TAB5 stages; physical acceptance is deferred to the next daytime session.
- Keep optional status fields wire-compatible with `docs/PROTOCOL.md`. Missing Mac capabilities must be omitted, not estimated.
- Run `swift test --package-path mac-app` and `swift build -c release --package-path mac-app` on macOS. Windows static review is not build evidence.
- Music or Apple Events changes must also run `bash scripts/build_macos_app.sh`, launch the generated app bundle, and verify the Automation prompt plus play/pause behavior.
- No vendor logos, legacy screenshots, sprites, generated binaries, `.build/`, runtime caches, or signing credentials may be committed.
- 调试时默认让桥接在后台运行，避免设置、连接和诊断窗口遮挡 Codex。优先使用已有日志、隔离检查及不抢焦点的诊断；只有必须交互时才打开相应窗口，完成后立即关闭，不同时堆叠多个窗口。关闭后不要为了核验关闭结果再次激活桥接而重新打开设备中心。密码及权限确认仍由用户在本机完成。
