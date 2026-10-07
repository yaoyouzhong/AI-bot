# TAB5 .145 分页与竖屏验收 / Pagination acceptance

日期：2026-10-07；Windows 桥接 0.6.0；设备 e8f60ae2ec56。

- 固件：`0.2.145-ui`，6,527,936 字节；OTA 分区余量 681,024 字节。
- SHA-256：`a9d98a8f9a9be26c32a7b426cd6c23ac819c11fb17add4ff8a02efd1e5e758b4`。
- ELF SHA-256：`39dc71cab78b5d9d7e8b49ac27109aa4fff164a4afa2dbaf05cc70b42bcdb326`。
- 固件源码提交：`02df4f2`。串口读取版本、ELF、OTA VALID 状态、三次递增运行时间与健康 IMU，均通过；显示错误与欠载为 0。

## 问题与修复

.144 的书法第二页竖屏仍保留横屏排版。传感器已识别竖放，`galleryDiag` 却连续停在阶段 6、状态 200：图片已接收，但输出缓冲分配失败。翻页虽复用了未显示图，替换可见图时仍释放旧缓冲，下一方向因此再次申请大块内存。

.145 将退役图和失败任务的输出缓冲回收到一个原子备用槽。当前可见缓冲不交给后台解码；两块大图在显示与解码之间轮换，不修改方向映射、字体或显示驱动。

## 完成证据

- 原生 LVGL 调度回归：两类图库、24 次分页、96 次方向检查、下载失败重试；强制禁止第三次大图分配，仍全部通过。
- 解码回归：禁止新分配时，失败后的同一缓冲仍可重试；保留缓存同步、脏尾部、完整像素及短写拒绝检查。
- 真机：第二页 `4,0,1,1,10,0,1,1,2`；第三页 `6,0,1,1,10,0,2,1,2`。均解码完成，复用标志为 1，累计输出分配保持 2。
- 用户确认：第二页及后续分页竖屏都正常。既有 Codex、八种屏保及稳定显示验收保持原证据范围。
- 收尾：恢复自动模式与循环展示，原六页顺序及 15 秒间隔保持。

首刷包与升级包共享此应用镜像，未擦除设备重做出厂首刷。上述观察不是无限时稳定性或重新覆盖全部三通道的承诺。

## English

The .145 image and ELF identities, OTA VALID state, uptime progression and IMU health were verified on hardware. A retained spare buffer fixes later-page portrait allocation failures without changing orientation mapping or the display driver. Native tests cover 24 page cycles and 96 orientation checks with exactly two output allocations, including failed-download recovery. Hardware decoded calligraphy pages 2 and 3 using recycled buffers; the user confirmed portrait layouts on subsequent pages. Original automatic cycling was restored. First-install and per-transport coverage are not inferred from this targeted acceptance.
