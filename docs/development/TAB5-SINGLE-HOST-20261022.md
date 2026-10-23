# TAB5 单一电脑通道归属与 Mac 迁移（2026-10-22）

## 用户确认的规则

- 所选可信电脑同时约束 USB、Wi-Fi、BLE；通道不可用时等待，不能自动改连其他电脑。
- 已绑定电脑的 USB 签名认证成功可自动接管；未绑定电脑仅插 USB 不改变目标。首次绑定由用户明确发起，保留其他可信电脑。拔 USB 保留所选目标。
- Wi-Fi 发现、BLE 连接/系统 bond/公开 hostId 都不能改选电脑。手动切换遵循已有 USB 占用与升级忙碌门槛。
- 保留配对、凭据、系统 bond、网络与显示设置。本轮 Mac 只有迁移参考，没有操作用户的实际 Mac 本地未提交代码。

## .237 实现与验证

固件增量见 [TAB5-SINGLE-HOST-237.patch](TAB5-SINGLE-HOST-237.patch)，在 .236 上追加：独立 `host_ble_v1` 身份映射、所选 peer 过滤、同一 BLE 连接的密钥证明、未验证连接时限、旧电脑代次失效、Wi-Fi 解锁通道判定和延迟 LAN 发现响应隔离。公开归属提示新增 `bleProofRequired:1`；Windows 先发送 `tab5_ble_owner` 短证明并校验签名 ACK，再进入 USB 待命。具体协议见 [PROTOCOL](../PROTOCOL.md)。

- ESP-IDF 交叉编译通过，7,155,696 字节，镜像 SHA-256 `24e2441216ad25b22f0eb59265b338ff90fed2fb8f08e1c72c7e60df3cc23266`；ELF `e16603d1c8543cc5b399e55a6f30be0757ae2f00532f52238f246539927e6256`。
- 七项原生测试通过：可信电脑、BLE 归属、短证明、Wi-Fi 归属、广播、BLE 键盘后端、语音 mailbox；完整 LVGL 回归及实际 sidecar 顶部/滚动预览通过。源码增量在隔离目录重放通过。
- Windows 配套短证明版本构建 0 警告/错误，BLE 传输/USB 待命/小 MTU/签名 ACK 回归、可信电脑回归通过。普通 Explorer 桌面启动及 `--status-once` 通过，未绕过资料目录保护。既有设置、配对、凭据和登录辅助组件哈希不变。
- 用户明确批准对应候选后，通过桥接正常 OTA 发起一次应用升级。实际采用 Wi-Fi 分块压缩，传输 4,898,524 字节约 7.55 秒；设备记录写入约 9.73 秒、校验约 1.32 秒。
- 实际 `.237`、精确 ELF、`ota_1 VALID` 与递增 UI 心跳通过；亮度仍为 79，显示错误为 0，USB 认证及 Wi-Fi 恢复。初次核验发生在固件连续 30 秒健康检查完成前而未确认成功；等到健康检查完成后的正式核验通过，没有重复刷写。

## 尚未通过的蓝牙项

Windows 实机在服务/电脑归属特征发现阶段出现 `0x8000FFFF` / `0x80070016`，尚未到密钥证明。用户确认 Mac 蓝牙关闭、Windows 蓝牙开启后仍可复现，不能归因为 Mac 抢占。

定向查询 TAB5 服务、缓存优先的服务/特征查询均未完成真实蓝牙认证；缓存优先实验已从最终源代码移除，保留定向查询与分阶段诊断。USB 与 Wi-Fi 持续正常。不能把蓝牙白名单、短证明或两机互斥标记为真机通过。

缓存兼容是排查方向，尚未证实为唯一根因。[Microsoft 的 BluetoothCacheMode 文档](https://github.com/MicrosoftDocs/winrt-api/blob/docs/windows.devices.bluetooth/bluetoothcachemode.md)说明 GATT 缓存属于整个系统，服务变化通知可使缓存失效。现场 NimBLE 已注册标准 Service Changed 特征、保留 NVS bond，但应用没有主动声明升级后的服务布局变化。后续候选补充该通知；不删除配对记录。

## .238 蓝牙升级兼容及安装

相对 .237 仅在 NimBLE 启动同步完成、开始广播前调用标准 `ble_svc_gatt_changed(1,0xffff)`；通过已有 Service Changed 特征通知已订阅的 bond 刷新服务表，不删除配对。源码增量见 [TAB5-GATT-CACHE-238.patch](TAB5-GATT-CACHE-238.patch)，独立重放通过。此处不宣称缓存已证实为唯一根因，也不以通知代码代替蓝牙认证实测。

- 镜像 7,156,208 字节，SHA-256 `d141144b93c2aaf0b2e122bdf5a857299691bf93f803af7aabebb404b19c9128`；ELF `78e6b2758d1673a50bf5dcbc824b11569e7a59393ba134aee7c20c0f856bf77a`。
- ESP-IDF 构建、正式准备脚本、实际 sidecar 顶部/滚动预览及完整 UI 回归通过。`latest/aibot_tab5.bin` 和 `versions/TAB5` 指向本地 .238，历史 .237 保留归档。
- Windows 最终源代码 DLL 为 `e0526c7575ad90c0358d42cc0dd26f6188f895745a846e65b7a8b7467df9e9b6`，构建 0 警告/错误、固定路径部署与正常桌面状态检查通过；缓存优先实验已移除，用户资料/凭据和辅助组件不变。
- 用户另外批准这份 .238 后，正常 OTA 仅发起一次。Wi-Fi 压缩传输 4,898,995 字节约 7.81 秒；设备记录写入约 9.81 秒、校验约 0.94 秒。实际精确版本、ELF、`ota_0 VALID` 与递增 UI 心跳通过，原亮度 79、显示错误 0，USB 签名认证和 Wi-Fi 恢复。
- Windows 蓝牙在初始重试后完成设备及短归属证明认证，进入“USB 优先待命”，MTU 517；没有把旧的发现错误当成当前连接成功。此结果证明当前 Windows 蓝牙认证恢复，尚不证明缓存是先前故障的唯一原因。
- 用户保持 USB 接 Windows，关闭 Windows 蓝牙后在 Mac 尝试连接已有 TAB5，再重新打开 Windows 蓝牙。用户反馈 Mac 列表“好像连接”，但 TAB5 蓝牙标识没有连接；Windows 重开后设备蓝牙恢复。设备记录在 Windows 关闭时仍为已认证 USB 主机，随后 Windows BLE 重新完成短证明并待命，三个递增设备样本确认恢复，MTU 517。未见目标改选或有效 Mac 蓝牙通道。
- 此处只确认设备认可的通道归属和 Windows 恢复。Mac 系统列表显示的具体含义、是否有瞬时底层连接尚未从 Mac 日志核查，不能把系统列表文字当作已取得控制权，也不能声称所有底层连接情形均已实测。Mac USB 接管及新的 Mac 桥接无线跟随仍需其当前本地代码移植完成后实测。证据见 `ble-238-owner-witness.json` 和 `ble-238-mac-attempt-samples.jsonl`。

## .237 的 Wi-Fi 解锁复测

用户拔出 USB、关闭 Windows 蓝牙、开启 Mac 蓝牙后，Windows 诊断确认 USB 未连接、蓝牙关闭且 Wi-Fi 桥接正常。壁纸页与指纹页各点一次一键解锁，用户确认两次实际解锁成功。设备 attempt=1/2 均为 `transport=Wi-Fi/result=success`，设备计时约 5.58/5.18 秒；桥接分别确认 unlocked，约 5.09/4.69 秒。未重复点击或记录凭据内容。

用户同时说明 Mac 蓝牙没有实际连接到 TAB5。因此本轮证明上述连接条件下的两次完整 Wi-Fi 解锁，不能宣称已经复现并通过“Mac 实际占用蓝牙”或双机同时在线的完整互斥验收。相应证据为任务目录 `wifi-mac-on-before-test.json`、`wifi-mac-on-after-test.json`。

## Mac 当前开发代码迁移

用户的实际 Mac 代码在本地开发、未提交，基线未提供；不能合并旧 adapter 分支或强行应用补丁覆盖它。

本地交接目录为 `artifacts/development/tab5-single-host-20261022/mac-binding-migration-20261022/`，同级 ZIP 内含操作说明、可直接交给 Mac 当前会话的任务文字、精确协议/验收表、两份明确标注基线的参考补丁及 SHA-256。第二份参考补丁在任务自有目录重放通过；ZIP 内容逐项验证。未在 Windows 声称 Swift 编译成功。

Mac 会话先备份现有未提交源码，再按语义补齐 USB 追加绑定、签名接管、无线待命与 USB 待命前的 BLE 短证明；复用现有队列/actor 和共享序列分配，保留已有功能。必须在实际 Mac 执行 `swift test`、`swift build -c release` 和双电脑真机验收。Mac 密码解锁不在这次迁移范围。

## 证据与范围

任务目录 `artifacts/development/tab5-single-host-20261022/` 保存构建日志、原生/Windows 回归、镜像与源码摘要、增量重放、部署回执、正常 OTA 请求、启动核验、实时心跳和实际说明截图。参考旧 Mac adapter 源码只读使用；未执行 Git commit、push、tag、主干合并或公开发布。

相机 `.236` 的 6/12 仍是独立未修复项；本次没有把通道修复当作相机修复。一位密码实测仍按用户要求推迟。USB/Wi-Fi 的旧 PIN 验收记录另见 [Windows 登录方式](WINDOWS-LOGIN-METHOD-20261022.md)。
