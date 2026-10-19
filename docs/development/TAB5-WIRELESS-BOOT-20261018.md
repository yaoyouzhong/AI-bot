# TAB5 无线启动核验协议候选

原始功能在 0.2.226-ui 候选中实现；现已随 0.2.229-ui 与配套桥接部署。当前准确 USB 启动核验通过；无线实机验收在 2026-10-19 准备阶段提速这一轮继续执行，结果见文末。代码接入与无线验收分开记录。

## 行为

原“核验启动”入口优先沿用 USB。没有已配对 USB 时，新固件可使用当前已配对 Wi-Fi 或 BLE 通道；一次核验固定一个通道，不自动混合两种通道的证据。旧固件明确提示升级或接 USB，不将已回报版本视为精确核验。

复用状态推送和现有加密 RPC，不增加系统服务、驱动、端口或第三方软件。无线启动核验与 Windows Wi-Fi 解锁是不同功能，不涉及 PIN 或键盘操作。

## 协议

桥接在已认证状态 `data.connectionHealth.bootVerifyCommand` 中附加：`version=1`、32 位小写十六进制 `requestId`、`transport`（`Wi-Fi` / `BLE`）及毫秒 `expiresAt`。每个采样独立随机请求号，有效 10 秒；设备仅处理新鲜状态、相符通道和有效到期时间，每个已接受请求只启动一次只读工作线程。

设备经现有 `POST /tab5/v1/rpc` 或 BLE RPC 发回 `kind=boot-verify`、`requestId`、当前会话，以及 `hello` 与 `diagnostic`。沿用配对身份、HMAC、AES-GCM、会话/时间和重放检查。桥接还检查当前请求号、配对设备、通道、有效期和所选镜像对象；消费后的报告不重复接受。

`hello` 与 USB 启动检查一致：版本、设备 ID、固件版本、运行毫秒、UI 更新年龄、刷新计数和刷新错误。`diagnostic` 与 USB 镜像检查一致：`tab5_ota_diagnostic`、版本、运行 ELF SHA-256、分区名/地址、OTA 状态和状态读取错误。分别确认 `ota_0/0x20000` 或 `ota_1/0x700000`，状态必须为 `VALID`。

三次独立采样，间隔至少 1200 毫秒，运行时间必须持续增加，UI 更新年龄不超过 2000 毫秒、无刷新错误；每次都检查精确指纹和有效分区。失败或取消清空请求并释放操作占用，不标记成功。无线暂不采集 OTA 历史性能诊断，明确标记 `timing=unavailable`。

## 原始候选验证范围（历史）

Windows Release 编译、现有升级回归、Wi-Fi/BLE 加密 RPC 生命周期模拟，以及设备实际命令接收代码的原生测试。模拟覆盖错误身份、通道、请求号、认证、重复消费、错误镜像、未确认分区、非递增运行时间和取消；设备测试覆盖过期/陈旧/非整数时间、通道限制和同请求去重。

原始 .226 候选源、日志和构建结果在 `artifacts/development/wireless-boot-verification-20261018/`。`--status-once` 仍受现有 AppProfileGuard 保护，未绕过；这是原始候选阶段的状态，当前部署与实际无线结果另列，旧候选与交付物继续保留。

## English

The original .226 candidate introduced exact boot verification over the existing authenticated Wi-Fi/BLE status and RPC channels. Three fresh challenges validate paired identity, advancing uptime, UI health, ELF hash and a valid running OTA partition. Each attempt stays on one transport; stale, consumed, mismatched or unauthenticated reports cannot establish success. No service, driver, new port, credential input or third-party component is introduced. USB remains supported; older firmware requires USB. The implementation is now deployed in .229 with the matching bridge; current wireless hardware results are recorded separately below.

## 2026-10-19 无线实机结果

用户拔掉 USB、保持蓝牙与 Wi-Fi 开启后，现场确认 USB 未连接、蓝牙已鉴权。通过现有“核验启动”控制入口实际执行，返回成功；选定通道为 Wi-Fi，3 次新鲜挑战全部通过。准确 ELF SHA-256 为 `f5134ef1146793f770064225c9f71535b7f6b9dd37518d1d29f2290e49331f29`，固件 0.2.229-ui，运行分区 ota_0，状态 VALID。无线性能历史仍标记 `timing=unavailable`，没有用断开前的 USB 健康记录代替本次无线证据。

证据：`artifacts/development/wifi-oneclick-20261018/usb-ble-preparation-20261019/wireless-before-verify.json`、`wireless-boot-verify.json`、`wireless-boot-verified.json`。本次证明 Wi-Fi 无线启动核验主链路；仅 BLE 隔离联网的启动核验仍未实机验收，不能以蓝牙已连接代替。

English hardware result: With USB physically unplugged, exact boot verification succeeded over Wi-Fi with three fresh challenges. Firmware .229, the expected ELF hash and ota_0 VALID were verified. This is live wireless acceptance, independent of stale USB health telemetry. BLE-only isolated boot verification has not been hardware-tested; wireless OTA timing history remains unavailable.

同轮蓝牙短 RPC 调度补修部署后，再次通过完全相同的无线核验。最终桥接 DLL SHA-256 为 `fbf812720b9b243afe070a9cb2ac044040e601993b040b6822bd0ccd146f8ce2`，固件指纹和分区不变，仍为 Wi-Fi、`liveSamples=3`。补充证据为同目录 `bridge-scheduling-wireless-boot-verify.json`、`bridge-scheduling-wireless-boot-verified.json`。

English final bridge check: After the BLE scheduling correction, Wi-Fi boot verification passed again on the final deployed bridge (DLL SHA-256 `fbf812720b9b243afe070a9cb2ac044040e601993b040b6822bd0ccd146f8ce2`), with the same exact .229 ELF, ota_0 VALID and three fresh samples.
