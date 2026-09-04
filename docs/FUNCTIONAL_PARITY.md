# Functional parity contract

本文件定义 AI-bot 的完整功能边界。状态只允许使用：`planned`、`implemented`、`build-verified`、`device-verified`、`platform-unverified`。没有真实证据时不得提升状态。

| 能力 | Windows | 固件 | macOS | 验收标准 |
| --- | --- | --- | --- | --- |
| Claude / Codex 活动状态 | implemented | implemented | planned | 工作、空闲、离线和待授权语义一致；不上传会话正文 |
| Claude / Codex 账户额度 | planned | planned | planned | 显示供应商返回的窗口、重置时间、套餐；失败保留最近成功值 |
| 国产模型额度 | planned | planned | 不适用 | 阿里云百炼、Kimi、MiniMax、DeepSeek 分来源显示；凭据不进配置或缓存 |
| 天气 | planned | planned | planned | 当前温度、高低温、湿度、空气质量、城市和更新时间；主源失败可降级并保留缓存 |
| 股票 | planned | planned | planned | A/H/美股自选、涨跌颜色、中文名称、分页和最近成功缓存 |
| 系统监控 | planned | planned | planned | 上下行速率、CPU、内存以固定节奏更新 |
| 音乐 | planned | planned | planned | 标题、歌手、进度、封面；AUTO 播放时进入、停止后恢复 |
| 桌宠 | planned | planned | planned | 内置原创形象；可选择兼容许可的外部桌宠并上传；不分发来源不明资产 |
| 屏保 | planned | build-verified | planned | 手动预览、空闲进入、事件临时唤醒、恢复原页面；独立时钟可用 |
| USB 优先 | implemented | implemented | planned | 460800 握手；小帧、控制和大资源可传输；端口按设备握手识别 |
| Wi-Fi 回退 | build-verified | build-verified | planned | USB 心跳失效 8 秒后转带配对令牌的 HTTP；USB 恢复后无人工干预切回 |
| 设备控制 | planned | planned | planned | 模式、亮度、配对、设备信息和 Wi-Fi 重置具备明确错误反馈 |
| PC 离线独立时钟 | build-verified | build-verified | planned | 主机离线显示 `PC OFF`；NTP/保持时间可用；桥接恢复后还原页面 |
| Windows 托盘和镜像 | planned | 不适用 | 不适用 | 分类菜单、240×240 镜像、配置入口和启动项行为可用 |
| macOS 菜单栏和镜像 | 不适用 | 不适用 | platform-unverified | 源码与构建流程齐全；在真实 macOS 验证前不宣称可用 |

## 完成定义

“完整迁移”必须同时满足：

1. Windows Release 构建、状态接口和自动化测试通过。
2. 固件 PlatformIO 构建通过，并在真实 ESP8266 上逐页验证。
3. USB 状态、控制、大资源传输和 8 秒 Wi-Fi 回退均做断线/恢复测试。
4. 天气、股票和额度使用真实数据源测试；断网时验证最近成功缓存。
5. 仓库不包含旧上游源码、截图、logo、精灵图、凭据或运行缓存。
6. macOS 至少完成 Swift 构建；若没有真实 Mac，则发布说明必须标记未验证。
