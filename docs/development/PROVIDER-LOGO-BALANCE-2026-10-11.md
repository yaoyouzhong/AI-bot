# 模型厂商图标视觉大小统一

后续交付：图标调整已与两项独立倒计时合并到规范 `.171` 升级包，并由桥接提供给设备；最终镜像及验证见 [TAB5 .171 验收记录](../TAB5-ACCEPTANCE-171.md)。以下为合并前仅图标阶段的记录，包大小和交付状态保留为历史证据。

总览卡片与内容轮播的模型选择列表共用 TAB5 的图标资源。本次以已确认的 Codex 30×29 为基准，收小实心底色图标，保留 36×36 透明画布、原始宽高比、颜色与页面布局。

| 图标 | 调整后可视尺寸（alpha ≥ 8） |
| --- | --- |
| Codex | 30×29，保持不变 |
| Kimi、MiniMax、智谱、阶跃、百度、小米 | 32×32 |
| Claude | 34×34 |
| 火山方舟 | 34×32 |
| 百炼 | 30×34，保持不变 |
| DeepSeek | 36×27，保持不变 |

生成规则集中于相邻 TAB5 工程的 `scripts/generate-brand-logos.py`，重新生成 `firmware/main/brand_logos.h`。像素比较确认只有上述八个需调整的资源发生变化；Codex、DeepSeek、百炼及其他快捷入口资源保持原字节。

证据目录：`artifacts/development/provider-logo-balance-20261011/`。`dimensions.json` 记录调整前后尺寸与资源散列；`ui/` 保存原生 LVGL 预览和回归日志，覆盖十一厂商卡片、分组三卡片及模型选择列表。原生回归得到 `LVGL_PREVIEW_OK`，已检查模型列表和总览的实际渲染。

本次只修改源码并本地验证；设备上的 .170 与规范升级包保持原样。原生截图不是设备照片，设备实际显示仍待安装包含本调整的后续版本后验收。没有导入或发布仍在整理的图库。

本地 ESP-IDF 编译使用 `0.2.171-ui` 标识并通过，镜像 6,797,200 字节，应用分区剩余 6%；日志为 `firmware-build.log`。本次涉及文件的 `git diff --check` 通过。该编译结果未登记或替换规范交付包，现有 .170 包 SHA-256 仍为 `1d8f34f7311558c25610e66334df71bca1537ba090c9c397b4ff9b9856ef4d36`。

## English

Delivery follow-up: the logo changes are now included with both independent countdowns in the canonical .171 package offered by the bridge. See [the .171 acceptance record](../TAB5-ACCEPTANCE-171.md) for the final image and validation. The details below describe the earlier logo-only stage.

The local ESP-IDF build passed using the development version `0.2.171-ui`; the image is 6,797,200 bytes with 6% partition space remaining. Whitespace checks passed. The canonical .170 delivery package remains unchanged.

The overview and provider picker share the same assets. Filled tiles are reduced to 32×32, Claude to 34×34 and Ark to 34×32. The confirmed 30×29 Codex mark, DeepSeek and Alibaba remain unchanged. All assets retain the 36×36 canvas, original aspect ratio and colors. Pixel comparison confirms exactly eight changed assets. Native LVGL regression and visual inspection passed; screenshots are local renders, not device photographs. This source change has not been installed or offered as an upgrade, and no gallery content was imported or published.
