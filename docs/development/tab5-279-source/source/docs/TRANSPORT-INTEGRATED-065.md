# 0.2.65-ui 三通道整合候选

**已撤回**：真机栈保护异常并回滚至 .064。修复候选与证据见 [TRANSPORT-STACK-066.md](TRANSPORT-STACK-066.md)。下文为原协议设计，不代表 .065 可安装。

2026-09-30：配套 Windows 桥接与固件已完成本地构建/回归，尚未部署。0.2.64 相机启动和仅蓝牙照片添加由用户确认成功，128759 字节耗时 71417 ms；速度未通过。不能用下述模拟耗时声称达到硬件上限。

## 路由与兼容

日常自动模式仍为 USB > Wi-Fi > BLE。照片、OTA 和语音的自动选择优先 Wi-Fi > USB > BLE；固定模式只走所选通道，一次操作锁定通道，失败不跨通道重放。旧固件/旧桥接按协商能力回退，ESP8266 协议不变。

## 协议增量

- BLE 身份读数新增 `telemetryVersion=3,mailboxWindow=8`。`link` 数组依次是连接间隔微秒、从机延迟、TX PHY、RX PHY、TX/RX 数据长度、bulk 标志、连接参数/PHY/长度请求返回码。实际协商值与请求结果分开；未知为 0。bulk 请求 7.5–15 ms、零延迟、2M PHY 和 251 字节数据长度，5 秒空闲后申请恢复连接时的间隔。中央可拒绝，不保证请求生效。
- RPC/语音通知额度最大 8，旧端默认 4，仍受实际 MTU 和队列容量约束。支持时 grant 用无响应写，后续通知核对 ID/offset；回包每 4 包及最后一包保留有响应写屏障。丢片/超时失败，不用终包掩盖缺口。
- `tab5_delta` 只引用桥接已获认证 ACK 的完整等价状态：`deviceId,session,sequence,baseSequence,changes`，每项含字符串数组 `path` 与 `value` 或 `remove:true`。最多 256 项、16 层；数组整体替换，JSON null 不等于删除。接收端克隆并验证后原子替换，基线、会话、新鲜度不符拒绝；重复帧不续心跳。重连先全量，OTA offer 改变强制全量；任务状态的 4 秒发送预算和现有过期规则不放宽。
- USB hello/ACK 宣告 `rpcUsbBinary=1`。`@AIBOT` JSON 控制帧新增 `tab5_rpc_binary`，字段 `version=1,deviceId,tag,count`。count=0 读取邮箱，否则随后精确 count 个原始字节；上限 16393。响应为 16 字节：ASCII `AIB2`、LE uint32 tag、payload 长度、ok(0/1)，然后 payload。整次交易持有 USB 门锁。接收超时/内存失败后等待 CDC 断开重连恢复帧界限，不以二进制中的换行猜测边界。旧 JSON/base64 邮箱仍可用，460800 配置不变。
- Wi-Fi 状态 `data.imageBinary=1` 允许 `/tab5/v1/codex/image` 的加密明文为 `T5I1` + LE uint32 metadataLength + UTF-8 JSON metadata + 原图字节。metadata 为 2–4096 字节，图像 16 字节至 2 MiB；任务、session、issuedAt、nonce、防重放和图片所有权检查与旧路径相同。旧端使用原 base64。设备在加密包内原位加密，释放上传副本，保留用户照片用于重试。
- `/tab5/v1/rpc` 复用有界 HTTP 连接，每个请求独立认证/加密，完整响应后再发下个请求；错误关闭连接。照片用上述独立大图路径，Wi-Fi OTA 保持原 TCP 双 64 KiB 管线。
- USB RPC `kind=voice` 复用原语音状态机和加密认证，保留草稿审阅/手动发送。BLE OTA 使用受认证 range RPC，每次最多 8192 字节，复用现有哈希、镜像、分区和启动验证流程；仅代码/故障测试通过，真实 BLE 升级尚未验收。

## 自动测速

Windows「TAB5 连接 → 传输测速」。USB `tab5_benchmark` 控制命令包含 version、deviceId、action(start/status/cancel)，只负责控制/取结果；有效数据仍绑定用户选择的通道。活动照片/语音/OTA 期间拒绝开始。自动模式测新鲜可用通道，固定模式仅测指定通道；开始后改变模式/session 会中止对应传输。

每通道上下行各 3 次，每次 64 KiB、每片 8 KiB。设备生成确定性字节，经原认证 RPC `kind=benchmark` 验证 SHA-256、offset 和下载内容；不创建消息/附件、不写闪存。单通道 5 分钟预算、桥接 16 分钟预算，取消在当前分段完成/原 RPC 超时后结束，不能宣称立即停止。原始结果为状态与 `channel,direction,repeat,bytes,ms,error`；界面显示有效 KiB/s。complete 只表示记录中的可用通道完成，不代表三通道齐全。

现阶段测速结果不包含内存峰值，也不等于语音实时性或真实刷机通过。需结合既有 RPC 排队/收发诊断、BLE link 读数、OTA read/write timing 和设备内存诊断评估。采用同内容三次结果的中位数和范围，保留失败次数，不拿模拟测试的耗时做硬件速度对比。

## 本地证据与剩余验收

AI-bot `artifacts/acceptance-20260930/`：`integrated-065-bridge-tests.log`、`integrated-065-native-tests-final.log`、`integrated-065-ui-v5.log`、`integrated-065-elf-memory.json`、`integrated-065-elf-flash-safe.json`、`integrated-065-image-info.log`。覆盖真实 loopback 加密图片字节、长连接 RPC、USB 语音、BLE 窗口故障、生产接收器增量原子性、USB 帧边界、原生 HTTP 截断/内存失败、三通道 OTA 故障矩阵、相机/JPEG/SDIO 内存及扫描中断链。

剩余：统一部署后的真实通道速率、实际 BLE 参数、固定通道隔离、断线恢复、照片完整添加与发送、语音草稿、真实 OTA 启动与哈希/分区验证、长期稳定性。部署结束恢复用户原自动轮播及页面顺序/间隔。当前候选不得标为「硬件最优已通过」。
