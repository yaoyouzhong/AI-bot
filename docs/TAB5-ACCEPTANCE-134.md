# 本地 .134 自动艺术屏保方向修复

2026-10-07。本地艺术完整版，不是公开 TAB5 候选。

## 原因与修改

用户报告预览时可旋转，但电脑及桥接正常运行时，自动名画／书法屏保不能竖屏显示。固件的后台 tab 动画可以在屏保启动后发出延迟 `VALUE_CHANGED` 事件；原处理函数清除 `saver` 状态却留下可见屏保层，导致刷新定时器不再按屏保处理方向。

`firmware/main/ui.c` 在屏保活动时忽略后台 tab 事件。未更改此前已验收的四方向映射、倾角判断、图片解码缓存同步或画面布局。

## 已验证

- 回归通过实际 tab 事件与生产 `clock_tick` 路径验证普通屏保；名画、书法分别检查四个方向。修复前能复现断言失败，修复后完整 LVGL 回归通过。
- 本地 .134 Release 编译通过；6,526,048 字节，7,208,960 字节应用槽剩余 682,912 字节。字体无损压缩沿用 .132 验证结果，不改变分区。
- 镜像 SHA-256：`8c9bceeaabd0a63f8a875f30e48f6c8f52c3b0938cab7a5ac48ee758fbce1658`。
- ELF SHA-256：`a79515baaa2e641672ae31f82ce6cdeacd9e6891fb4fcc376a6f162ccbc9e9be`。
- 固定 latest 与历史归档已通过生产镜像校验器；实际更新说明预览通过。
- 本地完整桥接 0.5.1 已运行并提供 .134 OTA，全部 813 件作品检查通过，原六页及 15 秒自动轮播保持。
- .134 已刷入实机；USB 精确镜像核验通过，运行在 `ota_1`、状态 `VALID`，ELF 哈希与本次镜像一致。连续三次采样界面心跳正常、无显示错误或欠载；方向传感器就绪、错误为 0。已正常重启桥接并恢复原自动轮播。

## 用户实机验收

用户确认：“已升级，自动屏保方向正常”。本次确认对应回到日常页面、等待自动进入名画或书法屏保后，左右竖放及转回横屏的操作，不再仅依据预览模式。结合精确镜像启动核验及生产定时器回归，本次自动屏保方向问题验收通过。收尾复核仍运行 .134，自动模式、原六页顺序及 15 秒间隔保持。

证据目录：`artifacts/development/unified-updates/`。固定交付：`artifacts/firmware/tab5/latest/aibot_tab5.bin`。原 .131 与桥接运行目录保留。

## English

Local full-art .134 ignores a delayed background tab-change event while the screensaver is active. Previously it cleared the saver state without hiding its layer, stopping automatic art orientation handling. A regression reproduces the failure before the fix and passes through the production timer for both galleries and all four directions afterward. Build, image/sidecar validation, notes preview and native UI checks pass. Hardware installation and exact-image USB boot verification also pass: ota_1 VALID, matching ELF hash, three healthy UI samples with no flush errors/underruns and a ready, error-free orientation sensor. The user confirms normal orientation after automatic entry, testing portrait in both directions and returning to landscape. This issue is accepted on hardware. The full 0.5.1 bridge is running, with all 813 artworks and original six-page, 15-second automatic cycling preserved. Public .133 is separate.
