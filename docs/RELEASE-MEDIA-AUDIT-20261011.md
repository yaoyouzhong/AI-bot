# 本次发布素材审核 / Release media audit

2026-10-11，桥接 0.6.5、TAB5 0.2.176-ui、ESP8266 0.5.2。结论：截图、文字、安装说明、宣传视频与三种比例封面均已按本版实质变化同步；未变化的画面保留历史来源。

| 类别 | 核验与处理 | 状态 |
| --- | --- | --- |
| 公开截图 | 全部 87 张逐图查看、比对引用和已登记哈希；本次变更 42 张，保留 45 张未变化或明确历史的素材 | 完成 |
| 本版新功能图 | 九款表盘、时钟计时、日期倒计时、统一返回和标题、总览／模型图标、显示模式、桥接设置、图库窗口、安装与升级窗口 | 已刷新 |
| 功能／平台介绍 | 中英文 README、功能图鉴、图库能力和许可来源、平台与版本专属验收边界 | 已同步 |
| 安装与开发者说明 | 更新下载版本／包名／校验链接及首刷、升级、配对、原字库与旧图库保留说明；源码页切到 .176，核对源码与应用哈希 | 已同步 |
| 图库内容 | 正式 2026.10.09 名画／书法各 366 件；新作品与整理中的导读、原文继续留在本地 | 本次不发布新内容 |
| 宣传视频和封面 | 108 秒／14 个镜头，新增表盘、两种倒计时和已读同步，刷新导航、音乐、图库管理与安装更新；16:9、4:3、3:4 封面全部刷新 | 已更新，主页播放器、MP4、来源哈希和文字引用同步 |

截图来自生产 WinForms／LVGL 的隔离演示数据，不是硬件照片。图库窗口使用正式版元数据夹具，不导入或下载新图库。固件升级截图读取本版实际 sidecar，旧的当前版本标签属于夹具，不作为实机版本或跨分区兼容证明。Windows／Mac 新包构建测试与独立设备验收分别记录；Apple Music 实际播放和 ESP8266 .5.2 实机验收仍待完成。

本次视频更新保留 108 秒时长、产品风格和原创配乐，并按新分镜重新对位 28 个动作音效。已读、计时和音乐进度使用实际原生 UI 逻辑的隔离状态；封面使用新的昼夜环。新图库不纳入影片。细节和可复现工程见 [产品介绍视频](PRODUCT_VIDEO.md)。

All 87 public screenshots were visually reviewed and their registered fingerprints checked. Forty-two changed captures are refreshed; forty-five unchanged or historical captures are retained. Current descriptions, installation routes, developer-source identities and acceptance limits are synchronized. The updated 108-second, 14-shot film covers new clock faces, separate timer/date-countdown functions, read synchronization and navigation, and refreshes music, collection management and setup/update scenes. All three cover ratios, the native homepage player, MP4 identity and references are synchronized; unchanged footage retains its provenance. No new collection is released.

## 本次刷新文件 / Refreshed files

- `docs/assets/screens/add-device.png`
- `docs/assets/screens/api-settings.png`
- `docs/assets/screens/authorization-empty.png`
- `docs/assets/screens/cycle-settings.png`
- `docs/assets/screens/device-center-accounts.png`
- `docs/assets/screens/device-center-bridge.png`
- `docs/assets/screens/device-center-esp8266.png`
- `docs/assets/screens/device-center.png`
- `docs/assets/screens/device-control.png`
- `docs/assets/screens/firmware-flasher.png`
- `docs/assets/screens/gallery-packs.png`
- `docs/assets/screens/mirror-window.png`
- `docs/assets/screens/quota-trend-empty.png`
- `docs/assets/screens/settings.png`
- `docs/assets/screens/tab5-apps.png`
- `docs/assets/screens/tab5-clock-0.png`
- `docs/assets/screens/tab5-clock-1.png`
- `docs/assets/screens/tab5-clock-2.png`
- `docs/assets/screens/tab5-clock-3.png`
- `docs/assets/screens/tab5-clock-4.png`
- `docs/assets/screens/tab5-clock-countdown.png`
- `docs/assets/screens/tab5-clock-geometric.png`
- `docs/assets/screens/tab5-clock-lcd.png`
- `docs/assets/screens/tab5-clock-picker.png`
- `docs/assets/screens/tab5-clock-solar.png`
- `docs/assets/screens/tab5-clock-star.png`
- `docs/assets/screens/tab5-connection.png`
- `docs/assets/screens/tab5-countdown-settings.png`
- `docs/assets/screens/tab5-date-countdown.png`
- `docs/assets/screens/tab5-first-install.png`
- `docs/assets/screens/tab5-overview.png`
- `docs/assets/screens/tab5-quick.png`
- `docs/assets/screens/tab5-quota.png`
- `docs/assets/screens/tab5-settings-connection.png`
- `docs/assets/screens/tab5-settings-content.png`
- `docs/assets/screens/tab5-settings-firmware.png`
- `docs/assets/screens/tab5-settings-info.png`
- `docs/assets/screens/tab5-settings-screen.png`
- `docs/assets/screens/tab5-upgrade.png`
- `docs/assets/screens/tab5-usb-pairing.png`
- `docs/assets/screens/update-center.png`
- `docs/assets/screens/weather-settings.png`
