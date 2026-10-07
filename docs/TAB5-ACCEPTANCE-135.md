# 本地 .135 图片同步与未完成的闪屏验收

2026-10-07。本地艺术完整版；公开 TAB5 候选仍为 .133。

## 修改与验证

名画预览曾停在“等待电脑同步图片”。本次在加载另一类别前释放非当前类别的 RGB565 缓存，保留当前类别的最近成功图片，并增加接收、校验、分配和解码阶段诊断。此前版本没有这些诊断，不能据此断言原故障一定由内存不足造成。

- 本地完整版固件与 Windows 桥接构建通过；公共配置桥接 Release 构建通过。
- 原生 LVGL 回归、类别切换内存回归、JPEG DMA 缓存同步及失败处理回归通过；RPC 模拟传输回归通过。这些测试不能代替实屏验收。
- 镜像为 6,526,912 字节，SHA-256 `9561b5579b6f937e33f04d7dc8651b99daa06b3bc15b285c137c559d3683248b`。
- 用户升级后，USB 精确镜像启动核验通过：`ota_0`、`VALID`，ELF SHA-256 `49ab2a686c9911bf1ba9e9600e06b42c4e1fadd639e3972754b0f97bc6af0099`。三次采样界面心跳正常，显示错误及欠载为零；方向传感器就绪。
- 用户确认“名画已显示”。设备诊断记录横、竖两张图片成功解码，`stage=9;error=0;attempts=2;successes=2`。
- 已恢复原自动模式、六页顺序和 15 秒轮播间隔。

## 尚未通过的实屏验收

用户随后报告名画闪屏，并明确“设备不动、画面静止时也反复闪”。10:54:16–10:54:46 的七次采样显示图片下载/解码计数、方向、背光设置次数和亮度均保持不变，显示欠载为零。此证据只能排除该采样期间的重复图片加载和背光设置变化，不能排除屏幕扫描、面板或未记录的显示问题；静态闪屏原因及修复仍未确认。

本版本的“等待同步”现象已获用户确认恢复，但整体艺术屏保显示验收尚未通过。不得将健康计数或编译成功写成闪屏已解决。

后续用户视频（30 fps）第 245 帧、约 8.17 秒处捕获整屏泛蓝、作品及日期错位重影，下一帧恢复；用户随后确认书法预览也持续蓝闪。由此将排查范围收敛到两种屏保共用的显示输出路径，不能仅归因于单张图片或背光亮度。提取证据在 `video/flash-245.jpg`；本地 .136 候选及尚待完成的验收见 `TAB5-ACCEPTANCE-136.md`。

## 本地运行路径

完整桥接继续从 `artifacts/development/tab5-ble-ota/bridge-all-transports/AIBotBridge.exe` 启动。该固定路径已有 Windows 防火墙规则；诊断构建先在临时输出目录完成，再正常退出桥接并替换固定目录内的程序文件。不要直接从新构建目录启动另一份日常桥接，以免再次触发按程序路径识别的权限提示。没有修改防火墙配置；原二进制保存在本次证据目录的 `runtime-backup-*` 中。

证据：`artifacts/development/gallery-preview-sync/`，包括 `boot-verified.json`、`gallery-device-success.txt`、`flicker-samples.json`、构建和回归日志。固定固件交付仍为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。

## English

Local full-art .135 releases inactive-category image buffers before allocating the next full-screen decode, retains the active category's last successful image, and exposes gallery worker diagnostics. Builds, native UI/cache/DMA regressions and simulated RPC checks pass. Exact-image boot verification confirms ota_0 VALID with the matching ELF hash. The user confirms paintings now display; both landscape and portrait images decoded successfully. However, the user reports repeated flicker with a stationary device and static picture. Seven samples show stable image-work, orientation and backlight-setting counters with no reported underruns; this does not prove the physical display is healthy. Flicker remains unresolved and screen acceptance is incomplete. The full bridge uses its existing fixed runtime path and the original automatic carousel settings are preserved. Public .133 remains separate.
