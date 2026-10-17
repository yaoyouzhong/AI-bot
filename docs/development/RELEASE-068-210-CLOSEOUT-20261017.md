# Windows 0.6.8 / TAB5 210 发布收尾

2026-10-17 按用户授权正式发布，Codex 会员继续显示 Pro。Mac 无功能改动，保留 0.6.7；ESP8266 0.5.3 和现有图库不重发。

## 发布身份

- 发布提交：`68382bf73b1273062d74a03d1e51778609ab0887`；带注释标签 `bridge-v0.6.8`、`tab5-v0.2.210-ui` 指向同一提交。
- [Windows 正式版](https://github.com/yaoyouzhong/AI-bot/releases/tag/bridge-v0.6.8)为 Latest；[TAB5 正式版](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.210-ui)为独立稳定版。
- [发布 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/39004611973)和[桥接候选流水线](https://github.com/yaoyouzhong/AI-bot/actions/runs/39004611921)通过。流水线自动生成的 Mac 候选没有发布，Windows 草稿换为本机已验收字节后公开。
- 文档与媒体提交：`5c4df47e567037e02f891dcea6f73c492e4be573`；[对应 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/39005488912)通过。

## 包与实机

Windows 安装器、便携包及冻结源码沿用验收文件，372 个 Windows 源文件与发布源码一致。安装器 SHA-256：`9b3d7e95dd622002c608762fbafa4febb49b4478fa17c0e651c3a2e2626ac5d9`；旧客户端别名同字节。

TAB5 首刷与升级应用一致，应用 SHA-256：`21e733f4c9f5e0940c4d3a7d5debac29e60051ad7c4bdb6466b48d32c47f4335`；公开源码提交 `dc61c081d409f18978e8496f8915b747901e71e9` 与应用匹配。源码 ZIP SHA-256：`bd4a8d9dc6608781f323b7ed00978e850931f6135ec0b9ba73bc080205d11058`。

沿用[210 真机验收](TAB5-210-ACCEPTANCE-20261017.md)：精确启动版本、`ota_1 VALID`、日程操作、9.609 秒写入、75,087 字节内部空闲／32,768 字节最大连续块，以及 USB 持续连接时重启电量自动恢复。发布没有重新刷机或改动用户展示设置。

## 文档、媒体与公开核验

- 中英文主页／下载中心、安装手册与旧安装入口、更新说明、功能图鉴、组件版本、源码入口和 Release 说明同步；按实际附件分别展示 Windows 0.6.8、Mac 0.6.7、TAB5 210。
- 刷新相关原生截图及 108 秒／3,240 帧视频、三种封面、可复现工程与媒体校验清单。视频 SHA-256：`7aa9cfa4d53002d1c9375ae36c2c3b6fab3b6eebb5f8413161630af702c57e13`。
- 画面复核：216 张 2 fps 联系帧、13 处 10 fps 转场、变化镜头原尺寸帧、三种封面与截图联系表；完整解码、字体／图像加载、确定性定位、音轨结构及 31 个动作落点检查通过。主观试听和网页内实际播放未验，未以文件可达替代播放结论。
- 本次 20 个公开附件均完成本地完整 SHA-256、GitHub 上传 digest／大小比对，以及公开 HEAD 和首尾小 Range 字节比对；每个大附件最多抽取 32 KiB，未重新整包下载，不能称为远端完整下载哈希校验。七类下载与两套高清图库索引验证通过。
- 14 份当前文档的 259 个本地链接检查通过；公开内容及许可检查、下载索引检查和回归、Git 差异检查通过。实际浏览器主页已确认显示新版组合、Pro 和新版视频入口。
- [固定发布清单](../RELEASE-CHECKLIST.md)及项目 `AGENTS.md` 已加入每次发布后同步主页全部介绍、安装手册／说明、截图和宣传视频的必做步骤。

原始本地证据保存在本轮 `artifacts/publish-068-210-20261017/`、`artifacts/product-intro-release-068-210-20261017/` 和既有 210 验收目录；不上传账户资料、设备备份或私人日志。独立环境逐字节复现 BIN、BLE 整包 OTA、满额清理及长期稳定性仍按各自边界处理。

## English

Windows 0.6.8 and TAB5 0.2.210-ui are stable public releases; Mac retains 0.6.7. Published binaries preserve accepted bytes and independent source identities. Release/source CI and the documentation/media CI passed. Twenty assets passed local full hashes, GitHub server digest/size checks, public HEAD and bounded first/last Range comparisons; no full remote re-download was performed. Bilingual home/download pages, installation/update/feature guides, native screenshots, the 108-second video, three covers and reproducible media sources are updated. The public homepage was checked in a browser. Media decode, contact sheets, transitions, dense frames, fonts, deterministic seeking and audio structure/timing passed; subjective listening and in-browser playback remain unverified. Hardware acceptance is separate from synthetic screenshots. Future releases must complete the linked documentation/media closeout checklist.
