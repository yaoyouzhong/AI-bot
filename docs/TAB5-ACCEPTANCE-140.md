# 本地 .140 显示回归修复候选

2026-10-07。本地完整版，尚未公开发布；构建和原生回归通过，精确镜像 `ota_1 VALID` 启动已核验，但用户确认“又开始蓝闪了”。本候选实屏验收失败，恢复字体与显示路径不足以解决问题，不能认定字体压缩为根因。

## 对照依据

- 原始 `.125` 恢复普通界面及已显示内容的稳定性，但名画仍等待同步且不含竖屏，不能视作完整通过。
- 原始 `.131` 镜像 SHA-256 `3a1b3655f4becc544575df4871a965f4a578dd4552a077444728ec90a03d0125`，精确 ELF、`ota_0 VALID`、UI 心跳与方向传感器核验通过。用户确认：“不再蓝闪，两类图片和转向都正常”。
- 回归发生在 `.131` 之后；字体压缩及放宽字形缓存条件是主要嫌疑，尚未单独证明哪个改动是根因。`.136` 至 `.139` 显示调参未解决问题。

## 本次改动

- 两套中文字体、字形缓存、BSP 显示配置及帧统计实现恢复为已验收 `.131`（源码 `87881e1`）。保留全部字形，不缩减字符覆盖。
- SDK 恢复固定版本的原始扫描驱动及原有诊断补丁，撤回 `.139` 重启顺序/中断优先级、AXI 优先级实验。原 IRAM、PSRAM XIP 和显示错误检查保留；不再要求已经撤销的实验调用顺序。
- 撤回临时硬件彩条接口与每帧 DSI host 状态采样；对应实验和实测证据保留在本地 artifacts。
- 保留 `.134` 自动屏保期间忽略延迟后台翻页事件的修复；保留 `.135` 当前作品缓存和解码诊断，切换类别先释放另一类别的缓存。
- 进入两类艺术屏保时释放可重建的后台页面缓存，避免挤占整张 RGB565 图片所需的连续内存。现有后台缓存定时器在屏保期间暂停重建；不修改正常页面、图像和方向布局。

## 验证

- 固件构建与分区尺寸检查通过：7,207,312 字节，OTA 槽位 7,208,960 字节，余量 1,648 字节。
- 字体、字形缓存、BSP 与帧统计源码和 `.131` 基线一致；扫描 ISR/调用链的 IRAM 及 XIP 链接检查通过。
- 新增真实页面缓存生命周期回归：修复前失败，修复后进入艺术屏保缓存归零，屏保期间不重建。原生完整 UI 测试通过。
- 用实际横竖屏图片运行两类别的正常屏保时钟、延迟翻页事件和四方向回归；JPEG DMA 测试覆盖脏缓存尾部、完整图像、分配/同步/解码失败拒绝。
- 固定 OTA 路径及历史归档使用正式包校验器生成；实际 sidecar 首屏及滚动预览已检查，分类和完整条目可读。12:22 提供给 TAB5，随后核验精确镜像和 ELF、`ota_1 VALID`、三次正常 UI 心跳及 IMU；用户随后报告仍然蓝闪，物理验收失败。原始 `.131` 已重新提供作稳定回退。

镜像 SHA-256：`35f2caf0ff4f77f15fb5e32f39993cf8f088233a2efdd76870ab735dca52c90f`。

ELF SHA-256：`8c69f11483d8588da189e837bc26b6fb273ff7c5de599e2d865bafb6628b189d`。

证据目录：`artifacts/development/gallery-preview-sync/`；回退 `.131` 原始镜像保留。安装后须观察普通界面、连续输入、名画/书法预览及自动屏保四方向。编译、方向模拟或零错误计数均不能替代实屏无蓝闪结论。

## 容量与发布

本版适合本地验证，容量问题尚未解决。不沿用原压缩方案的容量收益。现有公开 `.133` 含可疑字体压缩，必须重新构建、完成对应验收再准备发布；本次不推送、不打标签、不发布。图库仍只保留本地。

## English

Local .140 restores the font, glyph-cache and display path accepted in the exact archived .131 regression control. The user confirmed both art categories and orientations worked without blue flashes on .131; the individual root cause after that version is not yet proven. Ineffective scanout tuning and temporary hardware-pattern diagnostics are withdrawn. Automatic-screensaver rotation, inactive-category cache release and image diagnostics remain. Entering either art saver now drops optional background page snapshots before decoding; the new real-cache regression fails before the fix and passes afterward.

Firmware/size checks, native UI/gallery regressions, JPEG DMA safety and linked IRAM/XIP checks pass. The 7,207,312-byte image leaves only 1,648 bytes in the existing slot. Exact-image/ELF ota_1 VALID boot, UI heartbeats and IMU were verified, but the user reported blue flashes again. Physical acceptance failed: reverting fonts and display tuning was insufficient and does not establish compression as the cause. Original .131 was offered again as the stable fallback. Existing public .133 remains on hold pending renewed diagnosis and physical validation; no push, tags or publication occurred.
