# Local-only DailyArt resources

每日名画与每日书法仅供本地使用，不发布图库 ZIP，也不包含在公开安装包或公开源码快照中。3,804 张 JPEG 留在本地并排除于 Git；catalog、来源、选集及 `files.json` 记录本地素材身份。

本地完整桥接使用 `dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release -p:LocalArt=true`；公开构建默认关闭此功能。保留现有图库目录与运行中的本地完整桥接。不要用公开版覆盖需要艺术屏保的本地安装。

Daily painting and calligraphy are local-only. Neither artwork ZIPs nor these resources are included in public packages/source snapshots. JPEGs stay local and outside Git; catalog/provenance/hash metadata records their identity. Local full builds opt in with `-p:LocalArt=true`; public builds disable the feature by default.
