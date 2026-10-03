# 产品介绍视频 / Product introduction video

[![双硬件产品介绍封面](assets/product-intro/AI-bot-cover.png)](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/PRODUCT_VIDEO.md) · 54 秒产品介绍（桌宠设计预览）

https://github.com/user-attachments/assets/540c8206-a33f-46e6-aee9-d738f691d45d

[打开新版视频（54 秒）](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/PRODUCT_VIDEO.md) · [下载 MP4](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4)

2026-10-03 按用户选定方案重新制作：中文、1920×1080 横版、30fps、54 秒，沿用 AI-bot 当前白底蓝色设备中心风格，完整介绍 Windows 设备中心、ESP8266 与 M5Stack TAB5，展示 v0.5.0的变化。首页和本页提供 GitHub 视频播放器，点击封面或“打开视频”可进入新版视频，同时保留 MP4 下载。Release 下载区只保留安装和固件所需文件，不复用旧 36 秒附件。

## 内容与来源

- 0–6 秒两种硬件；6–12 秒设备中心；12–18 秒任务状态；18–24 秒账户额度；24–30 秒 TAB5 查看与语音；30–36 秒天气/音乐/系统；36–42 秒两种刷机路线；42–48 秒桌宠与日历；48–54 秒产品定位与版本范围。
- 封面与 0–6 秒开场并排展示 ESP8266 Codex 功能页和 TAB5 Codex 额度页，演示数据统一为 PRO、本周已用 88%、4 张重置卡；左侧由原生截图工具的 `--codex-pro-cover` 生成，不使用任务总览页代替。
- 当前 WinForms 窗口、镜像和 TAB5 LVGL 预览使用固定演示数据。原生界面直接嵌入、局部裁切放大，没有重绘业务控件；来源和生成方式见[截图说明](SCREENSHOTS.md)。WinForms/LVGL 无法导入 React，浏览器工程负责外层排版和动效。画面不是硬件实拍，也不是新一轮真机验收。
- 语音恢复镜头保留“本段可能不完整”的真实提示，发送仍需用户操作；未宣称断连已修复。首刷与升级镜头区分 TAB5 完整 ZIP 和应用 BIN，未宣称首刷恢复已实测。发布记录见[发布记录](RELEASE-0.5.0.md)。
- 视频品牌图及桌宠已更新为选定的蓝紫尖顶形象，包含呼吸、键盘动作与墨镜反光循环；仅为素材设计预览，已安装程序图标、默认桌宠与固件保持现状。素材来源及 Codex 标识说明见 [桌宠素材](assets/pet/README.md)。不含私人动画、真实账户、凭据或会话。TAB5 预览所含服务标识保留相邻固件的来源说明。
- 英文 Inter、中文 Noto Sans SC，均为 SIL OFL；成片为字体渲染，不另附字体。可复现的本地工程保留字体许可。
- 本片配乐由 `score.py` 原创合成，104 BPM；独立动作音效使用[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill) 的原创合成 WAV。没有使用参考片音轨或下载第三方音乐。
- 制作脚手架为上述技能的 AGPL-3.0 工程，源码交付保留 LICENSE / NOTICE；未使用其 BSL 默认样式。视频工程在本地 `artifacts/product-intro-mascot-20261003/`，另附可复现 ZIP；不把工具许可自动套用于全部产品素材。
- 全片 2 fps 联系表、全部转场 10 fps 条带和信息密集镜头全尺寸帧已实际审阅。导出为 H.264 / AAC、48 kHz 双声道、MP4 faststart；媒体检查和交付结构检查通过。配乐与音效分别保留音轨及哈希；测得约 -16.0 LUFS、真峰值 -1.5 dBFS。**未完成实际听感确认**。
- 视频与封面的精确 SHA-256 记录在 `licenses/materials.json`；公开内容检查只接受明确路径与对应字节。

## English

The video branding and animated pet now use the selected periwinkle mascot as a media design preview; installed application icons, pet defaults and firmware remain unchanged. The rebuilt 54-second overview uses Chinese captions at 1920×1080, 30 fps. It covers current Windows Device Center, ESP8266 and TAB5, including v0.5.0 changes, AI status/quotas, TAB5 voice interaction, daily information, separate flashing routes, the selected mascot design preview and calendar. The cover and opening pair the ESP8266 Codex page with the TAB5 Codex quota page, using matching synthetic PRO, 88% weekly usage and four reset credits; the small-screen image is rendered natively with --codex-pro-cover. It does not demonstrate macOS runtime. Current native WinForms/LVGL pixels use synthetic fixtures; they are not hardware footage or fresh device acceptance. Music is composed in code for this film, with independent original synthesized SFX from the credited skill. Inter and Noto Sans SC use SIL OFL; the reproducible project retains applicable notices. Contact sheets, every cut, dense full-size frames, duration/codec/audio structure and loudness were checked. Subjective listening remains unverified. The README and this page provide a native GitHub video player, with cover and text links opening the public playback page for the same 54-second video. A separate MP4 download remains available. The video is stored in the repository rather than duplicated as an installation asset; exact media hashes are enforced in `licenses/materials.json`.

## 历史附件

[2026-09-19 的旧 36 秒 GitHub 附件](https://github.com/user-attachments/assets/dc69b524-0c0e-46cb-b92a-83d794968ccf)仅作为历史记录，不是本次 54 秒新版。旧片的播放器与网络复核结果也不能用于证明新片的公开播放链路。新版使用独立的 54 秒 GitHub 视频附件；仓库 MP4 作为素材与下载备份。
