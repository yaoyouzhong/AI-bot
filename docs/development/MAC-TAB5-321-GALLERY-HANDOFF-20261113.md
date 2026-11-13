# Mac 交接｜TAB5 321 图库批量增量同步

## 范围与接手入口

来源为 Windows 桥接和 TAB5，接收方为 macOS 桥接。拉取 `origin/codex/tab5-321-gallery-sync-20261113`，本文所在提交即交接源码。设备目标为 0.2.321-ui（内部 321-R4），Mac 桥接版本随后续候选确定。遵循[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)。

名画、书法按类别同步。电脑状态携带各自的版本摘要，TAB5 发现变化后发送 TF 上的逐件摘要。桥接比对后只打包变更详情和完整原文分页。同图、同顺序、同作品范围的更新复用 TF 原图。普通版和高清版的选择作用于电脑本地浏览，TAB5 使用设备适配 JPEG。

## 对齐清单

| 优先级 | 能力 | Windows / TAB5 参考 | Mac 入口与状态 |
| --- | --- | --- | --- |
| P0 | 目录协商、逐件回退 | [图库协议](../PROTOCOL.md#tab5-321-批量详情同步--batched-artwork-guides) | [MacTab5Gallery.swift](../../mac-app/Sources/AIBotBridge/MacTab5Gallery.swift) 已有版本通告、整库目录和逐件详情的本轮源码。待验证 macOS 编译、兼容目录与旧桥接行为 |
| P0 | 摘要上传与压缩包 | [Tab5GalleryGuideBatch.cs](../../windows-app/AIBotBridge/Tab5GalleryGuideBatch.cs) | Mac 补 `guideSummary`、`guideBatch`、`guideBatchRead`。完成后协商 `guideBatch:1`，沿用 zlib、边界校验和可用旧版备份回退 |
| P0 | 跨平台摘要 | [图库内容摘要](../../windows-app/AIBotBridge/GalleryPack.cs)、[图片摘要](../../windows-app/AIBotBridge/Tab5Gallery.cs) | Mac 当前目录摘要基于原始 PACK 字节及范围后缀，Windows 对清单对象和范围做序列化。用同一组输入核对版本摘要、图片摘要和逐件 v3 记录的实际字节。保留对现有 Mac/Windows TF 记录的读取和迁移能力 |
| P0 | 认证传输 | [Windows RPC](../../windows-app/AIBotBridge/Tab5Rpc.cs)、[Wi-Fi 数据流](../../windows-app/AIBotBridge/Tab5GalleryStream.cs) | [MacTab5Service.swift](../../mac-app/Sources/AIBotBridge/MacTab5Service.swift) 中仅放行认证图库的原始摘要上传。先验证加密分段，兼容 `T5S1` 后发布 `galleryStream:1` |
| P1 | USB 失败重试、ST7121 显示 | [321-R4 重试](TAB5-321-R4-GALLERY-RETRY-20261113.md)、[321-R3 显示](TAB5-321-R3-ST7121-CPU-COPY-20261113.md) | 固件已实现。Mac 配套验证强制 USB、自动模式和 Wi-Fi 接管。普通桌面消息继续走现有通道 |
| P1 | 普通版与高清版 | [Mac 画质设置](../../mac-app/Sources/AIBotBridge/MacGalleryManager.swift) | 已有，验收时切换同作品范围的桌面画质并确认设备图片及摘要兼容 |

Windows 逐件摘要当前直接对 UTF-8 v3 记录序列化结果计算 SHA-256。Mac 应先用共同测试数据核对空导读、空字段、多页原文、中文、非 BMP 字符、JSON 转义和字段顺序。仅以 Swift 和 .NET 解析结果语义一致判断兼容会导致相同详情被判为变化。旧摘要迁移方案与协议测试一起提交。

## 构建与验收

- 先在 macOS 上运行 `cd mac-app && swift test && swift build -c release`，补充真实旧包和新包的跨平台摘要比对、截断/坏哈希/超限拒绝，以及重复上传和跨链路继续。
- 使用标准全量图库 `2026.11.03.1 → 2026.11.12.1` 与同一台 TAB5。确认 11,198 张设备 JPEG 摘要一致，名画 539 件、书法 585 件变更，含 31 个额外原文分页。记录接收、写卡及目录提交、单类总耗时和 JPEG 下载字节。验证 Windows → Mac 和 Mac → Windows 切换、设备离线读取、两种方向与原文分页。
- Windows 现场 0.6.19 同基线测试桥接和 321-R2 实测两类合计收到 632,117 字节，JPEG 为 0 字节。名画接收 397 ms、TF 298 ms，书法接收 894 ms、TF 483 ms。名画首次 USB 未完成，成功批次走 Wi-Fi。R4 的重试在原生故障注入通过，真机核验了启动、显示设置和原目录保留。 [Windows 与 TAB5 实测](TAB5-321-R2-GALLERY-SCANOUT-20261113.md)
- 接收方记录 Mac 构建 SHA、附件 SHA-256、对应系统的自测及实际设备结果。当前交接没有 Mac 候选包或 Mac 真机结果，后续发布由对应发布任务授权。

第一步在 Mac 上跑现有图库测试并建立共享摘要测试数据，再接入批量压缩传输。
