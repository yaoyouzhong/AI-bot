# Functional parity contract

本文件定义 AI-bot 的完整功能边界。状态只允许使用：`planned`、`partial`、`implemented`、`build-verified`、`device-verified`、`platform-unverified`。`partial` 表示只有明确列出的子能力完成；没有真实证据时不得提升状态。

| 能力 | Windows | 固件 | macOS | 验收标准 |
| --- | --- | --- | --- | --- |
| Claude / Codex 活动状态 | build-verified | build-verified | platform-unverified | Mac 源码读取文件元数据并发送状态；需在 Mac 构建，核对工作、空闲、离线语义且不读取会话正文 |
| Claude / Codex 账户额度 | partial | partial | platform-unverified | Windows、固件及 Mac 源码已有解析/缓存链路；真实账号、令牌刷新、设备显示和 Mac 构建待验收 |
| 国产模型额度 | partial | partial | 不适用 | 四家解析/缓存、隔离 WebView2 登录捕获和固件总览已构建；MiniMax 环境变量 Key 可直连，真实账号响应及实机显示待验收 |
| 天气 | partial | partial | platform-unverified | Windows/固件及 Mac 源码均有数据、缓存、设置和中文 RGB565 资源链路；真实配置、设备显示及 Mac 构建待验收 |
| 股票 | partial | partial | platform-unverified | Windows/固件及 Mac 源码均有 A/H/美股解析、缓存、设置和名称资源链路；真实配置、分页显示及 Mac 构建待验收 |
| 系统监控 | build-verified | build-verified | platform-unverified | Windows 实际取样与固件页已构建；Mac 公共 Darwin/Mach 取样源码待平台构建，设备数值待实机核对 |
| 音乐 | partial | partial | planned | 媒体会话、中文文本位图、封面、进度和 AUTO 切换已构建；真实歌曲与设备显示待验收 |
| 桌宠 | partial | partial | planned | 原创建模动画、许可文件门槛、外部图片转换/上传/渲染已构建；真实 USB 与设备显示待验收 |
| 屏保 | partial | partial | platform-unverified | Mac 已有手动模式及基于本机空闲时间的进入/输入恢复源码；AI/音乐临时唤醒和 Mac/设备时序待验收 |
| USB 优先 | partial | partial | platform-unverified | Mac 已有 460800 探测、握手、状态帧及 COBS/CRC/ACK 大资源传输源码，但无资源生产集成且未编译/实机；真实串口待验收 |
| Wi-Fi 回退 | build-verified | build-verified | platform-unverified | Mac 已有 Keychain 随机令牌、认证 LAN 和 USB `lan_config` 源码；需验证 8 秒回退及 USB 恢复自动切回 |
| 设备控制 | partial | partial | platform-unverified | Mac 菜单已有 USB 模式/亮度/重发回退配置源码；设备信息/Wi-Fi 重置尚缺可靠地址发现，整体待设备验收 |
| PC 离线独立时钟 | build-verified | build-verified | platform-unverified | Mac 状态帧含 epoch/时区并在退出时发 host-away；需验证 `PC OFF`、NTP/保持时间及恢复页面 |
| Windows 托盘和镜像 | partial | 不适用 | 不适用 | 托盘、设备控制、非敏感设置持久化和九页面 240×240 镜像已构建；启动项及设置窗视觉待验收 |
| macOS 菜单栏和镜像 | 不适用 | 不适用 | platform-unverified | 菜单栏、活动/账户额度、天气/股票、系统指标、设置、Keychain、认证 LAN 和 USB 小帧源码已建；镜像、其余能力及真实 Mac 构建待完成 |

## 完成定义

“完整迁移”必须同时满足：

1. Windows Release 构建、状态接口和自动化测试通过。
2. 固件 PlatformIO 构建通过，并在真实 ESP8266 上逐页验证。
3. USB 状态、控制、大资源传输和 8 秒 Wi-Fi 回退均做断线/恢复测试。
4. 天气、股票和额度使用真实数据源测试；断网时验证最近成功缓存。
5. 仓库不包含旧上游源码、截图、logo、精灵图、凭据或运行缓存。
6. macOS 至少完成 Swift 构建；若没有真实 Mac，则发布说明必须标记未验证。
