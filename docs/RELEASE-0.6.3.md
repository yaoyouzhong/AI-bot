# 桥接 0.6.3 / TAB5 0.2.156-ui / 年度精选集 2026.10.09

2026-10-09 已正式发布桥接 0.6.3、TAB5 0.2.156-ui 与两个年度精选图库包。QQ 音乐 22.71 已完成真实播放采样，交互与设备显示仍保留独立验收边界；Chrome 既有正常播放保留用户已验基线。TAB5 .156 已安装并通过启动核验，用户确认网络每秒同步显示正常，见[实际部署记录](LOCAL-063-156.md)。

本次电脑桥接 **0.6.3** 与 TAB5 **0.2.156-ui** 已本地运行，ESP8266 **0.5.2** 为未安装候选。按用户最新要求，同期发布精选作品集 **2026.10.09**：名画 **366 件**、书法 **366 件**；只发布精选集，全量版保留本地；两个独立可选 ZIP 附于本次 TAB5 Release，程序升级保留用户图库。[完整下载中心](../DOWNLOADS.md)已同步本次正式包。

## 变化与升级

- Windows／TAB5：电脑状态页上传、下载数字与曲线末端共用 1 秒均值，每秒发布完整快照并同步显示；取消设备额外播放缓冲，内部原始采样仍为 250 毫秒，CPU／内存仍每 2 秒采集。ESP8266 同步修复保留为候选，本次未纳入正式附件。
- Windows：修复多会话与日志缓冲导致的 Codex 桌宠误显示空闲；网易云音乐 3.x 补齐真实播放进度、中文别名、歌手及专辑，持续状态刷新时仍发送封面。
- Windows：增加 QQ 音乐只读当前歌曲与异步封面；多个系统媒体会话优先选择正在播放的来源，Chrome／Edge 媒体助手补充网页进度、暂停、拖动和倍速。其他客户端依赖其系统媒体接口，具体版本仍待实测，详见[媒体支持说明](MEDIA-PLAYERS.md)。
- Windows：股票搜索与排序、天气源及定位独立设置、生日编辑、开机启动保存反馈与服务状态布局改进；更新窗口隐藏同版／旧版升级动作，三个组件的更新说明支持离线阅读。
- TAB5：合并 .151～.154 的天气、音乐、设置和 Codex 布局；周额度以七等分表示每天计划用量，仍连续显示实际总用量；十二月历保留三种字体并改善行书辨识度。
- TAB5：设置增加设备信息、统一四字分类；任务页默认定位最近活动会话，保留手动选择及草稿；Codex 直达标题居中，升级页明确区分当前已安装与可用固件。
- 先安装电脑桥接。已有 AI-bot 的 TAB5 使用升级应用，首次安装使用首刷包。TAB5 网络曲线修复使用本次 .156；仅扩展媒体来源不要求刷机。ESP8266 本次继续使用已发布的 0.5.1。
- macOS 同步桥接版本元数据，功能保持原样；Windows 专用采集与设置改动不适用于 Mac。
- 宣传资料同步变化的原生截图、主页说明与 108 秒视频；图库镜头采用最终精选包的实际隔离导入结果，书法示例也核对为入选作品。原音轨保留，GitHub 原生视频附件已同步更新。

## 验证范围

网络修复已通过桥接构建、16 次真实 Windows 采样对应检查及原生 LVGL 数字／曲线、突增／停止／重连／丢包／批量数据回归。TAB5 .155 未单独安装，修复并入 .156；.156 已安装并通过精确运行镜像、VALID 分区、三次界面心跳和新版常驻桥接 16 次采样核验。ESP8266 0.5.2 未安装；.154 的下述验收仅适用于原版本。

随后的一秒更新改动通过完整公共自测；更新后的常驻桥接 107 次 HTTP 轮询取得九份快照，更新间隔 994～1004 毫秒，两次发布之间数字与历史保持一致，每次原始序号增加 4。用户随后确认实际显示正常。Mac 新包已在 macOS CI 完成测试、Release 构建、arm64 与签名校验，未沿用旧版 ZIP 更名；交互验收仍独立记录。

TAB5 .154 已安装并通过真实 USB 启动核验：镜像 SHA-256 `e4ce81c8d7a34507319255438ee5ace01652ebdd84c42bbec96cb0547efdbabc`，ELF SHA-256 `b10141aacb3c440777377807cb7b417617b1e8a751d668ef823aa84ee1770a34`，`ota_1 / VALID`、三次界面心跳推进、无显示写入错误或 LCD 欠载。用户确认设置分类及十二月历行书显示正常。详情见[本地设备记录](LOCAL-063-154.md)。任务选择、草稿与升级空状态等交互另有原生 LVGL 检查，不冒作全部实机操作验收。

本次保留原协议与用户资料。真实账户尚未验收的额度扩展不纳入发布；作品集作为独立图库 ZIP 同期发布，不内置于程序／固件；导览交互仍为浏览器预览，未接入设备。现有间歇 BLE 断连仍待定位，本版不宣称已解决。Windows 安装器未签名；Mac 为 Apple Silicon、临时签名、未公证，自动化构建与包校验不代表另一台电脑的交互安装验收。对应源码与构建资料记录镜像身份，不宣称已验证逐字节可复现构建。

## English

Bridge 0.6.3, TAB5 0.2.156-ui and both curated collection ZIPs were published on 2026-10-09. QQ Music 22.71 passed live playback sampling, with interactive/device acceptance tracked separately. The user's working Chrome playback baseline is retained. TAB5 .156 passed exact boot verification, and the user confirmed normal one-second synchronized network display. See the [deployment record](LOCAL-063-156.md).

Changed native screenshots, homepage descriptions and the 108-second film are synchronized. The gallery scene uses actual isolated imports of the final curated packs, and artwork examples are checked against the selected list. Original audio is preserved; the native GitHub video attachment has been synchronized.

Bridge **0.6.3** and TAB5 **0.2.156-ui** run locally; ESP8266 **0.5.2** remains an uninstalled candidate. The user's latest scope includes curated collection edition **2026.10.09**, with **366 paintings** and **366 calligraphy works**, with the full library excluded, as separate optional ZIP attachments on the TAB5 release. App upgrades preserve installed collections. The download center now lists these published packages.

Windows fixes Codex activity merging, adds read-only NetEase playback progress and translated metadata, preserves cover delivery during frequent state refreshes, and improves stock search/order, weather/location, birthdays, startup feedback, service status and component update notes. TAB5 includes .151–.154 layout changes, the music hierarchy, seven weekly-budget segments, readable calendar calligraphy, device information, recent-session selection and clearer navigation/update states. Network numbers and curve endpoints share a trailing-second mean, with complete snapshots published together once per second; raw sampling remains 250 ms and CPU/memory sampling two seconds. Use TAB5 .156 for this repair. ESP8266 .5.2 remains an uninstalled candidate and is excluded from public attachments; keep published .5.1. Media-source extensions alone do not require firmware updates. Upgrade the bridge first; existing TAB5 installations use the upgrade application, while factory installations use the first-install package. macOS only synchronizes version metadata and has a new macOS CI build with tests, arm64 and signature checks, without renaming an old ZIP; interactive acceptance remains separate.

Network checks passed the Release bridge build, 16 actual Windows snapshot comparisons and native LVGL burst/stop/reconnect/gap/batch regressions. The .155 repair was included in .156 without separately installing .155. Installed .156 passed exact image/ELF identity, VALID partition state, three advancing UI heartbeats and 16 new resident-bridge comparisons. ESP8266 0.5.2 remains unflashed; the following .154 hardware record applies only to that image.

The subsequent one-second publication change passed complete public self-tests and 107 resident HTTP polls covering nine snapshots with 994–1004 ms intervals, held headers/history and four raw sequence steps per update. The user confirmed normal physical display afterwards.

Windows additionally supports bounded read-only QQ Music current-song collection
and asynchronous artwork, selects playing system sessions ahead of paused
defaults, and accepts website progress, seek, pause and rate from the Chrome/Edge
companion. Other clients depend on their public system media interface; specific
versions remain unverified. See [media support](MEDIA-PLAYERS.md).

The installed TAB5 .154 passed exact USB ELF/partition verification and three advancing UI heartbeats with zero display errors. The user confirmed settings/calendar rendering; native LVGL interaction checks remain distinct from hardware acceptance. Unvalidated provider-quota extensions remain excluded. Curated collections are separate ZIPs, without bundling into apps/firmware; browser guides remain outside device integration. Intermittent BLE disconnections remain unresolved. Windows setup is unsigned; Mac is Apple Silicon, ad-hoc signed and not notarized. Package/build checks do not replace interactive installation on another computer, and byte-for-byte reproducibility is not asserted.
