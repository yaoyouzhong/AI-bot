# 产品介绍视频 / Product introduction video

本轮 0.6.10 / TAB5 .234 的 116 秒影片正在最终导出和审阅；沿用已批准的方向和声音，更新实际 .234 sidecar。下方 .217 为此前已公开记录。最终结果见 [本轮媒体记录](development/RELEASE-0610-MEDIA-20261019.md)。

The 116-second 0.6.10 / TAB5 .234 revision is being exported and reviewed with the approved direction/audio and actual .234 sidecar. The .217 record below remains the previous published film until publication. See the linked current media record.

2026-10-17 更新：Windows **0.6.9**、TAB5 **0.2.217-ui**、ESP8266 **0.5.3**；Mac 保留 **0.6.7**，不出镜。116 秒中文产品片，1920×1080、30 fps、15 个镜头。新增电脑音量、静音、锁屏状态和带图标的快捷启动，以及十网络管理和实际 217 更新说明；保留双屏额度开场、Codex、时钟、日程、监控与艺术浏览。

https://github.com/user-attachments/assets/f8aefacb-cf94-40c2-9b07-8be8b348a03b

[MP4](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.9/AI-bot-product-intro-20261017-069-217.mp4) · [可复现视频工程](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.9/AI-bot-product-video-source-20261017-069-217.zip) · [媒体校验清单](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.9/VIDEO-SHA256SUMS.txt) · [16:9](assets/product-intro/AI-bot-cover.png) / [4:3](assets/product-intro/AI-bot-cover-4x3.png) / [3:4](assets/product-intro/AI-bot-cover-3x4.png) 封面

## 分镜 / Timeline

| 时间 | 内容 |
| --- | --- |
| 0–8 s | 让 AI 进展，留在桌边；电脑同步状态与额度；小屏常显，大屏触控。 |
| 8–15 s | 不用切回主屏，也知道任务进展；Claude / Codex · 工作状态 · 等待输入 · 完成提醒 |
| 15–21 s | 还剩多少额度，什么时候恢复；账户额度、余额与重置时间，抬眼便知。 |
| 21–31 s | 日常信息，也有自己的位置；天气、负载与网速；汽水音乐的封面和进度同步。 |
| 31–39 s | 顺手控制电脑；音量、静音、锁屏与快捷启动，触手可及。 |
| 39–50 s | 选好会话，说出下一步；最近会话 → 语音草稿 → 检查后发送；返回入口统一。 |
| 50–56 s | 电脑已经读过，屏上也同步；会话未读状态与右上角数字同步；回复和历史继续查看。 |
| 56–65 s | 九款表盘，换一种看时间的方式；经典圆盘、昼夜环与绿色液晶；日出日落随当地数据变化。 |
| 65–72 s | 专注这一刻，也期待重要那一天；时钟计时只显示剩余时间；屏保按天倒数，各自独立。 |
| 72–78 s | 空闲时，把时间变成风景；经典时钟与年度点阵，跟随本地时间自动切换昼夜。 |
| 78–89 s | 在桌面，细看每一件作品；两套高清完整图库；图片、导读与设备展示图一起安装。 |
| 89–95 s | 连接与系统压力，有异常及时知道；运行监控、原因与建议；中断提示和自动恢复。 |
| 95–103 s | 把日程，放在顺手的位置；今日／全部与列表对齐；空闲时居中新建，离线可用。 |
| 103–110 s | 一个入口，管理自己的设备；连接状态、十个已存网络，软件与固件分别更新。 |
| 110–116 s | 把进展留在桌边，把注意力留给工作；从电脑端开始，选择适合你的桌面屏。 |

## 画面、声音与来源

新控制页、静音／锁屏、十网络和实际 217 sidecar 来自独立源码 `d31a601` 的生产 LVGL 原生预览。旧“当前版本 .21”、固定电量、时间、网络名与连接状态是隔离夹具，不是实际设备身份；启动项画面使用通用类型字形，不分发本机提取的软件图标。未变化的 Windows 设备中心及 210 日程、经典时钟画面保留先前正式片来源，当前功能仍相符。Windows 启动项配置截图另由最终 0.6.9 ZIP 内 DLL 的隔离自检生成。完整来源见 [SCREENSHOTS](SCREENSHOTS.md) 和工程内 SOURCES.md、NATIVE-SHA256.json。

画面不是硬件摄影；真实功能与限制见 [0.6.9／217 发布记录](RELEASE-0.6.9.md)。未调用私人账号、麦克风、设备写入或发送消息。Mac 运行态、Apple Music 订阅播放和浏览器高清封面扩展不在影片中。

高清阅读仍为米芾《行书虹县诗卷》，[东京国立博物馆／ColBase TB-1459](https://colbase.nich.go.jp/collection_items/tnm/TB-1459?locale=zh)，[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)，原生界面内缩放与裁切，馆方不为本项目背书。项目原创角色也用于虚构示例专辑图；无商业专辑封面或音乐。开场与三比例封面均对比相同 Codex Pro 周额度 88%／5d 14h。

原创 104 BPM 配乐重新编排至 116 秒，35 个合成动作音效与动作落点一致，音乐在音效时让位。脚手架／混音来自 [guizang-product-video-skill](https://github.com/op7418/guizang-product-video-skill)，工程保留 AGPL-3.0 LICENSE／NOTICE；字体 Inter、Noto Sans SC 保留 OFL。未使用 BSL 默认素材。

## 检查与复现

工程 `product-intro-release-069-217-20261017` 保留分镜、固定原生像素、原创音频、渲染脚本、字体及许可；公开 ZIP 不含 node_modules、工具链、设备二进制、私人配置和日志，无需下载完整图库即可重新导出。编码器差异可能影响 MP4 字节，校验清单标识本次已审阅导出。

已完成完整音视频解码、3,480 帧结构、232 张 2 fps 联系帧、14 处 10 fps 切点、密集原尺寸画面与三比例封面审查；字体／图像加载、正反定位一致和混音落点／电平检查通过。主混音 -16 LUFS／-1.5 dBTP。静止比例提示已人工复核：保留阅读停留与真实状态变化，不为指标增加装饰动作。**主观试听未验**。

本次新影片已重新上传并保存为 GitHub 附件；公开主页实际播放、暂停、继续、进度跳转及到达结尾均通过，证据另记于[发布收尾](development/RELEASE-069-217-CLOSEOUT-20261017.md)。MP4 为 9,098,411 字节，SHA-256 `4f4a99b33a77738a6e65a6905bf931e44e4e67ad51b1dc25b555fe84301bac07`。全部媒体摘要登记在 `licenses/materials.json`。

## English

This 116-second Chinese tour covers Windows 0.6.9, TAB5 217 and ESP8266 0.5.3; Mac remains 0.6.7 and is not shown. Fifteen scenes add PC volume/mute/lock, real lock state, launchers with icons, ten-network management and the actual 217 notes. Native production fixtures are separate from hardware acceptance. Unchanged footage retains its prior provenance. Opening and three covers compare the same Codex Pro quota.

Mi Fu / Tokyo National Museum / ColBase TB-1459 retains CC BY 4.0 and crop/scaling credit. Original 104 BPM music and 35 synthesized cues retain AGPL/OFL and original notices; no commercial music is added. Full decode, contact sheets, cuts, dense frames, covers, fonts/images, deterministic seek, mix timing and levels passed. Subjective listening remains unverified. The new attachment uses the current MP4 bytes; public native-player checks passed for play, pause/resume, seeking and reaching the end, with evidence in the release closeout. Reproduction source and checksums accompany the bridge release.
