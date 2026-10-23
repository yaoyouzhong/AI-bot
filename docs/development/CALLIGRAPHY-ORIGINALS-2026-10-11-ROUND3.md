# 书法原文并行增补 · 2026-10-11 第三轮

从 597 件待补作品中新增核定 76 件，累计 346/867 件可读：211 件作品正文、135 件明确标注的参考原文。剩余 521 件（507 件待可靠全文、14 件原有待复核项）。年度精选书法可读 171/366 件。没有把初筛、疑字候选或局部释文冒充全文补齐。

This content-only round adds 76 reviewed originals, reaching 346/867, with 521 research gaps remaining. Annual coverage is 171/366. Screening is not a claim of complete transcription or exhaustive source research.

## 来源与核查

3 个子 agent 分工研究 ColBase、MIA、Met/其他馆藏，主协调者独立复核原图、图库身份和资料证据，并补核碑文。优先博物馆、文化机构与大学资料；明确篇目后使用古籍参考全文。日本文化厅同件馆藏“铭文”补充了 ColBase 未给出的正文。每件保存实际取得方式、快照、SHA-256、核对范围与必要异文说明。

新增长文包括《集王圣教序》《雁塔圣教序》《曹全碑》《史晨碑》前后碑、《礼器碑》《张迁碑》《尹宙碑》《爨宝子碑》等核定范围内的参考全文。馆录补字、残拓现存字、后跋题名与原始正文明确分开。诗文参考本与实际墨迹有差别时直接说明，不用通行版本悄悄替换。

全轮分工审计在 `colbase/audit.json`、`mia/audit.json`、`met_other/audit.json`；文化厅专项复查另见 `mia/bunka-audit-001.json` 及后续批次。首图联系表初筛不等于完整册页逐字核对。剩余队列保存于 `artifacts/development/calligraphy-originals-20261010/research-queue-current.json`，不是“这些作品不存在原文”的断言。

## 图库包

本轮资源版本 `2026.10.11.2`，目录：`artifacts/development/calligraphy-originals-20261010/round-20261011-3/`。

- `AI-bot-DailyPainting-Curated-2026.10.11.2.zip`：366 件作品，860 张图片，可读原文 0 件；SHA-256 `4a06ef4e1e098123fce3ef5f197df3579d769ce531e36b72375026fa19a7ed21`。
- `AI-bot-DailyCalligraphy-Curated-2026.10.11.2.zip`：366 件作品，1782 张图片，可读原文 171 件；SHA-256 `47188cf1414e9cd8fd7153cab2fadf6d6989c4ec64d28e9786e28e477b4c083d`。
- `AI-bot-DailyCalligraphy-2026.10.11.2.zip`：867 件作品，5708 张图片，可读原文 346 件；SHA-256 `5002319eddd7f1d2e69f553375661568f4ca694c73db73837210df1afeb95125`。

年度包位于 `curated/packs/`，全量书法位于 `full-calligraphy/`。每包携带简介、欣赏提示、原文、说明、可移植来源、版权文件和校验信息。名画文字本轮未变，按同一内容版本配套生成；年度作品选择、顺序及图片保持不变。兼容桥接完成首次格式接入后，后续此类增补只需导入相应图库 ZIP。

## 验证与限制

全部新增来源与图片证据 SHA-256、包内 ZIP CRC、元数据哈希、作品身份与正文逐字一致性通过。使用既有生产导入器二进制在隔离测试目录导入年度书画及全量书法，检查每件横竖屏导读原样读回和取图，两个日志共四个 `GENERATED_GUIDE_PACK_OK`。最长正文 7390 UTF-8 字节，符合既有协议限额，没有截断。

本任务未修改、构建、安装或重启桥接与固件，未操作用户图库、设备及轮播设置，未提交或公开发布。主图库和年度选集哈希匹配开工基线。共享工作区中若出现其他任务的产物变化，已列入 `verification.json.externalChanges` 并关联已有部署文档；不能把共享文件整体未变作为本任务结果。

新增原文及说明对照当前字库源码发现 202 个未覆盖字符，逐字和逐件清单在 `font-coverage.json`。这只是源码覆盖检查，未验证已安装固件。图库包保留原文完整性，但导入测试通过不等于设备能显示每个新生僻字；本轮未扩字库或构建设备固件。

The packages include portable guides and source provenance. The existing production importer passed isolated imports and both-orientation readback; no bridge or firmware was rebuilt or deployed by this task. New rare-character coverage and real-device rendering remain separately documented limits.
