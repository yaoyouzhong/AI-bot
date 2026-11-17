# Mac / Windows 最终开发交接：软件固件入口、会话兼容与 TAB5（2026-11-17）

- **同一开发分支**：`codex/tab5-calendar-and-claude-idle-20261116`。Windows 上游 `4d4051fd4c09d2f3a6840e14474b41be0f4444fc` 已合入；合并后的验收源码为 **`c2e9b998e09b023f6313691c7b7f348d2c409669`**。交接文档提交在其后，不改变验收源码。Windows 从 `origin` 的同一开发分支拉取。
- **交付范围**：仅开发交接。桥接组件号仍是 `0.6.23`，TAB5 固件和消息协议未改，不要求重新配对或刷机。未指定发布负责人，没有合并 `main`、创建标签、上传草稿或发布。
- **本轮 Mac 修正**：Windows 原提交将图库管理移到「软件固件」，旧设备窗口仍引用已移除的对象，导致 Mac 编译失败。修正后的退出、手动刷机、首次安装和设备重连都检查新窗口里的图库操作；退出时会回到正在运行的图库窗口。

| 对齐项 | 共同约定与平台差异 | 本次证据 |
| --- | --- | --- |
| 软件固件与图库 | 设备中心「桥接设置 → 软件固件」集中显示桥接、ESP8266、TAB5 和屏保图库。两个图库均有新版时，入口仍只计一项；详情显示各自当前和目标版本，管理按钮打开原有工具。设备卡片「屏保图库」只浏览作品。 | Mac Release 原生渲染，用已安装的隔离图库和测试发布记录验证两项新版、一个计数、版本说明和管理窗口。Windows 上游记录为原生窗口通过；合并版已交叉编译。 |
| 连接与维护 | TAB5「连接设置」保留 Wi-Fi、USB 配对、固件升级、传输测速。ESP8266 从「管理 → 手动刷机…」进入刷机工具，从「连接设置」进入重置网络确认。没有有效小屏当前版本时，Mac 不计入可比较更新。 | 核看 Mac 设备中心和连接页的正常及最小窗口预览；原生回归覆盖菜单、四页与未知版本。没有连接 ESP8266，未执行重置或刷机。 |
| 更新通知和安装取消 | Mac 用组件定位，Windows 用设备 ID 定位，同批 Windows 通知选择首项。Windows 下载后取消打开安装器时显示「安装包已下载，尚未安装。」 | Mac 原生回归验证首次加载时保留指定行、窗口已打开时可切换目标、普通入口选中可用更新。Windows 上游记录为通知定位和取消安装器离屏通过。两端实际系统通知送达和用户点击仍需实测。 |
| Claude 和 Codex | Claude 后台和 VS Code 入口按进程、同一会话、可用操作及数据结构判断；Codex 桌面状态按已知字段判断。已读入口仍是 `electron-thread-read-state-v1`，取消对象内部固定版本值，账号、本机和 UUID 校验保留。 | Mac 自动测试通过，实体 TAB5 通过隔离 Claude 后台收发。合并版 Windows 待跑原生 Codex、Claude 路径；Mac 真实 VS Code 扩展窗口待验。细节见[兼容规则和前次证据](CLAUDE-CAPABILITY-ALIGNMENT-20261117.md)。 |
| TAB5 传输 | BLE 超时恢复和串口格式保持前次对齐，未更改固件、图库或配对。 | 前次 BLE 真机证据见[传输对齐记录](MAC-TAB5-BLE-ALIGNMENT-20261117.md)。本候选重新验收 USB，未复跑 BLE 录音或刷机。 |

本次 Mac `swift test` 运行 541 项、跳过 4 项、0 失败；签名 Release 候选的 `--self-test-parity` 通过 29,178 项检查，`--self-test-tab5-ui` 完成窗口检查。实体 TAB5 R6（`0.2.322-ui`）用 USB 和本地隔离的真实 Claude CLI 后台：首次自动触摸探测未确认，重连后通过；从设备发出测试草稿后出现新回复，打开能看到测试内容，原会话只新增一条用户消息。草稿、模型响应均来自测试程序，没有改用户日常会话，设备设置读回一致，原桥接进程已恢复。没有使用个人 Codex 会话改变已读状态。

- **Mac 签名候选**：`AIBotBridge.app`，arm64，本地签名校验通过；主可执行文件 14,415,344 字节，SHA-256 `e01043848874e60800a5834d08b96103ee1072042e8ea61430741f411a01aa27`。本机证据目录为 `artifacts/desktop-final-alignment-20261117/`；`preview/updates-gallery*.png`、`preview/connection-*.png`、`tab5-claude-retry.log` 和 `tab5-claude-retry/reply.png` 分别对应窗口、连接页和实体设备。没有制作正式安装包或验收覆盖升级。
- **Windows**：`4d4051f` 的 Windows 本机 Release、`--status-once`、更新窗口、设备中心、图库浏览及连接窗口缩放结果来自原交接记录；原预览在 Windows 的 `artifacts/update-center-unified-20261117/update-ui/`、`artifacts/tab5-connection-settings-20261117/ui-final/`。同一候选 `c2e9b99` 已重新交叉编译（0 错误、0 警告），没有在本机运行 Windows 桌面程序或连接 Windows 真机。以下原生验收完成前不用于发布。

Windows 在同一开发分支且工作区干净时运行 `git pull --ff-only origin codex/tab5-calendar-and-claude-idle-20261116`。从验收源码建立干净的验收目录，用 `git rev-parse HEAD` 核对上面的候选 SHA，再运行（示例路径用于保存隔离截图）：

```powershell
$p = 'windows-app/AIBotBridge/AIBotBridge.csproj'
$out = Join-Path (Get-Location) 'artifacts/desktop-final-alignment-20261117'
New-Item -Path $out -ItemType Directory -Force | Out-Null
dotnet build $p -c Release
dotnet run --project $p -c Release --no-build -- --status-once
dotnet run --project $p -c Release --no-build -- --self-test-unified-updates
dotnet run --project $p -c Release --no-build -- --self-test-update-ui (Join-Path $out 'update-ui')
dotnet run --project $p -c Release --no-build -- --self-test-device-center (Join-Path $out 'device-center')
dotnet run --project $p -c Release --no-build -- --self-test-codex-read-sync
dotnet run --project $p -c Release --no-build -- --self-test-claude-native-workspace
```

首项真机操作：Windows 连接已配对的 TAB5，用一个专用的、同一 UUID 的 Claude 后台会话，从设备发送一次测试文字。确认原会话只收到一次，TAB5 能打开同一回复；再核对运行中的 Claude VS Code 面板与通知定位，和真实系统通知送达/点击后选中正确更新项。分别记录结果，未通过不重发、未实测不记为通过。
