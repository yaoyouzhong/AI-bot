# Codex 直达与设置返回一致性

2026-10-11。设备源码位于相邻 `m5stack TAB5` 工程。本次针对返回入口的可见差异及临时进入直达后上一级目标被覆盖的问题；不涉及 Windows 桥接、图库或通信协议。

## 修改

- Codex 直达改为与设置、应用详情共用的左上角箭头按钮：`(30,12)`、`52×56`、Montserrat 24 px 图标，默认／按下颜色与圆角均复用公共控件。设置页的布局不变。
- “Codex 直达”标题移至返回按钮右侧 `(94,21)`，左对齐、30 px，与设置页标题保持一致。
- 进入直达时保存原页面、主导航、设置返回目标及详情返回目标，关闭后恢复。原来的忙碌状态操作锁保留。
- 修复“设置 → 直达 → 设置”之后再返回会误入 Codex 的路径；修复从总览打开应用详情后，经直达返回再点击返回误入应用列表的路径。

## 验证与边界

新增业务回归先在旧实现复现失败：`repro-preview.log` 的 `current_page==primary_pages[origin]` 断言失败，确认设置页原始目标被覆盖。修复后验证通过：

- `QUICK_RETURN_CONTEXT_OK`：从四个主页面进入设置，经过直达后仍返回各自原页面；总览详情及详情嵌套设置的上一级目标不被覆盖。
- `QUICK_RETURN_POINTER_OK`：原生 LVGL 实际指针点击“应用 → 电脑状态 → 设置 → 直达”，连续点击左上角箭头依次返回设置、电脑状态、应用。
- `QUICK_NAV_BUSY_UI_OK`、`QUICK_CONSOLE_UI_OK`、`LVGL_PREVIEW_OK`：原有忙碌锁、语音草稿／发送及完整页面回归通过。保留完整 `preview.log`。
- 原生修改前后截图已检查；两页按钮内区的箭头、背景和文字颜色逐像素一致。对比页的四幅图片引用有效。
- 补充标题左对齐后，完整原生回归再次通过，已检查新的标题字形、位置及与返回按钮的间距；对比预览同步更新。
- `scripts/build.ps1 -LocalArt -LocalVersion '0.2.175-ui'` 最终开发构建通过，镜像 6,895,168 字节，SHA-256 `54db9111579e6fd8e08ce8c3297839ea4f1302d70d260e5b90b4b52b50641e2d`。仅保留既有 `weather_icon_create` 未使用警告；应用分区余量 313,792 字节。
- `DISPLAY_FLASH_SAFE_ELF_PASS` 通过，证据为 `display-flash-safe.json`；两个仓库的 `git diff --check` 通过。

[修改前后对比](../../artifacts/development/navigation-return-20261011/preview.html)使用实际原生渲染截图，不是设备截图。

实现阶段仅完成本地实现、验证与开发构建，当时固定升级入口和 `versions/TAB5` 保持 `0.2.174-ui`。随后用户授权将返回、标题和显示模式合并打包，规范升级包与版本登记现为 `0.2.175-ui`，最终合并镜像身份见 [交付记录](../TAB5-ACCEPTANCE-175.md)。现有桥接已回报设备运行 `.175`，按当前包重新执行 USB 启动核验通过；目视／触控验收仍待完成。没有替换桥接二进制、修改设备展示设置、提交或公开发布。

Codex Direct now uses the existing Settings back control, places its 30 px left-aligned title beside that control, and preserves nested navigation destinations. After subsequent delivery authorization, this change was bundled with the display-mode changes in the canonical .175 image. The existing bridge reports .175 running on TAB5, and the production USB boot verifier passed against the current package. Physical acceptance remains pending. No bridge binary replacement or public release.

证据目录：`artifacts/development/navigation-return-20261011/`。
