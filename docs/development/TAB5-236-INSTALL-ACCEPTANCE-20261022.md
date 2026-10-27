> 后续单一电脑归属、.237 实际升级、蓝牙发现故障与 Mac 本地迁移见 [最新记录](TAB5-SINGLE-HOST-20261022.md)。本文件保留 .236 安装时的历史结果；Windows 自动选择登录方式的后续验收见 [登录方式记录](WINDOWS-LOGIN-METHOD-20261022.md)。

# TAB5 .236 安装与真机验收（2026-10-22）

## 授权和范围

用户明确要求安装新的 Windows 桥接、刷入 `0.2.236-ui`，先测试拍照和现有密码解锁；一位密码以后再测。仅本地交付，没有推送、标签、主干合并或公开发布。Mac 构建和 Mac 密码扩展仍另行处理。

## 已安装并核验

- Windows 固定运行路径保持 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.exe`。最初安装多电脑候选后，真机发现 .236 的 USB 差量状态包不续期主机租约，引起约 8 秒后重连。Windows 补充每 4 秒以内一次完整状态 ACK，保留期间的差量、metrics 和 RPC；旧固件行为保留。
- 最终 Windows DLL SHA-256：`32f7d2a2148ac73d7ddc40e760c3839ec2ed4dd8d2c7ce43f8ba5d1018bb967d`。构建 0 警告/错误，42 项可信电脑检查、RPC 及遥测/差量 ACK 回归通过。普通 Explorer 桌面环境的 status-once、串口运行时和资料文件核对通过；AppProfileGuard 保留。
- TAB5 7,150,592 字节，SHA-256 `0ef426380b135e3eaa0622b3094e77c605237e262ea0315dd816eaf5dc628a6e`，ELF `91693b6155750a957f384d9addc13540ba904226cf1ebe77353da4cf3be1cb3b`。最终 Windows 心跳修正没有重新刷入固件。
- 旧 .234 的 USB RPC 为 HMAC rejected，未传固件。按授权进入 USB ROM 下载模式，核对同一 MAC、ESP32-P4 v1.3、16 MiB 及安全状态。完整 Flash 备份并校验后，只写空闲 `ota_0` 和一份启动选择扇区；NEW 状态保留启动回退检查。
- 与预期完整 Flash 比较通过：NVS 配对/配置、启动程序、分区表、PHY、资源和旧 .234 `ota_1` 均未改写。原始备份已核对摘要并移到本机 AppData/Local/AI-bot/Backups/TAB5，位于 Git 仓库之外；包含秘密，不能纳入源码、日志附件或发布包。
- 用户短按 RESET 后，实际版本 .236、精确 ELF、`ota_0 VALID` 及三份递增 UI 心跳通过；显示错误和欠载为 0，亮度设置仍为 79。
- 使用本机既存密钥加入新固件可信列表，签名接管通过；本机配对和已保存凭据文件逐字节不变。旧设备身份留作独立信任记录；Mac 实际连接未在这台 Windows 上验收。

## 功能验收状态

| 项目 | 状态 |
| --- | --- |
| 精确固件、VALID 分区、界面心跳 | 通过 |
| USB 90 秒连接稳定性 | 通过，46 份样本；完整心跳修正后没有新 RPC 中断 |
| Codex 页面拍照、重拍、图库后拍照与加入草稿 | 未通过；首次打开即复现 6/12，后续拍摄/草稿项目未执行成功 |
| 现有密码实际解锁 | 部分通过；已停在 PIN 输入页时成功，人脸/指纹等页面须先手动选择对应输入页 |
| 一位密码 | 用户明确推迟；不能用当前密码或模拟测试代替 |
| Mac 编译、Mac 真机及双机完整回归 | 未完成 |

相机、照片与解锁诊断采用数字状态/计数，不记录照片内容或密码。实际照片只加入用户草稿，测试不要求向 Codex 发送新消息。

## 证据

原项目任务目录 `artifacts/development/multi-host-20261021/`：

- `deployment-236-20261022-092655/`：首个多电脑候选安装。
- `deployment-236-20261022-094102/`：完整心跳兼容修正安装，原运行文件保留备份。
- `boot-236-verified-20261022.json`：实际 ELF、运行分区和心跳。
- `windows-enrollment-20261022.json`：签名接管和本机资料不变。
- `hardware-samples-20261022.jsonl`、`usb-stability-20261022.json`：功能诊断和连接观察。
- `windows-lease-build-20261022.log`、`windows-lease-host-tests-20261022.log`、`windows-lease-rpc-20261022.log`、`windows-lease-telemetry-20261022.log`：最终 Windows 验证。

## 实际失败与证据边界

用户报告“相机缓存区不足，请重新打开 (6/12)”。设备同次回传 `cameraDiag=6,12,...`，帧计数为 0；原始相机线程的首个驱动错误为 `esp_video: Failed to create buffer`。失败发生在 VIDIOC_REQBUFS 的帧池创建/映射阶段。单帧 RGB565 1280×720 为 1,843,200 字节，当前代码申请三帧；事后诊断约有 13.95 MB PSRAM 空闲。现有回传未提供失败瞬间的最大连续 PSRAM 块、具体失败帧索引和请求属性，不能凭总空闲量把根因确定为碎片化，也不能把清理图库或重新打开当作已修复。保留 .236 及其源码，未继续刷入猜测性修复。

现有 PIN 的第一次 USB 自动解锁回传为 `failure / input_interrupted`，`hidSubmitted=True`，总耗时 11,646 ms；第二次为 `success`，总耗时 2,180 ms。用户先确认只有先停在 PIN 输入页才能成功，随后补充：Windows 有时停在人脸或指纹识别页，需要先选择密码或 PIN。该结果证明当前保存凭据在匹配输入页上的提交与解锁成功，不能证明能从人脸/指纹页自动识别并选择匹配的 PIN/密码输入页，也未验证未知焦点、其他登录方式或一位密码。原接口对 LockApp 封面只做正向识别，unknown 不是凭据输入框已验证，当前没有自动选择登录方式的实现；本次失败不能仅归因为唤起锁屏封面。

实际诊断文件：`camera-driver-failure-236-20261022.json`、`camera-failure-236-20261022.txt`、`unlock-current-password-result-20261022.txt`。真实拍照仍失败，从任意登录方式开始的完整一键解锁尚未通过，本次不能标记两个问题均已修复。
