# 产品介绍视频 / Product introduction video

2026-10-14 更新：对应桥接 0.6.6、TAB5 0.2.194-ui；ESP8266 沿用 0.5.2。108 秒、1920×1080、30 fps、14 个镜头。本轮更新运行监控、TF 离线图库、名画横竖屏布局与升级说明；九款表盘、倒计时、Codex 直达及已读同步保留。开场和三种封面保持两端 Codex 周额度的同功能对比。名画采用本次 2026.10.14.1 布局，书法沿用已有正式内容；不发布整理中的新书法集。

The 2026-10-14 film reflects bridge 0.6.6 / TAB5 0.2.194-ui, retaining ESP8266 0.5.2. Monitoring, TF storage, landscape artwork captions and update notes are refreshed; clocks, timers and Codex flows remain. Opening and covers compare the same quota function on both devices. Published painting layout and existing calligraphy are shown; unpublished editorial work is excluded.

https://github.com/user-attachments/assets/4b9e1321-ccf9-4035-89e0-4ae690ca19b6

[下载 MP4（108 秒）](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [横版封面](assets/product-intro/AI-bot-cover.png) · [4:3 封面](assets/product-intro/AI-bot-cover-4x3.png) · [3:4 封面](assets/product-intro/AI-bot-cover-3x4.png) · [素材与作品来源](SCREENSHOTS.md)

## 分镜 / Timeline

| 时间 | 内容 |
| --- | --- |
| 0–8 s | 让 AI 进展，留在桌边；电脑同步状态与额度；小屏常显，大屏触控。 |
| 8–15 s | 不用切回主屏，也知道任务进展；Claude / Codex · 工作状态 · 等待输入 · 完成提醒 |
| 15–21 s | 还剩多少额度，什么时候恢复；账户额度、余额与重置时间，抬眼便知。 |
| 21–31 s | 日常信息，也有自己的位置；天气、负载与网速；汽水音乐的封面和进度同步。 |
| 31–42 s | 选好会话，说出下一步；最近会话 → 语音草稿 → 检查后发送；返回入口统一。 |
| 42–48 s | 电脑已经读过，屏上也同步；会话未读状态与右上角数字同步；回复和历史继续查看。 |
| 48–57 s | 九款表盘，换一种看时间的方式；经典圆盘、昼夜环与绿色液晶；日出日落随当地数据变化。 |
| 57–64 s | 专注这一刻，也期待重要那一天；时钟计时只显示剩余时间；屏保按天倒数，各自独立。 |
| 64–70 s | 空闲时，把时间变成风景；全年点阵、自动昼夜配色与十二月花卉台历。 |
| 70–81 s | 横着看，竖着细细欣赏；已有名画与书法随设备转向，作品与馆藏信息同屏。 |
| 81–87 s | 运行监控；Windows 与 TAB5 同步异常、原因及建议。 |
| 87–95 s | 主动选择 TF 卡保存整套图库；保存完成后离线欣赏，横屏信息在图画左侧。 |
| 95–102 s | 电脑升级入口与本版真实 sidecar 更新说明；完成后核验启动，保留设置与图库。 |
| 102–108 s | 把进展留在桌边，把注意力留给工作；从电脑端开始，选择适合你的桌面屏。 |

## 画面与来源

- Windows 使用生产 WinForms 控件和当前正式版本元数据的隔离夹具；更新、设备和图库窗口使用“已安装／可用更新”宣传占位。没有下载、安装、导入新图库或连接真实账户。
- TAB5 新增和变化镜头来自冻结 .194 源码的原生 LVGL 捕获，源提交 `69cf5de8830059690f2ff336fdeb1323276fe43a`。已读映射驱动实际通知渲染；25 分钟计时和音乐进度由原生逻辑生成逐秒状态。语音、回填、发送界面采用远端桩，未发送任何消息。无实机摄影，也不替代[本版设备验收范围](RELEASE-0.6.6.md)。
- 未变化的额度、全年点阵、花卉和书法镜头保留原有原生来源记录。影片展示代表性场景，不穷举所有功能；macOS 运行态未展示，Apple Music 订阅播放验收仍待完成。
- 名画仍为林良《孔雀竹石圖》（Cleveland Museum of Art，CC0），书法为王羲之《快雪时晴帖》（传唐摹本，台北国立故宫博物院，正式包保留 Public domain 来源记录）。不新增作品或未发布导读。完整链接、署名与加工说明见[作品来源](SCREENSHOTS.md#tab5-捕获与艺术作品)。
- 蓝紫桌宠、封面和音乐的示例专辑图沿用已批准的项目角色；“桌面之光 · 示例曲目”为虚构音乐数据，汽水来源标签用于说明支持能力，不使用真实商业专辑图片。宣传素材不更改用户默认桌宠。
- 开场和三种封面恢复大小屏同功能对比：两端均展示 Codex Pro 周额度 88%、距重置 5d 14h；昼夜环保留在表盘专属镜头。16:9 为第 0 帧完整画面，4:3 与 3:4 各自重排；不显示组件版本号。当前视频附件已重新上传并核验字节；仅版本递增无需重做影片，功能或界面发生实质变化时刷新相关镜头。

## 声音、工程与验收

沿用 `score.py` 原创 104 BPM 配乐，29 个动作音效按本次分镜重新对位并混音。音效来自[归藏 product video skill](https://github.com/op7418/guizang-product-video-skill)的原创合成 WAV；制作脚手架和混音脚本保留 AGPL-3.0 LICENSE／NOTICE，未使用 BSL 默认样式。Inter 和 Noto Sans SC 保留 SIL OFL 许可。没有第三方歌曲。

修正可复现工程：`artifacts/product-intro-release-066-194-20261014/`；源码归档：`AI-bot-product-video-source-20261014.zip`。原始 `product-intro-release-20261011` 工程和归档保留。包含分镜、真实界面像素、原生捕获夹具、配乐／音效、混音和导出脚本、字体与许可；不含 node_modules、运行配置、私人账户、日志或新图库内容。历史 20261009 工程继续保留。

成片检查覆盖正反向定位、字体／图片加载、标题和界面边界、完整解码、2 fps 全片联系表、13 处 10 fps 转场、密集画面原尺寸复核，以及三种封面和 320 px 缩略图。H.264／AAC、48 kHz 双声道，108 秒／3,240 帧；精确媒体 SHA-256 登记于 `licenses/materials.json`。音效落点和混音电平检查通过；**主观试听未完成**，不将自动检查写成听感验收。

## English

The film retains the approved product palette, overview-first narrative, 108-second duration and original score. Current changed features use isolated native WinForms/LVGL interfaces. Actual native read maps clear an unread count, the 25-minute timer decreases, and synthetic Soda progress advances. Voice/desktop stubs submit no messages; none of this is new hardware acceptance. Unchanged annual/calendar/art footage retains earlier provenance. macOS runtime and subscribed Apple Music playback are not shown. Artwork remains the previously published Lin Liang and attributed Tang-copy Wang Xizhi selections, with complete museum attribution in SCREENSHOTS.md.

The opening and three cover ratios compare Codex Pro weekly quota on both devices (88% used, 5d 14h to reset); the solar ring remains in its clock scene; the 16:9 poster is the complete clean first frame. The original 104 BPM score is mixed with 29 retimed synthesized action cues. AGPL scaffold/mixer, original SFX notices and OFL fonts remain in the reproducible local project/source archive. Deterministic seeking, fonts/images, layout, full decoding, 216 contact frames, all 13 transitions and three covers are reviewed separately. The export is H.264/AAC, 48 kHz stereo, 108 seconds / 3,240 frames. Exact hashes are registered. Subjective listening remains unverified.

## 历史记录

2026-10-07 原片建立“整体用途 → 日常场景 → Codex／屏保 → 安装更新”的叙事。2026-10-08 将开场改为第 0 帧完整显示双设备，去除版本号，并更正 Pro 周额度示例。2026-10-09 刷新 .156 日常页面和正式 366 + 366 精选图库；旧片采用 13 个镜头和 23 个音效。上述历史文件和来源留在对应本地工程，不代表当前成片。

本次新增监控界面使用已验收 F6 DLL 的原生 WinForms 捕获；TF 设置、运行监控和升级说明来自 .194 原生 LVGL。升级页的“当前版本 .21”是旧版演示夹具，待升级版本与说明来自真实 .194 sidecar，不作为实机版本证据。TF 设置示例保持默认设备缓存，说明主动选择后的行为。名画使用正式 ZIP 中 cma:1964.242 的实际 JPEG，不用文字图层覆盖界面。制作工程公开源码附于桥接 Release，保留 AGPL 脚手架与 OFL 字体许可。

New monitor captures use the accepted F6 DLL; TAB5 monitoring, storage and actual update notes use native .194 LVGL. The old installed-version label is fixture data, while offered notes come from the real .194 sidecar. Storage shows the default cache and explains explicit TF opt-in. Exact painting pixels come from the reviewed pack. The reproducible video source archive retains AGPL/OFL materials and is included in the bridge release.
