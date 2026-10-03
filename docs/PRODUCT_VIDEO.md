# 产品介绍视频 / Product introduction video

[![AI-bot 96 秒产品介绍封面](assets/product-intro/AI-bot-cover.png)](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4)

https://github.com/user-attachments/assets/31393f58-edd9-4a50-a81f-0e8d0ff72a80

[下载新版 MP4（96 秒）](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [截图与素材来源](SCREENSHOTS.md)

2026-10-03 修订版：中文、1920×1080 横版、30fps、96 秒。中英文首页及本页均可通过 GitHub 内嵌播放器直接播放新版视频，下载入口提供同一版本的仓库 MP4。

## 内容与来源

- 开场采用蓝紫机器人品牌封面，结尾为“选一块适合你的桌面屏”。相同功能并排展示 ESP8266 和 TAB5：Codex 额度、DeepSeek 余额、智谱余额、任务状态、当前天气、行情、系统监控和音乐。DeepSeek、智谱的演示余额分别为 28.50、16.80 CNY。已移除 TAB5 独有的小时和七日天气镜头。
- 音乐页完整展示左侧专辑封面，两种硬件均使用项目原创角色作为示例封面；曲名“桌面之光 · 示例曲目”和歌手“AI-bot 演示”是虚构演示信息，不是商业录音，也不显示“封面同步中”。
- TAB5 亮点包括任务回复、豆包语音输入、拍照添加附件、四种时钟样式切换、日历提醒及设备中心。语音拍照段依次展示点击麦克风后的收音状态、音量条和识别文字，相机取景与拍摄确认，以及照片缩略图加入草稿。电脑端按透明收音方式呈现，不展示草稿输入框。相机使用项目原创形象作示例取景，不冒充实机摄影。
- 界面来自原生 WinForms/LVGL 渲染，使用固定演示数据；没有重绘业务控件。Windows 与相邻 TAB5 工程的原生组件在隔离预览程序中运行，浏览器工程负责外层排版、镜头与动效。新增语音/相机素材执行原生按钮处理及离线状态分支，没有调用真实语音服务或发送消息。画面不是硬件实拍，也不是新一轮真机验收；不展示 macOS 运行态。
- 品牌图及桌宠使用已选定的蓝紫尖顶形象；仅更新宣传素材，已安装程序图标、默认桌宠与固件保持现状。来源及标识说明见[桌宠素材](assets/pet/README.md)。不含真实账户、凭据或私人会话。
- 配乐由 `score.py` 原创合成，104 BPM；18 个独立动作音效使用[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill)的原创合成 WAV。未使用第三方歌曲。字体 Inter 与 Noto Sans SC 均为 SIL OFL。制作脚手架采用该技能的 AGPL-3.0 工程，源码保留 LICENSE / NOTICE，未使用其 BSL 默认样式。
- 可复现工程位于本地 `artifacts/product-intro-highlights-20261003/`；源码归档为 `artifacts/video-storyboard-20261003/AI-bot-highlights-v4-source.zip`。这些本地产物未作为安装或固件附件发布。
- 全片 2 fps 联系表、全部转场 10 fps 条带，以及语音/取景/拍摄确认/附件等全尺寸帧已审阅。H.264 / AAC、48 kHz 双声道、MP4 faststart；96 秒、2880 帧、完整解码与交付结构检查通过。混音约 -16.0 LUFS、真峰值 -1.5 dBFS；**尚未完成主观试听**。视频和封面的 SHA-256 登记于 `licenses/materials.json`。

## 分镜 / Timeline

| 时间 / Time | 展示内容 / Content |
| --- | --- |
| 0–6 s | 品牌封面 / Brand cover |
| 6–12 s | Codex 额度对照 / Codex quota comparison |
| 12–18 s | DeepSeek 余额对照 / DeepSeek balance comparison |
| 18–24 s | 智谱余额对照 / Zhipu balance comparison |
| 24–30 s | 任务状态对照 / Task status comparison |
| 30–36 s | 当前天气对照 / Current weather comparison |
| 36–42 s | 行情对照 / Market comparison |
| 42–48 s | 系统与网络对照 / System and network comparison |
| 48–54 s | 音乐与专辑封面对照 / Music and album artwork comparison |
| 54–60 s | TAB5 任务回复 / TAB5 task replies |
| 60–72 s | 豆包语音、拍照、附件 / Doubao dictation, camera and attachment |
| 72–78 s | 四种时钟样式切换 / Four clock styles |
| 78–84 s | 日历与提醒 / Calendar and reminders |
| 84–90 s | 设备中心 / Device Center |
| 90–96 s | 品牌结尾 / Closing brand frame |

## English

The revised 96-second film uses Chinese captions at 1920×1080, 30 fps. The cover opens the film, followed by matching ESP8266/TAB5 views of Codex quotas, DeepSeek and Zhipu balances, task activity, current weather, markets, system/network data and music. The two domestic-provider balances are synthetic 28.50 and 16.80 CNY. TAB5-only hourly and seven-day forecasts have been removed. Both music views show the approved project mascot as sample album artwork with a fictional track; no artwork-sync placeholder remains.

TAB5 highlights include task replies, Doubao dictation, camera attachments, four switchable clock styles, calendar reminders and Device Center. The 60–72-second sequence shows the native microphone-click recording state, meter and transcript, camera preview/review, and a photo thumbnail added to the draft. The desktop voice editor is omitted to reflect transparent operation. The camera image is an explicitly labeled sample of the project's original character, not real photography.

Native WinForms/LVGL interfaces use offline fixtures and real UI handlers. They do not call the live speech service or send messages. These are rendered previews, not hardware footage or new device acceptance, and no macOS runtime is shown. Installed application icons, default pets and firmware are unchanged. Sources contain no real accounts, credentials or private conversations.

The original 104 BPM code-generated score includes 18 independent synthesized action cues from the credited skill. Inter and Noto Sans SC are SIL OFL; the local reproducible project retains AGPL-3.0 scaffold notices and uses no BSL fallback styling. The project and versioned source ZIP paths are listed above; they are not installation assets. Full-film contact sheets, every cut and dense frames were visually reviewed. The 96-second, 2880-frame H.264/AAC export passed full decoding and delivery checks; the mix measures approximately -16 LUFS and -1.5 dBFS true peak. Subjective listening remains unverified. Exact video and cover hashes are recorded in `licenses/materials.json`.

Both READMEs and this page provide a native GitHub player for the updated 96-second video, with a download link to the identical repository MP4.
