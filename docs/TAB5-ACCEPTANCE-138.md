# 本地 .138 蓝闪隔离诊断

2026-10-07。已完成实机隔离：用户确认彩条稳定，恢复原画面后重新蓝闪。问题未修复。

.137 升级后用户先报告持续闪动并伴随蓝闪；随后补充普通闪动明显减轻、仍有轻微闪动，但蓝闪持续。退出桥接也不能消除蓝闪。新增一次限时硬件彩条测试，避免继续仅调整扫描参数。

## 隔离方法

配对 USB 命令 `tab5_display_pattern` 触发 ESP-IDF 的 DSI host 彩条发生器，临时停止 bridge 从帧缓冲输出像素。此时颜色由 host 直接生成，绕过艺术图片、LVGL 像素、PSRAM 和扫描 DMA；原界面及应用继续运行。20 秒计时由设备执行，电脑断开也自动恢复，另有立即取消命令。不写入 NVS，不更改轮播、方向、亮度或作品。

彩条仍蓝闪：优先检查 host/PHY/面板时序及硬件状态。彩条稳定、恢复原画面后蓝闪：优先检查帧缓冲扫描链路。两种结果都只缩小范围，不直接证明根因。

## 构建与检查

- 本地完整固件构建和扫描 ISR/调用链检查通过。
- 直接运行生产计时控制代码的主机测试通过：非法模式、设备未就绪、计时器创建失败、20 秒到期、提前取消、重新计时及启动计时失败时立即恢复。
- 正式包校验器完成镜像/说明校验与历史归档。
- 镜像 6,528,560 字节，SHA-256 `a0e441b184accb91313aa5d18bb8d0d8e39b621eccf135356af6fed335377efc`。
- ELF SHA-256 `37dddbcac2b14ac2e7607f956faf385d80a6c0b61f692edb3af6ae5beb399bdd`。
- `hline` 改读配置寄存器；此前 .137 的 shadow 读数为零，不能确认时序。该纠正只改诊断，不改显示时序。
- 完整原生 LVGL 回归通过，实际升级说明首屏和滚动预览已检查。
- 精确镜像/ELF/VALID 分区核验通过；实际 ST7121、DPI 80 MHz/分频 3、配置行时序 1303。
- 真机拒绝非法模式通过。彩条期间扫描 DMA 帧计数保持 4187；20 秒到期自动恢复，随后帧计数 4331→4404，UI 心跳年龄 36/34 ms，显示错误与欠载计数为零。
- 上述错误为应用刷新/bridge 欠载计数；DSI host 原始锁存状态单独记录：进入彩条时 `host1` 从 `0` 变为 `80`，退出时变为 `80080`，分别包含 DPI payload 写入错误和 buffer 欠载位。事件数对应两次切换从 1→2→3，此后至 11:53 保持 3；不能将切换时的锁存状态当作持续蓝闪根因，也不能把应用错误计数为零等同于全部硬件错误为零。
- 用户观察：“彩条稳定，恢复原画面后又蓝闪”。因此转向帧缓冲扫描/内存供数链路；稳定彩条只证明隔离条件下的表现，不等于已排除所有硬件因素。
- 桥接已正常恢复至固定运行路径，自动模式、原六页顺序及 15 秒间隔均已核对。

Windows 诊断脚本 `scripts/diagnose_tab5_display.py` 核对设备和 .138 版本，验证拒绝非法命令、彩条期间扫描帧计数停止、20 秒后自行恢复且扫描/界面继续。运行前正常退出桥接，运行后恢复原固定路径桥接和六页、15 秒自动轮播。实屏观察必须单独记录。

固定 OTA：`artifacts/firmware/tab5/latest/aibot_tab5.bin`。公开 .133 及发布版本登记不变。证据保存在 `artifacts/development/gallery-preview-sync/`。

## English

This diagnostic candidate does not claim a flicker fix. The user reports reduced ordinary flicker but persistent blue flashes on .137. A paired local USB command enables DSI host-generated bars for 20 seconds, bypassing rendered pixels and framebuffer scanout. Production lease tests and exact-image/VALID boot checks passed. On the device, scanout frame counts stopped during the bars and resumed after the automatic timeout. The user confirmed stable bars and blue flashes returning with normal output, focusing further investigation on framebuffer scanout without proving a specific root cause. The bridge and original six-page, 15-second automatic cycle were restored. Public .133 is unchanged.
