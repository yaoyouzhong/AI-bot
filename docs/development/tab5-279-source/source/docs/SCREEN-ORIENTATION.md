# 横屏翻转 / Landscape rotation

普通页面与时钟屏保仅支持正向和倒置 180° 两个横屏方向，不新增竖屏布局。

- 设置 → 屏幕声音 → 屏幕方向：默认“自动翻转”；“锁定当前方向”保存当前横屏方向，重启后仍生效。
- 屏幕声音页使用两列等宽布局，标题置于控件上方；声音控制位于提示音量下方，第三行左侧为“屏保等待”、右侧为屏保类型与预览，常用设置无需滚动。
- 连续有效的 IMU 采样稳定 500 ms 后切换。竖放、平放、明显运动、传感器失效或数据过期时保留当前方向。
- 触摸、滑动、相机预览、固件升级和 Flash 测试期间暂缓自动翻转；结束后重新确认姿态。
- 书画屏保继续使用原有四向阅读布局。进入时恢复其基准坐标，退出时恢复普通界面的横屏方向。
- 原始面板为竖向坐标，普通横屏使用 LVGL 90°，倒置横屏使用 270°。显示与触控共用 LVGL 的方向，不再对触摸控制器重复镜像。
- 相机预览期间保持打开时的方向；倒置打开相机时使用已有 LVGL canvas 路径，避免正常方向的直接合成器错位。该路径的真机帧率尚待验证。
- 方向偏好独立存储，不修改原有展示模式、轮播页面、顺序、间隔或电脑桥接协议。
- “提示静音”关闭提示声音但保留屏幕提示；“任务状态提醒”控制任务完成、等待操作和 Codex 新回复的主动提醒，任务状态与未读标记仍保留。

Ordinary pages and clock screensavers support normal and inverted landscape only; there is no new portrait layout.

- Settings → Screen and sound → Screen orientation defaults to automatic. Lock current direction persists the current landscape direction across restarts.
- The screen/sound page uses two equal columns with labels above controls. Sound controls sit below volume; the third row places screensaver delay on the left and type/preview on the right. Ordinary controls fit without scrolling.
- Switching requires 500 ms of continuous valid IMU samples. Portrait, flat, accelerated, unavailable and stale samples preserve the current direction.
- Touch gestures, camera preview, firmware updates and Flash tests defer automatic rotation; pose confirmation restarts afterwards.
- Art screensavers retain their existing four-way reader layout. Entering restores their baseline coordinate system; leaving restores the ordinary landscape direction.
- The physical panel uses portrait coordinates: LVGL 90° and 270° implement the two landscape directions. LVGL maps raw touch coordinates using that same rotation.
- Camera sessions retain their opening direction. Inverted sessions use the existing LVGL canvas fallback instead of the normal-direction direct compositor. Hardware frame rate remains unverified.
- A separate preference key preserves all existing display/carousel settings and leaves the bridge protocol unchanged.
- Mute notification sounds silences audio while retaining visual notices. Task status notifications control completion, input-needed and Codex reply announcements without hiding task status or unread indicators.

## Validation

Native coverage: `ui-orientation-test`, `saver-prefs-test`, `display-rotation-test`, and production UI preview with `TAB5_ORIENTATION_ONLY=1`.

2026-10-17：0.2.211-ui 已刷入 TAB5，精确版本、ELF 指纹、OTA 分区及 VALID 启动核验通过。用户确认：“实际效果可以，翻转和触控也正常”。此确认覆盖本次设置布局、横屏翻转和触控的实际体验，不扩展为所有专项场景均已验收。

2026-10-17: 0.2.211-ui was installed on TAB5 and passed exact-version, ELF fingerprint, OTA partition and VALID boot checks. The user confirmed the physical layout, rotation and touch behavior. This confirms the observed experience, not every specialized scenario.

- Application SHA-256: `ecda8b9b373502bb69fb951cca886c68059fe698f62dca348b4bf29d4fa723d9`.
- ELF SHA-256: `ad348ad17e8b34a795e1c36447d31a6366bf08e0ccdc82c6b804118d59679f87`.

尚未单独记录的真机场景：全部页面及四角触控、按住滑动时延后翻转、锁定后重启、进出书画屏保、两个方向下相机的实际性能，以及持续显示错误监测。

Checks without separate hardware evidence: every page and all touch corners, rotation deferral during held swipes, lock persistence across restart, art screensaver transitions, camera performance in both directions and sustained display-error monitoring.
