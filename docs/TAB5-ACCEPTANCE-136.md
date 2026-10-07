# 本地 .136 蓝闪排查候选

2026-10-07。已安装并完成精确镜像启动核验；用户报告界面快速闪动，实屏验收未通过。

## 依据和改动

用户在 .135 确认名画可以显示，但静止时反复闪屏，随后确认书法预览也持续蓝闪。视频第 245 帧（8.17 秒）可见整屏泛蓝及作品、日期错位重影，下一帧恢复。同期设备诊断未发现重复下载、解码、方向变化或背光设置变化，桥接 FIFO 欠载计数为零。

用户进一步报告普通系统界面同样蓝闪，输入时更严重。11:13 的现场诊断确认设备仍运行 .135，`saverActive=0`，并非 .136 的验收结果。问题范围因此扩大为全局显示输出；输入相关重绘/内存流量与闪烁加重存在用户观察到的关联，但尚无同步硬件追踪证明因果。本候选的 BSP 扫描时钟调整作用于全部页面，不局限于艺术屏保。升级后的实屏验收必须包含普通界面静止、连续输入和两种屏保。

现场 IDF 5.4.2 的 `mipi_dsi_hal_host_dpi_calculate_divider` 采用整数截断。ST712x 配置请求 70 MHz、默认源 240 MHz，实际得到 80 MHz，而不是 70 MHz。本候选改为可精确分频的 60 MHz，ST7121/ST7123 扫描约从 65/66 Hz 降至 49/50 Hz，以降低扫描带宽和时序压力。ILI9881C 原本即 60 MHz，保持不变。未更改方向映射、布局、缓存同步或桥接安装路径。频率差异已由源码核实；它是否是本次蓝闪根因尚未确认。

新增 DSI host 两个错误状态寄存器的逐帧累计记录，并报告 BSP 已识别的面板、实际时钟和分频。原欠载计数仅覆盖 DSI bridge FIFO，不能作为面板或 host 链路无错误的证明。诊断只读取状态，不重置硬件、不掩蔽错误、不逐帧打印日志、不读取图像或凭据。

## 验证与交付

- 本地完整固件构建通过；ELF 扫描中断及调用链检查通过，保持在 IRAM。
- 完整原生 LVGL 回归通过；使用真实 sidecar 的升级说明预览通过并检查截图。用户通过设备 Wi-Fi 升级安装该镜像。
- 镜像 6,527,296 字节；SHA-256 `21c588f14788896c88af283acf32f4ef29df12cfa13ba60f6946cc50b05e0feb`。
- ELF SHA-256 `14f2b21e30b02bc36906fc131571d9196d3c4b5d9d4d1b84b38fe5991dfb8bed`。
- 固件与说明已经正式包校验器验证并归档。设备精确镜像启动、面板和时钟核对已完成，蓝闪实屏验收失败；历史包保留在 `artifacts/firmware/tab5/versions/0.2.136-ui/`。
- 原桥接固定运行路径和六页、15 秒自动轮播保持；公开 .133 不替换、不发布。

证据目录 `artifacts/development/gallery-preview-sync/`。M5Stack 官方知识库未提供此组合的专项诊断；依 m5stack-assistant 技能提交请求/实际 DPI 时钟差异与诊断资料反馈，受理编号 `053a3457bfed45dabb8769180cfaa20c`。技术参考：[Espressif MIPI DSI FAQ](https://github.com/espressif/esp-iot-solution/blob/master/docs/en/display/lcd/mipi_dsi_lcd.rst)。该 FAQ 区分时序失配与伴随欠载日志的蓝闪，不能直接据此判定本机根因。

## 实机失败结果

用户升级后报告“界面在快速闪动”。设备和 ELF 精确匹配本候选；面板明确为 ST7121，实际 60 MHz、分频 4。三次 USB 启动样本健康，镜像已 VALID，显示错误/欠载为零。DSI host 只记录一次 `host0=0x100000`，`host1=0`，后续没有持续增加；这不能代替实屏结果。图片尚未加载（`gallery stage=0`）时普通界面已经闪动，因此不是艺术图片下载/解码才能触发的故障。降低扫描时钟未解决问题，不能作为最终修复合入发布。

11:17:33–11:18:04 正常退出固定路径桥接进行隔离，随后恢复原自动模式、页面顺序和间隔。用户确认“暂停桥接后仍一样闪动”，之后手动退出桥接。电脑同步不是持续闪动的必要条件；继续检查设备显示驱动。隔离日志保存在 `bridge-isolation.log`。

## English

This local diagnostic candidate failed physical-screen acceptance. Build, package validation, IRAM call-chain checks and exact-image VALID boot passed. Hardware identified ST7121 with a 60 MHz clock and divider 4, but the user reported rapid flashing across normal UI and screensavers. The user also confirmed flashing persisted during a 30-second bridge shutdown. Lowering the pixel clock did not resolve the issue. Public .133 remains separate.
