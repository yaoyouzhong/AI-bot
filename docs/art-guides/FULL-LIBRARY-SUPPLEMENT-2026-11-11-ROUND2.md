# 绘画和书法全库资料补充 · 第二轮

范围仍为全量：1,765 件绘画、929 件书法，共 2,694 件独立作品。本轮在上一轮基础上新增或改写 54 件中文导读，52 件在年度精选之外。

| 类别 | 新增作品级赏析 | 改写已有赏析 | 当前仍缺赏析 |
|---|---:|---:|---:|
| 绘画 | 31 | 3 | 444 |
| 书法 | 15 | 5 | 362 |

书法新增可读文字 5 件：高泉性潡的单行五字完整核录；吴伟业寿诗、王士禛《念奴娇》、桂馥隶书扇面按馆图录入可确读节选；金农六言诗另附学术出版物所引通行文本，没有把它当成横披的逐字释文。书法现有 517 件提供正文或可读参考（包含节选），412 件仍无可用正文或参考。

- [直接阅读本轮 54 件](../../artifacts/development/art-guides-full-20261111/round-2/本次新增与改写导读.md)
- [全库逐件索引、来源与余项](../../artifacts/development/art-guides-full-20261111/round-2/full-library-audit.json)
- [本轮逐件编辑表](full-library-editorial-round2.zh.tsv) · [来源证据表](full-library-editorial-round2-evidence.json)
- [年度阅读页（本轮同步 2 件书法）](annual-materials-2026-10-12.html)

绘画优先使用原作题跋、保护报告和具体的递藏研究。吉约曼《沙朗通的马恩河岸》的 X 射线和红外成像都显示，横幅风景下面有一幅竖向女性肖像；肯塞特《纽波特附近景色》的红外成像显示，简练的水天构图曾先以含碳材料起稿。《江南春霭图》的自题交代汪牧庭将赴闽海；卡尔斯牌局的旧拍卖目录则以椭圆形、尺寸和灯下牌局的题名，为同件作品辨识提供证据。出处：[吉约曼](https://www.clevelandart.org/print/art/1963.573)、[肯塞特](https://www.clevelandart.org/print/art/1946.255)、[石涛](https://www.clevelandart.org/art/1954.126)、[卡尔斯](https://www.clevelandart.org/print/art/1980.241)。

书法依据馆藏原图、馆方题识、同件英译与已核正文来区分书体、书写者和作品内容。吴昌硕八言联的上款明记为子林集阮刻北宋本石鼓字；大都会《诗三首》同一扇面有文徵明、王守、王穀祥三人署名。博物馆只给出英译的作品，只按同件译文说明诗意，未反译成古文。出处：[吴昌硕馆录](https://artplatform.go.jp/ja/collections/W203442)、[大都会《诗三首》](https://www.metmuseum.org/art/collection/search/45786)、[东京国立博物馆吴伟业寿诗展录](https://www.tnm.jp/modules/r_exhibition/index.php?controller=item&id=8583)。

本轮取得并按馆藏号匹配了上一轮仍缺赏析的 475 件绘画的馆方页面。完整来源归入本地证据档案；只存在年代尺寸、一般性花絮或无法支持解读的图像说明时，赏析继续留空。书法仍存在只存款印描述、馆站限流以及原图有疑字的条目。索引也保留短句或通用句式的待复核标记，供后续逐件查看；标记本身不判定赏析无效。

全库打包字段、来源证据、年度读本数据和长文分页已检查。图像、选集与年度顺序保持；现有释文未被改写。本地资料未部署或发布。

重建第二轮索引：`python scripts/audit_full_art_library.py --output artifacts/development/art-guides-full-20261111/round-2 --dataset-dir artifacts/development/art-guides-full-20261111 --round 2`。
