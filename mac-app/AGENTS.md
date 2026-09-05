# macOS bridge rules

- `Sources/AIBotBridge/` contains the independently implemented menu-bar bridge. Do not copy Swift source or image assets from the earlier project.
- Use public macOS frameworks only. The minimum deployment target is macOS 13.
- The LAN status listener must require the same `X-AIBot-Token` used by firmware and must not expose tokens in status bodies, logs, defaults, or diagnostics.
- Persist pairing secrets only in Keychain. Non-secret preferences may use `UserDefaults`.
- Read Claude/Codex activity from file metadata only; never read or transmit conversation text.
- Keep optional status fields wire-compatible with `docs/PROTOCOL.md`. Missing Mac capabilities must be omitted, not estimated.
- Run `swift test --package-path mac-app` and `swift build -c release --package-path mac-app` on macOS. Windows static review is not build evidence.
- Music or Apple Events changes must also run `bash scripts/build_macos_app.sh`, launch the generated app bundle, and verify the Automation prompt plus play/pause behavior.
- No vendor logos, legacy screenshots, sprites, generated binaries, `.build/`, runtime caches, or signing credentials may be committed.
