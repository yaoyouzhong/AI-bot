# AI-bot v0.6.8 / TAB5 0.2.210-ui

2026-10-17 正式发布 Windows **0.6.8** 和 TAB5 **0.2.210-ui**。Mac 无功能变更，保留已发布 **0.6.7**；ESP8266 **0.5.3** 与现有标准／高清图库不重新编号。Codex 会员保留 **Pro**，不推测接口未提供的倍率或显示价格。

[完整下载](../DOWNLOADS.md) · [安装图文指南](INSTALL.zh.md) · [更新步骤](UPDATES.md) · [功能图鉴](FEATURES.zh.md) · [宣传视频与来源](PRODUCT_VIDEO.md)

## Windows 0.6.8

- 服务状态区分小屏、TAB5、局域网和数据采集故障，给出对应建议；保留已有报警时序和恢复清除规则。小屏拔线或串口驱动异常不再终止工作线程并引发 TAB5 全局报警。
- 普通设置及查看窗口可与设备中心同时使用，同一设备和功能复用窗口，保留原设备目标、保存／取消和未保存关闭保护。
- 安装器及固件下载支持取消、网络中断或应用重启后续传；缓存绑定组件、版本、文件名、大小和 SHA-256。忽略 Range 时安全重下，校验失败不进入安装／刷机。
- 安装器保留同字节的旧客户端兼容附件；升级保留配对、额度历史、图库及展示设置。下载完成后仍明确确认安装或设备升级。

## TAB5 0.2.210-ui

- 日程“今日／全部”分段筛选下移，与左侧列表对齐；“新建”置于列表旁，右侧突出时间与详情。保留五条完整行、长标题和滚动；没有事项时居中显示“暂无事项”和“新建”。移除多余操作与容量说明，语音识别需要电脑，离线可用键盘。
- 经典时钟时分与小秒数组合居中，每秒更新不再带动时分抖动；保留每分钟轻微移位。经典时钟及年度点阵按本地 07:00–19:00 使用浅色，其余黑底；年度点阵保留暖灰，不单列暗夜或自动昼夜。
- 语音状态副本与文字缓冲移入 PSRAM，保留内部 OTA 缓冲，恢复内部可用内存和固件写入速度。
- 插电启动且缺少可信电量时自动采样估算并恢复充电；读写故障有恢复和重试边界，无需拔线触发。初始采样期间可能短暂显示“--”。
- Windows 固件构建由 CMake 自动生成响应文件，解决命令行长度限制；最多八个物理核心、最多两个性能核心，保留低优先级及增量构建。

## 验证与交付

Windows 安装、真实运行、隔离窗口与下载回归以及最终包检查已完成；发布使用同一批验收字节。TAB5 210 已刷入并验证精确版本和 `ota_1 VALID`：写入 **9.609 秒**，OTA 安装共 **19.654 秒**；日程验收后内部空闲 **75,087 字节**、最大连续块 **32,768 字节**，无本次采样错误。用户确认日程布局、点击返回及 USB 始终连接时的重启电量正常。以上速度是本次连接测量，不承诺所有设备和通道相同。

[完整 210 验收记录](development/TAB5-210-ACCEPTANCE-20261017.md)保留发布前状态；公开源码见[开发入口](development/TAB5-SOURCE.md)。独立环境逐字节复现 BIN 未验证，BLE 整包 OTA、满额清理和长期稳定性不扩大为本次已验收结论。TAB5 保留用户展示设置，自动模式／轮播恢复仅适用于 ESP8266 小屏。

Windows 安装器：`AI-bot-bridge-v0.6.8-windows-x64-setup.exe`，SHA-256 `9b3d7e95dd622002c608762fbafa4febb49b4478fa17c0e651c3a2e2626ac5d9`。

TAB5 应用：`aibot_tab5.bin`，SHA-256 `21e733f4c9f5e0940c4d3a7d5debac29e60051ad7c4bdb6466b48d32c47f4335`。首刷 ZIP 与升级 ZIP 包含相同应用，格式不可混用。发布包附许可证、第三方声明与校验清单。

## English

Released on October 17: Windows **0.6.8** and TAB5 **0.2.210-ui**. Mac retains **0.6.7**, ESP8266 retains **0.5.3**, and existing artwork packs keep their versions. Codex membership remains **Pro**, without inferred multipliers or prices.

Windows adds attributed connection advice, resilient serial workers, reusable modeless Device Center panels with unsaved-edit protection, and resumable installer/firmware downloads with full identity and SHA-256 validation. Installation and flashing remain explicit. Published packages preserve the locally accepted bytes and user data.

TAB5 includes the refined agenda list/detail layout and centered empty state, concise offline keyboard/connected-computer dictation guidance, stable classic-clock hour/minute positioning, automatic local-time day/night colors, restored internal-memory OTA staging and automatic battery estimation after a connected-USB reboot. CMake response files address Windows command length; parallelism follows the bounded eight-physical-core policy.

Windows installation/runtime and local regressions passed. TAB5 210 boot identity, agenda interactions, internal memory, USB writing and connected-USB reboot battery display passed local hardware acceptance: 9.609 seconds writing, 19.654 seconds total installation, 75,087 free internal bytes and a 32,768-byte largest block after agenda acceptance. These are measurements for this connection. Independent byte-identical rebuilds, full-image BLE OTA and long-running stability are not claimed. Native screenshots/video use isolated demo data; hardware evidence is recorded separately. Every release now follows the [documentation and media closeout checklist](RELEASE-CHECKLIST.md).

[发布收尾与公开附件核验](development/RELEASE-068-210-CLOSEOUT-20261017.md) · [Release closeout](development/RELEASE-068-210-CLOSEOUT-20261017.md)
