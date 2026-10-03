# 0.2.67 三通道性能候选（2026-09-30）

## 状态与完成标准

当前真机已运行 0.2.67-ui，配套原目录桥接使用 USB 原生读取修复版。21:01 经正常桥接界面 USB 启动核验通过；三通道双向各三次 256 KiB 测试全部通过，真实蓝牙照片也已收齐。不能把短时测速或照片明显变快称为达到硬件极限。

.066 已通过 USB 启动核验（设备、版本、ELF 指纹、分区及 VALID）、三通道双向各三次 64 KiB RAM 测速。中位有效吞吐，设备→电脑 / 电脑→设备：USB 212.6 / 77.7 KiB/s，Wi-Fi 61.1 / 55.5 KiB/s，BLE 21.3 / 16.4 KiB/s。仅蓝牙真实照片 139542 字节，设备端 8417 ms、桥接收齐 7141 ms，用户确认明显变快。不同照片不能作为同文件对照实验。

下一次统一真机验收依次检查：新版本启动与回滚状态；三通道双向持续吞吐及瓶颈；固定模式下照片、语音和 OTA 的真实主链路；断线后的数据保留及恢复；原展示设置恢复。照片发送、语音文字发送仍由用户确认；不自动发送测试消息。

## 本次实现

- USB：Windows 二进制读取使用原生阻塞读取等待数据，以 50 ms 最大切片限定取消延迟，保留绝对截止时间；不修改系统计时器分辨率。460800 配置不变；TAB5 使用原生 CDC，这个串口参数不是其 USB 物理吞吐上限。
- Wi-Fi：每个 TCP 连接使用有界 4096 字节读缓冲，保留 HTTP 头后预读的正文与下一请求，移除逐字节 socket await。照片、语音、OTA 和 RAM 测速期间按引用计数临时设 WIFI_PS_NONE，最后一个任务结束恢复进入前模式；失败恢复由空闲循环重试，不写入 NVS。设备返回 `wifiPower=users,changed,lastError`，忙时返回 `busy`，诊断不等待 ESP-Hosted 锁。
- BLE：成功完成一个邮箱请求后立即再次接收，空闲时才退避；回复每个协商窗口和末片做 ATT 确认，窗口 1–8，旧设备默认 4。完整偏移、长度、请求 ID 检查保留；不绕过新鲜度、认证或最后确认。
- 公共 RPC：照片片段及 OTA/测速数据可协商二进制载荷，减少 Base64 的线上字节；业务对象仍使用 Base64，尚非全链零拷贝。语音保持既有 PCM/ADPCM 格式，受益于公共 I/O、邮箱调度与 Wi-Fi 临时性能策略。
- 路由规则保持：固定模式只走所选通道，自动模式按已认证可用通道选择；一次操作不跨通道重发。

## 二进制协议

认证状态 `data.rpcBinary=1` 表示桥接支持。固件未见能力时保持旧 JSON。认证请求 `binaryReply=true` 允许桥接返回二进制；无数据回复仍是 JSON。

加密前格式：`T5R1[4] | metadataLength:u32le | JSON metadata | raw data`。沿用现有 HMAC、AES-GCM、nonce、会话、时间及业务范围检查。metadata 长 2–4096，请求原始数据 1–8192 字节，回复原始数据 1–16384 字节；原总报文上限继续适用。请求 JSON 的根 `data` 或回复 `body.data` 被移出，接收端重建；metadata 已有同名数据则拒绝。旧固件与新桥接仍使用原格式。

## 测量口径

新 RAM 测试每方向每轮 256 KiB，8192 字节片段，每通道双向各三轮。逐片核对偏移、哈希和全部字节。数据仅在内存往返，不写闪存、不发送用户内容。与旧 64 KiB 结果需注明样本长度差异，不能假装同条件直接倍数比较。

每行 15 字段：`channel,direction,repeat,bytes,ms,error,calls,encodeMs,exchangeMs,decodeMs,wireTx,wireRx,minInternal,minPsram,minLargest`。计时由设备统一时钟测量；encode/decode 是公共 RPC 内部阶段，不包含调用方生成测试载荷与最后业务校验。exchange 包含等待桥接、队列、链路及对端处理，不等同无线净空口时间。wire 只计加密 RPC 数据，不含 USB/HTTP/GATT 及无线底层包头。内存是阶段采样最低值，不是连续峰值；最大连续块也只报告采样低值。

“充分利用硬件”验收需要同一设备、文件和场景的持续测量，在零损坏、无重复执行、稳定内存和可用界面前提下确认剩余瓶颈。USB 端点、Wi-Fi 的 ESP-Hosted/SDIO 与共存调度、Windows BLE 中央端控制都可能限制实际速率；不把芯片标称速率当作应用吞吐保证。[M5Stack 板级资料](https://docs.m5stack.com/en/core/Tab5)、[Espressif Wi-Fi 功耗说明](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-guides/wifi-driver/wifi-performance-and-power-save.html)。

## 本地验证

- Windows Release 零警告/错误；同一候选 DLL 的 `--status-once` 输出有效 JSON。项目 `dotnet run -c Release --no-build -- --status-once` 仍因宿主 `Console.OutputEncoding` 无效句柄失败，日志 `performance-067-status-required.log`；该命令不计通过。
- 真 loopback TCP：连续 RPC/语音请求、图片新旧格式、认证、防重放、精确图片字节；合成音频验证 USB/BLE/Wi-Fi 语音生命周期。未冒充真实麦克风/识别结果。
- 生产代码故障矩阵：三通道 OTA 读写/分配/哈希/断线/启动失败，固定模式隔离；USB 完成/断线竞态、有界等待；BLE MTU 23/247/517、窗口、缺片/乱序/取消；Wi-Fi 临时策略嵌套与恢复失败重试。
- C# 生成的 1/8192/16384 字节二进制载荷，由固件实际 C 解码核对所有字节（含零字节）；请求编码、畸形长度和旧 JSON 兼容通过。
- Clang 原生构建与 LVGL 全页预览通过。默认 MSVC 预览因既有 C 原子支持和大对象编译选项不全失败，保留原日志，未弱化测试。
- 最终固件 P4 镜像校验通过；BLE 接收回调局部栈帧仍为 224 字节；LVGL 分配落到 PSRAM、显示扫描 ISR/辅助调用落到 IRAM。静态检查不等于运行峰值或启动通过。

证据目录：`artifacts/acceptance-20260930/performance-067-*`；冻结包及哈希清单：`performance-067-package/manifest.json`。原目录桥接已在用户正常退出后备份并替换，部署回执为 `performance-067-deployment.json`；配对和展示设置未由部署操作修改。待用户正常启动并安装固件后继续实机验证，不记为全部完成。

## 首次桥接启动失败与修复

20:47 首次候选桥接因 `SerialStream.EventLoopRunner.CallReceiveEvents` 空引用崩溃；.NET Windows 串口实现对事件检查与调用之间存在解绑竞争。候选按每段订阅/解绑暴露此缺陷，固定监听在 Dispose 时仍有相同竞争，因此最终取消 DataReceived 订阅。原生读取放在一个后台工作项内，50 ms 最长读等待保证取消有界；调用完成前继续持有 USB 门，不遗留读取器消费后续事务。原读取超时结束后恢复。

在真实 COM9、同一 .066 设备上，300 次空闲二进制读取、5 次连接重开和取消检查通过，未写配置、配对或固件。此检查验证实际驱动路径，仍不能代替正常桥接和新固件全链路验收。修复 DLL `7c0223b95984b2a6e3f71098ed7c7e379cb2fd5233ebbbabecd506f4817f6cb2`，固件哈希保持不变；原失败桥接已撤回。证据：`performance-067-usb-event-crash.json`、`performance-067-usb-live-read.log`、`performance-067-usb-fix-deployment.json`。

## 0.2.67 真机结果

正常桌面桥接与配对记录一致；USB 核验通过。18/18 轮、合计 4718592 字节精确校验通过，每轮 32 个 RPC。设备→电脑 / 电脑→设备中位速率：USB 187.7 / 140.6 KiB/s、Wi-Fi 47.2 / 53.3 KiB/s、BLE 20.5 / 19.0 KiB/s。Wi-Fi 第二次上传为 14727 ms，另两次 4986/5426 ms，存在等待长尾，不能记为性能收敛。

内存阶段采样最低：内部 185747 字节、PSRAM 17788884 字节、最大连续内部块 77824 字节。Wi-Fi 功耗诊断在测试中为 1,1,0，完成后为 0,0,0，观察到临时模式恢复。桥接近期 USB/BLE 中断仍是 20:59 升级重启记录，后续测量无新增中断。原自动轮播六页/顺序/15 秒一致。

21:05 新照片仅蓝牙上传 94782 字节成功，设备端 4560 ms，桥接收齐 4204 ms；USB/Wi-Fi 物理连接同时可用，实际图片路由 BLE。不同照片大小及采样长度、网络环境不同，不能用它与旧结果直接宣称严格提升倍数。语音、USB/BLE 真实 OTA、断线负例与长期运行仍待验。

证据：`performance-067-boot-verified.json`、`performance-067-benchmark-summary.json`、`performance-067-acceptance-metrics.json`、`performance-067-phases.json`、`performance-067-photo-success.json`。下一轮性能工作优先拆解无线等待长尾、USB 编码开销与业务排队；硬件 AES/GCM/SHA 已启用，不能再把打开这些选项当作新增优化。
