# 绘画和书法全库资料补充 · 第三轮

仍按全库 2,694 件统计，包括 1,765 件绘画和 929 件书法。补写或改写 51 件，其中 49 件在年度精选之外；年度阅读页同步 2 件。

| 类别 | 首次补写赏析 | 改写已有赏析 | 仍缺赏析 |
|---|---:|---:|---:|
| 绘画 | 25 | 4 | 419 |
| 书法 | 20 | 2 | 342 |

书法新增 12 件可读正文：林麟焻《行书五言律诗轴》核录八句；全魁、周煌、毛奇龄、祇园南海、福田古道人等作品只收确认的节录。现有 529 件书法有正文、节录或可读参考；400 件仍没有可用文字。已核节录保留“待核全文”标记。

- [直接阅读本轮 51 件及逐件来源](../../artifacts/development/art-guides-full-20261111/round-3/本次新增与改写导读.md)
- [待核作品与文献线索](../../artifacts/development/art-guides-full-20261111/round-3/待核作品与文献线索.tsv) · [绘画馆藏参考书目索引](../../artifacts/development/art-guides-full-20261111/round-3/绘画馆藏参考书目索引.jsonl)
- [全库逐件审计](../../artifacts/development/art-guides-full-20261111/round-3/full-library-audit.json) · [逐件编辑表](full-library-editorial-round3.zh.tsv) · [来源证据表](full-library-editorial-round3-evidence.json)
- [年度阅读页](annual-materials-2026-10-12.html)

石涛《秦淮忆旧图册》将册末自题和故宫学术季刊对第八开《秦淮探梅》的讨论合读，说明寄纸、旧游与舟中独望的关系。布朗什双人肖像用巴塞尔艺术博物馆的展览解说补入拉斐尔双人像的比较，并保留展方对后方人物身份的判断限度。全魁书轴从原图核出「冊封後一日書於敷命堂」，与东京国立博物馆特展第 164 号和冲绳县立博物馆的使行研究对应。出处：[石涛馆录](https://www.clevelandart.org/art/1966.31)、[《故宫学术季刊》图 52](https://www.npm.gov.tw/NewChineseArtDownload.ashx?bid=3849)、[巴塞尔展览第 2 单元](https://kunstmuseumbasel.ch/en/exhibitions/2026/the-first-homosexuals)、[全魁馆图](https://colbase.nich.go.jp/collection_items/tnm/TB-29?locale=zh)、[冲绳县立博物馆年报第 106 页](https://okimu.jp/userfiles/files/page/museum/issue/kanpou/nenpo_17.pdf)。

把本轮起点 444 件缺赏析绘画逐件与馆藏号和现有书目对应：258 件馆方列出参考书目，共 586 条。索引区分题录、尝试访问但未读到正文的条目和已进入导读的证据；单有书目、年代尺寸或不完整原图仍不足以填补空项。书法只按馆方译文能确认诗意时，可补赏析；没有可靠释文和可辨原图时，原文字段仍留空。短文与通用句式筛出的待复核记录仍保留，未被统计成已改写。

已核对全量目录、年度数据与阅读页、编辑证据及书法原图校验值，并运行导读打包和长文分页回归。资料仅更新在本地，未部署设备或发布。

重建审计：`python scripts/audit_full_art_library.py --output artifacts/development/art-guides-full-20261111/round-3 --dataset-dir artifacts/development/art-guides-full-20261111 --round 3`。
