# 名画与书法库扩充 / Artwork library expansion

最新本地资源版本为 `2026.10.08.1`，共2632件独立作品：1765件名画、867件书法。书法在第一批497件基础上再增加370件，其中108件馆方归属中国、261件归属日本、1件归属韩国。第二批的包与验证见本文末尾，以下第一批记录保留作历史依据。

The latest local resource edition, `2026.10.08.1`, contains 2,632 independent works: 1,765 paintings and 867 calligraphy works. The second batch adds 370 calligraphy works, comprising 108 with museum culture/place attribution to China, 261 to Japan and one to Korea. Earlier batch evidence below remains historical.

2026-10-08，本地目录已从813件扩充至2262件独立作品。新增1363件绘画、86件书法；当前为1765件绘画、497件书法。已确认中国归属的绘画从36件增至180件。分页、局部、册页画页及横竖屏版本均不另算作品。

The local catalogue now contains 2,262 independent works: 1,765 paintings and 497 calligraphy works, adding 1,363 and 86 respectively. Confirmed Chinese paintings increased from 36 to 180. Pages, details and orientations do not increase work counts.

## 中国传统画 / Chinese painting

本批既有整轴、扇面和手卷，也补入16册完整画册。按馆方登记核对全部画页，陈洪绶双册保留二十二页，樊圻画册保留十页绘画及一页跋文，佛道主题画册保留五十页。书画混合册作为整件保留，不拆分到两个类别重复计数。

| 题材 | 本批可查看的例子 | 完整来源与展示 |
| --- | --- | --- |
| 山水 | 米友仁《雲山圖》、梅清《倣古山水圖冊》、石涛《秦淮憶舊圖冊》 | 手卷、整轴与完整册页 |
| 花鸟与墨竹 | 张熊《Three Purities》、张若霭花鸟画册、四季竹图 | 花鸟册十页；春秋、夏冬两对竹轴合为一件四屏作品 |
| 人物与历史叙事 | 《歸去來辭圖》、禹之鼎《春泉洗藥圖》、陈洪绶双册 | 连续叙事、人物园林与完整双册 |
| 宫苑、建筑与园林 | 仿仇英《漢宮春曉圖》、沈周《虎丘十二景圖》 | 保留摹本限定；完整手卷与十二景册页 |
| 风俗与节庆 | 吴彬《迎春圖》 | 灯会、表演、农事等连续画幅 |
| 佛道题材 | 《蘆葉達磨圖》、《佛道主題畫冊》 | 整轴与五十页整册 |

长卷先展示全幅，再提供按传统从右向左展开的重叠局部。《迎春圖》《歸去來辭圖》《漢宮春曉圖》改用馆方高分辨率TIFF来源，避免以网页重点配图代替完整原图。《漢宮春曉圖》的全卷保留装裱、题名和题跋；局部分页排除已核对的空白卷轴边缘，不产生只有空白装裱的局部页。原始照片不改写。

Sixteen complete Chinese albums were added, including all 50 leaves of the religious album. Mixed painting/calligraphy albums remain one work. Complete scrolls precede overlapping right-to-left detail views. Official high-resolution TIFFs replace cropped or low-detail web views where necessary; the complete mounted overview remains visible.

## 资料与图像来源 / Evidence and image rights

- 新增作品来自克利夫兰艺术博物馆、ColBase／东京国立博物馆及明尼阿波利斯艺术博物馆的官方资料和图像服务，许可分别为CC0、CC BY 4.0、Public domain。逐件图像URL、馆藏链接、许可及原图SHA-256保留于图库 `selection.json` 与 `provenance.json`。
- ColBase条款已核对：[官方中文使用条件](https://colbase.nich.go.jp/pages/term?locale=zh)。图像标明来源与经排版加工，不以项目MIT许可替代图像许可。克利夫兰来源见[官方开放获取说明](https://www.clevelandart.org/open-access)。
- 本批1449件独立作品使用1668张原始照片；1583条克利夫兰图像URL与相应官方记录逐一匹配，相关资料文件哈希一并检查。所有新来源照片经联系表复核；只有单页、单屏的完整书册或成套作品未纳入。
- 作者优先采用馆方提供的原语言姓名，保留“传”“仿”“摹本”等限定；画家与题写者有明确不同角色时分别说明。
- 中文导读共2262件，其中671件有经审定的作品背景，1591件提供经核实的基础资料。欣赏提示是编辑导读；14件核实正文仍单独保存，未核实正文不显示入口。研究缺漏不进入阅读画面。

Official per-work image URLs, licenses, authorship qualifications and original fingerprints are retained. This batch uses 1,668 originals for 1,449 works. Chinese context paraphrases reviewed museum evidence; viewing suggestions are editorial. Fourteen verified transcriptions remain intact, and research gaps stay outside the reading UI.

## 本地图库包 / Local optional packs

| 类别 | 作品 | 横竖屏JPEG | ZIP字节数 | SHA-256 |
| --- | ---: | ---: | ---: | --- |
| [名画](../../artifacts/development/art-expansion-2026-10-08/packs/AI-bot-DailyPainting-2026.10.08.zip) | 1765 | 4036 | 1122199354 | `f2a1e2bb573a888a82a0055e99b8601a4b08fd3de9f4101531867baa95dc6bc4` |
| [书法](../../artifacts/development/art-expansion-2026-10-08/packs/AI-bot-DailyCalligraphy-2026.10.08.zip) | 497 | 3718 | 919062329 | `2737bccab5f7c62f5e3c4b8d172be4f721796cd6f80127900a05da23dbfeb282` |

包位于工作区本地 `artifacts/development/art-expansion-2026-10-08/packs/`，附项目LICENSE、逐件图像许可、来源记录、包清单与SHA-256文件。导读JSON与交互原型另在本目录维护，尚未成为桥接／固件的详情页功能。包可按现有[图库导入说明](../GALLERY-PACKS.md)使用；本次没有公开发布、安装到用户运行资料或刷机。

These locally prepared packs include licenses, provenance and fingerprints. They are not a public release or a device installation. The separate guide dataset and browser prototype remain outside the bridge/firmware detail-page implementation.

## 完成证据 / Validation

整库年度校验通过：两类均达到366件独立作品，7754张JPEG全部符合1280×720、单张不超过1MiB，横竖屏页数相等、来源无重复，书法继续优先短篇。原有3804张JPEG哈希逐一保持一致。新目录SHA-256为 `bb0ef0c1299e1d7221d8e8efaa3eb6377e05776f09ed6c91e198467a14b549d6`。

生成证据保留在 `artifacts/development/art-expansion-2026-10-08/`：`integration.json`、`annual-validation.json`、`source-chain-validation.json`、来源联系表、下载回执及扩充前备份。图库已合入本地资源目录；浏览器复核与实际包导入结果另记于导读预览和图库包验证文件。构建、隔离导入与浏览器检查不能替代TAB5真机验收。

Annual validation passed for both categories. All 7,754 JPEGs meet native dimensions and transport size limits, and all 3,804 previous image fingerprints are unchanged. Local integration is complete; actual hardware acceptance is separate.

## 书法追加批次 / Calligraphy supplement: 2026.10.08.1

本批新增370件独立书法，使用675张馆方原图。来源为ColBase／东京国立博物馆98件、大都会55件、普林斯顿1件、克利夫兰3件、明尼阿波利斯213件。按馆方文化／制作地字段归属中国108件、日本261件、韩国1件，不能把在日本活动的中国籍书家一律改算中国馆藏文化。

新增中国作品包含傅山草书四屏、何绍基《山谷题跋语》四屏、吴昌硕《般若心经》十二屏、罗振玉临金文四屏与甲骨文对联、曾国藩／李鸿章等人的对联，以及文徵明、董其昌、康里巎巎、阮元的手卷、梁同书整册和宋代禅僧尺牍。碑帖包括《祭姪文稿》《洛神赋十三行》《唐文皇哀册文》等拓本；明确区分原迹、传刻与拓印。《致鄱阳复道者偈颂》保留馆方所述的前半限定；无题记录使用形制名称，不编造题名。

完整书写照片和必要组件均经联系表复核。仅有一叶的整册、一幅的成套屏幅、手卷开头而缺后半的网页配图不收录。对联完整呈现上下联，三联幅按馆方位置合成全套概览，四屏与十二屏依次展示成对屏幅；完整册页共用一个workId。新增1990张横竖屏JPEG，当前整库9744张，原有7754张哈希逐一保持一致。原图短边至少600像素、长边至少2400像素；不生成式修补、不放大伪造细节。

The second batch adds 370 independent calligraphy works using 675 official photographs. Source culture/place attribution is China 108, Japan 261, Korea one. Complete couplets, triptychs, four/twelve-panel sets and albums retain a single work identity. Rubbings, copied works, surviving portions and attribution qualifications remain explicit. All 7,754 earlier native frames are unchanged; 1,990 new frames bring the full catalogue to 9,744.

全量中文资料现为2632件：732件有经审定的作品背景，1900件有核实的基础资料与欣赏提示；构建核对2679条证据哈希。书法正文仍为14件逐字核实，810件未取得正文、43件存在候选但未审定，后两类不显示释文入口。研究缺漏不显示在阅读画面。

The full guide covers all 2,632 works: 732 with reviewed work-specific context, 1,900 with verified metadata and viewing suggestions. Source validation checks 2,679 evidence fingerprints. Fourteen verified transcriptions remain separate; unreviewed candidates and gaps do not appear in the reading UI.

| 最新本地包 | 作品 | 横竖屏JPEG | ZIP字节数 | SHA-256 |
| --- | ---: | ---: | ---: | --- |
| [名画 2026.10.08.1](../../artifacts/development/calligraphy-expansion-2026-10-08/packs/AI-bot-DailyPainting-2026.10.08.1.zip) | 1765 | 4036 | 1122199357 | `81e88e09a5f0e2dc07efb56bac6ce202dc587b6ed54cf96645fb282601be216c` |
| [书法 2026.10.08.1](../../artifacts/development/calligraphy-expansion-2026-10-08/packs/AI-bot-DailyCalligraphy-2026.10.08.1.zip) | 867 | 5708 | 1393854102 | `0a1cec9629962529341ba535658bdc96192ed17d2db16e61c5abd007c1fc6e15` |

第二批证据保留于 `artifacts/development/calligraphy-expansion-2026-10-08/`：`before-expansion/`、`approved-selection.json`、`integration.json`、`annual-validation.json`、`source-chain-validation.json`、下载回执、原图／原生图片联系表与图库包。目录SHA-256为 `5eca04546475135ff865ed132e510e905794d3534bcb7e6db1722b4591b70f44`。两类均达到366件独立作品；每类数量、图像数量、包体积和元数据保持现有导入限制。

Annual/native-image checks passed. Both categories exceed 366 distinct works and stay within the existing importer limits. The pack metadata is byte-identical to the activated local source catalogue. Guide and browser detail-page behavior remain separate from bridge/firmware integration; no runtime profile installation or hardware flashing is asserted.

Windows 实际消费者隔离导入通过：`PACK_INSTALLED_OK category=painting works=1765 images=4036 annual=366 orientations=2`、`PACK_INSTALLED_OK category=calligraphy works=867 images=5708 annual=366 orientations=2`，最终为 `GALLERY_PACKS_REAL_OK`。使用本日已构建的独立Release验证程序，验证所有JPEG可读取、热导入后年度服务、1×／1.5×／2×界面截图；新建测试资料位于本批artifact工作区，未写入用户实际图库。日志、程序哈希和截图保留于 `pack-consumer-validation.json`、同名log及 `pack-consumer-proof/`。两幅／四屏旧排版回归检查了6张JPEG，均保持原有哈希。

The actual Windows consumer passed both pack installations, loaded every JPEG, verified the annual service after hot import and captured 1×/1.5×/2× UI scales in an isolated test profile. The user's runtime collection was not installed. A two/four-panel rendering regression reproduces all six previous image fingerprints.

全量预览生成5264张横竖屏导读PNG、9744张原生JPEG副本与83张联系表；4524张旧导读按历史哈希复用，新增740张。浏览器实际复核六件新作品的12种横竖屏组合，图像均加载成功；横屏与竖屏的详情返回保留作品、分页、方向和查看方式。回到作品第一页不退出，触碰画面其余位置退出，继续欣赏恢复原查看方式；未审定正文不显示释文，导读仅有一个查看解读入口。当前浏览器与整库一致性报告覆盖新版，原813件的报告只保留作历史基线。

The current browser proof checks six new works in both orientations, including return/paging/exit behavior and reproduction qualifiers. The preview has 5,264 guide layouts, 9,744 native gallery copies and 83 contact sheets; all 4,524 previous layouts were reused after fingerprint verification. Current reports are `preview/browser-validation.json` and `preview/final-consistency.json`, and the visible proof is `calligraphy-expansion-2026-10-08/browser-calligraphy-proof.jpg`.
