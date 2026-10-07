# TAB5 蓝牙固件分块压缩：内部验证

2026-10-05 更新：.116 已通过 Wi-Fi 安装并核对精确镜像启动；完整 BLE RAM 预检接收 6,867,152 字节并通过 SHA-256，耗时 61,275 ms，含加密回复 3,615,162 字节，140 段。Wi-Fi 隔离已释放、恢复确认，USB 控制未中断。按既定余量预估 99 秒，但尚未执行完整 BLE Flash 升级。证据：`artifacts/development/tab5-ble-ota/hardware-116/firmware-probe-second.json`。

.117 本地候选扩展 Wi-Fi/USB 压缩并补齐蓝牙入口，详见协议与组件更新日志。下面保留 .116 及此前候选形成时的内部验证记录，其中“待部署”只表示当时状态。

Hardware follow-up: .116 is installed and its exact boot image verified. Full BLE RAM preflight receives and hashes all 6,867,152 decoded bytes in 61.275 seconds (3,615,162 encrypted reply bytes, 140 ranges), with confirmed Wi-Fi restoration and no USB control interruption. The 99-second estimate is not measured Flash installation. The .117 candidate adds Wi-Fi/USB compression and the explicit BLE entry; the sections below preserve earlier evidence and its historical acceptance state.

## 当前交付：0.2.116-ui

- 固定候选：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，6,867,152 字节，SHA-256 `cd1df494e4806428e5b8f9f3a35aad45e97150e6e585bb5a7ff9d6e7e187d038`。历史归档：`versions/0.2.116-ui/cd1df494e480-af8ab2c2`。旧 .115 历史包保留。
- 精确 .116 镜像经过生产加密 RPC 与解压逐字节核验：140 段，压缩载荷 3,599,230 字节，含元数据和加密封装的回复 3,615,162 字节，载荷减少 47.59%。152 个向量；原生边界测试通过 160 个正常场景并拒绝 1,614 个异常。
- 新增完整固件 RAM 预检，读取设备认证状态里的实际 offer，调用生产范围读取/解压并做流式 SHA-256，无 Flash 句柄。主机执行设备预检代码，使用系统 BCrypt 核验真实镜像 SHA-256；完整路径、末段损坏/取消/会话变化、超时、能力缺失、offer 缺失及 Wi-Fi 恢复失败均通过故障测试。
- `ota_probe` 复用固件范围认证与编码，限制 BLE，不获取 OTA 90 秒租期。完整预检期间暂停背景资源传输；退出释放。固定 BLE 与 USB 控制，110 秒传输预算、125 秒 Wi-Fi 恢复租期、160 秒桥接控制期限；分段内最多三次 10 秒等待可能越过传输预算，失败仍不会通过。
- 桥接要求所选镜像的字节数与 SHA-256 一致，再按实测接收时间 * 1.2 + 25 秒估算，继续以 120 秒为门槛。跟踪窗口覆盖完整预检，最多保存 160 段详细记录，超过上限仍累计总量。
- Windows Release 构建及直接 DLL 状态检查、RPC/BLE/语音调度/升级回归、原生范围/OTA/竞态/Wi-Fi 恢复测试、ESP-IDF 构建均通过。实际 sidecar 在 LVGL 中的首屏与滚动预览已检查。
- 证据目录：`artifacts/development/tab5-ble-ota/firmware-preflight/`；配套桥接隔离输出：`artifacts/development/tab5-ble-ota/bridge-compressed-ranges/`。主机测试与模拟不测射频、不执行 P4 ROM，也不代表实机通过。

下一步先通过 USB 或 Wi-Fi 安装 .116 和配套桥接，再保留 USB 控制、仅蓝牙模式执行一次完整固件预检；检查实际解压、完整性、耗时、Wi-Fi 恢复和语音。可以用已安装的 .116 镜像做 RAM 预检。正常升级拒绝同版本重复安装，后续完整 BLE OTA 必须使用下一份真实升级候选，不绕过保护。真机阶段结束恢复原自动轮播设置。

Current delivery: locally packaged .116 plus the isolated bridge, not deployed. The exact candidate payload is 47.59% smaller and all 140 encrypted ranges roundtrip. Full-image RAM preflight uses the production reader/decoder and streaming SHA-256; host fault tests reject last-range corruption/cancellation/session changes, deadlines and unconfirmed Wi-Fi restoration. Builds, regressions and actual release-notes previews pass. Hardware decoding, BLE timing, voice and full OTA remain pending. Install .116 over USB/Wi-Fi first; RAM preflight accepts the selected installed image while ordinary OTA keeps its same-version guard.

## 定位与选择

- 复用两轮真实窗口对照证据，保留窗口 64 / 原生并发 7。复测中块间空隙 233 ms、设备接收处理 37.176 ms，相比总计 12,982 ms 较小；继续微调这些部分缺少足够收益依据。
- ACK 等待 8,082 ms 包含无线排空，与其他计时重叠，不能全部算作可删除的协议开销。日常 7.5 ms 与旧大传输 15 ms 记录不是同条件性能对照，不据此修改射频默认值。
- 对真实固件同时检查 16/48/64 KiB 分块与压缩级别。保留既有 48 KiB 范围、使用 zlib，避免扩大邮箱或协议缓冲区。实际生产 .NET 编码结果以以下完整链路测量为准，而非 Python 算法试算。

## 实现

已认证状态新增 `rpcOtaZlib=1`。只有支持该能力的 TAB5 在 BLE 批量固件范围请求中发送 `acceptEncoding:"zlib"`。每块为独立 zlib 流，压缩发生在 HMAC/AES-GCM 封装之前，支持原有按原始偏移重试；解压后仍走原分区写入、整包 SHA-256、镜像身份与启动核验。

压缩至少节省 96 字节才启用，否则原文回退。USB、Wi-Fi、旧桥接、旧设备保持既有路径。接收端拒绝未协商压缩、错误偏移、未知编码、错误/非整数原始长度、解压超限、截断、尾随数据和 zlib 校验失败，失败块不会返回给 Flash 写入任务。

沿用设备已有 ESP32-P4 ROM miniz。每个范围额外最多 48 KiB 输入缓冲及约 11 KiB 解码工作区均在 PSRAM；不把解码工作区放入 8 KiB OTA 接收任务栈。内存不足失败退出，原有首块接收后才擦除备用分区的顺序保持。

## 已通过的内部验证

固定输入为 .115 固件，6,863,824 字节，SHA-256 `f7206f75cf562c83328ec0a70d67bcb190fbe037e6253eb64b3d7b23494a3d5a`。

| 结果 | 数值 |
| --- | ---: |
| 原始镜像 | 6,863,824 B |
| 生产代码压缩载荷 | 3,598,729 B |
| 含 RPC 元数据和加密封装的回复 | 3,614,661 B |
| 载荷减少 | 47.57% |
| 镜像范围 | 140 个，全量还原一致 |
| 含边界和随机输入的测试向量 | 152 个 |
| 原生 C 正常验证 / 异常拒绝 | 160 / 1,614 次 |

- 使用真实 `Tab5Service.RpcAsync`，完整镜像经过配对测试密钥认证、加密请求、生产范围服务、加密回复、解密解压与逐字节核验；覆盖错误 HMAC、nonce 重放、过期 offer、错误范围、禁止 OTA、USB 与不支持压缩的回退。
- 编译并调用设备实际 `ota_range_decode` 和 `rpc_ota_read`，核验返回的是原始字节数而非压缩长度；覆盖错误元数据、尾随/截断/坏校验、PSRAM 分配失败及保护区无越界。
- 原有 BLE 窗口/通知/取消/最终确认、BLE 传输、RPC、升级校验回归通过；原生 OTA 的 USB/Wi-Fi/BLE 故障矩阵、重连隔离和完成竞态测试通过。
- Windows Release 独立输出构建通过；TAB5 ESP-IDF 固件构建通过。标准 `dotnet run -- --status-once` 遇到本机 GUI 控制台句柄错误，直接执行候选 Release DLL 的 `--status-once` 成功，未将前者记为通过。

内部入口：

```powershell
dotnet <candidate>/AIBotBridge.dll --self-test-tab5-ota-compression <image.bin> <vectors-directory>
ota-zlib-test.exe <vectors-directory>
ota-range-test.exe
```

主机 C 测试通过 `TAB5_TEST_ZLIB` 指定已安装的 zlib DLL（默认查找 Git 的 `ucrt64/bin/zlib1.dll`），验证线上格式和生产解码边界；固件实际链接 ROM `tinfl_decompress`。主机测试**不执行 P4 ROM、不测设备解码速度或 PSRAM 并发时延**。Windows 侧完整链路约 1.1 秒包含本地文件输出，仅为测试运行时间，不是蓝牙性能结果。

初轮 .115 输入的原始向量和摘要在本地 `artifacts/development/tab5-ble-ota/compressed-ranges/vectors/`；当时尚未打包新候选。后续 .116 交付与精确镜像验证见本文开头。

## 留待真机阶段

后续需一次性核验设备 ROM 解码、实际内存余量、真实固件在 BLE 上的压缩后传输，以及完整写入/校验/重启/精确镜像启动。现有随机模式 RAM 预检仍测原始链路，不能把压缩率直接折算成通过；120 秒门槛不降低，不能宣布已达标。照片本身为 JPEG，此次固件压缩不代表照片三秒目标已通过。语音调度不变，真机回归仍须覆盖语音及断线恢复。

## English: initial validation before the .116 candidate

Internal-only implementation and validation on 2026-10-05; no deployment, device test, reflash or latest-package replacement. Withdraw the undeployed connection-preference comparison UI. Existing measurements place inter-RPC gaps and receive processing well below transfer time; retain window64/native7 rather than attributing overlapping ACK waits entirely to removable overhead.

Authenticated `rpcOtaZlib=1` allows BLE bulk OTA requests to opt into independent zlib ranges with `acceptEncoding:"zlib"`. Compress before authenticated encryption, only when savings exceed 96 bytes; retain raw ranges, legacy/USB/Wi-Fi paths, original offsets/retries and full-image validation. Device bounds and checksum checks reject malformed, oversized, truncated, trailing or unnegotiated compressed data. A bounded input buffer and ROM decoder workspace live in PSRAM, not the receiver stack.

The exact .115 image above shrinks from 6,863,824 to 3,598,729 payload bytes (47.57% reduction); authenticated replies total 3,614,661 bytes. All 140 image ranges roundtrip exactly through production encrypted RPC. Across 152 vectors, native production checks pass 160 valid cases and reject 1,614 invalid cases; range-length/retry/fallback integration and existing BLE/RPC/OTA fault suites pass. Windows Release and ESP-IDF builds pass. `dotnet run --status-once` encounters the GUI console-handle issue; direct candidate Release DLL status succeeds. The C host adapter uses installed zlib, not P4 ROM; device decode speed, PSRAM behavior and full BLE OTA remain unverified. The current raw RAM preflight is not compressed-image acceptance. Keep the 120-second target and photo/voice/device acceptance boundaries. No new installable version or package is prepared.
