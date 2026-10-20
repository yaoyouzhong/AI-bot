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
| 正常 App 运行 | **待验收**：界面控制工具选择候选 App 和访达均超时，未取得正常启动证据；已请求用户从访达打开临时候选 |

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
| 正常 App 配对、状态 ACK、实际数据 | **待验收** |
| 拔插与桥接重启恢复 | **待验收** |

不将真实设备配对材料、网络配置或原始串口流写入记录。ESP8266 未连接或执行硬件验收，不宣称通过。

## 本轮修复：正式固件包的许可证目录

使用当前 Mac 代码的固件加载器读取 SHA 已核对的官方 `.234` 升级 ZIP，实际复现“ZIP 含重复或不安全的文件名”。官方包的七个许可证路径有九段，例如 `licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE`；原 `safePath` 最多接受八段，导致合法升级包与同类首刷包无法进入预览。

将嵌套 ASCII 路径上限调整为 16 段，并增加 720 字节总长度上限。继续逐段验证文件名，拒绝绝对路径、空段、点段、穿越、反斜线和解压工具通配符；重复项、符号链接、条目数和文件大小校验保持。依然只通过 `unzip -p` 读取明确条目，不按包内路径解压。没有协议变更。

新增包含同类九段许可证路径的合成升级包回归及边界/危险路径检查，首刷回归也加入该目录。测试资源为独立合成内容，不收录第三方许可证或二进制。

修复后的本机加载器已成功读取真实升级 ZIP，核对版本 `0.2.234-ui`、应用 SHA `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8`、ELF SHA `9e6d0e4523381385b721a46389c87ec02dd6ea492225260771f38b3d7bbc29fb` 以及真实 sidecar 的分类标题和列表。真实完整首刷 ZIP（SHA `aec51f9d21f8f9ce2f7ad3ec7cf98604969710b7aee3b80615bb4889549a17e3`）也通过清单、布局、空白区、全部分段 SHA 与精确应用提取校验。这些离线检查未创建桥接服务、未访问设备、未提供升级。

修复后 Release、候选包与签名再次通过，输出为 `artifacts/mac-tab5-20261020/fixed/AIBotBridge.app`。本机 XCTest 仍因缺少 Xcode 受阻；CI 结果完成后补记。

## 后续项目

- Wi-Fi：USB 配网、认证、发现、地址恢复、固定模式和八秒回退待验收。
- BLE：首次授权、拒绝/恢复、连接传输、重连、睡眠唤醒及 USB 优先待验收。
- 显示与资源：设备设置的原值读取、匹配保存回执、重启保持、封面/桌宠/图库、分页与方向待验收。
- 系统控制与音乐：真实音量/静音、常用入口、锁屏、自动化权限和播放控制待验收。
- Codex：本机版本、精确任务、公开回复、草稿、送达状态和快捷控制台待验收；未对真实任务发送、追加或中断。
- 照片与语音：真实采集、校验、预览、中文本地识别、停止/取消/断流与手动确认待验收。
- 固件工具：已核对公开 `.234` 源码 ZIP SHA `c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890` 和升级 ZIP SHA `900612d0338c2350a3eda19a991536ce28475cce9667a4752613fccde211d509`；App 选包/说明预览待验收。OTA、首刷和恢复保持未执行。

## English

Acceptance resumed on the test branch from clean HEAD `bf16f1f`, matching the remote. The user confirmed the TAB5 USB connection and the Windows bridge was stopped or paused. This session permits temporary local candidate execution and related repairs, with no main merge/push, persistent installation, deployment, publication or firmware writes. Preserve TAB5 display settings; real Codex sends/appends/interrupts require a designated test task.

The host reports Apple Silicon, macOS 27.2 (`26B5112a`) and Swift 6.2.3. Local tests are **blocked before execution**: the selected Command Line Tools lack both the XCTest module and xctest, and no Xcode installation was found. Release, pinned bundled-tool hashes, ad-hoc signing, strict signature verification, offline tool-help checks and eight native fixture PNGs passed. The existing SerialBridge Sendable capture warning remains recorded. These checks do not establish normal application or hardware acceptance. Native UI automation timed out when selecting both the candidate and Finder; user launch feedback is pending.

USB descriptor and a read-only 460800-baud v1 ping confirmed a TAB5 running `0.2.234-ui`, with RPC v1/binary and packed/delta capabilities. The single UI sample had age 13 ms and zero flush errors; it does not establish boot health. Normal-app pairing, live status acknowledgements, cable/restart recovery and all later hardware, permission, display, audio, Codex, photo and speech steps remain pending. No ESP8266 was tested.

An actual official `.234` upgrade archive exposed a loader defect: seven dependency-license paths contain nine components, exceeding the previous eight-component ASCII limit. Bounded support now allows 16 components and at most 720 bytes while retaining all component, traversal, duplicate, symlink and size checks. Entries are still streamed by exact name, never extracted to archive-provided paths. Synthetic upgrade/first-install ZIP regressions cover the published layout and reject unsafe paths. The repaired loader successfully validated the real upgrade image and its bound sidecar version, application/ELF SHA and structured notes. The real first-install ZIP also passed its manifest, layout, erased-gap, segment-hash and exact-application checks. No bridge service or hardware access occurred. Updated Release/app/signature checks passed; local XCTest remains blocked and updated CI is pending. Normal-app preview and firmware writes remain untested. Evidence stays in the ignored local artifact directory; no credentials, private network configuration or raw runtime streams belong in Git.
