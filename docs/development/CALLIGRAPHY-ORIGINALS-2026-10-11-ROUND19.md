# 作品资料完善 · 第19轮 · 2026-10-11

本轮新增98条逐件欣赏提示：年度书法38条、年度名画48条、年度之外书法12条。年度名画现366/366件有提示；本轮48条明确标为据馆方原图编写的整理者观察，馆方深层分析及创作资料仍另列补充队列。原文没有新增达到审定标准的件，参考释文没有因新接口返回而自动升格。

## 当前覆盖

| 范围 | 独立作品 | 可读原文或明确标注的参考 | 欣赏提示 | 尚缺原文 | 尚缺提示 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 年度书法 | 366 | 192（审定112、参考80） | 250 | 174 | 116 |
| 年度之外书法 | 501 | 189（审定119、参考70） | 191 | 309待核、3逐件评估后忽略 | 310 |
| 年度名画 | 366 | 按作品实际需要处理题跋 | 366 | 不以画作原文缺省作缺口 | 0 |
| 年度之外名画 | 1399 | 按作品实际需要处理题跋 | 960 | 同上 | 439 |

全库2632件，1767件有审阅提示（名画1326、书法441），865件仍缺提示。书法381件可读原文或参考，483件主动检索缺口仍保留；此前3件忽略决定未改。年度书法133件同时有可读原文和提示，年度之外90件；不能把一个字段完成当作整件资料已齐。

## 查证内容与可靠性

- 同件证据共564件：ColBase 339、Mia 63、CMA 48、Met 113，另巴黎王铎1件保留馆方IIIF记录并记下本轮馆藏网页超时。174件年度原文缺口均逐件列出处和本轮结果。部分证据为已保存的完整馆方记录，核验日不冒称首次获取日；ColBase中202件使用保留记录、137件取得新记录。
- 新提示47条依据完整同件馆方分析改写，51条为明确标识的馆方原图观察。后者包括48件年度名画、两件Mia年度书法和丁敬《水仙花赋》扇面；不将观看方向冒充馆方结论或作者意图。
- Met馆藏网页补充开放接口缺失的分析。作者经历只在与本件有明确关系时纳入：例如[法若真《画说》](https://www.metmuseum.org/art/collection/search/49129)的仕途与文意，[黄道周、倪元璐合书卷](https://www.metmuseum.org/art/collection/search/73330)的交谊和相互题写。[渔父诗](https://www.metmuseum.org/art/collection/search/40059)保留高宗、孝宗归属限定。
- 48件年度名画逐件核对当前CMA接口的分析字段及现有原图。两册作品的8帧、10帧全部逐页查看，仍分别只计一件作品。接口没有可用分析，书目仅作后续线索，未读书目正文不进入赏析事实。
- NPM苏轼尺牍与吴琚书七言绝句使用已保存的完整馆方说明：将苏轼北归行旅、问候与晚年书风联系起来；吴琚所书诗作者为蔡襄，不能混淆诗作者和书写者。

## 参考释文与疑字

150件既有参考释文的来源文件哈希、状态、范围与正文保留情况已核对。这不是新完成150件墨迹逐字精校的声明。

导读题名“临李贽尺牍”已更正为“临李治《艺韫帖》扇面”。已有参考正文与解释本来正确，原样保留；依据为[国立故宫博物院同篇馆录](https://digitalarchive.npm.gov.tw/Collection/Detail/16452?dep=P)、[香港中文大学论文第491页](https://www.cuhk.edu.hk/ics/journal/articles/v15p479.pdf)以及本件馆图。资产身份、年度入选与排序没有因题名修正而替换。

重新核看三件已有参考释文的完整馆图，继续保留：韩天寿念佛书的“維”及第三分句；何绍基单轴末字翁形与古籍“螉”的差异；龙公美八字书的第二字“啀”与草书墨迹差异。保留参考版和异文说明，不凭常见成语、语义或通常念佛语补字。

本轮七件原文再核图：解缙草书、桂馥扇面、Mia篆书对联单侧、张瑞图韦应物诗、丁敬《水仙花赋》、陈鹏年五言绝句、许友《社集蜃舫》。草书疑字、仅存单侧、同题不同篇等原因逐件保留。许友虽新取得完整馆录候选，但“開橋”“万子”等与原图若干字形未对应，不能因此补入可靠完整原文。翁方纲已有审定字不被日文馆录“秋間”“清分”等误录覆盖。

馆页中的嘉庆/嘉靖、逆序生卒年以及倪瓒、米芾生年差异另留登记，新提示不转录这些有争议数据。

## 本地验证与包

四个最终本地包为`2026.10.11.18`。所有ZIP CRC、每个包内文件SHA-256、图片对照与已有正文/提示保留检查通过。固定年度366+366件身份和顺序、完整库元数据及全部随包图片保持一致；隔离生产导入器逐包验证原文、提示和横竖屏传输通过。最终包在`full-library-final`与`curated-final/packs`；此前初步包作为过程证据保留，书法最终包包含最后补入的两条提示。

| 包 | 作品 | 横竖屏帧 | 文件 | SHA-256 |
| --- | ---: | ---: | --- | --- |
| 完整库 名画 | 1765 | 4036 | [AI-bot-DailyPainting-2026.10.11.18.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/full-library-final/AI-bot-DailyPainting-2026.10.11.18.zip) | `d1dbbb150a649e8a3b3879fe9d6a0c07740f0f94d589b49ead6e5f5137c1f3ae` |
| 完整库 书法 | 867 | 5708 | [AI-bot-DailyCalligraphy-2026.10.11.18.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/full-library-final/AI-bot-DailyCalligraphy-2026.10.11.18.zip) | `26a45f768e7b9684a409905310af8d286d41716eba9e5969442d565113794d39` |
| 完整库 名画 | 366 | 860 | [AI-bot-DailyPainting-Curated-2026.10.11.18.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/curated-final/packs/AI-bot-DailyPainting-Curated-2026.10.11.18.zip) | `4bbbc820cc4c3e15cad09791635a511980a48aad9fb167a181102df649e068ed` |
| 完整库 书法 | 366 | 1782 | [AI-bot-DailyCalligraphy-Curated-2026.10.11.18.zip](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/curated-final/packs/AI-bot-DailyCalligraphy-Curated-2026.10.11.18.zip) | `1384fd1781c21ef5c3b855e14b3d516a94a84b11b928842a997b0cd8630a59ff` |

字库源码9027字形完全未改，覆盖当前全部5559种显示字符，缺字0；未见新增乱码或未说明的占位符。原作残损及文献本身的方框仍连同原因保留，不能把它们当字体缺字猜补。没有桥接或固件部署，也没有替换用户已安装图库；此轮是本地内容交付，未作硬件验收。

## 接续文件

- [逐件验证结果](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/verification.json)
- [年度174件原文核查](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/annual-original-evidence-review.json)
- [48件年度名画后续分析线索](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/annual-painting-evidence-review.json)
- [参考释文复核及三件疑字](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/reference-precision-review.json)
- [七件原文核图原因](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/deferred-originals.json)
- [来源差异登记](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/source-uncertainties.json)
- [年度及年度之外接续索引](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/continuation-index.json)
- [年度全部732件预览](../../artifacts/development/calligraphy-originals-20261010/round-20261011-19/curated-final/index.html)

下一轮仍按年度原文、年度书法提示、参考释文具体疑字优先，再推进年度之外的原文和提示。足够支持观看的提示结束当前整理；若发现新同件文献或新原图再补。没有完整主文、疑字未定、只有基本资料或未读书目时，保留原因，不标作资料已齐或检索已穷尽。

## English

Round 19 adds 98 individually reviewed notes: 38 annual calligraphy, 48 annual paintings and 12 nonannual calligraphy. Annual paintings now have 366/366 notes, including 48 explicitly labelled editorial observations from exact museum images. Those images do not supply missing museum textual analysis; further background research remains separately indexed.

Annual calligraphy has 192 readable originals or explicit references and 250 notes, leaving 174 original gaps and 116 note gaps. Nonannual calligraphy has 189 originals/references and 191 notes, with 309 active original gaps, three preserved exclusions and 310 note gaps. Nonannual paintings retain 439 note gaps. All 174 annual original gaps receive evidence checks; no new original reaches the verification threshold.

The 564 exact-object checks combine current and retained official evidence, clearly distinguishing acquisition from review dates. Forty-seven new notes paraphrase complete museum analysis; 51 are editorial image observations. Relevant biography is used only when tied to the individual work. The guide title for Met 49146 is corrected to Li Zhi (Emperor Gaozong of Tang), not Li Zhi the Ming philosopher; its accurate existing reference text is preserved. All 150 reference sources and texts are checked for integrity, while three prior ink disagreements remain explicit after focused image review.

Four final local packs pass every ZIP CRC and entry hash, unchanged metadata/image checks and isolated production imports in both orientations. The unchanged 9,027-glyph font covers all 5,559 displayed characters with no missing glyphs. Source lacunae are preserved, not guessed. No bridge, firmware or live gallery deployment and no hardware acceptance are claimed. This round does not complete the whole library.
