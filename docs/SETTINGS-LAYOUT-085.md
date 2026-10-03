# TAB5 085 设置布局 / Settings layout

2026-10-02。用户确认 084 亮度数字及保存功能正常，随后要求统一亮度与提示音量的布局、缩短屏保/连接方式/显示模式三个下拉框，并整理屏幕与声音页面。085 已刷入，用户回报 `BOOT_085_VERIFIED`。真实启动核验通过，自动轮播已恢复。用户随后指出布局仍不协调，尤其是连接页，布局验收未通过，继续修订为 086。

## 变化

- 屏幕亮度与提示音量使用相同的“名称 → 百分比 → 滑块”三列布局，数值右对齐，滑块位置、宽高及触控扩展范围一致。
- 屏幕与声音内容由 900 px 收到 468 px 视口内，一屏包含亮度、屏保、音量、静音、试听与完成/等待提醒。删除重复的声音分区标题和多余说明，使用两条分隔线分组；保存/错误反馈仍保留。
- 屏保、连接方式、显示模式三个下拉框从 598 px 缩为 300 px，左边界保持一致；48 px 高度、选项及事件保持。轮播间隔控件原本为 300 px，不改其设置逻辑。
- 点击顶部声音图标直接打开这张完整设置页，不再滚到旧的下半页。亮度与音量的实时预览、松手保存、静音、试听及错误反馈语义保持。

修改位于相邻 TAB5 仓库的 `firmware/main/ui.c`、`firmware/CMakeLists.txt`，预览脚本移除旧的 420 px 音量区域滚动。本次没有修改 Windows 桥接代码或配对资料。

## 验证

- ESP-IDF Release 构建成功。
- 完整 LVGL 原生预览通过，已有亮度、声音保存/静音/零音量/忙碌/保存失败交互检查通过。没有为像素位置增加复述实现的测试。
- 已实际查看三个页面的渲染图片，数值无重叠，主要控件在屏幕与声音首屏完整可见，缩短后的下拉框文字及箭头显示完整。
- 最终 ELF 显示 Flash 安全及 BLE 接收栈检查通过，`git diff --check` 通过。触屏实际手感与真机布局仍待升级后验收。

产物：`artifacts/development/settings-layout-085-20261002/`。

| 文件 | 用途 |
| --- | --- |
| `settings-category-1.png` | 屏幕与声音预览 |
| `settings-category-0.png` | 连接与网络预览 |
| `settings-category-2.png` | 内容与轮播预览 |
| `settings-sound-muted.png` | 静音/试听禁用状态预览 |
| `aibot_tab5-0.2.85-ui.bin` | 冻结候选镜像 |

镜像 SHA-256：`CF2D732638EAA69CAFE5104E61E7C3B5FA5511983F648861CDBD2EC8187DEB04`。

## English

The user accepted 084 brightness value/persistence, then requested consistent slider/value alignment and shorter settings selectors. The local 085 candidate aligns brightness and volume in identical label/value/slider columns, fits daily screen/sound controls into one viewport, and reduces the screensaver, connection-mode and display-mode dropdown widths from 598 to 300 px. The top sound shortcut opens the full page without the obsolete scroll offset. Existing settings behavior and feedback remain intact.

ESP-IDF build, the full native LVGL preview with existing interaction checks, visual inspection of all three pages, ELF display/stack guards and whitespace checks passed. Version 085 was flashed and its boot/image/partition checks passed. Original automatic cycling was restored. The user rejected the layout as still inconsistent; revision 086 continues that work.

## 升级收尾与说明修正 / Deployment and notes correction

17:02:54 桌面收尾回执确认启动核验通过、配对资料未改变，`auto`、循环启用、15 秒及原六页顺序恢复。镜像/ELF 哈希匹配，ota_1 为 VALID，连续三次启动样本的 UI 心跳正常，显示错误/欠载均为 0。证据为同一产物目录的 `boot-085-verified.json` 和 `desktop-finish.json`。

用户指出升级明细排版丢失。原因是生成的 sidecar 把说明写成单段普通文本，既有分类/列表渲染器没有回退。已改为分类标题与列表，并使用真实 sidecar 完成原生 LVGL 预览。新增准备脚本拒绝无分类列表的说明；重复运行可复用归档，失败不会替换 latest。这一说明修正不改变 085 镜像，不重复刷入同版本。

The desktop receipt confirms a valid boot and restoration of the original display policy without pairing changes. Unformatted release details came from a plain-paragraph sidecar, not a renderer regression. Structured notes and an actual-sidecar preview now accompany preparation; the unchanged 085 image does not require reflashing.
