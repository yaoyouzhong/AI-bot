# 书法原文与作品赏析 · 第七轮 · 2026-10-11

本轮新增2件可读原文和47条书法作品欣赏提示，纳入本地资源版 `2026.10.11.6`。可读原文累计372/867件（本幅核录229、明确标注参考版本143），年度书法188/366件；仍有495件待可靠核实。权威欣赏提示累计214条（绘画83、书法131），其余2418件暂未提供独立提示。

## 原文与缺字符

| 作品 | 本轮核实范围 |
|---|---|
| 楚石梵琦跋中峰明本书（`colbase-tnm-TB-36`） | 逐栏重新阅读完整馆藏原图，核录跋文正文。补馆方缺字符“海水”“歎”，并据墨迹校正“亘古亘今”“山靈”“請焉”“喜”“皆”等；复核“王始號海印”“王喜作亭”。年月、署款和印文除外。核对为本轮单人完整复读，不声称两人复核。 |
| 行书对联（`mia-129979`） | 完整核对上下联两张馆藏原图，确认所书为宋庠《屏居春日》第三联：“蠟屐幽懷高下齒，斲輪真想古今書。”提供这14字的文学参考文本，不拼入全诗；“幽”“斲”的草写仍不冒称逐字审定。参考版本及不确定范围在原文说明中明确标注。 |

原始图像、馆方资料、文学参考全文与SHA-256保存在[新增原文证据](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/new-originals.json)。此前审定正文保持不变。其余字形不能可靠辨认的对联和诗卷继续留在研究候选，不据常见句子猜补。

馆方碑刻释文中的 `□` 等有两种情况：已附补释的残缺字，以及确实尚不能辨认的残文；说明中引用馆方缺字标记也不是字体损坏。本轮逐条检查19个含标记字段，均为此前审定资料中明确说明的残缺或引文，保留其证据状态。全文没有发现Unicode替换字符或异常控制字符。

## 作者背景也是赏析的一部分

作者生平、创作经历、所处时代和取法背景，只要出处可靠、与本幅作品相关，就可以帮助理解作品。它们保留在导读中，也可与具体欣赏提示衔接；不会因为资料主要介绍作者就删除有用背景。

例如祝允明62岁时所书《前后赤壁赋》，本轮依据东京国立博物馆的作品说明，将其晚年消化魏晋唐宋书法的经历与本卷个人书风一起介绍。作者的一般风格评价仍与本幅具体分析分清，不据生平自行推断作品寓意、创作情绪或书体。

47条新增提示逐件依据东京国立博物馆ColBase的完整说明，涉及字形、笔势、章法、纸材、书写用途和取法背景；来源URL、字段定位、原始快照哈希及核验日期均保留。旧快照的原核对日期与本轮复核日期分别记录，未将本轮复核冒称首次采集。见[逐件提示及出处](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/new-looking.json)。

## 字库与本地验证

新原文需要的“瀋、頷、斲、蠟”4个设备字形已补入TAB5字库源码，字形数从9004增至9008。全库2632件作品的标题、作者、简介、欣赏提示、可读原文及说明共使用5283种字符，字库缺字为0。

实际LVGL测试核对原有9004个字形的尺寸，9000个非空字形的解码像素逐字节保持一致；4个新增字形分别解码且有有效像素。另检查旧压缩位图、描述符及字形映射完整保留。[新增字形实测图](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/new-glyphs.png)与[字库和字符检查](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/font-and-characters.json)可复核。

## 本地图库交付

| 包 | 作品 | 图片 | 可读原文 | 欣赏提示 |
|---|---:|---:|---:|---:|
| [年度名画2026.10.11.6](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/curated/packs/AI-bot-DailyPainting-Curated-2026.10.11.6.zip) | 366 | 860 | 0 | 82 |
| [年度书法2026.10.11.6](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/curated/packs/AI-bot-DailyCalligraphy-Curated-2026.10.11.6.zip) | 366 | 1782 | 188 | 93 |
| [全量书法2026.10.11.6](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/full-calligraphy/AI-bot-DailyCalligraphy-2026.10.11.6.zip) | 867 | 5708 | 372 | 131 |

三个ZIP附SHA-256、许可证和逐件来源。全量生成核对4144条来源证据；年度入选作品、顺序、完整分页、原图字节和全库元数据保持不变。资源版单独编号，没有重编桥接或固件版本。

包内CRC、全部条目SHA-256、文字与审定数据一致性通过检查；复用第六轮隔离构建的生产导入器，逐件检查年度书画和全量书法的横竖屏原文、导读与图片取读。全量书法组合测试的名画对照包沿用第六轮已审定版本，年度组合另测本轮两个新包。

[年度本地浏览文件](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/curated/index.html) · [完整验证记录](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/verification.json) · [待核原文清单](../../artifacts/development/calligraphy-originals-20261010/round-20261011-7/remaining-originals-current.json)

此为本地内容包与字库源码交付。本轮未部署桥接、刷入固件、安装用户图库或公开发布；未重新验收运行中的桥接兼容性。图库ZIP不携带设备字体，原生字形测试不等于真机显示已更新。

## English

Round seven adds two readable bodies or explicitly scoped literary references and 47 exact-work calligraphy notes. Totals are 372/867 originals (229 object transcriptions, 143 references), 188/366 annual originals and 214 viewing notes; 495 originals and 2,418 standalone notes remain unresolved. Relevant author biography, creation history and period context remain part of the guide without being asserted as unsupported analysis of an individual work.

Four missing font codepoints (U+700B, U+9837, U+65B2, U+881F) extend the font from 9,004 to 9,008 glyphs. All 5,283 required characters across 2,632 works are covered. Native LVGL verifies existing metrics, identical decoded pixels for 9,000 nonempty glyphs and valid pixels for all four additions. Documented historical lacunae remain explicit.

Edition 2026.10.11.6 includes local annual painting/calligraphy and full calligraphy ZIPs, with licenses, provenance and hashes. CRC, all entry hashes, reviewed text and isolated production imports in both orientations pass. Annual identity/order, full-library metadata and original images are preserved. No bridge deployment, device flashing, live gallery installation, commit, push or publication is performed; native glyph tests are not hardware acceptance.
