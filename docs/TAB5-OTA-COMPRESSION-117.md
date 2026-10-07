# TAB5 .117 三通道压缩升级验证

2026-10-05，.117 已通过蓝牙安装并完成 USB 启动核验；未公开发布。

## 候选与行为

- 固件：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，6,869,760 字节，SHA-256 `a9de55e909cc2d272dadb29c0051764961e0216dc8c8b966652a21426a5fda71`，ELF SHA-256 `270abee215a5361ad5f892d6181bcf2d9e79a0dde0b74cc740897fb6385d4a2e`。
- 归档：`artifacts/firmware/tab5/versions/0.2.117-ui/a9de55e909cc-f4442209/`；配套桥接：`artifacts/development/tab5-ble-ota/bridge-all-transports/`。运行中的旧桥接不会因替换 latest 文件自动加载新 offer。
- USB/BLE 压缩载荷 3,600,512 字节，含加密回复 3,616,444 字节；Wi-Fi 分块流含头 3,602,752 字节。载荷减少 47.59%，Flash 中仍写完整原始镜像。
- 三通道协商压缩、旧端原文兼容。固件升级页显示“通过蓝牙升级”；自动升级顺序 Wi-Fi > USB > BLE，固定模式和安装期间不跨通道。
- 压缩接收时显示镜像字节与传输数据量，保留备用分区、完整哈希、镜像身份、启动验证和下载中断保护。
- Codex 额度页显示“本周已使用”和单项“距重置”；右侧保留实际重置日期。

## 内部证据

安装前按用户要求移除滑动提示及存储测速入口，内部诊断保留；.116 的仅蓝牙按钮错误显示“等待连接”，连接就绪时实际已启用。.117 已补齐“通过蓝牙升级”。修订镜像再次通过构建、LVGL 与精确镜像压缩/原生解压验证，证据为 `117-cleanup-*.log` 和 `117-cleanup-vectors/`。原始 .117 候选归档保留。

证据均位于 `artifacts/development/tab5-ble-ota/`：

- `all-transports-build.log`、`all-transports-firmware-final.log`：Windows Release 与 ESP-IDF 构建通过。`all-transports-status.json`：直接 Release DLL 状态读取通过；该检查不是实际用户配置验收。
- `117-compression.log`、`117-vectors/`：生产认证 RPC 完整读取 USB/BLE 镜像并逐字节核验；Wi-Fi 新旧协商、原文回退、重放/HMAC/范围/offer 错误测试通过。
- `117-zlib-native.log`：152 向量，160 正常场景、1,614 异常拒绝；生产解压边界与精确字节通过。
- `117-stream-native.log`：生产 Wi-Fi 分块解析器读取 .NET 生成的精确镜像；1/32/1024/32768 字节碎片、原文块、错误头、截断、尾随、损坏与内存不足通过。
- `117-ota-task.log`：生产 OTA 任务在模拟 USB、Wi-Fi 原文、BLE、Wi-Fi 压缩下执行大小镜像及 20 类正常/故障情况；验证未通过不切换启动分区。
- `117-routing.log`：自动优先级、固定三通道与不可用路径拒绝通过。`all-transports-rpc.log`：RPC 完整回归通过。
- `117-preview.log`、`117-ota-*.png`、`117-quota-reset-countdown.png`：LVGL 界面与实际 sidecar 首屏/滚动预览通过。额度测试 fixture 同步渲染全部页面后再做缓存幂等检查，避免修改合成数据却未更新总览造成误报。

原生 PC 测试使用主机 zlib 适配 ESP ROM 接口，不能代替 P4 ROM、射频与 Flash 实测。开发构建仍有已有的未使用函数提示和分区余量提示，不以此宣称新增硬件故障。

## 已有硬件证据和下一步

`.116` 已通过 Wi-Fi 安装并核验精确镜像、分区与启动健康。完整 BLE RAM 预检 140 段、6,867,152 解码字节，SHA-256 一致，耗时 61,275 ms，含加密回复 3,615,162 字节；Wi-Fi 恢复确认、USB 控制无中断。按既定余量预估 99 秒，不是完整 OTA 实测。

下载代码由当前运行固件执行：`.116 → .117` 可直接验收 BLE 压缩；Wi-Fi/USB 压缩接收须先安装 `.117`，之后升级下一份真实候选才可测量，不能重复安装同版本或把内部测试当成实机结果。

用户确认蓝牙升级成功。USB 启动核验通过：设备 `e8f60ae2ec56` 运行 `.117`，ELF 与上述精确镜像一致，启动分区 `ota_1`、状态 `VALID`，连续启动心跳通过。保留记录为 `previousStage=9`、`previousOffset=previousTotal=6869760`、`previousError=0`；桥接升级前运行态记录完整镜像经 BLE 提供。

实际 OTA 记录 `transport=2`（BLE）、`installMs=114185`：从安装任务开始到镜像校验完成约 **114.2 秒**，包含准备 7.836 秒、Flash 写入 9.322 秒、等待接收数据 95.743 秒及校验 0.900 秒等阶段；并行阶段不可简单相加。此时间不含完成提示停留及重启，不等同于点击到桌面恢复的端到端计时，也不是先前 61.275 秒 RAM 预检。证据：`117-cleanup-after-install.json`、`117-boot-verified.json`。

随后清理桥接普通界面的短时“蓝牙升级预检”和“蓝牙窗口对照”，保留完整固件预检、传输测速、启动核验和语音排障。Release 构建零警告/零错误，普通及最小窗口布局检查通过，运行界面核对通过；证据：`bridge-ui-cleanup-build.log`、`bridge-ui-cleanup-layout.log`、`bridge-ui-cleanup-preview/`、`117-bridge-cleanup-closeout.json`。开发内部诊断仍保留。

配套桥接已更新并通过正常桌面配置启动，原配对记录匹配，USB/BLE/Wi-Fi 连接恢复，没有重新配对。正常退出旧桥接后运行 `--restore-cycle-default`，核对自动轮播、原六页顺序及 15 秒间隔保留。设备连接模式恢复自动的人工确认仍单独记录。Wi-Fi/USB 压缩升级及语音回归仍待真机验收。

## English

The local .117 candidate adds negotiated compression to all three OTA transports and exposes the BLE upgrade entry. The 6,869,760-byte image becomes a 3,600,512-byte USB/BLE payload or 3,602,752-byte framed Wi-Fi stream; encrypted RPC replies total 3,616,444 bytes. Builds, authenticated full-image tests, native framing/decoder faults, OTA failure paths, routing and actual notes/UI previews pass. The user completed the BLE installation; USB verification confirms the exact ELF, ota_1 VALID state, increasing boot heartbeats and zero retained OTA error. Recorded installation through verification took 114.185 seconds, excluding the completion delay and reboot; the prior 61.275-second RAM preflight is a different measurement. Wi-Fi/USB compressed reception requires a subsequent genuine update for hardware acceptance. The bridge's superseded short probe and queue comparison entries were removed; Release build, layout checks and the deployed UI pass. Existing pairing and automatic display cycling were preserved. Device connection-mode restoration and voice regression remain separately tracked.
