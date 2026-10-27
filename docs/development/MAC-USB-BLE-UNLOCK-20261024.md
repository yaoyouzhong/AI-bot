# Mac USB／蓝牙一键解锁候选 / Mac USB/BLE one-click unlock candidate

## 当前边界 / Current boundary

现已更新为取消恢复／图标修复 Mac 候选，Debug／Release 各 690 项隔离检查、严格签名及原 helper 核验通过，Wi-Fi／蓝牙连接恢复。系统图标解析显示原机器人 Logo；用户已确认取消后停止、第二次解锁成功及 30 秒未回锁，当前场景实机通过。TAB5 仍为 `.267`，未生成新固件。详细证据见[后续修复记录](MAC-REMAINING-FIXES-20261024.md#取消恢复图标候选部署)。

The cancellation/icon Mac candidate is now deployed, passing 690 isolated checks per configuration, strict signing and unchanged-helper verification, with native Wi-Fi/BLE recovery and system robot-icon resolution. The user confirms cancellation stopped input, the next explicit unlock succeeded, and no relock occurred for 30 seconds; the observed case passes. TAB5 stays at `.267`; no firmware was generated. See the linked follow-up for evidence.

当前 Mac 包另通过 USB 在途断连：用户确认拔线后输入停止、Mac 保持锁屏、原连接模式已恢复；测试后原生 USB 数据回执再次确认。USB 曾未识别、设备正常开关机后恢复的问题仍保留待查，不能由本次断连成功关闭。ESP8266 小屏用户确认当前没有可接入硬件，本轮暂缓而非通过。

The current Mac bundle also passes the user-observed in-flight USB disconnect: input stopped, the Mac stayed locked and the original mode was restored; post-test native USB data is confirmed. The earlier restart-recoverable enumeration issue remains open. The user has no ESP8266 hardware available for this run, so its physical regression is deferred, not passed.

此前验收运行 Mac ble-priority 版（Debug／Release 各 635 项隔离检查）和已精确核验的 TAB5 `0.2.267-ui`。最终普通锁屏验收通过：仅 USB、仅蓝牙、仅 Wi-Fi，以及拔掉 USB 后切蓝牙，用户均确认首点锁屏／解锁成功、设备提示正确、桌面观察 30 秒未回锁；已独立核对设备与 Mac 的实际通道及成功回执。拔线后恢复状态的耗时未测量。此次结论限定于已完成的场景，不抹除下方历史失败。

睡眠后恢复补充：用户在现有无线设置、USB 断开的测试安排下，明确反馈“唤醒后已进桌面；随后锁定、解锁并观察 30 秒未回锁”。本次手动唤醒后的 TAB5 操作恢复及再次锁定／解锁通过。睡眠时长、实际无线通道与恢复耗时未独立测量；唤醒即锁屏的起点和 TAB5 主动唤醒仍未覆盖。

原密码、配对与显示设置保留；不下发 Mac 密码，不改系统全局设置。Computer Use 外层保护的停止／交接／解锁仍未验收，取消后的恢复已有用户实机确认；USB 在途断连已有本轮实机确认；其他通道的断连仍不能由软件检查代替实机验收。未推送、建标签或发布。

The previously tested ble-priority Mac passes 635 isolated checks in both Debug and Release, paired with exact-boot-verified TAB5 `0.2.267-ui`. Final ordinary-lock acceptance passes for USB-only, BLE-only, Wi-Fi-only and USB-removal-to-BLE handoff: the user confirms first-tap lock/unlock, matching device results and 30 seconds without relock. Independent device and Mac receipts verify the actual successful transports. Handoff recovery time was not measured. In the subsequent sleep check, the user confirms that manual wake entered the desktop directly, followed by successful TAB5 lock/unlock and 30 seconds without relock. Post-wake operation recovery passes for this observed case. Sleep duration, actual wireless transport and recovery latency were not independently measured; wake directly to a locked screen and device-initiated wake remain untested. Earlier failures remain recorded below; acceptance is limited to the completed scenarios. Credentials, pairing, display settings and system-wide settings are preserved. Computer Use protection handoff and disconnect acceptance for other transports remain separate. No push, tag or public release occurred.

## 实现 / Implementation

- Windows 参考：`Tab5PinStore` / `Tab5PinBroker`、`Tab5Rpc`、设备 `ui_unlock.h`。完整 `.264` 基线包含数字 PIN／ASCII 密码授权及设备 HID 输入，保留其 `unlockCredentialVersion=2` 协商、字母符号键盘和三参数 `oneclick_start`；Mac 使用独立本机执行路径。
- Mac 保存的账户密码继续留在当前账户／当前配对绑定的 Keychain。新增独立 USB／蓝牙开关和 `hostUnlockVersion=1`，通过 `host-unlock` 传 begin/poll/cancel 与确认结果。旧 `oneClickUnlock` 仍为 false，旧固件不会收到 Mac 密码。
- 三个通道复用 `MacHostUnlock` 的同一执行器和去重记录。新请求固定实际通道；USB／BLE 断连或连接准备期间断连会撤销匹配请求，其他通道断连不取消正在执行的 Wi-Fi 解锁。保存两个权限使用原子校验，校验失败不部分改变 Wi-Fi 设置。
- 已验收的本机密码页唤出、单次 AX 写入／提交、桌面确认和 Computer Use 保护检查未改。设备新增独立 `host_unlock_remote.c`；`unlock_remote.c` 的 Wi-Fi 实现未改，Windows PIN/HID 路径保留。
- Windows 仅增加对可选 `host-unlock` 的明确“不支持”响应及集成检查，不通告新能力、不为其读取 PIN。

Windows supplies the reference for single-use admission, transport binding and cancellation. Its PIN/HID route stays intact. The Mac extension carries only intent/progress over USB/BLE, retains the password in the existing pairing-bound Keychain record, and uses the accepted local unlock backend. A shared coordinator prevents overlapping input and cross-transport replay. Capability and enablement are independent of Wi-Fi. No password is exported and no system-wide authentication or screen-sharing policy is changed.

## 固件来源与补丁 / Firmware source and patch

参考固定主干提交 `2bd5d69a56fd9a42953e29cfc428940dfb6daa6a`，版本 `0.2.264-ui`。该提交提供 `.262`／`.264` 完整文件覆盖包及此前缺失的 `.255` 补丁。

基底 `.234` 源码包 SHA-256：`c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890`。其非主程序文件是工作快照，部分已含后续改动。重建脚本逐块只接受唯一的精确原文或精确目标文本；对最终 `.262`／`.264` 已提供完整文件的路径，直接使用逐文件 SHA 验证后的最终覆盖内容。没有模糊匹配或强行合并。归档基线共 939 个文件。完整编译发现归档遗漏 `photo_upload_retry.h`，并非可直接构建的全量快照；该文件按同一提交的协议文档与原有 24 场景测试独立补齐，未伪称恢复了原文件。补齐保留精确数值能力协商、同电脑／模式／session、20 秒及单次恢复边界，未删除照片功能。来源记录在 `SOURCE-REPAIR.json`。

- [设备补丁](../proposals/tab5-mac-host-unlock.patch)：独立 worker、UI 入口、仅新 kind 的 RPC 时限、模拟测试及构建入口。
- [基线与候选逐文件摘要](../proposals/tab5-mac-host-unlock-source.json)：文本以 LF 归一后计算，旧文件必须匹配才应用。已在完整 `.264` 基线执行 `git apply --check`。
- 当前证据目录：`artifacts/mac-usb-ble-unlock-20261024-main264/`。`reconstructed-264/` 为归档基线，`candidate/` 为解锁候选；`reconstruct.py`、`RECONSTRUCTION.json`、`BASE-264-SHA256.json` 记录来源、每块决定及全树摘要。
- 旧 `artifacts/mac-usb-ble-unlock-20261023/source/` 和新目录中首次尝试的 `reconstructed/` 是停止的中间副本，不用于构建或刷写。
- `.264` OTA／启动诊断和 Windows 扩展密码输入保持。未修改组件正式版本登记；初始本地候选为 `0.2.265-ui`，当前为 `0.2.267-ui`。固定交付文件已由准备脚本生成，历史包保留；已授权安装并通过精确启动核验。

Main commit `2bd5d69a56fd9a42953e29cfc428940dfb6daa6a` supplies the missing `.255` patch and verified full-file overlays through `.264`. Reconstruction accepts only a unique exact before/after hunk; paths covered by final full-file overlays use their verified bytes. The 939-file archived base and every reconstruction decision are recorded. The archive omitted `photo_upload_retry.h`; an independent contract-compatible implementation is separately documented and passes the original 24 photo tests under UBSan. The rebased patch preserves Windows ASCII credential negotiation/UI as well as `.264` OTA/boot changes. Intermediate failed reconstructions are not build inputs. Official component version registrations remain unchanged. The local candidate has its own version and validated delivery artifacts; authorized installation and exact boot verification are recorded below.

## 初始实现验证（历史）/ Initial implementation validation (historical)

本节记录 `.265` 初始验证；当前构建与实机结论见顶部及最后的验收记录。

This section records initial `.265` validation. Current build and hardware results are at the top and in the final acceptance section.

- Mac Debug 和冻结源码 Release 构建通过；两者内置 `--self-test-parity` 均为 **501 项**隔离检查通过。包含完整加密 RPC 的 USB／BLE begin/poll/cancel、错误 proof/session、Wi-Fi 通道拒绝、权限能力通告和真实服务断连回调；只使用合成密码、隔离存储和伪执行器。此前 Debug/Release 证据位于 `artifacts/mac-usb-ble-unlock-20261023/`。本次 `.264` 证据目录另冻结当前 Mac 源码为 `mac-source/`，`MAC-SOURCE-SHA256.json` 核对无变化，Release 和 `mac-parity.log` 再次通过 501 项。
- 设备 worker 使用实际候选 C 源码、伪 USB／BLE RPC 与上游 cJSON v1.7.18，**507 项断言通过**；普通构建及 UndefinedBehaviorSanitizer 检查通过。覆盖成功、并发、取消、主机／会话／连接变化、能力停用、错误 offer/sequence、超时、调度失败和无跨通道回退。单次 RPC 时限另有 **8 项断言通过**。测试不调用 HID、网络或真实设备。
- `swift test` 未通过：当前 Command Line Tools 环境缺少 XCTest；首次执行还遇到其他工作对共享源码的同时修改，后续 Release 使用冻结源码快照验证。不能将内置检查写成 XCTest 通过。
- AddressSanitizer 测试进程在此环境中持续运行而未返回，已停止；不将其记录为通过。普通及 UBSan 结果单独保留。
- Windows 所需 `dotnet build` 和 `--status-once` 未执行成功：本机无 `dotnet`。Windows 新增拒绝路径的集成测试需要 Windows 环境执行。
- 迁入 `.264` 后，普通及 UBSan worker 检查再次各 507 项通过，请求时限 8 项通过；USB／BLE 实际 LVGL 事件和界面预览通过，包含单次请求、取消、断连、超时、新鲜状态确认及 Wi-Fi／Windows 入口保留。完整 TAB5 构建及生产安装包校验通过；原有电脑控制 focused UI 回归通过（仅将两处旧“Windows PIN”断言同步为 `.264` 的“PIN 或密码”文案），实际 sidecar 的分类、换行及滚动预览通过。硬件验收仍待执行。ESP8266 未修改，未刷写或改变小屏展示设置。

Validation distinguishes isolated checks from hardware: Debug and frozen-source Release builds pass, with 501 Mac checks in each; 507 device-worker assertions and eight RPC-budget assertions pass. XCTest is unavailable; Windows validation requires a Windows/.NET environment. ASan did not finish and is not counted as passed. The complete firmware, focused control UI and actual-sidecar preview now pass; device acceptance remains outstanding.

## 当前本地交付 / Current local delivery

- 当前 Mac：`artifacts/mac-usb-ble-unlock-20261024-ble-priority/AIBotBridge.app`（Debug／Release 各 635 项隔离检查通过）。旧候选已正常退出。沿用本机固定签名身份；严格签名核验通过，包内钥匙串 helper 与 r76 的已签名字节相同。helper 身份／私有管道检查通过，未访问真实凭据。
- TAB5：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，版本 **0.2.267-ui**，**7,191,936 字节**；现有 OTA 分区余 **17,024 字节**，未改分区。
- 镜像 SHA-256：`60ccffe6c2e1625a36b11ee3ad60eeec72b737b02b11619ad51fd538d08ff0e4`。
- 镜像 ELF 摘要：`d80dc5ec2fd937e7bc5911a87001df46dae9f49d7482dd9b68163c8abb638f8a`，已用于安装后精确启动核验。
- 构建使用归档指定的 ESP-IDF v5.4.2 提交及项目原有依赖／补丁。macOS 包装脚本仅为本次进程树设置后台优先级和两个编译槽，不改全机设置。`sdkconfig`、依赖锁、分区表、Wi-Fi worker 和 Windows one-click 接口与归档一致。
- `prepare_tab5_firmware.ps1` 复用未修改的生产包校验类；本次仅修正 macOS PowerShell 对隐藏 `.prepare.lock` 的清理，使用 `Remove-Item -Force`。重跑幂等准备成功，锁文件已移除。该验证不等于 Windows 应用构建通过。
- Mac 全部设置窗口的隔离原生 UI 渲染通过，包含新增 USB／蓝牙开关、最小尺寸、滚动及亮／暗色外观；未请求设备权限。
- 候选必须使用标准 `0.2.N-ui` 数字版本以保留既有 BLE 压缩／增量能力；初次构建的 `0.2.264-mac1-ui` 仅留在历史目录，不用于刷写。当前版本为本地 `0.2.267-ui`，未创建标签或更改正式版本登记。
- `BUILD-VALIDATION.json`、`MAC-BUNDLE-SHA256.json`、`SOURCE-REPAIR.json`、构建及测试日志均在上述候选证据目录。候选只包含源码／二进制摘要，无密码、配对记录或运行状态。

The signed ble-priority Mac passes 635 isolated checks per configuration and retains the exact r76 Keychain helper bytes. The installed `.267` firmware is 7,191,936 bytes and fits the existing OTA partition with 17,024 bytes free. Production image/sidecar validation, idempotent package preparation and exact boot verification pass. Build configuration, dependency lock, partition table and existing Wi-Fi worker remain unchanged. The preparation script's only portability fix permits deletion of its own hidden lock file on macOS. No public release has occurred.

## 验收范围 / Acceptance scope

已完成的最终实机回归为普通锁屏的 USB、蓝牙、Wi-Fi 首点动作及提示，另有 USB 拔线后蓝牙交接。后续通过手动唤醒直接回桌面后的 TAB5 再次锁定／解锁及 30 秒未回锁；不将其外推为设备主动唤醒或唤醒即锁屏的起点。较早 `.266` 阶段的 USB 有／无密码框、自然熄屏成功保留为该组合的证据，不推定当前组合覆盖全部起点。取消后的新请求恢复及 30 秒未回锁已由用户在当前 Mac 包确认；USB 在途断连停止亦已由用户确认；其他通道断连、全部通道熄屏及 Computer Use 外层交接仍不因这些结果而算验收。不同通道不能自动重试或替换为另一通道。

Final physical regression covers ordinary-lock first taps and matching results on USB, BLE and Wi-Fi, plus USB-removal-to-BLE handoff. A subsequent manual-wake-to-desktop check passes TAB5 lock/unlock and 30 seconds without relock, without establishing device-initiated wake or wake-to-locked-screen behavior. Earlier `.266` USB visible/hidden-field and natural-display-off successes remain evidence for that earlier pairing only. The user has now accepted cancellation/new-request recovery and 30 seconds without relock on the current Mac bundle. In-flight USB disconnect stopping input and retaining the lock is now user-accepted as well. Other transport disconnects, every transport's display-off behavior and Computer Use handoff remain separate from these observed passes. Attempts never retry or switch transports automatically.


## 历史安装进展 / Historical deployment progress

以下按当时状态保留安装、失败与修复过程；其中“当前”“待验收”仅指各阶段，不覆盖顶部最终结论。

The following preserves deployment, failure and repair history. “Current” and “pending” refer to each historical stage, not the final result at the top.

用户已明确授权安装本次 `0.2.265-ui` 和配套 Mac 桥接并进行 USB／BLE 实测。安装前发现当前运行的是后续 r78-offline；本候选冻结源码与当前工作区逐文件一致，包含该日历／快捷发送修复。已从界面正常退出 r78，启动本候选并确认三通道已认证，已有配对与钥匙串配置正常读取。

设备当前仍为 `.264`。第一次远程 OTA 请求收到设备 `rejected` 回执，尚未开始写入；没有绕过草稿／任务保护，正在通过设备升级页核对具体提示。Mac 独立 USB／蓝牙权限当前尚未启用，原 Wi-Fi 权限为开启。后续以精确镜像启动核验和实体解锁结果更新记录，不能把“已选择固件”写成安装成功。

The user authorized this exact firmware and Mac candidate. The previously running r78-offline bridge exited normally; this signed candidate starts successfully with authenticated transports and the existing pairing/Keychain configuration. The frozen Mac source matches the current workspace, including the r78 calendar/quick-submit changes. The first remote OTA request received a device rejection before writing; task/draft protection was preserved. The device is still on `.264`, with the new local unlock permission still disabled. Hardware acceptance remains pending.


## OTA 草稿阻塞修复 / OTA draft synchronization fix

设备明确提示“其他会话的草稿正在同步”。桥接只读诊断连续观察到 USB `draft-save` 返回 404；目录本身在线。`.264` 的 `codex_sync_tick` 会依次保存所有 dirty 草稿，但首条旧会话不在当前 Mac 目录内时永久 404，后续草稿不能推进，`ota_prepare` 因此持续拒绝升级。USB／蓝牙权限已从重新打开的设置页确认保存，原 Wi-Fi 开关仍为开启。

修复 Mac 的纯本地草稿保存边界：目录外有效 UUID 仅可保存该设备自己的加密草稿，200 只在写入成功后返回；不加入任务目录，不触发 IPC，不授权未知任务读取／导航／发送。保留图片所有权、2000 UTF-8 字节、功能开关和持久化失败保护；新目录外草稿在设备已有 128 条时停止新增，已有绑定可继续更新。没有清空草稿、重启 TAB5 或跳过 OTA 保护。

新增 20 项隔离断言随应用自检执行，Debug 共 521 项通过。完整 XCTest 仍受当前 CLT 缺少 XCTest 限制；Release 构建及 521 项隔离检查通过，固定签名核验与包内钥匙串 helper 身份检查通过。证据与源码快照位于 `artifacts/mac-usb-ble-unlock-20261024-draft-recovery/`，只用合成任务／草稿及临时加密存储。固件仍为已授权的同一 `.265` 镜像，未修改镜像或重新编号。

The device reported pending drafts from other conversations, while local USB diagnostics repeatedly showed `draft-save` 404 with an otherwise connected catalog. The oldest dirty draft blocked the firmware's existing sequential save queue and therefore OTA. The Mac fix durably preserves authenticated device/task-scoped orphan drafts without catalog admission or IPC. Existing read/navigation/send guards, image ownership, size, permission and failure rules remain. Twenty isolated checks cover the new boundary; Debug totals 521 checks. No live draft was cleared and firmware protection remains active. Release and all 521 isolated checks now pass; the signed helper identity check passes, and the authorized firmware bytes are unchanged.


草稿恢复桥接已从界面正常启动，实际收到一笔设备 `draft-save` 并返回 200（同步发生在已认证 Wi-Fi 通道）；未查看或记录草稿内容。原先持续的 404 队列已停止。已重新选择并核对同一 `.265` 镜像，向设备发起升级请求，等待运行镜像核验；不能把该持久化回执记为 USB 解锁通过。

The recovery bridge is running. One authenticated device draft-save completed with status 200, replacing the repeated 404 loop; no draft contents were inspected or logged. The same authorized firmware hash was reselected and an OTA start requested. Boot/image verification and actual USB/BLE unlock remain pending.


## 精确安装核验 / Exact installation verification

旧草稿成功持久化后，设备通过原 OTA 草稿保护并确认开始升级，已自动重启为 `0.2.265-ui`。首次 USB 启动核验失败，原因尚未确认；设备稳定后重新核验通过，桥接明确确认精确 ELF、VALID 运行分区及三个递增实时 UI 样本一致。没有因为初次失败而再次刷写或调整全局系统设置。

重新读取设备显示设置后，与安装前逐项一致；未点击保存、未恢复轮播默认。新 Mac 进程重新打开的一键解锁设置确认 Wi-Fi 和 USB／蓝牙两项权限均已保存开启，原密码正常从相同 helper 读取，没有再次输入或导出密码。现正等待“仅 USB”模式的一次实体解锁测试；该行为验收尚不能由启动核验替代。

The orphan draft saved successfully, allowing the existing OTA guard to pass. TAB5 rebooted into the exact authorized `.265` image. The first USB boot check failed for an unconfirmed reason; a subsequent check after settling passed exact ELF, VALID partition and three increasing live UI samples. No reflashing or global setting changes followed the first failure. Read-only display-setting comparison matches the pre-install settings. Both saved unlock permissions are enabled and the original Keychain record remains usable. Explicit USB unlock acceptance is pending.

## USB 首次实测与回包修复 / Initial USB acceptance and reply fix

用户在“仅 USB”模式实测返回“USB 解锁未完成”。Mac 已收到开始请求，停在等待键鼠静止后因 2.5 秒租约超时结束，收到续期为 0；设备数值诊断记录 USB RPC 无回包阶段失败。该次是普通登录锁屏，未观察到 Computer Use 外层保护。首次响应前的同步锁屏焦点查询可能耗尽设备两秒 RPC 窗口，尚不能仅凭这些记录确认它是唯一原因。

Mac 候选 `artifacts/mac-usb-ble-unlock-20261024-usb-reply/` 将 USB／BLE 锁屏查询移至既有可取消工作线程，先返回绑定请求的 `preparing`。权限、去重、配对／会话／连接代数及租约校验保留，密码输入前的独立可信控件校验和 Wi-Fi 入口次序不变。增加有界本地通道／操作／状态码／耗时诊断，不保留请求身份、正文或密码。新增 12 项隔离检查覆盖慢查询、续期、取消及未锁屏拒绝，Debug 和 Release 均为 533 项通过；Release 构建完成，部署及真实 USB 复测待执行。完整 XCTest 仍受当前 CLT 缺少 XCTest 限制。

The explicit USB test returned incomplete. The Mac accepted begin but received no lease renewals; the device reported an absent USB RPC reply. Synchronous focus inspection before the initial response can exhaust the two-second device budget, but this evidence does not establish the sole cause. The new Mac candidate acknowledges the reserved attempt before cancellable focus inspection, retaining all binding, lease and trusted-control guards and the Wi-Fi preflight order. Twelve new isolated checks cover slow inspection, renewal, cancellation and unlocked-session rejection; Debug and Release pass 533 checks. The Release build is complete; deployment and a real USB retest remain pending. XCTest remains unavailable in the current CLT installation. Firmware bytes and system settings are unchanged.


USB 回包修复候选已通过严格签名及 helper 身份／私有管道检查，从界面正常退出草稿恢复版后启动。运行路径确认只有新候选，重新打开设置确认两项权限与原钥匙串配置保留。等待一次明确仅 USB 的真实复测；当前不能记为已修复或已验收。

The signed USB reply candidate is running after normal UI shutdown of the recovery build. Strict signature and helper identity/private-pipe checks pass; both saved permissions and the existing Keychain configuration remain. One explicit USB hardware retest is pending; no functional pass is claimed.


## USB 续期竞争 / USB lease contention

回包修复版首次实体复测仍未完成：begin 处理 1 ms，随后收到 7 次 USB poll（处理均低于 1 ms），在“确认字符已唤出密码页”阶段续期超时。该阶段尚未进入实际密码写入；用户观察到的一位输入可能是固定准备字符，不能据此记录密码已发送。设备新增的失败仍为 RPC 无回包阶段。

核对实际设备调用方后发现，普通电脑状态每两秒继续查询，与本机解锁的短租约 poll 共用同一邮箱；原 host-unlock 无入队等待，遇到已在途的状态查询会立即失败。候选 `.266` 只在 host-unlock busy 期间暂停普通状态查询，终态后恢复以获取独立桌面确认；host-unlock 可在原两秒总预算内等待最多一秒入队，发布前仍核对主机／连接代数，发布后不重发。Wi-Fi worker 和 Windows PIN 请求预算不变。构建与实际源代码的邮箱竞争、超时及原生 UI 检查进行中，尚未刷写或声称实机通过。

The first reply-fix hardware retest remains incomplete. Begin took 1 ms and seven subsequent USB polls were handled in under 1 ms each, before lease expiry during public-character preparation. Actual password writing had not begun. Device status polling and host-unlock renewals share one mailbox; the previous zero-admission wait made a renewal fail immediately if a status read already occupied it. Candidate `.266` suppresses ordinary status reads only while local host unlock is busy, resumes them for independent desktop confirmation, and permits up to one second of admission within the same two-second total RPC budget. Host/link checks and no-replay rules remain; the Wi-Fi worker and Windows credential budget are unchanged. Build/source-level contention/UI validation are in progress; this candidate has not been flashed.


`.266` 候选的完整固件构建通过，27 个实际邮箱竞争／入队／超时场景在 UBSan 下通过，13 项预算选择断言和 507 项 worker 断言通过。实际 LVGL 的 USB／BLE busy 期间暂停状态请求、终态恢复、独立新状态确认、取消／断连及旧 Wi-Fi／Windows 入口回归通过；原电脑控制 focused 检查通过。使用固定 latest 的实际 sidecar 完成分类、换行及滚动预览并检查图片。Mac 源码未再修改，沿用已通过 533 项检查的 usb-reply 版。

固件 `0.2.266-ui` 为 7,190,320 字节，SHA-256 `22b7fa09c09d806a7ca937ed5b66622fc45f1dea8f95885e2f4f604e6302acce`，ELF SHA `926130ec260774205b79066cd53e04cd855a0028e9ed539dd8250910f1591583`；分区仍余 18,640 字节。生产镜像／说明校验器及固定目录准备通过；SDK 配置、依赖锁、分区、Wi-Fi worker 和 Windows one-click 源码与 `.265` 逐字节相同。`.265` 源码在复用构建目录前已保存至 `artifacts/mac-usb-ble-unlock-20261024-mailbox/source-265/` 并记录摘要，原镜像及历史包保留。

已在 Mac 正常选择固定 latest 的 `.266`，界面核对版本、完整 SHA 和更新说明一致，显示“尚未开始安装”。设备仍为 `.265`，等待本候选的用户安装授权；USB、蓝牙和新候选 Wi-Fi 解锁实机结果均不能记为通过。未推送、创建标签或发布。

The full `.266` build passes. UBSan validation passes 27 actual mailbox admission/contention/deadline scenarios, 13 budget-selector assertions and 507 worker assertions. Actual LVGL host-unlock and focused legacy control regressions pass, including busy suppression and terminal status resumption. The exact latest sidecar passes the categorized/wrapped/scrolled visual preview. The 7,190,320-byte image fits unchanged partitions with 18,640 bytes free; SDK/dependency/partition/Wi-Fi/Windows one-click files are byte-identical to `.265`. Exact `.265` sources and historical images remain preserved. The Mac selected and verified `.266` and its full hash/notes, but installation has not started. Candidate-specific authorization and hardware acceptance remain pending; no push, tag or public release occurred.


用户已明确授权安装 `.266` 并继续 USB／蓝牙实测。安装前再次核对固定 latest 的镜像、说明、完整镜像 SHA 和 ELF SHA 未变；从 Mac 正常界面发起一次升级请求，正在等待设备保存草稿／写入／重启及精确启动核验。未再次输入密码或修改全局设置。

The user explicitly authorized `.266` installation and continued hardware tests. The exact latest image/notes/image hash/ELF hash were rechecked unchanged before one normal UI upgrade request. Device preparation, writing, reboot and exact boot verification are pending. No password re-entry or global setting change was performed.


`.266` 已从已授权的正常升级请求写入并自动重启。首次启动核验失败，设备稳定后的第二次核验通过精确 ELF、VALID 分区及三个递增实时 UI 样本；没有再次刷写。该记录只确认安装及运行镜像，不代表 USB／蓝牙解锁成功。正在只读核对保留设置，随后进行仅 USB 的一次实体复测。

The authorized `.266` image was written and booted. The first boot check failed; after settling, the second passed exact ELF, VALID partition and three increasing live UI samples. No reflashing followed. This confirms installation only; USB/BLE unlock acceptance remains pending.


`.266` 安装后只读显示设置核对与安装前逐项一致，已取消设置页而未保存修改；原 Wi-Fi 和 USB／蓝牙权限均保持启用。现等待仅 USB 一次解锁测试。

Read-only display-setting comparison after `.266` matches the pre-install settings; the dialog was cancelled without saving. Both saved unlock permissions remain enabled. One explicit USB unlock test is pending.


## USB 两种锁屏起点与黑屏 / USB lock-screen and display-off acceptance

用户确认 `.266` 仅 USB 在有密码框、无密码框两种起点均自动解锁成功；黑屏起点没有亮屏或解锁。不能将这两次成功推广为黑屏、整机睡眠或蓝牙通过，30 秒未回锁也未在这条回复中单独确认。

本机诊断确认黑屏请求到达，begin/poll/cancel 均为 USB 200；停在锁屏核对后的外层保护状态未知检查，未输入密码，整机睡眠为 0，仅观察到一次显示器熄灭／亮起。原 `unlockSaved` 在调用显示器活动接口之前要求保护状态明确为 false；黑屏时显示几何信息不可得会使该状态未知，从而在亮屏前停止。

Mac 候选 `artifacts/mac-usb-ble-unlock-20261024-display-wake/` 调整显示器准备次序：已明确存在的保护直接停止；未知只允许一次有界显示器活动声明，随后重新读取保护状态，三秒内必须明确为无保护才进入原登录控件／公开准备字符／单次密码写入流程。取消、超时、唤醒失败或保护出现均停止并释放临时声明；不改全局设置，不退出 Computer Use，也不新增按键路径。固件保持已安装且已授权的 `.266`，USB/BLE RPC 和 Wi-Fi 协议未变。构建与隔离验证进行中。

The user confirmed USB unlock from both visible and hidden password-field starts on `.266`; display-off failed. The dark-display request reached the Mac over USB, but unknown protection evidence stopped execution before display activity, with no password input and no full-system sleep observed. The Mac-only candidate permits unknown evidence to initiate bounded display activity, then requires a fresh, explicitly unprotected display before the unchanged trusted-field and single-input flow. Known protection, cancellation, timeout and failures stop; temporary assertions are released. Global settings, Computer Use, the installed `.266` firmware and transport contracts remain unchanged. Build and isolated validation are in progress.


显示器准备补充的 Debug 构建及 555 项隔离检查通过（新增 22 项，覆盖未知→明确无保护、保护出现、取消、超时、慢查询、唤醒失败及临时声明释放）。完整 XCTest 仍因当前 CLT 缺少 XCTest 未通过；Release 构建进行中。

The display-preparation Debug build passes 555 isolated checks, including 22 new cases for fresh protection evidence and bounded assertion cleanup. XCTest remains unavailable in the current CLT environment; Release compilation is in progress.


显示器准备候选 Release 构建及 555 项隔离检查通过，冻结源码逐文件摘要与工作区一致。待签名、正常替换运行程序及黑屏起点的真实 USB 复测；现阶段不把黑屏记为通过。

The display-preparation Release build and all 555 isolated checks pass, with frozen source hashes matching the workspace. Signing, normal deployment and one real USB display-off test remain pending; no display-off pass is claimed.


显示器唤醒候选严格签名、helper 身份／私有管道核验通过，helper 签名字节保持。已正常退出 usb-reply 版并启动 display-wake 版，确认唯一运行路径；USB／Wi-Fi／BLE 已认证，原密码配置及两项开关均保留。等待仅 USB 的自然熄屏起点复测；未改变固件或全局设置。

The signed display-wake candidate is running after normal shutdown of usb-reply. Strict signature and unchanged helper identity/private-pipe checks pass. Existing pairing, Keychain configuration and both unlock permissions remain; USB/Wi-Fi/BLE are authenticated. One real USB display-off retest is pending. Firmware and global settings are unchanged.


## 黑屏 USB 成功与等待优化 / Successful USB display-off and latency follow-up

用户确认 display-wake 版从自然黑屏起点连续两次自动亮屏并解锁成功。Mac 诊断确认最近一次成功，记录两次屏幕熄灭／亮起、整机睡眠 0，解锁交互为 USB。设备仍为精确核验的 `.266`。蓝牙实测和当前候选 Wi-Fi 回归尚未执行。

用户反馈点击后约两三秒才执行、桌面出现后又等待两秒以上。代码核对显示 `waitForInputSource` 每次都重新等待至少三秒稳定，即使物理键鼠早已静止；该完整输入源等待原为屏幕共享键盘路径准备。本机 AX 替换前后已独立检查目标、空框／公开字符及登录按钮，因此本机直接输入只需 150 ms 新鲜焦点稳定；仍要求实际物理键鼠静止至少三秒。接入验证及临时屏幕共享测试保持原完整稳定等待。桌面恢复后的连续两秒核验保持，不提前报告成功。

Mac latency 候选另外只在内存保留最近两次请求的固定阶段名称和有界毫秒耗时，不含请求 ID、offer、配对身份、密码或输入内容；网络回包不新增字段。新隔离检查覆盖提前准备、焦点变化、实际输入活动、取消、未知静止证据、无效等待参数及诊断内容边界。构建／验证进行中，尚未部署提速版，不声称实测耗时已经下降。

The user confirmed two automatic USB wakes/unlocks from natural display-off; Mac diagnostics confirm success, two display sleep/wake events and no full-system sleep. BLE and current-candidate Wi-Fi smoke remain pending. The latency follow-up removes the redundant three-second source-stability wait only from the independently bound direct AX path, using 150 ms fresh stability while retaining three seconds of actual physical-input quiet. Enrollment/RFB preparation and the two-second stable-desktop success guard remain unchanged. Bounded in-memory stage durations for the last two attempts contain no identities, payloads or secrets and add no wire fields. Build/validation are in progress; no physical speed improvement is claimed yet.


提速候选 Debug 构建及 565 项隔离检查通过。新增 10 项覆盖本机 AX 提前就绪、焦点变化、实际键鼠静止阈值、取消／未知证据、无效稳定间隔及固定阶段诊断的隐私边界；既有默认三秒输入源等待及两秒桌面确认回归保持。XCTest 仍因当前 CLT 缺少该模块无法运行；Release 构建进行中。

The latency candidate Debug build passes 565 isolated checks. Ten new checks cover early direct-AX readiness without weakening physical-input quiet, changed focus, cancellation, unknown evidence, invalid stability intervals and timing metadata privacy. Default enrollment/RFB stability and the two-second desktop guard remain covered. XCTest is unavailable in the current CLT environment; Release compilation is in progress.


提速候选 Release 构建与 565 项隔离检查通过，冻结源码摘要与工作区一致。待正常部署和一次真实体感／分段计时复测；镜像仍为同一 `.266`，没有再次刷写。

The latency Release build passes all 565 isolated checks and frozen-source identity verification. Deployment and one real latency/stage-timing retest are pending. The same `.266` firmware remains installed without reflashing.


提速版已通过严格签名及 unchanged helper 身份／私有管道检查。从界面正常退出 display-wake 后启动 latency，确认唯一运行路径，三通道已认证，钥匙串配置和两项解锁开关保留。等待仅 USB 同一黑屏起点的一次体感与分段耗时复测；蓝牙及本候选 Wi-Fi 回归仍待执行。

The signed latency candidate is running after normal UI shutdown of display-wake. Signature/helper identity checks pass; the helper bytes, Keychain configuration and both permissions remain unchanged. Actual USB timing, BLE acceptance and current-candidate Wi-Fi smoke remain pending.


## USB／蓝牙成功反馈提速 / USB/BLE success-feedback latency

上一轮 USB 实测成功，用户确认点击到桌面的等待略有缩短，但要求继续缩短桌面出现后 TAB5 的成功反馈。读取该次本机固定阶段计时：总计约 7.14 秒，其中实际静止等待约 2.55 秒，桌面恢复确认约 2.99 秒。前一版仍按两秒稳定桌面返回成功。

feedback Mac 候选只对 USB／蓝牙选择 0.5 秒稳定桌面窗口，至少三次在不同单调时刻的独立核验均确认当前控制台已回桌面才可返回成功；未知状态重新计时，回锁、取消、超时或慢查询仍拒绝。Wi-Fi 保留原两秒确认行为，临时屏幕共享的断开后确认亦不变。密码输入前全部校验、单次提交、设备独立新鲜电脑状态确认、RPC 格式／租约及 `.266` 固件保持。构建及隔离验证进行中，尚未部署或声称实际回执已提速。

The previous USB test succeeded with a reported improvement before desktop, while success feedback remained slow. Local fixed-stage timing measured about 7.14 s total, including 2.55 s input quiet and 2.99 s desktop confirmation. The feedback candidate selects a 0.5 s stable-desktop window only for USB/BLE, requiring three distinct-time independent observations. Unknown evidence restarts stability; relock, cancellation and expired/late queries reject success. Wi-Fi and temporary RFB retain their two-second confirmation. Input guards, single submission, independent device state, protocol/lease and installed `.266` firmware remain unchanged. Build/validation are in progress; actual feedback speed is not yet confirmed.


feedback 候选 Debug 构建与 578 项隔离检查通过。新增 13 项覆盖 Wi-Fi 两秒／USB-BLE 半秒的真实执行器参数分流、至少三次不同时间采样、未知证据重置、回锁、取消、慢查询及非法阈值拒绝。原默认两秒确认检查仍通过。完整 XCTest 因当前 CLT 缺少 XCTest 无法运行；Release 构建进行中。

The feedback Debug build passes 578 isolated checks. Thirteen new checks cover actual executor duration routing, distinct-time samples, unknown-evidence reset, relock/cancel/late-read rejection and invalid thresholds; the accepted Wi-Fi two-second tests still pass. XCTest is unavailable in the current CLT environment; Release compilation is in progress.


feedback 候选 Release 构建及 578 项隔离检查通过，冻结源码逐文件身份核验通过；待签名部署及真实成功反馈计时。`.266` 固件未修改或重新刷写。

The feedback Release build and all 578 isolated checks pass; frozen source identity matches the workspace. Signing/deployment and actual success-feedback timing remain pending; `.266` firmware is unchanged.


feedback 候选严格签名及未变 helper 身份／私有管道核验通过，已正常退出 latency 并从唯一的新路径启动。三通道认证、原钥匙串配置及两项解锁权限保留；没有再次刷机或修改全局设置。等待一次仅 USB 普通锁屏测试，着重桌面出现到 TAB5 成功提示的实际间隔和 30 秒未回锁。蓝牙验收及当前候选 Wi-Fi 回归仍待执行。

The signed feedback build is running after normal UI shutdown of latency, with unchanged helper identity, pairing, Keychain configuration and both permissions. One ordinary USB lock test is pending for actual success-feedback timing and no-relock observation. BLE acceptance and current-candidate Wi-Fi smoke remain pending; no reflashing or global setting changes occurred.


feedback 实机 USB：用户确认“这次解锁很快”。本机只读诊断确认最近两次均成功，桌面确认阶段分别约 1.36／1.15 秒，总执行约 3.59／3.27 秒；前一版该阶段约 2.99 秒。测试起点不同，不能将总耗时差额当作严格同场景基准。用户自行进行了第二次尝试，未把它计为已明确完成 30 秒观察；当前无再次锁定的明确报告。下一步仅蓝牙及现候选 Wi-Fi 回归待执行。

The user confirmed fast USB unlock. Local diagnostics confirm two successful attempts, with desktop-confirmation phases about 1.36/1.15 s and total execution about 3.59/3.27 s; the previous measured confirmation phase was about 2.99 s. Different starting conditions preclude a strict total-time benchmark. The repeated user attempt is not counted as an explicit 30-second no-relock observation. BLE and current-candidate Wi-Fi smoke remain pending.


## 蓝牙成功与首次点击遗漏 / BLE success and first-tap loss

用户确认仅蓝牙、USB 拔出时锁屏和解锁均成功；本机诊断也核对实际 BLE 请求及成功结果，桌面确认阶段约 1.23／1.42 秒，总流程约 3.05／3.14 秒。用户同时报告偶尔需要点两次按钮，本项尚未完成稳定性验收。此前 USB 反馈提速已获用户“这次解锁很快”确认。

实际 LVGL 的红例复现了后台只读请求 busy 时按钮仍可点击、处理函数却直接返回，读完后副作用命令数仍为 0。另一个窗口是刷新末尾刚开始轮询，界面缓存仍显示空闲；修复在点击时重新取状态，并仅为在途只读查询保留一个明确点击，最多等三秒。新读取完成后必须仍为同一电脑／会话／连接／模式、同一预期锁定状态且具有更新样本，才消费并执行一次；重复点击不叠加，换电脑、断连、换页面、超时或状态不确定均取消。已发出的副作用不重发、不切换通道。锁屏后立即恢复状态查询，移除原 3.5 秒提示等待。

90 个实际 LVGL 点击场景及原 host-unlock、电脑控制 focused 回归通过。生产 computer-control worker 的 UBSan 检查核对只读忙碌标记和终态清理；完整 `.267` 构建进行中。沿用未修改的 Mac feedback 版，固件 Wi-Fi worker、Windows one-click、host-unlock、RPC 邮箱、配置、依赖及分区均保留 `.266` 原字节。安装、点击稳定性和 Wi-Fi 实机回归仍待完成。

The user confirmed successful BLE lock/unlock with USB unplugged; Mac metadata also shows BLE success, approximately 1.23/1.42 seconds for desktop confirmation and 3.05/3.14 seconds overall. Intermittent first-tap loss remains open. Actual LVGL reproduces a dropped explicit tap during a read-only busy interval, including a stale-frame race after polling begins. The candidate refreshes state on click and retains at most one tap behind a read for three seconds, executing once only after a newer matching lock sample and unchanged peer/session/connection/mode. Changes, uncertainty, navigation or timeout cancel it; sent effects are never replayed or moved to another transport. Lock commands trigger immediate state refresh. Ninety LVGL cases and existing host/control regressions pass; production worker UBSan checks pass and the full `.267` build is in progress. The running Mac, unlock workers, RPC mailbox, Windows credential path, configuration, dependencies and partitions are unchanged. Installation and hardware acceptance remain pending.


`.267` 完整构建、生产镜像／说明校验、固定 latest 准备及实际 sidecar 正常／滚动图片检查通过。镜像 7,191,936 字节，原分区余 17,024 字节；SHA-256 `60ccffe6c2e1625a36b11ee3ad60eeec72b737b02b11619ad51fd538d08ff0e4`，ELF SHA `d80dc5ec2fd937e7bc5911a87001df46dae9f49d7482dd9b68163c8abb638f8a`。25 个文件的累计源码补丁在归档 `.264` 基线上通过 `git apply --check`；952 个冻结源码文件与构建后逐项摘要一致。旧 `.266` 镜像及源码快照已保留。源码配置、分区、依赖、Wi-Fi／Windows／host-unlock worker 和 RPC 邮箱核对未变。Mac 源码本次没有修改，沿用现有签名 feedback 版。

新候选尚未安装。界面工具报告 Mac 锁定，已请用户手动回到桌面；未尝试绕过外层保护或自动解锁。按固件工作流，待本次 `.267` 的用户授权后才通过正常升级界面安装，并核验精确镜像、启动状态、设置保留及蓝牙首次点击。`git diff --check` 通过；未推送、创建标签或发布。

The full `.267` build, production image/notes validation, fixed-path preparation and normal/scrolled actual-sidecar visual checks pass. The 7,191,936-byte image leaves 17,024 bytes in the unchanged partition. Its SHA-256 is `60ccffe6c2e1625a36b11ee3ad60eeec72b737b02b11619ad51fd538d08ff0e4`; ELF SHA is `d80dc5ec2fd937e7bc5911a87001df46dae9f49d7482dd9b68163c8abb638f8a`. The cumulative 25-file patch applies cleanly to archived `.264`, and all 952 frozen source hashes still match after building. Previous `.266` source/image evidence is preserved; configuration, dependencies, partitions and the existing unlock/RPC workers are unchanged. The Mac is unchanged. Installation has not started: the UI tool reports a locked Mac and manual return to desktop has been requested. Candidate-specific authorization, exact boot verification and physical click/Wi-Fi regression remain pending. No protection bypass, push, tag or public release occurred.


用户已明确授权 `.267` 安装及继续实测，并确认 USB 已接回、Mac 在可操作桌面。安装前再次核对固定 latest 的版本、镜像 SHA、ELF SHA 和更新说明与已审阅候选一致；原生界面确认运行 `.266`、USB 数据通道有效、两项解锁权限仍启用。正在正常界面核对设置并准备升级；尚未宣称新固件已写入。

The user explicitly authorized `.267` installation and continued tests, confirmed USB connection and an operable Mac desktop. Fixed latest version/image/ELF/notes match the reviewed candidate. Native UI shows installed `.266`, confirmed USB data and both existing unlock permissions enabled. Read-only settings checks and normal upgrade preparation are in progress; installation is not yet claimed.


首个 `.267` 升级请求未收到确认，实时 USB 诊断仍为 `.266`，未据此认定写入。用户随后确认设备选择仅 USB、可看到 `.267`；新的只读显示设置请求成功，比较基线仅保留在界面控制会话内存，取消编辑且未写设置。在已确认链路上重新发起一次正常升级请求，完整 7,191,936 字节传输完成；正在等待写入／重启和精确启动核验，未继续重发。

The first `.267` request was unconfirmed while live USB diagnostics still showed `.266`; no installation was inferred. After the user confirmed USB-only mode and the `.267` offer, a read-only display-settings request succeeded; its comparison baseline stays only in UI-session memory and editing was cancelled. One further normal request on the confirmed link transferred all 7,191,936 bytes. Writing/reboot/exact boot verification are pending; no further request was sent.


`.267` 已完成授权安装。第一次 USB 启动读取未确认，设备稳定后第二次核验通过精确 ELF、VALID 分区及三个实时界面样本；没有再次写入。升级后显示设置只读核对 16 个值／顺序控件与升级前完全一致，取消窗口而未保存。原密码、权限与运行 Mac 未改。接下来验收仅蓝牙首次点击及纯 Wi-Fi 回归；安装通过不等于这两项功能验收完成。

Authorized `.267` installation is complete. The first USB boot read was unconfirmed; after settling, exact ELF, VALID partition and three live UI samples passed without reflashing. All 16 display-setting value/order controls match the pre-install baseline; editing was cancelled without saving. Credentials, permissions and the running Mac remain unchanged. BLE first-tap and pure Wi-Fi physical acceptance are next and are not inferred from boot success.


## `.267` 蓝牙失败后续 / BLE acceptance failure follow-up

用户确认仅蓝牙、USB 拔出后的约六七次锁屏操作等待或状态未知，之后一次实际解锁成功但设备提示未完成；又补充连续点击仍无法锁屏。现保留这些失败，不再请求连续重试。Mac 最近成功总计 2,974 ms，桌面确认 1,230 ms；只说明 Mac 实际完成本次解锁，不能证明设备收到成功回执。

Mac-only 候选修正显示判断：桥接自身为 `.accessory` 菜单栏应用，旧状态判断要求 `.regular` 前台应用，故桥接自己的窗口焦点会产生未知。新路径只在显示状态中允许系统 AX 的本进程焦点；仍要求当前控制台、明确无外层保护和已解锁观察，锁定观察始终优先。密码输入和后端桌面成功核验继续使用原普通应用规则，不以这项显示证据授权输入或成功。

已结束的 USB／蓝牙执行保留最多 2.5 秒终态读取窗口，仍受原 40 秒总时限约束；输入期间续期规则、Wi-Fi 租约行为和取消／身份校验保持。过期清理不再覆盖已记录的实际成败。新增仅内存诊断保留锁屏命令处理结果、固定的解锁逻辑回执（区分 HTTP 200）以及现有设备 `unlockHistory` 中最多八条有界数字记录，不保留原文、身份、凭据或内容，不改变协议或授权。

Debug 构建及 621 项隔离检查通过，包含菜单栏显示和密码核验边界、终态延迟／失败／总超时／Wi-Fi 保持、设备记录解析及诊断隐私。Release 构建和部署待执行；未声称新修复已经硬件通过。

The Mac-only candidate separates own menu-bar focus used for display from the unchanged strict credential/desktop-success checks. Console, explicit protection absence and unlocked observations remain mandatory; locks always veto. Completed USB/BLE attempts retain only their terminal receipt for up to one 2.5-second window inside the unchanged 40-second total budget; active-input leases, Wi-Fi behavior, cancellation and peer checks remain unchanged. Expired cleanup does not erase recorded outcomes. Bounded in-memory diagnostics add lock command results, whitelisted logical unlock receipts and up to eight numeric records from the existing `unlockHistory` field, never raw text, identities or secrets. Debug and 621 isolated checks pass; Release/deployment and hardware acceptance are pending.


Mac ble-confirmation 候选 Debug／Release 均通过 621 项隔离检查、严格签名和未改钥匙串 helper 核验，已正常替换 feedback 版运行。现有设备记录在新会话中成功读回：一次蓝牙未完成，约 5,080 ms，状态样本和 RPC 样本已旧约 8,825 ms；只记录固定阶段／时长／位标志，未读取密码或输入内容。新桥接冷启动尚无锁／解通知时仍显示未知，因此尚未请求用户再测试。

进一步的 ble-priority Mac 候选补齐显示专用证据：匹配的普通前台／本进程 AX 桌面焦点，完整且没有可见已验证 loginwindow 的窗口样本、当前控制台和明确无保护共同决定显示状态；有锁定观察始终优先，窗口采样不完整时不报告未锁屏。密码输入和后端桌面成功核验仍只使用原 `lockedState()`，不使用该显示辅助证据。

蓝牙只在真实新控制请求完成后保留一秒后续控制优先，收到空／重复读不续期；背景心跳最长四秒仍获得发送机会，语音既有优先及确认规则保留。完成实际控制回包后允许一次短延迟继续读取，避免两秒背景大帧抢在后续解锁 poll 前面。Wi-Fi worker、USB 调度、固件和系统设置不变。后续构建与实测待完成。

The signed ble-confirmation Mac passes 621 Debug/Release isolated checks and is running with unchanged helper/credentials. Existing device history was recovered: one incomplete BLE attempt after about 5,080 ms, with state/RPC samples around 8,825 ms old. The cold-start unknown-state boundary remained, so no new physical test was requested. The ble-priority candidate adds display-only matching desktop focus plus complete absence of visible verified loginwindow windows, current console and explicit protection absence; locks veto and incomplete window samples cannot report unlocked. Credential/backend-success rules remain unchanged. Real completed control requests receive a one-second BLE continuation priority with four-second background-heartbeat fairness; empty/duplicate reads never renew it, voice rules remain, and one short continuation follows an actual control reply. Wi-Fi, USB scheduling, firmware and global settings are unchanged. Build and hardware acceptance are pending.


最终 ble-priority Mac 的 Debug／Release 均为 635 项隔离检查通过；完整 `swift test` 再次因 CLT 缺少 XCTest 未通过。冻结源码摘要、严格签名、原 helper 字节及私有管道核验通过。已正常退出 ble-confirmation 并启动 ble-priority，原权限／钥匙串配置保留，TAB5 继续 `.267`，未再次刷机。启动后的真实蓝牙四个新鲜状态请求均为 200／未锁定，Mac 处理 8–24 ms；当时锁／解通知计数为 0，显示辅助证据明确，验证了冷启动显示边界。这不代表锁屏、解锁或终态误报已经实机通过。

The final ble-priority Mac passes 635 isolated checks in both Debug and Release; full XCTest remains unavailable in CLT. Frozen-source, strict signature and unchanged helper/private-pipe checks pass. It replaced ble-confirmation normally, preserving saved permissions/credentials and installed `.267`. Four fresh real BLE status replies correctly report unlocked after cold start (Mac handling 8–24 ms) with zero lock/unlock notifications and explicit display evidence. Physical lock/unlock, result matching and Wi-Fi regression remain pending; no additional flashing occurred.


## ble-priority 蓝牙首点通过 / BLE first-tap hardware pass

用户按“仅蓝牙”、USB 拔出、等待控制提示退出的流程确认：首次点击锁屏成功、首次点击解锁成功、TAB5 结果提示正确，桌面观察 30 秒未回锁。此为用户实机确认。随后只读打开桥接窗口成功，但点击诊断入口时工具报告 Mac 锁定／物理输入暂停，尚未读取本轮独立回执；没有据此要求重复蓝牙解锁，也没有试图绕过限制。纯 Wi-Fi 和新 Mac 版 USB 回归待完成，仍未将整机睡眠或 Computer Use 外层交接记为通过。

The user confirmed first-tap BLE lock and unlock, matching device results and no relock for 30 seconds, following USB-unplugged/BLE-only instructions. Post-test native inspection reached the bridge window, but entering diagnostics was blocked by the tool's lock/physical-input state; independent receipts are still pending. No repeat BLE unlock or bypass was attempted. Pure Wi-Fi and current-Mac USB regression remain pending; full sleep and Computer Use handoff are not accepted by implication.


## Wi-Fi 通过与独立回执核对 / Wi-Fi pass and independent receipts

用户确认仅 Wi-Fi、USB 拔出时，首点锁屏和解锁成功、设备提示正确、30 秒未回锁。本次原生界面恢复可操作，核对设备历史中蓝牙第 2、3 条为成功（总计 4,578／5,089 ms），Wi-Fi 第 4 条为成功（4,865 ms）；仅有最初第 1 条保留为旧失败。Mac 最新 BLE 后端成功为 3,238 ms，终态 poll 明确回“已确认解锁”、清理回“已结束”；Wi-Fi 后端成功 4,473 ms，其中桌面确认 2,731 ms，原两秒规则保持。近期锁屏命令也分别记录实际 BLE 与 Wi-Fi 通道 200 回执。两次权限均启用，未保存或重新输入密码。

这些记录支持用户观察到的结果；30 秒未回锁来自用户实测确认，不能将新收到的单条状态回包当作独立的 30 秒观察。当前 Mac 版 USB 回归和 USB 拔线后的蓝牙交接回归尚待完成；整机睡眠计数仍为 0。

The user confirmed pure-Wi-Fi first-tap lock/unlock, matching device results and 30 seconds without relock. Native inspection now verifies device BLE successes #2/#3 at 4,578/5,089 ms and Wi-Fi success #4 at 4,865 ms, retaining initial failure #1. The latest BLE backend succeeded in 3,238 ms with an explicit confirmed-unlocked poll and ended cleanup; Wi-Fi succeeded in 4,473 ms with 2,731 ms desktop confirmation, preserving the two-second rule. Lock receipts show their actual BLE/Wi-Fi transports. No password entry or permission change occurred. The no-relock interval is user-confirmed, not inferred from one fresh status sample. Current-Mac USB and unplug-to-BLE handoff regression remain pending; no full-system sleep was observed.


## 最终 USB 与拔线切蓝牙验收 / Final USB and unplug-to-BLE acceptance

用户按两轮流程确认“USB 和拔线后蓝牙均首点成功，提示正确，未回锁”；本轮要求每次桌面观察 30 秒。随后原生诊断刷新确认：设备第 6、7 条 USB 成功，总计 3,971／3,977 ms；第 8 条蓝牙成功，总计 5,006 ms，独立状态样本为 99／204／399 ms。Mac 最新 USB 后端成功 3,523 ms，桌面确认 934 ms；BLE 成功 3,729 ms，桌面确认 1,429 ms。两通道均有实际锁屏命令 200 回执；蓝牙终态 poll 为“已确认解锁”，清理为“已结束”。这些固定数值不含身份、凭据、原始控件或输入内容。

设备还保留第 5 条 USB 准备阶段未完成记录（375 ms、原因 20）；仅凭该数值记录不能关联到具体点击或认定原因，不将其删除或改成成功。最终成功记录和用户两轮确认支持本次普通流程验收，不代表所有历史尝试成功或长期稳定性统计。拔线到状态恢复的具体耗时没有收到测量值。30 秒不回锁来自用户观察，不能由单次状态回包推定；整机睡眠未测试。

当前保留已通过的 ble-priority Mac 与精确 `.267`，未继续刷机、重设密码、改显示偏好或系统全局设置。完整 XCTest 因 CLT 缺少模块受阻，Debug／Release 各 635 项隔离检查及签名检查已通过；本次收尾仅更新记录，没有改变已验收代码。Wi-Fi 回归、USB、蓝牙及拔线交接的普通锁屏场景完成；其他实机边界按顶部范围保留。

The user confirmed first-tap USB and post-unplug BLE lock/unlock, matching results and no relock, following two rounds requiring 30-second desktop observation. Fresh native diagnostics verify USB successes #6/#7 at 3,971/3,977 ms and BLE success #8 at 5,006 ms, with independent state samples 99/204/399 ms old. The latest USB backend succeeded in 3,523 ms with 934 ms desktop confirmation; BLE succeeded in 3,729 ms with 1,429 ms desktop confirmation. Actual lock commands returned 200 on both transports; BLE terminal polling confirmed unlocked and cleanup ended. Only fixed numeric metadata is recorded.

An additional USB preparation-stage incomplete record #5 (375 ms, reason 20) remains preserved; its numeric metadata does not identify the user click or root cause. Final successes and the user's two-round confirmation support this ordinary-flow acceptance, not a claim that every historical attempt succeeded or a long-term reliability statistic. Handoff recovery time was not measured. The 30-second interval comes from user observation, not one status sample; full sleep was not tested. The accepted Mac and exact `.267` remain running, with no further flashing, credential enrollment, display or global changes. Closeout edits affect records only, retaining the already-validated code and its 635 checks per configuration; full XCTest remains unavailable.
