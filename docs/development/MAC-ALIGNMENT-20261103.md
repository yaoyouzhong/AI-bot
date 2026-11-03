# Mac 桥接开发对齐清单（2026-11-03）

基线为 `a9f56a1db126d73d8aa1ec4abef2663db39b565f`。以下是 Windows 上对仓库中 Mac 源码的静态核对，不是 Mac 构建或设备验收。状态使用“待开发／待核查／已有待验证／不适用”，由 Mac 端按现场证据更新。

| 优先级 | 用户行为与当前 Mac 证据 | 状态与开发/验收要求 |
| --- | --- | --- |
| P1 | 小屏 USB 资源重连：`mac-app/Sources/AIBotBridge/SerialBridge.swift` 在每次握手后创建 `sentRevisions`，资源按 revision 比较，分片有 ACK 与重试；尚不能证明同内容不同 revision 不重复传输。 | 待开发/验证：参照 Windows `SerialPublisher.cs` 的按内容缓存语义，ACK 成功才缓存，变化、失败、连接切换和重连正确失效。后台日志只记类型、次数和结果；用真实小屏测试重插、无多余闪屏及设备信息查询，不记录私有资源。 |
| P1 | 股票镜像：`MirrorWindow.swift` 将代码与名称按系统字体绘在同一文本区域，与 ESP8266 的 Font 4 子集、56×16 代码区不一致。实际小屏字形由共享固件绘制。 | 待开发：镜像使用 `firmware/src/StockCodeGlyphs.h` 对应字形与截断规则，7 字符按 8×16 格；对齐经用户确认的较细 I、名称和数字布局。先设计并检查 240×240 离屏预览，再与真机比较；不得重引入 Roboto 旧候选。 |
| P1 | 股票切页：共用固件 `firmware/src/main.cpp` 和 `StockPaintState.h` 已处理本页局部更新、内部翻页与非阻塞亮度过渡。 | 已有待验证：Mac 不叠加亮度动画；检查实际 Mac 数据更新、页面与列表翻页，确认 1–2 秒无多余整页刷新。固件已部署的设备仍需 Mac 链路验收。 |
| P1 | TAB5 Codex 告警：`MacRuntimeHealth.swift` 当前 Codex 状态为 observing/disabled，仅 CPU、内存生成告警；不能产生与 Windows 相同的模型连接异常。 | 待核查/开发：只有能取得真实模型业务请求成功/失败证据时才生成对应告警和具体原因，同一受影响通道的真实成功应及时清除；不同通道不能互相掩盖。证据不可用时保持未确认，不制造断联或健康结论。验证告警详情、恢复及其他并发告警。 |
| P1 | DeepSeek/GLM 累计消费：`MacDataStore.deviceSnapshot` 使用独立 DeepSeek 网页账户快照；`MacDomesticQuota.zhipuPair` 校验匹配的余额与消费；已有 `AccountSpendParityTests.swift`。 | 已有待验证：检查实际两个设备都取同一网页账户余额及消费，0、失效和账户/币种变化正确；GLM 两接口匹配失败时不借用旧消费。先运行既有测试，再用已授权账户真机检查。 |
| P2 | TAB5 生日：`MacTab5Service.swift` 提供 `MacBirthdays.projection()`，显示 30 天范围由已刷入的共用固件负责。 | 已有待验证：确认 Mac 继续发送完整生日集合，不在桥接中截断；今天、第 30 天显示、第 31 天不在近期列表，跨年、农历及其他日历/提醒仍正确。真实边界在 Windows 尚未验收。 |
| P2 | 性能/网络速率：`AppDelegate.swift` 每 250 ms 采集；`SystemMetrics.swift` 按 1 秒发布和四样本均值处理。 | 已有待验证：Mac 上跑现有测试并核对 TAB5 USB/Wi-Fi/BLE 数据、重连及系统占用；不把 Windows CPU/内存采样频率直接套用到 Mac。 |
| P2 | Windows 专用本机升级控制：`Tab5OtaControl.cs` 使用当前用户 IPC 和设备/哈希范围校验。 | 不适用：不机械移植命名管道；若 Mac 要实现自动化，采用平台适合的明确授权、设备身份、镜像摘要、不可重复发送及启动核验，并经专项测试。 |

## 证据和发布准备

- Windows ESP8266 验收及源变更：[验证记录](ESP8266-WINDOWS-VALIDATION-20261102.md)；TAB5 实装、测试、源码包及补丁：[TAB5 记录](TAB5-302-BIRTHDAY-HEALTH-20261103.md)。
- Mac 请按 [开发指引](MAC-DEVELOPMENT-GUIDE-20261103.md)执行 `swift test`、Release 构建、定向回归及实际设备验收，记录未验证项；不能把 Windows 的结果写成 Mac 通过。
- 本轮是主干同步及文档交接，不等于正式发布批准。Mac 对齐后双方从同一源码候选构建并验证各自交付物，再按统一流程准备发布。