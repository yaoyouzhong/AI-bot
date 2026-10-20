# Mac TAB5 现场验收 / Local acceptance — 2026-10-20

接续 [交接文档](../MAC-TAB5-HANDOFF.md)，在测试分支 `codex/mac-tab5-phase1-ci` 执行。初始 HEAD 为 `bf16f1f9becd172407c51297fadb0b0bce57fb8f`，工作区干净，远端测试分支与本地一致。用户确认 TAB5 已连接 Mac，Windows 桥接已退出或暂停。

本轮只进行本地候选运行、测试、联调与修复；不合并或推送 main、不安装常驻服务、不部署、不发布、不写入 TAB5 固件。真实发送、追加或中断 Codex 任务仍须指定测试任务。TAB5 展示设置保持原值。

## 环境与基础检查

| 检查 | 实际结果 |
| --- | --- |
| 架构 | `arm64`（Apple Silicon） |
| macOS | `27.2`，build `26B5112a`（`sw_vers` 实测） |
| Swift | Apple Swift `6.2.3`，当前 developer directory 为 `/Library/Developer/CommandLineTools` |
| `swift test --package-path mac-app` | **受阻，未执行任何测试**：测试目标编译报 `no such module 'XCTest'`。当前工具链也没有 `xctest`，未找到已安装的 Xcode。此结果不能记为测试通过或应用测试失败。 |
| Release | **通过**：`scripts/build_macos_app.sh` 内执行 `swift build -c release --package-path mac-app` |
| 候选包 | **通过**：Info.plist、固定版本刷机工具包 SHA、ad-hoc 签名及严格签名验证 |
| 内置刷机工具 | **通过**：正常候选包的 `--self-test-flash-tool`，仅帮助/版本检查，无设备访问 |
| 原生界面夹具 | **通过**：`--self-test-tab5-ui` 生成 8 张 PNG；不是正常启动、权限或硬件验收 |
| 构建警告 | Swift 6.2.3 报 `SerialBridge` 跨 `@Sendable` 闭包捕获的既有警告；未靠添加未经证明的 Sendable 声明压制 |
| 正常 App 运行 | **启动与菜单栏通过，人工反馈**：用户从访达打开初始候选，确认菜单栏 `C:… A:…`。进程路径和 8765 监听已核对。界面控制工具选择候选 App 和访达均超时，不能据此认定 App 启动失败；其余窗口、正常退出与重启仍单独验收 |
| 实际 HTTP 鉴权 | **通过**：已运行候选的 `/status` 与 `/tab5/v1/status` 对无认证本机请求均返回 HTTP 401；未读取状态正文或配对材料 |

本机候选与离线证据位于忽略目录 `artifacts/mac-tab5-20261020/`，未安装到 Applications。不得将该目录的本地运行数据加入 Git 或 CI 附件。

当前 HEAD 的 [Mac CI 39353897262](https://github.com/yaoyouzhong/AI-bot/actions/runs/39353897262) 已成功；交接程序基线仍为 `f41a429`。CI 结果不替代本机尚未执行的测试。

## USB 与设备

| 检查 | 实际结果 |
| --- | --- |
| 端口 | `/dev/cu.usbmodem101` |
| USB 描述符 | `TAB5 Control`，vendor `TAB5 development`，VID `0x303a`，PID `0x4005` |
| 只读身份检查 | **通过**：以 `460800` 发送一条 `@AIBOT ` v1 `tab5_ping`，收到目标 `tab5_hello`；未配对、未改 Wi-Fi、未写固件 |
| 实际固件 | `0.2.234-ui` |
| 能力 | `rpcVersion=1`、`rpcUsbBinary=1`、`usbPacked=1`、`usbDelta=1` |
| 初次 UI 诊断样本 | `uptimeMs=56152410`、`uiAgeMs=13`、`flushCount=34381`、`flushErrors=0`；单份样本不构成启动健康验收 |
| 正常 App 配对、状态 ACK | **通过，人工反馈**：用户明确选择端口并配对，窗口显示 USB“数据已确认”；真实屏幕数据仍待观察 |
| 实际数据与屏幕 | **待验收** |
| USB 拔插 | **通过，人工反馈**：断开约 12 秒后重新连接，未点击重新配对，USB 自动恢复“数据已确认” |
| 桥接正常退出与重启恢复 | **待验收** |

显示设置的真机读取已通过。首次保持值不变保存时，设备仍处于本机设置页，收到“TAB5 正在编辑本机设置，请退出设备设置页后重试。”，记录为设备忙碌拒绝，未记为保存成功。用户退出设备设置页后，重新读取并原值保存，收到“TAB5 已确认保存显示设置。”，确认匹配成功回执；未改变用户配置。亮度、声音实际效果和重启保持仍待分别验收。

不将真实设备配对材料、网络配置或原始串口流写入记录。ESP8266 未连接或执行硬件验收，不宣称通过。

## 本轮修复：正式固件包的许可证目录

使用当前 Mac 代码的固件加载器读取 SHA 已核对的官方 `.234` 升级 ZIP，实际复现“ZIP 含重复或不安全的文件名”。官方包的七个许可证路径有九段，例如 `licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE`；原 `safePath` 最多接受八段，导致合法升级包与同类首刷包无法进入预览。

将嵌套 ASCII 路径上限调整为 16 段，并增加 720 字节总长度上限。继续逐段验证文件名，拒绝绝对路径、空段、点段、穿越、反斜线和解压工具通配符；重复项、符号链接、条目数和文件大小校验保持。依然只通过 `unzip -p` 读取明确条目，不按包内路径解压。没有协议变更。

新增包含同类九段许可证路径的合成升级包回归及边界/危险路径检查，首刷回归也加入该目录。测试资源为独立合成内容，不收录第三方许可证或二进制。

修复后的本机加载器已成功读取真实升级 ZIP，核对版本 `0.2.234-ui`、应用 SHA `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8`、ELF SHA `9e6d0e4523381385b721a46389c87ec02dd6ea492225260771f38b3d7bbc29fb` 以及真实 sidecar 的分类标题和列表。真实完整首刷 ZIP（SHA `aec51f9d21f8f9ce2f7ad3ec7cf98604969710b7aee3b80615bb4889549a17e3`）也通过清单、布局、空白区、全部分段 SHA 与精确应用提取校验。这些离线检查未创建桥接服务、未访问设备、未提供升级。

修复后 Release、候选包与签名再次通过。精确已提交源码的候选为 `artifacts/mac-tab5-20261020/3cf5fa8/AIBotBridge.app`，随包 `SOURCE_SHA.txt` 为 `3cf5fa82951ed5c3fba55b4699e5001ca174474f`。上述正常启动和首次 USB 配对先在初始 `bf16f1f` 候选执行；切换修复候选后的恢复仍待验收。

`3cf5fa8` 的 [Mac CI 39356044460](https://github.com/yaoyouzhong/AI-bot/actions/runs/39356044460) **通过**。下载完整 CI 附件后，`scripts/verify_mac_tab5_ci_artifacts.py` 核对 112 项测试、0 失败、0 跳过，Release arm64 可执行文件、候选 App、内外层哈希、源码 SHA、许可证、工具检查与 8 张原生 PNG 均通过。本机 XCTest 仍因缺少 Xcode 受阻，不用 CI 结果改写该本机结论。

## 本轮修复：操作回执可见性

用户反馈保存后表单关闭但没有可见成功或失败提示。滚动主窗口到底后，才看到设备忙碌拒绝。原结果标签在长表单末尾；现移到滚动区域之外的固定底部，保存等待、成功和错误均持续可见。最小窗口尺寸的原生夹具渲染已验证标签位于窗口内，实际显示文字为合成回执，不是真机保存截图。修复候选的正常运行仍待用户复验。

## Codex 本机探测与限制

本机 CLI 为 `0.163.0-alpha.10.2`，实际位于 `ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex`，原自动查找的 `Codex.app`、Homebrew 和 `/usr/local/bin` 均不存在。现按 `com.openai.codex` 的运行中或已注册应用目录查找新旧两种随包 CLI 布局，并保留用户显式路径优先及终端安装回退。独立本机探测已确认新解析器找到实际安装的 CLI；新增当前布局、应用重命名、用户覆盖、旧布局和无效路径回归。

CLI 帮助确认提供 `app-server proxy`，但只读 `initialize` 探测在握手前结束，默认 socket 返回 `ENOENT`；`daemon version` 同样不可达。当前桌面子进程使用 stdio。**因此这台 Mac 的 Codex 会话能力当前受阻**，不能把“找到 CLI”记为连接成功。未启动或部署另一套 daemon，未修改模型、权限、审批和服务参数，也没有列出、读取或向真实任务发送消息。交互窗口补充持续更新的 Codex 连接状态，避免静默失败。

初始化接口依据 [官方 App Server 说明](https://learn.chatgpt.com/docs/app-server)，本机安装布局、命令帮助和失败结果以实际 CLI 探测为准。没有从文档推断当前桌面已开放代理。

## 后续项目

- Wi-Fi：保持 TAB5 独立供电、断开 Mac USB 后，用户约 15 秒内观察到“收到已认证请求”，真实认证与回退初步通过。未改变原网络配置；尚未精确测量八秒阈值。无线设置回执、USB 配网、发现、地址恢复和固定模式待验收。
- BLE：首次授权、拒绝/恢复、连接传输、重连、睡眠唤醒及 USB 优先待验收。
- 显示与资源：设备设置的原值读取、匹配保存回执、重启保持、封面/桌宠/图库、分页与方向待验收。
- 系统控制与音乐：真实音量/静音、常用入口、锁屏、自动化权限和播放控制待验收。
- Codex：本机版本与 CLI 布局已核对，默认本机代理不可达；精确任务、公开回复、草稿、送达状态和快捷控制台受此阻塞。未对真实任务发送、追加或中断。
- 照片与语音：真实采集、校验、预览、中文本地识别、停止/取消/断流与手动确认待验收。
- 固件工具：已核对公开 `.234` 源码 ZIP SHA `c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890` 和升级 ZIP SHA `900612d0338c2350a3eda19a991536ce28475cce9667a4752613fccde211d509`；App 选包/说明预览待验收。OTA、首刷和恢复保持未执行。

## English

Two additional repairs follow the first firmware-package CI: operation receipts now stay in a fixed footer outside the scrolling setup form, verified by minimum-size native fixture rendering; Codex CLI discovery now handles the actual nested ChatGPT desktop layout and renamed/moved registered applications while preserving explicit overrides and legacy fallbacks. The interaction window now displays live Codex connection status. On this host, CLI `0.163.0-alpha.10.2` exposes the proxy command but its default socket is missing (`ENOENT`), initialization exits before a handshake and the desktop child uses stdio. Codex conversation acceptance is therefore blocked even after executable discovery succeeds. No alternate daemon was deployed, no task methods were called and no model/approval configuration changed. Updated Release/CI for these follow-up repairs is pending.

With TAB5 independently powered and USB disconnected, the user observed an authenticated Wi-Fi request within approximately 15 seconds. Authentication and fallback are provisionally confirmed; the exact eight-second threshold was not measured. Existing Wi-Fi configuration was retained. Other wireless checks remain pending.

Acceptance resumed on the test branch from clean HEAD `bf16f1f`, matching the remote. The user confirmed the TAB5 USB connection and the Windows bridge was stopped or paused. This session permits temporary local candidate execution and related repairs, with no main merge/push, persistent installation, deployment, publication or firmware writes. Preserve TAB5 display settings; real Codex sends/appends/interrupts require a designated test task.

The host reports Apple Silicon, macOS 27.2 (`26B5112a`) and Swift 6.2.3. Local tests are **blocked before execution**: the selected Command Line Tools lack both the XCTest module and xctest, and no Xcode installation was found. Release, pinned bundled-tool hashes, ad-hoc signing, strict signature verification, offline tool-help checks and eight native fixture PNGs passed. The existing SerialBridge Sendable capture warning remains recorded. Native UI automation timed out for both the candidate and Finder. The user subsequently launched the initial candidate normally and confirmed its menu bar; its process, listener and HTTP 401 responses for both unauthenticated status routes were independently checked. The user also confirmed explicit USB pairing and the window's acknowledged-data status. Actual screen contents, remaining windows, permissions, normal quit and restart remain separate checks.

USB descriptor and a read-only 460800-baud v1 ping confirmed a TAB5 running `0.2.234-ui`, with RPC v1/binary and packed/delta capabilities. The single UI sample had age 13 ms and zero flush errors; it does not establish boot health. The user confirmed automatic USB recovery after a 12-second cable disconnect without re-pairing. Display settings readback passed; saving unchanged settings was first correctly rejected while the device settings page was open, then acknowledged after leaving that page. Actual brightness/audio effects and restart persistence remain pending. No ESP8266 was tested.

An actual official `.234` upgrade archive exposed a loader defect: seven dependency-license paths contain nine components, exceeding the previous eight-component ASCII limit. Bounded support now allows 16 components and at most 720 bytes while retaining all component, traversal, duplicate, symlink and size checks. Entries are still streamed by exact name, never extracted to archive-provided paths. Synthetic upgrade/first-install ZIP regressions cover the published layout and reject unsafe paths. The repaired loader successfully validated the real upgrade image and its bound sidecar version, application/ELF SHA and structured notes. The real first-install ZIP also passed its manifest, layout, erased-gap, segment-hash and exact-application checks. These offline checks created no bridge service and accessed no hardware. Updated Release/app/signature checks passed; the committed candidate embeds source SHA `3cf5fa8`. Its linked Mac CI and downloaded full artifacts passed verification: 112 tests, zero failures/skips, executable/app/source hashes, licenses, tools and eight fixture PNGs. Local XCTest remains blocked. Startup and first pairing used the initial `bf16f1f` candidate; recovery on the repaired candidate, normal-app firmware preview and all firmware writes remain untested. Evidence stays in the ignored local artifact directory; no credentials, private network configuration or raw runtime streams belong in Git.
