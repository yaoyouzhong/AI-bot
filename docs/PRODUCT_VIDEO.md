# 产品介绍视频 / Product introduction video

[![双硬件产品介绍封面](assets/product-intro/AI-bot-cover.png)](assets/product-intro/AI-bot-product-intro.mp4)

[下载新版 MP4（54 秒）](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AI-bot-product-intro-0.5.0.mp4) · [仓库内视频](assets/product-intro/AI-bot-product-intro.mp4)

2026-10-03 按用户选定方案重新制作：中文、1920×1080 横版、30fps、54 秒，沿用 AI-bot 当前白底蓝色设备中心风格，完整介绍 Windows 设备中心、ESP8266 与 M5Stack TAB5，展示 v0.5.0 预发布版的变化。新版随 v0.5.0 Release 附件提供；README 同时链接仓库内视频，不复用旧 36 秒附件。

## 内容与来源

- 0–6 秒两种硬件；6–12 秒设备中心；12–18 秒任务状态；18–24 秒账户额度；24–30 秒 TAB5 查看与语音；30–36 秒天气/音乐/系统；36–42 秒两种刷机路线；42–48 秒桌宠与日历；48–54 秒产品定位与版本范围。
- 当前 WinForms 窗口、镜像和 TAB5 LVGL 预览使用固定演示数据。原生界面直接嵌入、局部裁切放大，没有重绘业务控件；来源和生成方式见[截图说明](SCREENSHOTS.md)。WinForms/LVGL 无法导入 React，浏览器工程负责外层排版和动效。画面不是硬件实拍，也不是新一轮真机验收。
- 语音恢复镜头保留“本段可能不完整”的真实提示，发送仍需用户操作；未宣称断连已修复。首刷与升级镜头区分 TAB5 完整 ZIP 和应用 BIN，未宣称首刷恢复已实测。候选限制见[发布记录](RELEASE-0.5.0.md)。
- 项目图标、原创 BYTE SPROUT 桌宠；不含私人动画、真实账户、凭据或会话。TAB5 预览所含服务标识保留相邻固件的来源说明。
- 英文 Inter、中文 Noto Sans SC，均为 SIL OFL；成片为字体渲染，不另附字体。可复现的本地工程保留字体许可。
- 本片配乐由 `score.py` 原创合成，104 BPM；独立动作音效使用[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill) 的原创合成 WAV。没有使用参考片音轨或下载第三方音乐。
- 制作脚手架为上述技能的 AGPL-3.0 工程，源码交付保留 LICENSE / NOTICE；未使用其 BSL 默认样式。视频工程在本地 `artifacts/product-intro-0.5.0/`，另附可复现 ZIP；不把工具许可自动套用于全部产品素材。
- 全片 2 fps 联系表、全部转场 10 fps 条带和信息密集镜头全尺寸帧已实际审阅。导出为 H.264 / AAC、48 kHz 双声道、MP4 faststart；媒体检查和交付结构检查通过。配乐与音效分别保留音轨及哈希；测得约 -16.0 LUFS、真峰值 -1.5 dBFS。**未完成实际听感确认**。
- 视频与封面的精确 SHA-256 记录在 `licenses/materials.json`；公开内容检查只接受明确路径与对应字节。

## English

The rebuilt 54-second overview uses Chinese captions at 1920×1080, 30 fps. It covers current Windows Device Center, ESP8266 and TAB5, including v0.5.0 pre-release changes, AI status/quotas, TAB5 voice interaction, daily information, separate flashing routes, the original pet and calendar. It does not demonstrate macOS runtime. Current native WinForms/LVGL pixels use synthetic fixtures; they are not hardware footage or fresh device acceptance. Music is composed in code for this film, with independent original synthesized SFX from the credited skill. Inter and Noto Sans SC use SIL OFL; the reproducible project retains applicable notices. Contact sheets, every cut, dense full-size frames, duration/codec/audio structure and loudness were checked. Subjective listening remains unverified. The new MP4 accompanies the v0.5.0 Release, with the repository copy also linked; exact media hashes are enforced in `licenses/materials.json`.

## 历史附件

[2026-09-19 的旧 36 秒 GitHub 附件](https://github.com/user-attachments/assets/dc69b524-0c0e-46cb-b92a-83d794968ccf)仅作为历史记录，不是本次 54 秒新版。旧片的播放器与网络复核结果也不能用于证明新片的公开播放链路。公开发布与附件上传后需另行核验实际页面及播放。
