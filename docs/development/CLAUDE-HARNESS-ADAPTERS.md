# Claude Harness adapter contract

The Windows candidate uses `ClaudeHarnessAdapters.cs` for VS Code, native terminal and explicit background-session implementations. macOS uses `MacClaudeHarness.swift` for VS Code, read-only native terminals, and Claude Code background sessions. The Mac background adapter enables text and photo sends to a verified idle job on Claude Code 2.1.318. The TAB5 source badge is passive metadata, independent of the actual model/provider, project and control availability.

To integrate another Claude-compatible Harness:

1. Implement `IClaudeHarnessAdapter` with a stable, unique `Source` descriptor. Validate the label/ID limits in `ClaudeSessionSource.Valid`. Register it through `ClaudeHarnessAdapters.Default.With(adapter)` and pass that registry into `ClaudeWorkspace`; ship an intentional compiled integration, not a downloaded module or command string.
2. Return verified session observations from `Discover`, current session UUIDs from `LiveSessions`, and an exact identity-bound `IClaudeNativeSession` from `Resolve`. Return null for sessions the adapter does not own. A title, working directory or source cache is insufficient proof of ownership. Multiple verified owners produce a read-only conflict, never first-match dispatch.
3. Implement the native session's `Source` identically to the descriptor. Default input/append/interrupt capabilities are false. Enable only implemented operations. Revalidate live ownership, scope, authorization and expected turn at dispatch, and reuse existing receipt/idempotency/draft protections. Do not retry delivery-unknown requests.
4. Adapt transcript records and stable identity if the Harness does not use the current Claude JSONL history model. The current seam does not add an arbitrary conversation provider, HTTP endpoint, configuration UI or dynamic plugin loader.
5. Verify discovery, restart, ambiguous ownership, read-only capability, disconnect and identity change using local fixtures before any explicitly authorized real model request. `ClaudeHarnessSelfTest` exercises a custom read-only adapter through the real workspace and rejects duplicate IDs/source mismatch; it sends no messages.

On macOS, construct a `MacClaudeHarnessAdapter` and register it with `MacClaudeHarnesses.adding`, then inject the registry into `MacClaudeWorkspaceBackend`. Returned sessions must match both the requested UUID and the registered descriptor. `allows` defaults to false; only exact native operations may opt in. Accessibility-based clients do not implement a session API.

Confirmed source descriptors are cached in the user's private DPAPI-protected `session-sources.dat` on Windows and the existing Keychain-backed encrypted store on macOS (512 entries, 256 KiB read limit on both). This cache improves label stability and has no role in routing/authorization. A missing or corrupt cache degrades to unknown provenance. Native controls retain their own freshness and revocation rules. Labels may be ellipsized on TAB5; known labels/ASCII use the 20px font and other text falls back to the existing 24px font's supported glyphs.

## 中文接入说明

未来接入时新增一个编译期适配器：登记稳定来源名，发现会话，校验真实承载端身份，再按实际支持情况实现发送、运行中追加和停止。默认仅查看；显示某个 Harness 标签不意味着具备发送能力。已有草稿、送达未知保护与派发前校验继续复用。

macOS 使用 `MacClaudeHarnessAdapter` 和现有钥匙串加密存储，遵循相同的来源、能力和冲突约束。已验证的 Claude Code 2.1.318 后台会话在空闲时可以从 TAB5 发送文字和图片。普通 Terminal/iTerm2 会话可以查看历史。后台接入的操作和回归方法见 [Mac Claude Code 后台会话接入](MAC-CLAUDE-BACKGROUND-20261107.md)。当前入口覆盖 Claude 兼容会话。使用不同历史格式或身份体系的第三方工具还需要对应的数据映射。此次未接入实际第三方工具，也未提供通用插件安装器。ESP8266 协议与显示未修改。
