# ESP8266 精简更新包：Windows / Mac 交接

来源平台为 Windows，接收平台为 macOS。实现和测试随本记录提交到 `codex/component-update-followup-20261110`，基于 `39085bc`。现有 ESP8266 0.5.8 发布页说明已更新；没有创建桥接新版本或替换附件。

两端从同版本正式 ESP8266 发布中优先选择 `AI-bot-esp8266-<version>-flash.zip`。精简包必须有同一发布下匹配的 `.zip.sha256` 和可信下载地址；缺少有效精简包时仍支持原材料包。最新版本优先，不会为选择精简包而降级。下载后沿用文件大小和 SHA-256 校验、刷机前确认流程。固件及协议没有变化。

| 平台 | 实现位置 | 验证及剩余事项 |
| --- | --- | --- |
| Windows | `ComponentUpdateCatalog.cs`、`UpdateService.cs` | Release 构建、`--status-once`、`--self-test-unified-updates` 通过。公开下载验收通过，无刷写。 |
| macOS | `MacUpdateWindow.swift`、`MacReleaseCatalogTests.swift` | 选包逻辑和 XCTest 已编写；在 Mac 上运行 `swift test --package-path mac-app` 和 `swift build -c release --package-path mac-app`，再从更新窗口验证精简 ZIP 下载。 |

Windows 实测命令：`dotnet run --no-build -c Release --project windows-app/AIBotBridge/AIBotBridge.csproj -- --self-test-update-live artifacts/esp8266-compact-update-20261110 esp8266`。成功选择并校验公开的 392,706 字节精简 ZIP；ZIP SHA-256 为 `135727db235e2ef88fc852357598539f236811564da353c86046bd4f759eca06`。本轮未安装新桥接、未执行设备刷写，现有配对、草稿和设备展示设置未改动。正式桥接候选应从同一源码提交在两端原生构建并分别验收。
