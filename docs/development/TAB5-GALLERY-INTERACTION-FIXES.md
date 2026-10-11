# 书画交互本地修复 / Local gallery interaction fixes

后续状态：用户现已授权完整核对后构建，修复纳入与 `.166` 候选见 [验收记录](../TAB5-ACCEPTANCE-166.md)。下文保留暂停构建期间的历史证据。 / Follow-up: the user subsequently authorized the audited .166 build; the linked record tracks inclusion and artifacts, while the evidence below remains historical.

2026-10-10。设备仍运行已核验的 `0.2.164-ui`。用户要求先解决现有问题，暂停构建新固件；本轮只修改源码并运行电脑端测试，未构建、打包、提供或安装新镜像。

## 当前实现

- 相同图片及布局不再反复设置图片源、控件位置和样式。八次相同画面更新产生零次 LCD 刷新。
- 缺失目标方向的图片时保留当前完整画面，目标图片就绪后再切换，减少中间布局刷新。
- 连续点击按待执行目标累加；后台预取和前台读取结束前保留用户的最新翻页目标，直到匹配结果到达。
- 已缓存的前台图片可独立于后台下载解码，网络读取仍保持串行；后台不生成 RGB 图片，输出缓冲仍为两块。
- 提前同步当天两类作品，首次加载显示同步状态。
- 转场仅保留在 `TAB5_UI_PREVIEW` 电脑端实验分支。设备分支始终关闭转场，不创建实验图层。用户体验以响应及时、画面稳定为先；电脑回放不能证明真机顺滑。

## 本地验证

完整 LVGL UI 回放及方向过滤、实际传感器驱动逻辑、JPEG 缓存、生产 worker、DMA 缓冲测试通过。覆盖两分类与四方向的实体坐标点击、32 次连续点击、后台/前台等待、详情返回、跨日及失败保留。4 MiB 绘图预算下分块输出与完整输出逐像素一致。失败记录和夹具修正记录均保留，未跳过断言。

电脑端转场实验另测局部刷新、缓冲归属和打断；这些结果不作为启用设备转场的依据。正式分支没有新增转场，仍须在未来获准构建后验证端到端响应、物理旋转到面板更新的耗时和操作手感。

证据目录：`artifacts/development/gallery-interaction-fixes-20261010/`，主记录 `native-day.log`、五项专项测试日志、`refresh-before.log`、`motion-real-gallery.log`。源码位于相邻独立 TAB5 工程 `firmware/main/gallery_remote.c`、`ui.c`、`ui_gallery_saver.h`、`ui_gallery_reader.h`、`ui_gallery_transition.h`，回放位于 `tests/ui-preview/preview.c`。

## 镜像与实机边界

此前生成但未批准安装的 `.165` 草稿不包含本轮完整修复，不能作为当前源码的候选交付。固定 latest 镜像 SHA-256 仍为 `b589bf9450e0584adb96f44a72dd258c2bd42b462d5b65bd5561ea87386781fd`；本轮没有更新该文件。

`.164` 当天缓存及断开电脑后的书法翻页、旋转、详情返回已获用户真机确认。一小时通信观察于 18:30:44 结束，1731 个样本，整体未通过：早期 GCM `-18` 与重连超时均保留。USB 在 18:04:50 已自行恢复，末样本回报新鲜，但这不消除此前失败，也未定位根因。用户操作时感到的刷新生硬尚未完成修复后真机验收。原自动循环展示已恢复；临时自动连接模式尚需在剩余设备验证结束后恢复用户的蓝牙专用偏好。

后续 USB 源码修复、本地并发复现以及 .164 的 18 项内存传输采样见 [USB 排查记录](TAB5-USB-RELIABILITY-2026-10-10.md)。新修复尚未在设备运行；短测通过不覆盖原一小时失败。

## English

The installed device remains on verified .164. New firmware builds, packaging and installation are paused at the user's request. Local fixes suppress unchanged-image redraws, preserve the complete visible image while the requested layout is unavailable, retain accumulated page intent through background/foreground waits, and let cached foreground decoding proceed independently of background transport. Two RGB output buffers remain the limit. Full native UI replay and five focused suites pass; physical latency and smoothness are pending hardware validation.

Motion is a desktop-only experiment under `TAB5_UI_PREVIEW`. The device branch disables it and creates no experimental layers. The existing unapproved .165 draft is unchanged and does not include all current fixes. The .164 one-hour observer completed with a failed overall result; USB later recovered, but the initial authentication and timeout failures remain unresolved. Do not treat recovery, native replay or an old candidate as full hardware acceptance.
