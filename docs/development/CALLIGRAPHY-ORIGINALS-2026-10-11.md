# 书法原文并行增补 · 2026-10-11

本轮从 655 件待补作品中新增核实 58 件，累计 270/867 件可读：166 件作品正文、104 件明确标注的参考原文。剩余 597 件（580 件待可靠原文、17 件原有待复核项），不表示这些作品没有原文，也不表示剩余作品均已深入研究。年度精选书法 366 件中，可读原文从 99 件增至 141 件。

This content-only round adds 58 reviewed originals, reaching 270 of 867 works: 166 object transcriptions and 104 labelled textual references. The annual selection now has 141 readable works. The remaining 597 are research gaps, not evidence that no original exists.

## 内容和核查

- 3 路并行研究，另由主协调者复核并补碑文。ColBase 贡献 23 件，MIA 11 件，Met/巴黎等 14 件，主协调者 10 件。
- 优先馆藏原图和馆方释文；明确篇目后，使用古籍、大学论文或公共文化机构资料。保留实际获取的来源快照、图像、SHA-256、核对范围与异文说明。
- 新增包括《乙瑛碑》《孔羡碑》《表忠观碑》《祀三公山碑》的参考释文，《兰亭集序》《洛神赋》《雪赋》的对应参考全文，以及诗、尺牍、对联和实际节录。
- 本幅只抄录一段时说明范围；参考全篇与作品实际墨迹分开。残损、馆录补字、实写异文不改成推测的标准答案；不将英文译文反译为原文。
- TB-1609 末句存在字形争议；MIA-97133《卧读书架赋》所获全文有疑似 OCR/繁化问题；TB-340/354《圣教序》所获馆录有未标出的省缺。这些条目保留待核，不计入新增。

## 图库交付

本轮包版本为 `2026.10.11.1`，保存在：

`artifacts/development/calligraphy-originals-20261010/round-20261011-2/`

- 年度书法：`curated/packs/AI-bot-DailyCalligraphy-Curated-2026.10.11.1.zip`，366 件、1,782 张横竖屏图片。
- 全量书法：`full-calligraphy/AI-bot-DailyCalligraphy-2026.10.11.1.zip`，867 件、5,708 张横竖屏图片。
- 配套年度名画：`curated/packs/AI-bot-DailyPainting-Curated-2026.10.11.1.zip`，366 件、860 张横竖屏图片；本轮未新增名画内容。
- 每个包均包含简介、欣赏提示、原文状态、已核实原文、说明、可携带来源、版权与校验文件。年度入选作品、顺序、原图字节保持不变。

`PACK.json` 使用此前已实现的 schemaVersion=2。兼容桥接完成首次接入后，后续此类内容更新只需重新导入图库包。共享工作区的[另项部署记录](../TAB5-ACCEPTANCE-168.md)记载桥接 0.6.4 / TAB5 .168 已接入该能力；本轮没有复验其部署或向真机导入这批内容，不能据打包成功宣称设备已显示新增原文。

## 验证与边界

包内验证结果及精确 SHA-256 以本轮 `verification.json`、各目录 `packs.json` 和 `.sha256` 文件为准。实际生产导入器在隔离测试目录运行，逐件校验两种排版的导读读回、作品身份和图片读取；用户图库、正在运行的桥接和设备不参与测试。

本轮不重新构建、安装或重启桥接，不构建或刷写固件，不改自动轮播，不提交或发布。旧包保留。最终核验发现共享工作区的 Release DLL 与 latest 固件哈希相较开工基线已改变，另项 .168/.169 部署记录与这些变化相符；如实列入 `verification.json.externalChanges`，不宣称这两个共享产物在整段期间未变。图库主目录和年度入选清单哈希仍须匹配开工基线。

新增正文与说明对比现有未构建字库源码发现 146 个未覆盖字符，详见 `font-coverage.json`。原文完整保存在包中；这项检查不代表已安装固件的字库实测。单篇最长原文仍为 7,390 UTF-8 字节，未截断内容以适配字库或传输限制。

## 继续补充的准确起点

- 当前剩余队列：`artifacts/development/calligraphy-originals-20261010/research-queue-current.json`。
- ColBase：326 件分配中，本轮深查 49、初筛 3、未查 265；另 9 件由主协调者处理。详见 `colbase/audit.json` 与 `colbase/next-candidates.json`。
- MIA：196 件中，深查 13、初筛 24、未查 159。详见 `mia/audit.json`、`mia/research-final.md`。
- Met/其他：130 件馆录及前轮证据复核；61 件年度作品完成首图初筛；14 件新增核实。其余 116 件尚需全文/字形研究，见 `met_other/audit.json` 与 `met_other/follow-up-queue.json`。首图初筛不等同于全篇释读。

所有相对研究路径均基于本轮目录；统计区分初筛、深入核查与可导入成果。
