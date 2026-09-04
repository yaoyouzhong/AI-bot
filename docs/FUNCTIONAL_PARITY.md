# Functional parity contract

本文件定义 AI-bot 的完整功能边界。状态只允许使用：`planned`、`partial`、`implemented`、`build-verified`、`device-verified`、`platform-unverified`。`partial` 表示只有明确列出的子能力完成；没有真实证据时不得提升状态。

| 能力 | Windows | 固件 | macOS | 验收标准 |
| --- | --- | --- | --- | --- |
| Claude / Codex 活动状态 | build-verified | build-verified | planned | 工作、空闲、离线和待授权语义一致；不上传会话正文 |
| Claude / Codex 账户额度 | partial | partial | planned | 解析、最近成功缓存和设备额度页已构建；真实账号授权、令牌过期刷新及设备显示待验收 |
| 国产模型额度 | partial | partial | 不适用 | 四家解析/缓存和固件总览已构建；MiniMax 环境变量 Key 可直连，阿里/Kimi/DeepSeek 登录捕获及实机验收待完成 |
| 天气 | partial | partial | planned | 数据/缓存、中文城市/天气位图和固件页已构建；真实配置及设备显示待验收 |
| 股票 | partial | partial | planned | A/H/美股解析、缓存、中文名称表、涨跌色和分页已构建；真实配置及设备显示待验收 |
| 系统监控 | build-verified | build-verified | planned | Windows 实际取样与固件页面已构建；设备端数值仍需实机核对 |
| 音乐 | partial | partial | planned | 媒体会话、中文文本位图、封面、进度和 AUTO 切换已构建；真实歌曲与设备显示待验收 |
| 桌宠 | partial | partial | planned | 原创建模动画、许可文件门槛、外部图片转换/上传/渲染已构建；真实 USB 与设备显示待验收 |
| 屏保 | partial | partial | planned | 手动模式、空闲进入/恢复、独立时钟和 AI/音乐事件临时唤醒已构建；设备时序待验收 |
| USB 优先 | partial | partial | planned | 460800 握手、小帧、大资源协议及音乐资源调用链已构建；真实串口传输待验收 |
| Wi-Fi 回退 | build-verified | build-verified | planned | USB 心跳失效 8 秒后转带配对令牌的 HTTP；USB 恢复后无人工干预切回 |
| 设备控制 | partial | partial | planned | 认证模式/亮度/设备信息/Wi-Fi 重置已构建；完整 UI 和设备验收待完成 |
| PC 离线独立时钟 | build-verified | build-verified | planned | 主机离线显示 `PC OFF`；NTP/保持时间可用；桥接恢复后还原页面 |
| Windows 托盘和镜像 | partial | 不适用 | 不适用 | 基础分类菜单已构建；240×240 镜像、完整配置入口和启动项待完成 |
| macOS 菜单栏和镜像 | 不适用 | 不适用 | platform-unverified | 菜单栏、活动状态、Keychain 令牌和认证 LAN 服务源码已建；其余能力及真实 Mac 构建待完成 |

## 完成定义

“完整迁移”必须同时满足：

1. Windows Release 构建、状态接口和自动化测试通过。
2. 固件 PlatformIO 构建通过，并在真实 ESP8266 上逐页验证。
3. USB 状态、控制、大资源传输和 8 秒 Wi-Fi 回退均做断线/恢复测试。
4. 天气、股票和额度使用真实数据源测试；断网时验证最近成功缓存。
5. 仓库不包含旧上游源码、截图、logo、精灵图、凭据或运行缓存。
6. macOS 至少完成 Swift 构建；若没有真实 Mac，则发布说明必须标记未验证。
