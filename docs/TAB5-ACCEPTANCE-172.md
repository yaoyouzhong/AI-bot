# TAB5 0.2.172-ui 倒计时界面优化

两项倒计时继续独立。本版完成界面调整、本地验证和规范打包。设备已通过 Wi-Fi 安装并回报 .172，完整启动核验及用户实际显示验收仍待完成。

## 界面与交互

- 时钟设置页：时、分、秒使用统一的大号选择控件，快捷时长显示选中状态，开始按钮独立居中。
- 计时页：运行时仅显示居中的 192 px 数字，隐藏标题、设置和操作。轻触空白或数字显示关闭、暂停、重置，5 秒无操作后重新隐藏；再次轻触也可收起。暂停和时间到时显示状态与操作。重置返回时长设置；关闭仍继续后台计时。
- 屏幕声音设置：仅选择「日期倒计时」屏保时显示「设置名称和日期」。切到其他屏保，整行收起，音量等控件上移。保存屏保类型失败时保留原选项与原布局。
- 日期设置页：名称、目标日期统一放入表单卡片，年月日对齐，底部突出保存按钮。编辑名称时收起日期与保存区，完整显示中文键盘；「完成输入」恢复表单，「返回」不保存退出。

原生预览：[各状态切换](../artifacts/development/countdown-ui-172-20261011/preview.html)、[计时画面](../artifacts/development/countdown-ui-172-20261011/clock-countdown-running.png)、[日期设置](../artifacts/development/countdown-ui-172-20261011/date-countdown-editor.png)。截图来自实际 LVGL 渲染，不是设备照片。

## 验证与候选身份

证据目录：`artifacts/development/countdown-ui-172-20261011/`。

- `native-focused.log` 和 `full/preview.log`：`COUNTDOWNS_UI_OK`；真实指针点击数字唤出控制、超时隐藏、点击暂停，验证暂停／继续／后台到时／重置、零时长保护、最长时长文字不溢出。
- 屏保切换验证条件显示、音量区位移与保存失败回滚；中文输入期间隐藏其他表单控件、独立拼音、日期校验、NVS 保存失败与原功能回归通过。
- 完整原生页面回归 `LVGL_PREVIEW_OK`；本镜像真实说明预览 `OTA_ACTUAL_SIDECAR_PREVIEW_OK`，已检查显示与滚动。
- ESP-IDF 构建通过，`display-flash-safe.json` 检查通过，日期／计时模型与持久化测试通过。
- 新增数字字库使用项目现有、经哈希校验的 Montserrat Medium 来源与生成脚本，不变更中文字体或图库内容。

| 项目 | 值 |
| --- | --- |
| 规范镜像 | `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| 字节数 | 6,823,344 |
| 镜像 SHA-256 | `db43cfd77f0b5fe90e2d388ede1279e06ba26a0b65b3db13f45699ad49a631ea` |
| ELF SHA-256 | `6d5f4304c02e6765e1ac55929c966bb84fd965ff5f4666e833c4df4dc69bcc6a` |
| 历史归档 | `versions/0.2.172-ui/db43cfd77f0b-10316c9a` |

## 设备交付边界

电脑桥接仍为 0.6.4，无需更新。2026-10-11 用户恢复桌面后，桥接加载规范 .172 包，窗口确认本版更新说明并显示「正在传输」。设备完成 Wi-Fi 分块压缩升级：镜像 6,823,344 字节，实际传输 4,288,800 字节，传输阶段 13,719 ms；该时间不包含完整设备重启验收。

11:50:55 的新心跳报告固件 `0.2.172-ui`、运行 14,139 ms、界面心跳年龄 9 ms、显示欠载和显示错误均为 0，USB、Wi-Fi 和蓝牙连接正常。后续只读快照保存在 `installed-device-diagnostics.txt`。这证明新版已启动回报，不等于确切 ELF 与 VALID 分区核验：后续桥接窗口最小化，桌面控制恢复报告用户正在操作该窗口，未完成「核验启动（USB）」步骤；不绕过桌面控制限制。

只读 `/status` 确认自动模式和循环开启，六个原页面与 15 秒间隔保留，未改变用户设置，证据为 `installed-display-policy.json`。未公开发布，未导入仍在整理的图库。待核验确切启动镜像与分区，以及用户确认设备触控、纯数字显示和日期表单效果。

## English

This candidate separates configuration from the running timer display. Only large centered digits remain during counting; tapping reveals controls for five seconds, while paused and completed states retain controls. Date-screensaver settings are contextual and collapse without a blank row. The date editor uses an aligned form, a clear save action and a separate keyboard layout.

Native pointer interaction, full LVGL regression, matching release-note preview, ESP-IDF build, display/flash safety and persistence/model tests passed. The bridge loaded the canonical .172 image, and the device installed it over Wi-Fi and reported fresh .172 heartbeats with all three connections healthy and no display errors. Exact ELF/VALID-partition verification remains pending because desktop input could not recover the minimized bridge window while user input was detected. Physical UI acceptance remains pending. The original automatic carousel configuration is preserved. No bridge update, gallery import or public release is included.
