# TAB5 .124 花卉屏保验收 / Floral screensaver acceptance

2026-10-06，继续用户已授权的屏保改动、固件更新及验收；根据 .123 实机反馈修订。设备 .123 已完成精确启动核验与原轮播恢复，详见 TAB5-ACCEPTANCE-123.md。

## 内容 / Changes

- 十二月历左侧配图替换为每月不同的传统花卉插画：梅、杏、桃、牡丹、芍药、栀子、荷、百合、桂、菊、山茶、水仙。此为公历页面的艺术主题，不作为农历花神顺序或实际花期说明。
- 左侧底部以每月独立短句替代两个节气名称，正式屏保和预览都有。十月保留“岁时有序，日子有光。”。
- 去掉下方假期汇总、节气日期汇总及当日描述，格子内的农历、节气、休/班继续显示。数据齐全时不再显示右侧页脚文字；缺少官方年份数据时只显示简短待更新提示。
- 新增独立“年度点阵 · 自动昼夜”选项，旧值 0–4 保持原含义，新增值 5。设备本地时间 07:00–19:00 浅色，其余暗夜，在正式屏保/预览显示期间均可切换。时间刷新不写 NVS，不改变用户设定或背光；沿用既有屏保降亮度及退出恢复。

## 本地验证 / Local validation

证据目录：`artifacts/development/tab5-124-screensavers/`。

已通过首轮文字与配色原生检查：12 个互不重复的短句、全部字体字形可用且不截断、月/年切换、07:00/19:00 边界与午夜、预览/正式屏保及原手动配色。花卉集成后的构建、实际 sidecar 预览、传输和设备验证随后追加。

- `build-final.log`：最终 ESP-IDF 构建通过，镜像可容纳于现有 OTA 应用分区，未修改分区表。
- `preview-final.log`：使用最终实际 sidecar 的全套原生检查通过，12 张花卉均由正式 LVGL 解码器成功解码，尺寸正确，随月份更换；短句 12 句互异、字体全部存在且宽度不截断；月份按钮仅预览可用，切月保持屏保。
- `preferences.log`：六个样式均可持久化，非法值和 open/set/commit 失败检查通过。自动昼夜刷新不写偏好、不变亮度，手动配色保持原选择。
- `compression.log`、`decoder.log`、`stream-decoder.log`：本包 .NET 压缩、固件原生区间解码与整个镜像流逐字节比对通过，含损坏/截断/尾随数据检查；这是本地验证，不是实际 OTA 耗时。
- `screensaver-calendar-current.png`、12 个月预览和 `ota-release-notes-actual*.png` 来自实际固件 UI。已查看冬/春/夏/秋的代表月份及实际更新说明，花卉无不透明背景块、短句可见。首次导出说明图片时文件名误写为 release-notes.ppm，已从实际 ota-release-notes-actual.ppm 导出；不影响原生测试结果。
- 两个仓库 `git diff --check` 通过。

本地包 `artifacts/firmware/tab5/latest/aibot_tab5.bin`：版本 `0.2.124-ui`，7,139,024 字节；镜像 SHA-256 `f9624834a373c71d4f58dfe8b8671cf854fcb08e30288f0661cc5e7aca0b04a6`，ELF SHA-256 `d54a4a02436125622cd2aafca02fc60575f9b5ca6b038abf796fd7cd7982ce4a`。历史目录 `versions/0.2.124-ui/f9624834a373-b6711425`。

十二张插画通过 built-in image_gen 为项目生成，无外部参考图。原透明 PNG、主体提示与来源记录保存在 TAB5 工程 `firmware/assets/calendar-flowers/`。编码沿用现有 LVGL I8/LZ4 支持，240×216 RGBA 索引色，十二张共 168,257 字节；原图 alpha 和源哈希保留。每张压缩后原生索引数据往返逐字节一致。

## 设备状态 / Hardware status

已正常退出桥接，使用同一二进制重新启动并提供 .124 新包；真实桌面 profile、pairMatchesDisk=true、auto/cycleEnabled、原六页顺序和 15 秒间隔复核正常。设备仍运行已核验的 .123；.124 尚未安装，已提示用户在设备端点击升级。新增视觉及自动进入的真机验收另行记录；不以原生桌面预览冒充设备屏幕验收。

后续视觉反馈：用户认可原花卉插画，但认为实际界面中的位置与布局死板，未认可 .124 的视觉效果。已停止提供此包并使用原桥接正常启动，设备保持 .123；后续重新构图的原生预览在 `artifacts/development/tab5-calendar-layout/`，不将旧图认定为最终验收。

## 首次背景布局预览（用户未认可） / First background preview (rejected)

按用户“花卉作为背景、文字浮在上面、大小恰到好处”的反馈，原花卉资源保留，绘制顺序改为花卉在底层、文字在上层。左侧封面宽 400 px，花卉画布显示约 341–350 × 307–315 px，按每月花型略调位置，透明度为 40%；保留完整枝叶与上下留白。右侧日历网格相应调整，农历、节气和休/班标记继续显示。

此轮资源使用 280×252、256 色 RGBA 索引画布，十二张合计 211,383 字节；提高原生显示细节，源 PNG 未修改。资源重新编码和压缩往返检查通过，`preview-background.log` 的全套原生检查通过；正式屏保截图为 `screensaver-calendar-current-background.png`，其余十二个月截图为 `screensaver-calendar-01-preview-background.png` 至 `screensaver-calendar-12-preview-background.png`。这些是运行实际 LVGL UI 产生的截图，尚未获得用户视觉验收，也不是设备屏幕照片。

`build-background.log`：ESP-IDF 编译和应用分区大小检查通过，镜像大小 0x6d9850，现有 OTA 分区剩余 0x67b0（26,544）字节；构建器提示分区接近满载。未更改分区表，未执行刷机，后续增加资源时须重新检查容量。

这轮源代码预览没有覆盖已归档的 .124 交付包。现场复核设备仍为 .123，桥接 auto/cycleEnabled=true，六页顺序和 15 秒间隔保持原设置。

## 最新左侧重排 / Latest left-cover revision

用户看过首次背景预览后要求重新布局左侧。当前改为花卉画页构图：去掉大号阿拉伯数字和重复的季节说明，右上竖排中文月份，左侧短句按传统阅读顺序分为右、左两列并错位排列，原花卉以适中尺寸置于下方背景层。不透明度由 40% 调到 70%，保留花瓣与枝叶色彩；十二句内容保持原意，竖排时省略标点。

新增月份标题字体仅包含“一二三四五六七八九十月”十一个字，48 px / 2 bpp，沿用已核验的 Noto Sans CJK SC Regular 和 OFL 许可。字体生成脚本、固件与原生预览使用同一份字体源。

`preview-album.log` 的全套原生检查通过，逐月核对双列短句合并后与原句一致、月份正确、字形存在、文字完整显示；月份切换、跨年、闰日及正式屏保/预览行为继续通过。13 张实际 LVGL 截图的右侧区域（x=400 至 1279）与上一版逐像素一致。最新截图后缀为 `-album.png`，包括 `screensaver-calendar-current-album.png` 和十二个月预览；尚未刷入设备，等待用户视觉评价。

`build-album.log` 的最终 ESP-IDF 编译和分区检查通过：镜像 7,185,472 字节，原 OTA 分区剩余 23,488 字节，仍有接近满载提示。未校时状态保留“等待校时”说明。两仓库 `git diff --check` 通过；本轮未生成或提供新的升级交付包。

## 左侧圆体修订 / Rounded cover lettering

用户随后反馈左侧字体不够圆润。本轮仅调整左侧字体：月份使用 Resource Han Rounded CN 0.990 Bold 48 px / 2 bpp，短句使用同系列 Medium 24 px / 4 bpp，均为原生字号字库；文字内容、坐标、花卉构图与右侧日历保持。月份子集 11 字、短句及等待校时子集 70 字，生成时校验两个源字体的 SHA-256，沿用项目既有 OFL 许可。

`preview-rounded.log` 全套原生检查通过，包括十二个月的标题、完整短句、字体字形和显示范围。最新截图为 `screensaver-calendar-current-rounded.png` 及十二个月 `-rounded.png` 预览。13 张截图的右侧日历区域与花卉下方区域均和上一版逐像素一致；已查看十月、四月、十二月的完整截图。此轮为原生界面预览，尚未刷机或获得用户视觉验收。

首次将两套圆体都编码为 4 bpp 时，分区检查超出 736 字节，失败日志保留为 `build-rounded-4bpp-overflow.log`。月份标题改为 2 bpp 灰度编码、短句保留 4 bpp 后，重新通过原生预览、像素对比及 ESP-IDF 编译；最终镜像 7,205,008 字节，现有 OTA 分区剩余 3,952 字节（`build-rounded.log`）。月份字体、字号和字形不变，未修改分区表；后续增加代码或资源必须重新核对容量。

## English

Visual follow-up: the user liked the flower illustrations but rejected their layout in the UI preview. The .124 offer was withdrawn; the original bridge remains running with .123 on the device. Revised native compositions are tracked under artifacts/development/tab5-calendar-layout and the old preview is not final visual acceptance.

The revised composition places the botanical artwork behind all cover labels. A 400 px cover contains a modest 341–350 × 307–315 px image canvas at 40% opacity, with small offsets for each flower shape. Original PNGs remain intact; the 280×252, 256-color RGBA import uses 211,383 bytes in total. Native decoder, calendar and screensaver checks passed in preview-background.log. The exported background PNGs are native LVGL previews, not hardware photographs or final user visual acceptance. This source iteration does not replace the archived .124 package; the device remains on .123 with the original automatic cycle settings.

The ESP-IDF build and application-size check passed in build-background.log. The image occupies 0x6d9850 bytes and leaves 26,544 bytes in the existing OTA partition; the builder reports that the partition is nearly full. The partition table was unchanged and no flashing was performed.

The user rejected that first background layout and requested a new left cover. The latest album composition removes the large Arabic month number and redundant seasonal caption, places a vertical Chinese month title at the upper right, and arranges the original verse in two staggered columns read from right to left. The original botanical image remains modest in size below the text, with opacity raised to 70%. An eleven-glyph 48 px / 2 bpp month font uses the same pinned OFL source. Native checks in preview-album.log verify all month titles, reconstructed verses, glyphs and text bounds. The right-hand calendar region is pixel-identical across all 13 before/after captures. Latest native previews use the -album.png suffix; this revision has not been flashed or visually accepted by the user.

The final ESP-IDF build and partition check passed in build-album.log: 7,185,472 image bytes, leaving 23,488 bytes in the unchanged OTA partition, with the near-full warning retained. The waiting-for-time message remains available. Both repository whitespace checks passed. No new upgrade delivery package was prepared or offered during this layout revision.

Following feedback that the cover lettering was too rigid, the cover now uses Resource Han Rounded CN 0.990 Bold at 48 px / 2 bpp for month names and Medium at 24 px / 4 bpp for verses, both at native size. The 11-glyph month and 70-glyph verse/waiting subsets use SHA-256-pinned existing OFL sources. Text, coordinates, flowers and the right calendar remain unchanged. All native checks passed in preview-rounded.log; the -rounded.png captures preserve the right calendar and lower botanical region pixel-for-pixel across 13 screenshots. This revision has not been flashed or visually accepted by the user.

The initial all-4-bpp build exceeded the unchanged application partition by 736 bytes (build-rounded-4bpp-overflow.log). Encoding the month title at 2 bpp while retaining 4 bpp for the verse resolved that failure without changing its font, size or glyph outlines. Final native previews, pixel comparisons and ESP-IDF build passed: 7,205,008 image bytes, leaving 3,952 bytes in the OTA partition. Further code or asset additions require another capacity check.

The user requested revisions to the installed .123 screensavers within the ongoing authorized firmware update and acceptance task. Version .124 adds twelve original floral illustrations, a distinct phrase per month in place of the cover term pair, and a cleaner calendar footer. Lunar notes, terms and rest/workday marks remain in the grid. An independent automatic annual style uses light colors from 07:00 to 19:00 device local time and night colors otherwise, with no preference writes or brightness changes during theme updates. Existing manual styles retain their values. Native checks, final image/notes validation and actual-device acceptance are recorded separately. No commit, push or public release is included.

## 2026-10-06 古典诗笺与多种书法字形 / Classical poems and varied calligraphy

左侧加入 24 首完整短诗，按桥接年度节气日期表在设备当地日期跨节气时切换，保留题名和作者。小寒之前使用冬至篇目；当年日期表缺失或非法则回退原月度短句。浏览其他月份以 15 日为参考，当月预览使用当天。选诗原文、出处、异文说明与字体分配见 [TAB5-SEASONAL-POEMS.md](TAB5-SEASONAL-POEMS.md) 及 TAB5 源码 `firmware/assets/calendar-poems.json`。

诗文采用霞鹜文楷、马善政、志莽行书三种字形，各篇内部统一。只生成该字体负责篇目的必要字符，并校验源字体 SHA-256 和字符覆盖；含碕、裯、荄等字的篇目保留完整文楷字形。月份、节气印章和落款继续保持统一风格。完整 OFL 许可和固定来源记录均已保留。

用户反馈花色暗淡且文字压在花瓣上后，花卉不透明度提高到 90%，保持此前约 5% 缩小后的尺寸；按各花轮廓下移到诗文下方，诗句和花瓣之间保留空隙。原图及 280×252 / 256 色 RGBA 导入数据保持；花卉仍为背景绘制层，诗文、题名和作者位于上层。

首次古典版构建曾超出应用分区 29,136 字节（`build-classical.log`）。本轮将已有 72/96/144 px 数字字体改为 LVGL 无损位图压缩，保留外形、灰度、字号和字距，未改通用中文字库或分区表。最终 159 张非月历原生截图与此前逐像素一致，包含时钟和年度点阵；13 张月历的右侧区域也逐像素一致。

- `preview-calligraphy-spaced.log`：完整原生检查通过，覆盖 24 篇全文、三种字体实际切换、字形与排版范围、节气前日/当日/次日、跨年、缺失和非法数据、预览与正式屏保行为、亮度恢复和电脑唤醒。
- `build-calligraphy-spaced.log`：最终 ESP-IDF 构建和分区检查通过。镜像 7,192,800 字节，现有应用分区剩余 16,160 字节，接近满载提示仍保留；此前超限已解决。
- 镜像 SHA-256：`938f2234c1b07c87c79f5c25bf82abbf02058fa4d7149406bc787250015ad8c4`。像素比较与容量证据为 `calligraphy-spaced-verification.json`。
- 最新预览为 `screensaver-calendar-current-calligraphy-spaced.png`、12 个月预览及 `screensaver-poem-01-calligraphy-spaced.png` 至 `screensaver-poem-24-calligraphy-spaced.png`。这些是实际 LVGL 原生截图，不是真机照片。已查看毛笔、行书、楷意及长题名代表页面。

本轮未刷机、未覆盖或重新提供旧 .124 交付包，未提交或推送 Git。视觉评价以用户看到这些最新截图后的反馈为准。

The cover now contains 24 complete seasonal quatrains, with sourced original text and title/author attribution. Local-date transitions follow the bridge's annual solar-term table; missing or malformed tables fall back to the monthly phrase. Three pinned OFL calligraphic faces vary by poem, with complete source-glyph coverage. Flowers retain the requested smaller size and restore pigment color at 90% opacity, positioned below the verse with clear space around the lettering. The original image and palette data are unchanged.

The first classical build overflowed the application partition by 29,136 bytes. Lossless compression of the existing 72/96/144 px numeral bitmaps recovered space without changing their appearance: all 159 non-calendar native captures are pixel-identical, and the right-hand calendar region matches across 13 captures. Final native checks and ESP-IDF build passed; the 7,192,800-byte image leaves 16,160 bytes in the unchanged partition. These are local build and native-preview results; no hardware flashing, package offer, commit, push or public release was performed.
