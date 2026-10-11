# TAB5 0.2.175-ui 返回、标题与显示模式

2026-10-11，用户授权将返回方式、标题位置和显示模式三项调整合并打包，提供给 TAB5 升级并检查实际效果。

## 内容与本地验证

- Codex 直达与设置共用左上角返回箭头；从设置、应用详情及嵌套设置经直达返回后，恢复原入口和上一级目标。
- Codex 直达标题放在返回按钮右侧，与设置保持 30 px、左对齐布局。
- 内容轮播、固定页面和自动切换互斥，只显示对应参数；选择后停留设置页，返回后显示目标页面。
- 模式切换保留轮播页面、顺序和间隔；普通浏览和切换表盘保留自动／轮播模式。保存失败回滚控件及偏好，保护最后一个轮播页面。
- 原生指针交互、完整 LVGL 回归、模式保存失败、自动音乐切换、全部 11 个模型图标操作通过；ESP-IDF 构建与 ELF 显示／Flash 安全检查通过。
- 对照最终源码和已构建镜像 SHA-256，没有重建或替换电脑桥接。使用生产镜像校验器生成规范升级包，旧 `.174` 包保留在归档。
- 使用规范包的实际 sidecar 完整重跑原生回归，`OTA_ACTUAL_SIDECAR_PREVIEW_OK` 和 `LVGL_PREVIEW_OK` 通过；已检查首屏和滚动后的说明截图，三项分类及列表完整可读。

源码及交互证据：`artifacts/development/display-mode-20261011/verification.json`、`preview.log`、`display-flash-safe.json` 和 `artifacts/development/navigation-return-20261011/`。本次交付证据位于 `artifacts/development/tab5-upgrade-175-20261011/`。

## 镜像身份

| 项目 | 值 |
| --- | --- |
| 版本 | `0.2.175-ui` |
| 规范镜像 | `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| 字节数 | 6,897,536 |
| 镜像 SHA-256 | `e99ca3f3816b7888c03cedb5a3242821895932cbfbeb64a51939b258aa3d0e97` |
| ELF SHA-256 | `78b159749a0ab9930b58e311456a0459901c81dd11ee9fb82bdc892dfc5c54ad` |
| 说明 SHA-256 | `60cb3cbd9fdfb5f2048103e7f5b2a21bd671209cb8806bfaf31ade34d046c94c` |
| 说明 UTF-8 字节数 | 616 |
| 归档 | `versions/0.2.175-ui/e99ca3f3816b-60cb3cbd` |

## 设备交付与验收边界

规范升级包已准备。电脑桥接窗口最初显示 `.174`；在检查实际说明期间窗口发生用户操作和最小化。恢复只读观察后，现有桥接显示“0.2.175-ui · 设备已运行此版本”，更新说明为返回导航、标题布局和显示模式，与规范 sidecar 一致。随后再次读回同一新版本状态，证据为 `bridge-observation.json`。没有在设备已经回报新版本后重复加载或触发升级。

最初读回时 Wi-Fi／蓝牙已连接，USB 等待重新连接，先前的启动核验仍在进行；下一次读回提示运行镜像、分区或启动确认状态与所选固件不符。核验函数在开始时捕获候选包，不能用候选切换期间的旧核验结果代替当前包核验。

USB 恢复后，在现有桥接按当前 `.175` 包重新点击只读“核验启动（USB）”，随后读回“启动核验通过。”及 USB／Wi-Fi／蓝牙全部已连接。生产核验检查三个连续启动响应、设备与版本、显示心跳、镜像 ELF 指纹、运行 OTA 分区及 VALID 状态，当前包验证已通过，记录于 `bridge-observation.json`。没有重复刷机或为验收切换设备页面。用户明确继续在设备检查实际效果，本轮保留其当前页面与设置；实际目视／触控结果仍待反馈。

本轮没有修改设备展示设置、轮播页面／顺序／间隔，没有导入图库或替换桥接二进制。没有提交、推送、标签或公开发布。

## English

The authorized .175 package combines consistent back navigation, a left-aligned Codex Direct title beside the back arrow and three explicit display modes. Native interaction regressions, ESP-IDF build, display/flash safety and production image validation passed. The canonical package and version registration now identify .175, with the previous image archived.

The actual sidecar passed the full native preview, and its first/scrolled screenshots were inspected. After user activity in the existing desktop window, read-only observations reported .175 running on the device with matching categorized notes. An earlier probe across candidate replacement failed; after USB reconnected, the production USB verifier was rerun against the current .175 package and reported success with all three links connected. It checks consecutive boot responses, device/version, display heartbeats, ELF identity, the running OTA partition and VALID state. No duplicate firmware selection or device update was triggered. Physical appearance/touch acceptance remains pending, and the user's current page/settings were retained for the requested inspection. No device display preferences, desktop binaries or gallery content were changed; no public release.
