# Functional parity contract

最新逐项证据及明确剩余项：[全功能对齐审计](FULL_PARITY_AUDIT_2026-09-09.md)。静态镜像对比不提升设备或平台验收等级。

本文件定义 AI-bot 的完整功能边界。状态只允许使用：`planned`、`partial`、`implemented`、`build-verified`、`device-verified`、`platform-unverified`。`partial` 表示只有明确列出的子能力完成；没有真实证据时不得提升状态。

当前以[持续迁移状态](MIGRATION_STATUS_2026-09-09.md)为准。旧“本轮完成”只是历史批次，不代表全部对齐；以下 `partial` 同时包含代码差异与尚待真实数据源/设备验收的状态。

| 能力 | Windows | 固件 | macOS | 验收标准 |
| --- | --- | --- | --- | --- |
| Claude / Codex 活动状态 | build-verified | build-verified | platform-unverified | Mac 源码读取文件元数据并发送状态；需在 Mac 构建，核对工作、空闲、离线语义且不读取会话正文 |
| Claude / Codex 账户额度 | partial | partial | platform-unverified | Windows、固件及 Mac 源码已有解析/缓存链路；真实账号、令牌刷新、设备显示和 Mac 构建待验收 |
| 国产模型额度 | partial | partial | 不适用 | 四家解析/缓存、隔离 WebView2 登录捕获和固件总览已构建；MiniMax 环境变量 Key 可直连，真实账号响应及实机显示待验收 |
| 天气 | partial | partial | platform-unverified | Windows/固件及 Mac 源码均有数据、缓存、设置和中文 RGB565 资源链路；真实配置、设备显示及 Mac 构建待验收 |
| 股票 | partial | partial | platform-unverified | Windows/固件及 Mac 源码均有 A/H/美股解析、缓存、设置和名称资源链路；真实配置、分页显示及 Mac 构建待验收 |
| 系统监控 | build-verified | build-verified | platform-unverified | Windows 实际取样与固件页已构建；Mac 公共 Darwin/Mach 取样源码待平台构建，设备数值待实机核对 |
| 音乐 | partial | partial | platform-unverified | Windows/固件已有媒体会话、文本、封面、进度和 AUTO；Mac 已有默认关闭的 Apple Music/Spotify 元数据、进度、封面、授权 App 包、中文文本和屏保唤醒源码，必须实机验证 |
| 桌宠 | partial | partial | platform-unverified | Windows 与 Mac 均有许可文件门槛、112×112 RGB565 转换/上传源码，固件另有原创建模动画；Mac 构建、真实 USB 与显示待验收 |
| 屏保 | partial | partial | platform-unverified | Mac 已有手动模式、空闲进入、输入恢复，以及音乐页/桌宠页 12 秒临时唤醒源码；Mac/设备时序待验收 |
| USB 优先 | partial | partial | platform-unverified | Mac 已有 460800 探测、握手、状态帧、COBS/CRC/ACK 及天气/股票/音乐/桌宠资源集成源码，但未编译/实机；真实串口待验收 |
| Wi-Fi 回退 | build-verified | build-verified | platform-unverified | 两端已有保持供电的暂停测试入口，Windows 有独立 CLI；检查 USB/LAN 计数和恢复，异常自动解除暂停；真实 8 秒回退及恢复待验收 |
| 设备控制 | partial | partial | platform-unverified | Windows/Mac 设备信息和二次确认 Wi-Fi 重置已改为 USB 请求/应答，不依赖 LAN；固件保留认证 HTTP 接口；需 Mac 构建及设备验收 |
| PC 离线独立时钟 | build-verified | build-verified | platform-unverified | Mac 状态帧含 epoch/时区并在退出时发 host-away；需验证 `PC OFF`、NTP/保持时间及恢复页面 |
| Windows 托盘和镜像 | partial | 不适用 | 不适用 | 托盘、设备控制、非敏感设置持久化和九页面 240×240 镜像已构建；启动项及设置窗视觉待验收 |
| macOS 菜单栏和镜像 | 不适用 | 不适用 | platform-unverified | 菜单栏、镜像、图库、分角色默认/自选缓存、状态事件/Token、轮播、认证 LAN 资源源码与 XCTest 已接入；真实 Mac 构建及完整运行待完成 |

## 完成定义

视觉完成还必须满足 [旧版视觉基线](VISUAL_PARITY.md)：保留旧版绝大部分展现形式。当前新版页面尚未符合该要求，不得以功能字段存在或串口测试通过代替视觉对齐。

恢复旧版后的隔离迁移进展与最新测试见 [续迁记录](MIGRATION_RESTART_2026-09-08.md)。
2026-09-08 新增维护者源码复用、14 页渲染、原图标、轮播策略、曲线、APET
和额度缓存适配均不提升本表的真机等级；原默认桌宠本机导入、角色缓存、提醒
以及 Mac 窗口现已有代码，完整实体视觉和 Mac 运行仍待验收。源码复用只限
`PROVENANCE.md` 逐项列明的维护者作品。

2026-09-08 首轮失败后，RX 缓冲修复版与大屏保时钟版各通过一轮完整 USB 真机测试（四类资源、九模式回读、亮度、超时/恢复）；用户确认大屏保时钟实体屏符合旧版。Wi-Fi 回退未通过，其他页面视觉未完成，功能等级不因此整体提升。详见 [真机记录](HARDWARE_ACCEPTANCE_2026-09-08.md)。

2026-09-08 近期差异核对与补齐范围见 [对齐记录](ALIGNMENT_2026-09-08.md)。它不提升本表的真机或 macOS 验收等级，也不宣称新旧界面逐像素一致。

“完整迁移”必须同时满足：

1. Windows Release 构建、状态接口和自动化测试通过。
2. 固件 PlatformIO 构建通过，并在真实 ESP8266 上逐页验证。
3. USB 状态、控制、大资源传输和 8 秒 Wi-Fi 回退均做断线/恢复测试。
4. 天气、股票和额度使用真实数据源测试；断网时验证最近成功缓存。
5. 仓库不包含旧上游源码、截图、logo、精灵图、凭据或运行缓存。
6. macOS 至少完成 Swift 构建；若没有真实 Mac，则发布说明必须标记未验证。
