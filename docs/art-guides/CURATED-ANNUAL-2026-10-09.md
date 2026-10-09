# 名画与书法年度精选 / Curated annual artwork edition

本地精选版 `2026.10.09`：绘画、书法各 **366 件独立作品**。全量库仍保留 1,765 件绘画、867 件书法；本次不替换全库目录，不安装到用户日常图库，不发布到网络。

## 入选范围

| 类别 | 入选内容 | 件数 |
| --- | --- | ---: |
| 中国传统绘画 | 山水与文人画 | 65 |
| 中国传统绘画 | 花鸟与走兽 | 43 |
| 中国传统绘画 | 人物与佛道 | 29 |
| 中国传统绘画 | 界画与风俗 | 7 |
| 欧美绘画 | 风景与海景 | 79 |
| 欧美绘画 | 肖像 | 50 |
| 欧美绘画 | 静物与花卉 | 25 |
| 欧美绘画 | 宗教与神话 | 43 |
| 欧美绘画 | 人物与生活 | 24 |
| 欧美绘画 | 现代构成 | 1 |
| 中国书法 | 墨迹、尺牍、扇面、对联及诗文 | 316 |
| 中国书法 | 经典碑帖拓本 | 50 |

绘画合计 366 件，其中中国作品 144 件、欧美作品 222 件；书法 366 件全部由既有馆藏证据确认属于中国书法。地域标签依馆藏的文化、制作地或国别记录，不根据藏馆所在国判断。

书法中，馆藏题名明确标注篆书 10 件、隶书 10 件、楷书 25 件、行书 91 件、草书 60 件、行草 20 件；其余 150 件保留原题名，不擅自判定书体。上述书体统计与墨迹/拓本统计是两种维度，不能相加。绘画题材是编辑为浏览组织的分类；不将它当作工笔、写意等技法的权威鉴定。

## 选择与完整性

- 以作品质量、知名度与名作代表性、附属资料完整性及中西艺术代表性为主要标准；分页和体积为辅助考虑。综合图像实际观看效果、原图细节、作者与题材、馆藏专属背景和已公布的年代/材质/尺寸/编号选择，逐项固定入选 ID，不按文件最小或目录前 366 件截取。
- 保留《睡莲》《卧室》《大碗岛的星期日下午》，以及唐寅、沈周、石涛、文徵明等中国画家的作品。欧美补充候选剔除题署为 imitator、copy after、follower 的作品；既有中国作品中的“传”“仿”“摹本”等限定原样保留。
- 书法保留《快雪时晴帖》（传唐摹本）、《伯远帖》、《中秋帖》（传）、《蜀素帖》及《祭姪文稿》（拓本）等；馆藏归属、作者争议与拓本限定随作品保留。拓本并不冒充墨迹原作。
- 普通候选优先不超过 8 个展示分页，少量代表作品例外：《汉宫春晓图》（仿仇英的摹本）10 页、《Desk Album: Flower and Bird Paintings》10 页、《虎丘十二景图》12 页、《九成宫醴泉铭》（拓本）13 页。
- **绘画共 430 页，书法共 891 页**，每页各有横、竖排版。分页、对联的两条、多联屏和册页都属于同一作品，不增加每日作品数量；所有入选作品使用全库已有的完整分页及原顺序。
- 已复核全部 732 件作品的首幅横屏缩略图；完整原图、组件与顺序的核查沿用全库扩充时的记录。此项不是 TAB5 真机验收。
- 图片直接复制全库已验证 JPEG，保留字节及哈希，不重新压缩、改色或生成式修复。来源、图像许可、署名和加工记录随 ZIP 附带。

## 本地交付与复现

固定清单：[`curated-annual-2026-10-09.json`](curated-annual-2026-10-09.json)。它记录每件入选 ID、作者、题材或馆藏书体、页数、入选说明及馆藏证据，锁定全库元数据 SHA-256。

每日轮换顺序也固定在清单中，绘画交错安排中西题材，书法在相同页数内交错安排书体。调整浏览标签不会自动改变日期对应的作品。

构建脚本：`scripts/build_curated_art.py`。从仓库根目录执行，输出目录必须是新目录：

```powershell
$env:PYTHONIOENCODING='utf-8'
python scripts/build_curated_art.py --selection docs/art-guides/curated-annual-2026-10-09.json --output artifacts/development/curated-annual-2026-10-09/rebuild
```

现有本地输出为 `artifacts/development/curated-annual-2026-10-09/build-selected/`：

- `selection.csv` / `selection.json`：最终清单，适合表格筛选与逐项复核。
- `index.html`：支持按类别、作者和题材筛选，展开查看所有横屏分页及现有导读。
- `guides.zh.json`：732 件入选作品的导读预览资料；尚未接入正式屏保程序，不能将审阅页当作已集成。
- `packs/AI-bot-DailyPainting-Curated-2026.10.09.zip` 与 `packs/AI-bot-DailyCalligraphy-Curated-2026.10.09.zip`：可独立导入的两类精选资源包，以及对应 SHA-256 文件。
- `annual-validation.json` / `validation.json`：366 件年度完整性、JPEG 规格、来源与分页、全量库元数据未变检查。
- `consumer-validation.log` / `consumer-validation.json`：Windows 程序在隔离配置中导入最终 ZIP，校验全部 JPEG，并分别读取闰年 366 天及横竖两种布局；另有 1×、1.5×、2× 图库管理界面截图。
- `browser-validation.json` 与浏览截图：两类筛选各返回 366 件，代表作搜索、分页、导读及馆藏链接可浏览。

最终绘画包为 **221,410,711 字节（221.41 MB / 211.15 MiB）**；书法包为 **447,081,384 字节（447.08 MB / 426.37 MiB）**。合计 **668.49 MB / 637.52 MiB**，较本地全量 ZIP 合计 2,516,053,459 字节减少约 **73.4%**。减小体积来自精选作品数量，图片未重新压缩。

同类别 ZIP 导入会替换当前使用的图库，并保留旧目录备份；应用升级保留已安装图库。本人继续使用本地完整版，公开用户可使用精选版。现有程序尚不支持把多份同类别专题包自动合并。

本地打包完成不表示公开发布；线上下载链接仍指向此前已发布的资源。精选资源将来可按其独立资源版本发布，无需为图库重刷固件。

## English

Local curated edition `2026.10.09` contains exactly 366 paintings and 366 source-confirmed Chinese calligraphy works. Paintings include 144 Chinese works and 222 European/American works; calligraphy includes 316 ink works and 50 rubbings. Editorial painting subjects and museum-explicit script labels are separate from technique or attribution claims.

Selection prioritizes image quality, recognized artists and representative works, completeness of attached museum information, and representation of Chinese and Western traditions. Page count and package size are secondary. Editorial scores are review aids, not objective art-value rankings.

The edition retains 430 painting pages and 891 calligraphy pages, with both native layouts per page. Complete works, panels, couplets and albums count once. Existing qualifiers, disputed attributions, copies and rubbing identities remain intact. Ordinary candidates favor eight or fewer frames, with four documented representative exceptions of 10–13 pages. All first-page landscape thumbnails were reviewed; previous complete-source review remains the provenance basis. Native JPEG bytes and source metadata are retained.

The locked JSON selection and `scripts/build_curated_art.py` reproduce the edition in a fresh output directory. The local output includes the searchable review page, CSV/JSON lists, separate preview-only Chinese guides, importable ZIPs with licenses and SHA-256 sidecars, and verification reports. The full local library is unchanged. Importing another pack of the same category replaces the active collection with a backup; automatic additive merging is not supported. This local delivery neither publishes assets nor establishes hardware acceptance.

The annual sequence is explicit in the manifest; label edits do not alter date assignments. The final painting ZIP is 221,410,711 bytes and the calligraphy ZIP is 447,081,384 bytes, totaling 668.49 MB, about 73.4% smaller than the full-library ZIPs without recompressing images. The Windows consumer imported both final packs under an isolated profile, validated all JPEGs, and read all 366 leap-year dates in both layouts. Browser checks covered category counts, representative-work search, pages, guides and source links.
