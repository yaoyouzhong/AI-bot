# TAB5 .194 书法竖屏偶发卡死

后续：2026-10-15 已对独立复现的绘图内存压力路径准备 201 本地修复，见 [201 稳定性记录](TAB5-STABILITY-201-20261015.md)。以下保留原故障调查；201 尚未实机验收，原事故根因仍未确认。

2026-10-14，用户报告书法图片页转为竖屏后卡死，无法退出，并伴随闪屏；尚未进入作品详情。手动重启后，用户确认图片竖屏、作品详情和返回恢复。问题仍未确认修复，不以重启恢复冲销故障。

## 现场证据

- 当前设备固件为 `0.2.194-ui`。重启后回报界面心跳年龄 44 ms、显示错误 0、欠载 0；崩溃查询为空。故障发生时的诊断未取得，重启后的零错误不能证明故障期间无显示异常。
- 实际 `.194` 开发源码位于 `C:/Users/yaououzhong/Work/tab5-178`，HEAD `69cf5de8830059690f2ff336fdeb1323276fe43a`；相邻 `m5stack TAB5` 主 checkout 并非本次镜像源码。开发源码未修改。
- 保留原自动循环模式、六页顺序、15 秒间隔；没有操作设备、修改图库、替换桥接、构建新固件或刷机。

## 本地检查及限制

- 使用实际源码的原生 LVGL，正常内存下回放名画和书法四个方向、简介/欣赏提示/原文、详情/返回与实际指针退出，通过。不能替代故障状态的实机复现。
- 方向过滤、2352 个正交旋转与 LVGL 原始实现的逐像素对照、196 次图库解码/72 次离线重开、180 次早到/延迟 DMA 完成检查，通过。
- 额外限制绘图缓存申请总量为 716800 字节时，倒置横屏详情所需图层申请失败：已有 557280 字节，仍请求 2693520 字节。测试明确以退出码 69 记录失败；未放宽检查或改生产代码。这是内存压力条件的回放，既不是此次书法图片页死机的复现，也没有证明设备故障时存在同样的空闲量或碎片状况。第二次指定 4 MiB 只是上限，原测试仍取 716800 字节，不构成 4 MiB 条件验证。
- 现有 LVGL 在图层无法分配时不能完成绘制，显示传输等待也依赖实际 DMA 完成；两者都是需故障时证据区分的阻塞位置。目前没有足够证据确定此次根因，不据此修改驱动或宣称修复。

下次出现故障时，应在重启前读取界面心跳、显示传输错误、PSRAM/缓存、方向与图库任务诊断，保留屏幕现象；如无通信，则明确记录通信阻塞。不要用事后正常回报覆盖故障记录。

证据目录：`artifacts/development/tab5-portrait-freeze-20261014/`。结论见 `assessment.json`，现场见 `recovered-diagnostics.json`、`final-recovered-diagnostics.json`、`crashes-after-restart.json`，正常回放见 `native-gallery-normal.log`，受限回放失败见 `native-gallery-baseline.log` 与 `native-gallery-4m-cap.log`。

English: Calligraphy froze immediately upon portrait rotation, with flashing and unresponsive exit. The user restored portrait/details/return by rebooting. Post-reboot diagnostics and normal native regressions pass, but no failed-state telemetry or retained panic trace was recovered. A deliberately constrained desktop allocation probe failed in an inverted reader; it does not establish the cause of the actual image-page freeze. The issue remains unresolved. No source, firmware, bridge, gallery or device settings were changed.
