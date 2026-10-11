# TAB5 .170 字库与 Codex 图标收尾

2026-10-11。已构建并通过正式候选校验，已由运行中的桥接提供给 TAB5，等待用户在设备点击升级。电脑桥接保持已安装的 0.6.4；尚未把 .170 安装或后续实机验收列为完成。

## 已完成

- 总览与模型选择页共用 30×29 可视 Codex 图案，36×36 占位不变。
- 内容任务仍在增补，使用固定 `guides-snapshot.zh.json` 验收。快照 SHA-256：`acc43f279dbb2c81c8c82960faa356059639c8b092529c8fe7388106f3891eb5`，含 1,765 件名画、867 件书法和 272 件可读原文。
- 字库增加 178 个字形、未删除原有字形，最终 8,716 个字形；快照所需字符缺失为零。Noto 原字体缺少的三个生僻字使用已校验来源的 Plangothic P1 补充，保留完整 OFL 许可及校验值。生成器在图库存在不支持的字符时失败，不再静默略过。
- 原生 LVGL 全目录真实字形查询、272 件原文及最长正文横竖屏到结尾、两页 Logo、十一供应商、未读控件回归和实际 sidecar 更新说明预览通过；ESP-IDF 编译、OTA 分区尺寸、ELF 显示/Flash 安全及版本登记检查通过。
- 原 `.169` 的正式 USB 启动核验已补齐，完整 ELF、`ota_1 VALID`、`previousError=0` 已归档。

## 精确候选

- 版本：`0.2.170-ui`；镜像：6,797,200 字节。
- SHA-256：`1d8f34f7311558c25610e66334df71bca1537ba090c9c397b4ff9b9856ef4d36`。
- ELF SHA-256：`a77b8a650dd5eebe866ae96c40ea568d48cc4d38063113e3ee59c968cc08a4ef`。
- 固定路径：`artifacts/firmware/tab5/latest/aibot_tab5.bin`。
- 归档：`artifacts/firmware/tab5/versions/0.2.170-ui/1d8f34f73115-5c04abd7/`。

## 待完成

1. 设备安装后核对精确 ELF、VALID 分区与新鲜 UI 心跳；重复电脑实际打开已有回复后的未读数字 1→0 验证。
2. 使用正常运行桥接导入两类年度图文包，保留旧版备份；在设备检验图片、简介、欣赏提示、原文及同日缓存刷新。
3. 长时间连续运行、Windows 睡眠恢复与最大分页作品仍需独立真机证据。已有 .166 三通道传输和物理拔插结果不冒充这些检查。
4. 部署结束通过正式退出、恢复轮播、启动入口确认原六页与 15 秒自动轮播。
5. 整理当前提交及正式发布范围；未推送、打标签或公开发布。

完整证据位于 `artifacts/development/upgrade-170-20261011/`。最初只使用 Noto 的回归确实因缺字失败，保留在 `gallery-ui/preview.log`；补充字形并冻结输入后的通过结果为 `gallery-ui-final/preview.log`。未用删字、改正文或放宽断言解决缺字。

## English

The .170 candidate is built, validated and offered, but device installation and final hardware acceptance remain pending. It shares the 30×29 Codex mark between overview and model selection. A frozen catalogue of 1,765 paintings, 867 calligraphy works and 272 readable originals requires 178 additional glyphs; all required characters are covered and existing glyphs remain. Three rare ideographs use hash-pinned OFL Plangothic P1 sources. Native LVGL glyph/reading regression, actual release-note preview, ESP-IDF build, OTA size and display/Flash safety checks pass. The original missing-glyph failure is preserved separately. Exact boot, live read synchronization, real gallery imports/cache refresh, longer stability/sleep/peak-work checks and final carousel restoration remain separate gates. No public release occurred.
