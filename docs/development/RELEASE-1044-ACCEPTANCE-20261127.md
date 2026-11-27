# AI-bot v1.0.44 发布验收

[产品发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/v1.0.44)交付 Windows x64 和 Apple Silicon Mac 桥接 0.6.35。两端使用源码 `32fbf674704b993cc1ef9c0e69cf952247e47482`，对应追溯标签 `bridge-v0.6.35`。候选 ID 为 `296f319725f4218b099c4b39a8e2510f6d147842337206c9c24880ead2705cc0`。

| 安装包 | SHA-256 |
| --- | --- |
| Windows x64 安装器 | `df2ac9cd741da998acf8f9f77536a403cb1e8350d6c0861f70bccab12d213fc4` |
| Apple Silicon Mac ZIP | `622769fe7ba33cc55c52320f45a807cddc35548efb4efbe014ee8382a9e58e32` |

- Windows 从公开 0.6.32 隔离覆盖升级，托盘约 17 秒恢复。安装字段、运行程序集、模拟配置、本地资料、加密解锁记录、历史额度和 LAN 权限均核对通过。安装器未签名。
- Mac 原包从公开 0.6.34 完成一键升级和安装启动，配置、配对、草稿及辅助功能权限保留。592 项 Swift 测试中 5 项跳过，0 失败。用户在自己的 Mac 上验证过浏览器首装和后续一键升级，首装按「隐私与安全性」确认打开，升级未再次弹出授权窗口。[Mac 原包与用户实测回填](TAB5-WEATHER-OTA-BRIDGE-HANDOFF-20261127.md#mac-原包安装回填--2026-11-27)
- Mac 固定自签原包未公证，Gatekeeper 自动评估拒绝。办公室 Mac 的原包环境未复测实体 TAB5 天气和 OTA 草稿。真实订阅用量待有账户后核验。用户已接受这些边界并确认发布。
- GitHub 附件大小、服务端摘要和下载入口均已核对。本次沿用现有截图和宣传视频，覆盖其已经展示的设备和界面。套餐统计选择与此次验收范围见双语发布说明和配置指南。
