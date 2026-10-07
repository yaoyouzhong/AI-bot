# TAB5 每日名画与每日书法 / Daily art screensavers

2026-10-07 拔线复核：`.145` 的 Wi-Fi 图库 RPC 使用了默认 GET，导致 USB 拔出后可能停留旧书法横屏图或提示等待名画同步。`.146` 已修正并安装，精确镜像启动核验通过；用户确认 USB、Wi-Fi、BLE 下两类图库均可显示、转向及分页。BLE 等待体验和 USB 标识恢复慢的问题仍在处理，见[无线同步修复记录](TAB5-GALLERY-TRANSPORTS-146.md)。

Unplugged follow-up (2026-10-07): .145 used the default GET method for gallery RPC over Wi-Fi. The fix in .146 is installed with exact-image boot verification. The user confirmed both galleries' display, rotation and paging over USB, Wi-Fi and BLE. BLE loading latency and the slow USB screen indicator remain under investigation. See the [transport repair record](TAB5-GALLERY-TRANSPORTS-146.md).

后续 `.147` 已本地安装并完成三通道图库与缓存验证；缓存重复查看即时，BLE 首次下载仍慢，见[缓存与下载记录](TAB5-GALLERY-CACHE-147.md)。`.148` 已安装，仅蓝牙模式增加下载期间的临时 Wi-Fi 暂停；实测竖屏预取遗漏暂停，`.149` 已安装修复，两轮同图竖屏下载 7.26 / 8.54 秒，较 .147 单次记录分别缩短约 23% / 10%，见[修复与验收记录](TAB5-BLE-GALLERY-148.md)。The installed .147 has three-transport gallery and cache evidence; cached revisits are immediate while first BLE downloads remain slow. The installed .148 temporarily pauses Wi-Fi during gallery downloads in explicit BLE-only mode, but portrait prefetch missed that pause during restoration. The installed .149 correction fetched the same uncached portrait in 7.26 / 8.54 seconds, approximately 23% / 10% shorter than the single .147 record; this does not establish a sustained gain.

标准版 .145 的既有验收：独立可选图库、预览与自动屏保四方向、第二页及后续分页通过，详见 [.145 验收](TAB5-ACCEPTANCE-145.md)和[图库安装](GALLERY-PACKS.md)。当前状态以上方 .149 后续记录为准，下方 .131 状态为历史记录。

Previous standard .145 acceptance covers optional packs and later-page portrait layouts; see [.145 acceptance](TAB5-ACCEPTANCE-145.md). The .149 follow-up above is current; the .131 status below is historical.

2026-10-07 较早部署记录（.131）：配套桥接、完整横竖屏图库及 .131 固件已部署。402件名画、411件书法，共1902个分页、3804张横竖屏JPEG；已核实中文备注显示在对应画面的原文下方。十二月历和三类屏保主链路在 .125 获确认，艺术屏保四方向在 .130 获确认，书法花屏修复在 .131 获确认。精确镜像启动核验通过，原自动轮播已恢复，当时见[.131验收记录](TAB5-ACCEPTANCE-131.md)。下方历史阶段记录不代表当前待办。

Earlier deployment (2026-10-07, .131): the bridge, complete gallery and .131 firmware are deployed: 402 paintings, 411 calligraphy works, 1902 pages and 3804 landscape/portrait JPEGs. Verified Chinese notes appear beneath original captions. The user accepted all three screensavers with .125, four art orientations with .130 and the calligraphy corruption fix with .131. Exact-image boot verification passes and original automatic cycling is restored. See the [.131 acceptance record](TAB5-ACCEPTANCE-131.md); historical pending entries below are not current open items.

已为原用英文的354件核查中文备注：16件补入有出处的作品中文名，200件补入作者中文名；其余338件作品名继续保留馆藏英文，不补暂译。原名保持不变，已核实备注现已排入对应200张屏保画面，详见[中文备注清单](TAB5-PAINTING-CHINESE-NOTES.md)。

For the 354 English-label works, source-backed notes now add 16 Chinese artwork titles and Chinese author names for 200 works. The other 338 titles retain museum English without provisional translation. Original labels are retained and verified notes are now rendered in 200 frames; see [Chinese reference notes](TAB5-PAINTING-CHINESE-NOTES.md).

## 年度图库验收 / Annual acceptance

目标是两类各至少 **366 件独立作品**，同一自然年每天不同（含闰年）。原先的 12 幅名画、8 幅书法只够功能预览，不能作为完整交付。

当前名画为 **402 件**：原有 366 件西方绘画，加上 36 件中国画，按固定顺序均匀穿插。另筛选 **24 件中国画备选**，未计入每日轮换；详见[中国画入选与备选](TAB5-CHINESE-ART-SELECTION.md)。中国画来自 Cleveland Museum of Art CC0 馆藏，原图已下载并人工检查完整构图，短边至少 1200 像素、长边 3400 像素；作品和作者使用馆方中文原文，保留繁体及异体字。原有 354 件 CMA 西画仍沿用馆方原文标题和作者，尚未完成全库中文化。402件均保留作品名、作者和馆藏信息，48件作品名及作者已有中文，其余354件沿用英文。现补齐独立中文博物馆字段，画面保留原文署名，见[名画资料清单](TAB5-PAINTING-METADATA.md)。

书法已整理为 **411件独立作品、1500个展示帧**。本轮先完成上次待入库的63件（582张原图、602帧），再新增45件不超过3页的完整作品（56张原图、60帧）：37件单页、1件两页、7件三页。详见[完整书法清单](TAB5-CALLIGRAPHY-SELECTION.md)和[分页统计](TAB5-CALLIGRAPHY-PAGINATION.md)。名画与书法均通过至少366件的年度数量检查。

短幅包含8副完整对联、1组四屏，各算一件，按阅读顺序每页并排两屏。新的`paired-panels`排版保留原图比例、全部文字与印章；原有长卷和册页不删页、不改为多个作品。所有来源、许可、尺寸、裁切、方向及SHA-256留档。未确认汉字的作者保留馆方罗马字拼写，不以猜测替换。

按用户要求，长卷和较长条幅继续保留本地。另整理8件完整候选（7件长条幅、1副低对比度对联），不加入每日catalog、不计入新增45件；其他已下载照片保留为待核查资料。详见[本地候选](TAB5-CALLIGRAPHY-CANDIDATES.md)。现有图库301件不超过3页、370件不超过8页，41件超过8页；每日书法目录已按展示页数升序排列，同页数保留原顺序；没有页数硬上限，名画顺序不变。日期索引基准不变，重排后的作品与日期对应会变化。全部411件记录作品名、作者标注与馆藏博物馆，涉及9家馆；其中31件佚名、1件诸家、12件暂保留馆方罗马字作者名。画面保留馆方署名，部分馆名仍为原文；馆藏信息不代表当前正在展出。

Calligraphy now contains **411 distinct works and 1500 frames**. The pending 63-work batch adds 582 originals and 602 frames; another 45 complete works add 56 originals and 60 frames, with 37 one-frame, one two-frame and seven three-frame works. Eight complete couplets and one four-panel set remain single works. The paired-panel layout preserves all text, seals and aspect ratios. Both categories meet the annual quantity target. Eight complete reserves remain local outside the daily catalog; other downloaded scroll material is retained for review. There are 301 works with at most three frames, 370 with at most eight and 41 with more than eight. The daily calligraphy catalog uses stable ascending frame counts without a cutoff; painting order is unchanged. Existing date indexing is retained, so the date-to-work mapping changes with this reorder. All 411 works record a title, author attribution and holding museum across nine institutions: 31 anonymous, one collective attribution and 12 museum-romanized author names. Image credits retain the institution names, sometimes in their original language. Holding institution does not imply current exhibition.

### 新增书法来源 / Additional calligraphy sources

- [Cleveland Museum of Art](https://www.clevelandart.org/art/1961.421.2)：馆藏页提供 CC0 Print 原图。已实际接入宋理宗、文徵明、梁启超、悦山道宗作品；每件核验授权与分辨率。
- [Princeton University Art Museum](https://artmuseum.princeton.edu/art/collections/objects/23232)：馆藏提供 Maximum Available 下载；其[图像使用政策](https://artmuseum.princeton.edu/art/our-collections/image-use-and-access)允许自由使用公有领域高清图。已接入郑燮、宋高宗、杨妹子、翁同龢作品，保留要求的来源署名。
- [Minneapolis Institute of Art](https://collections.artsmia.org/art/124722)：本轮新增26件短幅作品；仅采用馆方标为 Public Domain 的高清图，保留馆方署名及[开放使用政策](https://github.com/artsmia/collection-info/blob/gh-pages/open-access.md)。
- [The Met](https://www.metmuseum.org/about-the-met/policies-and-documents/open-access)：通过官方 API 和图片 CDN 累计补充作品；本轮新增17件。官网 HTML 当前仍可能返回 429；不能承诺各入口持续稳定。
- [Paris Musées / Musée Cernuschi](https://www.parismuseescollections.paris.fr/en/musee-cernuschi/oeuvres/calligraphie-eventail)：此前9件，原图来自馆方CC0下载包；许可在每件IIIF manifest中核验。包含王铎、张弼、康有为、文徵明、姚华、沈尹默、洛夫、张耕源。
- [Rijksmuseum](https://id.rijksmuseum.nl/20075101)：张謇六言联的A、B两联合为一件，馆方标注Public domain。使用官方IIIF按比例降采样至短边2000像素，遵守服务图像面积限制；没有放大图像。

- [ColBase／东京国立博物馆](https://colbase.nich.go.jp/collection_items/tnm/TB-1634?locale=zh)：累计229件，最近两批新增63+2件、582+2张原图。依[使用条款](https://colbase.nich.go.jp/documents/term?locale=zh)采用CC BY 4.0并注明加工；不能将许可套用于e国宝或其他网站。ColBase contributes 229 works; the latest batches add 65 works from 584 originals under its documented terms.

The PC serves the verified local catalog. Download failures never replace usable images with empty results. Museum availability and per-object permissions are checked separately; there is no promise that a public site never rate-limits requests.

```powershell
python scripts/verify_daily_art_year.py windows-app/AIBotBridge/Assets/DailyArt --report artifacts/development/tab5-daily-art/year-acceptance.json
```

该验收检查每类至少 366 个独立作品身份、重复来源/原图、原图尺寸与全部原生展示帧，并检查书法页数升序以及两类作品的作品名、作者、博物馆字段完整。它与功能自检分开，缺少作品时返回非零；不得跳过数量检查。`workId` 是整件作品的稳定身份，局部视图只在同一条目的 `frames` 中登记。

## 行为 / Behavior

- 设备屏保类型新增“每日名画”（6）、“每日书法”（7），原有 0–5 编号不变。
- 电脑使用本地精选图库；按设备当地日期确定作品，同一天重进屏保不随机换作品。两类均超过366件，可支持任一自然年内不重复；当前真机验收见文首。
- 图库在电脑侧，图片不编入固件、不占用桌宠 Flash 缓存。设备以低优先级通过已配对的 USB、Wi-Fi 或 BLE 拉取；升级、录音、照片处理与测速优先。
- 图片通过完整 SHA-256 校验及 JPEG 解码后才整体替换。连接中断、文件缺失或解码失败保留上一幅及其日期，两种屏保分别保留各自最近一幅。
- 设备缓存位于 RAM：断开电脑仍显示最近一幅；设备断电重启后需要重新同步。电脑无需为了每日换图联网下载。
- 长卷先显示全卷，再按从右到左的阅读顺序分段展示；落款明确区分“全卷”与“局部”。这是同一作品的不同视图，不是一天更换多件作品。
- 单段至少展示 30 秒；请求失败最多每分钟重试一次。正式屏保保留既有触摸唤醒、亮度与电脑输入唤醒规则。

The new device-only styles append IDs 6 (paintings) and 7 (Chinese calligraphy). A local curated PC catalog rotates deterministically by the device's local calendar date. Both categories exceed the required 366 distinct works; current hardware acceptance is recorded above. Complete authenticated transfers are SHA-256 checked and decoded before atomic replacement. Each category retains its own last frame and date on failure/disconnection. RAM retention does not survive a device power cycle. Handscroll overview and right-to-left detail views belong to the same daily artwork. Foreground transfers have priority; existing wake/brightness behavior remains unchanged.

## 图像质量 / Image quality

- 原作与作者依据馆藏记录核对；不得以 AI 仿作、补画、放大缩略图或不明授权的图像替代。
- 记录原始图像尺寸、来源、许可及 SHA-256；展示时保留比例，不拉伸、不重绘、不改变墨色。
- 输出为原生 1280×720、JPEG quality 96、4:4:4。作品画面与落款在电脑预先排版，固件直接呈现，避免额外字库或运行时字体差异。
- 长卷局部使用原始像素，不将低矮的全卷缩略图放大成细节。裁去摄影色卡或选取本幅时必须记录裁切范围并核对完整文字。
- `windows-app/AIBotBridge/Assets/DailyArt/catalog.json` 为实际顺序；`provenance.json` 为逐件来源与质量记录。
- `scripts/prepare_daily_art.py` 可根据审定的 selection 文件和已许可字体重新排版。源码图片保留在开发资料目录，不把庞大的原图塞进安装包。
- 解缙《草书七言诗》（Mia 4886）的源照片上下颠倒，selection 已明确设置 `rotation: 180`，在原图坐标裁切后旋转；原文件与 SHA-256 保持不变，provenance 记录方向修正，屏保帧及总览预览已重新生成。

Only documented public-domain/open-license reproductions are included. Source dimensions and checksums are recorded. The layout preserves aspect ratio and original color, with native-resolution 1280×720 JPEG quality 96 / 4:4:4 output. Long-scroll details must derive from sufficiently detailed originals. No generative restoration or thumbnail upscaling is used.

## 历史验证记录 / Historical validation records

以下描述对应当时尚未部署的阶段；中文备注现已渲染，完整图库现已部署。最新设备验收见文首。

The following entries describe earlier stages before deployment. Chinese notes are now rendered and the complete gallery deployed; see the current acceptance at the top.

2026-10-06中文备注补充：本地年度检查与813件／1902帧图库自检再次通过，Release构建0警告、0错误；见`bridge-build-chinese-notes.log`和`gallery-self-test-chinese-notes.log`。全部原始字段、顺序和JPEG校验值保持不变，新增备注未渲染到屏保。

Chinese-note validation passes annual checks, the 813-work / 1902-frame gallery self-test and a zero-warning Release build. Original fields, order and image hashes remain unchanged; the new notes are not rendered in the screensaver.

本轮完成图库、生成脚本、来源记录与分页文档。Windows Release构建0警告、0错误；813件作品／1902个展示帧的图库检查通过，见`artifacts/development/tab5-daily-art/bridge-build-short-first.log`、`gallery-self-test-short-first.log`与`year-acceptance.json`。新增638张原图的尺寸及SHA-256已核对，662个新增展示帧经联系表目检；45件短幅全部≤3帧。旧排版回归样本6件／24帧字节不变，集成前后的既有帧SHA-256保持不变。新短幅标题、作者及年代字体覆盖通过。本次重排与补齐馆藏后，813件元数据对齐，1902张JPEG的SHA-256全部不变，名画顺序不变；书法升序及两类必填资料检查通过。

配对RPC、原生LVGL及固件构建仅为此前证据，本轮未重跑；未修改Windows运行代码或固件。此前`--status-once`受既有资料目录保护阻止，本轮资产整理未重跑该入口，也未改变保护或资料路径。尚未部署、重启程序、打包或刷机。

Release builds with zero warnings/errors. Local validation passes for 813 works / 1902 frames, including annual unique-work counts, JPEG dimensions and transfer limits; gallery self-tests cover daily/leap-year rotation and chunk/hash behavior. All 638 new originals were dimension/hash checked and all 662 added frames visually reviewed. Every new short work has at most three frames. Six prior works / 24 frames were re-rendered byte-identically, and existing integrated frame hashes remain unchanged. Caption glyph coverage passes. RPC, native LVGL and firmware results are prior evidence only. The previous status-once profile-guard block was not bypassed. No deployment, restart, packaging or flashing was performed.

`--self-test-tab5-gallery` verifies date stability, collection rotation, leap/year boundaries, every encoded frame, chunk reconstruction/hash, stale revisions and invalid ranges. The RPC test exercises the authenticated gallery route. Native LVGL previews test category isolation, atomic replacement, failed-next-day retention, retry bounds and touch wake. Target build confirms compilation and partition fit. These do not substitute for on-device transfer/color/readability acceptance; this change does not flash or offer a firmware package automatically.

## 十二月历布局 / Monthly calendar layout

移除常驻备注后，日期区由原先固定 388 像素高改为按可用高度铺开；正式屏保排至约 y=686，预览排至约 y=641，为月份按钮留出空间。公历和农历作为一组在每行中居中排列，按实际周数分配行高。仅缺少官方节假日或节气数据时再为提示预留 32 像素。左侧花卉、诗词、字体保持当前方案。原生 LVGL 十二个月预览及目标固件编译通过；已随 .125 安装，用户确认十二月历正常，后续 .131 保留此布局。

After removing the persistent footer, the date grid uses the available height and vertically centers each Gregorian/lunar date pair. Normal and preview modes allocate space separately; only missing-data warnings reserve a footer. Existing flowers, poetry, month navigation and wake behavior are preserved. Native twelve-month rendering and target compilation pass; installed and user-accepted with .125, with this layout retained in .131.
