# 本地 .142 字体容量候选

2026-10-07。本地完整版，已完成 Wi-Fi 升级、精确镜像启动核验和用户实屏验收。用户确认：“正常，没有蓝闪，文字图片和转向都正常”。

## 范围与证据

基于用户已验收的 `.141`，仅恢复两套中文字库无损压缩、对应绘制缓存支持和字体生成参数。显示驱动、图片同步/缓存和自动屏保转向逻辑保持 `.141`；不恢复显示实验或外部堆遍历诊断。

- 全部 16,132 个字形的存在性、尺寸和像素一致；快速绘制 23,990 项逐像素核对通过。
- 原生 UI、实际画作横竖屏和自动屏保四方向通过；非更新说明截图与 `.141` 逐像素一致。
- 构建和扫描 ISR 的 IRAM/XIP 链接检查通过；实际 sidecar 两页预览已检查。
- 应用 6,526,048 字节，槽位 7,208,960 字节，剩余 682,912 字节（约 667 KiB）；相比 `.141` 新增 680,288 字节空间。
- SHA-256：`d6b5fc8e8023e98ccd851e4269d5c423e07d0c5d2c807a25127777614f3a5a89`。
- ELF SHA-256：`5d1879bca3b1006e17132983511979dc2da12d9e7c7046216349d04c3441e9ab`。

固定路径 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，历史包与证据位于 `artifacts/development/gallery-preview-sync/local-142/`。`.141` 保留为稳定回退。公开 `.143` 独立存放，不覆盖本地包。实机镜像与 ELF 完全匹配，OTA 状态 VALID；三次 UI 心跳年龄为 25/10/5 ms，显示错误及欠载均为零，方向传感器正常。已恢复自动模式、原六页顺序和 15 秒间隔，并由 `/status` 核对。启动证据为 `boot-142-verified.json`，用户观察为 `local-142/user-acceptance.json`。

## English

Local full-art .142 passes Wi-Fi installation, exact-image boot verification and user physical acceptance: no blue flashes; text, pictures and orientation are normal. It changes only two losslessly compressed fonts, matching glyph-cache support and generation options on accepted .141. Display, gallery and automatic orientation behavior are retained. All 16,132 glyphs and 23,990 drawing cases pass pixel checks; non-note preview images match .141. Build and IRAM/XIP checks pass. The image leaves 682,912 bytes free, saving 680,288 bytes. Stable .141 remains available for rollback; public .143 is stored separately. Exact image/ELF identity and VALID OTA state pass; UI ages are 25/10/5 ms, with no display errors or underruns and a healthy IMU. Original auto mode, six-page order and 15-second interval are restored and verified.
