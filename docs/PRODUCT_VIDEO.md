# 产品介绍视频 / Product introduction video

2026-10-17 更新：Windows **0.6.8**、TAB5 **0.2.210-ui**（Mac 保留 **0.6.7**，不出镜）、ESP8266 **0.5.3**。沿用已批准的 108 秒中文产品片，1920×1080、30 fps、14 个镜头。本轮刷新经典时钟昼夜配色、日程空状态／新首页／到点提醒和设备管理／真实 210 更新说明；高清作品浏览与原文阅读沿用原生素材；保留同功能双屏额度开场、Codex、表盘和日历镜头。

[![打开新版产品介绍](assets/product-intro/AI-bot-cover.png)](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.8/AI-bot-product-intro-20261017.mp4)

[新版 MP4](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.8/AI-bot-product-intro-20261017.mp4) · [可复现视频工程](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.8/AI-bot-product-video-source-20261017.zip) · [媒体校验清单](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.8/VIDEO-SHA256SUMS.txt) · [16:9](assets/product-intro/AI-bot-cover.png) / [4:3](assets/product-intro/AI-bot-cover-4x3.png) / [3:4](assets/product-intro/AI-bot-cover-3x4.png) 封面

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
| 64–70 s | 空闲时，把时间变成风景；经典时钟与年度点阵，跟随本地时间自动切换昼夜。 |
| 70–81 s | 在桌面，细看每一件作品；两套高清完整图库；图片、导读与设备展示图一起安装。 |
| 81–87 s | 连接与系统压力，有异常及时知道；运行监控、原因与建议；中断提示和自动恢复。 |
| 87–95 s | 把日程，放在顺手的位置；居中空状态、今日／全部分段筛选、列表详情与到点提醒。 |
| 95–102 s | 一个入口，管理自己的设备；连接状态看得明白；软件、固件与图库按需更新。 |
| 102–108 s | 把进展留在桌边，把注意力留给工作；从电脑端开始，选择适合你的桌面屏。 |

## 画面与来源

Windows 新镜头来自正式 0.6.8 源码编译的隔离 WinForms；日程、经典时钟昼夜和 210 实际 sidecar 来自已验收源码的原生 LVGL。两套实际高清完整包分别隔离导入，各 366 件，所有标准分页读取解码与高清浏览、全屏、缩放、阅读通过。视频中的阅读栏与管理详情采用原生捕获局部裁切，未重画或覆盖业务控件。公开截图保留完整窗口。更新页较旧的“当前版本 .21”是离线夹具，待升级版本与说明来自实际 210 sidecar。

新高清阅读镜头为米芾《行书虹县诗卷》，[东京国立博物馆／ColBase TB-1459](https://colbase.nich.go.jp/collection_items/tnm/TB-1459?locale=zh)，[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)，按原生界面缩放、裁切。完整资料与许可随高清包保留。未变化的表盘、花卉、额度、监控、Codex 与示例音乐镜头沿用[原生来源记录](SCREENSHOTS.md)；项目原创蓝紫角色用于桌宠和虚构专辑封面，不加入商业音乐或真实账户数据，馆方未为本项目背书。

开场及三种封面均为两端相同 Codex Pro 周额度（88%、距重置 5d 14h），封面刷新为日程与艺术用途。Mac 运行态、Apple Music 订阅播放未展示；没有设备写入、硬件摄影、真实语音服务调用或消息发送。浏览器高清音乐封面扩展不集成。原生捕获不替代[210 真机验收记录](development/TAB5-210-ACCEPTANCE-20261017.md)。

## 声音、工程与检查

沿用 `score.py` 原创 104 BPM 配乐，本轮按原有 31 个动作落点重新混音，保留音乐让位与独立动作音效。合成音效和混音脚手架来自[guizang-product-video-skill](https://github.com/op7418/guizang-product-video-skill)，保留 AGPL-3.0 LICENSE／NOTICE；未使用 BSL 默认样式。Inter、Noto Sans SC 保留 OFL 许可。没有第三方歌曲。

源工程 `product-intro-release-068-210-20261017`；公开 ZIP 包含分镜、固定原生像素、源码、音效、配乐、导出脚本、字体与许可，可从像素重新导出。无需下载整个高清图库；不含 node_modules、设备二进制、完整图库、私人配置、账户或日志。此前 20261014 工程及来源记录保留。

已检查：完整音视频解码、3,240 帧时长／编码结构、216 张 2 fps 联系帧、13 处 10 fps 转场、变化镜头的原尺寸帧、三种封面、字体与图像加载、确定性定位，以及音效时序和电平。媒体 SHA-256 登记在 `licenses/materials.json`，Release 附独立校验清单。**主观试听未验**；音轨结构、电平和动作落点单独检查。主页通过封面链接提供新版 MP4；公开播放检查见本次收尾记录。

## English

The updated 108-second Chinese tour covers Windows 0.6.8, TAB5 210 and ESP8266 0.5.3; Mac 0.6.7 remains available but is not demonstrated. Updated scenes show automatic day/night classic clocks, the centered agenda empty state and refined list/detail layout, current Windows controls and the actual 210 sidecar. HD artwork/reading footage is retained. Unchanged native footage retains its earlier source record. Opening and all three covers compare the same Codex Pro weekly quota on both displays. Captures use isolated fixtures, not hardware photographs; physical 210 acceptance has a separate record.

Mi Fu's Hongxian Poems is attributed to Tokyo National Museum / ColBase TB-1459, CC BY 4.0; native crops and scaling retain this license. The original 104 BPM score and 31 synthesized action cues retain AGPL/OFL and original sound notices. Source ZIP and media checksums are attached to the bridge release. Full decode, contacts, transitions, dense frames and covers were visually reviewed; audio structure/timing passed. Subjective listening remains unverified. Audio structure, levels and cue timing are checked separately; public playback verification is recorded in this release closeout. macOS runtime and subscribed Apple Music playback are outside this film.
