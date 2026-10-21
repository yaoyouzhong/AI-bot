# Mac TAB5 开发候选 / Development candidate

这是测试分支 `codex/mac-tab5-phase1-ci` 的未发布候选，适用于 Apple Silicon、macOS 13+。不替换现有下载，不代表 Mac/TAB5 真机验收通过。用户安排 2026-10-20 白天集中联调；本轮不安装到用户 Mac、不刷机、不部署、不合并 main、不建标签或 Release。Intel Mac 尚未验收。

## 使用入口与权限

1. 从 Mac 菜单栏打开 **TAB5**。已安装 AI-bot 固件的设备先通过 USB 数据线连接，刷新端口并明确选择目标设备，再配对。配对结果不确定时保留同一把钥匙，不替换其他设备身份。首次连接允许本地网络访问。
2. 同一窗口通过 USB 写入 Wi-Fi。电脑和设备位于同一局域网，桥接运行时可自动发现；发现受网络限制时，重新通过 USB 保存电脑地址。Wi-Fi 密码仅发往设备，不写入偏好或诊断。USB 数据失效八秒后由设备回退无线；设备固定连接模式仍保留。
3. **显示设置**先读取设备实际值，再编辑亮度、声音、固定页、轮播页面/顺序、间隔和屏保等待。只有匹配回执确认才显示保存成功；未确认时重新读取。不因测试或 TAB5 升级恢复 ESP8266 的默认轮播。
4. 蓝牙首次安装默认开启，系统首次使用时请求权限；已保存的关闭选择仍会保留。仅连接已通过 USB 配对的设备。通过系统设置管理本地网络、蓝牙、自动化、辅助功能、语音识别和麦克风权限。

首次系统蓝牙配对显示数字时，核对 Mac 与实体 TAB5 的数字一致，并在两端确认。固件同时提供 HID 键盘服务，macOS 可能打开“键盘设置助理”；可以退出该助理，TAB5 状态与设置传输无需配置键盘布局。不要将“重新配对／清除旧配对”当作普通数字确认。

以 AI-bot Bridge 名义访问 `io.github.yaoyouzhong.AI-bot` 的“登录”钥匙串弹窗，与蓝牙配对是两种授权。“允许”仅针对此次访问；“始终允许”记住该候选对相应项目的许可，详见 [Apple 说明](https://support.apple.com/zh-cn/guide/keychain-access/kyca1243/mac)。草稿／回执密钥每个进程只读取一次，拒绝结果也缓存，后续操作不会反复请求同一密钥。构建脚本优先使用 `AIBOT_MAC_SIGN_IDENTITY`，否则读取本机 `~/Library/Application Support/AI-bot/local-code-signing-identity.txt` 中的 40 位证书指纹；该文件不进入仓库或发布包。选择固定身份后，签名失败会停止构建，不会静默退回临时签名。两者都未配置时仍为临时签名。固定签名可保持不同构建的身份规则，但第一次切换签名、首次访问其他钥匙串项目或新权限仍可能需要确认；不需要开启所有 App 的访问权限。

## 资源与固件

**资源与固件**中导入现有 AI-bot 图库 ZIP；保留来源、许可、导读和全部图片 SHA。标准/高清包按类别导入，旧图库保留备份。设备显示专用 1280×720 JPEG，支持多页与横竖屏。电脑桌宠选择和当前音乐封面自动转为设备资源，收到资源确认后停止发送。

高清完整包校验外层索引和内置 `standard.zip` 的 SHA，再验证并导入 TAB5 适配图库；不解压或加载电脑端高清原作。本候选不含 Mac 高清原作浏览器，也不把未读取的高清原作称为已核验。

选择升级 BIN 时保留相邻 `.notes.json`；也可选择完整升级 ZIP。先预览版本、SHA 和说明，再显式开始升级。选择固件不等于已经写入。USB、Wi-Fi、BLE 共用验证与回执；原始数据传输完成也不等于新固件启动成功。

升级或首刷后，保持所选精确应用镜像，点 **核验所选固件的启动状态**。必须核对版本、ELF SHA、VALID 运行分区及三个递增的实时 UI 样本。旧设备版本不支持无线核验时使用 USB。屏幕、触摸和实际数据仍需要人眼确认。

## 电脑、音乐、Codex、照片和语音

系统诊断新增 App 内网络探测，每 30 秒沿系统网络路径检查 ChatGPT／资源服务的 HTTP 响应与耗时，不携带账户凭据、不调用模型。PAC 未启用时明确显示“未配置 PAC”；启用时比较系统解析与实际请求的代理／直连类型，不据此判断 VPN 隧道或具体节点。网络样本超过 75 秒失效，最多保留十轮曲线。CPU 等待仍不支持等价队列计数，不用平均负载或线程总数代填。

**交互与权限**记住已保存的开关，不在重启时重置。首次安装默认开启音乐采集、Codex 只读和蓝牙；天气未配置城市／坐标时默认自动定位。系统指标、账户额度、行情、日历和桌宠沿用已有自动采集；需要账号的来源复用已保存授权，未配置账号不产生虚构数据。电脑控制、音乐控制、任务发送、桌面输入框和语音输入单独启用，首次系统权限仍由用户确认：

| 功能 | 使用与边界 |
| --- | --- |
| 音量/静音/锁屏/常用入口 | 读取 CoreAudio 实际结果；最多四个本机程序或文件夹。设备不能传入任意路径或命令。锁屏需要辅助功能；9415cee 已在正确授权后通过实体锁屏。锁定/屏幕状态当前未采集，保持未知，不提供 Mac 一键解锁。 |
| Apple Music / Spotify | 采集和播放控制分开。新安装默认只采集；升级保留原来已开启的组合音乐权限，后续可单独关闭控制。无需开启音量／锁屏等电脑控制。首次触发自动化授权。控制与已显示歌曲绑定，只发送一次，随后读取状态；未知结果不重复跳曲。 |
| Codex 回复和草稿 | 通过已安装 CLI 的标准 stdio 临时子进程，只读本机近期会话与保存的公开回复，关闭功能／退出桥接时结束，不安装后台服务。可选择已安装的 CLI。只投影会话标题、项目名与公开助手回复；后台活跃统计继续只读文件元数据。历史分页有界，未知桌面实时状态不猜测。目录最多读取 512 条本机未归档会话；设备帧优先携带最近 64 条，并在 32 KiB 限额内逐级收缩。每组超过 8 条时显示编号分组页，最多 24 个分组页；不代表无限历史或跨电脑同步。 |
| Codex 发送 | 只读历史连接不执行写入；另行启用操作后，经已验证的现有 Codex 桌面任务持有者连接发送／中断。保留原任务模型和审批设置，不在独立进程恢复真实任务。当前桌面追加接口不能绑定指定轮次，追加保持不可用。指定任务的实体发送／停止及停止后的空回复提示已通过实体复验。加密草稿保存在本机，审批／用户问题仍在电脑处理。 |
| 快捷控制台 | 另需辅助功能。只有公共辅助功能地址能证明精确任务 UUID、且主输入框可确认时才写入和读回。Codex 版本没有提供这些信息时安全拒绝；不能把通用标题或“已请求打开”算成已到前台。此版本相关能力必须在目标 Mac 实测。普通 Codex 会话操作不依赖此输入框。 |
| 照片 | TAB5 摄像头上传后先验证完整 SHA、JPEG/PNG 解码和像素边界，最多三张绑定当前设备/任务。加密保存，只在确认发送时作为该任务附件。USB/BLE 分块与 Wi-Fi 整图均保留幂等校验。 |
| 语音 | Mac 原生本地识别，支持 TAB5 收音及 Mac 默认麦克风（可在系统设置选择 USB 接收器）。先授权语音识别/麦克风；仅本机支持中文本地识别时启动，不悄悄改为云端识别。不沿用 Windows 豆包输入法或虚拟声卡路径。最长一分钟，断流会停止收音。最终文字先预览，确认后才能发送或填入事项/日期；取消丢弃迟到结果。 |

## 首次安装、备份与恢复

从 **TAB5 → 首次安装与备份恢复**打开专用工具，不使用“小屏刷机”的 ESP8266 地址。

1. USB-C 数据线连接 TAB5；长按 RESET 约两秒，内部绿灯快速闪烁后松开，进入下载模式。[M5Stack 官方说明](https://docs.m5stack.com/zh_CN/guide/tab5/restore_factory)。刷新并手动选择下载口。
2. 首刷只接受完整 `AI-bot-tab5-…-first-install.zip`，不接受 OTA 应用 BIN。确认替换后，工具核对 ESP32-P4/16 MiB/安全状态/芯片修订、清单布局与全部 SHA，完整备份并与原机 Flash 比对，再核对硬件 MAC 后写入并验证。不自动重试写入，不绕过安全启动或加密。
3. 已安装 AI-bot 会在备份后停止首刷，转用正常升级。Flash 校验通过后短按 RESET，等实际画面启动，回 TAB5 窗口配对新串口；可在“资源与固件”选择同一首刷 ZIP，提取经过验证的精确应用来核验启动。
4. **只备份**生成 `.bin` 与 `.bin.json`；备份可能含私有网络设置，权限为当前用户读写，妥善保留。**恢复**选择同一设备的 `.bin.json`，验证硬件 MAC、16 MiB 长度和 SHA，先保存当前原机备份，再恢复并校验。Windows/Mac 备份元数据大小写兼容。不要仅凭文件名恢复。

串口与蓝牙在底层操作期间暂停，完成后恢复；保留 TAB5 当前展示设置。首次安装、恢复和实际 OTA 均未在本轮对硬件执行。

## CI 候选与现场验收

GitHub Actions 仅对测试分支运行：原生 `swift test`、Release、临时签名 App、ESP8266/P4 工具命令帮助检查，以及使用固定数据的 AppKit 界面 PNG。测试进程不创建正常 AppDelegate、不连接真实 Codex、不请求权限、不访问设备或 Keychain。PNG 是原生视图渲染，不是实机操作或屏幕截图验收。

CI 附件包括 xUnit、日志、工具链、源码 SHA、含 LICENSE/第三方声明的候选 App/可执行文件与 SHA-256 清单。候选为临时签名，未公证。`--self-test-flash-tool` 只执行工具版本/帮助，绝不刷机；`--self-test-tab5-ui <新目录>` 只渲染固定数据界面。正常使用需由用户将候选 App 放入应用程序目录并按 macOS 提示打开，本轮不执行安装。

集中真机验收顺序：USB 身份/拔插/恢复 → Wi-Fi/固定模式/八秒回退 → BLE 权限/独占/睡眠唤醒 → 展示与保存/资源/图库分页 → 音乐、音量和权限拒绝/恢复 → Codex 精确任务、草稿、真实发送/追加/中断/回执 → 照片与语音/事项/日期 → 经另行明确选择后验证 OTA、首刷/原机恢复。每项记录实际结果，不以构建通过代替。已确认现场现象逐项保留，问题修复后只补相关回归。

具体提交与 CI 证据见 [开发记录](development/MAC-TAB5-PORTING.md)。

2026-10-20 本机检查、实际固件包问题与待验收项目见 [现场验收记录](development/MAC-TAB5-ACCEPTANCE-20261020.md)。该记录区分工具链受阻、离线验证和正常 App／硬件结果。

本机 `0.163.0-alpha.10.2` 桌面默认 stdio 不创建公开控制 socket，且其 `app-server proxy` 转发原始 WebSocket 字节，不能直接交换 JSONL。当前候选改用标准 stdio 的临时只读子进程；只读连接状态及限制显示在“交互与权限”。正常 App／TAB5 验收以[最新链路记录](development/MAC-TAB5-CODEX-LINK-20261020.md)为准，不部署 daemon。

---

# English

This unpublished Apple Silicon/macOS 13+ candidate lives on `codex/mac-tab5-phase1-ci`. It does not replace public downloads. Physical acceptance is scheduled for daytime on 2026-10-20. No deployment, installation, hardware write, main merge, tag or Release is performed in this work. Intel support remains unverified.

Open **TAB5** from the menu bar. USB-pair the explicitly selected AI-bot device, then provision Wi-Fi over USB. Local discovery requires the same LAN; Bluetooth defaults on for fresh installs and retains any saved opt-out; first use still requests system permission. Read actual display settings before editing and require the matching device receipt before reporting success. Preserve TAB5 display preferences during upgrades and validation.

For a system Bluetooth numeric-comparison prompt, compare the Mac and physical TAB5 displays and confirm both ends. The firmware also exposes a HID keyboard service, so macOS may open Keyboard Setup Assistant. You can quit the assistant; TAB5 status/settings transfer requires no keyboard-layout setup. A request to re-pair or delete an old bond is a separate operation.

The login-Keychain prompt for AI-bot Bridge and service `io.github.yaoyouzhong.AI-bot` is separate from Bluetooth pairing. Allow grants one-time access; Always Allow remembers access for that candidate and item, as described by [Apple](https://support.apple.com/guide/keychain-access/kyca1243/mac). A newly built ad-hoc candidate may need renewed authorization. The private draft/receipt encryption key is loaded once per process, including caching denial, so later actions do not repeatedly request the same key. The bundle script uses `AIBOT_MAC_SIGN_IDENTITY` first, then a 40-digit certificate fingerprint in `~/Library/Application Support/AI-bot/local-code-signing-identity.txt`. This machine-local file is excluded from source and packages. A configured identity fails closed on signing errors; it never silently falls back to ad-hoc. Without either setting, signing remains ad-hoc. A fixed signer preserves the designated requirement across builds, but the first signer transition, first access to another Keychain item, or a new permission can still require approval. Do not enable access for all applications.

**Resources and firmware** imports standard/HD gallery ZIPs with provenance, licensing, guides, image decoding and complete hashes. Device pet/music resources stop after acknowledged transfer. Select a BIN plus matching notes or an upgrade ZIP, review it and explicitly start. Boot verification requires the exact version/ELF, VALID partition and three advancing live UI samples; transferred bytes alone are insufficient.

For an HD full bundle, validate the outer catalog and embedded standard ZIP hash, then validate/import only its complete TAB5 subset. Desktop HD originals are not extracted or advertised as verified; this candidate does not include a Mac HD-original browser.

**Interaction and permissions** independently enables CoreAudio volume/mute, whitelisted local launchers, lock, Music/Spotify, Codex and voice. Physical lock passed on 9415cee after Accessibility was granted to the correct candidate. Mac lock/display state is not collected and remains unknown; one-click unlock is unavailable. The music opt-in independently authorizes playback controls without enabling volume, lock or launchers. Music commands bind to the displayed track, execute once and read back actual state. First use requires the relevant macOS Automation/Accessibility permissions.

System diagnostics now probe ChatGPT/resource HTTP response and latency along the system network path every 30 seconds inside the app, without account credentials or model calls. Absent PAC is explicitly not configured; configured PAC compares resolved and observed proxy/direct types, without inferring a VPN tunnel or exact node. Network samples expire after 75 seconds and retain at most ten actual rounds. An equivalent public CPU waiting-queue counter remains unavailable; load average and total threads are not substituted.

Collection preferences persist across restarts and explicit opt-outs are preserved. Fresh installs enable playback collection, read-only Codex and Bluetooth; weather defaults to automatic location only when no manual city/coordinates are configured. Existing automatic metrics, quotas, stocks, calendar and pets remain automatic. Account sources reuse stored authorization. Music control is separate from collection; upgrading retains an existing combined music grant. Computer control, task sending, desktop composer access and voice input remain separate opt-ins. First-use OS authorization still applies.

Codex uses the installed CLI's standard stdio endpoint in an app-owned temporary read-only child, ending on opt-out/exit with no service installation or listener. It projects recent titles/projects and saved public assistant messages through bounded pagination; background activity remains metadata-only and unavailable desktop runtime remains unknown. The catalog reads at most 512 unarchived local chats; device frames prefer 64 recent chats and shrink to fit 32 KiB. Folders larger than eight chats use numbered picker pages, with at most 24 entries. This is bounded local history, not unlimited or cross-computer synchronization. Device/task-bound drafts and images remain encrypted. The read-only child never mutates tasks. A separate opt-in routes send/interrupt through the verified existing desktop owner, preserving the task model and approval settings without independent resume. Installed steer cannot bind an expected turn and remains disabled. Physical send/stop on the designated task and the subsequent empty-reply display repair passed acceptance. Desktop composer staging additionally requires Accessibility proof of the exact task URL and main composer; unsupported versions return unconfirmed rather than guessing. Handle approvals on the computer.

Photos use authenticated Wi-Fi or USB/BLE chunks, complete SHA/decode/dimension validation, and at most three attachments bound to the selected task. Native Speech accepts TAB5 audio or the Mac default microphone/USB receiver, only with supported on-device Chinese recognition. There is no silent cloud fallback or Windows IME/virtual-device dependency. Stop on disconnect, cap capture at one minute, review final text and manually confirm sending/insertion. Cancellation ignores late callbacks.

Use **First install and backup/restore** for the 16 MiB ESP32-P4. Connect USB-C, hold RESET for about two seconds until the internal green LED flashes rapidly, then release and select the download port. Accept only a complete first-install ZIP, validate the reviewed layout/chip/revision/security/hashes, read and verify the entire current Flash, recheck hardware identity, then write once and verify. Existing AI-bot installations are directed to upgrade after preserving their backup. Reboot manually, pair the application port and select the same first-install ZIP in the firmware tools to verify its exact application boot. Restore accepts only a verified `.bin.json` belonging to the same hardware MAC and backs up the current Flash first. Keep private backups securely; Windows/Mac metadata are interoperable.

CI performs native tests, Release, an ad-hoc signed app, non-device tool-help checks and fixture-only AppKit PNG rendering. It does not connect to a real conversation, device or Keychain, or request permissions. Artifacts include source SHA, reports, licenses and SHA-256 checksums; the app is not notarized. Hardware/permission/audio/display/OTA acceptance remains separate, following the ordered checklist above and the linked development record.

The [2026-10-20 local acceptance record](development/MAC-TAB5-ACCEPTANCE-20261020.md) distinguishes the local toolchain blocker, actual package-loader repairs, offline checks and pending normal-app/hardware acceptance.

The installed desktop defaults to stdio without creating a public control socket. Its proxy relays raw WebSocket bytes and cannot directly exchange JSONL. This candidate instead uses standard stdio for temporary read-only access, displaying its status and restrictions in the interaction window. See the linked follow-up for current normal-app/TAB5 acceptance. No daemon is deployed.
