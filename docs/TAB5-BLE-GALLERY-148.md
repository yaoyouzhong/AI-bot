# TAB5 BLE 图库 .148–.149 修复与验收 / BLE gallery fixes and acceptance

2026-10-07。当前正式发布为 [.149](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.149-ui)，配套桥接为 [0.6.1](https://github.com/yaoyouzhong/AI-bot/releases/tag/bridge-v0.6.1)；下文保留候选阶段及两轮实测记录。

`.148` 已获授权本地安装，精确启动核验通过，未发布；实测发现连续横竖图的暂停衔接遗漏，`.149` 已安装并核验，两轮竖屏下载改善；用户确认重连发生在升级/桥接重启附近，22:57 的再次启动也由用户主动测试触发。本页不将历史对照或模拟测试记为当前真机提速。

## .148 安装与发现的衔接问题

- Wi-Fi OTA 实际传输 4,071,889 字节、电脑发送 15,141 ms；重启后 `.148`、精确 ELF SHA-256、有效 OTA 分区和显示错误 0 核验通过。Windows 从新 development 路径启动曾再次产生网络访问授权提示；现已恢复原固定 Release 路径，DLL SHA-256 `1716897ca155196a43159ae4455d9452174097bd5df55abb68268d0287ef8382`。配对资料、自动轮播、六页顺序及 15 秒间隔核对一致。未改动防火墙规则。
- 用户首轮观察：名画横屏约 5 秒、竖屏约 10 秒；书法横竖约 3 秒。这一轮切换运行路径中断了连续计时，不能据此给出可靠提速比例。
- 22:32 冷启动复测仍为同一幅 `cma-1964.242` 名画，横屏 151,426 字节，竖屏 435,373 字节。横屏期间暂停成功，共持续 3,442 ms；竖屏预取在上一轮 Wi-Fi 恢复期间启动，申请因状态机忙而被直接跳过，后续未重新暂停。最终竖屏 fetch=11,557 ms、decode=34 ms、total=11,591 ms，未达到改善目标。
- 两张图均走 BLE。链路从首个 manifest 的 60 ms 间隔进入 15 ms / 2M；竖屏大回复 48 KiB 仍花费约 0.8–1.9 秒，读取/解码不是这次慢点。原始数据见 `artifacts/development/ble-gallery-148/hardware-observations.jsonl` 和 `image-events.json`。

## .149 修复设计

- 成功下载后保留 600 ms 的短暂空闲窗口，紧接着的另一方向可接管已暂停的无线状态，避免两版之间反复恢复/重新暂停。继承第一次申请的 35 秒期限，接管不延长硬上限。
- 没有后续任务、模式变化、失败或期限到达仍恢复 Wi-Fi；恢复提交和接管使用原子状态转换，只有取得恢复权的 Wi-Fi 任务才调用 start，避免在新任务传输中错误恢复。
- 若确实已开始恢复，新任务在原有总计 500 ms 准备预算内等待并尝试申请，不取消他人的租期；超过预算继续原共存路径。
- 诊断 stage 增加 5，表示等待下一方向的短暂空闲暂停。字段数量与旧 Windows 数字校验器保持兼容；桥接继续从已授权固定路径运行。
- 新增真实图库工作线程回归：两方向连续加载只 stop/start 一次、恢复尚未执行时仍可重新申请、模式变化恢复、空闲到期恢复、接管不延长 35 秒上限。固件构建、四组主机回归及实际更新说明预览通过；新镜像 6,530,912 字节，SHA-256 `59507517f4fa79f4192e982614d4861df56a81e088e78bf675e6a84184ec6946`，ELF `3c6489f333bdca51159acce8588ebef488e93f9019a3736a5cf2627c4fb0b919`。候选和验证见 `artifacts/development/ble-gallery-149/`，已安装，实机结果见下方。

## .149 本机验收

- 用户批准后通过 Wi-Fi OTA 安装；压缩传输 4,072,099 字节，电脑发送 15,156 ms。精确 ELF `3c6489f333bdca51159acce8588ebef488e93f9019a3736a5cf2627c4fb0b919`、`ota_1 VALID`、错误 0 和显示刷新核验通过。安装验收后桥接正常退出、恢复轮播并从固定 Release 路径启动，原配置保持。
- 22:51:58–22:52:09 同图横竖下载均走 BLE。竖屏 435,373 字节、SHA-256 `257adacb70b60613ab673c3c5bb311effd833bf8d4f480088ab45d959fead9af`，fetch **7,260 ms**、decode **33 ms**、total **7,294 ms**；未命中缓存。相较 .147 原记录 9,484 ms 缩短 **23.45%**；相较存在衔接遗漏的 .148 单轮 11,557 ms 缩短 **37.18%**。这是同图的跨运行单轮比较，不是长期/统计稳定增益。
- 暂停状态在竖屏下载期间继续保持，累计暂停 **11,713 ms** 后恢复，stop/start 成功、restored=1、expired=0；之后 Wi-Fi 重新联网且状态刷新。单次暂停覆盖两方向，.148 的衔接遗漏已在这一轮修正。
- 用户观察横屏 5 秒内、竖屏 8 秒内，并反馈曾断开重连。日志显示 .149 启动后 22:50:32–22:50:42 曾重连；22:51 的精确启动核验又主动退出/启动桥接，22:51:54 连接次数变为 3。实际图片传输全程及随后采样维持 3，最近中断为“无”。用户随后确认看到的重连是在升级/桥接重启附近，与时间线吻合；本轮下载未观察到掉线。这不等于长期稳定性证明。
- 22:57:37 用户主动重启再测（已确认），桥接记录 `FAILED voice read mailbox`；22:57:39 设备运行时间回到 2,377 ms，BLE 计数清零，22:57:48 已重新认证并确认数据。这是重启时旧连接失效，不是下载中掉线。22:58:03–22:58:18 同一名画横竖再次通过 BLE 冷下载，竖屏 fetch **8,540 ms**、decode **34 ms**、total **8,574 ms**，暂停 **13,195 ms** 后成功恢复且未到期；本次启动后的连接次数在下载及后续观察中保持 1，设备断开原因保持 0。
- 两轮 .149 竖屏 fetch 为 **7.26 / 8.54 秒**，相较 .147 同图一次记录 9.484 秒分别缩短约 **23% / 10%**；存在运行波动，不能保证每次低于 8 秒。22:58:33 之后的书法请求已经走 USB，不能计入 BLE 提速。23:03 复核自动轮播仍为 auto、启用、原六页原顺序、15 秒；保留重启中断历史，不将“当前已连接”写成“从未发生中断”。
- 证据：`artifacts/development/ble-gallery-149/{boot-149-verified.json,deployment-verified.json,hardware-observations.jsonl,image-events.json,performance-result.json}`。

## 判断依据

- .147 实机竖屏书法 250,678 字节下载 6,953 ms，名画 435,373 字节下载 9,484 ms；解码分别 134/18 ms。瓶颈主要在传输，缓存不能解决首次下载。
- [历史无线隔离对照](TAB5-BLE-OTA-PREFLIGHT.md)：三轮 256 KiB 下载暂停 Wi-Fi 共 13.997 秒，共存共 18.953 秒；一次短测缩短 26.15%，支持此方向，但不是新候选的收益保证。
- 滚动发送队列和原生并发 31 均已有撤回记录；保留 window64/native7、MTU 517、48 KiB 范围请求及最终 ACK。真实 JPEG 用 zlib level 6 无损压缩仅减少 4.09%/0.60%，本次不扩展压缩协议。

## 变更与边界

- 仅 `LINK_BLE` 明确选定模式、图片缓存未命中时申请暂停。自动模式下即使当前回落 BLE，也继续保留 Wi-Fi 恢复通路；USB/Wi-Fi 图库不申请此暂停。
- 复用现有 Wi-Fi 任务的 stop/start 状态机，工作线程以通知唤醒，最多等待准备 500 ms。准备失败或超时仍通过原 BLE 共存路径下载；后台驱动稍后完成 stop 时也必须执行恢复。
- 下载完成、错误和模式改变释放暂停，另有从申请时起 35 秒的期限。Wi-Fi start 失败每秒重试；驱动启动成功不等于路由器已经重新关联。平台驱动本身阻塞时不承诺硬实时期限。
- 超时恢复后，旧调用者仍持有取消权，直到主动释放；不能让旧请求取消新一轮测速。诊断测速仍要求 USB，普通图库暂停不要求连接 USB。
- 缓存命中完全跳过暂停。保留图片质量、完整性、配对及网络配置；同一张图横竖预取逻辑不变。本候选没有扩大到蓝牙语音、附件或完整 OTA。

USB 健康回复新增可选 `wifiIsolation` 字段，格式沿用已有测速回复：`version,stage,stopRc,startRc,heldMs,restored,expired`，其中 stage 0/1/2/3/4/5 对应 idle/arming/requested/stopped/restoring/quiet。Windows 使用原七项数字校验器读取；旧固件不带字段时兼容。该诊断记录最新一次暂停，单独出现旧记录不能证明当前图片正在暂停。

## .148 构建记录（安装前记录）

- 历史镜像：`artifacts/firmware/tab5/versions/0.2.148-ui/a56d41e49dde-fc81e37a/aibot_tab5.bin`，版本 `0.2.148-ui`，6,530,400 字节。固定 latest 现为上方 .149 修复候选。
- SHA-256：`a56d41e49dde5b13d343e5ffd0e6b4288d6356a09275a40653996f696a876313`。
- ELF SHA-256：`aa71895a3101e967526c3f4a1ea348b9f9c4467a89aba34067d8812bab4e6eea`。
- Windows Release 构建零警告/错误；BLE 分片、调度、窗口、语音、RPC 与 726 次鉴权图库回归通过。ESP-IDF 构建通过。
- 原生产 C 状态机、图库工作线程、JPEG 缓存和 OTA RAM 预检测试通过；覆盖取消、模式变化、期限、停止失败、恢复重试、租期占用、旧调用者释放、准备超时、坏哈希、缓存命中、三通道及离线复看。
- 证据位于 `artifacts/development/ble-gallery-148/`。实际 sidecar 经原生更新说明页预览；其后已获授权安装；实机记录及未通过项见上方。

验收用同一作品、相同方向和空设备缓存比较，并记录设备 fetch/decode、桥接传输及 Wi-Fi 暂停/恢复；缓存命中不得计入首次提速。结束恢复原自动轮播、六页顺序和 15 秒间隔。历史 26.15% 不作为 .148 承诺。

## English

The current published releases are [TAB5 .149](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.149-ui) and [bridge 0.6.1](https://github.com/yaoyouzhong/AI-bot/releases/tag/bridge-v0.6.1). Candidate-stage observations below remain dated evidence.

The locally installed .148 pauses Wi-Fi only for uncached gallery downloads in explicitly selected BLE-only mode. A 500 ms preparation budget falls back to normal coexistence; completion, failure, mode changes and a 35-second lease restore the driver. Failed starts retry. Automatic-mode fallback, saved networks, pairing, image quality and BLE window64/native7 remain unchanged. Cache hits do not pause networking. Lease ownership survives expiry until the original caller releases, preventing stale cancellation of a newer job. Diagnostic benchmarks retain their USB requirement.

USB health adds the optional existing seven-number `wifiIsolation` diagnostic, consumed by the Windows bridge. Builds and simulated production-path regressions pass; they do not prove real radio throughput, reassociation or whole-image BLE OTA. Historical Wi-Fi isolation reduced a short test by 26.15%; the cold .148 portrait painting fetch instead took 11,557 ms because its pause request collided with the preceding image restoration and was skipped. The .149 correction retains the paused radio across a 600 ms idle handoff, inherits the original 35-second hard deadline and retries an in-progress restoration within the existing 500 ms preparation budget. Atomic transitions arbitrate restoration and handoff. Stage 5 denotes the idle handoff. The correction was subsequently authorized, installed and hardware-checked as recorded below. The bridge has been moved back to its existing fixed Release path to avoid new-path firewall prompts; pairing and the original automatic carousel were verified.

The installed .149 passed exact ELF/valid-partition verification. The same uncached portrait JPEG fetched in 7,260 ms and decoded in 33 ms, compared with 9,484 ms in the .147 record and 11,557 ms in the flawed .148 run. These are single-run comparisons (23.45% and 37.18% shorter), not a sustained speed guarantee. One 11,713 ms Wi-Fi pause covered both layouts and restored successfully without expiry. A post-OTA reconnect and the deliberate desktop restart preceded image transfer; the connection count remained 3 throughout transfer. The user confirmed that the observed reconnect was near the upgrade/desktop restart. No disconnect was observed during this image transfer; long-term stability is not established by one run.

The user independently rebooted at 22:57:37 and confirmed this action. The old mailbox read failed during that reboot; uptime and connection counters reset, and authenticated data resumed by 22:57:48. The next uncached BLE transfer of the same portrait fetched in 8,540 ms and decoded in 34 ms, with one successful 13,195 ms Wi-Fi pause and no expiry. Connection count stayed at 1 throughout this second transfer and subsequent observation, with device disconnect reason 0. The two .149 fetches were approximately 23% and 10% shorter than the single .147 baseline, showing variation rather than an under-eight-second guarantee. Later calligraphy transfers used USB and are excluded from BLE measurements. The original automatic six-page carousel and 15-second interval were rechecked.
