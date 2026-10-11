# TAB5 0.2.171-ui 本地候选

包含两项独立倒计时及厂商图标视觉大小调整。当前已完成本地验证与规范打包，并由运行中的桥接提供升级，尚待设备安装和实际显示验收；不代表公开发布。电脑桥接无需更新，仍在整理的图库不导入。

## 使用

- **应用 → 时钟 → 倒计时**：设置时、分、秒，支持 5/15/25 分钟快捷选择、开始、暂停、继续和重置。关闭窗口后继续计时，到时显示提醒并按现有静音／音量播放短提示音。设备重启后停止计时。此版没有自动循环的番茄钟专注／休息阶段。
- **设置 → 屏幕声音 → 日期倒计时 → 设置名称和日期**：输入最多 24 字的事件名称，可使用独立中文拼音键盘；选择目标年月日并保存。然后将屏保类型改为「日期倒计时」，点击「预览屏保」查看。日期按本地日历天数计算，目标当天显示「就是今天」，过期显示「天前」。名称和日期重启保留，未校时不显示虚假天数。
- **总览／内容轮播**：共用统一视觉尺寸的 11 个厂商图标；Codex 保持 30×29，带底色的六个图标为 32×32。

## 候选身份

- 路径：`artifacts/firmware/tab5/latest/aibot_tab5.bin`
- 镜像字节：6,805,472；应用分区剩余约 6%。
- 镜像 SHA-256：`241aa0c3e59fc8453e2513f03b9397ab6c94e0e6d867c3ad69973a637f03e19e`
- ELF SHA-256：`ad98c11256e99df7cb899fc6b2753cb9889d261c9128e1b38988070e3a3ec83e`
- 归档：`artifacts/firmware/tab5/versions/0.2.171-ui/241aa0c3e59f-db5d5ffe/`

## 验证

证据位于 `artifacts/development/countdowns-171-20261011/validation/`：

- `COUNTDOWN_MODEL_OK`：日历天计算、跨年、闰年、世纪例外、NVS 读取与保存失败、最大时长、零时长和一次性到时状态。
- `COUNTDOWNS_UI_OK`：时钟开始／暂停／继续／后台到时一次提醒／重置，日期配置校验及保存失败不覆盖，目标前／当天／过期展示，独立拼音输入。
- `LVGL_PREVIEW_OK`：完整原生回归，包括既有 Codex 拼音与草稿、页面切换缓存和其他屏保。新增控件的状态动画在测试中按真实 LVGL 时钟结束，缓存断言保留。
- `OTA_ACTUAL_SIDECAR_PREVIEW_OK`：使用本镜像匹配的真实说明进行预览；已目视检查说明、设置布局、中文输入和两项倒计时画面。
- ESP-IDF 编译、分区大小及 `DISPLAY_FLASH_SAFE_ELF_PASS` 通过。

早期失败日志保留在同级 `full/`、`focused/` 和初始构建日志，不作为最终通过证据。未将模拟截图或持久化测试视作硬件验收。

## 待真机核验

2026-10-11 已在电脑桥接「固件升级」页确认显示 `0.2.171-ui · 可供设备升级`，并显示本版两项倒计时及图标调整说明。USB、Wi-Fi 和蓝牙均已连接。提供升级后 `/status` 确认自动模式、循环开启，原页面顺序和 15 秒间隔保留；尚未触发设备安装。

安装后核验确切 ELF、启动与有效 OTA 分区；在设备确认厂商图标大小、时钟后台到时／静音、中文日期设置／重启保留、屏保预览与退出。保留原轮播页面、顺序和 15 秒间隔；图库正式导入等内容整理完成后单独处理。

## English

This local candidate adds independent clock and date-screen-saver countdowns plus provider logo balancing. The relative timer uses monotonic time and stops on reboot. The calendar feature stores its own target date and event name, counts local civil days and handles today and past dates. Chinese input is isolated from Codex. Full native regression, persistence/date tests, exact release-note preview, ESP-IDF build and display/flash safety checks passed. The running bridge confirms that 0.2.171-ui is available for device upgrade, with automatic cycling and the original page order and interval preserved. Installation, exact boot identity and physical usability remain pending. No public release or unfinished gallery import is performed.
