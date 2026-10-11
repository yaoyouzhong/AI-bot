# TAB5 0.2.173-ui 时钟分段切换

本版将用户确认的「时钟｜倒计时」预览方案接入实际固件。时钟页与倒计时标题栏使用同位置的 284×56 分段控件，浅蓝色标示当前页；原独立倒计时入口移除。开始计时后标题栏、设置与操作隐藏，轻触显示操作，5 秒后自动收起。切回时钟继续计时，再进入时保留剩余时间；暂停、重置和到时提醒沿用原逻辑。屏保日期倒计时继续独立。

同时移除「计时中轻触屏幕可显示操作」提示，以及日期设置下方「按天倒计时 · 设置会保存在设备上」备注。

## 本地验证

证据目录：`artifacts/development/clock-segment-173-20261011/`。

- `native-focused.log`：`COUNTDOWNS_UI_OK`，通过实际指针点击验证两个分段、切回时钟、后台继续计时、重新进入、纯数字显示、控制自动隐藏、暂停／继续、到时只通知一次、零时长保护及独立日期表单。
- `firmware-build.log`：ESP-IDF 构建完成；`display-flash-safe.json` 验证运行镜像的显示 ISR 与 Flash 安全路径。
- 使用公共桥接正式镜像校验器核对 ESP32-P4 结构、内置版本、SHA-256 和更新说明，按规范生成 latest 与历史归档。
- `full-preview.log`：完整 LVGL 回归 `LVGL_PREVIEW_OK` 与本包实际 sidecar 预览 `OTA_ACTUAL_SIDECAR_PREVIEW_OK` 通过；已检查说明首屏与滚动后的真实渲染。

原生截图：[时钟页](../artifacts/development/clock-segment-173-20261011/clock-segment-classic.png)、[倒计时设置](../artifacts/development/clock-segment-173-20261011/clock-countdown-ready.png)、[计时画面](../artifacts/development/clock-segment-173-20261011/clock-countdown-running.png)。这些为实际源码的 LVGL 预览，并非设备照片。

## 镜像身份

| 项目 | 值 |
| --- | --- |
| 版本 | `0.2.173-ui` |
| 规范镜像 | `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| 字节数 | 6,860,608 |
| 镜像 SHA-256 | `25967d5d05bb172f22eb05947fafb957fb9384de9e58f1b7955e8bc531e8d2a7` |
| ELF SHA-256 | `f40ea6626e7e11020407e8127d315d4970b6c01c9fba8d056b647980c4b99f7a` |
| 归档 | `versions/0.2.173-ui/25967d5d05bb-06f8d72a` |

## 设备交付

候选已规范打包并由正在运行的电脑桥接提供。用户打开固件升级窗口后，刷新观察确认「0.2.173-ui · 可供设备升级」「已提供固件，等待设备确认」，且更新说明与本包一致；证据为 `bridge-offer-state.txt` 与 `bridge-offer.png`。窗口控制曾检测到用户输入，未继续使用旧控件索引；重新读取当前状态确认提供成功。

12:13:03 只读设备诊断仍报告 `0.2.172-ui`，USB／Wi-Fi／BLE 连接正常，界面心跳年龄 3 ms，显示错误与欠载均为 0。此为升级前状态，不视为 .173 设备安装验收。请在 TAB5「设置 → 固件升级」点击「通过 Wi-Fi 升级」安装。升级后的实际触控、显示与启动镜像验收待完成。

交付前后只读核对自动轮播保持开启，六个原页面、顺序和 15 秒间隔一致，见 `before-display-policy.json` 和 `after-display-policy.json`。本次未退出或更新桥接，不改变展示配置。不更改图库内容，不公开发布。

## English

The confirmed Clock / Countdown segments are implemented in both headers with matching geometry and a pale blue selected state. Returning to Clock preserves the running timer, and reopening preserves its remaining time. The running screen shows only large digits until touched. Both requested explanatory notes are removed.

Native pointer interaction, countdown/date regressions, ESP-IDF build, display/flash safety, canonical package validation, full LVGL regression and actual sidecar preview passed. The live bridge window confirms the .173 offer with matching notes. The device still reports .172 before installation; install over Wi-Fi from its firmware settings. Physical UI and exact boot-image acceptance remain pending. Read-only before/after checks preserve all six original automatic carousel pages, their order and the 15-second interval. Gallery content and desktop bridge binaries are unchanged.
