# 书画内容版次 / Artwork content editions

公开下载表同时显示内容版次和具体包版本，与软件中的版本号对应。“年度＋修订号”的内容版次：**2026.1** 表示这套 2026 年度内容在新编号规则下的第一个版次，后续内容修订为 2026.2、2026.3。书法和名画分别维护；“标准版／高清完整版”继续标明清晰度与容量，不混入版次数字。

2026.2 修订了两类精选的逐件导览、赏析和来源，并补充书法的可读正文、节录或参考。高清包含对应标准版的作品、展示图和资料，共用内容版次。

| 类别 | 内容版次 | 标准包内部版本 | 高清包内部版本 |
| --- | --- | --- | --- |
| 每日书法 | 2026.2 | 2026.11.12.1 | 2026.11.12.1 |
| 每日名画 | 2026.2 | 2026.11.12.1 | 2026.11.12.1 |

获取新版内容请从[图库下载表](GALLERY-PACKS.md)下载并导入对应包。包版本保留在文件名中，便于对照安装记录。

- **2026.2**　[名画、书法普通版与高清版](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.20)
- **2026.1**　[名画普通版](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.194-ui) · [书法普通版及两套高清版](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.205-ui)

以后修订作品、简介、原文或其他内容时，按实际类别递增内容版次；标准和高清内容发生分歧时分别登记对应版次。新增内部版本必须审查映射，不能仅从打包日期推断内容相同。未登记的版本保留原内部编号，避免误显示为旧版。映射由 `scripts/download_catalog.py` 维护并生成中英文下载表；更新日期另记在发布说明中。

## English

Public download tables show both the content edition and exact package version to match the application. Content editions use **year.revision**, starting with **2026.1** under this naming scheme. Painting and calligraphy are versioned separately. Standard and complete HD share a content edition when the HD archive includes the corresponding standard works, previews and guides; their archive bytes and hashes remain distinct. Edition 2026.2 updates guides, appreciation notes and sources for 366 works in each collection, including readable calligraphy text, excerpts or cited reference text where available. Standard and HD both use package version 2026.11.12.1. Download and import the corresponding package to get the revised content. Future content revisions require an explicit reviewed mapping; unknown package versions keep their original identifier rather than inheriting an old edition.
