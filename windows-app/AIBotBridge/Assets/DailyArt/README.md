> 已被 0.6.0 标准版与独立图库方案替代。当前范围及状态见 [图库说明](../../../../docs/GALLERY-PACKS.md)。
> Superseded by the unified standard edition and optional collection packs; historical details below.

2026-10-08本地资源已扩充为1765件名画、867件书法，共9744张横竖屏JPEG。第二批书法新增370件，扩充前7754张图片保持原字节。完整统计、逐件来源、可选图库包和验收边界见[本次扩充说明](../../../../docs/art-guides/EXPANSION-2026-10-08.md)；下文为旧方案的历史说明。

The current local resources contain 1,765 paintings and 867 calligraphy works, with 9,744 native JPEGs. The second batch adds 370 calligraphy works while preserving all 7,754 previous image bytes. See the expansion report above; the earlier local-only policy below is historical.

# Local-only DailyArt resources

每日名画与每日书法仅供本地使用，不发布图库 ZIP，也不包含在公开安装包或公开源码快照中。3,804 张 JPEG 留在本地并排除于 Git；catalog、来源、选集及 `files.json` 记录本地素材身份。

本地完整桥接使用 `dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release -p:LocalArt=true`；公开构建默认关闭此功能。保留现有图库目录与运行中的本地完整桥接。不要用公开版覆盖需要艺术屏保的本地安装。

Daily painting and calligraphy are local-only. Neither artwork ZIPs nor these resources are included in public packages/source snapshots. JPEGs stay local and outside Git; catalog/provenance/hash metadata records their identity. Local full builds opt in with `-p:LocalArt=true`; public builds disable the feature by default.
