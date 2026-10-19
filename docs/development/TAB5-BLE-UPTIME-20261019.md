# 纯 BLE 启动报告的整数毫秒修复

当前发布结果：用户已明确授权提交和公开发布；Windows 0.6.10 与 TAB5 .234 的带注释标签、正式附件及服务器摘要均已确认，下载中心同步更新。已验收 Windows 安装器和 c608 原字节保留，后续 CI 仅修复滚动可达性测试，远端全平台 CI 已通过；旧失败和未测系统分支保留。媒体 417 文件独立重建一致，主观试听未验，公开播放器交互由单独记录说明。后文早期“未授权”“待完成”均为保留的阶段记录。

Published outcome: explicit user authorization covered scoped commits, normal pushes, annotated component tags and public release. Formal asset sets and server digests match; downloads are current. Accepted Windows bytes remain unchanged. A test-only scroll-reachability repair passed remote CI; old failures and untested system branches remain recorded. The 417-file media project independently reproduces identical HTML. Subjective listening and separately tracked public-player interaction retain their stated limits. Earlier pending/unapproved states below are historical.


已取得真实失败字段、修复并通过 .234 纯 BLE 实机核验。用户批准精确镜像后完成 OTA，原版 Windows 0.6.10 / c608 在 USB 未连接、Wi-Fi 等待设备回报且蓝牙数据已确认的隔离条件下，只发出一次只读核验；9.34 秒后确认精确 ELF、`ota_1 VALID` 和三份实时样本，隔离违规为空。没有替换或重启 Windows、输入按键或重复已通过的功能。完成后已立即通知恢复电脑 WLAN，用户已确认恢复。尚未公开发布。

## 实际证据

经用户另行授权，临时诊断版只为既有错误补充数值，没有放宽健康、镜像或认证条件。普通桌面启动、`--status-once` 和保留性检查通过。用户断开 USB、关闭电脑 WLAN、保留蓝牙和网线；一次只读核验在 1.17 秒后失败，整个操作隔离有效，未执行输入、固件写入或自动重试。

实际认证报告对应的错误数值为：

| 字段 | 实际值 | 校验结果 |
| --- | --- | --- |
| `uptimeMs` | `562752.83` | 包含小数，`TryGetInt64` 拒绝 |
| `previous` | `-1` | 第一份样本 |
| `uiAgeMs` | `33` | 在 0–2000ms 范围内 |
| `flushErrors` | `0` | 正常 |
| `flushCount` | `629` | 已有刷新 |

原 `Number` 解析器把无法读取的整数记为 `-1`，所以第一份样本就触发 `uptime <= previous`。本次明确是运行时间的类型/格式错误，不能据此把旧 .231、.232 未保存的数值补写成已观察事实。.232 的心跳读取竞态是独立的真实缺陷，但没有解决此次拒绝。

证据保存在 `artifacts/development/release-closeout-20261019/hardware-boot-sample-diagnostic-233/`。用户随后立即恢复 WLAN，并运行恢复入口正常切回原桥接；恢复记录确认 c608、配对/PIN/展示设置保留及原文件未变。恢复步骤没有重复 `--status-once`，其 `statusOncePassed=false` 不表示恢复失败。

## 根因与修复

.231 的报告重发修复中增加了每次发送前刷新 `uptimeMs`，调用为：

```c
cJSON_SetNumberValue(..., esp_timer_get_time()/1000);
```

固定 SDK 的 cJSON 宏展开为 `cJSON_SetNumberHelper(object, (double)number)`，没有为参数加括号。该调用因此先把微秒时间转为 `double`，再除以 1000，产生小数毫秒。Wi-Fi 初始报告使用普通 `cJSON_AddNumberToObject` 函数，调用参数先完成整数除法，未受到此处宏展开影响。这是本项目 .231 调用方式引入的错误，不能归责为设备硬件异常或通过放宽整数校验处理。

修复先求整数值再传入宏：

```c
int64_t uptime_ms = esp_timer_get_time()/1000;
cJSON_SetNumberValue(..., uptime_ms);
```

仅 `boot_verify_worker.inc` 的上述时间赋值和解释注释变化；原有忙碌重发、时限、身份、三份递增实时样本、界面健康、精确镜像及 VALID 状态规则保持原值。Windows 代码和 c608 校验器不改。

## 新增验证

旧原生线程测试的模拟时钟只返回整毫秒乘以 1000，没有微秒尾数，因而漏检。新测试执行真实工作线程和固定 SDK 的真实 cJSON 宏/序列化，在 `562752830us` 下使旧源码生成 **`562752.83`**，与现场数值完全一致，正常退出 1 保留失败证据。

修复后覆盖尾数 830、1、83、500、999、0us，及通道瞬时占用后的重发和既有 Wi-Fi 路径，生成九份原始 JSON。独立 .NET 8 回放程序直接通过反射调用**实际 c608 DLL 的原校验函数**，不启动程序、不读取用户资料、不接触硬件：旧线包被原校验器拒绝，新九份整数且递增的线包全部通过。已有线程占用、超时、拒绝、并发及资源失败回归保留并通过。

`ble-uptime-integer-fix/` 保存旧源码、SDK 宏、失败/成功线包、原生日志以及实际 DLL 回放源代码与结果。此为本地跨语言数据路径证明；独立实机证据保存在 `hardware-boot-fix-234/ble-wireless-boot-verification.json`，支持命令返回 `ok=true`。此前 .233 启动电量仍保留“未观察”；用户随后直接确认 .234 本次升级重启后电量很快显示、插拔 USB 后也正常，新增电量验收据此通过，没有额外要求重启或重测旧项目。

最终候选 BIN SHA-256：`1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8`；ELF SHA-256：`9e6d0e4523381385b721a46389c87ec02dd6ea492225260771f38b3d7bbc29fb`。与 .233 的 223 项主目录输入相比仅上述报告时间文件变化。全部 962 份源码/验证文件、首刷私有擦除区域、许可证、包摘要和实际升级说明预览均已核对；完整原生界面通过。经针对这些字节的明确批准，标准准备脚本登记全局 latest 为 .234；现有选择器提供的 BIN/ELF 及 OTA 后的 installedVersion 均由真实 IPC 核对。首次重启后的 IPC 仍缓存 .233，原记录保留，随后新回包确认 .234，没有因此重复刷写。候选 VERIFIED.json 中的“未部署”是冻结时快照，由后续实机证据补充。Windows 仍使用原 c608 校验器。

English: The isolated .233 diagnostic captured uptimeMs=562752.83, previous=-1, uiAgeMs=33, flushErrors=0 and flushCount=629. The original c608 parser rejected fractional uptime. The .231 retry call passed an unparenthesized division to the pinned cJSON macro, which casts before dividing. The fix computes integer milliseconds first; all validation thresholds remain unchanged. Actual-worker tests reproduce the exact live baseline failure and cover microsecond tails and retries; the actual accepted c608 helper rejects the baseline and accepts all nine fixed wire samples. Firmware build, full native UI, 962 source entries and three licensed delivery archives passed local checks. After explicit approval of the exact .234 BIN/ELF, the user completed OTA, confirmed by fresh IPC. One isolated read-only BLE operation then passed after 9.34 seconds with the exact ELF, ota_1 VALID and three live samples, without isolation violations. Windows stayed on the unchanged original c608; no previously accepted physical test was repeated. WLAN restoration was immediately requested and confirmed by the user. The user also directly accepted quick battery display after this reboot and normal display across USB changes. The old .233 startup observation remains unobserved. Earlier .231/.232 missing numeric fields remain unknown, and the separate heartbeat race is not relabeled as this observed cause. Public release remains unapproved.
