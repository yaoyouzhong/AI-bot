# 全量书画继续采集 · 第十轮 · 2026-10-11

本轮新增60条名画、24条书法欣赏提示，以及3件书法正文或古籍参考，全部来自年度精选之外。累计377/867件可读正文或参考（核实正文230、参考147），欣赏提示415条（名画183、书法232）。全量名画与书法包更新为本地资源版`2026.10.11.9`，年度版`2026.10.11.8`的作品、顺序与导读逐项保持一致。

## 还能继续收集到什么、何时停止

仍有1063件名画已保存馆方作品说明、36件有补充字段候选，尚待完整阅读与中文整理；另483件当前馆录未提供可用分析。这1582件均登记了现阶段原因和下一步，不将“尚未整理”写成“收集不到”。书法原始缺漏490件，其中3件已有逐件略过决定，487件继续核对，含年度之外311件、年度176件。本轮10件书法核图仍未能可靠补全，保存具体疑难和原图哈希，未把局部读字算成全文。

收集按作品逐件结束，无须无限补资料，也不按固定轮次停止：

- **完成**：可靠正文或明确版本的参考、足够的作品赏析及相关背景已取得，来源和正文范围可核对；有据可读即可，不追求作者生平百科全书式完整。
- **无需补**：单字“虎”“寿”等不另找长篇正文；保留已有可靠字文和欣赏资料。
- **证据不足而暂缓**：实际核查馆方记录、可用原图与相关可靠著录后，仍有不可判字、残损、版本不明或资料无法取得，保存查过的对象、结果、缺口及原因，等新证据再重开。
- **继续**：当前馆录没有正文、仅有元数据、或已有说明还未整理，均不是“已经查尽”。全库只有每件适用作品完成或具备经调查支持的暂缓理由，才可宣布本阶段收集结束；当前尚未达到。

[逐件研究状态](../art-guides/research-status.zh.json)保存全部490件原文缺漏与1582件名画提示缺漏的出处、快照哈希、现状、原因及下一步。每项均明确`exhaustiveSearchCompleted=false`；本轮没有声称所有作品已穷尽外部搜索。[统计](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/research-status-summary.json)和[当前书法队列](../../artifacts/development/calligraphy-originals-20261010/research-queue-current.json)保留有效待核对象，年度之外优先。

## 新原文与版本边界

| 作品 | 内容与依据 | 核实范围 |
|---|---|---|
| Mia 118908 篆书 | 馆方录文与完整原图核对“行仁義事，致遠經方。” | 主文八字，整理标点；不录侧款印章 |
| 东京国立博物馆TB-871，康熙《虎丘》 | [《西陂类稿》卷四十一所引御制诗](https://www.shidianguji.com/mid-page/7439098725068193830)，完整40字 | 古籍参考版本；原轴中段、末句有差异，末句墨迹“貴”与参考“盡”分别保留 |
| 东京国立博物馆TB-1454，乾隆《清涼山》 | [《新修江宁府志》卷一御制诗](https://www.shidianguji.com/book/NLG312001076319/chapter/1ljvt2fiimrr4)，完整56字 | 古籍参考版本；首尾与主文对应，尚不称逐字墨迹释文；排除署款 |

[三件新增审定记录](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/new-originals.json)保存馆录、实际原图、正文边界、主文核图范围和出处；古籍来源保存为网页读取的完整诗文摘录，明确方法为`web_tool_full_poem_excerpt`，不冒充原始HTML下载。已有正文、异文与历史缺字标记完整保留，不据英语译意或常见成语猜补。

## 欣赏提示与作者经历

60条名画均采用CMA逐件馆方说明，24条书法采用CMA、Mia与ColBase资料。[新增84条记录](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/new-looking.json)保存完整馆方分析、中文改写、关键事实对应片段与来源哈希；相关作者经历可以纳入赏析，保留馆方的传称、推测与年代限定，不据一般生平推断本幅情绪。

本轮资料涵盖游历、作画地点、素描到工作室成画、委托及收藏背景等。作品身份与参考诗作者、书者分开；生平信息必须有可靠来源并与本幅观看有关。原文页、简介与欣赏提示保留各自范围，无可靠依据的独立提示留空。名画仍有1582件、书法仍有635件缺独立审定提示，未宣称全库赏析完成。

## 缺字补齐与验证

新增古籍参考涉及“剎、畝、蘿、賒”4个缺字，已用原有来源字体及保留算法补入相邻TAB5字库源码，字形从9008增至9012。当前全库2632件作品的标题、作者、简介、赏析、可读原文和说明使用5321种字符，缺字为0；19处已审定历史缺文或馆录标记原样保留，未出现新的编码破损或无说明占位符。

实际LVGL字形测试核对原有9008个字形的尺寸，其中9004个非空字形的解码像素逐字节一致；4个新增字形均有有效像素。旧压缩位图、描述符和字形映射完整保留。[字形实测图](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/new-glyphs.png)、[字符报告](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/font-and-characters.json)和[原生测试日志](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/font-extension.log)可复核。字库源码变更不代表设备已更新。

## 全量本地资源包

| 资源包 | 独立作品 / 原始分页 | 可读书法 / 提示 | SHA-256 |
|---|---|---|---|
| [全量名画ZIP](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/full-library/AI-bot-DailyPainting-2026.10.11.9.zip) | 1765 / 4036 | 0 / 183 | `8fea295e5dfd2c72f3cb90f299705e4aefeacdb673ee9c7ea147e21c173a4c03` |
| [全量书法ZIP](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/full-library/AI-bot-DailyCalligraphy-2026.10.11.9.zip) | 867 / 5708 | 377 / 232 | `490bed2c819e95f9e287649f7943a1efd6ee64153811dce927b28d8d52f2224f` |

ZIP包含许可、出处、导读和内容哈希。全包CRC、所有条目哈希、原图逐字节副本、审定文字、旧资料保留与年度内容不变均核验。复用第六轮隔离生产程序集执行真实导入，逐件验证两种方向的精确文字与取图：名画1765件/4036分页、书法867件/5708分页，全部通过。[综合验证](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/verification.json)与[生产导入日志](../../artifacts/development/calligraphy-originals-20261010/round-20261011-10/full-import.log)保存证据。

本轮仅本地内容与字库源码交付，未部署桥接、构建设备固件、刷机、安装用户图库、提交或公开发布；复用程序集测试不冒称当前运行桥接验收，字形主机测试不替代真机验收，ZIP不携带设备字体。

## English record

Round ten adds 60 painting notes, 24 calligraphy notes and three readable originals/references, all outside the annual selection. Totals are 377 readable texts (230 verified bodies, 147 labelled references) and 415 notes (183 painting, 232 calligraphy). Full-library edition `2026.10.11.9` covers 1,765 paintings/4,036 frames and 867 calligraphy works/5,708 frames. Annual edition `2026.10.11.8` remains byte-for-byte equivalent in reviewed guide records and unchanged in selection/order.

Remaining research includes 487 active originals (311 nonannual, 176 annual), three previously reviewed exclusions and 1,582 painting notes. Of those painting gaps, 1,063 retain descriptions and 36 retain supplemental candidates. Per-work reasons describe current evidence and do not claim exhaustive searches. Collection ends individually when useful sourced content is complete, is inapplicable for a single-character original, or has a documented evidence barrier after actual research. Whole-library closeout has not been reached.

Four glyphs extend the font to 9,012, covering all 5,321 required characters. Native LVGL preserves metrics for all 9,008 old glyphs and identical decoded pixels for 9,004 nonempty old glyphs, with valid new pixels. Package CRC, every entry hash, exact source-image bytes and isolated production imports in both orientations pass. No live installation, firmware build, hardware update, commit or publication is claimed.
