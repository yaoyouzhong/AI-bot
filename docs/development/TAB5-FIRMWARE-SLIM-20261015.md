# TAB5 0.2.197-ui 无损减包记录

本轮仅生成本地候选，未刷机、安装桥接、提交、推送或公开发布。实际固件工程为相邻 `tab5-178`；继续搭配已打包的 Windows 0.6.7，不要求重新打包桥接。

| 项目 | 196 候选 | 197 瘦身候选 |
| --- | ---: | ---: |
| 应用 BIN | 7,208,928 B | 6,685,184 B |
| 每个 OTA 分区 | 7,208,960 B | 7,208,960 B |
| 剩余空间 | 32 B | 523,776 B（511.5 KiB） |

净减少 **523,744 B，约 7.27%**。分区表与 196 源码归档逐字节一致；保留双 OTA 分区、2 MiB 桌宠缓存及 NVS。当前可继续沿既有应用 OTA 路径验收，无需分区迁移。

## 改动与保留范围

- 24 号字体按 Unicode 顺序重排已有描述符和压缩位图，用 sparse-tiny 映射省掉重复的 glyph ID 数组。不重新栅格化、不删字、不改变字号、字距、基线或透明度。保留 10,535 个字符，30 号字体的 7,552 个字符和原始文件保持不变。生成器的保留流程也应用相同精简，避免以后补字恢复冗余。
- 55,365 条离线拼音记录改用 11 字节行：24 位拼音偏移、24 位文字偏移、24 位权重及 16 位音节边界；两份字符串池共享完整 NUL 结尾后缀。生成器严格验证容量及数值范围，超限报错，不截断词条或权重。查询仍直接读取 Flash，返回的词条仍为静态字符串，无整库解压或常驻词库缓存。
- 小品牌图标使用已启用的 LVGL LZ4 无损解码路径；已有大图标、月历花卉和键帽原本已压缩。新增小图标每次解码的像素缓冲为 5,184 B，由已有解码器关闭流程释放；没有新增长期图片缓存。15 个图标的尺寸、颜色、透明度和解码像素逐字节一致。
- 本轮 `firmware/main` 只改变 `tab5_font_24.c`、`tab5_pinyin_dict.inc`、`tab5_ime.c` 和 `brand_logos.h`。日程、监控、图库、声音、三通道、持久化等实现与本轮开始快照一致。静态 `.dram0.data`、`.dram0.bss`、`.dram1.bss` 的 ELF 大小保持不变。

## 验证

完整证据保存在 `artifacts/development/tab5-lossless-slim-20261015/`，含原始字库／词库／BIN／ELF／map 快照、构建日志、界面截图、源码和升级归档及校验清单。

- ESP-IDF Release 构建、原有分区容量检查和正式桥接镜像加载器通过。
- 字库对照遍历 U+000001 至 U+10FFFF，覆盖旧字库的所有码位，逐个比较可渲染字形的度量和 A8 像素；18,597 个有像素的渲染结果一致（含 LVGL 对特殊码位的处理）。
- 拼音逐条比较 55,365 条记录全部字段；204,756 次查询对照覆盖完整拼音、大小写、1/4/48 候选容量、音节分隔、单／双字母前缀、无效输入。结果、权重、排序和消耗字符数一致。
- 15 个图标各执行 16 次真实 LVGL 解码打开／关闭，逐字节比较像素。
- 日程 90 个跨语言时间夹具、实际 worker 的存储／同步／断网重试／自动结束、真实 LVGL 点击与滚轮／输入法／语音草稿分支通过。worker 的硬件依赖由测试替身提供，不代表真机语音或 RTC 已验收。
- 完整原生界面回归、监控夹具及离线防重绘通过；RGB565／字形绘制 24,008 个像素对照和旋转 2,352 个 LVGL 对照通过。
- 已用实际 197 sidecar 生成更新说明界面并核对；两个仓库 `git diff --check` 通过。预览改用独立构建目录，避免共享 `.tools` 下其他 checkout 的 CMake 缓存冲突。

## 交付与真机边界

- 固定升级入口：`artifacts/firmware/tab5/latest/aibot_tab5.bin`。
- 版本：`0.2.197-ui`。
- BIN SHA-256：`c6fd83ac63ab0f3252f7191694313a6f1f31be8556489808de79e9ce26686302`。
- 历史归档：`artifacts/firmware/tab5/versions/0.2.197-ui/c6fd83ac63ab-ad90a4cf/`，196 原归档保留。
- 开发证据目录中的升级 ZIP 含相同 BIN、实际说明、许可证、来源和 SHA-256 清单；源码 ZIP 包含当时的未提交源码快照。

尚未刷机。下午仍须按 196 验收清单在真实设备上验证日程提醒、中文输入、语音、图库和原轮播；以实际 197 版本与 OTA VALID 读回记录收尾。构建和模拟对照不能代替设备性能、录音、声音和稳定性验证。

## English

Local TAB5 0.2.197-ui reduces the application from 7,208,928 to 6,685,184 bytes, freeing 523,776 bytes in each unchanged 7,208,960-byte OTA slot. Preserve all glyphs/metrics, all 55,365 Pinyin entries/weights/order, existing partitions, settings and the 196 feature set. Compact font maps and dictionary storage and losslessly compress small icons using the existing decoder. Unicode/pixel checks, 204,756 lookup comparisons, icon decoding, native UI/agenda/monitor/rendering regressions and the production image loader pass. No device was flashed; physical acceptance remains pending. The existing Windows 0.6.7 candidate remains the companion bridge.
