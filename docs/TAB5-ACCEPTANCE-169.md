# TAB5 .169 总览 Codex Logo 调整

2026-10-11。用户已在设备安装 .169，运行诊断确认该版本；电脑桥接保持 0.6.4。正式 USB 启动核验已通过：完整 ELF 与候选一致、`ota_1 VALID`、`previousError=0`，设备记录安装 19,242 ms。用户先确认 Logo 正常，随后指出它相对 DeepSeek／智谱偏大，后续 30×29 调整另记入 .170。

## 修改与预览

总览初始化和按供应商更新时统一复用模型选择列表的 `brand_openai_option_image`。36×36 画布、对齐、文字位置和其他厂商图标保持原样；按 alpha≥8 测量，Codex 可见边界从 26×26 调整为 34×33。原图来源及像素不重新绘制，运行时不增加缩放。

[总览原生预览](../artifacts/development/upgrade-169-20261011/ui/overview-brand-group-0.png)。预览为本机构建运行的 LVGL 界面，不是物理设备照片。

## 候选与已完成验证

- 镜像 `0.2.169-ui`，6,774,368 字节。
- SHA-256：`817423d6bce27cfd8ee84a0cdcddd89fe32da459fda92a1293bff047527b7d67`。
- ELF SHA-256：`808b9b7cadba374c8a69d12f56fc89722b7b205fb6b64bbec7d8546a9d792a14`。
- 固定包 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，正式镜像及实际 sidecar 校验通过；原生全回归、十一种供应商路由、实际更新说明预览、ELF 显示/Flash 安全检查、版本登记与 `git diff --check` 通过。
- 桥接正常退出，经原 Release 的 `--restore-cycle-default` 恢复后重新启动，读取 `/status` 确认自动模式、轮播开启、原六页顺序和 15 秒间隔；原用户资料与配对一致。
- .168 已完成电脑实际打开回复后的真机角标 1→0 验证，见[.168 记录](TAB5-ACCEPTANCE-168.md)；最新固件的该流程复测仍待最终部署收尾。

证据保存在 `artifacts/development/upgrade-169-20261011/`。未公开发布，图库内容包仍独立交付。

## English

The .169 image is installed. Official USB boot verification confirms the exact ELF, ota_1 VALID and previousError=0; device installation took 19,242 ms. The overview mark changed from 26×26 to 34×33 within its 36×36 canvas. After initially confirming the display, the user requested a smaller shared 30×29 mark, tracked in .170. The six-page, fifteen-second auto carousel remains correct; the final firmware still needs its own desktop-read-to-badge regression.
