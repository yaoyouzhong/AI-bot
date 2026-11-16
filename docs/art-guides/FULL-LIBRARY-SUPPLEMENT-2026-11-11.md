# 绘画和书法全库资料补充 · 2026-11-11

本次检索与盘点范围为 **2,694 件作品**：主库 1,765 件绘画、867 件书法，另加 62 件未与主库重复的书法资料档案。对照年度选集时按类别与 `workId` 去重；分页、不同方向的展示图、重复收录不增加作品数。

| 类别 | 全库范围 | 本次写入 | 年度外 | 有作品级赏析 | 仍无作品级赏析 |
|---|---:|---:|---:|---:|---:|
| 绘画 | 1,765 | 49 | 27 | 1,290 | 475 |
| 书法 | 929 | 9 | 9 | 552 | 377 |

58 件包括新补背景、改写已有赏析或首次补入赏析；其中 11 件原来没有作品级赏析。覆盖数包含旧资料，不能理解为所有旧导读已重写。全库书法有 512 件提供可读文字或有出处的参考，包含本次新增的两件通行本节选；仍有 417 件无可用正文或参考。写本节选不等于全文校勘。

- [直接阅读本次 58 件导读](../../artifacts/development/art-guides-full-20261111/本次新增与改写导读.md)
- [逐件全库索引与剩余字段](../../artifacts/development/art-guides-full-20261111/full-library-audit.json)
- [克利夫兰馆方资料索引](../../artifacts/development/art-guides-full-20261111/cma-work-specific-source-material.json)
- [原年度阅读页（其中 22 件已更新）](annual-materials-2026-10-12.html)

绘画优先补作品为何而画、构图和笔法怎样服务于题材，以及来源能证实的故事。莫奈 1906 年《睡莲》写入技术检查发现的多层颜料和被盖掉的莲叶；陈洪绶《宣文君授经图》写入六十寿辰的创作缘由及铁线描、矿物色与礼器；年度外《退潮时的普尔维尔》补入莫奈在诺曼底住处的书信背景和崖脚笔痕实际画的是浴者。出处：[芝加哥艺术博物馆学术图录](https://publications.artic.edu/node/135621)、[陈洪绶馆藏页](https://www.clevelandart.org/art/1961.89)、[普尔维尔馆藏页](https://www.clevelandart.org/art/1947.196)。

书法按同件辨识书写者、受书者、题跋与传藏层次。云顶德敷尺牍依据日本文化厅资料补入赠木和重建经过、唐纸与笔迹特点；《保宁寺赋跋》依据东京国立博物馆 e 国宝解说，更正后半附记为古林清茂所写、月林道皎为受赠者。敦煌写经 TB-1430、TB-1449 核对馆藏号、原图和 CBETA 固定版；后者仍保留写本自题第三十三卷，并另注对应通行本第二十四、二十五卷。出处：[德敷尺牍](https://online.bunka.go.jp/heritages/detail/212219)、[保宁寺赋跋](https://emuseum.nich.go.jp/detail?content_base_id=100858&content_part_id=0&content_pict_id=0&langId=zh)、[东京国立博物馆写经展](https://www.tnm.jp/modules/r_exhibition/index.php?controller=item&id=7650)、[CBETA 正式经文资料库](https://github.com/cbeta-org/xml-p5)。

全库逐件登记现有背景、赏析、文字状态和馆藏入口；克利夫兰美术馆固定版开放数据按馆藏号匹配 1,753 件绘画，其中 1,284 件有至少一个馆方说明字段。馆方英文原始字段作为来源档案保留；通用题材知识、无关花絮、只有年代尺寸的条目和未校订的馆录释文不计作本次完成。此检查不代表其他馆的公开展览图录和论文已经检索穷尽。

主库和年度数据已同步。2,632 条主库导读通过打包器的身份、来源和容量检查；真实图像的小样包和年度七篇长文分页检查通过；年度阅读页内嵌 732 条与年度 JSON 对应。原选集、排序、图像及既有书法释文保留。本次是本地资料修订，未安装图库、刷写设备或发布。

复现：`python scripts/apply_full_art_editorial.py` 校验并同步修订；`python scripts/audit_full_art_library.py --output artifacts/development/art-guides-full-20261111` 从锁定的官方数据与本地馆藏证据重建全库索引。编辑原稿与出处在 [逐件修订表](full-library-editorial.zh.tsv) 和 [来源校验表](full-library-editorial-evidence.json)。
