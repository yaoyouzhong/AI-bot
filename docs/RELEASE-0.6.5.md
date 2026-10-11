# 桥接 0.6.5、TAB5 0.2.176-ui 与 ESP8266 0.5.2

本次发布桥接程序与 TAB5 固件，新增字库随固件交付。名画集、书法集及整理中的简介、欣赏提示和原文不纳入本次发布，继续使用正式精选集 2026.10.09；用户已安装图库保留。ESP8266 同期发布 0.5.2：修复网速数字与曲线的一秒均值对齐及桥接会话变化后的旧曲线清除。

## 主要变化

- **Codex 已读同步**：电脑查看具体回复后，Tab5 同步更新右上角未读数、回复入口及概览；新回复重新提醒。
- **音乐**：新增汽水音乐和 Apple Music 来源识别，沿用曲名、歌手、封面、时长和进度。汽水音乐已在 Tab5 验证暂停、恢复、跳转和连续进度；Apple Music 实际播放待后续验收。
- **时钟**：九款表盘、九宫格选择、经典绿色液晶。昼夜环保留普通 12 小时读时，外环显示当地 24 小时昼夜及当前时间，太阳／月亮标记日出日落；无当天数据时使用默认 06:00／18:00。
- **两类倒计时**：时钟的计时功能通过分段入口切换，运行时只显示大数字，切页继续计时；屏保的按天日期倒计时独立设置，未选中时隐藏配置。
- **导航与展示**：Codex 直达与设置统一左上角返回和靠左标题；轮播、固定页面、自动切换互斥，保留页面、顺序和间隔。供应商图标可视面积统一，Codex 使用 30×29。
- **桥接设置与传输**：统一设备中心及子页面视觉与高 DPI 布局，改善实际额度周期、USB 接收隔离、Wi-Fi／BLE 恢复和不含正文的诊断。
- **图库程序能力**：支持两类独立图文包校验、原子安装、备份、热重载及断点下载。本次标准桥接不嵌入整理中的导读目录；未来内容包可单独更新。

## 字库与升级

新增字形编译进 TAB5 固件，无需单独安装或导入图库。相较 .170 扩字前快照，原有 8,538 个字形全部保留，尺寸和位图不变，本次源字库共 9,013 个字形。图库包不安装字体；后续内容发布仍需对冻结字库检查缺字，出现新生僻字时再补固件。

已有 AI-bot 的 Tab5 使用升级 ZIP；出厂系统首次安装使用首刷 ZIP，两个包不能混用。Windows 用户先安装新桥接，再升级 TAB5。Mac 保留平台能力范围，本次重新构建同版本 Apple Silicon 包，不宣称具备 Windows 的全部新增功能。

## 验证与限制

发布前完整回归发现 USB 清理误吞无关异常，修正后桥接候选从 0.6.4 顺延为 0.6.5；0.6.4 标签仅保留为未公开候选。安装器依赖校验同时支持长路径。

桥接 Release 构建、公共空白用户目录自测及打包边界测试通过。TAB5 使用本次 .176 版本重新构建并运行原生 LVGL 回归，首刷、升级镜像、说明、源码身份和许可材料分别核验。完整交付哈希与结果保存在本地 `artifacts/development/release-064-176/`。

电脑已读同步已在 .168 实机清除未读数；汽水音乐已在 .175 实机验收。它们是对应版本的运行证据，不冒充 .176 新镜像的实机验收。本版完整目视／触摸、长期运行／睡眠恢复、新图库导入后的设备验收和 Apple Music 实际播放仍待完成；历史传输不稳定项目保留原验收边界。ESP8266 .5.2 经过构建和材料校验，尚未刷入实机，本次发布不自动升级设备。Windows 安装器未签名，Mac 为临时签名且未公证。

新截图来自本版原生 UI 隔离渲染，使用演示数据，见 [截图来源](SCREENSHOTS.md)。未变化的素材和历史产品介绍视频保留，不以视频代替本版功能列表。[完整下载中心](../DOWNLOADS.md) · [图库说明](GALLERY-PACKS.md) · [全部素材审核](RELEASE-MEDIA-AUDIT-20261011.md)

## English

This release delivers bridge 0.6.5 and TAB5 0.2.176-ui with extended fonts built into firmware. New collections and in-progress introductions, viewing suggestions and originals are excluded. Published curated edition 2026.10.09 remains available; ESP8266 0.5.2 ships alongside with aligned one-second network means and session-change history reset; installed user artwork is preserved.

Highlights include exact-turn desktop Codex read synchronization, Soda/Apple media-session recognition, nine clock faces and a classic green LCD, local sunrise/sunset day/night ring, separate clock timer and date-countdown screensaver, consistent navigation/titles, explicit display modes, balanced provider icons, refined Windows settings, actual quota periods and transport recovery/diagnostics. Independent image/text pack imports retain identity/hash checks, atomic installation, backups and hot reload; standard bridge builds do not embed the curation catalogue.

All 8,538 baseline glyphs retain their metrics/bitmaps; the source font contains 9,013 glyphs. Future gallery text must still pass coverage against the frozen release font. Upgrade and first-install ZIPs serve different installation states and cannot be interchanged. macOS is rebuilt on its existing platform scope, without claiming Windows feature parity.

The unpublished bridge 0.6.4 candidate was superseded by 0.6.5 after complete regression exposed overly broad USB cleanup recovery; unrelated exceptions now remain visible. Installer dependency hashing also supports long paths.

Windows Release/public-profile/package checks and fresh TAB5 build/native regressions passed. Firmware/source/notes/license package identity is verified separately. Physical read-sync (.168) and Soda playback (.175) evidence remains version-specific; .176 visual/touch, sustained/sleep recovery, new collection hardware imports and Apple playback remain pending. ESP8266 .5.2 passes build/material checks but remains unflashed and not hardware-accepted; publication never installs it automatically. Windows setup is unsigned; Apple Silicon macOS is ad-hoc signed and not notarized. New captures are native fixture renderings, not photographs; unchanged media and the historical product video remain.
