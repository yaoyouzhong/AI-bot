# 本地 .137 显示总线修正候选

2026-10-07。已安装；用户确认仍持续闪动并伴随蓝闪，实屏验收失败。

## 依据

.136 精确镜像启动正常，但用户确认快速闪动，退出电脑桥接后仍然闪动。降低像素时钟未解决问题，电脑同步不是持续闪动的必要条件。设备诊断已识别面板为 ST7121。

M5Stack 的 [M5GFX 修正 27483fc5](https://github.com/m5stack/M5GFX/commit/27483fc5be60fff7f82819e3aaddbe946adf2189) 将 TAB5 ST712x DSI 总线从 960 改为 1040 Mbps。该提交标题提及 ST7123；同时核对了当前官方 [ST7121 配置](https://github.com/m5stack/M5GFX/blob/f3e51bd12b8986b6a7f7a7e14c6d687403e820e0/extras/board_spec/generated/resolved/m5stack_tab5%2Blcd%3Dst7121.json)，同样使用双通道 1040 Mbps、DPI 请求 70 MHz，横向时序 2/40/720/40，纵向 20/24/1280/200。

本地 ESP-IDF 5.4.2 的 PLL 计算会将原 BSP 的 965 Mbps 请求取整为 960 Mbps。此候选采用官方 1040 Mbps，同时撤回 .136 的 60 MHz DPI 请求，恢复官方 70 MHz 请求（此 IDF 中实际为 80 MHz、分频 3）。ILI9881C 配置、横竖屏映射和作品布局保持原样。配置差异已核实；尚不能据此宣称蓝闪根因已被证实或修复。

## 验证与交付

- 本地完整固件构建、扫描 ISR/调用链检查、完整原生 LVGL 回归通过。正式包校验器完成归档，实际 sidecar 首屏与滚动预览已检查。
- 镜像 6,527,312 字节，SHA-256 `efb570fa06c894c18d0ebfd88fe6c92d1776e82b6e1f62dd52d5cdbe27aeaf55`；ELF SHA-256 `9fe0f9adc6cdab80ea840bdd64bbfed11646293c7507c9de3d3dc9eca8b194e2`。
- `displayDiag` 新增行时序 shadow 寄存器 `hline`。按 HAL 计算，配置应为 1303，但实机此 shadow 寄存器返回零，原时序断言未通过；该读数不能用于确认总线时序。后续 .138 将改读实际配置寄存器。
- 用户手动退出桥接，准备候选期间保持退出。交付时继续使用原固定运行路径，不更改防火墙配置。
- 固定 OTA 路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`；历史候选保留。公开 .133 未替换或发布。

后续必须核验设备精确 ELF、VALID 分区、实际面板/时钟/行时序，并由用户观察普通界面静止、连续输入、名画和书法屏保；方向切换及原自动轮播同样保留。设备健康计数不代表显示验收通过。

## 实机结果

设备身份、镜像 ELF 和 VALID 分区核验通过，ST7121 实际 DPI 为 80 MHz、分频 3。原核验因为 `hline=0` 失败，另做身份/启动专项核验并明确保留 `hostLineTimingVerified=false`，不把该时序断言改成通过。用户先后反馈“还是不停在闪”“屏幕不停闪的同时，还伴随着蓝闪”。总线速率调整未解决问题；转入限时硬件彩条隔离，区分 DSI host/面板与绘制/帧缓冲链路。桥接恢复原六页、15 秒自动轮播。

用户随后补充：普通闪动已比之前好很多，仍有轻微闪动；蓝闪持续。按此记录为部分改善，不能判定整体验收通过。

证据目录：`artifacts/development/gallery-preview-sync/`。

## English

This candidate also failed physical-screen acceptance: the user reports rapid flashing with blue flashes. Build and exact-image VALID boot passed; ST7121 reports 80 MHz/divider 3. The hline shadow register reads zero, so its timing assertion failed and remains unverified. A separate identity-only check explicitly records that limitation. Next is a timed host-generated color-bar isolation test. Public .133 remains unchanged.
