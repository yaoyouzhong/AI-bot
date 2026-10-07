# TAB5 .146 图库无线同步候选 / Gallery transport candidate

2026-10-07。`.146` 已安装，实际启动镜像核验通过；用户确认 USB、Wi-Fi、BLE 下两类图库均能显示、转向及分页。BLE 首次加载仍慢，USB 标识恢复超过 10 秒的反馈仍待定位，不能把链路打通等同于体验验收完成。未公开发布。

## 问题与改动

用户拔掉 USB 后，书法仍保持横屏图，名画预览提示等待电脑同步。桥接在线、蓝牙状态 ACK 正常，但图库最后一条请求仍来自 USB。固件图库 Wi-Fi HTTP client 没有指定请求方法，ESP-IDF 默认 GET；桥接只路由 `POST /tab5/v1/rpc`。Wi-Fi 状态心跳正常时会优先于 BLE 被选中，因此蓝牙已连接不代表图库请求会经过 BLE。

公共 `http_exchange` 在打开连接前显式设置 POST，失败时关闭连接。沿用既有鉴权、数据完整性检查及 USB → Wi-Fi → BLE 的日常读取优先级；不改画作、方向映射和双缓冲显示。

## 本地证据

- 负向复现：加入默认 GET／桥接路由模型后，旧实现无法取得有效图片回复，测试失败；修复后同一测试通过。
- Windows 图库传输自检：三个通道各重组 12 张合成原生 JPEG，覆盖两类图库、两种排版、三页、48 KiB 与兼容 8 KiB 分块、SHA-256、JPEG 解码及过期图片哈希拒绝。共 720 次鉴权请求。Wi-Fi 经过真实本机 TCP/HTTP 路由；USB/BLE 使用生产 mailbox pump 和模拟 I/O，未连接物理设备。
- 原生 RPC：HTTP 默认方法、最大回复、碎片及失败关闭；USB 断开/恢复、优先级与固定通道约束；USB/BLE mailbox 重组、跨语言二进制数据校验通过。
- 原生显示：两类图库 24 次翻页、96 次方向检查，失败重试及两块输出缓冲约束通过。
- Windows Release 构建 0 警告、0 错误；ESP-IDF 构建通过。`--status-once` 在 Codex 及 Shell.Application 继承的启动环境中被既有资料目录保护拒绝；改由 Windows WMI 启动正常桌面 PowerShell 7 进程后，Release `--status-once` 在 19:49:11 返回成功（退出码 0）。未修改资料目录保护。

证据目录：`artifacts/development/gallery-transports-146/`。新增自检入口：

```powershell
dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release -- --self-test-tab5-gallery-transports
```

## 候选与待验收

- 固定升级路径：`artifacts/firmware/tab5/latest/aibot_tab5.bin`。
- 版本：`0.2.146-ui`；6,527,936 字节；OTA 槽位剩余 681,024 字节。
- SHA-256：`0b5143ac61641551a62730b063528b57859e22937c2e2cbf6d8c00e2b37d09ec`。
- ELF SHA-256：`4c9be480a99af4271dfdec7290311928b3b53f0358538ccb38b43ad347b55629`。
- 固件经准备脚本校验，旧候选归档保留，实际 sidecar 在原生更新页预览。

授权安装后须分别在仅 USB、仅 Wi-Fi、仅 BLE 下核对两类图片首次同步、后续分页、横竖屏；再检查自动模式拔插与回退。启动核验与用户可见结果分别记录，完成后恢复原自动循环及页面顺序/间隔。未完成这些步骤前不宣称三通道实机通过。

## 安装进展

- 用户已授权并在设备开始安装；Wi-Fi 分块压缩传输完成，镜像 6,527,936 字节，线上 4,070,993 字节，桥接传输耗时 12,188 毫秒。传输完成不等于启动核验通过。
- USB 拔出期间，桥接已收到两类图库的真实 Wi-Fi 图片请求：19:50:47 起名画、19:51:39 书法，均出现竖屏请求与状态 200。用户随后确认两类显示、横竖屏及翻页正常。
- 19:52:45 完成 USB 启动核验：三个递增运行时间样本、精确 ELF SHA-256、`ota_0`、`VALID` 均符合候选；显示错误和欠载均为 0。方向传感器就绪、错误 0、样本年龄 81 毫秒。设备保留的升级总安装耗时为 18,705 毫秒，Wi-Fi 通道编号为 1。
- 核验后正常重启桥接，Release `--restore-cycle-default` 成功，HTTP 状态确认自动轮播和原页面/顺序/15 秒间隔一致。配对文件匹配、未重新配对。
- 安装前保存的默认展示：自动模式、循环开启、15 秒间隔，页面依次为 `codex, domestic_deepseek, domestic_zhipu, weather, stocks, system`。
- 用户确认 USB 图库正常；19:54:56.277 Windows 已枚举 COM9，19:54:57.159 桥接确认连接，采样耗时 891 毫秒。Windows 记录复合设备到达时间为 19:54:55.967，串口子设备为 19:54:56.183。该记录未覆盖用户实际插线时刻及屏幕标识变绿时刻，不能否定用户报告的 10 秒以上等待。
- 用户确认仅蓝牙模式下两类图库及 Codex 均正常，随后恢复自动选择。真实 BLE 图库请求在 19:57:03–19:57:37 返回成功；竖版书法 250,678 字节约 6 秒、竖版名画 435,373 字节约 8 秒，仅为请求日志时间跨度，不是完整点击到显示计时。用户随后明确认为这段等待体验不好，要求横竖两版首次同步后本地复用，并同时优化首次传输。

## 同次 Codex 提示排查

用户同时反馈 Codex 打开结果有时未确认。现有 Wi-Fi 打开请求超时为 12 秒；历史 BLE 通用 RPC 曾记录电脑处理 19,563 毫秒。但该 BLE 记录没有业务种类、请求 ID 或选择瞬间 Wi-Fi 状态，不能认定它就是报错那次 Codex 请求，也不能据此确认 Wi-Fi 超时或优先级错误。选择代码在自动模式下优先 USB → Wi-Fi → BLE，Wi-Fi 关联正常且完整状态年龄小于 8 秒时不会选 BLE。最近成功打开记录为 19:37:22、46 毫秒；过去错误的具体原因仍未确定。

后续仅蓝牙复测：19:57:54 成功确认当前目标会话及前台，窗口操作耗时 235 毫秒；用户确认成功。旧偶发错误仍未复现，未将其标为已修复。

## English

Local .146 explicitly uses POST for authenticated HTTP RPC, fixing gallery clients that inherited GET. Daily read priority remains USB, Wi-Fi, BLE. Native regressions, 720 authenticated simulated/loopback requests and both builds passed. The authorized installation passed exact ELF/ota_0 VALID checks, three advancing uptime samples and display/sensor checks. Wi-Fi transfer took 12,188 ms; device installation took 18,705 ms. The original auto cycle, six-page order and 15-second interval were restored. The user confirmed both galleries' display, rotation and paging on all three links, and one BLE Codex open succeeded. Usability remains incomplete: BLE portrait requests took about 6/8 seconds for calligraphy/painting, and the user reports over 10 seconds before the USB screen indicator recovers. The measured 891 ms covers only Windows serial enumeration to bridge acknowledgment. Further caching and first-download performance work is tracked separately; the earlier intermittent Codex error remains unexplained.
