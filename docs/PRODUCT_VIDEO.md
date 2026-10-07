# 产品介绍视频 / Product introduction video

https://github.com/user-attachments/assets/2278d971-7792-4377-a365-29d070240055

[下载新版 MP4（126 秒）](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [截图、作品与素材来源](SCREENSHOTS.md)

2026-10-07 修订版：中文、1920×1080、30 fps、126 秒。对应电脑端 0.6.0、ESP8266 0.5.0 与 TAB5 0.2.145-ui。主页与本页使用 GitHub 原生播放器，可直接播放。附件与仓库 MP4 的 SHA-256 一致，均为本次 126 秒新版。

## 内容与来源

- 新版开场展示 Codex 直达和艺术竖屏，随后介绍统一更新。保留双硬件额度、DeepSeek/智谱余额、任务状态、天气、行情、系统、音乐等基础功能，再展示语音/相机附件、四种时钟、日历、设备中心、全年点阵（三种配色及自动昼夜说明）、十二月历、名画/书法横竖屏和独立图库导入。
- Windows 和 TAB5 界面均重新从当前原生 WinForms/LVGL 代码渲染，使用固定演示数据；浏览器只负责外层排版与动效，不重画业务控件。画面不是硬件实拍，不展示 macOS 运行态，也不代替[真机验收](TAB5-ACCEPTANCE-145.md)。
- 更新窗口模拟可用版本，图库窗口模拟已安装的 402 件名画/411 件书法状态，不代表影片拍摄时另做了真实升级或导入。语音/相机镜头运行真实原生按钮处理和离线返回分支，不调用语音服务或发送消息。
- 桌宠使用已批准的项目蓝紫角色设计预览，默认形象不随宣传素材变更。相机取景和音乐封面使用同一原创角色；虚构曲目为“桌面之光 · 示例曲目 / AI-bot 演示”。不含真实账户、凭据或私人会话。
- 作品包括林良《孔雀竹石圖》（Cleveland Museum of Art，CC0）和王嗣奭《行书七言律诗轴》（东京国立博物馆 / ColBase，CC BY 4.0）。书法也用于封面。逐项链接、署名与加工说明见[作品来源表](SCREENSHOTS.md#tab5-捕获与艺术作品)。
- 配乐由 `score.py` 原创合成，104 BPM，24 个独立动作音效来自[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill)的原创合成 WAV，无第三方歌曲。Inter 与 Noto Sans SC 为 SIL OFL。制作脚手架保留 AGPL-3.0 LICENSE/NOTICE，未使用 BSL 默认样式。
- 可复现工程：本地 `artifacts/product-intro-release-060-20261007/`；源码归档 `artifacts/product-intro-release-060-20261007/AI-bot-060-video-source.zip`。包含分镜、界面像素、音频、脚本与许可，不包含 node_modules、运行日志或个人数据；这些本地文件不是安装包或固件包。

## 分镜 / Timeline

| 时间 / Time | 内容 / Content |
| --- | --- |
| 0–6 s | 桌面 AI，触手可及 / AI-BOT |
| 6–14 s | 选中会话，说完再发送 / CODEX DIRECT |
| 14–22 s | 软件与固件，各自更新 / ONE UPDATE ENTRY |
| 22–26.5 s | 同一份额度，两种查看方式 / ACCOUNT QUOTAS |
| 26.5–31 s | DeepSeek 余额，两块屏同步看 / DEEPSEEK · API BALANCE |
| 31–35.5 s | 智谱 GLM，额度同样一目了然 / ZHIPU · API BALANCE |
| 35.5–40 s | 任务进行到哪，抬眼便知 / AI ACTIVITY |
| 40–44.5 s | 今天的天气，两块屏都能看 / WEATHER |
| 44.5–49 s | 关注的行情，随时看一眼 / MARKET WATCH |
| 49–53.5 s | 电脑忙不忙，网络快不快 / PC MONITOR |
| 53.5–58 s | 正在听的歌，也在桌面上 / NOW PLAYING |
| 58–64 s | 离开主屏，也能阅读任务回复 / TAB5 · TASK HISTORY |
| 64–76 s | 支持豆包语音输入 / 拍照添加附件 / TAB5 · VOICE & PHOTO |
| 76–82 s | 多种时钟样式，随心切换 / TAB5 · CLOCK STYLES |
| 82–88 s | 日期、节假日与生日，一起记住 / TAB5 · CALENDAR |
| 88–94 s | 两种设备，一个管理入口 / DEVICE CENTER |
| 94–100 s | 把一整年，放进一屏 / A YEAR IN DOTS |
| 100–106 s | 十二个月，十二种花卉 / TWELVE FLOWERS |
| 106–116 s | 横着看，竖着赏 / ROTATE TO APPRECIATE |
| 116–120 s | 喜欢哪一类，就下载哪一包 / OPTIONAL COLLECTIONS |
| 120–126 s | 选一块适合你的桌面屏 / AI-BOT |

## 交付检查 / Delivery checks

全片 2 fps 联系表（252 帧）、20 个转场的 10 fps 条带及信息密集镜头原尺寸画面已审阅，三种比例封面已检查。正向/反向定位画面逐像素一致；字体、图片与标题溢出检查通过。最终 MP4 为 H.264 / AAC、48 kHz 双声道、126 秒、3,780 帧，完整解码、faststart 与交付结构检查通过。混音约 -16 LUFS、真峰值 -1.4 dBFS；**尚未完成主观试听**。视频及公开封面的 SHA-256 登记于 `licenses/materials.json`。

## English

The revised film is 126 seconds at 1920×1080, 30 fps with Chinese captions, covering computer apps 0.6.0, ESP8266 0.5.0 and TAB5 0.2.145-ui. It leads with Codex Direct and unified updates, retains the dual-device feature tour, and adds Annual Dots (including color and day/night options), the floral calendar, landscape/portrait art and optional collection imports. The homepage and this page embed a native GitHub player. Its attachment matches the repository MP4 by SHA-256; both contain the current 126-second film.

All Windows and TAB5 interfaces were regenerated from current native code using isolated synthetic data. They are neither hardware footage nor evidence of live speech, message sending, installations or imports. No macOS runtime is shown. Voice/camera fixtures exercise native handlers with offline results. The project mascot supplies fictional camera and album artwork; installed defaults remain unchanged. Art credits and CC0/CC BY 4.0 licenses are recorded in SCREENSHOTS.md.

The original 104 BPM score has 24 independent synthesized action cues. Font, scaffold and skill notices are retained. The reproducible local project and source archive are listed above. The 252-frame full-film contact review, all 20 transition strips, dense frames and three cover ratios were visually checked. Forward/backward seeking matches exactly. The 126-second, 3,780-frame H.264/AAC export passed full decoding, faststart, font/image/overflow and delivery checks. Audio is 48 kHz stereo at approximately -16 LUFS and -1.4 dBFS true peak. Subjective listening remains unverified. Exact public media hashes are registered in licenses/materials.json.
