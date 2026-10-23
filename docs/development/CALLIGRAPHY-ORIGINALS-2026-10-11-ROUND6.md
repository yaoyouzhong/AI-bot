# 书法原文与权威赏析 · 第六轮 · 2026-10-11

本轮继续核对剩余作品，将审定内容纳入本地新图库包 `2026.10.11.5`；并补齐 TAB5 字库源码。桥接程序、设备固件和用户已装图库未部署更新，没有提交、推送或公开发布。

新增 3 件完整正文或明确标注的参考原文，可读原文累计 370/867，年度书法 187/366，仍有 497 件待可靠核实。新增40件名画欣赏提示，累计 167 件；其余 2465 件提示留空。

## 原文与补字

正文标点为阅读整理；主体范围和参考版本逐件说明，不将署款、日期或其他版本混入本幅逐字释文。旧审定记录不变。

| 作品 | 核实范围 |
|---|---|
| 草书杜甫诗（`mia-124722`） | 原图与馆方题名可可靠识别为杜甫《秋兴八首》其一完整正文。据台北故宫同篇释文提供参考全文，不并署款、日期和印文；泪字依参考文本，未把异体与草书省并笔逐字审定。不是祝允明本的逐字释文。 |
| 杰山偈（`colbase-tnm-TB-1438`） | 两人独立核完整二十八字正文。百匝依墨迹校馆方万湮；潭已核水旁、右覃结构。傑山號為賦、休居叟清茂为题款；泰定三年正旦日為靈巖偉首座書为年月及受书人款，均不并入正文。 |
| 楷书送以亨诗（`colbase-tnm-TB-37`） | 完整五十六字正文逐栏核图。生林经两人独立核形，生有竖撇与横画、林为双木，按本幅保留，未据语意改成扶桑。彷佛、衆依墨迹。右侧奉送以亨藏主歸日本为题款，至正二十四年及嘉禾天寧萬金书于深雪轩为年月署款，除外。 |

馆方释文存在缺字或错字时，结合原作清晰图像逐字复核再补正；不能确定的字仍记入研究候选，不放入读者正文。文义不寻常也不擅自换成常见句子。

## 欣赏提示来源

本轮40件新增赏析均依据克利夫兰艺术博物馆对应藏品的完整分析，逐条保留作品身份、来源URL、字段定位、快照SHA-256及核对日期。中文为据馆方分析的简明改写，保留可能、传为等限定。旧快照的获取证据与本轮复核日期分别登记；没有出处的提示为空。

## 字库修复与显示验证

字库从 8716 增至 9004 字形，补入 288 个字符；核对全库2632件的题名、作者、说明、赏析及全部可读原文，涉及 5273 种字符，缺字为0。

生成器在新字追加时保留已有字形的尺寸和压缩像素，并在所需字符没有字体来源时明确报错。新增 Noto Sans / Noto Sans Math 官方字形用于窄不换行空格和箭头，来源版本、SHA-256和SIL OFL许可已记录；生僻汉字继续使用既有有许可补充字体。

- 实际 LVGL 字形测试：原有8716字形的尺寸全部相同，8713个非空字形的解码像素逐字节相同；新增288字形逐个解码，除窄空格外均有有效像素；[新增字形实测图](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/new-glyphs.png)可复核。
- 实际 LVGL 图库预览：全量书画字段和原文无占位字形，最长原文可滚动到底；横竖屏和离线打开通过。
- 此为源码和原生渲染验证；图库ZIP不含固件字库，未刷入设备，不能宣称真机显示已更新。

## 本地新图库包

| 包 | 作品 | 图片 | 原文 | 权威赏析 |
|---|---:|---:|---:|---:|
| [AI-bot-DailyPainting-Curated-2026.10.11.5.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/curated/packs/AI-bot-DailyPainting-Curated-2026.10.11.5.zip) | 366 | 860 | 0 | 82 |
| [AI-bot-DailyCalligraphy-Curated-2026.10.11.5.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/curated/packs/AI-bot-DailyCalligraphy-Curated-2026.10.11.5.zip) | 366 | 1782 | 187 | 46 |
| [AI-bot-DailyCalligraphy-2026.10.11.5.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/full-calligraphy/AI-bot-DailyCalligraphy-2026.10.11.5.zip) | 867 | 5708 | 370 | 84 |

三个ZIP均附SHA-256、许可证和逐件来源；CRC及包内校验通过。全量生成核对 4092 条来源记录，年度作品选择、稳定身份和原图字节保持不变。隔离构建的生产导入器逐件核对文字、图片与横竖屏，年度书画及年度名画/全量书法两组均通过。测试未安装到用户图库。

运行中的桥接程序与新包的兼容性未重新做真实用户路径验收；当前交付为本地资源包和字库源码，没有设备安装验收。其他会话对固件交付物的变化单独记在验证记录，不由本轮替换。

[完整验证](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/verification.json) · [全量字库检查](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/font-coverage.json) · [原生预览日志](../../artifacts/development/calligraphy-originals-20261010/round-20261011-6/native-preview.log)

## English

Local edition 2026.10.11.5 adds 3 complete or explicitly scoped reference texts (370/867 total; 497 unresolved) and 40 exact-work CMA analyses (167 total; 2465 empty). Original image readings, version boundaries and uncertainty remain explicit. Source snapshots, identity, provenance dates and hashes are retained.

The TAB5 font source now has 9004 declared glyphs, adding 288. All 2632 catalogue entries and readable original texts have zero missing glyphs. Native LVGL verifies every existing glyph metric and nonempty bitmap is unchanged, and exercises complete original readers in both orientations. Sources and SIL OFL licenses for supplemental glyphs are preserved. Gallery ZIPs do not install firmware fonts.

All three packages pass hashes, CRC and isolated production imports with exact text/image readback. No bridge deployment, device flashing, user-gallery installation, commit, push or public release was performed. Native rendering is not hardware acceptance.
