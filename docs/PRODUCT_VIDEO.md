# 产品介绍视频 / Product introduction video

https://github.com/user-attachments/assets/2c6fc353-5958-4780-b087-63999f1bc710

[下载 MP4（108 秒）](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [截图、作品与素材来源](SCREENSHOTS.md)

2026-10-07 修订：先介绍 AI-bot 的整体用途和双硬件关系，再展示日常场景、Codex 直达与屏保亮点，最后说明安装和更新。中文、1920×1080、30 fps、108 秒；对应电脑端 0.6.0、ESP8266 0.5.0 与 TAB5 0.2.145-ui。主页与本页使用 GitHub 原生播放器，附件与仓库 MP4 字节一致。

2026-10-08 修订开场：第 0 帧即完整显示双设备、标题及用途说明，取消首镜主体延迟淡入；无播放控件的首帧同步为 16:9 封面，解决默认只见标题、其余空白的问题。小屏仅展示 WK 周额度，使用批准的新款桌宠；两边均为 88%、距重置 5d 14h。画面、页脚和封面不展示具体版本号，设备中心、更新和图库原生控件使用“已安装／可用更新”等宣传专用占位文字。仅版本递增不要求重做视频；功能或界面实质变化时更新对应镜头。原音轨保持，实际下载以[完整下载中心](../DOWNLOADS.md)为准。

## 分镜 / Timeline

| 时间 | 内容 |
| --- | --- |
| 0–8 s | 整体用途：电脑同步状态，小屏常显、大屏触控 |
| 8–17 s | AI 任务进展、等待输入与完成状态 |
| 17–24 s | 账户额度、余额与恢复时间 |
| 24–33 s | 日常场景：天气、电脑负载、音乐 |
| 33–45 s | Codex 直达：选择会话、语音输入、确认草稿 |
| 45–51 s | 在 TAB5 阅读回复与历史 |
| 51–59 s | 全年点阵、三种配色与自动昼夜 |
| 59–67 s | 十二月花卉台历 |
| 67–81 s | 名画和书法：横竖排版、作品与馆藏信息 |
| 81–87 s | 两类可选图库，按需下载和导入 |
| 87–95 s | 先安装电脑软件，再添加自己的设备 |
| 95–102 s | 统一更新入口，三个组件各自更新 |
| 102–108 s | 回到产品用途与开始使用的步骤 |

## 画面、作品与音频来源

- Windows / TAB5 画面来自当前原生 WinForms / LVGL 代码的隔离渲染，使用固定演示数据。浏览器只编排外层画面和动效，不重画业务控件。不是硬件实拍，不展示 macOS 运行态，也不代替[真机验收](TAB5-ACCEPTANCE-145.md)。完整功能另见[界面图鉴](FEATURES.zh.md)。
- 更新窗口模拟发现可用版本；图库窗口模拟已安装的 402 件名画／411 件书法状态。语音镜头使用原生流程的离线模拟结果，不调用语音服务或发送消息，也不代表拍摄时另做了真实升级或导入。
- 蓝紫桌宠与音乐封面使用已批准的项目角色设计预览；宣传素材不修改已安装程序的默认形象。曲目“桌面之光 · 示例曲目”为虚构数据，不含真实账户或私人会话。
- 艺术作品为林良《孔雀竹石圖》（Cleveland Museum of Art，CC0）和王嗣奭《行书七言律诗轴》（东京国立博物馆 / ColBase，CC BY 4.0）。逐项链接、署名及加工说明见[作品来源表](SCREENSHOTS.md#tab5-捕获与艺术作品)。封面使用设备额度页面。
- 配乐由 `score.py` 原创合成，104 BPM；23 个动作音效使用[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill)的原创合成 WAV，无第三方歌曲。Inter、Noto Sans SC 为 SIL OFL。制作脚手架和混音脚本保留 AGPL-3.0 LICENSE / NOTICE，未使用 BSL 默认样式。

可复现工程保存在本地 `artifacts/product-intro-story-20261007/`，源码归档为该目录下的 `AI-bot-story-video-source-20261008-evergreen.zip`。包含分镜、原生界面像素、配乐／音效、混音与渲染脚本、字体和许可，不包含 node_modules、运行日志或私人数据；它不是应用安装包。

2026-10-08 复核：已检查新 MP4 首帧和 216 帧全片联系表；正反向定位、字体、图片及标题溢出检查通过，完整解码通过。108 秒、3240 帧、H.264/AAC 不变；新旧 AAC 音轨 SHA-256 相同，未新增主观试听结论。三种比例封面已同步复核；原有转场条带的验收记录属于下述历史版本。

## 交付检查（原片记录）

已审阅最终 MP4 的 2 fps 全片联系表（216 帧）、全部 12 个转场的 10 fps 条带、信息密集镜头原尺寸画面和三种封面比例。正反向定位一致，字体、图片、标题溢出检查通过。最终文件为 H.264 / AAC、48 kHz 双声道、108 秒、3,240 帧；完整解码和交付结构检查通过。编码后音频测量为 -16.04 LUFS、真峰值 -1.50 dB；**主观试听尚未完成**。精确媒体哈希登记于 `licenses/materials.json`。

## English

The 108-second film introduces the overall product and device choices first, followed by everyday use, selected Codex Direct/screensaver highlights, then setup and updates. It uses Chinese captions at 1920×1080, 30 fps for computer apps 0.6.0, ESP8266 0.5.0 and TAB5 0.2.145-ui. The native GitHub attachment matches the repository MP4 by SHA-256.

The 2026-10-08 revision shows both devices, the title and purpose labels from frame zero, removing the opening visual delay that left the paused player mostly blank. The clean first frame is also the 16:9 cover; the small screen shows only the weekly quota with the approved new mascot, aligned with TAB5 at 88% and 5d 14h until reset. Film frames, footers and covers omit explicit release numbers. Native device/update/gallery controls use presentation-only installed/available placeholders; runtime controls are unchanged. Version increments alone do not require a new film; material feature or interface changes do. The original audio is preserved. Use the complete download center for current packages.

All interfaces are native WinForms/LVGL captures with synthetic fixtures, not hardware footage or evidence of real speech, message sending, installs or imports. No macOS runtime is shown. The project character and fictional album artwork do not change installed defaults. Lin Liang and Wang Sishi artwork credits, CC0/CC BY 4.0 terms and modifications are recorded in SCREENSHOTS.md. Covers show quota interfaces.

The original 104 BPM score uses 23 synthesized action cues. Font, scaffold and mixer notices are retained in the reproducible local project/archive listed above. All 216 contact frames, 12 transition strips, selected full-size frames and three covers were visually reviewed. Seeking, fonts/images, overflow, full decoding and media structure checks passed. The export contains 3,240 frames with 48 kHz stereo audio measured at -16.04 LUFS and -1.50 dB true peak. Subjective listening remains unverified.

2026-10-08 verification: the new encoded first frame and all 216 contact frames were reviewed; deterministic seeking, fonts, images, overflow and full decoding passed. Duration remains 108 seconds / 3240 frames. The encoded AAC stream has the same SHA-256 as the original; subjective listening remains unverified. All three cover ratios were also reviewed; the transition-strip records above describe the original film.
