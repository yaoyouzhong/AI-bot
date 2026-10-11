# TAB5 天气、模型标识与书法原文 / UI refinements

后续状态：本页以下为最初源码预览记录。天气、模型标识和原文入口后来已随.167安装，用户确认“显示正常”，并恢复原自动轮播，详见[.167验收](../TAB5-ACCEPTANCE-167.md)。进一步的原文增补、标签底色和空原文提示调整仍为本地源码，详见[原文增补与验证](TAB5-CALLIGRAPHY-ORIGINALS-2026-10-10.md)；本页历史的14件统计、.166镜像和旧提示不再代表当前源码。

Later status: .167 installed these UI changes and passed the user's display check; the original carousel was restored. The following .166/source-only evidence is historical. Subsequent text expansion, opaque tab styling and the shorter missing-text message remain local, separately documented.

范围：仅修改相邻 TAB5 工程的界面源码和原生预览；未构建设备固件、未刷机、未部署 Windows 桥接，未改用户的显示模式、轮播页面、顺序或间隔。固定交付镜像仍为原 .166，SHA-256 `7fb461cfa889c1474888e22aea861134585c64beae3c36e2137c899ef3de19a2`。

Scope: TAB5 UI source and native desktop rendering only. No device firmware build, installation or Windows bridge deployment. The user's display and carousel settings remain intact; the canonical .166 image is unchanged.

## 天气温度区 / Weather temperatures

- 最低温度采用冰蓝色 `#A5D8F4`、向下标记与浅蓝灰底；最高温度采用暖橙色 `#F2BE82`、向上标记与浅棕底。沿用当前主温度、字体与深色页面。
- 冷暖范围条上的白点表示当前温度位置。缺失、非有限、超出有效范围或颠倒的上下限显示中性轨道；当前值不在上下限内时隐藏白点。上下限相同时使用中心位置。
- 全部为静态对象；重复天气数据不会使这部分重复刷新。未新增转场或动画。

Low and high temperatures use cool/warm colors and directional marks. A static range track indicates the current value only when the data is valid. Missing/inverted bounds and out-of-range observations cannot invent a position. Equal endpoints are handled without division by zero.

## 总览模型选项 / Overview model choices

- “内容轮播 → 总览显示的模型”的全部 11 个模型选项复用总览的现有厂商标识，名称与资源按同一个供应商 ID 对应。
- 三列统一图标位置与文字间隔：勾选框至图标约 16 像素，图标至名称约 12 像素。图标按勾选框实际中心对齐，而非按整个 48 像素触摸区域居中。
- 原资源均为 36×36 的透明画布；OpenAI 图案的实际轮廓只有约 24×24。此列表从同一份高分辨率原图生成去除透明留白的变体，可见图案约 34×33，画布仍为 36×36。保留中心、原色与原始资源，其他页面不受影响。
- 点击图标、文字或该选项留白均走原来的勾选逻辑，保留自动选择、最多三个模型和保存失败回滚。

All eleven choices reuse their existing provider marks. Icons align with the checkbox center. Codex uses a list-specific variant generated from the same high-resolution OpenAI source after removing transparent margins, without runtime scaling. The original selection, maximum-three and failed-save behavior remains in place.

## 书法原文 / Calligraphy original text

- 原文放在“作品详情”内，章节为“简介 / 欣赏提示 / 原文”；屏保画面保留原有按钮布局。
- 原文保留已经核实的原字形；释义另起段落并明确标识。阅读可滚动，横竖屏、反向显示与返回原作品、原页码的行为沿用原实现。
- 目前资料库有 867 件书法，14 件正文标记为 `source_verified`。本次没有新增未经核实的转录。其余作品的“原文”页显示“这幅作品的原文尚未核实。可先阅读简介与欣赏提示。”
- 阅读使用现有、与作品 ID 和日期绑定的本地导读缓存；打开详情或切换到原文不另行请求图片。

Original text lives inside Artwork Details, alongside Introduction and Viewing Tips. Verified original characters and explanation are separated. Four-direction layout and exact-frame return are preserved. Fourteen of the 867 calligraphy works currently have reviewed transcriptions; unavailable text is explicitly identified rather than substituted with guesses.

## 验证与预览 / Validation and previews

证据目录：`artifacts/development/weather-model-logos-20261010/`。运行目标为相邻 TAB5 的 `.tools/ui-preview166/ui-preview.exe`，不是 ESP32 固件；测试开启真实书画 RGB565 样本、完整导读目录及绘图内存预算检查。

- 天气：负温度、相同上下限、端点、当前值越界、颠倒/无效/缺失上下限，以及重复数据不刷新。
- 模型：全部 11 个图标与供应商对应，图标中心与勾选框中心误差不超过半像素，24/30 字号名称宽度检查，实际指针点击图标勾选/取消。
- 书法：四方向实际指针操作“作品详情 → 原文 → 返回作品”，离线缓存、页码和身份绑定；全目录字形及全部 14 件已核实正文、真实长文本横竖屏渲染。
- 保留完整 `preview.log`；原生预览与 Windows 导读数据检查不代表设备安装或真机验收。

最终原生预览运行退出码为 0；`WEATHER_TEMPERATURE_OK`、`OVERVIEW_OPTION_LOGOS_OK`、`GALLERY_ORIGINAL_OK` 与 `LVGL_PREVIEW_OK` 全部通过。最新间距另外断言全部 11 项的勾选框与图标间隔至少 16 像素。Windows 现有 Release 程序的 `--self-test-art-guides` 返回 `ART_GUIDES_OK`（2632 件作品、14 件已核实正文）。源码与日志哈希保存在证据目录的 `verification.json`。

The final native run exited successfully and passed all four UI validation markers, including a minimum 16-pixel checkbox-to-icon gap for all eleven choices. The existing Windows Release catalogue check also passed. Source and log hashes are recorded in `verification.json`.

Evidence contains native renderer screenshots and the complete assertion log. Checks cover temperature edge cases, real provider-icon taps, four-direction Details/Original/Return interaction and all reviewed text glyphs. Native checks and catalogue validation do not substitute for later hardware acceptance.

预览：

- [天气温度区](../../artifacts/development/weather-model-logos-20261010/after/weather.png)
- [总览模型标识](../../artifacts/development/weather-model-logos-20261010/after/models.png)
- [书法原文与释义](../../artifacts/development/weather-model-logos-20261010/after/gallery-original-reviewed.png)
- [书法原文竖屏](../../artifacts/development/weather-model-logos-20261010/after/gallery-original-reviewed-portrait.png)
