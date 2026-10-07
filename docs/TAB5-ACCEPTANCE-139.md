# 本地 .139 扫描供数候选

2026-10-07。精确镜像启动与优先级生效已核验，但用户确认仍然蓝闪；本候选未通过实屏验收，不能作为修复交付。

## 依据与改动

用户确认 .138 的 DSI host 硬件彩条稳定，恢复原画面后再次蓝闪。记录证明彩条期间扫描 DMA 帧计数停止，设备在 20 秒后自动恢复，桥接和原轮播已恢复。这将排查重点缩小到帧缓冲扫描/供数路径，但不直接证明某个参数就是根因。

当前 IDF 5.4.2 的扫描驱动每帧结束后，需要 DMA 中断重新启用下一帧。候选做以下处理：

- 下一帧 DMA 重启排在帧统计和欠载诊断之前；保留欠载计数、错误日志及原刷新回调。
- DSI 首先申请 DW-GDMA 中断优先级 3，仍在 IDF 允许的 LOWMED 范围内；后续使用默认优先级的共享 DW-GDMA 用户继承组优先级。
- DW-GDMA 的内存端口 1 读取优先级设为 3，保留其写优先级及 CPU/cache/DMA2D 等其他主设备的设置；避免扫描与普通读写仅按默认相同优先级竞争。
- 诊断读取真实仲裁寄存器，报告 `qos=DW,CPU,CACHE,DMA2D`。不改时钟、方向映射、作品内容或轮播设置。

这些是基于已隔离路径的候选改动，仍需真机确认蓝闪消失，以及输入、界面与屏保行为正常。不能用编译或错误计数替代实屏验收。

## 验证与镜像

- 完整本地固件构建通过。
- 补丁在干净的固定 IDF 源码上验证，通过重复应用、保留错误处理/刷新回调、拒绝未知 SDK 结构测试。
- ELF 检查通过：扫描 ISR/调用链仍在 IRAM，且实际链接的 DMA 启动调用先于诊断调用。
- 镜像 6,528,704 字节，SHA-256 `e2844f4f22765ac1feef8e5cba17240366bd5dd8517c30c5ac6f39e64737fa5b`。
- ELF SHA-256 `12339846d4b3c68c0dbfc15386db069d5fa8aa199161918eacb107c82ef6babd`。
- 正式包校验器完成固定 OTA 路径和历史归档。
- 完整原生 LVGL 回归通过，实际 sidecar 生成的升级说明首屏与滚动预览已检查，版本为 `.139`、分类标题和完整条目均可读。
- 真机启动核验通过：`ota_0`、`VALID`，ELF 与候选一致，三次采样 UI 心跳正常；真实 `qos=3,0,0,0`，DPI 80 MHz、分频 3、`hline=1303`。
- 用户反馈：“仍然蓝闪”。扫描优先级与重启顺序调整未解决现象，不再沿该方向逐项调参。
- 桥接已恢复固定运行路径，自动模式、原六页顺序和 15 秒间隔保持。

## 回归基线调整

用户随后明确此前没有此现象，并回忆可能从新增竖屏后出现。归档说明确认 `.125` 为横屏艺术屏保，`.126` 首次加入竖屏。因此下一步以原始 `.125` 二进制作为回退对照，而非用当前代码重建近似旧版。该判断是待验证线索，不把 `.131` 的书法花屏验收扩大解释成持续蓝闪已通过。

12:01 已从固定桥接路径提供原始 `.125` 对照镜像，SHA-256 `47cfd54b06a9ba09b4796403477c73b9bc3b47bd2f6fdac41b249f4ddb7e7147`。生产校验器与实际说明预览均通过。12:02 用户已安装，核验为 `ota_1 VALID`、ELF `f7992f756c632fe6d6901a2176fffedcb5f4ea62b27e05d79b45a6c2cad13fa2`，三次 UI 心跳正常，桥接及原六页 15 秒自动轮播恢复。原配置与图库保留，艺术屏保暂时仅横屏；仍需用户确认普通界面、输入、两类屏保的实际对照结果。记录在 `artifacts/development/gallery-preview-sync/regression-control.json`。

随后用户补充：`.125` 普通界面和已显示内容不再蓝闪，但名画仍等待电脑同步，不能记为名画完整通过。

第二次使用原始 `.131` 二进制对照：SHA-256 `3a1b3655f4becc544575df4871a965f4a578dd4552a077444728ec90a03d0125`，实机 `ota_0 VALID`、ELF `ee715c1822172327acff1ff2afdf3ab30d6739ea9153ad1b4ffbe356e4b0bbea`，方向传感器正常，原轮播已恢复。用户确认：“不再蓝闪，两类图片和转向都正常”。这证明竖屏功能在 `.131` 可正常运行，将回归范围缩小到其后的改动；字体压缩与扩展字形缓存是主要嫌疑，尚未单独证明是哪一项造成蓝闪。

修改仅为本地候选。公开 `.133` 含可疑字体压缩，须重新构建、验收后才能发布；本次未发布。证据目录 `artifacts/development/gallery-preview-sync/`。

## English

The .138 hardware color-bar test was stable while normal framebuffer output still flashed blue. This candidate rearms scanout before diagnostics, requests a supported level-3 DW-GDMA interrupt, and prioritizes reads from its memory master while preserving other arbitration settings. Build, patch, IRAM, linked call-order, native UI and notes-preview checks passed. Exact-image ota_0 VALID boot and live QoS registers were verified, but the user confirmed continued blue flashes: the candidate failed physical acceptance. The original cycle was restored. Archived .125 stopped flashing on normal UI and displayed content, but paintings still waited for synchronization. The original .131 subsequently passed exact-image/ELF/VALID boot checks, and the user confirmed both art categories and rotations worked without blue flashes. The regression is therefore after .131; font compression and its glyph cache are suspects rather than individually proven causes. Existing public .133 contains the suspect compression and must be rebuilt and revalidated before release.
