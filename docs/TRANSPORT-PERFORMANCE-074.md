# TAB5 蓝牙照片优化 .074 候选

2026-10-01。目标仍为照片点击“添加”至附件成功小于 3 秒，保持 1280×720 / JPEG quality 85。

## 当前真机结果

.073b 在 .073 固件上仅蓝牙六项完成，每项 262144 字节、错误 0、校验通过。上传中位 25.17 KiB/s，下载中位 25.21 KiB/s；通知提交失败计数前后均为 1，未新增失败。本轮为六项蓝牙验收，不是三通道 18 项验收。证据 speed-073b-ble-summary.json / speed-073b-ble-runtime.txt。

实拍 128703 字节，完整设备上传 4620 ms，用户确认成功、4.6 秒。电脑从首批收齐之后计时 2812 ms，不能用它替代点击/设备全过程。恢复自动选择后出现一次状态发送 ObjectDisposedException，与前面照片成功区分记录。3 秒目标未达成。证据 speed-073b-photo.txt。

## .074 实现

.073 一次同步提交最多 32 条通知在真机出现提交失败；SDK ble_gatts_notify_custom 的 notify_tx 事件表示提交尝试，不是无线实际完成通知，不能拿它直接作为队列排空信号。SDK ble_l2cap_tx 对控制器回压会排队剩余数据；异常提交可能已有 ACL 片段发出，因此不会盲目重发整条通知。

.074 将 RPC 通知放到 NimBLE host queue callout：一次最多提交四片，只有全局 MSYS 空闲块大于总块数减八时继续；资源不足或四片额度用完则 2 ms 后继续。每个授权窗口最多三秒，断开取消，错误提交终止。原请求标识/偏移/总长度/认证、整批接收后再授权保持，语音沿用原八片同步路径。

能力 rpcNotifyWindow=32 只允许电脑授权最多 32 个设备通知；rpcMailboxWindow=8 限制电脑回写每批八片。新桥接遇到 .073 或旧固件缺失新字段时通知保持八片，旧桥接连接新固件也保持八片。rpcPacing=[yield次数,终止次数,最近通知错误码] 辅助实际诊断。

## 验证与部署

Windows Release 构建零错误/警告；窗口回归含通知 32 / 回写 8、MTU 23/247/517、65535 字节；原乱序/丢授权/取消/溢出校验通过。RPC 与整合传输回归通过。原生 RPC 核心测试新增暂停不消费字节、每次最多四片、32 片授权、错误后终止不重发。固件构建通过。规定 dotnet run --status-once 因宿主 Console.OutputEncoding 无效句柄仍失败；直接候选 DLL status-once JSON 有效。

候选尚未部署；真实队列计数、全部蓝牙六项、照片耗时、语音和自动展示必须继续验收。不能以构建或模拟通过宣称提速。
冻结候选 speed-074-package 共 29 文件，未部署：
- aibot_tab5.bin: 6637872 bytes; SHA-256 aeed185e308313b00b694418c58d3f48810b96e23ad1e924630abb1ce5918520
- aibot_tab5.elf: 30353968 bytes; SHA-256 925126f56201d3a77bb655e088d8c7b24be0952a6d7be8379fde3e0eaef640e8
- AIBotBridge.dll: 2589184 bytes; SHA-256 2f802ef9a380b9f4fa134012a459168b750a848dd5999663b2544f42a72a0c17


2026-10-01 20:46 已将配套 .074 桥接安装到原 Release 目录，49 个原文件已备份并核对，29 个候选文件和 26 个运行文件哈希通过；仅 DLL/PDB 改变。固件升级、启动核验及蓝牙六项仍待真机操作。证据 speed-074-deployment.json。


## .074 已部署真机结果

启动核验 verified，固件 0.2.74-ui、ELF 925126f56201d3a77bb655e088d8c7b24be0952a6d7be8379fde3e0eaef640e8、ota_1、VALID。OTA 保留记录完整 6637872 字节、previousStage=9/error=0，设备接收写入 26917 ms（读 26573、写 18783）；并行不能相加。

仅蓝牙六项均完整 262144 字节、error=0；notify32-command/write8 已生效。上传耗时 13468/9641/21244 ms、下载 11645/26555/22499 ms，中位速度分别 19.01/11.38 KiB/s。相比 .073b 的 25.17/25.21 更慢。mailboxStats=[1632,0,0,52]、rpcPacing=[5906,0,0]，无通知分配/提交或窗口终止失败。分段运行间隔与无线发送可重叠，不能将 yield 次数乘 2 ms 全部当作额外延迟。此轮 Wi-Fi 服务 IP 从 192.168.31.122 变成 192.168.1.160，环境发生变化，不能把全部速度差异单独归因于窗口算法。

保留的 USB/蓝牙状态中断时间为升级前后 20:47:55/20:48:01，不能当成后来完整六项的失败。自动展示策略仍 auto、cycleEnabled=true、原六页顺序和 15 秒。照片及语音真实验收仍待继续，3 秒目标未达成。证据 speed-074-runtime.txt / speed-074-summary.json / speed-074-cycle-runtime.json。

## .074 实拍与桥接 074b 候选

用户确认成功、3.8 秒；设备完整 BLE 上传为 111532 字节 / 3845 ms / status=200，电脑收到 111532/111532 字节，elapsed=1938 ms（从首块已接收后计时）。图片较 .073b 的 128703 字节小约 13.3%，不能把耗时缩短全部记为算法收益；有效速度约 28.33 KiB/s，旧样本约 27.20 KiB/s。目标三秒仍未达成。

直播记录中图片三块 request=1703/1328/515 ms，queue=78/124/78 ms；notifySpan=1547/1156/375 ms，notifyMaxGap=63/47/47 ms。mailboxStats 从 [1632,0,0,52] 到 [1866,0,0,52]，窗口终止仍零。证据 speed-074-photo.txt / speed-074-photo-live.jsonl。

074b 只改桥接调度：大请求或图片上传事务期间状态最长避让六秒，覆盖块间间隙；长上传仍定期更新状态，保持八秒新鲜度规则。仅当图片/大请求进行中且语音尚未活跃时，将空闲语音轮询从 250 ms 改为 500 ms；活跃语音仍 15 ms，正常空闲仍 250 ms。保持 .074 固件、通知 32 / 回写 8，照片质量不变，无需再次刷固件。

Release 构建零警告/错误；新调度回归模拟跨块照片与长上传，验证照片不被状态插入、长上传不超过八秒状态新鲜度，以及活跃语音节奏；BLE 大帧/慢连接/取消/完整确认、BLE 窗口/语音和 RPC 回归通过。规定 dotnet run status-once 仍因宿主 Console.OutputEncoding 无效句柄失败，直接候选 DLL 输出有效 JSON。已冻结 speed-074b-package，尚未部署，实际收益待真机验证。

2026-10-01 21:10 桥接 074b 已备份替换。原运行目录 49 个文件已备份并核对，26 个候选运行文件哈希全部通过，仅 DLL/PDB 改变。DLL SHA-256 a1f22e6746206cd1437257a6fd5522e4aacb216a220183130c1823a3df497410。固件仍为 .074；照片对比待用户操作。证据 speed-074b-deployment.json。


## 桥接 074b 真机照片与 074c 候选

074b 实拍 115186 字节 / 3396 ms / status=200，用户确认成功、3.4 秒。比 .074 的 111532 字节 / 3845 ms 图片更大，但本轮耗时少 449 ms；单样本不能承诺普遍收益。电脑从首块收齐后计时 1484 ms，不能代替全过程。mailboxStats=[2108,0,0,52] / rpcPacing=[6142,0,0]，仍无通知失败或窗口终止。

三块 request=1516/891/343 ms、queue=108/0/172 ms，最后一块 handler=141 ms、response=172 ms。已确认实现缺口：BulkReceiving 标记在请求字节收齐时被清除，最后一块 Received=size 又令 ImageUploadActive 变为 false，附件处理和最终回复仍可能与状态竞争。

074c 将该标记改为 BulkActive，覆盖读请求、处理附件和成功回复的整段事务，并在成功/取消/失败 finally 清除。RPC 还能在开始时继承图片事务优先级，保护不足 12 KiB 的最后一块；不因小尾块提前解除避让。六秒状态避让上限、活跃语音节奏和通知32/回写8不变；固件保持 .074，无需刷机。

Release 构建零警告/错误。大块/小尾块的处理与回传优先级、取消和回传失败释放回归通过，原窗口/语音/RPC认证与字节回归通过。规定 dotnet run status-once 的宿主无效句柄错误仍存在，直接候选 DLL JSON 有效。候选 speed-074c-package 已冻结，未部署，三秒目标未达成。证据 speed-074b-photo.txt / speed-074b-photo-live.jsonl / speed-074c-ble-voice.log。
074c DLL SHA-256: eb0d9d6da4383290d038bc1fd0f65ae1033b00de165ef1759d3cb3c6e81e08f1

2026-10-01 21:28 桥接 074c 已备份替换，原 49 个运行文件备份并核对，候选 26 文件哈希通过，仅 DLL/PDB 改变。固件仍 .074，实际照片对比待完成。证据 speed-074c-deployment.json。启动后 displayPolicy 为 auto、循环启用，保留原六页与 15 秒间隔。


## 074c 本轮照片达到三秒目标

用户确认“已添加，2.7s”。设备完整 BLE 上传 118509 字节 / 2748 ms / status=200，电脑收到 118509/118509 字节；照片保持 1280×720、JPEG quality 85。这是本轮单张实拍通过，不保证不同图像大小、无线环境下都低于三秒。

三块 RPC request=922/937/547 ms，queue=0/16/0 ms，response=15/16/32 ms；最后附件处理 140 ms 后成功回复未被状态帧插队。mailboxStats=[2357,0,0,52]、rpcPacing=[6195,0,0]，无分配/提交/终止失败。电脑 elapsed=1687 ms 从首块接收后计时，不用于替代全过程。证据 speed-074c-photo.txt / speed-074c-photo-live.jsonl / speed-074c-photo-summary.json。

自动展示已核对：auto、cycleEnabled=true、15秒，原 codex/domestic_deepseek/domestic_zhipu/weather/stocks/system 六页顺序不变，配对仍匹配磁盘。下述收尾复测已完成，代码优化在本轮照片达到目标后停止；未做新的真实语音测试。

## 074c 完整三通道收尾验收

2026-10-01 完整测速结束为 complete：USB、Wi-Fi、BLE 各上传/下载三轮，共 18/18 成功。每项完整 262144 字节、error=0；USB 第三轮上传 530 ms、下载 836 ms，先前 .073b 的第三轮中断本轮未重现，不能据此承诺已消除所有偶发断连。

| 通道 | 上传中位耗时（ms） | 上传中位（KiB/s） | 下载中位耗时（ms） | 下载中位（KiB/s） |
| --- | ---: | ---: | ---: | ---: |
| USB | 530 | 483.02 | 994 | 257.55 |
| Wi-Fi | 1428 | 179.27 | 608 | 421.05 |
| BLE | 5268 | 48.60 | 4228 | 60.55 |

最终 mailboxStats=[5621,0,0,52]、rpcPacing=[6910,0,0]，通知分配/提交失败、窗口终止和最近通知错误均为零。本轮最低内部空闲内存 155599 字节、最大连续块最低 63488 字节。诊断保留的 USB/蓝牙中断时间为 22:14:48/22:14:50，早于本轮完成的测速；它们未被清除，也不作为本轮数据项失败。收尾时 link=[60000,...] 是空闲状态，不用于推断测速期间实际连接间隔。

收尾重新核对 26 个运行文件，大小及 SHA-256 均匹配 speed-074c-manifest.json；固件仍 0.2.74-ui，配对匹配磁盘。电脑自动轮播保持原六页顺序和 15 秒，没有以测试列表覆盖。真实照片 118509 字节 / 2748 ms / status=200、用户确认 2.7 秒，作为本次三秒目标的单张实拍通过证据；照片质量保持 1280×720 / JPEG 85。

本地构建及协议/优先级回归已通过。规定的 dotnet run --status-once 仍有宿主 Console.OutputEncoding 无效句柄限制，直接候选 DLL 输出有效 JSON；不把该命令记为通过。本轮没有重新做真实语音测试，也没有提交、推送或发布。

证据位于 AI-bot/artifacts/acceptance-20261001：speed-074c-closeout-runtime.txt、speed-074c-closeout-final-runtime.txt、speed-074c-closeout-live.jsonl、speed-074c-closeout-summary.json、speed-074c-closeout-cycle.json。照片汇总 speed-074c-photo-summary.json 已关联完整三通道验收结果。
