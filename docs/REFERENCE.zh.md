# 功能与开发参考

当前版本：电脑端 **0.6.0**、ESP8266 **0.5.0**、TAB5 **0.2.145-ui**。三者独立编号。[首页](../README.md) · [English](REFERENCE.en.md)

## 使用项目

| 目的 | 当前说明 |
| --- | --- |
| 下载、首刷、配对和升级 | [完整安装指南](INSTALL.zh.md) |
| 当前菜单、页面和功能 | [界面图鉴](FEATURES.zh.md) |
| 更新提醒与适用包 | [软件与固件更新](UPDATES.md) |
| 名画与书法安装 | [可选图库](GALLERY-PACKS.md) |
| 新增功能与验证范围 | [三组件发布说明](RELEASE-0.6.0.md) · [TAB5 .145 验收](TAB5-ACCEPTANCE-145.md) |

Windows 支持 ESP8266 与 TAB5；macOS 13+ Apple Silicon 提供菜单栏、镜像与 ESP8266 功能，不包含 Windows 的 TAB5 服务、统一更新和艺术导入入口。Intel Mac 未验证。Mac 的测试／构建结果不代替 GUI 或设备验收。

## 源码与构建

| 目录／组件 | 资料 |
| --- | --- |
| `windows-app/AIBotBridge/` | .NET 8 Windows 托盘程序；[构建与打包](BUILD_WINDOWS.zh.md) |
| `mac-app/` | 独立 Swift 菜单栏应用；在 macOS 执行 `swift test --package-path mac-app` 和 `swift build -c release --package-path mac-app` |
| `firmware/` | ESP8266 PlatformIO 工程；[构建与刷写](FLASH_BUILD.zh.md) |
| TAB5 | 独立 ESP-IDF 工程的[公开源码与构建快照](development/TAB5-SOURCE.md)，[固件交付流程](TAB5-FIRMWARE-WORKFLOW.md) |

架构、数据源与通信边界见 [DEVELOPMENT](DEVELOPMENT.md)、[数据来源](DATA_SOURCES.md)和[协议](PROTOCOL.md)。USB 波特率为 460800；ESP8266 小消息采用 `@AIBOT ` 前缀的 version 1 JSON，TAB5 有自己的协议服务。不要混用两个设备的固件或刷写流程。

电脑端本地只读状态接口默认 `127.0.0.1:8765/status`，TAB5 服务默认端口为 `18765`；局域网管理需配对认证。本机 Token 元数据与厂商账户额度分别统计。详细[额度历史口径](QUOTA_TRENDS.md)。

## 发布与历史记录

[组件版本规则](COMPONENT-VERSIONS.md)定义各自版本源、标签与更新日志。推送主分支只运行 CI；组件标签进入候选发布流程，正式包另经验证与发布。程序升级不要求两个设备同时升级。

旧的[发布准备记录](RELEASE_READINESS.md)和[重实现对齐合同](FUNCTIONAL_PARITY.md)保留历史过程，不作为当前安装路径或发布状态。当前以本页上方版本、发布说明与对应验收记录为准。

自有源码采用 MIT，第三方字体、组件和图像保留各自条款。[来源记录](../PROVENANCE.md) · [分发条款](DISTRIBUTION_TERMS.md) · [第三方声明](../THIRD_PARTY_NOTICES.md)
